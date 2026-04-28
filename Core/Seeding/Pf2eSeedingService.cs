using Microsoft.Data.Sqlite;

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

        // Called once per campaign at creation time — plain INSERT (fresh campaign has no data).
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
                cmd.CommandText = @"INSERT INTO pathfinder_trait_types (campaign_id, name, description)
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
                cmd.CommandText = "INSERT INTO pathfinder_traditions (campaign_id, name, description) VALUES (@cid, @name, '')";
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
                cmd.CommandText = "INSERT INTO pathfinder_creature_types (campaign_id, name) VALUES (@cid, @name)";
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
                cmd.CommandText = @"INSERT INTO pathfinder_damage_types
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
                cmd.CommandText = @"INSERT INTO pathfinder_condition_types
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
                cmd.CommandText = @"INSERT INTO pathfinder_sense_types
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
                cmd.CommandText = @"INSERT INTO pathfinder_skill_types (campaign_id, name, ability_score_id)
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
                cmd.CommandText = "INSERT INTO pathfinder_language_types (campaign_id, name) VALUES (@cid, @name)";
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
                cmd.CommandText = "INSERT INTO pathfinder_movement_types (campaign_id, name) VALUES (@cid, @name)";
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
                cmd.CommandText = "INSERT INTO pathfinder_feat_types (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", e.GetProperty("name").GetString());
                cmd.ExecuteNonQuery();
            }
        }
    }
}
