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

    [Export] private VBoxContainer _npcsContainer;
    [Export] private VBoxContainer _factionsContainer;
    [Export] private VBoxContainer _locationsContainer;
    [Export] private VBoxContainer _sessionsContainer;

    [Export] private Control _detailPanel;

    [Export] private PackedScene _npcDetailPaneScene;
    [Export] private PackedScene _factionDetailPaneScene;
    [Export] private PackedScene _locationDetailPaneScene;
    [Export] private PackedScene _sessionDetailPaneScene;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

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
            var session = new Session { CampaignId = _campaignId, Number = existing.Count + 1, Title = "New Session" };
            int id = _db.Sessions.Add(session);
            LoadSessions();
            ShowDetailPane("session", id);
        };

        LoadAll();
    }

    public void SetCampaign(int campaignId)
    {
        _campaignId = campaignId;
    }

    private void LoadAll()
    {
        LoadNpcs();
        LoadFactions();
        LoadLocations();
        LoadSessions();
    }

    private void LoadNpcs()
    {
        ClearItems(_npcsContainer, _addNpcsButton);
        foreach (var npc in _db.Npcs.GetAll(_campaignId))
        {
            int id = npc.Id;
            var btn = new Button { Text = npc.Name, Flat = true, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
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
            var btn = new Button { Text = faction.Name, Flat = true, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
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
            var btn = new Button { Text = location.Name, Flat = true, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
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
            var btn = new Button { Text = $"#{session.Number:D3} – {session.Title}", Flat = true, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            btn.SetMeta("id", id);
            btn.Pressed += () => ShowDetailPane("session", id);
            _sessionsContainer.AddChild(btn);
        }
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
                npcPane.NavigateTo  += ShowDetailPane;
                npcPane.NameChanged += OnNameChanged;
                npcPane.Deleted     += OnEntityDeleted;
                npcPane.Load(npc);
                break;

            case "faction":
                var faction = _db.Factions.Get(entityId);
                if (faction == null) return;
                var facPane = _factionDetailPaneScene.Instantiate<FactionDetailPane>();
                AddDetailPane(facPane);
                facPane.NavigateTo  += ShowDetailPane;
                facPane.NameChanged += OnNameChanged;
                facPane.Deleted     += OnEntityDeleted;
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
                locPane.SubLocationAdded += (parentId, newId) => { LoadLocations(); ShowDetailPane("location", newId); };
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
}