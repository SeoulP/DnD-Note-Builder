using Godot;

public partial class CampaignDashboard : Control
{
    private int _campaignId;
    private DatabaseService _databaseService;

    [Export] private Button _addFactionButton;
    [Export] private Button _addNpcsButton;
    [Export] private Button _addSessionsButton;
    [Export] private Button _addLocationsButton;

    [Export] private Control _factionsFoldablePanel;
    [Export] private Control _npcsFoldablePanel;
    [Export] private Control _sessionsFoldablePanel;
    [Export] private Control _locationsFoldablePanel;

    [Export] private VBoxContainer _npcsContainer;
    [Export] private VBoxContainer _factionsContainer;
    [Export] private VBoxContainer _locationsContainer;
    [Export] private VBoxContainer _sessionsContainer;

    [Export] private AddNpcModal _addNpcModal;
    [Export] private AddFactionModal _addFactionModal;
    [Export] private AddLocationModal _addLocationModal;
    [Export] private AddSessionModal _addSessionModal;

    public override void _Ready()
    {
        _databaseService = GetNode<DatabaseService>("/root/DatabaseService");

        _addNpcsButton.Pressed      += () => _addNpcModal.OpenForNew(_campaignId);
        _addFactionButton.Pressed   += () => _addFactionModal.OpenForNew(_campaignId);
        _addLocationsButton.Pressed += () => _addLocationModal.OpenForNew(_campaignId);
        _addSessionsButton.Pressed  += () => _addSessionModal.OpenForNew(_campaignId);

        _addNpcModal.NpcCreated          += _ => LoadNpcs();
        _addFactionModal.FactionCreated  += _ => LoadFactions();
        _addLocationModal.LocationCreated += _ => LoadLocations();
        _addSessionModal.SessionCreated  += _ => LoadSessions();

        _addNpcModal.Hide();
        _addFactionModal.Hide();
        _addLocationModal.Hide();
        _addSessionModal.Hide();

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
        foreach (var npc in _databaseService.Npcs.GetAll(_campaignId))
            _npcsContainer.AddChild(new Label { Text = npc.Name });
    }

    private void LoadFactions()
    {
        ClearItems(_factionsContainer, _addFactionButton);
        foreach (var faction in _databaseService.Factions.GetAll(_campaignId))
            _factionsContainer.AddChild(new Label { Text = faction.Name });
    }

    private void LoadLocations()
    {
        ClearItems(_locationsContainer, _addLocationsButton);
        foreach (var location in _databaseService.Locations.GetAll(_campaignId))
            _locationsContainer.AddChild(new Label { Text = location.Name });
    }

    private void LoadSessions()
    {
        ClearItems(_sessionsContainer, _addSessionsButton);
        foreach (var session in _databaseService.Sessions.GetAll(_campaignId))
            _sessionsContainer.AddChild(new Label { Text = $"#{session.Number:D3} – {session.Title}" });
    }

    private static void ClearItems(VBoxContainer container, Button keepButton)
    {
        foreach (Node child in container.GetChildren())
            if (child != keepButton) child.QueueFree();
    }
}