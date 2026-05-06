using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Text.Json;

namespace DndBuilder.Core.Seeding
{
    public class Pf2eSeedingService
    {
        private readonly SqliteConnection _conn;

        public Pf2eSeedingService(SqliteConnection conn)
        {
            _conn = conn;
        }

        // Called once from DatabaseService.RunMigrations() — must be idempotent (INSERT OR IGNORE).
        public void SeedGlobalData()
        {
            SeedAbilityScores();
            SeedAbilityTypes();
            SeedActionCosts();
            SeedSaveTypes();
            SeedDieTypes();
            SeedSpellFrequencies();
            SeedAreaTypes();
            SeedSizes();
            SeedProficiencyRanks();
            SeedAttackCategories();
        }

        // Idempotent — INSERT OR IGNORE; safe to call on both new and existing campaigns.
        public void SeedAll(int campaignId)
        {
            SeedTraitTypes(campaignId);
            SeedTraditions(campaignId);
            SeedCreatureTypes(campaignId);
            SeedDamageTypes(campaignId);
            SeedConditionTypes(campaignId);
            SeedSenseTypes(campaignId);
            SeedSkillTypes(campaignId);
            SeedLanguageTypes(campaignId);
            SeedMovementTypes(campaignId);
            SeedFeatTypes(campaignId);
            SeedAncestries(campaignId);
            SeedHeritages(campaignId);
            SeedClasses(campaignId);
            SeedBackgrounds(campaignId);
            SeedCreatures(campaignId);
            SeedFeats(campaignId);
        }

        // ── Global seeds (no campaign_id) ────────────────────────────────────────

