using System;
using System.Collections.Generic;
using System.Linq;
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

    [Export] private Control _detailPanel;

    [Export] private PackedScene _npcDetailPaneScene;
    [Export] private PackedScene _factionDetailPaneScene;
    [Export] private PackedScene _locationDetailPaneScene;
    [Export] private PackedScene _sessionDetailPaneScene;
    [Export] private PackedScene _itemDetailPaneScene;
    [Export] private PackedScene _questDetailPaneScene;

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
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
        _collapsedLocations.Clear();
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

    private static readonly Color NpcColor      = new Color(0.53f, 0.72f, 0.90f); // pastel blue
    private static readonly Color FactionColor  = new Color(0.90f, 0.58f, 0.58f); // pastel red
    private static readonly Color LocationColor = new Color(0.58f, 0.82f, 0.64f); // pastel green
    private static readonly Color SessionColor  = new Color(0.74f, 0.62f, 0.90f); // pastel purple
    private static readonly Color ItemColor     = new Color(0.95f, 0.78f, 0.50f); // pastel amber
    private static readonly Color QuestColor    = new Color(0.50f, 0.82f, 0.82f); // pastel teal

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
        foreach (Node child in _detailPanel.GetChildren())
            child.QueueFree();

        switch (entityType)
        {
            case "npc":
                var npc = _db.Npcs.Get(entityId);
                if (npc == null) return;
                var npcPane = _npcDetailPaneScene.Instantiate<NpcDetailPane>();
                AddDetailPane(npcPane);
                npcPane.NavigateTo     += ShowDetailPane;
                npcPane.NameChanged    += OnNameChanged;
                npcPane.Deleted        += OnEntityDeleted;
                npcPane.EntityCreated  += (type, _) => { if (type == "faction") LoadFactions(); if (type == "npc") LoadNpcs(); };
                npcPane.Load(npc);
                break;

            case "faction":
                var faction = _db.Factions.Get(entityId);
                if (faction == null) return;
                var facPane = _factionDetailPaneScene.Instantiate<FactionDetailPane>();
                AddDetailPane(facPane);
                facPane.NavigateTo    += ShowDetailPane;
                facPane.NameChanged  += OnNameChanged;
                facPane.Deleted      += OnEntityDeleted;
                facPane.EntityCreated += (type, _) => { if (type == "npc") LoadNpcs(); if (type == "faction") LoadFactions(); };
                facPane.Load(faction);
                break;

            case "location":
                var location = _db.Locations.Get(entityId);
                if (location == null) return;
                var locPane = _locationDetailPaneScene.Instantiate<LocationDetailPane>();
                AddDetailPane(locPane);
                locPane.NavigateTo           += ShowDetailPane;
                locPane.NameChanged          += OnNameChanged;
                locPane.Deleted              += OnEntityDeleted;
                locPane.SubLocationAdded     += (_, __)     => LoadLocations();
                locPane.ParentLocationChanged += _           => LoadLocations();
                locPane.EntityCreated         += (type, id) =>
                {
                    if (type == "faction")  LoadFactions();
                    if (type == "location") LoadLocations();
                };
                locPane.Load(location);
                break;

            case "session":
                var session = _db.Sessions.Get(entityId);
                if (session == null) return;
                var sesPane = _sessionDetailPaneScene.Instantiate<SessionDetailPane>();
                AddDetailPane(sesPane);
                sesPane.NavigateTo    += ShowDetailPane;
                sesPane.NameChanged   += OnNameChanged;
                sesPane.Deleted       += OnEntityDeleted;
                sesPane.EntityCreated += (type, _) =>
                {
                    if (type == "npc")      LoadNpcs();
                    if (type == "faction")  LoadFactions();
                    if (type == "location") LoadLocations();
                    if (type == "item")     LoadItems();
                    if (type == "quest")    LoadQuests();
                };
                sesPane.Load(session);
                break;

            case "item":
                var item = _db.Items.Get(entityId);
                if (item == null) return;
                var itemPane = _itemDetailPaneScene.Instantiate<ItemDetailPane>();
                AddDetailPane(itemPane);
                itemPane.NavigateTo    += ShowDetailPane;
                itemPane.NameChanged   += OnNameChanged;
                itemPane.Deleted       += OnEntityDeleted;
                itemPane.EntityCreated += (type, _) =>
                {
                    if (type == "npc")      LoadNpcs();
                    if (type == "faction")  LoadFactions();
                    if (type == "location") LoadLocations();
                    if (type == "item")     LoadItems();
                    if (type == "quest")    LoadQuests();
                };
                itemPane.Load(item);
                break;

            case "quest":
                var quest = _db.Quests.Get(entityId);
                if (quest == null) return;
                var questPane = _questDetailPaneScene.Instantiate<QuestDetailPane>();
                AddDetailPane(questPane);
                questPane.NavigateTo    += ShowDetailPane;
                questPane.NameChanged   += OnNameChanged;
                questPane.Deleted       += OnEntityDeleted;
                questPane.EntityCreated += (type, _) =>
                {
                    if (type == "npc")      LoadNpcs();
                    if (type == "faction")  LoadFactions();
                    if (type == "location") LoadLocations();
                    if (type == "item")     LoadItems();
                    if (type == "quest")    LoadQuests();
                };
                questPane.Load(quest);
                break;
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
        foreach (Node child in _detailPanel.GetChildren())
            child.QueueFree();
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
    }

    private void AddDetailPane(Control pane)
    {
        pane.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pane.SizeFlagsVertical   = SizeFlags.ExpandFill;
        _detailPanel.AddChild(pane);
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