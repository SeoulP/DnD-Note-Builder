using Godot;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Repositories;

public partial class DatabaseService : Node
{
    private SqliteConnection _conn;

    public CampaignRepository             Campaigns             { get; private set; }
    public SessionRepository              Sessions              { get; private set; }
    public FactionRepository              Factions              { get; private set; }
    public SpeciesRepository              Species               { get; private set; }
    public LocationFactionRoleRepository  LocationFactionRoles  { get; private set; }
    public LocationRepository             Locations             { get; private set; }
    public NpcRelationshipTypeRepository  NpcRelationshipTypes  { get; private set; }
    public NpcStatusRepository            NpcStatuses           { get; private set; }
    public NpcRepository                  Npcs                  { get; private set; }
    public ItemTypeRepository             ItemTypes             { get; private set; }
    public ItemRepository                 Items                 { get; private set; }

    public override void _Ready()
    {
        var path = OS.GetUserDataDir() + "/campaign.db";
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();

        Campaigns            = new CampaignRepository(_conn);
        Sessions             = new SessionRepository(_conn);
        Factions             = new FactionRepository(_conn);
        Species              = new SpeciesRepository(_conn);
        LocationFactionRoles = new LocationFactionRoleRepository(_conn);
        Locations            = new LocationRepository(_conn);
        NpcRelationshipTypes = new NpcRelationshipTypeRepository(_conn);
        NpcStatuses          = new NpcStatusRepository(_conn);
        Npcs                 = new NpcRepository(_conn);
        ItemTypes            = new ItemTypeRepository(_conn);
        Items                = new ItemRepository(_conn);

        RunMigrations();
    }

    private void RunMigrations()
    {
        // Order matters — each table must be created after all tables it references
        Campaigns           .Migrate();  // top-level container
        Sessions            .Migrate();  // references campaigns
        Factions            .Migrate();  // references campaigns
        Species             .Migrate();  // references campaigns
        LocationFactionRoles.Migrate();  // references campaigns; must precede Locations (location_factions FK)
        Locations           .Migrate();  // references campaigns, factions, location_faction_roles
        NpcRelationshipTypes.Migrate();  // references campaigns; must precede Npcs
        NpcStatuses         .Migrate();  // references campaigns; must precede Npcs
        Npcs                .Migrate();  // references campaigns, species, locations, factions, npc_relationship_types, npc_statuses
        ItemTypes           .Migrate();  // references campaigns; must precede Items
        Items               .Migrate();  // references campaigns, item_types, characters, locations
    }

    public override void _ExitTree()
    {
        _conn?.Close();
    }
}