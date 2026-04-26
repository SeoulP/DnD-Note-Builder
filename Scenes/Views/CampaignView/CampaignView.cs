using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DndBuilder.Core.Models;
using Godot;

public partial class CampaignView : Control
{
    private int              _campaignId;
    private Campaign         _campaign;
    private DatabaseService  _db;
    private string           _currentPanel = "notes";
    private SystemVocabulary _vocab        = SystemVocabulary.Default;

    [Export] private Control _notesSidebarControl;
    [Export] private Control _systemSidebarControl;
    [Export] private Control _trackerSidebarControl;
    private NotesSidebar   _notesSidebar;
    private SystemSidebar  _systemSidebar;
    private TrackerSidebar _trackerSidebar;

    [Export] private Control         _detailPanel;
    [Export] private Control         _paneContainer;
    [Export] private HBoxContainer   _tabList;
    [Export] private ScrollContainer _tabScroll;
    [Export] private Button          _backButton;
    [Export] private Button          _forwardButton;
    private Control      _addTabWidget;
    private StyleBoxFlat _addTabInactiveSb;
    private StyleBoxFlat _addTabHoverSb;
    private StyleBoxFlat _addTabActiveSb;

    private StyleBoxFlat _backNormalSb,  _backHoverSb,  _backPressedSb,  _backDisabledSb;
    private StyleBoxFlat _fwdNormalSb,   _fwdHoverSb,   _fwdPressedSb,   _fwdDisabledSb;

    private TabEntry _dragTab;
    private bool     _dragging;
    private float    _dragStartX;
    private float    _dragGrabOffset;
    private Control  _dragGhost;
    private const float DragThreshold = 6f;

    [Export] private PackedScene _npcDetailPaneScene;
    [Export] private PackedScene _factionDetailPaneScene;
    [Export] private PackedScene _locationDetailPaneScene;
    [Export] private PackedScene _sessionDetailPaneScene;
    [Export] private PackedScene _itemDetailPaneScene;
    [Export] private PackedScene _questDetailPaneScene;
    [Export] private PackedScene _abilityDetailPaneScene;
    [Export] private PackedScene _classDetailPaneScene;
    [Export] private PackedScene _subclassDetailPaneScene;
    [Export] private PackedScene _speciesDetailPaneScene;
    [Export] private PackedScene _subspeciesDetailPaneScene;
    [Export] private PackedScene _playerCharacterDetailPaneScene;
    [Export] private PackedScene _pf2eCharacterDetailPaneScene;
    [Export] private PackedScene _pf2eCreatureDetailPaneScene;
    [Export] private PackedScene _pf2eEncounterDetailPaneScene;

    [Signal] public delegate void SidebarPanelChangedEventHandler(string panel);

    private static bool IsSystemEntity(string et) =>
        et is "class" or "subclass" or "species" or "subspecies" or "ability"
           or "pf2e_creature" or "pf2e_class" or "pf2e_ancestry";

    private static bool IsTrackerEntity(string et) => et is "encounter";

    private sealed class TabEntry
    {
        public string       EntityType = "";
        public int          EntityId;
        public string       Panel      = "notes";
        public string       Label      = "";
        public bool         IsPinned;
        public Control      Pane;
        public Control      Widget;
        public Label        NameLabel;
        public Button       ActionBtn;
        public StyleBoxFlat ActiveSb;
        public StyleBoxFlat InactiveSb;
        public StyleBoxFlat HoverSb;
        public StyleBoxFlat DeleteSb;
        public TabHistory   History    = new();
    }

    private readonly List<TabEntry> _tabs      = new();
    private int                     _activeTab = -1;

    private static Color TabActiveBg   => ThemeManager.Instance.Current.Hover;
    private static Color TabInactiveBg => ThemeManager.Instance.Current.NavBar;
    private static Color TabHoverBg    => ThemeManager.Instance.Current.Component;

