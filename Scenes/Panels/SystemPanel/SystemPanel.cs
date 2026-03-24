using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DndBuilder.Core.Models;
using Godot;

public partial class SystemPanel : Control
{
    private int             _campaignId;
    private DatabaseService _db;
    private HashSet<int>    _collapsedClasses = new();
    private HashSet<int>    _collapsedSpecies = new();
    private SystemVocabulary _vocab           = SystemVocabulary.Default;

    [Export] private Button _addClassesButton;
    [Export] private Button _addSpeciesButton;
    [Export] private Button _addAbilitiesButton;

    [Export] private VBoxContainer _classesContainer;
    [Export] private VBoxContainer _speciesContainer;
    [Export] private VBoxContainer _abilitiesContainer;

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

    [Export] private PackedScene _abilityDetailPaneScene;
    [Export] private PackedScene _classDetailPaneScene;
    [Export] private PackedScene _subclassDetailPaneScene;
    [Export] private PackedScene _speciesDetailPaneScene;
    [Export] private PackedScene _subspeciesDetailPaneScene;

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
        if (_campaignId > 0)
            _vocab = SystemVocabulary.For(_db.Campaigns.Get(_campaignId)?.System);
        ApplySidebarWidth();

        _addClassesButton.Pressed += () =>
        {
            var cls = new Class { CampaignId = _campaignId, Name = $"New {_vocab.Class}" };
            int id = _db.Classes.Add(cls);
            LoadClasses();
            ShowDetailPane("class", id);
        };
        _addSpeciesButton.Pressed += () =>
        {
            var species = new Species { CampaignId = _campaignId, Name = $"New {_vocab.Species}" };
            int id = _db.Species.Add(species);
            LoadSpecies();
            ShowDetailPane("species", id);
        };
        _addAbilitiesButton.Pressed += () =>
        {
            var ability = new Ability { CampaignId = _campaignId, Name = $"New {_vocab.Ability}" };
            int id = _db.Abilities.Add(ability);
            LoadAbilities();
            ShowDetailPane("ability", id);
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

        StyleAddButton(_addClassesButton,   ClassColor);
        StyleAddButton(_addSpeciesButton,   SpeciesColor);
        StyleAddButton(_addAbilitiesButton, AbilityColor);

        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/ClassesPanel"),    ClassColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/SpeciesPanel"),    SpeciesColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/AbilitiesPanel"),  AbilityColor);
        ApplySystemVocabulary();

        GetNode<LineEdit>("ScrollContainer/VBoxContainer/SearchInput").TextChanged += FilterSidebar;

