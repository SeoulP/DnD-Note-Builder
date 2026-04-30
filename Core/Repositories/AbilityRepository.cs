using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using DndBuilder.Core;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class AbilityRepository
    {
        private readonly SqliteConnection _conn;

        public AbilityRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            // abilities — system-level ability definitions
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS abilities (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                type        TEXT    NOT NULL DEFAULT '',
                action      TEXT    NOT NULL DEFAULT '',
                trigger     TEXT    NOT NULL DEFAULT '',
                cost        TEXT    NOT NULL DEFAULT '',
                uses        INTEGER NOT NULL DEFAULT 0,
                recovery    TEXT    NOT NULL DEFAULT '',
                effect      TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // class_abilities — join: class ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS class_abilities (
                class_id   INTEGER NOT NULL REFERENCES classes(id)   ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                PRIMARY KEY (class_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // subclass_abilities — join: subclass ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subclass_abilities (
                subclass_id INTEGER NOT NULL REFERENCES subclasses(id) ON DELETE CASCADE,
                ability_id  INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                PRIMARY KEY (subclass_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // species_abilities — join: species ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS species_abilities (
                species_id INTEGER NOT NULL REFERENCES species(id)   ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                PRIMARY KEY (species_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // subspecies_abilities — join: subspecies ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subspecies_abilities (
                subspecies_id INTEGER NOT NULL REFERENCES subspecies(id) ON DELETE CASCADE,
                ability_id    INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                PRIMARY KEY (subspecies_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // class_level_abilities — join: class_level ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS class_level_abilities (
                level_id   INTEGER NOT NULL REFERENCES class_levels(id) ON DELETE CASCADE,
                ability_id INTEGER NOT NULL REFERENCES abilities(id)    ON DELETE CASCADE,
                PRIMARY KEY (level_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // ability_species_levels — join: species_level ↔ ability
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_species_levels (
                species_level_id INTEGER NOT NULL REFERENCES species_levels(id) ON DELETE CASCADE,
                ability_id       INTEGER NOT NULL REFERENCES abilities(id)      ON DELETE CASCADE,
                PRIMARY KEY (species_level_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // character_abilities — per-PC ability assignments
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_abilities (
                character_id   INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                ability_id     INTEGER NOT NULL REFERENCES abilities(id)  ON DELETE CASCADE,
                uses_remaining INTEGER NOT NULL DEFAULT 0,
                source         TEXT    NOT NULL DEFAULT 'auto',
                PRIMARY KEY (character_id, ability_id)
            )";
            cmd.ExecuteNonQuery();

            // Migration: add required_level to subclass_abilities
            var hasSubLevel = _conn.CreateCommand();
            hasSubLevel.CommandText = "SELECT COUNT(*) FROM pragma_table_info('subclass_abilities') WHERE name = 'required_level'";
            if ((long)hasSubLevel.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE subclass_abilities ADD COLUMN required_level INTEGER NOT NULL DEFAULT 1";
                alter.ExecuteNonQuery();
            }

            // Migration: add uses to subclass_abilities
            var hasSubUses = _conn.CreateCommand();
            hasSubUses.CommandText = "SELECT COUNT(*) FROM pragma_table_info('subclass_abilities') WHERE name = 'uses'";
            if ((long)hasSubUses.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE subclass_abilities ADD COLUMN uses TEXT NOT NULL DEFAULT '--'";
                alter.ExecuteNonQuery();
            }

            // Migration: add source column to existing character_abilities tables
            var hasSource = _conn.CreateCommand();
            hasSource.CommandText = "SELECT COUNT(*) FROM pragma_table_info('character_abilities') WHERE name = 'source'";
            if ((long)hasSource.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE character_abilities ADD COLUMN source TEXT NOT NULL DEFAULT 'auto'";
                alter.ExecuteNonQuery();
            }

            // Choices support — additive columns on abilities
            AddAbilityColumnIfMissing("max_choices",        "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_pool_type",   "TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("cost_resource_id",   "INTEGER REFERENCES abilities(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("resource_type_id",   "INTEGER REFERENCES ability_resource_types(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("recovery_interval",       "TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("recovery_amount",       "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("type_id",           "INTEGER REFERENCES ability_types(id) ON DELETE SET NULL");
            AddAbilityColumnIfMissing("pick_count_mode",       "TEXT    NOT NULL DEFAULT 'formula'");
            AddAbilityColumnIfMissing("choice_count_base",     "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_count_attribute","TEXT    NOT NULL DEFAULT ''");
            AddAbilityColumnIfMissing("choice_count_add_prof", "INTEGER NOT NULL DEFAULT 0");
            AddAbilityColumnIfMissing("choice_count_add_level","TEXT    NOT NULL DEFAULT ''");

            // ability_costs — structured resource costs per ability (source of truth for runtime logic)
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_costs (
                ability_id       INTEGER NOT NULL REFERENCES abilities(id)              ON DELETE CASCADE,
                resource_type_id INTEGER NOT NULL REFERENCES ability_resource_types(id) ON DELETE CASCADE,
                amount           INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (ability_id, resource_type_id)
            )";
            cmd.ExecuteNonQuery();

            // Seed structured costs from the legacy abilities.resource_type_id column after ensuring it exists.
            var migrateCmd = _conn.CreateCommand();
            migrateCmd.CommandText = @"
                INSERT OR IGNORE INTO ability_costs (ability_id, resource_type_id, amount)
                SELECT id, resource_type_id, 1
                FROM abilities
                WHERE resource_type_id IS NOT NULL";
            migrateCmd.ExecuteNonQuery();

            // ability_choices — predefined options for fixed-pool abilities
            var choicesCmd = _conn.CreateCommand();
            choicesCmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_choices (
                id               INTEGER PRIMARY KEY,
                ability_id       INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                name             TEXT    NOT NULL DEFAULT '',
                description      TEXT    NOT NULL DEFAULT '',
                sort_order       INTEGER NOT NULL DEFAULT 0,
                linked_ability_id INTEGER REFERENCES abilities(id) ON DELETE SET NULL
            )";
            choicesCmd.ExecuteNonQuery();

            // Migration: add linked_ability_id to existing ability_choices tables
            var hasLink = _conn.CreateCommand();
            hasLink.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ability_choices') WHERE name = 'linked_ability_id'";
            if ((long)hasLink.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE ability_choices ADD COLUMN linked_ability_id INTEGER REFERENCES abilities(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var progressionCmd = _conn.CreateCommand();
            progressionCmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_choice_progression (
                id             INTEGER PRIMARY KEY,
                ability_id     INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                required_level INTEGER NOT NULL,
                choice_count   INTEGER NOT NULL DEFAULT 0
            )";
            progressionCmd.ExecuteNonQuery();

            var usageProgressionCmd = _conn.CreateCommand();
            usageProgressionCmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_usage_progression (
                id             INTEGER PRIMARY KEY,
                ability_id     INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                required_level INTEGER NOT NULL,
                usages         INTEGER NOT NULL,
                UNIQUE (ability_id, required_level)
            )";
            usageProgressionCmd.ExecuteNonQuery();

            var characterChoicesCmd = _conn.CreateCommand();
            characterChoicesCmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_ability_choices (
                character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                ability_id   INTEGER NOT NULL REFERENCES abilities(id) ON DELETE CASCADE,
                choice_id    INTEGER NOT NULL REFERENCES ability_choices(id) ON DELETE CASCADE,
                PRIMARY KEY (character_id, ability_id, choice_id)
            )";
            characterChoicesCmd.ExecuteNonQuery();
        }

        private void AddAbilityColumnIfMissing(string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('abilities') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE abilities ADD COLUMN {column} {definition}";
                alter.ExecuteNonQuery();
            }
        }

        // ── Abilities CRUD ────────────────────────────────────────────────────

        public List<Ability> GetAll(int campaignId)
        {
            var list = new List<Ability>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id
                                FROM abilities WHERE campaign_id = @cid ORDER BY sort_order ASC, name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Ability Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id
                                FROM abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var ability = Map(reader);
            reader.Close();
            ability.Costs = GetCosts(id);
            return ability;
        }

        public int Add(Ability ability)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO abilities (campaign_id, name, type, action, trigger, recovery, effect, notes, sort_order, max_choices, choice_pool_type, cost_resource_id, recovery_interval, recovery_amount, pick_count_mode, choice_count_base, choice_count_attribute, choice_count_add_prof, choice_count_add_level, type_id)
                                VALUES (@cid, @name, @type, @action, @trigger, @recovery, @effect, @notes, @sort, @maxchoices, @pooltype, NULL, @recoveryInterval, @recoveryAmount, @pickMode, @ccBase, @ccAttr, @ccProf, @ccLevel, @typeId);
                                SELECT last_insert_rowid();";
            Bind(cmd, ability);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Ability ability)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE abilities
                                SET name = @name, type = @type, action = @action, trigger = @trigger,
                                    recovery = @recovery, effect = @effect,
                                    notes = @notes, sort_order = @sort,
                                    max_choices = @maxchoices, choice_pool_type = @pooltype,
                                    recovery_interval = @recoveryInterval, recovery_amount = @recoveryAmount,
                                    pick_count_mode = @pickMode,
                                    choice_count_base = @ccBase, choice_count_attribute = @ccAttr,
                                    choice_count_add_prof = @ccProf, choice_count_add_level = @ccLevel,
                                    type_id = @typeId
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", ability.Id);
            Bind(cmd, ability);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Class ability links ───────────────────────────────────────────────

        public List<int> GetAbilityIdsForClass(int classId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM class_abilities WHERE class_id = @cid";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddClassAbility(int classId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO class_abilities (class_id, ability_id) VALUES (@cid, @aid)";
            cmd.Parameters.AddWithValue("@cid", classId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveClassAbility(int classId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM class_abilities WHERE class_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", classId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Subclass ability links ────────────────────────────────────────────

        public List<int> GetAbilityIdsForSubclass(int subclassId, int maxLevel = int.MaxValue)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM subclass_abilities WHERE subclass_id = @sid AND required_level <= @level";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@level", maxLevel);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public List<(int abilityId, int requiredLevel)> GetSubclassAbilityLinks(int subclassId)
        {
            var list = new List<(int, int)>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, required_level FROM subclass_abilities WHERE subclass_id = @sid ORDER BY required_level ASC, ability_id ASC";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetInt32(0), reader.GetInt32(1)));
            return list;
        }

        public void UpdateSubclassAbilityLevel(int subclassId, int abilityId, int level)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE subclass_abilities SET required_level = @level WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@aid",   abilityId);
            cmd.Parameters.AddWithValue("@level", level);
            cmd.ExecuteNonQuery();
        }

        public List<(int abilityId, string uses)> GetAbilityIdsForSubclassLevel(int subclassId, int level)
        {
            var list = new List<(int, string)>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, uses FROM subclass_abilities WHERE subclass_id = @sid AND required_level = @level";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@level", level);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetInt32(0), reader.IsDBNull(1) ? "--" : reader.GetString(1)));
            return list;
        }

        public void UpdateSubclassAbilityUses(int subclassId, int abilityId, string uses)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE subclass_abilities SET uses = @uses WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid",  subclassId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.Parameters.AddWithValue("@uses", uses);
            cmd.ExecuteNonQuery();
        }

        public void AddSubclassAbilityAtLevel(int subclassId, int abilityId, int level)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subclass_abilities (subclass_id, ability_id, required_level) VALUES (@sid, @aid, @level)";
            cmd.Parameters.AddWithValue("@sid",   subclassId);
            cmd.Parameters.AddWithValue("@aid",   abilityId);
            cmd.Parameters.AddWithValue("@level", level);
            cmd.ExecuteNonQuery();
        }

        public void AddSubclassAbility(int subclassId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subclass_abilities (subclass_id, ability_id, required_level) VALUES (@sid, @aid, 1)";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSubclassAbility(int subclassId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subclass_abilities WHERE subclass_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid", subclassId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Species ability links ─────────────────────────────────────────────

        public List<int> GetAbilityIdsForSpecies(int speciesId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM species_abilities WHERE species_id = @sid";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddSpeciesAbility(int speciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO species_abilities (species_id, ability_id) VALUES (@sid, @aid)";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSpeciesAbility(int speciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM species_abilities WHERE species_id = @sid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Species level ability links ───────────────────────────────────────

        public List<int> GetAbilityIdsForSpeciesLevel(int speciesLevelId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM ability_species_levels WHERE species_level_id = @slid";
            cmd.Parameters.AddWithValue("@slid", speciesLevelId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddSpeciesLevelAbility(int speciesLevelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO ability_species_levels (species_level_id, ability_id) VALUES (@slid, @aid)";
            cmd.Parameters.AddWithValue("@slid", speciesLevelId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSpeciesLevelAbility(int speciesLevelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_species_levels WHERE species_level_id = @slid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@slid", speciesLevelId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Subspecies ability links ──────────────────────────────────────────

        public List<int> GetAbilityIdsForSubspecies(int subspeciesId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM subspecies_abilities WHERE subspecies_id = @ssid";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddSubspeciesAbility(int subspeciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO subspecies_abilities (subspecies_id, ability_id) VALUES (@ssid, @aid)";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveSubspeciesAbility(int subspeciesId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subspecies_abilities WHERE subspecies_id = @ssid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@ssid", subspeciesId);
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Class level ability links ─────────────────────────────────────────

        public List<int> GetAbilityIdsForLevel(int levelId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM class_level_abilities WHERE level_id = @lid";
            cmd.Parameters.AddWithValue("@lid", levelId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public void AddLevelAbility(int levelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO class_level_abilities (level_id, ability_id) VALUES (@lid, @aid)";
            cmd.Parameters.AddWithValue("@lid", levelId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveLevelAbility(int levelId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM class_level_abilities WHERE level_id = @lid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@lid", levelId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        // ── Character ability use tracking ────────────────────────────────────

        public List<CharacterAbility> GetCharacterAbilities(int characterId)
        {
            var list = new List<CharacterAbility>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT character_id, ability_id, source FROM character_abilities WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new CharacterAbility
                {
                    CharacterId = reader.GetInt32(0),
                    AbilityId   = reader.GetInt32(1),
                    Source      = reader.GetString(2),
                });
            return list;
        }

        public void UpsertCharacterAbility(int characterId, int abilityId, string source = "auto")
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO character_abilities (character_id, ability_id, uses_remaining, source)
                                VALUES (@cid, @aid, 0, @src)
                                ON CONFLICT(character_id, ability_id) DO NOTHING";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.Parameters.AddWithValue("@src", source);
            cmd.ExecuteNonQuery();
        }

        public void RemoveCharacterAbility(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_abilities WHERE character_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveAutoAbilitiesForCharacter(int characterId, IEnumerable<int> abilityIdsToKeep)
        {
            // Remove all 'auto' sourced abilities not in the keep set
            var keepSet = new HashSet<int>(abilityIdsToKeep);
            var existing = GetCharacterAbilities(characterId);
            foreach (var ca in existing)
            {
                if (ca.Source == "auto" && !keepSet.Contains(ca.AbilityId))
                    RemoveCharacterAbility(characterId, ca.AbilityId);
            }
        }

        public List<CharacterAbilityChoice> GetCharacterAbilityChoices(int characterId, int abilityId)
        {
            var list = new List<CharacterAbilityChoice>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT character_id, ability_id, choice_id
                                FROM character_ability_choices
                                WHERE character_id = @cid AND ability_id = @aid
                                ORDER BY choice_id ASC";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new CharacterAbilityChoice
                {
                    CharacterId = reader.GetInt32(0),
                    AbilityId = reader.GetInt32(1),
                    ChoiceId = reader.GetInt32(2),
                });
            }
            return list;
        }

        public void SetCharacterAbilityChoiceSelected(int characterId, int abilityId, int choiceId, bool selected)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = selected
                ? @"INSERT OR IGNORE INTO character_ability_choices (character_id, ability_id, choice_id)
                    VALUES (@cid, @aid, @choice)"
                : @"DELETE FROM character_ability_choices
                    WHERE character_id = @cid AND ability_id = @aid AND choice_id = @choice";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.Parameters.AddWithValue("@choice", choiceId);
            cmd.ExecuteNonQuery();
        }

        public void ClearCharacterAbilityChoices(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_ability_choices WHERE character_id = @cid AND ability_id = @aid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public List<AbilityChoiceProgression> GetChoiceProgressionForAbility(int abilityId)
        {
            var list = new List<AbilityChoiceProgression>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, ability_id, required_level, choice_count
                                FROM ability_choice_progression
                                WHERE ability_id = @aid
                                ORDER BY required_level ASC, id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AbilityChoiceProgression
                {
                    Id = reader.GetInt32(0),
                    AbilityId = reader.GetInt32(1),
                    RequiredLevel = reader.GetInt32(2),
                    ChoiceCount = reader.GetInt32(3),
                });
            }
            return list;
        }

        public int AddChoiceProgression(AbilityChoiceProgression progression)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_choice_progression (ability_id, required_level, choice_count)
                                VALUES (@aid, @level, @count);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@aid", progression.AbilityId);
            cmd.Parameters.AddWithValue("@level", progression.RequiredLevel);
            cmd.Parameters.AddWithValue("@count", progression.ChoiceCount);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void EditChoiceProgression(AbilityChoiceProgression progression)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE ability_choice_progression
                                SET required_level = @level, choice_count = @count
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", progression.Id);
            cmd.Parameters.AddWithValue("@level", progression.RequiredLevel);
            cmd.Parameters.AddWithValue("@count", progression.ChoiceCount);
            cmd.ExecuteNonQuery();
        }

        public void DeleteChoiceProgression(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_choice_progression WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int ResolveChoiceCount(Ability ability, int characterLevel, PlayerCharacter pc = null)
        {
            int picks;
            if (ability.PickCountMode == "progression")
            {
                // Base from MaxChoices/ChoiceCountBase, overridden by progression table
                picks = ability.ChoiceCountBase > 0 ? ability.ChoiceCountBase : ability.MaxChoices;
                foreach (var step in GetChoiceProgressionForAbility(ability.Id))
                    if (step.RequiredLevel <= characterLevel)
                        picks = step.ChoiceCount;
            }
            else
            {
                // Formula mode: ChoiceCountBase is primary; fall back to MaxChoices for legacy data
                picks = ability.ChoiceCountBase > 0 ? ability.ChoiceCountBase : ability.MaxChoices;
                if (!string.IsNullOrEmpty(ability.ChoiceCountAttribute) && pc != null)
                    picks += DnD5eMath.AbilityMod(GetScore(pc, ability.ChoiceCountAttribute));
                if (ability.ChoiceCountAddProf)
                    picks += DnD5eMath.ProfBonus(characterLevel);
                picks += LevelBonus(ability.ChoiceCountAddLevel, characterLevel);
            }

            return Math.Max(0, picks);
        }

        // ── Usage Progression ─────────────────────────────────────────────────

        public List<AbilityUsageProgression> GetUsageProgressionForAbility(int abilityId)
        {
            var list = new List<AbilityUsageProgression>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, ability_id, required_level, usages
                                FROM ability_usage_progression
                                WHERE ability_id = @aid
                                ORDER BY required_level ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityUsageProgression
                {
                    Id            = reader.GetInt32(0),
                    AbilityId     = reader.GetInt32(1),
                    RequiredLevel = reader.GetInt32(2),
                    Usages        = reader.GetInt32(3),
                });
            return list;
        }

        public void AddUsageProgression(AbilityUsageProgression entry)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_usage_progression (ability_id, required_level, usages)
                                VALUES (@aid, @level, @usages)
                                ON CONFLICT(ability_id, required_level) DO UPDATE SET usages = excluded.usages";
            cmd.Parameters.AddWithValue("@aid",   entry.AbilityId);
            cmd.Parameters.AddWithValue("@level", entry.RequiredLevel);
            cmd.Parameters.AddWithValue("@usages",entry.Usages);
            cmd.ExecuteNonQuery();
        }

        public void ClearUsageProgression(int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_usage_progression WHERE ability_id = @aid";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteUsageProgression(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_usage_progression WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int ResolveUsagesAtLevel(int abilityId, int characterLevel)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT usages FROM ability_usage_progression
                                WHERE ability_id = @aid AND required_level <= @level
                                ORDER BY required_level DESC
                                LIMIT 1";
            cmd.Parameters.AddWithValue("@aid",   abilityId);
            cmd.Parameters.AddWithValue("@level", characterLevel);
            var result = cmd.ExecuteScalar();
            return result == null ? 0 : (int)(long)result;
        }

        private static int LevelBonus(string mode, int level) => mode switch
        {
            "full"      => level,
            "half_down" => level / 2,
            "half_up"   => (level + 1) / 2,
            _           => 0,
        };
        private static int GetScore(PlayerCharacter pc, string attr) => attr switch
        {
            "str" => pc.Strength,
            "dex" => pc.Dexterity,
            "con" => pc.Constitution,
            "int" => pc.Intelligence,
            "wis" => pc.Wisdom,
            "cha" => pc.Charisma,
            _     => 10,
        };

        // ── Private helpers ───────────────────────────────────────────────────

        private static void Bind(SqliteCommand cmd, Ability a)
        {
            cmd.Parameters.AddWithValue("@cid",              a.CampaignId);
            cmd.Parameters.AddWithValue("@name",             a.Name);
            cmd.Parameters.AddWithValue("@type",             a.Type);
            cmd.Parameters.AddWithValue("@action",           a.Action);
            cmd.Parameters.AddWithValue("@trigger",          a.Trigger);
            cmd.Parameters.AddWithValue("@recovery",         a.Recovery);
            cmd.Parameters.AddWithValue("@effect",           a.Effect);
            cmd.Parameters.AddWithValue("@notes",            a.Notes);
            cmd.Parameters.AddWithValue("@sort",             a.SortOrder);
            cmd.Parameters.AddWithValue("@maxchoices",       a.MaxChoices);
            cmd.Parameters.AddWithValue("@pooltype",         a.ChoicePoolType);
            cmd.Parameters.AddWithValue("@recoveryInterval", a.RecoveryInterval);
            cmd.Parameters.AddWithValue("@recoveryAmount",   a.RecoveryAmount);
            cmd.Parameters.AddWithValue("@pickMode",         a.PickCountMode);
            cmd.Parameters.AddWithValue("@ccBase",           a.ChoiceCountBase);
            cmd.Parameters.AddWithValue("@ccAttr",           a.ChoiceCountAttribute);
            cmd.Parameters.AddWithValue("@ccProf",           a.ChoiceCountAddProf ? 1 : 0);
            cmd.Parameters.AddWithValue("@ccLevel",          a.ChoiceCountAddLevel);
            cmd.Parameters.AddWithValue("@typeId",            a.TypeId.HasValue ? a.TypeId.Value : DBNull.Value);
        }

        private static Ability Map(SqliteDataReader r) => new Ability
        {
            Id               = r.GetInt32(0),
            CampaignId       = r.GetInt32(1),
            Name             = r.GetString(2),
            Type             = r.GetString(3),
            Action           = r.GetString(4),
            Trigger          = r.GetString(5),
            Recovery         = r.GetString(6),
            Effect           = r.GetString(7),
            Notes            = r.GetString(8),
            SortOrder        = r.GetInt32(9),
            MaxChoices       = r.IsDBNull(10) ? 0  : r.GetInt32(10),
            ChoicePoolType   = r.IsDBNull(11) ? "" : r.GetString(11),
            RecoveryInterval     = r.IsDBNull(12) ? ""       : r.GetString(12),
            RecoveryAmount       = r.IsDBNull(13) ? 0        : r.GetInt32(13),
            PickCountMode        = r.IsDBNull(14) ? "formula": r.GetString(14),
            ChoiceCountBase      = r.IsDBNull(15) ? 0        : r.GetInt32(15),
            ChoiceCountAttribute = r.IsDBNull(16) ? ""       : r.GetString(16),
            ChoiceCountAddProf   = !r.IsDBNull(17) && r.GetInt32(17) != 0,
            ChoiceCountAddLevel  = r.IsDBNull(18) ? ""       : r.GetString(18),
            TypeId               = r.IsDBNull(19) ? null     : r.GetInt32(19),
        };

        // ── Ability costs CRUD ────────────────────────────────────────────────

        public List<AbilityCost> GetCosts(int abilityId)
        {
            var list = new List<AbilityCost>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id, resource_type_id, amount FROM ability_costs WHERE ability_id = @aid ORDER BY resource_type_id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityCost
                {
                    AbilityId      = reader.GetInt32(0),
                    ResourceTypeId = reader.GetInt32(1),
                    Amount         = reader.GetInt32(2),
                });
            return list;
        }

        public void AddCost(AbilityCost cost)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO ability_costs (ability_id, resource_type_id, amount)
                                VALUES (@aid, @rtid, @amount)";
            cmd.Parameters.AddWithValue("@aid",    cost.AbilityId);
            cmd.Parameters.AddWithValue("@rtid",   cost.ResourceTypeId);
            cmd.Parameters.AddWithValue("@amount", cost.Amount);
            cmd.ExecuteNonQuery();
        }

        public void UpdateCostAmount(int abilityId, int resourceTypeId, int amount)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_costs SET amount = @amount WHERE ability_id = @aid AND resource_type_id = @rtid";
            cmd.Parameters.AddWithValue("@aid",    abilityId);
            cmd.Parameters.AddWithValue("@rtid",   resourceTypeId);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.ExecuteNonQuery();
        }

        public void RemoveCost(int abilityId, int resourceTypeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_costs WHERE ability_id = @aid AND resource_type_id = @rtid";
            cmd.Parameters.AddWithValue("@aid",  abilityId);
            cmd.Parameters.AddWithValue("@rtid", resourceTypeId);
            cmd.ExecuteNonQuery();
        }

        // ── Ability Choices CRUD ──────────────────────────────────────────────

        public List<AbilityChoice> GetChoicesForAbility(int abilityId)
        {
            var list = new List<AbilityChoice>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ability_id, name, description, sort_order, linked_ability_id FROM ability_choices WHERE ability_id = @aid ORDER BY sort_order ASC, id ASC";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityChoice
                {
                    Id              = reader.GetInt32(0),
                    AbilityId       = reader.GetInt32(1),
                    Name            = reader.GetString(2),
                    Description     = reader.GetString(3),
                    SortOrder       = reader.GetInt32(4),
                    LinkedAbilityId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                });
            return list;
        }

        public int AddChoice(AbilityChoice choice)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_choices (ability_id, name, description, sort_order, linked_ability_id)
                                VALUES (@aid, @name, @desc, @sort, @link);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@aid",  choice.AbilityId);
            cmd.Parameters.AddWithValue("@name", choice.Name);
            cmd.Parameters.AddWithValue("@desc", choice.Description);
            cmd.Parameters.AddWithValue("@sort", choice.SortOrder);
            cmd.Parameters.AddWithValue("@link", choice.LinkedAbilityId.HasValue ? (object)choice.LinkedAbilityId.Value : DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void EditChoice(AbilityChoice choice)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_choices SET name = @name, description = @desc, sort_order = @sort, linked_ability_id = @link WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",   choice.Id);
            cmd.Parameters.AddWithValue("@name", choice.Name);
            cmd.Parameters.AddWithValue("@desc", choice.Description);
            cmd.Parameters.AddWithValue("@sort", choice.SortOrder);
            cmd.Parameters.AddWithValue("@link", choice.LinkedAbilityId.HasValue ? (object)choice.LinkedAbilityId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void DeleteChoice(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ability_choices WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
