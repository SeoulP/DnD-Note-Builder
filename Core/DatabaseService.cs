using Godot;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Repositories;

public partial class DatabaseService : Node
{
    private SqliteConnection _conn;

    public CampaignRepository Campaigns  { get; private set; }
    public SessionRepository  Sessions   { get; private set; }
    public LocationRepository Locations  { get; private set; }
    public FactionRepository  Factions   { get; private set; }
    public NpcRepository      Npcs       { get; private set; }

    public override void _Ready()
    {
        var path = OS.GetUserDataDir() + "/campaign.db";
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();

        Campaigns = new CampaignRepository(_conn);
        Sessions  = new SessionRepository(_conn);
        Locations = new LocationRepository(_conn);
        Factions  = new FactionRepository(_conn);
        Npcs      = new NpcRepository(_conn);

        RunMigrations();
    }

    private void RunMigrations()
    {
        // Order matters — child tables must come after their FK targets
        Campaigns.Migrate();
        Sessions .Migrate();
        Locations.Migrate();
        Factions .Migrate();
        Npcs     .Migrate();   // References factions + locations, so must be last
    }

    public override void _ExitTree()
    {
        _conn?.Close();
    }
}
