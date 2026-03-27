using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class PlayerCharacterRepository
    {
        private readonly SqliteConnection _conn;

        public PlayerCharacterRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            // player_characters — NpcRepository creates the base table as a guard;
            // we add new columns here via additive migrations.

            var hasClassId = _conn.CreateCommand();
            hasClassId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'class_id'";
            if ((long)hasClassId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN class_id INTEGER REFERENCES classes(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var hasSubclassId = _conn.CreateCommand();
            hasSubclassId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'subclass_id'";
            if ((long)hasSubclassId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN subclass_id INTEGER REFERENCES subclasses(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var hasSubspeciesId = _conn.CreateCommand();
            hasSubspeciesId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'subspecies_id'";
            if ((long)hasSubspeciesId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN subspecies_id INTEGER REFERENCES subspecies(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var hasLevel = _conn.CreateCommand();
            hasLevel.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'level'";
            if ((long)hasLevel.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN level INTEGER NOT NULL DEFAULT 1";
                alter.ExecuteNonQuery();
            }

            var hasSubspeciesText = _conn.CreateCommand();
            hasSubspeciesText.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'subspecies_text'";
            if ((long)hasSubspeciesText.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN subspecies_text TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }

            var hasClassText = _conn.CreateCommand();
            hasClassText.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'class_text'";
            if ((long)hasClassText.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN class_text TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }

            var hasSubclassText = _conn.CreateCommand();
            hasSubclassText.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'subclass_text'";
            if ((long)hasSubclassText.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN subclass_text TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }

            foreach (var col in new[] { "str", "dex", "con", "int", "wis", "cha" })
            {
                var check = _conn.CreateCommand();
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = '{col}'";
                if ((long)check.ExecuteScalar() == 0)
                {
                    var alter = _conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE player_characters ADD COLUMN {col} INTEGER NOT NULL DEFAULT 10";
                    alter.ExecuteNonQuery();
                }
            }

            var hasBackgroundId = _conn.CreateCommand();
            hasBackgroundId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'background_id'";
            if ((long)hasBackgroundId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN background_id INTEGER REFERENCES dnd5e_backgrounds(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            var hasBackgroundAsi = _conn.CreateCommand();
            hasBackgroundAsi.CommandText = "SELECT COUNT(*) FROM pragma_table_info('player_characters') WHERE name = 'background_asi'";
            if ((long)hasBackgroundAsi.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE player_characters ADD COLUMN background_asi TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }
        }

        // ── Manual abilities ──────────────────────────────────────────────────

        public void AddManualAbility(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO character_abilities (character_id, ability_id, source)
                                VALUES (@cid, @aid, 'manual')
                                ON CONFLICT(character_id, ability_id) DO NOTHING";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveManualAbility(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_abilities WHERE character_id = @cid AND ability_id = @aid AND source = 'manual'";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public List<int> GetManualAbilityIds(int characterId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM character_abilities WHERE character_id = @cid AND source = 'manual'";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetInt32(0));
            return list;
        }

        // ── Background abilities ───────────────────────────────────────────────

        public void AddBackgroundAbility(int characterId, int abilityId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO character_abilities (character_id, ability_id, source)
                                VALUES (@cid, @aid, 'background')
                                ON CONFLICT(character_id, ability_id) DO NOTHING";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@aid", abilityId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveBackgroundAbilities(int characterId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_abilities WHERE character_id = @cid AND source = 'background'";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.ExecuteNonQuery();
        }

        public List<int> GetBackgroundAbilityIds(int characterId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT ability_id FROM character_abilities WHERE character_id = @cid AND source = 'background'";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetInt32(0));
            return list;
        }

        // ── Character resources CRUD ──────────────────────────────────────────

        public void MigrateResources()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_resources (
                character_id     INTEGER NOT NULL REFERENCES characters(id)              ON DELETE CASCADE,
                resource_type_id INTEGER NOT NULL REFERENCES ability_resource_types(id)  ON DELETE CASCADE,
                current_amount   INTEGER NOT NULL DEFAULT 0,
                maximum_amount   INTEGER NOT NULL DEFAULT 0,
                value_text       TEXT    NOT NULL DEFAULT '',
                notes            TEXT    NOT NULL DEFAULT '',
                PRIMARY KEY (character_id, resource_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<CharacterResource> GetResources(int characterId)
        {
            var list = new List<CharacterResource>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT character_id, resource_type_id, current_amount, maximum_amount, value_text, notes
                                FROM character_resources WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new CharacterResource
                {
                    CharacterId    = reader.GetInt32(0),
                    ResourceTypeId = reader.GetInt32(1),
                    CurrentAmount  = reader.GetInt32(2),
                    MaximumAmount  = reader.GetInt32(3),
                    ValueText      = reader.GetString(4),
                    Notes          = reader.GetString(5),
                });
            return list;
        }

        public void UpsertResource(CharacterResource resource)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO character_resources (character_id, resource_type_id, current_amount, maximum_amount, value_text, notes)
                                VALUES (@cid, @rtid, @cur, @max, @vtext, @notes)
                                ON CONFLICT(character_id, resource_type_id) DO UPDATE SET
                                    current_amount = excluded.current_amount,
                                    maximum_amount = excluded.maximum_amount,
                                    value_text     = excluded.value_text,
                                    notes          = excluded.notes";
            cmd.Parameters.AddWithValue("@cid",   resource.CharacterId);
            cmd.Parameters.AddWithValue("@rtid",  resource.ResourceTypeId);
            cmd.Parameters.AddWithValue("@cur",   resource.CurrentAmount);
            cmd.Parameters.AddWithValue("@max",   resource.MaximumAmount);
            cmd.Parameters.AddWithValue("@vtext", resource.ValueText);
            cmd.Parameters.AddWithValue("@notes", resource.Notes);
            cmd.ExecuteNonQuery();
        }

        public void DeleteResource(int characterId, int resourceTypeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_resources WHERE character_id = @cid AND resource_type_id = @rtid";
            cmd.Parameters.AddWithValue("@cid",  characterId);
            cmd.Parameters.AddWithValue("@rtid", resourceTypeId);
            cmd.ExecuteNonQuery();
        }

        // ── Resource sync helpers ─────────────────────────────────────────────

        private HashSet<int> GetOwnedAbilityIds(PlayerCharacter pc)
        {
            var ids = new HashSet<int>();

            if (pc.ClassId.HasValue)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"SELECT la.ability_id FROM class_level_abilities la
                                    JOIN class_levels cl ON cl.id = la.level_id
                                    WHERE cl.class_id = @cid AND cl.level <= @level";
                cmd.Parameters.AddWithValue("@cid",   pc.ClassId.Value);
                cmd.Parameters.AddWithValue("@level", pc.Level);
                using var r = cmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt32(0));
            }

            if (pc.SubclassId.HasValue)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"SELECT ability_id FROM subclass_abilities
                                    WHERE subclass_id = @sid AND required_level <= @level";
                cmd.Parameters.AddWithValue("@sid",   pc.SubclassId.Value);
                cmd.Parameters.AddWithValue("@level", pc.Level);
                using var r = cmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt32(0));
            }

            if (pc.SpeciesId.HasValue)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT ability_id FROM species_abilities WHERE species_id = @sid";
                cmd.Parameters.AddWithValue("@sid", pc.SpeciesId.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt32(0));

                // Species level abilities — up to character level
                var slCmd = _conn.CreateCommand();
                slCmd.CommandText = @"SELECT asl.ability_id
                                      FROM ability_species_levels asl
                                      JOIN species_levels sl ON sl.id = asl.species_level_id
                                      WHERE sl.species_id = @sid AND sl.level <= @level";
                slCmd.Parameters.AddWithValue("@sid",   pc.SpeciesId.Value);
                slCmd.Parameters.AddWithValue("@level", pc.Level);
                using var slr = slCmd.ExecuteReader();
                while (slr.Read()) ids.Add(slr.GetInt32(0));
            }

            if (pc.SubspeciesId.HasValue)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT ability_id FROM subspecies_abilities WHERE subspecies_id = @sid";
                cmd.Parameters.AddWithValue("@sid", pc.SubspeciesId.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt32(0));
            }

            return ids;
        }

        private HashSet<int> GetResourceTypesForAbilities(HashSet<int> abilityIds)
        {
            var rtIds = new HashSet<int>();
            if (abilityIds.Count == 0) return rtIds;
            var cmd = _conn.CreateCommand();
            cmd.CommandText = $@"SELECT DISTINCT ac.resource_type_id
                                 FROM ability_costs ac
                                 JOIN ability_resource_types art ON art.id = ac.resource_type_id
                                 WHERE ac.ability_id IN ({string.Join(",", abilityIds)})
                                   AND art.inactive = 0";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rtIds.Add(r.GetInt32(0));
            return rtIds;
        }

        private int CalculateResourceMax(PlayerCharacter pc, int resourceTypeId)
        {
            var costCmd = _conn.CreateCommand();
            costCmd.CommandText = "SELECT ability_id FROM ability_costs WHERE resource_type_id = @rtid";
            costCmd.Parameters.AddWithValue("@rtid", resourceTypeId);
            var costAbilityIds = new HashSet<int>();
            using (var r = costCmd.ExecuteReader()) while (r.Read()) costAbilityIds.Add(r.GetInt32(0));

            int max = 0;

            // Class levels — iterate ASC so higher levels overwrite lower
            if (pc.ClassId.HasValue)
            {
                var lvCmd = _conn.CreateCommand();
                lvCmd.CommandText = "SELECT class_data FROM class_levels WHERE class_id = @cid AND level <= @level ORDER BY level ASC";
                lvCmd.Parameters.AddWithValue("@cid",   pc.ClassId.Value);
                lvCmd.Parameters.AddWithValue("@level", pc.Level);
                using var r = lvCmd.ExecuteReader();
                while (r.Read())
                {
                    var data = r.IsDBNull(0) ? "" : r.GetString(0);
                    foreach (var seg in data.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var p = seg.Split(':', 2);
                        if (p.Length == 2
                            && int.TryParse(p[0].Trim(), out int abilityId)
                            && costAbilityIds.Contains(abilityId)
                            && int.TryParse(p[1].Trim(), out int uses)
                            && uses > 0)
                            max = uses;
                    }
                }
            }

            // Subclass abilities — iterate ASC by required_level, last wins
            if (pc.SubclassId.HasValue)
            {
                var scCmd = _conn.CreateCommand();
                scCmd.CommandText = @"SELECT ability_id, uses FROM subclass_abilities
                                      WHERE subclass_id = @sid AND required_level <= @level
                                      ORDER BY required_level ASC";
                scCmd.Parameters.AddWithValue("@sid",   pc.SubclassId.Value);
                scCmd.Parameters.AddWithValue("@level", pc.Level);
                using var r = scCmd.ExecuteReader();
                while (r.Read())
                {
                    int    abilityId = r.GetInt32(0);
                    string usesStr   = r.IsDBNull(1) ? "" : r.GetString(1);
                    if (costAbilityIds.Contains(abilityId)
                        && int.TryParse(usesStr, out int uses)
                        && uses > 0)
                        max = uses;
                }
            }

            // Species levels — iterate ASC so higher levels overwrite lower
            if (pc.SpeciesId.HasValue)
            {
                var slCmd = _conn.CreateCommand();
                slCmd.CommandText = @"SELECT sl.class_data
                                      FROM species_levels sl
                                      WHERE sl.species_id = @sid AND sl.level <= @level
                                      ORDER BY sl.level ASC";
                slCmd.Parameters.AddWithValue("@sid",   pc.SpeciesId.Value);
                slCmd.Parameters.AddWithValue("@level", pc.Level);
                using var r = slCmd.ExecuteReader();
                while (r.Read())
                {
                    var data = r.IsDBNull(0) ? "" : r.GetString(0);
                    foreach (var seg in data.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var p = seg.Split(':', 2);
                        if (p.Length == 2
                            && int.TryParse(p[0].Trim(), out int abilityId)
                            && costAbilityIds.Contains(abilityId)
                            && int.TryParse(p[1].Trim(), out int uses)
                            && uses > 0)
                            max = uses;
                    }
                }
            }

            return max;
        }

        /// <summary>
        /// Discovers all resource types the PC's current abilities require, computes the max for each,
        /// upserts rows (preserving current amount), and removes stale rows.
        /// </summary>
        public void SyncResources(PlayerCharacter pc)
        {
            var abilityIds      = GetOwnedAbilityIds(pc);
            var resourceTypeIds = GetResourceTypesForAbilities(abilityIds);

            foreach (int rtId in resourceTypeIds)
            {
                int newMax = CalculateResourceMax(pc, rtId);
                if (newMax <= 0) continue;

                var existCmd = _conn.CreateCommand();
                existCmd.CommandText = "SELECT current_amount FROM character_resources WHERE character_id = @cid AND resource_type_id = @rtid";
                existCmd.Parameters.AddWithValue("@cid",  pc.Id);
                existCmd.Parameters.AddWithValue("@rtid", rtId);
                var existResult = existCmd.ExecuteScalar();
                int current = existResult == null ? newMax : Math.Min((int)(long)existResult, newMax);

                UpsertResource(new CharacterResource
                {
                    CharacterId    = pc.Id,
                    ResourceTypeId = rtId,
                    CurrentAmount  = current,
                    MaximumAmount  = newMax,
                });
            }

            // Remove stale rows
            if (resourceTypeIds.Count > 0)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = $"DELETE FROM character_resources WHERE character_id = @cid AND resource_type_id NOT IN ({string.Join(",", resourceTypeIds)})";
                cmd.Parameters.AddWithValue("@cid", pc.Id);
                cmd.ExecuteNonQuery();
            }
            else
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "DELETE FROM character_resources WHERE character_id = @cid";
                cmd.Parameters.AddWithValue("@cid", pc.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public List<(CharacterResource resource, string name)> GetResourcesWithNames(int characterId)
        {
            var list = new List<(CharacterResource, string)>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT cr.character_id, cr.resource_type_id, cr.current_amount, cr.maximum_amount,
                                       cr.value_text, cr.notes, art.name
                                FROM character_resources cr
                                JOIN ability_resource_types art ON art.id = cr.resource_type_id AND art.inactive = 0
                                WHERE cr.character_id = @cid
                                ORDER BY art.name ASC";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var res = new CharacterResource
                {
                    CharacterId    = r.GetInt32(0),
                    ResourceTypeId = r.GetInt32(1),
                    CurrentAmount  = r.GetInt32(2),
                    MaximumAmount  = r.GetInt32(3),
                    ValueText      = r.GetString(4),
                    Notes          = r.GetString(5),
                };
                list.Add((res, r.GetString(6)));
            }
            return list;
        }

        private const string SelectColumns = @"
            c.id, c.campaign_id, c.name, c.portrait_path, c.gender, c.occupation,
            c.description, c.personality, c.notes, c.species_id,
            pc.class_id, pc.subclass_id, pc.subspecies_id, pc.level,
            pc.str, pc.dex, pc.con, pc.int, pc.wis, pc.cha,
            pc.background_id, pc.background_asi";

        private const string FromJoin = @"
            FROM characters c
            JOIN player_characters pc ON pc.id = c.id";

        public List<PlayerCharacter> GetAll(int campaignId)
        {
            var list = new List<PlayerCharacter>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} {FromJoin} WHERE c.campaign_id = @cid ORDER BY c.name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public PlayerCharacter Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} {FromJoin} WHERE c.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(PlayerCharacter pc)
        {
            // Insert shared character row
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO characters
                                    (campaign_id, name, portrait_path, gender, occupation, description,
                                     personality, notes, species_id)
                                VALUES
                                    (@cid, @name, @portrait, @gender, @occ, @desc, @pers, @notes, @sid);
                                SELECT last_insert_rowid();";
            BindCharacter(cmd, pc);
            int newId = (int)(long)cmd.ExecuteScalar();

            // Insert PC-specific row
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO player_characters (id, class_id, subclass_id, subspecies_id, level, str, dex, con, int, wis, cha, background_id, background_asi)
                                VALUES (@id, @clid, @scid, @ssid, @level, @str, @dex, @con, @int, @wis, @cha, @bgid, @bgasi)";
            cmd.Parameters.AddWithValue("@id", newId);
            BindPc(cmd, pc);
            cmd.ExecuteNonQuery();

            return newId;
        }

        public void Edit(PlayerCharacter pc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE characters
                                SET name = @name, portrait_path = @portrait, gender = @gender,
                                    occupation = @occ, description = @desc, personality = @pers,
                                    notes = @notes, species_id = @sid
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", pc.Id);
            BindCharacter(cmd, pc);
            cmd.ExecuteNonQuery();

            cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE player_characters
                                SET class_id = @clid, subclass_id = @scid, subspecies_id = @ssid, level = @level,
                                    str = @str, dex = @dex, con = @con, int = @int, wis = @wis, cha = @cha,
                                    background_id = @bgid, background_asi = @bgasi
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", pc.Id);
            BindPc(cmd, pc);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            // Deleting from characters cascades to player_characters and character_abilities
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM characters WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void BindCharacter(SqliteCommand cmd, PlayerCharacter pc)
        {
            cmd.Parameters.AddWithValue("@cid",     pc.CampaignId);
            cmd.Parameters.AddWithValue("@name",    pc.Name);
            cmd.Parameters.AddWithValue("@portrait",pc.PortraitPath);
            cmd.Parameters.AddWithValue("@gender",  pc.Gender);
            cmd.Parameters.AddWithValue("@occ",     pc.Occupation);
            cmd.Parameters.AddWithValue("@desc",    pc.Description);
            cmd.Parameters.AddWithValue("@pers",    pc.Personality);
            cmd.Parameters.AddWithValue("@notes",   pc.Notes);
            cmd.Parameters.AddWithValue("@sid",     pc.SpeciesId.HasValue ? pc.SpeciesId.Value : DBNull.Value);
        }

        private static void BindPc(SqliteCommand cmd, PlayerCharacter pc)
        {
            cmd.Parameters.AddWithValue("@clid",  pc.ClassId.HasValue      ? pc.ClassId.Value      : DBNull.Value);
            cmd.Parameters.AddWithValue("@scid",  pc.SubclassId.HasValue   ? pc.SubclassId.Value   : DBNull.Value);
            cmd.Parameters.AddWithValue("@ssid",  pc.SubspeciesId.HasValue ? pc.SubspeciesId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@level", pc.Level);
            cmd.Parameters.AddWithValue("@str",   pc.Strength);
            cmd.Parameters.AddWithValue("@dex",   pc.Dexterity);
            cmd.Parameters.AddWithValue("@con",   pc.Constitution);
            cmd.Parameters.AddWithValue("@int",   pc.Intelligence);
            cmd.Parameters.AddWithValue("@wis",   pc.Wisdom);
            cmd.Parameters.AddWithValue("@cha",   pc.Charisma);
            cmd.Parameters.AddWithValue("@bgid",  pc.BackgroundId.HasValue ? pc.BackgroundId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@bgasi", pc.BackgroundAsi ?? "");
        }

        private static PlayerCharacter Map(SqliteDataReader r) => new PlayerCharacter
        {
            // Character base fields (0–9)
            Id           = r.GetInt32(0),
            CampaignId   = r.GetInt32(1),
            Name         = r.GetString(2),
            PortraitPath = r.GetString(3),
            Gender       = r.GetString(4),
            Occupation   = r.GetString(5),
            Description  = r.GetString(6),
            Personality  = r.GetString(7),
            Notes        = r.GetString(8),
            SpeciesId    = r.IsDBNull(9)  ? null : r.GetInt32(9),
            // PC-specific fields (10–19)
            ClassId      = r.IsDBNull(10) ? null : r.GetInt32(10),
            SubclassId   = r.IsDBNull(11) ? null : r.GetInt32(11),
            SubspeciesId = r.IsDBNull(12) ? null : r.GetInt32(12),
            Level        = r.IsDBNull(13) ? 1    : r.GetInt32(13),
            Strength     = r.IsDBNull(14) ? 10   : r.GetInt32(14),
            Dexterity    = r.IsDBNull(15) ? 10   : r.GetInt32(15),
            Constitution = r.IsDBNull(16) ? 10   : r.GetInt32(16),
            Intelligence = r.IsDBNull(17) ? 10   : r.GetInt32(17),
            Wisdom       = r.IsDBNull(18) ? 10   : r.GetInt32(18),
            Charisma     = r.IsDBNull(19) ? 10   : r.GetInt32(19),
            BackgroundId  = r.IsDBNull(20) ? null : r.GetInt32(20),
            BackgroundAsi = r.IsDBNull(21) ? ""   : r.GetString(21),
        };
    }
}
