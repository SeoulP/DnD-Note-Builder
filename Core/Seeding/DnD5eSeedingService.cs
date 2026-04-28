using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DndBuilder.Core.Seeding
{
    public class DnD5eSeedingService
    {
        private readonly SqliteConnection _conn;

        public DnD5eSeedingService(SqliteConnection conn) { _conn = conn; }

        public void SeedAll(int campaignId)
        {
            SeedSpecies(campaignId);
            SeedSubspecies(campaignId);
            SeedAbilityTypes(campaignId);
            SeedAbilityResourceTypes(campaignId);
            SeedLocationFactionRoles(campaignId);
            SeedNpcRelationshipTypes(campaignId);
            SeedNpcStatuses(campaignId);
            SeedNpcFactionRoles(campaignId);
            SeedFactionRelationshipTypes(campaignId);
            SeedCharacterRelationshipTypes(campaignId);
            SeedItemTypes(campaignId);
            SeedQuestStatuses(campaignId);
            SeedSkills(campaignId);
            SeedClasses(campaignId);
            SeedAbilities(campaignId);
            SeedBackgrounds(campaignId);
            LinkBackgroundFeats(campaignId);
        }

        private void SeedSpecies(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/Species.json"))
            {
                string name = entry.GetProperty("name").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO species (campaign_id, name)
                    SELECT @cid, @name WHERE NOT EXISTS
                        (SELECT 1 FROM species WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSubspecies(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/Subspecies.json"))
            {
                string speciesName = entry.GetProperty("species").GetString();
                string name        = entry.GetProperty("name").GetString();
                string description = entry.GetProperty("description").GetString();

                var speciesCmd = _conn.CreateCommand();
                speciesCmd.CommandText = "SELECT id FROM species WHERE campaign_id = @cid AND name = @name AND inactive = 0 LIMIT 1";
                speciesCmd.Parameters.AddWithValue("@cid",  campaignId);
                speciesCmd.Parameters.AddWithValue("@name", speciesName);
                var speciesResult = speciesCmd.ExecuteScalar();
                if (speciesResult == null) continue;
                int speciesId = (int)(long)speciesResult;

                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO subspecies (campaign_id, species_id, name, description)
                    SELECT @cid, @sid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM subspecies WHERE campaign_id = @cid AND species_id = @sid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@sid",  speciesId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", description);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedClasses(int campaignId)
        {
            int sortOrder = 0;
            foreach (var entry in JsonSeedLoader.Load("DnD5e/Classes.json"))
            {
                string name      = entry.GetProperty("name").GetString();
                string desc      = entry.GetProperty("description").GetString();
                int    hitDie    = entry.GetProperty("hit_die").GetInt32();
                string primary   = entry.GetProperty("primary_ability").GetString();
                string saving    = entry.GetProperty("saving_throw_profs").GetString();
                string armor     = entry.GetProperty("armor_profs").GetString();
                string weapon    = entry.GetProperty("weapon_profs").GetString();
                string tools     = entry.GetProperty("tool_profs").GetString();
                int    skillCnt  = entry.GetProperty("skill_choices_count").GetInt32();
                string skillOpts = entry.GetProperty("skill_choices_options").GetString();
                string equipA    = entry.GetProperty("starting_equip_a").GetString();
                string equipB    = entry.GetProperty("starting_equip_b").GetString();
                string spell     = entry.GetProperty("spellcasting_ability").GetString();
                bool   ritual    = entry.GetProperty("is_ritual_caster").GetBoolean();
                bool   prepared  = entry.GetProperty("is_prepared_caster").GetBoolean();
                int    unlock    = entry.GetProperty("subclass_unlock_level").GetInt32();

                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO classes
                    (campaign_id, name, description, sort_order, subclass_unlock_level,
                     hit_die, primary_ability, saving_throw_profs, armor_profs, weapon_profs, tool_profs,
                     skill_choices_count, skill_choices_options, starting_equip_a, starting_equip_b,
                     spellcasting_ability, is_ritual_caster, is_prepared_caster)
                    SELECT @cid, @name, @desc, @sort, @unlock,
                           @hitdie, @primary, @saving, @armor, @weapon, @tool,
                           @skillcount, @skillopts, @equipa, @equipb,
                           @spell, @ritual, @prepared
                    WHERE NOT EXISTS (SELECT 1 FROM classes WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",       campaignId);
                cmd.Parameters.AddWithValue("@name",      name);
                cmd.Parameters.AddWithValue("@desc",      desc);
                cmd.Parameters.AddWithValue("@sort",      sortOrder);
                cmd.Parameters.AddWithValue("@unlock",    unlock);
                cmd.Parameters.AddWithValue("@hitdie",    hitDie);
                cmd.Parameters.AddWithValue("@primary",   primary);
                cmd.Parameters.AddWithValue("@saving",    saving);
                cmd.Parameters.AddWithValue("@armor",     armor);
                cmd.Parameters.AddWithValue("@weapon",    weapon);
                cmd.Parameters.AddWithValue("@tool",      tools);
                cmd.Parameters.AddWithValue("@skillcount",skillCnt);
                cmd.Parameters.AddWithValue("@skillopts", skillOpts);
                cmd.Parameters.AddWithValue("@equipa",    equipA);
                cmd.Parameters.AddWithValue("@equipb",    equipB);
                cmd.Parameters.AddWithValue("@spell",     spell);
                cmd.Parameters.AddWithValue("@ritual",    ritual   ? 1 : 0);
                cmd.Parameters.AddWithValue("@prepared",  prepared ? 1 : 0);
                cmd.ExecuteNonQuery();

                var idCmd = _conn.CreateCommand();
                idCmd.CommandText = "SELECT id FROM classes WHERE campaign_id = @cid AND name = @name LIMIT 1";
                idCmd.Parameters.AddWithValue("@cid",  campaignId);
                idCmd.Parameters.AddWithValue("@name", name);
                int classId = (int)(long)idCmd.ExecuteScalar();

                int subSort = 0;
                foreach (var sub in entry.GetProperty("subclasses").EnumerateArray())
                {
                    string subName = sub.GetString();
                    var subCmd = _conn.CreateCommand();
                    subCmd.CommandText = @"INSERT INTO subclasses (campaign_id, class_id, name, sort_order)
                        SELECT @cid, @clid, @name, @sort
                        WHERE NOT EXISTS (SELECT 1 FROM subclasses WHERE campaign_id = @cid AND class_id = @clid AND name = @name)";
                    subCmd.Parameters.AddWithValue("@cid",  campaignId);
                    subCmd.Parameters.AddWithValue("@clid", classId);
                    subCmd.Parameters.AddWithValue("@name", subName);
                    subCmd.Parameters.AddWithValue("@sort", subSort);
                    subCmd.ExecuteNonQuery();
                    subSort++;
                }
                sortOrder++;
            }
        }

        private void SeedAbilities(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/Abilities.json"))
            {
                string name     = entry.GetProperty("name").GetString();
                string typeName = entry.GetProperty("type").GetString();
                string action   = entry.GetProperty("action").GetString();
                string trigger  = entry.GetProperty("trigger").GetString();
                string recovery = entry.GetProperty("recovery").GetString();
                string effect   = entry.GetProperty("effect").GetString();

                int? typeId = null;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var typeCmd = _conn.CreateCommand();
                    typeCmd.CommandText = "SELECT id FROM ability_types WHERE campaign_id = @cid AND name = @name LIMIT 1";
                    typeCmd.Parameters.AddWithValue("@cid",  campaignId);
                    typeCmd.Parameters.AddWithValue("@name", typeName);
                    var typeResult = typeCmd.ExecuteScalar();
                    if (typeResult != null) typeId = (int)(long)typeResult;
                }

                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO abilities (campaign_id, name, type, action, trigger, recovery, effect, type_id)
                    SELECT @cid, @name, @type, @action, @trigger, @recovery, @effect, @typeid
                    WHERE NOT EXISTS (SELECT 1 FROM abilities WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",     campaignId);
                cmd.Parameters.AddWithValue("@name",    name);
                cmd.Parameters.AddWithValue("@type",    typeName);
                cmd.Parameters.AddWithValue("@action",  action);
                cmd.Parameters.AddWithValue("@trigger", trigger);
                cmd.Parameters.AddWithValue("@recovery",recovery);
                cmd.Parameters.AddWithValue("@effect",  effect);
                cmd.Parameters.AddWithValue("@typeid",  (object)typeId ?? System.DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAbilityTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/AbilityTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO ability_types (campaign_id, name)
                    SELECT @cid, @name WHERE NOT EXISTS
                        (SELECT 1 FROM ability_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAbilityResourceTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/AbilityResourceTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO ability_resource_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM ability_resource_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedLocationFactionRoles(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/LocationFactionRoles.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO location_faction_roles (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM location_faction_roles WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedNpcRelationshipTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/NpcRelationshipTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO npc_relationship_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM npc_relationship_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedNpcStatuses(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/NpcStatuses.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO npc_statuses (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM npc_statuses WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedNpcFactionRoles(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/NpcFactionRoles.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO npc_faction_roles (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM npc_faction_roles WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedFactionRelationshipTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/FactionRelationshipTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO faction_relationship_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM faction_relationship_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedCharacterRelationshipTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/CharacterRelationshipTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO character_relationship_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM character_relationship_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedItemTypes(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/ItemTypes.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO item_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM item_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedQuestStatuses(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/QuestStatuses.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string desc = entry.GetProperty("description").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO quest_statuses (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM quest_statuses WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSkills(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/DnD5eSkills.json"))
            {
                string name = entry.GetProperty("name").GetString();
                string attr = entry.GetProperty("attribute").GetString();
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO dnd5e_skills (campaign_id, name, attribute)
                    SELECT @cid, @name, @attr WHERE NOT EXISTS
                        (SELECT 1 FROM dnd5e_skills WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@attr", attr);
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedBackgrounds(int campaignId)
        {
            foreach (var entry in JsonSeedLoader.Load("DnD5e/Backgrounds.json"))
            {
                string name   = entry.GetProperty("name").GetString();
                string skills = entry.GetProperty("skill_names").GetString();
                string desc   = entry.GetProperty("description").GetString();
                string tools  = entry.GetProperty("tool_options").GetString();
                string attrs  = entry.GetProperty("ability_score_options").GetString();

                var insert = _conn.CreateCommand();
                insert.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description, tool_options, ability_score_options)
                    SELECT @cid, @name, 2, @skills, @desc, @tools, @attrs WHERE NOT EXISTS
                        (SELECT 1 FROM dnd5e_backgrounds WHERE campaign_id = @cid AND name = @name)";
                insert.Parameters.AddWithValue("@cid",   campaignId);
                insert.Parameters.AddWithValue("@name",  name);
                insert.Parameters.AddWithValue("@skills", skills);
                insert.Parameters.AddWithValue("@desc",  desc);
                insert.Parameters.AddWithValue("@tools", tools);
                insert.Parameters.AddWithValue("@attrs", attrs);
                insert.ExecuteNonQuery();

                // Backfill tool_options + description for existing campaigns
                var backfillTools = _conn.CreateCommand();
                backfillTools.CommandText = @"UPDATE dnd5e_backgrounds
                    SET tool_options = @tools, description = @desc
                    WHERE campaign_id = @cid AND name = @name AND is_custom = 0 AND tool_options = ''";
                backfillTools.Parameters.AddWithValue("@cid",   campaignId);
                backfillTools.Parameters.AddWithValue("@name",  name);
                backfillTools.Parameters.AddWithValue("@desc",  desc);
                backfillTools.Parameters.AddWithValue("@tools", tools);
                backfillTools.ExecuteNonQuery();

                // Backfill ability_score_options independently
                var backfillAttrs = _conn.CreateCommand();
                backfillAttrs.CommandText = @"UPDATE dnd5e_backgrounds
                    SET ability_score_options = @attrs
                    WHERE campaign_id = @cid AND name = @name AND is_custom = 0 AND ability_score_options = ''";
                backfillAttrs.Parameters.AddWithValue("@cid",   campaignId);
                backfillAttrs.Parameters.AddWithValue("@name",  name);
                backfillAttrs.Parameters.AddWithValue("@attrs", attrs);
                backfillAttrs.ExecuteNonQuery();
            }
        }

        // Background name → origin feat name (must match ability names in Abilities.json)
        private static readonly (string background, string feat)[] BackgroundFeatLinks =
        {
            ("Acolyte",     "Magic Initiate (Cleric)"),
            ("Artisan",     "Crafter"),
            ("Charlatan",   "Skilled"),
            ("Criminal",    "Alert"),
            ("Entertainer", "Musician"),
            ("Farmer",      "Tough"),
            ("Guard",       "Alert"),
            ("Guide",       "Magic Initiate (Druid)"),
            ("Hermit",      "Healer"),
            ("Merchant",    "Lucky"),
            ("Noble",       "Skilled"),
            ("Sage",        "Magic Initiate (Wizard)"),
            ("Sailor",      "Tavern Brawler"),
            ("Soldier",     "Savage Attacker"),
            ("Wayfarer",    "Lucky"),
        };

        private void LinkBackgroundFeats(int campaignId)
        {
            foreach (var (background, feat) in BackgroundFeatLinks)
            {
                var lookupCmd = _conn.CreateCommand();
                lookupCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @feat LIMIT 1";
                lookupCmd.Parameters.AddWithValue("@cid",  campaignId);
                lookupCmd.Parameters.AddWithValue("@feat", feat);
                var result = lookupCmd.ExecuteScalar();
                if (result == null) continue;
                int featId = (int)(long)result;

                var updateCmd = _conn.CreateCommand();
                updateCmd.CommandText = @"UPDATE dnd5e_backgrounds
                    SET feat_ability_id = @featId
                    WHERE campaign_id = @cid AND name = @bg AND is_custom = 0 AND feat_ability_id IS NULL";
                updateCmd.Parameters.AddWithValue("@featId", featId);
                updateCmd.Parameters.AddWithValue("@cid",    campaignId);
                updateCmd.Parameters.AddWithValue("@bg",     background);
                updateCmd.ExecuteNonQuery();
            }
        }
    }
}
