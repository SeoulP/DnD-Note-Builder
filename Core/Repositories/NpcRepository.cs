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
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS npcs (
                id                  INTEGER PRIMARY KEY,
                campaign_id         INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name                TEXT    NOT NULL,
                species_id          INTEGER REFERENCES species(id) ON DELETE SET NULL,
                gender              TEXT    NOT NULL DEFAULT '',
                occupation          TEXT    NOT NULL DEFAULT '',
                description         TEXT    NOT NULL DEFAULT '',
                personality         TEXT    NOT NULL DEFAULT '',
                notes               TEXT    NOT NULL DEFAULT '',
                portrait_path       TEXT    NOT NULL DEFAULT '',
                status              INTEGER NOT NULL DEFAULT 0,
                relationship        INTEGER NOT NULL DEFAULT 2,
                home_location_id    INTEGER REFERENCES locations(id) ON DELETE SET NULL,
                first_seen_session  INTEGER
            )";
            cmd.ExecuteNonQuery();

            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS npc_factions (
                npc_id     INTEGER NOT NULL REFERENCES npcs(id)     ON DELETE CASCADE,
                faction_id INTEGER NOT NULL REFERENCES factions(id) ON DELETE CASCADE,
                PRIMARY KEY (npc_id, faction_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Npc> GetAll(int campaignId)
        {
            var list = new List<Npc>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, species_id, gender, occupation,
                                       description, personality, notes, portrait_path,
                                       status, relationship, home_location_id, first_seen_session
                                FROM npcs WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));

            // Load all faction memberships for this campaign in one query (avoids N+1)
            var factionMap = LoadFactionMap(campaignId);
            foreach (var npc in list)
                npc.FactionIds = factionMap.TryGetValue(npc.Id, out var fids) ? fids : new List<int>();

            return list;
        }

        public Npc Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, species_id, gender, occupation,
                                       description, personality, notes, portrait_path,
                                       status, relationship, home_location_id, first_seen_session
                                FROM npcs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var npc = Map(reader);
            reader.Close();
            npc.FactionIds = GetFactionIds(id);
            return npc;
        }

        public List<Npc> GetByFaction(int factionId)
        {
            var list = new List<Npc>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT n.id, n.campaign_id, n.name, n.species_id, n.gender, n.occupation,
                                       n.description, n.personality, n.notes, n.portrait_path,
                                       n.status, n.relationship, n.home_location_id, n.first_seen_session
                                FROM npcs n
                                JOIN npc_factions nf ON nf.npc_id = n.id
                                WHERE nf.faction_id = @fid ORDER BY n.name ASC";
            cmd.Parameters.AddWithValue("@fid", factionId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            foreach (var npc in list) npc.FactionIds = GetFactionIds(npc.Id);
            return list;
        }

        public int Add(Npc npc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO npcs
                                    (campaign_id, name, species_id, gender, occupation, description,
                                     personality, notes, portrait_path, status, relationship,
                                     home_location_id, first_seen_session)
                                VALUES
                                    (@cid, @name, @sid, @gender, @occ, @desc,
                                     @pers, @notes, @portrait, @status, @rel,
                                     @hlid, @fss);
                                SELECT last_insert_rowid();";
            Bind(cmd, npc);
            int newId = (int)(long)cmd.ExecuteScalar();
            SetFactions(newId, npc.FactionIds);
            return newId;
        }

        public void Edit(Npc npc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE npcs
                                SET name = @name, species_id = @sid, gender = @gender,
                                    occupation = @occ, description = @desc, personality = @pers,
                                    notes = @notes, portrait_path = @portrait,
                                    status = @status, relationship = @rel,
                                    home_location_id = @hlid, first_seen_session = @fss
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", npc.Id);
            Bind(cmd, npc);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM npcs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void AddFaction(int npcId, int factionId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO npc_factions (npc_id, faction_id) VALUES (@nid, @fid)";
            cmd.Parameters.AddWithValue("@nid", npcId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveFaction(int npcId, int factionId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM npc_factions WHERE npc_id = @nid AND faction_id = @fid";
            cmd.Parameters.AddWithValue("@nid", npcId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.ExecuteNonQuery();
        }

        private void SetFactions(int npcId, List<int> factionIds)
        {
            var del = _conn.CreateCommand();
            del.CommandText = "DELETE FROM npc_factions WHERE npc_id = @nid";
            del.Parameters.AddWithValue("@nid", npcId);
            del.ExecuteNonQuery();

            foreach (var fid in factionIds)
            {
                var ins = _conn.CreateCommand();
                ins.CommandText = "INSERT INTO npc_factions (npc_id, faction_id) VALUES (@nid, @fid)";
                ins.Parameters.AddWithValue("@nid", npcId);
                ins.Parameters.AddWithValue("@fid", fid);
                ins.ExecuteNonQuery();
            }
        }

        private List<int> GetFactionIds(int npcId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT faction_id FROM npc_factions WHERE npc_id = @nid";
            cmd.Parameters.AddWithValue("@nid", npcId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        private Dictionary<int, List<int>> LoadFactionMap(int campaignId)
        {
            var map = new Dictionary<int, List<int>>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT nf.npc_id, nf.faction_id
                                FROM npc_factions nf
                                JOIN npcs n ON n.id = nf.npc_id
                                WHERE n.campaign_id = @cid";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int npcId = reader.GetInt32(0);
                int facId = reader.GetInt32(1);
                if (!map.ContainsKey(npcId)) map[npcId] = new List<int>();
                map[npcId].Add(facId);
            }
            return map;
        }

        private static void Bind(SqliteCommand cmd, Npc n)
        {
            cmd.Parameters.AddWithValue("@cid",     n.CampaignId);
            cmd.Parameters.AddWithValue("@name",    n.Name);
            cmd.Parameters.AddWithValue("@sid",     n.SpeciesId.HasValue       ? n.SpeciesId.Value        : DBNull.Value);
            cmd.Parameters.AddWithValue("@gender",  n.Gender);
            cmd.Parameters.AddWithValue("@occ",     n.Occupation);
            cmd.Parameters.AddWithValue("@desc",    n.Description);
            cmd.Parameters.AddWithValue("@pers",    n.Personality);
            cmd.Parameters.AddWithValue("@notes",   n.Notes);
            cmd.Parameters.AddWithValue("@portrait",n.PortraitPath);
            cmd.Parameters.AddWithValue("@status",  (int)n.Status);
            cmd.Parameters.AddWithValue("@rel",     (int)n.Relationship);
            cmd.Parameters.AddWithValue("@hlid",    n.HomeLocationId.HasValue  ? n.HomeLocationId.Value   : DBNull.Value);
            cmd.Parameters.AddWithValue("@fss",     n.FirstSeenSession.HasValue? n.FirstSeenSession.Value : DBNull.Value);
        }

        private static Npc Map(SqliteDataReader r) => new Npc
        {
            Id               = r.GetInt32(0),
            CampaignId       = r.GetInt32(1),
            Name             = r.GetString(2),
            SpeciesId        = r.IsDBNull(3)  ? null : r.GetInt32(3),
            Gender           = r.GetString(4),
            Occupation       = r.GetString(5),
            Description      = r.GetString(6),
            Personality      = r.GetString(7),
            Notes            = r.GetString(8),
            PortraitPath     = r.GetString(9),
            Status           = (NpcStatus)r.GetInt32(10),
            Relationship     = (NpcRelationship)r.GetInt32(11),
            HomeLocationId   = r.IsDBNull(12) ? null : r.GetInt32(12),
            FirstSeenSession = r.IsDBNull(13) ? null : r.GetInt32(13),
        };
    }
}