        private void SeedAbilityScores()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/AbilityScores.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_ability_scores (name, abbreviation) VALUES (@name, @abbr)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@abbr", e.GetProperty("abbreviation").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAbilityTypes()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/AbilityTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_ability_types (name) VALUES (@name)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedActionCosts()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/ActionCosts.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_action_costs (name, sort_order) VALUES (@name, @sort)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@sort", e.GetProperty("sort_order").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSaveTypes()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/SaveTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_save_types (name) VALUES (@name)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedDieTypes()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/DieTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_die_types (name, sides) VALUES (@name, @sides)";
                cmd.Parameters.AddWithValue("@name",  e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@sides", e.GetProperty("sides").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSpellFrequencies()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/SpellFrequencies.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_spell_frequencies (name, sort_order) VALUES (@name, @sort)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@sort", e.GetProperty("sort_order").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAreaTypes()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/AreaTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_area_types (name, description, sort_order) VALUES (@name, @desc, @sort)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@desc", e.GetProperty("description").GetString());
                cmd.Parameters.AddWithValue("@sort", e.GetProperty("sort_order").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSizes()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Sizes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_sizes (name, sort_order) VALUES (@name, @sort)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@sort", e.GetProperty("sort_order").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedProficiencyRanks()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/ProficiencyRanks.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_proficiency_ranks (name, rank_value) VALUES (@name, @rank)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@rank", e.GetProperty("rank_value").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAttackCategories()
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/AttackCategories.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_attack_categories (name) VALUES (@name)";
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        // ── Campaign-scoped seeds ─────────────────────────────────────────────────

        private void SeedTraitTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/TraitTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_trait_types (campaign_id, name, description)
                    VALUES (@cid, @name, @desc)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@desc", e.GetProperty("description").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedTraditions(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Traditions.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_traditions (campaign_id, name, description) VALUES (@cid, @name, '')";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedCreatureTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/CreatureTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_creature_types (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedDamageTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/DamageTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_damage_types
                    (campaign_id, name, is_physical, is_energy, is_persistent, is_splash)
                    VALUES (@cid, @name, @phys, @energy, @persist, @splash)";
                cmd.Parameters.AddWithValue("@cid",     campaignId);
                cmd.Parameters.AddWithValue("@name",    e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@phys",    e.GetProperty("is_physical").GetInt32());
                cmd.Parameters.AddWithValue("@energy",  e.GetProperty("is_energy").GetInt32());
                cmd.Parameters.AddWithValue("@persist", e.GetProperty("is_persistent").GetInt32());
                cmd.Parameters.AddWithValue("@splash",  e.GetProperty("is_splash").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedConditionTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/ConditionTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_condition_types
                    (campaign_id, name, has_value, description) VALUES (@cid, @name, @hv, '')";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@hv",   e.GetProperty("has_value").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSenseTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/SenseTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_sense_types
                    (campaign_id, name, is_precise, description) VALUES (@cid, @name, @prec, '')";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@prec", e.GetProperty("is_precise").GetInt32());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedSkillTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/SkillTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_skill_types (campaign_id, name, ability_score_id)
                    SELECT @cid, @name, id FROM pathfinder_ability_scores WHERE name = @attr";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@attr", e.GetProperty("ability").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedLanguageTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/LanguageTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_language_types (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedMovementTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/MovementTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_movement_types (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedFeatTypes(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/FeatTypes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO pathfinder_feat_types (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedAncestries(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Ancestries.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_ancestries (campaign_id, name, base_hp, size_id, speed_feet, description)
                    SELECT @cid, @name, @hp, id, @speed, @desc FROM pathfinder_sizes WHERE name = @size";
                cmd.Parameters.AddWithValue("@cid",   campaignId);
                cmd.Parameters.AddWithValue("@name",  e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@hp",    e.GetProperty("base_hp").GetInt32());
                cmd.Parameters.AddWithValue("@size",  e.GetProperty("size").GetString());
                cmd.Parameters.AddWithValue("@speed", e.GetProperty("speed_feet").GetInt32());
                cmd.Parameters.AddWithValue("@desc",  e.GetProperty("description").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedHeritages(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Heritages.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_heritages (campaign_id, ancestry_id, name, description)
                    SELECT @cid, id, @name, @desc FROM pathfinder_ancestries
                    WHERE campaign_id = @cid AND name = @ancestry";
                cmd.Parameters.AddWithValue("@cid",     campaignId);
                cmd.Parameters.AddWithValue("@ancestry", e.GetProperty("ancestry").GetString());
                cmd.Parameters.AddWithValue("@name",    e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@desc",    e.GetProperty("description").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedClasses(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Classes.json"))
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_classes (campaign_id, name, key_ability_score_id, hp_per_level, description)
                    SELECT @cid, @name, id, @hp, @desc FROM pathfinder_ability_scores WHERE name = @ability";
                cmd.Parameters.AddWithValue("@cid",     campaignId);
                cmd.Parameters.AddWithValue("@name",    e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@ability", e.GetProperty("key_ability").GetString());
                cmd.Parameters.AddWithValue("@hp",      e.GetProperty("hp_per_level").GetInt32());
                cmd.Parameters.AddWithValue("@desc",    e.GetProperty("description").GetString());
                cmd.ExecuteNonQuery();
            }
        }

        private void SeedBackgrounds(int campaignId)
        {
            foreach (var e in JsonSeedLoader.Load("Pf2e/Backgrounds.json"))
            {
                string skill1Name = e.GetProperty("skill_1").GetString();
                string skill2Name = e.GetProperty("skill_2").ValueKind == JsonValueKind.Null
                                    ? null : e.GetProperty("skill_2").GetString();
                string lore       = e.GetProperty("lore").GetString();

                int? skill1Id = LookupSkillTypeId(campaignId, skill1Name);
                int? skill2Id = skill2Name != null ? LookupSkillTypeId(campaignId, skill2Name) : (int?)null;

                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_backgrounds
                    (campaign_id, name, description, skill_1_id, skill_2_id, lore_skill_name, granted_feat_id)
                    VALUES (@cid, @name, @desc, @sk1, @sk2, @lore, NULL)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@desc", e.GetProperty("description").GetString());
                cmd.Parameters.AddWithValue("@sk1",  skill1Id.HasValue ? (object)skill1Id.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@sk2",  skill2Id.HasValue ? (object)skill2Id.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@lore", lore ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private int? LookupSkillTypeId(int campaignId, string skillName)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM pathfinder_skill_types WHERE campaign_id = @cid AND name = @name";
            cmd.Parameters.AddWithValue("@cid",  campaignId);
            cmd.Parameters.AddWithValue("@name", skillName);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (int?)(int)(long)result;
        }

        private void SeedCreatures(int campaignId)
        {
            // Pre-cache size and creature_type IDs to avoid per-row queries
            var sizeIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var sCmd = _conn.CreateCommand();
            sCmd.CommandText = "SELECT id, name FROM pathfinder_sizes";
            using (var r = sCmd.ExecuteReader()) while (r.Read()) sizeIds[r.GetString(1)] = r.GetInt32(0);

            var typeIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var tCmd = _conn.CreateCommand();
            tCmd.CommandText = "SELECT id, name FROM pathfinder_creature_types WHERE campaign_id = @cid";
            tCmd.Parameters.AddWithValue("@cid", campaignId);
            using (var r = tCmd.ExecuteReader()) while (r.Read()) typeIds[r.GetString(1)] = r.GetInt32(0);

            if (sizeIds.Count == 0 || typeIds.Count == 0) return;

            int defaultSizeId = sizeIds.TryGetValue("Medium",  out var ms) ? ms : 0;
            int defaultTypeId = typeIds.TryGetValue("Monster", out var mt) ? mt : 0;

            static string MapCreatureType(string t) => t switch
            {
                "Animal"    => "Animal",
                "Undead"    => "Undead",
                "Construct" => "Construct",
                "Humanoid"  => "Humanoid NPC",
                _           => "Monster"
            };

            using var tx = _conn.BeginTransaction();
            foreach (var e in JsonSeedLoader.Load("Pf2e/Monster_Core.json"))
            {
                string name     = e.GetProperty("name").GetString();
                string jsonType = e.TryGetProperty("creature_type", out var ct) && ct.ValueKind != JsonValueKind.Null ? ct.GetString() ?? "" : "";
                string sizeName = e.TryGetProperty("size",          out var sz) && sz.ValueKind != JsonValueKind.Null ? sz.GetString() ?? "Medium" : "Medium";
                int    typeId   = typeIds.TryGetValue(MapCreatureType(jsonType), out var tid) ? tid : defaultTypeId;
                int    sizeId   = sizeIds.TryGetValue(sizeName, out var sid) ? sid : defaultSizeId;

                var pageEl = e.GetProperty("source_page");

                var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO pathfinder_creatures
                    (campaign_id, name, creature_type_id, level, size_id,
                     str_mod, dex_mod, con_mod, int_mod, wis_mod, cha_mod,
                     ac, max_hp, fortitude, reflex, will, perception, source, source_page, notes)
                    SELECT @cid, @name, @type, @level, @size,
                           @str, @dex, @con, @int, @wis, @cha,
                           @ac, @hp, @fort, @reflex, @will, @perc, @source, @page, ''
                    WHERE NOT EXISTS (
                        SELECT 1 FROM pathfinder_creatures WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",    campaignId);
                cmd.Parameters.AddWithValue("@name",   name);
                cmd.Parameters.AddWithValue("@type",   typeId);
                cmd.Parameters.AddWithValue("@level",  e.GetProperty("level").GetInt32());
                cmd.Parameters.AddWithValue("@size",   sizeId);
                cmd.Parameters.AddWithValue("@str",    e.GetProperty("str_mod").GetInt32());
                cmd.Parameters.AddWithValue("@dex",    e.GetProperty("dex_mod").GetInt32());
                cmd.Parameters.AddWithValue("@con",    e.GetProperty("con_mod").GetInt32());
                cmd.Parameters.AddWithValue("@int",    e.GetProperty("int_mod").GetInt32());
                cmd.Parameters.AddWithValue("@wis",    e.GetProperty("wis_mod").GetInt32());
                cmd.Parameters.AddWithValue("@cha",    e.GetProperty("cha_mod").GetInt32());
                cmd.Parameters.AddWithValue("@ac",     e.GetProperty("ac").GetInt32());
                cmd.Parameters.AddWithValue("@hp",     e.GetProperty("max_hp").GetInt32());
                cmd.Parameters.AddWithValue("@fort",   e.GetProperty("fortitude").GetInt32());
                cmd.Parameters.AddWithValue("@reflex", e.GetProperty("reflex").GetInt32());
                cmd.Parameters.AddWithValue("@will",   e.GetProperty("will").GetInt32());
                cmd.Parameters.AddWithValue("@perc",   e.GetProperty("perception").GetInt32());
                cmd.Parameters.AddWithValue("@source", e.GetProperty("source").GetString() ?? "");
                cmd.Parameters.AddWithValue("@page",   pageEl.ValueKind == JsonValueKind.Null ? System.DBNull.Value : (object)pageEl.GetInt32());
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        private void SeedFeats(int campaignId)
        {
            var featTypeIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var ftCmd = _conn.CreateCommand();
            ftCmd.CommandText = "SELECT id, name FROM pathfinder_feat_types WHERE campaign_id = @cid";
            ftCmd.Parameters.AddWithValue("@cid", campaignId);
            using (var r = ftCmd.ExecuteReader()) while (r.Read()) featTypeIds[r.GetString(1)] = r.GetInt32(0);

            var classIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var clCmd = _conn.CreateCommand();
            clCmd.CommandText = "SELECT id, name FROM pathfinder_classes WHERE campaign_id = @cid";
            clCmd.Parameters.AddWithValue("@cid", campaignId);
            using (var r = clCmd.ExecuteReader()) while (r.Read()) classIds[r.GetString(1)] = r.GetInt32(0);

            var ancestryIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var anCmd = _conn.CreateCommand();
            anCmd.CommandText = "SELECT id, name FROM pathfinder_ancestries WHERE campaign_id = @cid";
            anCmd.Parameters.AddWithValue("@cid", campaignId);
            using (var r = anCmd.ExecuteReader()) while (r.Read()) ancestryIds[r.GetString(1)] = r.GetInt32(0);

            var actionCostIds = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var acCmd = _conn.CreateCommand();
            acCmd.CommandText = "SELECT id, name FROM pathfinder_action_costs";
            using (var r = acCmd.ExecuteReader()) while (r.Read()) actionCostIds[r.GetString(1)] = r.GetInt32(0);

            if (featTypeIds.Count == 0 || actionCostIds.Count == 0) return;

            int defaultFeatTypeId   = featTypeIds.TryGetValue("General", out var gid) ? gid : 1;
            int defaultActionCostId = actionCostIds.TryGetValue("None",   out var nid) ? nid : 1;

            using var tx = _conn.BeginTransaction();
            foreach (var e in JsonSeedLoader.Load("Pf2e/Feats.json"))
            {
                string featTypeName  = e.GetProperty("feat_type").GetString() ?? "General";
                string classNameRaw  = e.GetProperty("class").ValueKind    == JsonValueKind.Null ? null : e.GetProperty("class").GetString();
                string ancestryRaw   = e.GetProperty("ancestry").ValueKind == JsonValueKind.Null ? null : e.GetProperty("ancestry").GetString();
                string actionCostRaw = e.GetProperty("action_cost").GetString() ?? "None";

                int  featTypeId   = featTypeIds.TryGetValue(featTypeName,   out var ftId) ? ftId : defaultFeatTypeId;
                int? classId      = classNameRaw != null && classIds.TryGetValue(classNameRaw,     out var clId) ? (int?)clId : null;
                int? ancestryId   = ancestryRaw  != null && ancestryIds.TryGetValue(ancestryRaw,   out var anId) ? (int?)anId : null;
                int  actionCostId = actionCostIds.TryGetValue(actionCostRaw, out var acId) ? acId : defaultActionCostId;

                var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_feats
                    (campaign_id, name, feat_type_id, class_id, ancestry_id, level_required, action_cost_id, trigger, prerequisites, description)
                    VALUES (@cid, @name, @ftype, @clid, @anc, @lvl, @act, @trig, @prereq, @desc)";
                cmd.Parameters.AddWithValue("@cid",    campaignId);
                cmd.Parameters.AddWithValue("@name",   e.GetProperty("name").GetString());
                cmd.Parameters.AddWithValue("@ftype",  featTypeId);
                cmd.Parameters.AddWithValue("@clid",   classId.HasValue    ? (object)classId.Value    : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@anc",    ancestryId.HasValue ? (object)ancestryId.Value : System.DBNull.Value);
                cmd.Parameters.AddWithValue("@lvl",    e.GetProperty("level_required").GetInt32());
                cmd.Parameters.AddWithValue("@act",    actionCostId);
                cmd.Parameters.AddWithValue("@trig",   e.GetProperty("trigger").GetString() ?? "");
                cmd.Parameters.AddWithValue("@prereq", e.GetProperty("prerequisites").GetString() ?? "");
                cmd.Parameters.AddWithValue("@desc",   e.GetProperty("description").GetString() ?? "");
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }
}