        if (_campaignId > 0) LoadAll();
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
        _collapsedClasses.Clear();
        _collapsedSpecies.Clear();
        foreach (var tab in _tabs) { tab.Pane?.QueueFree(); tab.Widget?.QueueFree(); }
        _tabs.Clear();
        _activeTab = -1;
        RefreshNavButtons();
    }

    public void ReloadSidebar() => LoadAll();

    private void LoadAll()
    {
        LoadClasses();
        LoadSpecies();
        LoadAbilities();
    }

    private static readonly Color ClassColor   = new Color(0.98f, 0.65f, 0.28f); // orange
    private static readonly Color SpeciesColor = new Color(0.28f, 0.78f, 0.98f); // sky blue
    private static readonly Color AbilityColor = new Color(0.72f, 0.95f, 0.28f); // lime

    private void LoadClasses()
    {
        ClearItems(_classesContainer, _addClassesButton);
        AddClassRows(_db.Classes.GetAll(_campaignId));
    }

    private void AddClassRows(List<Class> classes)
    {
        foreach (var cls in classes)
        {
            int  clsId       = cls.Id;
            var  subclasses  = _db.Classes.GetSubclassesForClass(cls.Id);
            bool isCollapsed = _collapsedClasses.Contains(cls.Id);

            if (subclasses.Count > 0)
            {
                var hbox = new HBoxContainer();
                hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                var toggleBtn = new Button
                {
                    Text              = isCollapsed ? "▶" : "▼",
                    Flat              = true,
                    CustomMinimumSize = new Vector2(24, 0),
                };
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedClasses.Contains(clsId)) _collapsedClasses.Remove(clsId);
                    else _collapsedClasses.Add(clsId);
                    LoadClasses();
                };

                var btn = MakeSidebarButton(cls.Name, ClassColor);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", clsId);
                btn.Pressed += () => ShowDetailPane("class", clsId);
                WireCtrlClick(btn, "class", clsId);

                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _classesContainer.AddChild(hbox);

                if (!isCollapsed)
                {
                    foreach (var sub in subclasses)
                    {
                        int subId = sub.Id;
                        var subBtn = MakeSidebarButton(sub.Name, ClassColor, extraLeft: 24);
                        subBtn.SetMeta("id", subId);
                        subBtn.Pressed += () => ShowDetailPane("subclass", subId);
                        WireCtrlClick(subBtn, "subclass", subId);
                        _classesContainer.AddChild(subBtn);
                    }
                }
            }
            else
            {
                var btn = MakeSidebarButton(cls.Name, ClassColor);
                btn.SetMeta("id", clsId);
                btn.Pressed += () => ShowDetailPane("class", clsId);
                WireCtrlClick(btn, "class", clsId);
                _classesContainer.AddChild(btn);
            }
        }
    }

    private void LoadSpecies()
    {
        ClearItems(_speciesContainer, _addSpeciesButton);
        var allSpecies = _db.Species.GetAll(_campaignId);
        foreach (var sp in allSpecies)
        {
            int  spId        = sp.Id;
            var  subspecies  = _db.Subspecies.GetAllForSpecies(sp.Id);
            bool isCollapsed = _collapsedSpecies.Contains(sp.Id);

            if (subspecies.Count > 0)
            {
                var hbox = new HBoxContainer();
                hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                var toggleBtn = new Button
                {
                    Text              = isCollapsed ? "▶" : "▼",
                    Flat              = true,
                    CustomMinimumSize = new Vector2(24, 0),
                };
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedSpecies.Contains(spId)) _collapsedSpecies.Remove(spId);
                    else _collapsedSpecies.Add(spId);
                    LoadSpecies();
                };

                var btn = MakeSidebarButton(sp.Name, SpeciesColor);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", spId);
                btn.Pressed += () => ShowDetailPane("species", spId);
                WireCtrlClick(btn, "species", spId);

                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _speciesContainer.AddChild(hbox);

                if (!isCollapsed)
                {
                    foreach (var sub in subspecies)
                    {
                        int subId = sub.Id;
                        var subBtn = MakeSidebarButton(sub.Name, SpeciesColor, extraLeft: 24);
                        subBtn.SetMeta("id", subId);
                        subBtn.Pressed += () => ShowDetailPane("subspecies", subId);
                        WireCtrlClick(subBtn, "subspecies", subId);
                        _speciesContainer.AddChild(subBtn);
                    }
                }
            }
            else
            {
                var btn = MakeSidebarButton(sp.Name, SpeciesColor);
                btn.SetMeta("id", spId);
                btn.Pressed += () => ShowDetailPane("species", spId);
                WireCtrlClick(btn, "species", spId);
                _speciesContainer.AddChild(btn);
            }
        }
    }

    private void LoadAbilities()
    {
        ClearItems(_abilitiesContainer, _addAbilitiesButton);
        foreach (var ability in _db.Abilities.GetAll(_campaignId))
        {
            int id = ability.Id;
            var btn = MakeSidebarButton(ability.Name, AbilityColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("ability", id);
            WireCtrlClick(btn, "ability", id);
            _abilitiesContainer.AddChild(btn);
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

    private static void ApplyButtonStyle(Button btn, Color color, int extraLeft = 0)
    {
        var normal  = MakeBox(color,                  padding: 2, extraLeft: extraLeft);
        var hover   = MakeBox(color.Lightened(0.12f), padding: 2, extraLeft: extraLeft);
        var pressed = MakeBox(color.Darkened(0.12f),  padding: 2, extraLeft: extraLeft);
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

    private void ApplySystemVocabulary()
    {
        GetNode<Control>("ScrollContainer/VBoxContainer/ClassesPanel") .Set("title", _vocab.ClassesPlural);
        GetNode<Control>("ScrollContainer/VBoxContainer/SpeciesPanel") .Set("title", _vocab.SpeciesPlural);
        GetNode<Control>("ScrollContainer/VBoxContainer/AbilitiesPanel").Set("title", _vocab.AbilitiesPlural);
    }

    private static void StyleAccordion(Control accordion, Color baseColor)
    {
        accordion.AddThemeStyleboxOverride("title_panel",                 MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_hover_panel",           MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_panel",       MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_hover_panel", MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("panel",                       MakeBox(baseColor.Darkened(0.35f)));
    }

    private static StyleBoxFlat MakeBox(Color color, int padding = 0, int extraLeft = 0)
    {
        var box = new StyleBoxFlat { BgColor = color };
        const int r = 3;
        box.CornerRadiusTopLeft = box.CornerRadiusBottomLeft = box.CornerRadiusTopRight = box.CornerRadiusBottomRight = r;
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

    private void WireCtrlClick(Button btn, string entityType, int entityId)
    {
        btn.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && mb.CtrlPressed)
            {
                btn.AcceptEvent();
                ShowDetailPaneInNewTab(entityType, entityId);
            }
        };
    }

    private (Control Pane, string Label, Action Load) InstantiatePane(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "ability":
            {
                var e = _db.Abilities.Get(entityId); if (e == null) return (null, null, null);
                var p = _abilityDetailPaneScene.Instantiate<AbilityDetailPane>();
                p.NavigateTo  += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Ability}" : e.Name, () => p.Load(e));
            }
            case "class":
            {
                var e = _db.Classes.Get(entityId); if (e == null) return (null, null, null);
                var p = _classDetailPaneScene.Instantiate<ClassDetailPane>();
                p.NavigateTo    += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubclassAdded += (_, subId) => LoadClasses();
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Class}" : e.Name, () => p.Load(e));
            }
            case "subclass":
            {
                var e = _db.Classes.GetSubclass(entityId); if (e == null) return (null, null, null);
                var p = _subclassDetailPaneScene.Instantiate<SubclassDetailPane>();
                p.NavigateTo  += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Subclass}" : e.Name, () => p.Load(e));
            }
            case "species":
            {
                var e = _db.Species.Get(entityId); if (e == null) return (null, null, null);
                var p = _speciesDetailPaneScene.Instantiate<SpeciesDetailPane>();
                p.NavigateTo      += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                p.SubspeciesAdded += (_, subId) => LoadSpecies();
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Species}" : e.Name, () => p.Load(e));
            }
            case "subspecies":
            {
                var e = _db.Subspecies.Get(entityId); if (e == null) return (null, null, null);
                var p = _subspeciesDetailPaneScene.Instantiate<SubspeciesDetailPane>();
                p.NavigateTo  += ShowDetailPane; p.NameChanged += OnNameChanged; p.Deleted += OnEntityDeleted;
                return (p, string.IsNullOrEmpty(e.Name) ? $"New {_vocab.Subspecies}" : e.Name, () => p.Load(e));
            }
            default:
                return (null, null, null);
        }
    }

    private void OnEntityDeleted(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "ability":    _db.Abilities.Delete(entityId);          LoadAbilities(); break;
            case "class":      _db.Classes.Delete(entityId);            LoadClasses();   break;
            case "subclass":   _db.Classes.DeleteSubclass(entityId);    LoadClasses();   break;
            case "species":    _db.Species.Delete(entityId);            LoadSpecies();   break;
            case "subspecies": _db.Subspecies.Delete(entityId);         LoadSpecies();   break;
        }
        for (int i = _tabs.Count - 1; i >= 0; i--)
            if (_tabs[i].EntityType == entityType && _tabs[i].EntityId == entityId)
                CloseTab(i);
    }

    private void OnNameChanged(string entityType, int entityId, string displayText)
    {
        var container = entityType switch
        {
            "ability"    => _abilitiesContainer,
            "class"      => _classesContainer,
            "subclass"   => _classesContainer,
            "species"    => _speciesContainer,
            "subspecies" => _speciesContainer,
            _            => null
        };
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            Button btn = null;
            if (child is Button b && b.HasMeta("id"))
                btn = b;
            else if (child is HBoxContainer hbox)
                btn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
            if (btn != null && (int)btn.GetMeta("id") == entityId)
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

    // ── tab management (mirrors CampaignDashboard) ────────────────────────────

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
            { var (type, id) = tab.History.Back(); LoadIntoTab(_activeTab, type, id, pushHistory: false); AcceptEvent(); }
            else if (key.AltPressed && key.Keycode == Key.Right && tab.History.CanGoForward)
            { var (type, id) = tab.History.Forward(); LoadIntoTab(_activeTab, type, id, pushHistory: false); AcceptEvent(); }
        }
    }

    private void CloseTab(int index)
    {
        var tab = _tabs[index];
        if (tab.Pane   != null && IsInstanceValid(tab.Pane))   tab.Pane.QueueFree();
        if (tab.Widget != null && IsInstanceValid(tab.Widget)) tab.Widget.QueueFree();
        _tabs.RemoveAt(index);
        if (_tabs.Count == 0) { _activeTab = -1; return; }
        int next = Mathf.Clamp(index < _tabs.Count ? index : _tabs.Count - 1, 0, _tabs.Count - 1);
        ActivateTab(next);
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
                case 0: tab.IsPinned = !tab.IsPinned; ApplyActionBtnState(tab, hovering: false); break;
                case 1: if (i >= 0) CloseTab(i); break;
                case 2: for (int j = _tabs.Count - 1; j >= 0; j--) if (j != i && !_tabs[j].IsPinned) CloseTab(j); break;
                case 3: for (int j = i - 1; j >= 0; j--) if (!_tabs[j].IsPinned) CloseTab(j); break;
                case 4: for (int j = _tabs.Count - 1; j > i; j--) if (!_tabs[j].IsPinned) CloseTab(j); break;
                case 5: for (int j = _tabs.Count - 1; j >= 0; j--) if (!_tabs[j].IsPinned) CloseTab(j); break;
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
            { var (type, id) = navTab.History.Back(); LoadIntoTab(_activeTab, type, id, pushHistory: false); AcceptEvent(); return; }
            if (mb.ButtonIndex == MouseButton.Xbutton2 && navTab.History.CanGoForward)
            { var (type, id) = navTab.History.Forward(); LoadIntoTab(_activeTab, type, id, pushHistory: false); AcceptEvent(); return; }
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
            { toIdx = i; break; }
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

        var vbox  = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        var hbox  = new HBoxContainer();
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
        var sb = new StyleBoxFlat { BgColor = bg, DrawCenter = true };
        sb.CornerRadiusTopLeft = sb.CornerRadiusBottomLeft = roundLeft  ? r : 0;
        sb.CornerRadiusTopRight = sb.CornerRadiusBottomRight = roundRight ? r : 0;
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
        "class"      => ClassColor,
        "subclass"   => ClassColor,
        "species"    => SpeciesColor,
        "subspecies" => SpeciesColor,
        "ability"    => AbilityColor,
        _            => new Color(0.40f, 0.40f, 0.40f),
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

    private void FilterSidebar(string query)
    {
        bool searching = !string.IsNullOrEmpty(query);
        var sections = new (Control Panel, VBoxContainer Items)[]
        {
            (GetNode<Control>("ScrollContainer/VBoxContainer/ClassesPanel"),   _classesContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/SpeciesPanel"),   _speciesContainer),
            (GetNode<Control>("ScrollContainer/VBoxContainer/AbilitiesPanel"), _abilitiesContainer),
        };

        foreach (var (panel, items) in sections)
        {
            if (!searching)
            {
                panel.Visible = true;
                panel.Set("folded", true);
                items.Visible = false;
                foreach (Node child in items.GetChildren())
                    if (child is Control c) c.Visible = true;
            }
            else
            {
                panel.Set("folded", false);
                items.Visible = true;
                string lq = query.ToLowerInvariant();
                foreach (Node child in items.GetChildren())
                {
                    if (child is Button btn)
                        btn.Visible = btn.Text.ToLowerInvariant().Contains(lq);
                    else if (child is HBoxContainer hbox)
                    {
                        bool any = hbox.GetChildren().OfType<Button>()
                            .Any(b => b.Text.ToLowerInvariant().Contains(lq));
                        hbox.Visible = any;
                    }
                }
            }
        }
    }
}