    private const float SidebarMaxWidth   = 250f;
    private const float DetailPadding     = 8f;
    private const float DetailFooterPadding = 24f;

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            ApplySidebarWidth();
    }

    private void ApplySidebarWidth()
    {
        float width = Mathf.Min(Size.X * 0.25f, SidebarMaxWidth);
        GetNode<Control>("ScrollContainer").OffsetRight = width;
        _detailPanel.OffsetLeft   =  width + DetailPadding;
        _detailPanel.OffsetRight  = -DetailPadding;
        _detailPanel.OffsetBottom = -DetailFooterPadding;
    }

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _notesSidebar  = _notesSidebarControl  as NotesSidebar;
        _systemSidebar = _systemSidebarControl as SystemSidebar;
        _trackerSidebar = _trackerSidebarControl as TrackerSidebar;

        if (_campaignId > 0)
        {
            _campaign = _db.Campaigns.Get(_campaignId);
            _vocab    = SystemVocabulary.For(_campaign?.System);
            _db.MigrateLegacyImagePaths(_campaignId);
            _notesSidebar?.SetCampaign(_campaignId, _campaign, _vocab);
            _systemSidebar?.SetCampaign(_campaignId, _campaign, _vocab);
            _trackerSidebar?.SetCampaign(_campaignId, _campaign, _vocab);
        }

        if (_notesSidebar != null)
        {
            _notesSidebar.EntitySelected       += ShowDetailPane;
            _notesSidebar.EntitySelectedNewTab += ShowDetailPaneInNewTab;
        }
        if (_systemSidebar != null)
        {
            _systemSidebar.EntitySelected       += ShowDetailPane;
            _systemSidebar.EntitySelectedNewTab += ShowDetailPaneInNewTab;
        }
        if (_trackerSidebar != null)
        {
            _trackerSidebar.EntitySelected       += ShowDetailPane;
            _trackerSidebar.EntitySelectedNewTab += ShowDetailPaneInNewTab;
        }

        ApplySidebarWidth();

        if (_backButton != null && _forwardButton != null)
            InitNavButtonStyles();

        if (_backButton != null)
            _backButton.Pressed += () =>
            {
                if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
                var tab = _tabs[_activeTab];
                if (!tab.History.CanGoBack) return;
                var (type, id) = tab.History.Back();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
            };
        if (_forwardButton != null)
            _forwardButton.Pressed += () =>
            {
                if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
                var tab = _tabs[_activeTab];
                if (!tab.History.CanGoForward) return;
                var (type, id) = tab.History.Forward();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
            };

        _addTabWidget = BuildAddTabWidget();
        ThemeManager.Instance.ThemeChanged += OnTabThemeChanged;

        if (_campaignId > 0) RestoreTabs();
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
        if (_db != null)
        {
            _campaign = _db.Campaigns.Get(campaignId);
            _vocab    = SystemVocabulary.For(_campaign?.System);
        }
        foreach (var tab in _tabs) { tab.Pane?.QueueFree(); tab.Widget?.QueueFree(); }
        _tabs.Clear();
        _activeTab = -1;
        RefreshNavButtons();
    }

    public void ReloadSidebar()
    {
        _notesSidebar?.ReloadAll();
        _systemSidebar?.ReloadAll();
        _trackerSidebar?.ReloadAll();
    }

    // ── detail pane ───────────────────────────────────────────────────────────

    private void ShowDetailPane(string entityType, int entityId)
    {
        int existing = _tabs.FindIndex(t => t.EntityType == entityType && t.EntityId == entityId);
        if (existing >= 0) { ActivateTab(existing); return; }
        if (_activeTab >= 0 && _activeTab < _tabs.Count && !_tabs[_activeTab].IsPinned)
        { LoadIntoTab(_activeTab, entityType, entityId); return; }
        OpenNewTab(entityType, entityId);
    }

    private void ShowDetailPaneInNewTab(string entityType, int entityId)
    {
        int existing = _tabs.FindIndex(t => t.EntityType == entityType && t.EntityId == entityId);
        if (existing >= 0) { ActivateTab(existing); return; }
        OpenNewTab(entityType, entityId);
    }

    private void ShowDetailPaneNewTabBackground(string entityType, int entityId)
    {
        int existing = _tabs.FindIndex(t => t.EntityType == entityType && t.EntityId == entityId);
        if (existing >= 0) { ActivateTab(existing); return; }
        OpenNewTabBackground(entityType, entityId);
    }

    private (Control Pane, string Label, Action Load) InstantiatePane(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "npc":
            {
                var e = _db.Npcs.Get(entityId); if (e == null) return (null, null, null);
                var p = _npcDetailPaneScene.Instantiate<NpcDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New NPC" : e.Name, () => p.Load(e));
            }
            case "faction":
            {
                var e = _db.Factions.Get(entityId); if (e == null) return (null, null, null);
                var p = _factionDetailPaneScene.Instantiate<FactionDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Faction" : e.Name, () => p.Load(e));
            }
            case "location":
            {
                var e = _db.Locations.Get(entityId); if (e == null) return (null, null, null);
                var p = _locationDetailPaneScene.Instantiate<LocationDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubLocationAdded      += (_, __) => _notesSidebar?.Reload("location");
                p.ParentLocationChanged += _       => _notesSidebar?.Reload("location");
                p.EntityCreated         += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Location" : e.Name, () => p.Load(e));
            }
            case "session":
            {
                var e = _db.Sessions.Get(entityId); if (e == null) return (null, null, null);
                var p = _sessionDetailPaneScene.Instantiate<SessionDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Title) ? "Untitled Session" : e.Title, () => p.Load(e));
            }
            case "item":
            {
                var e = _db.Items.Get(entityId); if (e == null) return (null, null, null);
                var p = _itemDetailPaneScene.Instantiate<ItemDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Item" : e.Name, () => p.Load(e));
            }
            case "quest":
            {
                var e = _db.Quests.Get(entityId); if (e == null) return (null, null, null);
                var p = _questDetailPaneScene.Instantiate<QuestDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { _notesSidebar?.Reload(type); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Quest" : e.Name, () => p.Load(e));
            }
            case "ability":
            {
                var e = _db.Abilities.Get(entityId); if (e == null) return (null, null, null);
                var p = _abilityDetailPaneScene.Instantiate<AbilityDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Ability}" : e.Name, () => p.Load(e));
            }
            case "class":
            {
                var e = _db.Classes.Get(entityId); if (e == null) return (null, null, null);
                var p = _classDetailPaneScene.Instantiate<ClassDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubclassAdded += (_, __) => _systemSidebar?.Reload("subclass");
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Class}" : e.Name, () => p.Load(e));
            }
            case "subclass":
            {
                var e = _db.Classes.GetSubclass(entityId); if (e == null) return (null, null, null);
                var p = _subclassDetailPaneScene.Instantiate<SubclassDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Subclass}" : e.Name, () => p.Load(e));
            }
            case "species":
            {
                var e = _db.Species.Get(entityId); if (e == null) return (null, null, null);
                var p = _speciesDetailPaneScene.Instantiate<SpeciesDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubspeciesAdded += (_, __) => _systemSidebar?.Reload("subspecies");
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Species}" : e.Name, () => p.Load(e));
            }
            case "subspecies":
            {
                var e = _db.Subspecies.Get(entityId); if (e == null) return (null, null, null);
                var p = _subspeciesDetailPaneScene.Instantiate<SubspeciesDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Subspecies}" : e.Name, () => p.Load(e));
            }
            case "playercharacter":
            {
                var e = _db.PlayerCharacters.Get(entityId); if (e == null) return (null, null, null);
                var p = _playerCharacterDetailPaneScene.Instantiate<PlayerCharacterDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? "New Character" : e.Name, () => p.Load(e));
            }
            case "pf2e_pc":
            {
                var e = _db.Pf2eCharacters.Get(entityId); if (e == null) return (null, null, null);
                var p = _pf2eCharacterDetailPaneScene.Instantiate<Pf2eCharacterDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? "New Character" : e.Name, () => p.Load(e));
            }
            case "pf2e_class":
            {
                var e = _db.Pf2eClasses.Get(entityId); if (e == null) return (null, null, null);
                var label = new Label { Text = $"PF2e Class: {e.Name}\n(detail pane coming soon)", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill };
                return (label, string.IsNullOrEmpty(e.Name) ? "New Class" : e.Name, null);
            }
            case "pf2e_ancestry":
            {
                var e = _db.Pf2eAncestries.Get(entityId); if (e == null) return (null, null, null);
                var label = new Label { Text = $"PF2e Ancestry: {e.Name}\n(detail pane coming soon)", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill };
                return (label, string.IsNullOrEmpty(e.Name) ? "New Ancestry" : e.Name, null);
            }
            case "pf2e_creature":
            {
                var e = _db.Pf2eCreatures.Get(entityId); if (e == null) return (null, null, null);
                var p = _pf2eCreatureDetailPaneScene.Instantiate<Pf2eCreatureDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NavigateToNewTab += ShowDetailPaneInNewTab; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? "New Creature" : e.Name, () => p.Load(e));
            }
            case "encounter":
            {
                var enc = _db.Encounters.Get(entityId); if (enc == null) return (null, null, null);
                if (_campaign?.System == "pathfinder2e" && _pf2eEncounterDetailPaneScene != null)
                {
                    var p = _pf2eEncounterDetailPaneScene.Instantiate<Pf2eEncounterDetailPane>();
                    p.EncounterUpdated += () => _trackerSidebar?.Reload("encounter");
                    p.Deleted          += OnEntityDeleted;
                    p.NavigateTo       += ShowDetailPaneInNewTab;
                    return (p, string.IsNullOrEmpty(enc.Name) ? "New Encounter" : enc.Name, () => p.Load(enc));
                }
                var lbl = new Label { Text = "Battle Tracker — coming soon", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill };
                return (lbl, enc.Name, null);
            }
            default:
                return (null, null, null);
        }
    }

    private void OnEntityDeleted(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "playercharacter": _db.PlayerCharacters.Delete(entityId); break;
            case "pf2e_pc":         _db.Pf2eCharacters.Delete(entityId);  break;
            case "pf2e_class":      _db.Pf2eClasses.Delete(entityId);     break;
            case "pf2e_ancestry":   _db.Pf2eAncestries.Delete(entityId);  break;
            case "pf2e_creature":   _db.Pf2eCreatures.Delete(entityId);   break;
            case "encounter":       _db.Encounters.Delete(entityId);      break;
            case "npc":        _db.Npcs.Delete(entityId);             break;
            case "faction":    _db.Factions.Delete(entityId);         break;
            case "location":   _db.Locations.Delete(entityId);        break;
            case "session":    _db.Sessions.Delete(entityId);         break;
            case "item":       _db.Items.Delete(entityId);            break;
            case "quest":      _db.Quests.Delete(entityId);           break;
            case "ability":    _db.Abilities.Delete(entityId);        break;
            case "class":      _db.Classes.Delete(entityId);          break;
            case "subclass":   _db.Classes.DeleteSubclass(entityId);  break;
            case "species":    _db.Species.Delete(entityId);          break;
            case "subspecies": _db.Subspecies.Delete(entityId);       break;
        }
        _notesSidebar?.Reload(entityType);
        _systemSidebar?.Reload(entityType);
        _trackerSidebar?.Reload(entityType);
        for (int i = _tabs.Count - 1; i >= 0; i--)
            if (_tabs[i].EntityType == entityType && _tabs[i].EntityId == entityId)
                CloseTab(i);
    }

    private void OnNameChanged(string entityType, int entityId, string displayText)
    {
        _notesSidebar?.UpdateButtonName(entityType, entityId, displayText);
        _systemSidebar?.UpdateButtonName(entityType, entityId, displayText);
        _trackerSidebar?.UpdateButtonName(entityType, entityId, displayText);
        foreach (var tab in _tabs)
        {
            if (tab.EntityType == entityType && tab.EntityId == entityId)
            {
                tab.Label = displayText;
                if (tab.NameLabel != null && IsInstanceValid(tab.NameLabel))
                    tab.NameLabel.Text = displayText;
            }
        }
    }

    // ── tab management ────────────────────────────────────────────────────────

    private void LoadIntoTab(int index, string entityType, int entityId, bool pushHistory = true)
    {
        var tab = _tabs[index];
        if (tab.Pane != null && IsInstanceValid(tab.Pane)) { tab.Pane.Visible = false; tab.Pane.QueueFree(); }
        var (pane, label, load) = InstantiatePane(entityType, entityId);
        if (pane == null) return;
        tab.EntityType = entityType;
        tab.EntityId   = entityId;
        tab.Panel      = IsSystemEntity(entityType) ? "system"
                       : IsTrackerEntity(entityType) ? "tracker"
                       : "notes";
        tab.Label      = label;
        tab.Pane       = pane;
        _paneContainer.AddChild(pane);
        load?.Invoke();
        if (tab.NameLabel != null && IsInstanceValid(tab.NameLabel))
            tab.NameLabel.Text = label;
        UpdateTabSwatch(tab);
        if (pushHistory) tab.History.Push(entityType, entityId);
        ActivateTab(index);
    }

    private void OpenNewTab(string entityType, int entityId)
    {
        var (pane, label, load) = InstantiatePane(entityType, entityId);
        if (pane == null) return;
        var tab = new TabEntry { EntityType = entityType, EntityId = entityId, Panel = IsSystemEntity(entityType) ? "system" : IsTrackerEntity(entityType) ? "tracker" : "notes", Label = label, Pane = pane };
        tab.History.Push(entityType, entityId);
        _paneContainer.AddChild(pane);
        load?.Invoke();
        _tabs.Add(tab);
        BuildTabWidget(tab);
        ActivateTab(_tabs.Count - 1);
    }

    private void OpenNewTabBackground(string entityType, int entityId)
    {
        var (pane, label, load) = InstantiatePane(entityType, entityId);
        if (pane == null) return;
        var tab = new TabEntry { EntityType = entityType, EntityId = entityId, Panel = IsSystemEntity(entityType) ? "system" : IsTrackerEntity(entityType) ? "tracker" : "notes", Label = label, Pane = pane };
        tab.History.Push(entityType, entityId);
        pane.Visible = false;
        _paneContainer.AddChild(pane);
        load?.Invoke();
        _tabs.Add(tab);
        BuildTabWidget(tab);
        SaveTabs();
    }

    private void OpenEmptyTab()
    {
        string panel = _currentPanel;
        string label = panel == "system" ? "System New Tab" : panel == "tracker" ? "Tracker New Tab" : "Notes New Tab";
        var placeholder = new Control();
        var tab = new TabEntry { Label = label, Panel = panel, Pane = placeholder };
        _paneContainer.AddChild(placeholder);
        _tabs.Add(tab);
        BuildTabWidget(tab);
        ActivateTab(_tabs.Count - 1);
    }

    private void ActivateTab(int index)
    {
        _activeTab = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool active = i == index;
            if (_tabs[i].Pane != null && IsInstanceValid(_tabs[i].Pane))
                _tabs[i].Pane.Visible = active;
            var w = _tabs[i].Widget;
            if (w != null && IsInstanceValid(w))
                w.AddThemeStyleboxOverride("panel", active ? _tabs[i].ActiveSb : _tabs[i].InactiveSb);
        }

        if (_activeTab >= 0 && _activeTab < _tabs.Count)
        {
            string mode = _tabs[_activeTab].Panel;
            if (mode != _currentPanel) ApplySidebarPanel(mode);
        }

        CallDeferred(nameof(DoScrollToActiveTab));
        RefreshNavButtons();
        SaveTabs();
    }

    private void RefreshNavButtons()
    {
        if (_backButton == null || _forwardButton == null) return;
        if (_activeTab < 0 || _activeTab >= _tabs.Count)
        {
            _backButton.Disabled = _forwardButton.Disabled = true;
            return;
        }
        var h = _tabs[_activeTab].History;
        _backButton.Disabled    = !h.CanGoBack;
        _forwardButton.Disabled = !h.CanGoForward;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
        var tab = _tabs[_activeTab];

        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.AltPressed && key.Keycode == Key.Left && tab.History.CanGoBack)
            {
                var (type, id) = tab.History.Back();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
                AcceptEvent();
            }
            else if (key.AltPressed && key.Keycode == Key.Right && tab.History.CanGoForward)
            {
                var (type, id) = tab.History.Forward();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
                AcceptEvent();
            }
        }
    }

    private void CloseTab(int index)
    {
        var tab = _tabs[index];
        if (tab.Pane   != null && IsInstanceValid(tab.Pane))   tab.Pane.QueueFree();
        if (tab.Widget != null && IsInstanceValid(tab.Widget)) tab.Widget.QueueFree();
        _tabs.RemoveAt(index);
        if (_tabs.Count == 0) { _activeTab = -1; SaveTabs(); return; }
        int next = Mathf.Clamp(index < _tabs.Count ? index : _tabs.Count - 1, 0, _tabs.Count - 1);
        ActivateTab(next);
    }

    private void SaveTabs()
    {
        if (_db.Settings.Get("remember_tabs", "false") != "true") return;
        var entries = _tabs
            .Where(t => !string.IsNullOrEmpty(t.EntityType))
            .Select(t => new { type = t.EntityType, id = t.EntityId, pinned = t.IsPinned })
            .ToList();
        var state = new { active = _activeTab, tabs = entries };
        _db.Settings.Set($"tabs_{_campaignId}", JsonSerializer.Serialize(state));
    }

    private void RestoreTabs()
    {
        if (_db.Settings.Get("remember_tabs", "false") != "true") return;
        var json = _db.Settings.Get($"tabs_{_campaignId}", "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            using var doc   = JsonDocument.Parse(json);
            var root        = doc.RootElement;
            int savedActive = root.TryGetProperty("active", out var ap) ? ap.GetInt32() : 0;
            var tabs        = root.TryGetProperty("tabs",   out var tp) ? tp : default;
            if (tabs.ValueKind != JsonValueKind.Array) return;

            int restored = 0;
            foreach (var entry in tabs.EnumerateArray())
            {
                string type = entry.GetProperty("type").GetString() ?? "";
                int    id   = entry.GetProperty("id").GetInt32();
                bool pinned = entry.TryGetProperty("pinned", out var pp) && pp.GetBoolean();
                var (pane, label, load) = InstantiatePane(type, id);
                if (pane == null) continue;
                var tab = new TabEntry { EntityType = type, EntityId = id, Panel = IsSystemEntity(type) ? "system" : IsTrackerEntity(type) ? "tracker" : "notes", Label = label, Pane = pane, IsPinned = pinned };
                tab.History.Push(type, id);
                _paneContainer.AddChild(pane);
                load?.Invoke();
                _tabs.Add(tab);
                BuildTabWidget(tab);
                restored++;
            }

            int activeIdx = Mathf.Clamp(savedActive, 0, _tabs.Count - 1);
            if (_tabs.Count > 0) ActivateTab(activeIdx);
        }
        catch (Exception ex) { AppLogger.Instance.Debug("TabHistory", $"Corrupt saved tab state — ignored: {ex.Message}"); }
    }

    private Control BuildAddTabWidget()
    {
        var inactiveSb = MakeTabBox(TabInactiveBg);
        var hoverSb    = MakeTabBox(TabHoverBg);
        var activeSb   = MakeTabBox(TabActiveBg);
        _addTabInactiveSb = inactiveSb;
        _addTabHoverSb    = hoverSb;
        _addTabActiveSb   = activeSb;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(36, 0), MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", inactiveSb);

        var label = new Label
        {
            Text                = "+",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        panel.AddChild(label);

        panel.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", hoverSb);
        panel.MouseExited  += () => panel.AddThemeStyleboxOverride("panel", inactiveSb);
        panel.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                panel.AddThemeStyleboxOverride("panel", activeSb);
                OpenEmptyTab();
            }
        };

        _tabList.AddChild(panel);
        return panel;
    }

    private static void ApplyActionBtnState(TabEntry tab, bool hovering)
    {
        var btn = tab.ActionBtn;
        if (btn == null) return;
        if (tab.IsPinned)
        {
            btn.Text        = "◆";
            btn.Modulate    = Colors.White;
            btn.TooltipText = "Pinned — unpin to close\nShift+middle-click to force close";
        }
        else
        {
            btn.Text        = "×";
            btn.Modulate    = hovering ? Colors.White : Colors.Transparent;
            btn.TooltipText = "";
        }
    }

    private void BuildTabWidget(TabEntry tab)
    {
        var activeSb   = MakeTabBox(TabActiveBg);
        var inactiveSb = MakeTabBox(TabInactiveBg);
        var hoverSb    = MakeTabBox(TabHoverBg);
        var deleteSb   = MakeTabBox(ThemeManager.DeleteHoverColor);
        tab.ActiveSb   = activeSb;
        tab.InactiveSb = inactiveSb;
        tab.HoverSb    = hoverSb;
        tab.DeleteSb   = deleteSb;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(200, 0), MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", inactiveSb);
        tab.Widget = panel;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 3);

        var nameLabel = new Label
        {
            Text                = tab.Label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Off,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        tab.NameLabel = nameLabel;

        var actionBtn = new Button { Flat = true, FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(18, 0) };
        actionBtn.AddThemeFontSizeOverride("font_size", 12);
        tab.ActionBtn = actionBtn;
        ApplyActionBtnState(tab, hovering: false);
        actionBtn.Pressed += () =>
        {
            if (tab.IsPinned)
            {
                tab.IsPinned = false;
                ApplyActionBtnState(tab, hovering: true);
            }
            else
            {
                int i = _tabs.IndexOf(tab); if (i >= 0) CloseTab(i);
            }
        };

        hbox.AddChild(nameLabel);
        hbox.AddChild(actionBtn);

        var swatch = new Panel { CustomMinimumSize = new Vector2(0, 4), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        swatch.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = GetEntityColor(tab.EntityType) });
        tab.Widget.SetMeta("swatch", swatch);

        vbox.AddChild(hbox);
        vbox.AddChild(swatch);
        panel.AddChild(vbox);

        panel.MouseEntered += () =>
        {
            int i = _tabs.IndexOf(tab);
            panel.AddThemeStyleboxOverride("panel", i == _activeTab ? activeSb : hoverSb);
            ApplyActionBtnState(tab, hovering: true);
        };
        panel.MouseExited += () =>
        {
            int i = _tabs.IndexOf(tab);
            panel.AddThemeStyleboxOverride("panel", i == _activeTab ? activeSb : inactiveSb);
            ApplyActionBtnState(tab, hovering: false);
        };
        actionBtn.MouseEntered += () =>
        {
            ApplyActionBtnState(tab, hovering: true);
            if (!tab.IsPinned) panel.AddThemeStyleboxOverride("panel", deleteSb);
        };
        actionBtn.MouseExited += () =>
        {
            int i = _tabs.IndexOf(tab);
            panel.AddThemeStyleboxOverride("panel", i == _activeTab ? activeSb : hoverSb);
            ApplyActionBtnState(tab, hovering: true);
        };

        var ctxMenu = new PopupMenu();
        panel.AddChild(ctxMenu);
        ctxMenu.IdPressed += (long id) =>
        {
            int i = _tabs.IndexOf(tab);
            switch (id)
            {
                case 0:
                    tab.IsPinned = !tab.IsPinned;
                    ApplyActionBtnState(tab, hovering: false);
                    break;
                case 1: if (i >= 0) CloseTab(i); break;
                case 2:
                    for (int j = _tabs.Count - 1; j >= 0; j--)
                        if (j != i && !_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 3:
                    for (int j = i - 1; j >= 0; j--)
                        if (!_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 4:
                    for (int j = _tabs.Count - 1; j > i; j--)
                        if (!_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 5:
                    for (int j = _tabs.Count - 1; j >= 0; j--)
                        if (!_tabs[j].IsPinned) CloseTab(j);
                    break;
            }
        };

        panel.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    int i = _tabs.IndexOf(tab); if (i >= 0) ActivateTab(i);
                    _dragTab        = tab;
                    _dragStartX     = mb.GlobalPosition.X;
                    _dragGrabOffset = mb.GlobalPosition.X - tab.Widget.GetGlobalRect().Position.X;
                    _dragging       = false;
                }
                else if (mb.ButtonIndex == MouseButton.Middle && mb.Pressed)
                    { if (!tab.IsPinned || mb.ShiftPressed) { int i = _tabs.IndexOf(tab); if (i >= 0) CloseTab(i); } }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    int i = _tabs.IndexOf(tab);
                    ctxMenu.Clear();
                    ctxMenu.AddItem(tab.IsPinned ? "Unpin" : "Pin", 0);
                    ctxMenu.AddSeparator();
                    ctxMenu.AddItem("Close",          1); ctxMenu.SetItemDisabled(ctxMenu.ItemCount - 1, tab.IsPinned);
                    ctxMenu.AddItem("Close Others",   2); ctxMenu.SetItemDisabled(ctxMenu.ItemCount - 1, _tabs.Count <= 1);
                    ctxMenu.AddItem("Close to Left",  3); ctxMenu.SetItemDisabled(ctxMenu.ItemCount - 1, i <= 0);
                    ctxMenu.AddItem("Close to Right", 4); ctxMenu.SetItemDisabled(ctxMenu.ItemCount - 1, i >= _tabs.Count - 1);
                    ctxMenu.AddItem("Close All",      5);
                    ctxMenu.Popup(new Rect2I((Vector2I)mb.GlobalPosition, Vector2I.Zero));
                }
            }
        };

        int insertAt = _addTabWidget.GetIndex();
        _tabList.AddChild(panel);
        _tabList.MoveChild(panel, insertAt);
    }

    private void UpdateTabSwatch(TabEntry tab)
    {
        if (tab.Widget == null || !IsInstanceValid(tab.Widget)) return;
        if (!tab.Widget.HasMeta("swatch")) return;
        var swatch = tab.Widget.GetMeta("swatch").As<Panel>();
        if (swatch != null && IsInstanceValid(swatch))
            swatch.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = GetEntityColor(tab.EntityType) });
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb && _activeTab >= 0 && _activeTab < _tabs.Count)
        {
            var navTab = _tabs[_activeTab];
            if (mb.ButtonIndex == MouseButton.Xbutton1 && navTab.History.CanGoBack)
            {
                var (type, id) = navTab.History.Back();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
                AcceptEvent();
                return;
            }
            if (mb.ButtonIndex == MouseButton.Xbutton2 && navTab.History.CanGoForward)
            {
                var (type, id) = navTab.History.Forward();
                LoadIntoTab(_activeTab, type, id, pushHistory: false);
                AcceptEvent();
                return;
            }
        }

        if (_dragTab == null) return;

        if (e is InputEventMouseMotion motion)
        {
            if (!_dragging && Mathf.Abs(motion.GlobalPosition.X - _dragStartX) > DragThreshold)
            {
                _dragging = true;
                _dragTab.Widget.Modulate = new Color(1, 1, 1, 0.35f);
                _dragGhost = BuildDragGhost();
            }
            if (_dragging)
            {
                if (_dragGhost != null)
                    _dragGhost.Position = new Vector2(
                        motion.GlobalPosition.X - _dragGrabOffset,
                        _dragTab.Widget.GetGlobalRect().Position.Y);
                UpdateDragOrder(motion.GlobalPosition.X);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            if (_dragTab.Widget != null && IsInstanceValid(_dragTab.Widget))
                _dragTab.Widget.Modulate = Colors.White;
            _dragGhost?.QueueFree();
            _dragGhost = null;
            _dragTab   = null;
            _dragging  = false;
        }
    }

    private void UpdateDragOrder(float globalX)
    {
        int fromIdx = _tabs.IndexOf(_dragTab);
        if (fromIdx < 0) return;
        int toIdx = fromIdx;
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (i == fromIdx) continue;
            var w = _tabs[i].Widget;
            if (w == null || !IsInstanceValid(w)) continue;
            if (w.GetGlobalRect().HasPoint(new Vector2(globalX, w.GetGlobalRect().Position.Y)))
            {
                toIdx = i;
                break;
            }
        }
        if (toIdx == fromIdx) return;
        _tabs.RemoveAt(fromIdx);
        _tabs.Insert(toIdx, _dragTab);
        _tabList.MoveChild(_dragTab.Widget, toIdx);
        if      (_activeTab == fromIdx)                        _activeTab = toIdx;
        else if (fromIdx < _activeTab && toIdx >= _activeTab) _activeTab--;
        else if (fromIdx > _activeTab && toIdx <= _activeTab) _activeTab++;
    }

    private Control BuildDragGhost()
    {
        var tabRect = _dragTab.Widget.GetGlobalRect();
        var ghost = new PanelContainer
        {
            MouseFilter       = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(tabRect.Size.X, 0),
            Position          = tabRect.Position,
            ZIndex            = 100,
            Modulate          = new Color(1, 1, 1, 0.80f),
        };
        ghost.AddThemeStyleboxOverride("panel", MakeTabBox(TabActiveBg));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 3);

        var label = new Label
        {
            Text                = _dragTab.Label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Off,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        hbox.AddChild(label);

        var swatch = new Panel { CustomMinimumSize = new Vector2(0, 4), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        swatch.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = GetEntityColor(_dragTab.EntityType) });

        vbox.AddChild(hbox);
        vbox.AddChild(swatch);
        ghost.AddChild(vbox);

        GetTree().Root.AddChild(ghost);
        return ghost;
    }

    private void OnTabThemeChanged()
    {
        _addTabInactiveSb.BgColor = TabInactiveBg;
        _addTabHoverSb.BgColor    = TabHoverBg;
        _addTabActiveSb.BgColor   = TabActiveBg;
        foreach (var tab in _tabs)
        {
            tab.ActiveSb.BgColor   = TabActiveBg;
            tab.InactiveSb.BgColor = TabInactiveBg;
            tab.HoverSb.BgColor    = TabHoverBg;
        }

        if (_backNormalSb == null) return;
        var p     = ThemeManager.Instance.Current;
        var dimBg = p.NavBar; dimBg.A *= 0.4f;

        _backNormalSb.BgColor   = p.NavBar;    _fwdNormalSb.BgColor   = p.NavBar;
        _backHoverSb.BgColor    = p.Component; _fwdHoverSb.BgColor    = p.Component;
        _backPressedSb.BgColor  = p.Hover;     _fwdPressedSb.BgColor  = p.Hover;
        _backDisabledSb.BgColor = dimBg;        _fwdDisabledSb.BgColor = dimBg;
    }

    private void InitNavButtonStyles()
    {
        var p     = ThemeManager.Instance.Current;
        var dimBg = p.NavBar; dimBg.A *= 0.4f;

        _backNormalSb   = MakeNavBtnBox(p.NavBar,    roundLeft: true,  roundRight: false);
        _backHoverSb    = MakeNavBtnBox(p.Component, roundLeft: true,  roundRight: false);
        _backPressedSb  = MakeNavBtnBox(p.Hover,     roundLeft: true,  roundRight: false);
        _backDisabledSb = MakeNavBtnBox(dimBg,        roundLeft: true,  roundRight: false);
        _fwdNormalSb    = MakeNavBtnBox(p.NavBar,    roundLeft: false, roundRight: true);
        _fwdHoverSb     = MakeNavBtnBox(p.Component, roundLeft: false, roundRight: true);
        _fwdPressedSb   = MakeNavBtnBox(p.Hover,     roundLeft: false, roundRight: true);
        _fwdDisabledSb  = MakeNavBtnBox(dimBg,        roundLeft: false, roundRight: true);

        _backButton.Flat        = false;
        _backButton.TooltipText = "Go back (Alt+←)";
        _backButton.AddThemeStyleboxOverride("normal",   _backNormalSb);
        _backButton.AddThemeStyleboxOverride("hover",    _backHoverSb);
        _backButton.AddThemeStyleboxOverride("pressed",  _backPressedSb);
        _backButton.AddThemeStyleboxOverride("disabled", _backDisabledSb);
        _backButton.AddThemeStyleboxOverride("focus",    _backNormalSb);

        _forwardButton.Flat        = false;
        _forwardButton.TooltipText = "Go forward (Alt+→)";
        _forwardButton.AddThemeStyleboxOverride("normal",   _fwdNormalSb);
        _forwardButton.AddThemeStyleboxOverride("hover",    _fwdHoverSb);
        _forwardButton.AddThemeStyleboxOverride("pressed",  _fwdPressedSb);
        _forwardButton.AddThemeStyleboxOverride("disabled", _fwdDisabledSb);
        _forwardButton.AddThemeStyleboxOverride("focus",    _fwdNormalSb);
    }

    // ── sidebar panel switching ───────────────────────────────────────────────

    public void SetSidebarPanel(string panel)
    {
        ApplySidebarPanel(panel);
        if (_activeTab >= 0 && _activeTab < _tabs.Count && _tabs[_activeTab].Panel == panel) return;
        int matchIdx = -1;
        for (int i = _tabs.Count - 1; i >= 0; i--)
            if (_tabs[i].Panel == panel) { matchIdx = i; break; }
        if (matchIdx >= 0) ActivateTab(matchIdx);
        else OpenEmptyTab();
    }

    private void ApplySidebarPanel(string panel)
    {
        _currentPanel = panel;
        if (_notesSidebarControl  != null) _notesSidebarControl.Visible  = (panel == "notes");
        if (_systemSidebarControl != null) _systemSidebarControl.Visible = (panel == "system");
        if (_trackerSidebarControl != null) _trackerSidebarControl.Visible = (panel == "tracker");
        EmitSignal(SignalName.SidebarPanelChanged, panel);
    }

    // ── style helpers ─────────────────────────────────────────────────────────

    private static StyleBoxFlat MakeNavBtnBox(Color bg, bool roundLeft, bool roundRight)
    {
        const int r = 4;
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = Colors.Transparent, DrawCenter = true };
        sb.BorderWidthTop = sb.BorderWidthBottom = sb.BorderWidthLeft = sb.BorderWidthRight = 0;
        sb.CornerRadiusTopLeft     = sb.CornerRadiusBottomLeft     = roundLeft  ? r : 0;
        sb.CornerRadiusTopRight    = sb.CornerRadiusBottomRight    = roundRight ? r : 0;
        sb.ContentMarginLeft = sb.ContentMarginRight = 6;
        sb.ContentMarginTop  = sb.ContentMarginBottom = 4;
        return sb;
    }

    private static StyleBoxFlat MakeTabBox(Color bg)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 4;
        sb.ContentMarginLeft = sb.ContentMarginRight = 6;
        sb.ContentMarginTop  = sb.ContentMarginBottom = 2;
        return sb;
    }

    private static Color GetEntityColor(string entityType) => entityType switch
    {
        "playercharacter" or "pf2e_pc"   => NotesSidebar.PartyColor,
        "npc"                             => NotesSidebar.NpcColor,
        "faction"                         => NotesSidebar.FactionColor,
        "location"                        => NotesSidebar.LocationColor,
        "session"                         => NotesSidebar.SessionColor,
        "item"                            => NotesSidebar.ItemColor,
        "quest"                           => NotesSidebar.QuestColor,
        "encounter"                       => TrackerSidebar.EncounterColor,
        "ability"                         => SystemSidebar.AbilityColor,
        "class" or "subclass" or "pf2e_class"   => SystemSidebar.ClassColor,
        "species" or "subspecies" or "pf2e_ancestry" => SystemSidebar.SpeciesColor,
        "pf2e_creature"                   => SystemSidebar.CreatureColor,
        _                                 => new Color(0.40f, 0.40f, 0.40f),
    };

    private void DoScrollToActiveTab()
    {
        if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
        var widget = _tabs[_activeTab].Widget;
        if (widget == null || !IsInstanceValid(widget)) return;
        float left     = widget.Position.X;
        float right    = left + widget.Size.X;
        float visLeft  = _tabScroll.ScrollHorizontal;
        float visRight = visLeft + _tabScroll.Size.X;
        if (left < visLeft)
            _tabScroll.ScrollHorizontal = (int)left;
        else if (right > visRight)
            _tabScroll.ScrollHorizontal = (int)(right - _tabScroll.Size.X);
    }
}
