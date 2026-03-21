using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DndBuilder.Core.Models;
using Godot;

public partial class CampaignDashboard : Control
{
    private int             _campaignId;
    private DatabaseService _db;
    private HashSet<int>    _collapsedLocations = new();

    [Export] private Button _addNpcsButton;
    [Export] private Button _addFactionButton;
    [Export] private Button _addLocationsButton;
    [Export] private Button _addSessionsButton;
    [Export] private Button _addItemsButton;
    [Export] private Button _addQuestsButton;

    [Export] private VBoxContainer _npcsContainer;
    [Export] private VBoxContainer _factionsContainer;
    [Export] private VBoxContainer _locationsContainer;
    [Export] private VBoxContainer _sessionsContainer;
    [Export] private VBoxContainer _itemsContainer;
    [Export] private VBoxContainer _questsContainer;

    [Export] private Control         _detailPanel;
    [Export] private Control         _paneContainer;
    [Export] private HBoxContainer   _tabList;
    [Export] private ScrollContainer _tabScroll;
    [Export] private Button          _backButton;
    [Export] private Button          _forwardButton;
    private Control     _addTabWidget;
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

    private sealed class TabEntry
    {
        public string       EntityType = "";
        public int          EntityId;
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

    private const float SidebarMaxWidth = 250f;

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            ApplySidebarWidth();
    }

    private const float DetailPadding       = 8f;
    private const float DetailFooterPadding = 24f;

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
        ApplySidebarWidth();

        _addNpcsButton.Pressed += () =>
        {
            var npc = new Npc { CampaignId = _campaignId, Name = "New NPC" };
            int id = _db.Npcs.Add(npc);
            LoadNpcs();
            ShowDetailPane("npc", id);
        };
        _addFactionButton.Pressed += () =>
        {
            var faction = new Faction { CampaignId = _campaignId, Name = "New Faction" };
            int id = _db.Factions.Add(faction);
            LoadFactions();
            ShowDetailPane("faction", id);
        };
        _addLocationsButton.Pressed += () =>
        {
            var location = new Location { CampaignId = _campaignId, Name = "New Location" };
            int id = _db.Locations.Add(location);
            LoadLocations();
            ShowDetailPane("location", id);
        };
        _addSessionsButton.Pressed += () =>
        {
            var existing = _db.Sessions.GetAll(_campaignId);
            int nextNumber = existing.Count > 0 ? existing.Max(s => s.Number) + 1 : 1;
            var session = new Session { CampaignId = _campaignId, Number = nextNumber, Title = "New Session" };
            int id = _db.Sessions.Add(session);
            LoadSessions();
            ShowDetailPane("session", id);
        };
        _addItemsButton.Pressed += () =>
        {
            var item = new Item { CampaignId = _campaignId, Name = "New Item" };
            int id = _db.Items.Add(item);
            LoadItems();
            ShowDetailPane("item", id);
        };
        _addQuestsButton.Pressed += () =>
        {
            var quest = new Quest { CampaignId = _campaignId, Name = "New Quest" };
            int id = _db.Quests.Add(quest);
            LoadQuests();
            ShowDetailPane("quest", id);
        };

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

