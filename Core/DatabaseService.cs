using Godot;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;
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
    public NpcFactionRoleRepository            NpcFactionRoles            { get; private set; }
    public FactionRelationshipTypeRepository  FactionRelationshipTypes   { get; private set; }
    public CharacterRelationshipTypeRepository CharacterRelationshipTypes { get; private set; }
    public NpcRepository                  Npcs                  { get; private set; }
    public ItemTypeRepository             ItemTypes             { get; private set; }
    public ItemRepository                 Items                 { get; private set; }
    public EntityImageRepository          EntityImages          { get; private set; }
    public QuestStatusRepository          QuestStatuses         { get; private set; }
    public QuestRepository                Quests                { get; private set; }
    public QuestHistoryRepository         QuestHistory          { get; private set; }
    public SettingsRepository             Settings              { get; private set; }

    public string DbPath { get; private set; }

    public override void _Ready()
    {
        DbPath = OS.GetUserDataDir() + "/campaign.db";
        InitConnection();
    }

    public void Reconnect()
    {
        _conn?.Close();
        InitConnection();
    }

    private void InitConnection()
    {
        _conn = new SqliteConnection($"Data Source={DbPath}");
        _conn.Open();

        Campaigns            = new CampaignRepository(_conn);
        Sessions             = new SessionRepository(_conn);
        Factions             = new FactionRepository(_conn);
        Species              = new SpeciesRepository(_conn);
        LocationFactionRoles = new LocationFactionRoleRepository(_conn);
        Locations            = new LocationRepository(_conn);
        NpcRelationshipTypes = new NpcRelationshipTypeRepository(_conn);
        NpcStatuses          = new NpcStatusRepository(_conn);
        NpcFactionRoles            = new NpcFactionRoleRepository(_conn);
        FactionRelationshipTypes   = new FactionRelationshipTypeRepository(_conn);
        CharacterRelationshipTypes = new CharacterRelationshipTypeRepository(_conn);
        Npcs                       = new NpcRepository(_conn);
        ItemTypes            = new ItemTypeRepository(_conn);
        Items                = new ItemRepository(_conn);
        EntityImages         = new EntityImageRepository(_conn);
        QuestStatuses        = new QuestStatusRepository(_conn);
        Quests               = new QuestRepository(_conn);
        QuestHistory         = new QuestHistoryRepository(_conn);
        Settings             = new SettingsRepository(_conn);

        RunMigrations();
    }

    private void RunMigrations()
    {
        // Order matters — each table must be created after all tables it references
        Settings            .Migrate();  // global app settings — no FK dependencies
        Campaigns           .Migrate();  // top-level container
        Sessions            .Migrate();  // references campaigns
        Factions            .Migrate();  // references campaigns
        FactionRelationshipTypes.Migrate();  // references campaigns; must precede faction_relationships
        Species             .Migrate();  // references campaigns
        LocationFactionRoles.Migrate();  // references campaigns; must precede Locations (location_factions FK)
        Locations           .Migrate();  // references campaigns, factions, location_faction_roles
        NpcRelationshipTypes.Migrate();  // references campaigns; must precede Npcs
        NpcStatuses         .Migrate();  // references campaigns; must precede Npcs
        NpcFactionRoles     .Migrate();  // references campaigns; must precede Npcs (character_factions FK)
        CharacterRelationshipTypes.Migrate(); // references campaigns; must precede character_relationships
        Npcs                .Migrate();  // references campaigns, species, locations, factions, npc_relationship_types, npc_statuses, npc_faction_roles, character_relationship_types
        ItemTypes           .Migrate();  // references campaigns; must precede Items
        Items               .Migrate();  // references campaigns, item_types, characters, locations
        EntityImages        .Migrate();  // polymorphic; no FK constraints — references any entity by type+id
        QuestStatuses       .Migrate();  // references campaigns; must precede Quests
        Quests              .Migrate();  // references campaigns, quest_statuses, characters, locations
        QuestHistory        .Migrate();  // references quests, sessions

        MigrateLegacyPortraits();

        // Ensure every campaign has all current seed defaults (idempotent — skips names that already exist)
        SeedAllCampaigns();
    }

    private void SeedAllCampaigns()
    {
        foreach (var campaign in Campaigns.GetAll())
        {
            Species             .SeedDefaults(campaign.Id);
            LocationFactionRoles.SeedDefaults(campaign.Id);
            NpcRelationshipTypes.SeedDefaults(campaign.Id);
            NpcStatuses         .SeedDefaults(campaign.Id);
            NpcFactionRoles           .SeedDefaults(campaign.Id);
            FactionRelationshipTypes  .SeedDefaults(campaign.Id);
            CharacterRelationshipTypes.SeedDefaults(campaign.Id);
            ItemTypes                 .SeedDefaults(campaign.Id);
            QuestStatuses             .SeedDefaults(campaign.Id);
        }
    }

    // One-time migration: move any legacy portrait_path strings from the characters table
    // into entity_images rows. Safe to run every startup — MigrateLegacyPortrait no-ops if
    // a row already exists for that NPC.
    private void MigrateLegacyPortraits()
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, portrait_path FROM characters WHERE portrait_path != ''";
        using var reader = cmd.ExecuteReader();
        var rows = new System.Collections.Generic.List<(int id, string path)>();
        while (reader.Read())
            rows.Add((reader.GetInt32(0), reader.GetString(1)));
        reader.Close();

        foreach (var (id, path) in rows)
            EntityImages.MigrateLegacyPortrait(EntityType.Npc, id, path);
    }

    public override void _ExitTree()
    {
        _conn?.Close();
    }
}