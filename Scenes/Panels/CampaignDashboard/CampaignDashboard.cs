using System;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class CampaignDashboard : Control
{
    private int _campaignId;
    private DatabaseService _db;

    [Export] private Button _addNpcsButton;
    [Export] private Button _addFactionButton;
    [Export] private Button _addLocationsButton;
    [Export] private Button _addSessionsButton;
    [Export] private Button _addItemsButton;

    [Export] private VBoxContainer _npcsContainer;
    [Export] private VBoxContainer _factionsContainer;
    [Export] private VBoxContainer _locationsContainer;
    [Export] private VBoxContainer _sessionsContainer;
    [Export] private VBoxContainer _itemsContainer;

    [Export] private Control _detailPanel;

    [Export] private PackedScene _npcDetailPaneScene;
    [Export] private PackedScene _factionDetailPaneScene;
    [Export] private PackedScene _locationDetailPaneScene;
    [Export] private PackedScene _sessionDetailPaneScene;
    [Export] private PackedScene _itemDetailPaneScene;

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

        StyleAddButton(_addNpcsButton,      NpcColor);
        StyleAddButton(_addFactionButton,   FactionColor);
        StyleAddButton(_addLocationsButton, LocationColor);
        StyleAddButton(_addSessionsButton,  SessionColor);
        StyleAddButton(_addItemsButton,     ItemColor);

        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/NpcsPanel"),                  NpcColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/FactionsPanel"),              FactionColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/LocationsFoldableContainer"), LocationColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/SessionsPanel"),              SessionColor);
        StyleAccordion(GetNode<Control>("ScrollContainer/VBoxContainer/ItemsPanel"),                 ItemColor);

        GetNode<LineEdit>("ScrollContainer/VBoxContainer/SearchInput").TextChanged += FilterSidebar;

        LoadAll();
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
    }

    public void ReloadSidebar() => LoadAll();

    private void LoadAll()
    {
        LoadNpcs();
        LoadFactions();
        LoadLocations();
        LoadSessions();
        LoadItems();
    }

    private static readonly Color NpcColor      = new Color(0.53f, 0.72f, 0.90f); // pastel blue
    private static readonly Color FactionColor  = new Color(0.90f, 0.58f, 0.58f); // pastel red
    private static readonly Color LocationColor = new Color(0.58f, 0.82f, 0.64f); // pastel green
    private static readonly Color SessionColor  = new Color(0.74f, 0.62f, 0.90f); // pastel purple
    private static readonly Color ItemColor     = new Color(0.95f, 0.78f, 0.50f); // pastel amber

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
        foreach (var location in _db.Locations.GetAll(_campaignId))
        {
            int id = location.Id;
            var btn = MakeSidebarButton(location.Name, LocationColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("location", id);
            _locationsContainer.AddChild(btn);
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

    private static readonly Color DarkText = new Color(0.10f, 0.10f, 0.10f);

    private static Button MakeSidebarButton(string text, Color color)
    {
        var btn = new Button
        {
            Text                = text,
            Flat                = false,
            Alignment           = HorizontalAlignment.Left,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        ApplyButtonStyle(btn, color);
        return btn;
    }

    private static void ApplyButtonStyle(Button btn, Color color)
    {
        var normal  = MakeBox(color,                padding: 2);
        var hover   = MakeBox(color.Lightened(0.12f), padding: 2);
        var pressed = MakeBox(color.Darkened(0.12f),  padding: 2);
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

    private static StyleBoxFlat MakeBox(Color color, int padding = 0)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(3);
        if (padding > 0)
        {
            box.ContentMarginLeft   = padding;
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
                locPane.NavigateTo      += ShowDetailPane;
                locPane.NameChanged     += OnNameChanged;
                locPane.Deleted         += OnEntityDeleted;
                locPane.SubLocationAdded += (_, __)     => LoadLocations();
                locPane.EntityCreated    += (type, id) =>
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
                sesPane.NavigateTo  += ShowDetailPane;
                sesPane.NameChanged += OnNameChanged;
                sesPane.Deleted     += OnEntityDeleted;
                sesPane.Load(session);
                break;

            case "item":
                var item = _db.Items.Get(entityId);
                if (item == null) return;
                var itemPane = _itemDetailPaneScene.Instantiate<ItemDetailPane>();
                AddDetailPane(itemPane);
                itemPane.NavigateTo  += ShowDetailPane;
                itemPane.NameChanged += OnNameChanged;
                itemPane.Deleted     += OnEntityDeleted;
                itemPane.Load(item);
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
            _          => null
        };
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            if (child is Button btn && btn.HasMeta("id") && btn.GetMeta("id").AsInt32() == entityId)
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