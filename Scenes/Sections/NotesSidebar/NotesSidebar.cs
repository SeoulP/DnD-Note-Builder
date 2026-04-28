using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class NotesSidebar : VBoxContainer
{
    private int              _campaignId;
    private Campaign         _campaign;
    private DatabaseService  _db;
    private HashSet<int>     _collapsedLocations = new();

    [Export] private Button        _addPartyButton;
    [Export] private Button        _addNpcsButton;
    [Export] private Button        _addFactionButton;
    [Export] private Button        _addLocationsButton;
    [Export] private Button        _addSessionsButton;
    [Export] private Button        _addItemsButton;
    [Export] private Button        _addQuestsButton;

    [Export] private VBoxContainer _partyContainer;
    [Export] private VBoxContainer _npcsContainer;
    [Export] private VBoxContainer _factionsContainer;
    [Export] private VBoxContainer _locationsContainer;
    [Export] private VBoxContainer _sessionsContainer;
    [Export] private VBoxContainer _itemsContainer;
    [Export] private VBoxContainer _questsContainer;

    [Signal] public delegate void EntitySelectedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntitySelectedNewTabEventHandler(string entityType, int entityId);

    internal static readonly Color PartyColor    = new(0.82f, 0.69f, 0.63f);
    internal static readonly Color NpcColor      = new(0.38f, 0.60f, 0.98f);
    internal static readonly Color FactionColor  = new(0.92f, 0.50f, 0.50f);
    internal static readonly Color LocationColor = new(0.42f, 0.88f, 0.48f);
    internal static readonly Color SessionColor  = new(0.74f, 0.55f, 0.95f);
    internal static readonly Color ItemColor     = new(0.98f, 0.78f, 0.38f);
    internal static readonly Color QuestColor    = new(0.92f, 0.52f, 0.88f);

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _addPartyButton.Pressed += () =>
        {
            if (_campaign?.System == "pathfinder2e")
            {
                var pc = new Pf2eCharacter { CampaignId = _campaignId, Name = "New Character" };
                int id = _db.Pf2eCharacters.Add(pc);
                LoadParty();
                EmitSignal(SignalName.EntitySelected, "pf2e_pc", id);
            }
            else
            {
                var pc = new PlayerCharacter { CampaignId = _campaignId, Name = "New Character" };
                int id = _db.PlayerCharacters.Add(pc);
                LoadParty();
                EmitSignal(SignalName.EntitySelected, "playercharacter", id);
            }
        };
        _addNpcsButton.Pressed += () =>
        {
            var npc = new Npc { CampaignId = _campaignId, Name = "New NPC" };
            int id = _db.Npcs.Add(npc);
            LoadNpcs();
            EmitSignal(SignalName.EntitySelected, "npc", id);
        };
        _addFactionButton.Pressed += () =>
        {
            var faction = new Faction { CampaignId = _campaignId, Name = "New Faction" };
            int id = _db.Factions.Add(faction);
            LoadFactions();
            EmitSignal(SignalName.EntitySelected, "faction", id);
        };
        _addLocationsButton.Pressed += () =>
        {
            var location = new Location { CampaignId = _campaignId, Name = "New Location" };
            int id = _db.Locations.Add(location);
            LoadLocations();
            EmitSignal(SignalName.EntitySelected, "location", id);
        };
        _addSessionsButton.Pressed += () =>
        {
            var existing   = _db.Sessions.GetAll(_campaignId);
            int nextNumber = existing.Count > 0 ? existing.Max(s => s.Number) + 1 : 1;
            var session    = new Session { CampaignId = _campaignId, Number = nextNumber, Title = "New Session" };
            int id         = _db.Sessions.Add(session);
            LoadSessions();
            EmitSignal(SignalName.EntitySelected, "session", id);
        };
        _addItemsButton.Pressed += () =>
        {
            var item = new Item { CampaignId = _campaignId, Name = "New Item" };
            int id   = _db.Items.Add(item);
            LoadItems();
            EmitSignal(SignalName.EntitySelected, "item", id);
        };
        _addQuestsButton.Pressed += () =>
        {
            var quest = new Quest { CampaignId = _campaignId, Name = "New Quest" };
            int id    = _db.Quests.Add(quest);
            LoadQuests();
            EmitSignal(SignalName.EntitySelected, "quest", id);
        };

        StyleAddButton(_addPartyButton,     PartyColor);
        StyleAddButton(_addNpcsButton,      NpcColor);
        StyleAddButton(_addFactionButton,   FactionColor);
        StyleAddButton(_addLocationsButton, LocationColor);
        StyleAddButton(_addSessionsButton,  SessionColor);
        StyleAddButton(_addItemsButton,     ItemColor);
        StyleAddButton(_addQuestsButton,    QuestColor);

        StyleAccordion(GetNode<Control>("PartyPanel"),                 PartyColor);
        StyleAccordion(GetNode<Control>("NpcsPanel"),                  NpcColor);
        StyleAccordion(GetNode<Control>("FactionsPanel"),              FactionColor);
        StyleAccordion(GetNode<Control>("LocationsFoldableContainer"), LocationColor);
        StyleAccordion(GetNode<Control>("SessionsPanel"),              SessionColor);
        StyleAccordion(GetNode<Control>("ItemsPanel"),                 ItemColor);
        StyleAccordion(GetNode<Control>("QuestsPanel"),                QuestColor);

        GetNode<LineEdit>("SearchInput").TextChanged += FilterSidebar;
    }

    public void SetCampaign(int campaignId, Campaign campaign, SystemVocabulary vocab)
    {
        _campaignId = campaignId;
        _campaign   = campaign;
        _collapsedLocations.Clear();
        ReloadAll();
    }

    public void ReloadAll()
    {
        LoadParty();
        LoadNpcs();
        LoadFactions();
        LoadLocations();
        LoadSessions();
        LoadItems();
        LoadQuests();
    }

    public void Reload(string entityType)
    {
        switch (entityType)
        {
            case "playercharacter": case "pf2e_pc": LoadParty();     break;
            case "npc":                             LoadNpcs();      break;
            case "faction":                         LoadFactions();  break;
            case "location":                        LoadLocations(); break;
            case "session":                         LoadSessions();  break;
            case "item":                            LoadItems();     break;
            case "quest":                           LoadQuests();    break;
        }
    }

    public void UpdateButtonName(string entityType, int entityId, string name)
    {
        var container = entityType switch
        {
            "playercharacter" or "pf2e_pc" => _partyContainer,
            "npc"                           => _npcsContainer,
            "faction"                       => _factionsContainer,
            "location"                      => _locationsContainer,
            "session"                       => _sessionsContainer,
            "item"                          => _itemsContainer,
            "quest"                         => _questsContainer,
            _                               => null,
        };
        FindAndRenameButton(container, entityId, name);
    }

    // ── load methods ──────────────────────────────────────────────────────────

    private void LoadParty()
    {
        ClearItems(_partyContainer, _addPartyButton);
        if (_campaign?.System == "pathfinder2e")
        {
            foreach (var pc in _db.Pf2eCharacters.GetAll(_campaignId))
            {
                int id  = pc.Id;
                var btn = MakeSidebarButton(string.IsNullOrEmpty(pc.Name) ? "New Character" : pc.Name, PartyColor);
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "pf2e_pc", id);
                WireCtrlClick(btn, "pf2e_pc", id);
                _partyContainer.AddChild(btn);
            }
            return;
        }
        foreach (var pc in _db.PlayerCharacters.GetAll(_campaignId))
        {
            int id  = pc.Id;
            var btn = MakeSidebarButton(string.IsNullOrEmpty(pc.Name) ? "New Character" : pc.Name, PartyColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "playercharacter", id);
            WireCtrlClick(btn, "playercharacter", id);
            _partyContainer.AddChild(btn);
        }
    }

    private void LoadNpcs()
    {
        ClearItems(_npcsContainer, _addNpcsButton);
        foreach (var npc in _db.Npcs.GetAll(_campaignId))
        {
            int id  = npc.Id;
            var btn = MakeSidebarButton(npc.Name, NpcColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "npc", id);
            WireCtrlClick(btn, "npc", id);
            _npcsContainer.AddChild(btn);
        }
    }

    private void LoadFactions()
    {
        ClearItems(_factionsContainer, _addFactionButton);
        foreach (var faction in _db.Factions.GetAll(_campaignId))
        {
            int id  = faction.Id;
            var btn = MakeSidebarButton(faction.Name, FactionColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "faction", id);
            WireCtrlClick(btn, "faction", id);
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
                    hbox.AddChild(new Control { CustomMinimumSize = new Vector2(depth * 14, 0) });

                var toggleBtn = new Button { Text = isCollapsed ? "▶" : "▼", Flat = false, CustomMinimumSize = new Vector2(24, 0) };
                ApplyButtonStyle(toggleBtn, LocationColor, roundRight: false);
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedLocations.Contains(id)) _collapsedLocations.Remove(id);
                    else _collapsedLocations.Add(id);
                    LoadLocations();
                };

                var btn = new Button { Text = loc.Name, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                ApplyButtonStyle(btn, LocationColor, roundLeft: false);
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "location", id);
                WireCtrlClick(btn, "location", id);

                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _locationsContainer.AddChild(hbox);

                if (!isCollapsed) AddLocationRows(children, depth + 1);
            }
            else if (depth == 0)
            {
                var btn = MakeSidebarButton(loc.Name, LocationColor);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "location", id);
                WireCtrlClick(btn, "location", id);
                _locationsContainer.AddChild(btn);
            }
            else
            {
                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 0);
                hbox.AddChild(new Control { CustomMinimumSize = new Vector2(depth * 14, 0) });
                var btn = MakeSidebarButton(loc.Name, LocationColor, extraLeft: 24);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "location", id);
                WireCtrlClick(btn, "location", id);
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
            int id  = session.Id;
            var btn = MakeSidebarButton(string.IsNullOrEmpty(session.Title) ? "Untitled Session" : session.Title, SessionColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "session", id);
            WireCtrlClick(btn, "session", id);
            _sessionsContainer.AddChild(btn);
        }
    }

    private void LoadItems()
    {
        ClearItems(_itemsContainer, _addItemsButton);
        foreach (var item in _db.Items.GetAll(_campaignId))
        {
            int id  = item.Id;
            var btn = MakeSidebarButton(item.Name, ItemColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "item", id);
            WireCtrlClick(btn, "item", id);
            _itemsContainer.AddChild(btn);
        }
    }

    private void LoadQuests()
    {
        ClearItems(_questsContainer, _addQuestsButton);
        foreach (var quest in _db.Quests.GetAll(_campaignId))
        {
            int id  = quest.Id;
            var btn = MakeSidebarButton(quest.Name, QuestColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "quest", id);
            WireCtrlClick(btn, "quest", id);
            _questsContainer.AddChild(btn);
        }
    }

    // ── search ────────────────────────────────────────────────────────────────

    private void FilterSidebar(string query)
    {
        bool searching = !string.IsNullOrEmpty(query);
        var sections = new (Control Panel, VBoxContainer Items)[]
        {
            (GetNode<Control>("PartyPanel"),                 _partyContainer),
            (GetNode<Control>("NpcsPanel"),                  _npcsContainer),
            (GetNode<Control>("FactionsPanel"),              _factionsContainer),
            (GetNode<Control>("LocationsFoldableContainer"), _locationsContainer),
            (GetNode<Control>("SessionsPanel"),              _sessionsContainer),
            (GetNode<Control>("ItemsPanel"),                 _itemsContainer),
            (GetNode<Control>("QuestsPanel"),                _questsContainer),
        };

        foreach (var (panel, items) in sections)
        {
            if (!searching)
            {
                panel.Visible = true;
                panel.Set("folded", true);
                items.Visible = false;
                if (items == _locationsContainer) LoadLocations();
                else foreach (Node child in items.GetChildren()) if (child is Control c) c.Visible = true;
                continue;
            }

            bool hasMatch = false;
            foreach (Node child in items.GetChildren())
            {
                if (child is Button btn && btn.HasMeta("id"))
                    { bool m = SidebarSearch.FuzzyMatch(query, btn.Text); btn.Visible = m; if (m) hasMatch = true; }
                else if (items == _locationsContainer && child is HBoxContainer hbox)
                {
                    var locBtn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
                    if (locBtn != null) { bool m = SidebarSearch.FuzzyMatch(query, locBtn.Text); hbox.Visible = m; if (m) hasMatch = true; }
                }
            }
            panel.Visible = hasMatch;
            if (hasMatch) { panel.Set("folded", false); items.Visible = true; }
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void WireCtrlClick(Button btn, string entityType, int entityId)
    {
        btn.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && mb.CtrlPressed)
            { btn.AcceptEvent(); EmitSignal(SignalName.EntitySelectedNewTab, entityType, entityId); }
        };
    }

    private static void FindAndRenameButton(VBoxContainer container, int entityId, string name)
    {
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            Button btn = null;
            if (child is Button b && b.HasMeta("id")) btn = b;
            else if (child is HBoxContainer hbox) btn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
            if (btn != null && btn.GetMeta("id").AsInt32() == entityId) { btn.Text = name; break; }
        }
    }

    private static void ClearItems(VBoxContainer container, Button keepButton)
    {
        foreach (Node child in container.GetChildren())
            if (child != keepButton) child.QueueFree();
    }

    private static readonly Color DarkText = new(0.10f, 0.10f, 0.10f);

    private static Button MakeSidebarButton(string text, Color color, int extraLeft = 0)
    {
        var btn = new Button { Text = text, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        ApplyButtonStyle(btn, color, extraLeft);
        return btn;
    }

    internal static void ApplyButtonStyle(Button btn, Color color, int extraLeft = 0, bool roundLeft = true, bool roundRight = true)
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

    internal static void StyleAddButton(Button btn, Color baseColor)
    {
        ApplyButtonStyle(btn, baseColor.Darkened(0.25f));
        btn.Alignment = HorizontalAlignment.Left;
    }

    internal static void StyleAccordion(Control accordion, Color baseColor)
    {
        accordion.AddThemeStyleboxOverride("title_panel",                 MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_hover_panel",           MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_panel",       MakeBox(baseColor.Darkened(0.50f)));
        accordion.AddThemeStyleboxOverride("title_collapsed_hover_panel", MakeBox(baseColor.Darkened(0.40f)));
        accordion.AddThemeStyleboxOverride("panel",                       MakeBox(baseColor.Darkened(0.35f)));
    }

    internal static StyleBoxFlat MakeBox(Color color, int padding = 0, int extraLeft = 0, bool roundLeft = true, bool roundRight = true)
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
}
