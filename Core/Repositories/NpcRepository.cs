using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class NpcRepository
    {
        private readonly SqliteConnection _conn;

        public NpcRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            // characters — shared base for NPCs and Player Characters
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS characters (
                id            INTEGER PRIMARY KEY,
                campaign_id   INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name          TEXT    NOT NULL DEFAULT '',
                portrait_path TEXT    NOT NULL DEFAULT '',
                gender        TEXT    NOT NULL DEFAULT '',
                occupation    TEXT    NOT NULL DEFAULT '',
                description   TEXT    NOT NULL DEFAULT '',
                personality   TEXT    NOT NULL DEFAULT '',
                notes         TEXT    NOT NULL DEFAULT '',
                species_id    INTEGER REFERENCES species(id) ON DELETE SET NULL
            )";
            cmd.ExecuteNonQuery();

            // npcs — NPC-specific fields; id is shared with characters (Table-Per-Type)
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS npcs (
                id                   INTEGER PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE,
                home_location_id     INTEGER REFERENCES locations(id)              ON DELETE SET NULL,
                first_seen_session   INTEGER,
                relationship_type_id INTEGER REFERENCES npc_relationship_types(id) ON DELETE SET NULL,
                status_id            INTEGER REFERENCES npc_statuses(id)           ON DELETE SET NULL
            )";
            cmd.ExecuteNonQuery();

            // player_characters — placeholder for Phase 2; id shared with characters
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS player_characters (
                id INTEGER PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE
            )";
            cmd.ExecuteNonQuery();

            // character_factions — replaces npc_factions; keyed on character.id
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_factions (
                character_id INTEGER NOT NULL REFERENCES characters(id)    ON DELETE CASCADE,
                faction_id   INTEGER NOT NULL REFERENCES factions(id)      ON DELETE CASCADE,
                role_id      INTEGER          REFERENCES npc_faction_roles(id) ON DELETE SET NULL,
                PRIMARY KEY (character_id, faction_id)
            )";
            cmd.ExecuteNonQuery();

            // character_relationships join table
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_relationships (
                character_id         INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                related_character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                relationship_type_id INTEGER REFERENCES character_relationship_types(id) ON DELETE SET NULL,
                PRIMARY KEY (character_id, related_character_id)
            )";
            cmd.ExecuteNonQuery();

            // Migration: add to_type_id to character_relationships (per-side independent relationship type)
            var hasToTypeId = _conn.CreateCommand();
            hasToTypeId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('character_relationships') WHERE name = 'to_type_id'";
            if ((long)hasToTypeId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE character_relationships ADD COLUMN to_type_id INTEGER REFERENCES character_relationship_types(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }

            // Migration: add role_id to existing character_factions tables
            var hasRoleId = _conn.CreateCommand();
            hasRoleId.CommandText = "SELECT COUNT(*) FROM pragma_table_info('character_factions') WHERE name = 'role_id'";
            if ((long)hasRoleId.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE character_factions ADD COLUMN role_id INTEGER REFERENCES npc_faction_roles(id) ON DELETE SET NULL";
                alter.ExecuteNonQuery();
            }
        }

        private const string SelectColumns = @"
            c.id, c.campaign_id, c.name, c.portrait_path, c.gender, c.occupation,
            c.description, c.personality, c.notes, c.species_id,
            n.home_location_id, n.first_seen_session, n.relationship_type_id, n.status_id";

        private const string FromJoin = @"
            FROM characters c
            JOIN npcs n ON n.id = c.id";

        public List<Npc> GetAll(int campaignId)
        {
            var list = new List<Npc>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} {FromJoin} WHERE c.campaign_id = @cid ORDER BY c.name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));

            var factionMap = LoadFactionMap(campaignId);
            foreach (var npc in list)
            {
                npc.Factions   = factionMap.TryGetValue(npc.Id, out var facs) ? facs : new List<DndBuilder.Core.Models.NpcFaction>();
                npc.FactionIds = npc.Factions.ConvertAll(f => f.FactionId);
            }

            return list;
        }

        public Npc Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} {FromJoin} WHERE c.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var npc = Map(reader);
            reader.Close();
            npc.Factions       = GetFactions(id);
            npc.FactionIds     = npc.Factions.ConvertAll(f => f.FactionId);
            npc.Relationships  = GetRelationships(id);
            return npc;
        }

        public List<Npc> GetByFaction(int factionId)
        {
            var list = new List<Npc>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = $@"SELECT {SelectColumns} {FromJoin}
                                JOIN character_factions cf ON cf.character_id = c.id
                                WHERE cf.faction_id = @fid ORDER BY c.name ASC";
            cmd.Parameters.AddWithValue("@fid", factionId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            foreach (var npc in list)
            {
                npc.Factions   = GetFactions(npc.Id);
                npc.FactionIds = npc.Factions.ConvertAll(f => f.FactionId);
            }
            return list;
        }

        public int Add(Npc npc)
        {
            // Insert shared character row first
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO characters
                                    (campaign_id, name, portrait_path, gender, occupation, description,
                                     personality, notes, species_id)
                                VALUES
                                    (@cid, @name, @portrait, @gender, @occ, @desc, @pers, @notes, @sid);
                                SELECT last_insert_rowid();";
            BindCharacter(cmd, npc);
            int newId = (int)(long)cmd.ExecuteScalar();

            // Insert NPC-specific row using the same id
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO npcs
                                    (id, home_location_id, first_seen_session, relationship_type_id, status_id)
                                VALUES (@id, @hlid, @fss, @relTypeId, @statusId)";
            cmd.Parameters.AddWithValue("@id", newId);
            BindNpc(cmd, npc);
            cmd.ExecuteNonQuery();

            SetFactions(newId, npc.Factions);
            return newId;
        }

        public void Edit(Npc npc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE characters
                                SET name = @name, portrait_path = @portrait, gender = @gender,
                                    occupation = @occ, description = @desc, personality = @pers,
                                    notes = @notes, species_id = @sid
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", npc.Id);
            BindCharacter(cmd, npc);
            cmd.ExecuteNonQuery();

            cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE npcs
                                SET home_location_id = @hlid, first_seen_session = @fss,
                                    relationship_type_id = @relTypeId, status_id = @statusId
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", npc.Id);
            BindNpc(cmd, npc);
            cmd.ExecuteNonQuery();

            SetFactions(npc.Id, npc.Factions);
        }

        public void Delete(int id)
        {
            // Deleting from characters cascades to npcs and character_factions
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM characters WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void AddFaction(int characterId, int factionId, int? roleId = null)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO character_factions (character_id, faction_id, role_id) VALUES (@cid, @fid, @rid)";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.Parameters.AddWithValue("@rid", roleId.HasValue ? roleId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void RemoveFaction(int characterId, int factionId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_factions WHERE character_id = @cid AND faction_id = @fid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.ExecuteNonQuery();
        }

        public List<CharacterRelationship> GetRelationships(int characterId)
        {
            var list = new List<CharacterRelationship>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT character_id, related_character_id, relationship_type_id FROM character_relationships WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new CharacterRelationship
                {
                    CharacterId        = reader.GetInt32(0),
                    RelatedCharacterId = reader.GetInt32(1),
                    RelationshipTypeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                });
            return list;
        }

        public void AddRelationship(int characterId, int relatedCharacterId, int? typeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO character_relationships (character_id, related_character_id, relationship_type_id) VALUES (@cid, @rcid, @tid)";
            cmd.Parameters.AddWithValue("@cid",  characterId);
            cmd.Parameters.AddWithValue("@rcid", relatedCharacterId);
            cmd.Parameters.AddWithValue("@tid",  typeId.HasValue ? typeId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void RemoveRelationship(int characterId, int relatedCharacterId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_relationships WHERE character_id = @cid AND related_character_id = @rcid";
            cmd.Parameters.AddWithValue("@cid",  characterId);
            cmd.Parameters.AddWithValue("@rcid", relatedCharacterId);
            cmd.ExecuteNonQuery();
        }

        private void SetFactions(int characterId, List<DndBuilder.Core.Models.NpcFaction> factions)
        {
            var del = _conn.CreateCommand();
            del.CommandText = "DELETE FROM character_factions WHERE character_id = @cid";
            del.Parameters.AddWithValue("@cid", characterId);
            del.ExecuteNonQuery();

            foreach (var f in factions)
            {
                var ins = _conn.CreateCommand();
                ins.CommandText = "INSERT INTO character_factions (character_id, faction_id, role_id) VALUES (@cid, @fid, @rid)";
                ins.Parameters.AddWithValue("@cid", characterId);
                ins.Parameters.AddWithValue("@fid", f.FactionId);
                ins.Parameters.AddWithValue("@rid", f.RoleId.HasValue ? f.RoleId.Value : DBNull.Value);
                ins.ExecuteNonQuery();
            }
        }

        private List<DndBuilder.Core.Models.NpcFaction> GetFactions(int characterId)
        {
            var list = new List<DndBuilder.Core.Models.NpcFaction>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT character_id, faction_id, role_id FROM character_factions WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new DndBuilder.Core.Models.NpcFaction
                {
                    NpcId     = reader.GetInt32(0),
                    FactionId = reader.GetInt32(1),
                    RoleId    = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                });
            return list;
        }

        private Dictionary<int, List<DndBuilder.Core.Models.NpcFaction>> LoadFactionMap(int campaignId)
        {
            var map = new Dictionary<int, List<DndBuilder.Core.Models.NpcFaction>>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT cf.character_id, cf.faction_id, cf.role_id
                                FROM character_factions cf
                                JOIN characters c ON c.id = cf.character_id
                                WHERE c.campaign_id = @cid";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int charId = reader.GetInt32(0);
                if (!map.ContainsKey(charId)) map[charId] = new List<DndBuilder.Core.Models.NpcFaction>();
                map[charId].Add(new DndBuilder.Core.Models.NpcFaction
                {
                    NpcId     = charId,
                    FactionId = reader.GetInt32(1),
                    RoleId    = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                });
            }
            return map;
        }

        private static void BindCharacter(SqliteCommand cmd, Npc n)
        {
            cmd.Parameters.AddWithValue("@cid",     n.CampaignId);
            cmd.Parameters.AddWithValue("@name",    n.Name);
            cmd.Parameters.AddWithValue("@portrait",n.PortraitPath);
            cmd.Parameters.AddWithValue("@gender",  n.Gender);
            cmd.Parameters.AddWithValue("@occ",     n.Occupation);
            cmd.Parameters.AddWithValue("@desc",    n.Description);
            cmd.Parameters.AddWithValue("@pers",    n.Personality);
            cmd.Parameters.AddWithValue("@notes",   n.Notes);
            cmd.Parameters.AddWithValue("@sid",     n.SpeciesId.HasValue ? n.SpeciesId.Value : DBNull.Value);
        }

        private static void BindNpc(SqliteCommand cmd, Npc n)
        {
            cmd.Parameters.AddWithValue("@hlid",      n.HomeLocationId.HasValue     ? n.HomeLocationId.Value     : DBNull.Value);
            cmd.Parameters.AddWithValue("@fss",       n.FirstSeenSession.HasValue   ? n.FirstSeenSession.Value   : DBNull.Value);
            cmd.Parameters.AddWithValue("@relTypeId", n.RelationshipTypeId.HasValue ? n.RelationshipTypeId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@statusId",  n.StatusId.HasValue           ? n.StatusId.Value           : DBNull.Value);
        }

        private static Npc Map(SqliteDataReader r) => new Npc
        {
            // Character fields (columns 0–9)
            Id                 = r.GetInt32(0),
            CampaignId         = r.GetInt32(1),
            Name               = r.GetString(2),
            PortraitPath       = r.GetString(3),
            Gender             = r.GetString(4),
            Occupation         = r.GetString(5),
            Description        = r.GetString(6),
            Personality        = r.GetString(7),
            Notes              = r.GetString(8),
            SpeciesId          = r.IsDBNull(9)  ? null : r.GetInt32(9),
            // NPC-specific fields (columns 10–13)
            HomeLocationId     = r.IsDBNull(10) ? null : r.GetInt32(10),
            FirstSeenSession   = r.IsDBNull(11) ? null : r.GetInt32(11),
            RelationshipTypeId = r.IsDBNull(12) ? null : r.GetInt32(12),
            StatusId           = r.IsDBNull(13) ? null : r.GetInt32(13),
        };
    }
}