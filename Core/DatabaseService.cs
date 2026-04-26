using System;
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
    public ClassRepository                Classes               { get; private set; }
    public AbilityRepository              Abilities             { get; private set; }
    public AbilityTypeRepository          AbilityTypes          { get; private set; }
    public AbilityResourceTypeRepository  AbilityResourceTypes  { get; private set; }
    public SubspeciesRepository           Subspecies            { get; private set; }
    public PlayerCharacterRepository      PlayerCharacters      { get; private set; }
    public DnD5eSkillRepository           DnD5eSkills           { get; private set; }
    public DnD5eBackgroundRepository      DnD5eBackgrounds      { get; private set; }
    public DnD5eCharacterSkillRepository  DnD5eCharacterSkills  { get; private set; }
    public EntityAliasRepository          EntityAliases         { get; private set; }

    // ── P2e Global Lookups (no campaign_id) ──────────────────────────────────
    public Pf2eAbilityScoreRepository     Pf2eAbilityScores     { get; private set; }
    public Pf2eAbilityTypeRepository      Pf2eAbilityTypes      { get; private set; }
    public Pf2eActionCostRepository       Pf2eActionCosts       { get; private set; }
    public Pf2eSaveTypeRepository         Pf2eSaveTypes         { get; private set; }
    public Pf2eDieTypeRepository          Pf2eDieTypes          { get; private set; }
    public Pf2eSpellFrequencyRepository   Pf2eSpellFrequencies  { get; private set; }
    public Pf2eAreaTypeRepository         Pf2eAreaTypes         { get; private set; }
    public Pf2eSizeRepository             Pf2eSizes             { get; private set; }
    public Pf2eProficiencyRankRepository  Pf2eProficiencyRanks  { get; private set; }
    public Pf2eAttackCategoryRepository   Pf2eAttackCategories  { get; private set; }

    // ── P2e Campaign-Scoped Lookups ───────────────────────────────────────────
    public Pf2eTraditionRepository        Pf2eTraditions        { get; private set; }
    public Pf2eCreatureTypeRepository     Pf2eCreatureTypes     { get; private set; }
    public Pf2eDamageTypeRepository       Pf2eDamageTypes       { get; private set; }
    public Pf2eConditionTypeRepository    Pf2eConditionTypes    { get; private set; }
    public Pf2eTraitTypeRepository        Pf2eTraitTypes        { get; private set; }
    public Pf2eSenseTypeRepository        Pf2eSenseTypes        { get; private set; }
    public Pf2eSkillTypeRepository        Pf2eSkillTypes        { get; private set; }
    public Pf2eLanguageTypeRepository     Pf2eLanguageTypes     { get; private set; }
    public Pf2eMovementTypeRepository     Pf2eMovementTypes     { get; private set; }
    public Pf2eFeatTypeRepository         Pf2eFeatTypes         { get; private set; }

    // ── P2e Character Building ────────────────────────────────────────────────
    public Pf2eAncestryRepository              Pf2eAncestries             { get; private set; }
    public Pf2eAncestryFeatureRepository       Pf2eAncestryFeatures       { get; private set; }
    public Pf2eAncestryAbilityBoostRepository  Pf2eAncestryAbilityBoosts  { get; private set; }
    public Pf2eHeritageRepository              Pf2eHeritages              { get; private set; }
    public Pf2eBackgroundRepository            Pf2eBackgrounds            { get; private set; }
    public Pf2eClassRepository                 Pf2eClasses                { get; private set; }
    public Pf2eClassFeatureRepository          Pf2eClassFeatures          { get; private set; }
    public Pf2eArchetypeRepository             Pf2eArchetypes             { get; private set; }
    public Pf2eArchetypeFeatureRepository      Pf2eArchetypeFeatures      { get; private set; }
    public Pf2eFeatRepository                  Pf2eFeats                  { get; private set; }

    // ── P2e PC Sheet ──────────────────────────────────────────────────────────
    public Pf2eCharacterRepository             Pf2eCharacters             { get; private set; }
    public Pf2eCharacterSkillRepository        Pf2eCharacterSkills        { get; private set; }
    public Pf2eCharacterSaveRepository         Pf2eCharacterSaves         { get; private set; }
    public Pf2eCharacterAttackRepository       Pf2eCharacterAttacks       { get; private set; }
    public Pf2eCharacterFeatRepository         Pf2eCharacterFeats         { get; private set; }
    public Pf2eCharacterConditionRepository    Pf2eCharacterConditions    { get; private set; }
    public Pf2eCharacterTraitRepository        Pf2eCharacterTraits        { get; private set; }

    // ── P2e Creatures ─────────────────────────────────────────────────────────
    public Pf2eCreatureRepository              Pf2eCreatures              { get; private set; }
    public Pf2eCreatureSpeedRepository         Pf2eCreatureSpeeds         { get; private set; }
    public Pf2eCreatureLanguageRepository      Pf2eCreatureLanguages      { get; private set; }
    public Pf2eCreatureSenseRepository         Pf2eCreatureSenses         { get; private set; }
    public Pf2eCreatureSkillRepository         Pf2eCreatureSkills         { get; private set; }
    public Pf2eCreatureTraitRepository         Pf2eCreatureTraits         { get; private set; }
    public Pf2eCreatureImmunityRepository      Pf2eCreatureImmunities     { get; private set; }
    public Pf2eCreatureResistanceRepository    Pf2eCreatureResistances    { get; private set; }
    public Pf2eCreatureWeaknessRepository      Pf2eCreatureWeaknesses     { get; private set; }
    public Pf2eCreatureAbilityRepository       Pf2eCreatureAbilities      { get; private set; }
    public Pf2eAbilityTraitRepository          Pf2eAbilityTraits          { get; private set; }
    public Pf2eStrikeDamageRepository          Pf2eStrikeDamage           { get; private set; }
    public Pf2eStrikeConditionRepository       Pf2eStrikeConditions       { get; private set; }
    public Pf2eAbilityVariableActionRepository Pf2eAbilityVariableActions { get; private set; }
    public Pf2eInnateSpellRepository           Pf2eInnateSpells           { get; private set; }

    // ── P2e PC Strikes (Battle Tracker) ──────────────────────────────────────
    public Pf2eCharacterStrikeRepository       Pf2eCharacterStrikes       { get; private set; }
    public Pf2eCharacterStrikeDamageRepository Pf2eCharacterStrikeDamage  { get; private set; }
    public Pf2eCharacterStrikeTraitRepository  Pf2eCharacterStrikeTraits  { get; private set; }

    // ── Encounters (Battle Tracker) ───────────────────────────────────────────
    public EncounterRepository                           Encounters                       { get; private set; }
    public Pf2eEncounterCombatantRepository              Pf2eEncounterCombatants          { get; private set; }
    public Pf2eEncounterCombatantHpLogRepository         Pf2eEncounterCombatantHpLog      { get; private set; }
    public Pf2eEncounterCombatantConditionRepository     Pf2eEncounterCombatantConditions { get; private set; }

    public string DbPath { get; private set; }
    public string ImgDir { get; private set; }

    public override void _Ready()
    {
        string appDir = OS.HasFeature("editor")
            ? OS.GetUserDataDir()
            : System.IO.Path.GetDirectoryName(OS.GetExecutablePath());
        DbPath = System.IO.Path.Combine(appDir, "campaign.db");
        ImgDir = System.IO.Path.Combine(appDir, "img");
        System.IO.Directory.CreateDirectory(ImgDir);
        InitConnection();

        AppLogger.Instance.SetLogDirectory(System.IO.Path.GetDirectoryName(DbPath));
        if (System.Enum.TryParse<LogLevel>(Settings.Get("log_level", "Info"), ignoreCase: true, out var logLevel))
            AppLogger.Instance.SetMinLevel(logLevel);
    }

    public void Reconnect()
    {
        _conn?.Close();
        InitConnection();
    }

    public void Disconnect()
    {
        _conn?.Close();
        SqliteConnection.ClearAllPools(); // flush connection pool so the file lock is actually released
        _conn = null;
    }

    private void InitConnection()
    {
        _conn = new SqliteConnection($"Data Source={DbPath}");
        _conn.Open();

        using (var pragma = _conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }

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
        Classes              = new ClassRepository(_conn);
        Abilities            = new AbilityRepository(_conn);
        AbilityTypes         = new AbilityTypeRepository(_conn);
        AbilityResourceTypes = new AbilityResourceTypeRepository(_conn);
        Subspecies           = new SubspeciesRepository(_conn);
        PlayerCharacters     = new PlayerCharacterRepository(_conn);
        DnD5eSkills          = new DnD5eSkillRepository(_conn);
        DnD5eBackgrounds     = new DnD5eBackgroundRepository(_conn);
        DnD5eCharacterSkills = new DnD5eCharacterSkillRepository(_conn);
        EntityAliases        = new EntityAliasRepository(_conn);

        // ── P2e Global Lookups ────────────────────────────────────────────────
        Pf2eAbilityScores    = new Pf2eAbilityScoreRepository(_conn);
        Pf2eAbilityTypes     = new Pf2eAbilityTypeRepository(_conn);
        Pf2eActionCosts      = new Pf2eActionCostRepository(_conn);
        Pf2eSaveTypes        = new Pf2eSaveTypeRepository(_conn);
        Pf2eDieTypes         = new Pf2eDieTypeRepository(_conn);
        Pf2eSpellFrequencies = new Pf2eSpellFrequencyRepository(_conn);
        Pf2eAreaTypes        = new Pf2eAreaTypeRepository(_conn);
        Pf2eSizes            = new Pf2eSizeRepository(_conn);
        Pf2eProficiencyRanks = new Pf2eProficiencyRankRepository(_conn);
        Pf2eAttackCategories = new Pf2eAttackCategoryRepository(_conn);

        // ── P2e Campaign-Scoped Lookups ───────────────────────────────────────
        Pf2eTraditions       = new Pf2eTraditionRepository(_conn);
        Pf2eCreatureTypes    = new Pf2eCreatureTypeRepository(_conn);
        Pf2eDamageTypes      = new Pf2eDamageTypeRepository(_conn);
        Pf2eConditionTypes   = new Pf2eConditionTypeRepository(_conn);
        Pf2eTraitTypes       = new Pf2eTraitTypeRepository(_conn);
        Pf2eSenseTypes       = new Pf2eSenseTypeRepository(_conn);
        Pf2eSkillTypes       = new Pf2eSkillTypeRepository(_conn);
        Pf2eLanguageTypes    = new Pf2eLanguageTypeRepository(_conn);
        Pf2eMovementTypes    = new Pf2eMovementTypeRepository(_conn);
        Pf2eFeatTypes        = new Pf2eFeatTypeRepository(_conn);

        // ── P2e Character Building ────────────────────────────────────────────
        Pf2eAncestries            = new Pf2eAncestryRepository(_conn);
        Pf2eAncestryFeatures      = new Pf2eAncestryFeatureRepository(_conn);
        Pf2eAncestryAbilityBoosts = new Pf2eAncestryAbilityBoostRepository(_conn);
        Pf2eHeritages             = new Pf2eHeritageRepository(_conn);
        Pf2eBackgrounds           = new Pf2eBackgroundRepository(_conn);
        Pf2eClasses               = new Pf2eClassRepository(_conn);
        Pf2eClassFeatures         = new Pf2eClassFeatureRepository(_conn);
        Pf2eArchetypes            = new Pf2eArchetypeRepository(_conn);
        Pf2eArchetypeFeatures     = new Pf2eArchetypeFeatureRepository(_conn);
        Pf2eFeats                 = new Pf2eFeatRepository(_conn);

        // ── P2e PC Sheet ──────────────────────────────────────────────────────
        Pf2eCharacters          = new Pf2eCharacterRepository(_conn);
        Pf2eCharacterSkills     = new Pf2eCharacterSkillRepository(_conn);
        Pf2eCharacterSaves      = new Pf2eCharacterSaveRepository(_conn);
        Pf2eCharacterAttacks    = new Pf2eCharacterAttackRepository(_conn);
        Pf2eCharacterFeats      = new Pf2eCharacterFeatRepository(_conn);
        Pf2eCharacterConditions = new Pf2eCharacterConditionRepository(_conn);
        Pf2eCharacterTraits     = new Pf2eCharacterTraitRepository(_conn);

        // ── P2e Creatures ─────────────────────────────────────────────────────
        Pf2eCreatures              = new Pf2eCreatureRepository(_conn);
        Pf2eCreatureSpeeds         = new Pf2eCreatureSpeedRepository(_conn);
        Pf2eCreatureLanguages      = new Pf2eCreatureLanguageRepository(_conn);
        Pf2eCreatureSenses         = new Pf2eCreatureSenseRepository(_conn);
        Pf2eCreatureSkills         = new Pf2eCreatureSkillRepository(_conn);
        Pf2eCreatureTraits         = new Pf2eCreatureTraitRepository(_conn);
        Pf2eCreatureImmunities     = new Pf2eCreatureImmunityRepository(_conn);
        Pf2eCreatureResistances    = new Pf2eCreatureResistanceRepository(_conn);
        Pf2eCreatureWeaknesses     = new Pf2eCreatureWeaknessRepository(_conn);
        Pf2eCreatureAbilities      = new Pf2eCreatureAbilityRepository(_conn);
        Pf2eAbilityTraits          = new Pf2eAbilityTraitRepository(_conn);
        Pf2eStrikeDamage           = new Pf2eStrikeDamageRepository(_conn);
        Pf2eStrikeConditions       = new Pf2eStrikeConditionRepository(_conn);
        Pf2eAbilityVariableActions = new Pf2eAbilityVariableActionRepository(_conn);
        Pf2eInnateSpells           = new Pf2eInnateSpellRepository(_conn);

        // ── P2e PC Strikes ────────────────────────────────────────────────────
        Pf2eCharacterStrikes      = new Pf2eCharacterStrikeRepository(_conn);
        Pf2eCharacterStrikeDamage = new Pf2eCharacterStrikeDamageRepository(_conn);
        Pf2eCharacterStrikeTraits = new Pf2eCharacterStrikeTraitRepository(_conn);

        // ── Encounters ────────────────────────────────────────────────────────
        Pf2eEncounterCombatantHpLog      = new Pf2eEncounterCombatantHpLogRepository(_conn);
        Pf2eEncounterCombatants          = new Pf2eEncounterCombatantRepository(_conn, Pf2eEncounterCombatantHpLog);
        Pf2eEncounterCombatantConditions = new Pf2eEncounterCombatantConditionRepository(_conn);
        Encounters                       = new EncounterRepository(_conn);

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
        Classes             .Migrate();  // references campaigns; creates classes + subclasses
        Subspecies          .Migrate();  // references campaigns, species
        AbilityTypes        .Migrate();  // references campaigns
        AbilityResourceTypes.Migrate();  // references campaigns
        Abilities           .Migrate();  // references campaigns, classes, subclasses, species, subspecies, characters
        PlayerCharacters    .Migrate();  // additive columns on player_characters; references classes, subclasses, subspecies
        PlayerCharacters    .MigrateResources();  // character_resources — references ability_resource_types; must run after AbilityResourceTypes
        DnD5eSkills         .Migrate();  // references campaigns
        DnD5eBackgrounds    .Migrate();  // references campaigns; background_id FK on player_characters resolves at runtime
        DnD5eCharacterSkills.Migrate();  // references player_characters, dnd5e_skills
        EntityAliases       .Migrate();  // references campaigns; no other FK dependencies

        // ── P2e Global (no campaign_id — seeded once here) ───────────────────
        Pf2eAbilityScores   .Migrate();   Pf2eAbilityScores   .SeedDefaults();
        Pf2eAbilityTypes    .Migrate();   Pf2eAbilityTypes    .SeedDefaults();
        Pf2eActionCosts     .Migrate();   Pf2eActionCosts     .SeedDefaults();
        Pf2eSaveTypes       .Migrate();   Pf2eSaveTypes       .SeedDefaults();
        Pf2eDieTypes        .Migrate();   Pf2eDieTypes        .SeedDefaults();
        Pf2eSpellFrequencies.Migrate();   Pf2eSpellFrequencies.SeedDefaults();
        Pf2eAreaTypes       .Migrate();   Pf2eAreaTypes       .SeedDefaults();
        Pf2eSizes           .Migrate();   Pf2eSizes           .SeedDefaults();
        Pf2eProficiencyRanks.Migrate();   Pf2eProficiencyRanks.SeedDefaults();
        Pf2eAttackCategories.Migrate();   Pf2eAttackCategories.SeedDefaults();

        // ── P2e Campaign-scoped lookups ───────────────────────────────────────
        Pf2eTraditions    .Migrate();  // references campaigns
        Pf2eCreatureTypes .Migrate();  // references campaigns
        Pf2eDamageTypes   .Migrate();  // references campaigns
        Pf2eConditionTypes.Migrate();  // references campaigns
        Pf2eTraitTypes    .Migrate();  // references campaigns
        Pf2eSenseTypes    .Migrate();  // references campaigns
        Pf2eSkillTypes    .Migrate();  // references campaigns, pathfinder_ability_scores
        Pf2eLanguageTypes .Migrate();  // references campaigns
        Pf2eMovementTypes .Migrate();  // references campaigns
        Pf2eFeatTypes     .Migrate();  // references campaigns

        // ── P2e Character building ────────────────────────────────────────────
        Pf2eAncestries           .Migrate();  // references campaigns, pathfinder_sizes
        Pf2eAncestryFeatures     .Migrate();  // references pathfinder_ancestries
        Pf2eAncestryAbilityBoosts.Migrate();  // references pathfinder_ancestries, pathfinder_ability_scores
        Pf2eHeritages            .Migrate();  // references campaigns, pathfinder_ancestries
        Pf2eBackgrounds          .Migrate();  // references campaigns, pathfinder_skill_types, pathfinder_feats (self-ref OK — SQLite defers FK checks)
        Pf2eClasses              .Migrate();  // references campaigns, pathfinder_ability_scores
        Pf2eClassFeatures        .Migrate();  // references pathfinder_classes
        Pf2eArchetypes           .Migrate();  // references campaigns, pathfinder_classes
        Pf2eArchetypeFeatures    .Migrate();  // references pathfinder_archetypes
        Pf2eFeats                .Migrate();  // references campaigns, pathfinder_feat_types, pathfinder_classes, pathfinder_ancestries, pathfinder_action_costs

        // ── P2e Creatures ─────────────────────────────────────────────────────
        Pf2eCreatures             .Migrate();  // references campaigns, pathfinder_creature_types, pathfinder_sizes
        Pf2eCreatureSpeeds        .Migrate();  // references pathfinder_creatures, pathfinder_movement_types
        Pf2eCreatureLanguages     .Migrate();  // references pathfinder_creatures, pathfinder_language_types
        Pf2eCreatureSenses        .Migrate();  // references pathfinder_creatures, pathfinder_sense_types
        Pf2eCreatureSkills        .Migrate();  // references pathfinder_creatures, pathfinder_skill_types
        Pf2eCreatureTraits        .Migrate();  // references pathfinder_creatures, pathfinder_trait_types
        Pf2eCreatureImmunities    .Migrate();  // references pathfinder_creatures, pathfinder_damage_types, pathfinder_condition_types
        Pf2eCreatureResistances   .Migrate();  // references pathfinder_creatures, pathfinder_damage_types
        Pf2eCreatureWeaknesses    .Migrate();  // references pathfinder_creatures, pathfinder_damage_types
        Pf2eCreatureAbilities     .Migrate();  // references pathfinder_creatures, pathfinder_ability_types, pathfinder_action_costs, pathfinder_area_types, pathfinder_traditions
        Pf2eAbilityTraits         .Migrate();  // references pathfinder_creature_abilities, pathfinder_trait_types
        Pf2eStrikeDamage          .Migrate();  // references pathfinder_creature_abilities, pathfinder_damage_types, pathfinder_die_types
        Pf2eStrikeConditions      .Migrate();  // references pathfinder_creature_abilities, pathfinder_condition_types, pathfinder_save_types
        Pf2eAbilityVariableActions.Migrate();  // references pathfinder_creature_abilities, pathfinder_action_costs, pathfinder_die_types, pathfinder_damage_types, pathfinder_save_types, pathfinder_area_types
        Pf2eInnateSpells          .Migrate();  // references pathfinder_creature_abilities, pathfinder_spell_frequencies

        // ── P2e PC sheet ──────────────────────────────────────────────────────
        Pf2eCharacters         .Migrate();  // references characters, pathfinder_ancestries, pathfinder_heritages, pathfinder_backgrounds, pathfinder_classes, pathfinder_archetypes
        Pf2eCharacterSkills    .Migrate();  // references characters, pathfinder_skill_types, pathfinder_proficiency_ranks
        Pf2eCharacterSaves     .Migrate();  // references characters, pathfinder_save_types, pathfinder_proficiency_ranks
        Pf2eCharacterAttacks   .Migrate();  // references characters, pathfinder_attack_categories, pathfinder_proficiency_ranks
        Pf2eCharacterFeats     .Migrate();  // references characters, pathfinder_feats
        Pf2eCharacterConditions.Migrate();  // references characters, pathfinder_condition_types
        Pf2eCharacterTraits    .Migrate();  // references characters, pathfinder_trait_types

        // ── P2e PC strikes (Battle Tracker) ──────────────────────────────────
        Pf2eCharacterStrikes      .Migrate();  // references characters, pathfinder_area_types
        Pf2eCharacterStrikeDamage .Migrate();  // references pathfinder_character_strikes, pathfinder_damage_types, pathfinder_die_types
        Pf2eCharacterStrikeTraits .Migrate();  // references pathfinder_character_strikes, pathfinder_trait_types, pathfinder_die_types, pathfinder_damage_types

        // ── Encounters ────────────────────────────────────────────────────────
        Encounters                      .Migrate();  // references campaigns, sessions
        Pf2eEncounterCombatantHpLog     .Migrate();  // must precede combatants (FK in repo logic)
        Pf2eEncounterCombatants         .Migrate();  // references encounters, characters, pathfinder_creatures
        Pf2eEncounterCombatantConditions.Migrate();  // references pf2e_encounter_combatants, pathfinder_condition_types

        MigrateLegacyPortraits();

        // Ensure every campaign has all current seed defaults (idempotent — skips names that already exist)
        SeedAllCampaigns();
    }

    private void SeedAllCampaigns()
    {
        foreach (var campaign in Campaigns.GetAll())
        {
            Species             .SeedDefaults(campaign.Id);
            Subspecies          .SeedDefaults(campaign.Id);
            DnD5eSkills         .SeedDefaults(campaign.Id);
            DnD5eBackgrounds    .SeedDefaults(campaign.Id);
            Classes             .SeedDefaults(campaign.Id);
            AbilityTypes        .SeedDefaults(campaign.Id);
            AbilityResourceTypes.SeedDefaults(campaign.Id);
            Abilities           .SeedDefaults(campaign.Id);
            DnD5eBackgrounds    .LinkBackgroundFeats(campaign.Id);
            LocationFactionRoles.SeedDefaults(campaign.Id);
            NpcRelationshipTypes.SeedDefaults(campaign.Id);
            NpcStatuses         .SeedDefaults(campaign.Id);
            NpcFactionRoles           .SeedDefaults(campaign.Id);
            FactionRelationshipTypes  .SeedDefaults(campaign.Id);
            CharacterRelationshipTypes.SeedDefaults(campaign.Id);
            ItemTypes                 .SeedDefaults(campaign.Id);
            QuestStatuses             .SeedDefaults(campaign.Id);

            if (campaign.System == "pathfinder2e")
            {
                Pf2eTraditions    .SeedDefaults(campaign.Id);
                Pf2eCreatureTypes .SeedDefaults(campaign.Id);
                Pf2eDamageTypes   .SeedDefaults(campaign.Id);
                Pf2eConditionTypes.SeedDefaults(campaign.Id);
                Pf2eTraitTypes    .SeedDefaults(campaign.Id);
                Pf2eSenseTypes    .SeedDefaults(campaign.Id);
                Pf2eSkillTypes    .SeedDefaults(campaign.Id);
                Pf2eLanguageTypes .SeedDefaults(campaign.Id);
                Pf2eMovementTypes .SeedDefaults(campaign.Id);
                Pf2eFeatTypes     .SeedDefaults(campaign.Id);
            }
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

    /// <summary>
    /// On campaign open: copies any legacy absolute-path images into the managed
    /// img/{campaignName}/ folder and updates the DB to relative paths.
    /// Idempotent — skips rows that are already relative.
    /// </summary>
    public void MigrateLegacyImagePaths(int campaignId)
    {
        string appDir     = System.IO.Path.GetDirectoryName(DbPath);
        var    campaign   = Campaigns.Get(campaignId);
        string subDir     = campaign != null
            ? System.IO.Path.Combine(ImgDir, SanitizeFolderName(campaign.Name))
            : ImgDir;

        var entityGroups = new System.Collections.Generic.Dictionary<DndBuilder.Core.Models.EntityType, System.Collections.Generic.IEnumerable<int>>
        {
            [DndBuilder.Core.Models.EntityType.Faction]  = Factions .GetAll(campaignId).ConvertAll(f => f.Id),
            [DndBuilder.Core.Models.EntityType.Npc]      = Npcs     .GetAll(campaignId).ConvertAll(n => n.Id),
            [DndBuilder.Core.Models.EntityType.Location] = Locations.GetAll(campaignId).ConvertAll(l => l.Id),
            [DndBuilder.Core.Models.EntityType.Session]  = Sessions .GetAll(campaignId).ConvertAll(s => s.Id),
            [DndBuilder.Core.Models.EntityType.Item]     = Items    .GetAll(campaignId).ConvertAll(i => i.Id),
            [DndBuilder.Core.Models.EntityType.Quest]    = Quests   .GetAll(campaignId).ConvertAll(q => q.Id),
        };

        foreach (var (entityType, ids) in entityGroups)
        {
            foreach (var entityId in ids)
            {
                foreach (var img in EntityImages.GetAll(entityType, entityId))
                {
                    if (!System.IO.Path.IsPathRooted(img.Path)) continue; // already relative
                    if (!System.IO.File.Exists(img.Path)) continue;       // file missing — skip silently

                    try
                    {
                        System.IO.Directory.CreateDirectory(subDir);
                        string ext     = System.IO.Path.GetExtension(img.Path);
                        string dest    = System.IO.Path.Combine(subDir, System.Guid.NewGuid().ToString("N") + ext);
                        System.IO.File.Copy(img.Path, dest, overwrite: false);

                        string relPath = System.IO.Path.GetRelativePath(appDir, dest).Replace('\\', '/');
                        EntityImages.UpdatePath(img.Id, relPath);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.Warn("DatabaseService", $"Legacy image migration failed for {img.Path}: {ex.Message}");
                    }
                }
            }
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars   = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (System.Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        return new string(chars).Trim();
    }

    public override void _ExitTree()
    {
        _conn?.Close();
    }
}