        StyleAddButton(_addNpcsButton,      NpcColor);
        StyleAddButton(_addFactionButton,   FactionColor);
        StyleAddButton(_addLocationsButton, LocationColor);
        StyleAddButton(_addSessionsButton,  SessionColor);
        StyleAddButton(_addItemsButton,     ItemColor);
        StyleAddButton(_addQuestsButton,    QuestColor);

        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/NpcsPanel"),                  NpcColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/FactionsPanel"),              FactionColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/LocationsFoldableContainer"), LocationColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/SessionsPanel"),              SessionColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/ItemsPanel"),                 ItemColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/QuestsPanel"),                QuestColor);

        GetNode<LineEdit>("ScrollContainer/VBoxContainer/SearchInput").TextChanged += FilterSidebar;

        LoadAll();
        RestoreTabs();
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
        _collapsedLocations.Clear();
        foreach (var tab in _tabs) { tab.Pane?.QueueFree(); tab.Widget?.QueueFree(); }
        _tabs.Clear();
        _activeTab = -1;
        RefreshNavButtons();
    }

    public void ReloadSidebar() => LoadAll();

    private void LoadAll()
    {
        LoadNpcs();
        LoadFactions();
        LoadLocations();
        LoadSessions();
        LoadItems();
        LoadQuests();
    }

    private static readonly Color NpcColor      = new Color(0.38f, 0.60f, 0.98f); // blue
    private static readonly Color FactionColor  = new Color(0.92f, 0.50f, 0.50f); // red
    private static readonly Color LocationColor = new Color(0.42f, 0.88f, 0.48f); // green
    private static readonly Color SessionColor  = new Color(0.74f, 0.55f, 0.95f); // purple
    private static readonly Color ItemColor     = new Color(0.98f, 0.78f, 0.38f); // amber
    private static readonly Color QuestColor    = new Color(0.92f, 0.52f, 0.88f); // magenta

    private void LoadNpcs()
    {
        ClearItems(_npcsContainer, _addNpcsButton);
        foreach (var npc in _db.Npcs.GetAll(_campaignId))
        {
            int id = npc.Id;
            var btn = MakeSidebarButton(npc.Name, NpcColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("npc", id);
            _npcsContainer.AddChild(btn);
        }
    }

    private void LoadFactions()
    {
        ClearItems(_factionsContainer, _addFactionButton);
        foreach (var faction in _db.Factions.GetAll(_campaignId))
        {
            int id = faction.Id;
            var btn = MakeSidebarButton(faction.Name, FactionColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("faction", id);
            _factionsContainer.AddChild(btn);
        }
    }

    private void LoadLocations()
    {
        ClearItems(_locationsContainer, _addLocationsButton);
        AddLocationRows(_db.Locations.GetTopLevel(_campaignId), 0);
    }

    private void AddLocationRows(System.Collections.Generic.List<Location> locs, int depth)
    {
        foreach (var loc in locs)
        {
            int  id          = loc.Id;
            var  children    = _db.Locations.GetChildren(loc.Id);
            bool isCollapsed = _collapsedLocations.Contains(loc.Id);

            if (children.Count > 0)
            {
                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 0);
                hbox.SetMeta("location_id", id);

                if (depth > 0)
                {
                    var spacer = new Control { CustomMinimumSize = new Vector2(depth * 14, 0) };
                    hbox.AddChild(spacer);
                }

                var toggleBtn = new Button
                {
                    Text              = isCollapsed ? "▶" : "▼",
                    Flat              = false,
                    CustomMinimumSize = new Vector2(24, 0),
                };
                ApplyButtonStyle(toggleBtn, LocationColor, roundRight: false);
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedLocations.Contains(id)) _collapsedLocations.Remove(id);
                    else _collapsedLocations.Add(id);
                    LoadLocations();
                };

                var btn = new Button
                {
                    Text                = loc.Name,
                    Flat                = false,
                    Alignment           = HorizontalAlignment.Left,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                ApplyButtonStyle(btn, LocationColor, roundLeft: false);
                btn.SetMeta("id", id);
                btn.Pressed += () => ShowDetailPane("location", id);

                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _locationsContainer.AddChild(hbox);

                if (!isCollapsed)
                    AddLocationRows(children, depth + 1);
            }
            else if (depth == 0)
            {
                var btn = MakeSidebarButton(loc.Name, LocationColor);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", id);
                btn.Pressed += () => ShowDetailPane("location", id);
                _locationsContainer.AddChild(btn);
            }
            else
            {
                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 0);

                var spacer = new Control { CustomMinimumSize = new Vector2(depth * 14, 0) };
                hbox.AddChild(spacer);

                var btn = MakeSidebarButton(loc.Name, LocationColor, extraLeft: 24);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", id);
                btn.Pressed += () => ShowDetailPane("location", id);
                hbox.AddChild(btn);

                _locationsContainer.AddChild(hbox);
            }
        }
    }

    private void LoadSessions()
    {
        ClearItems(_sessionsContainer, _addSessionsButton);
        foreach (var session in _db.Sessions.GetAll(_campaignId))
        {
            int id = session.Id;
            var btn = MakeSidebarButton(string.IsNullOrEmpty(session.Title) ? "Untitled Session" : session.Title, SessionColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("session", id);
            _sessionsContainer.AddChild(btn);
        }
    }

    private void LoadItems()
    {
        ClearItems(_itemsContainer, _addItemsButton);
        foreach (var item in _db.Items.GetAll(_campaignId))
        {
            int id = item.Id;
            var btn = MakeSidebarButton(item.Name, ItemColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("item", id);
            _itemsContainer.AddChild(btn);
        }
    }

    private void LoadQuests()
    {
        ClearItems(_questsContainer, _addQuestsButton);
        foreach (var quest in _db.Quests.GetAll(_campaignId))
        {
            int id = quest.Id;
            var btn = MakeSidebarButton(quest.Name, QuestColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("quest", id);
            _questsContainer.AddChild(btn);
        }
    }

    private static readonly Color DarkText = new Color(0.10f, 0.10f, 0.10f);

    private static Button MakeSidebarButton(string text, Color color, int extraLeft = 0)
    {
        var btn = new Button
        {
            Text                = text,
            Flat                = false,
            Alignment           = HorizontalAlignment.Left,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        ApplyButtonStyle(btn, color, extraLeft);
        return btn;
    }

    private static void ApplyButtonStyle(Button btn, Color color, int extraLeft = 0, bool roundLeft = true, bool roundRight = true)
    {
        var normal  = MakeBox(color,                  padding: 2, extraLeft: extraLeft, roundLeft: roundLeft, roundRight: roundRight);
        var hover   = MakeBox(color.Lightened(0.12f), padding: 2, extraLeft: extraLeft, roundLeft: roundLeft, roundRight: roundRight);
        var pressed = MakeBox(color.Darkened(0.12f),  padding: 2, extraLeft: extraLeft, roundLeft: roundLeft, roundRight: roundRight);
        btn.AddThemeStyleboxOverride("normal",  normal);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus",   hover);
        btn.AddThemeColorOverride("font_color",          DarkText);
        btn.AddThemeColorOverride("font_hover_color",    DarkText);
        btn.AddThemeColorOverride("font_pressed_color",  DarkText);
        btn.AddThemeColorOverride("font_focus_color",    DarkText);
        btn.AddThemeFontSizeOverride("font_size", 12);
    }

    private static void StyleAddButton(Button btn, Color baseColor)
    {
        ApplyButtonStyle(btn, baseColor.Darkened(0.25f));
        btn.Alignment = HorizontalAlignment.Left;
    }

    private static void StyleAccordion(Control accordion, Color baseColor)
    {
        // "title_panel" = header row, "panel" = content area behind buttons
        accordion.AddThemeStyleboxOverride("title_panel",              MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_hover_panel",        MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_panel",    MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_hover_panel", MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("panel",                    MakeBox(baseColor.Darkened(0.35f)));
    }

    private static StyleBoxFlat MakeBox(Color color, int padding = 0, int extraLeft = 0, bool roundLeft = true, bool roundRight = true)
    {
        var box = new StyleBoxFlat { BgColor = color };
        const int r = 3;
        box.CornerRadiusTopLeft     = roundLeft  ? r : 0;
        box.CornerRadiusBottomLeft  = roundLeft  ? r : 0;
        box.CornerRadiusTopRight    = roundRight ? r : 0;
        box.CornerRadiusBottomRight = roundRight ? r : 0;
        if (padding > 0 || extraLeft > 0)
        {
            box.ContentMarginLeft   = padding + extraLeft;
            box.ContentMarginRight  = padding;
            box.ContentMarginTop    = padding / 2f;
            box.ContentMarginBottom = padding / 2f;
        }
        return box;
    }

    private void ShowDetailPane(string entityType, int entityId)
    {
        // 1. Already open in a tab → switch to it
        int existing = _tabs.FindIndex(t => t.EntityType == entityType && t.EntityId == entityId);
        if (existing >= 0) { ActivateTab(existing); return; }

        // 2. Current tab is unpinned → reuse it
        if (_activeTab >= 0 && _activeTab < _tabs.Count && !_tabs[_activeTab].IsPinned)
        { LoadIntoTab(_activeTab, entityType, entityId); return; }

        // 3. Any other unpinned tab → reuse it
        int unpinned = _tabs.FindIndex(t => !t.IsPinned);
        if (unpinned >= 0) { LoadIntoTab(unpinned, entityType, entityId); return; }

        // 4. Open a new tab
        OpenNewTab(entityType, entityId);
    }

    // Returns (pane, label, loadAction). Call AddChild BEFORE invoking loadAction — _Ready() must run first.
    private (Control Pane, string Label, System.Action Load) InstantiatePane(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "npc":
            {
                var e = _db.Npcs.Get(entityId); if (e == null) return (null, null, null);
                var p = _npcDetailPaneScene.Instantiate<NpcDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { if (type == "faction") LoadFactions(); if (type == "npc") LoadNpcs(); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New NPC" : e.Name, () => p.Load(e));
            }
            case "faction":
            {
                var e = _db.Factions.Get(entityId); if (e == null) return (null, null, null);
                var p = _factionDetailPaneScene.Instantiate<FactionDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { if (type == "npc") LoadNpcs(); if (type == "faction") LoadFactions(); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Faction" : e.Name, () => p.Load(e));
            }
            case "location":
            {
                var e = _db.Locations.Get(entityId); if (e == null) return (null, null, null);
                var p = _locationDetailPaneScene.Instantiate<LocationDetailPane>();
                p.NavigateTo += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubLocationAdded      += (_, __) => LoadLocations();
                p.ParentLocationChanged += _       => LoadLocations();
                p.EntityCreated         += (type, _) => { if (type == "faction") LoadFactions(); if (type == "location") LoadLocations(); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Location" : e.Name, () => p.Load(e));
            }
            case "session":
            {
                var e = _db.Sessions.Get(entityId); if (e == null) return (null, null, null);
                var p = _sessionDetailPaneScene.Instantiate<SessionDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { if (type == "npc") LoadNpcs(); if (type == "faction") LoadFactions(); if (type == "location") LoadLocations(); if (type == "item") LoadItems(); if (type == "quest") LoadQuests(); };
                return (p, string.IsNullOrEmpty(e.Title) ? "Untitled Session" : e.Title, () => p.Load(e));
            }
            case "item":
            {
                var e = _db.Items.Get(entityId); if (e == null) return (null, null, null);
                var p = _itemDetailPaneScene.Instantiate<ItemDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { if (type == "npc") LoadNpcs(); if (type == "faction") LoadFactions(); if (type == "location") LoadLocations(); if (type == "item") LoadItems(); if (type == "quest") LoadQuests(); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Item" : e.Name, () => p.Load(e));
            }
            case "quest":
            {
                var e = _db.Quests.Get(entityId); if (e == null) return (null, null, null);
                var p = _questDetailPaneScene.Instantiate<QuestDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.EntityCreated += (type, _) => { if (type == "npc") LoadNpcs(); if (type == "faction") LoadFactions(); if (type == "location") LoadLocations(); if (type == "item") LoadItems(); if (type == "quest") LoadQuests(); };
                return (p, string.IsNullOrEmpty(e.Name) ? "New Quest" : e.Name, () => p.Load(e));
            }
            default:
                return (null, null, null);
        }
    }

    private void OnEntityDeleted(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "npc":      _db.Npcs.Delete(entityId);      LoadNpcs();      break;
            case "faction":  _db.Factions.Delete(entityId);  LoadFactions();  break;
            case "location": _db.Locations.Delete(entityId); LoadLocations(); break;
            case "session":  _db.Sessions.Delete(entityId);  LoadSessions();  break;
            case "item":     _db.Items.Delete(entityId);     LoadItems();     break;
            case "quest":    _db.Quests.Delete(entityId);    LoadQuests();    break;
        }
        for (int i = _tabs.Count - 1; i >= 0; i--)
            if (_tabs[i].EntityType == entityType && _tabs[i].EntityId == entityId)
                CloseTab(i);
    }

    private void OnNameChanged(string entityType, int entityId, string displayText)
    {
        var container = entityType switch
        {
            "npc"      => _npcsContainer,
            "faction"  => _factionsContainer,
            "location" => _locationsContainer,
            "session"  => _sessionsContainer,
            "item"     => _itemsContainer,
            "quest"    => _questsContainer,
            _          => null
        };
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            Button btn = null;
            if (child is Button b && b.HasMeta("id"))
                btn = b;
            else if (child is HBoxContainer hbox)
                btn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
            if (btn != null && btn.GetMeta("id").AsInt32() == entityId)
            {
                btn.Text = displayText;
                break;
            }
        }
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
        tab.Label      = label;
        tab.Pane       = pane;
        _paneContainer.AddChild(pane);
        load();
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
        var tab = new TabEntry { EntityType = entityType, EntityId = entityId, Label = label, Pane = pane };
        tab.History.Push(entityType, entityId);
        _paneContainer.AddChild(pane);
        load();
        _tabs.Add(tab);
        BuildTabWidget(tab);
        ActivateTab(_tabs.Count - 1);
    }

    private void OpenEmptyTab()
    {
        var placeholder = new Control();
        var tab = new TabEntry { Label = "New Tab", Pane = placeholder };
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
            using var doc     = JsonDocument.Parse(json);
            var root          = doc.RootElement;
            int savedActive   = root.TryGetProperty("active", out var ap) ? ap.GetInt32() : 0;
            var tabs          = root.TryGetProperty("tabs",   out var tp) ? tp : default;
            if (tabs.ValueKind != JsonValueKind.Array) return;

            int restored = 0;
            foreach (var entry in tabs.EnumerateArray())
            {
                string type = entry.GetProperty("type").GetString() ?? "";
                int    id   = entry.GetProperty("id").GetInt32();
                bool pinned = entry.TryGetProperty("pinned", out var pp) && pp.GetBoolean();
                var (pane, label, load) = InstantiatePane(type, id);
                if (pane == null) continue;
                var tab = new TabEntry { EntityType = type, EntityId = id, Label = label, Pane = pane, IsPinned = pinned };
                tab.History.Push(type, id);
                _paneContainer.AddChild(pane);
                load();
                _tabs.Add(tab);
                BuildTabWidget(tab);
                restored++;
            }

            int activeIdx = Mathf.Clamp(savedActive, 0, _tabs.Count - 1);
            if (_tabs.Count > 0) ActivateTab(activeIdx);
        }
        catch { /* corrupt saved state — ignore */ }
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
            Text              = "+",
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
            btn.Text = "◆";
            btn.Modulate = Colors.White;
        }
        else
        {
            btn.Text = "×";
            btn.Modulate = hovering ? Colors.White : Colors.Transparent;
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
            // panel.MouseExited fires first (same frame), re-assert visible state here
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
                case 0: // Pin / Unpin
                    tab.IsPinned = !tab.IsPinned;
                    ApplyActionBtnState(tab, hovering: false);
                    break;
                case 1: // Close
                    if (i >= 0) CloseTab(i);
                    break;
                case 2: // Close Others
                    for (int j = _tabs.Count - 1; j >= 0; j--)
                        if (j != i && !_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 3: // Close to the Left
                    for (int j = i - 1; j >= 0; j--)
                        if (!_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 4: // Close to the Right
                    for (int j = _tabs.Count - 1; j > i; j--)
                        if (!_tabs[j].IsPinned) CloseTab(j);
                    break;
                case 5: // Close All
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
                    { if (!tab.IsPinned) { int i = _tabs.IndexOf(tab); if (i >= 0) CloseTab(i); } }
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
        // Mouse thumb buttons — handled here (not _UnhandledInput) because GUI controls consume mouse events first
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

        // Swap into whichever tab the cursor is currently hovering over.
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

        if      (_activeTab == fromIdx)                          _activeTab = toIdx;
        else if (fromIdx < _activeTab && toIdx >= _activeTab)   _activeTab--;
        else if (fromIdx > _activeTab && toIdx <= _activeTab)   _activeTab++;
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
        "npc"      => NpcColor,
        "faction"  => FactionColor,
        "location" => LocationColor,
        "session"  => SessionColor,
        "item"     => ItemColor,
        "quest"    => QuestColor,
        _          => new Color(0.40f, 0.40f, 0.40f),
    };

    private void DoScrollToActiveTab()
    {
        if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
        var widget = _tabs[_activeTab].Widget;
        if (widget == null || !IsInstanceValid(widget)) return;
        float left  = widget.Position.X;
        float right = left + widget.Size.X;
        float visLeft  = _tabScroll.ScrollHorizontal;
        float visRight = visLeft + _tabScroll.Size.X;
        if (left < visLeft)
            _tabScroll.ScrollHorizontal = (int)left;
        else if (right > visRight)
            _tabScroll.ScrollHorizontal = (int)(right - _tabScroll.Size.X);
    }

    private static void ClearItems(VBoxContainer container, Button keepButton)
    {
        foreach (Node child in container.GetChildren())
            if (child != keepButton) child.QueueFree();
    }

    // ── sidebar search ────────────────────────────────────────────────────────

    private void FilterSidebar(string query)
    {
        bool searching = !string.IsNullOrEmpty(query);

        var sections = new (Control Panel, VBoxContainer Items)[]
        {
            (GetNode<Control>("ScrollContainer/VBoxContainer/NpcsPanel"),                  _npcsContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/FactionsPanel"),              _factionsContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/LocationsFoldableContainer"), _locationsContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/SessionsPanel"),              _sessionsContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/ItemsPanel"),                 _itemsContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/QuestsPanel"),                _questsContainer),
        };

        foreach (var (panel, items) in sections)
        {
            if (!searching)
            {
                panel.Visible = true;
                panel.Set("folded", true);
                items.Visible = false;
                if (items == _locationsContainer)
                    LoadLocations();
                else
                    foreach (Node child in items.GetChildren())
                        if (child is Control c) c.Visible = true;
                continue;
            }

            bool hasMatch = false;
            foreach (Node child in items.GetChildren())
            {
                if (child is Button btn && btn.HasMeta("id"))
                {
                    bool match = FuzzyMatch(query, btn.Text);
                    btn.Visible = match;
                    if (match) hasMatch = true;
                }
                else if (items == _locationsContainer && child is HBoxContainer hbox)
                {
                    var locBtn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
                    if (locBtn != null)
                    {
                        bool match = FuzzyMatch(query, locBtn.Text);
                        hbox.Visible = match;
                        if (match) hasMatch = true;
                    }
                }
            }

            panel.Visible = hasMatch;
            if (hasMatch)
            {
                panel.Set("folded", false);
                items.Visible = true;
            }
        }
    }

    private const float FuzzyThreshold = 0.7f;

    private static bool FuzzyMatch(string query, string target)
    {
        if (string.IsNullOrEmpty(query)) return true;
        query  = query.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // 1. Direct substring
        if (target.Contains(query)) return true;

        // 2. Subsequence (handles abbreviations like "tfac" → "Test Faction")
        if (IsSubsequence(query, target)) return true;

        // 3. Sliding-window Levenshtein — catches typos like "Boblins" → "Goblins"
        int qLen = query.Length;
        int tLen = target.Length;
        int windowLen = qLen;

        if (windowLen <= tLen)
        {
            for (int start = 0; start <= tLen - windowLen; start++)
            {
                float sim = Similarity(query, target.Substring(start, windowLen));
                if (sim >= FuzzyThreshold) return true;
            }
        }
        else
        {
            // Query longer than target — compare against the whole target
            if (Similarity(query, target) >= FuzzyThreshold) return true;
        }

        return false;
    }

    private static bool IsSubsequence(string query, string target)
    {
        int qi = 0;
        for (int i = 0; i < target.Length && qi < query.Length; i++)
            if (target[i] == query[qi]) qi++;
        return qi == query.Length;
    }

    private static float Similarity(string a, string b)
    {
        int dist = LevenshteinDistance(a, b);
        return 1f - (float)dist / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int aLen = a.Length, bLen = b.Length;
        var dp = new int[aLen + 1, bLen + 1];
        for (int i = 0; i <= aLen; i++) dp[i, 0] = i;
        for (int j = 0; j <= bLen; j++) dp[0, j] = j;
        for (int i = 1; i <= aLen; i++)
            for (int j = 1; j <= bLen; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
        return dp[aLen, bLen];
    }
}