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
                species             TEXT    NOT NULL DEFAULT '',
                gender              TEXT    NOT NULL DEFAULT '',
                occupation          TEXT    NOT NULL DEFAULT '',
                description         TEXT    NOT NULL DEFAULT '',
                personality         TEXT    NOT NULL DEFAULT '',
                notes               TEXT    NOT NULL DEFAULT '',
                portrait_path       TEXT    NOT NULL DEFAULT '',
                status              INTEGER NOT NULL DEFAULT 0,
                relationship        INTEGER NOT NULL DEFAULT 2,
                faction_id          INTEGER REFERENCES factions(id)  ON DELETE SET NULL,
                location_id         INTEGER REFERENCES locations(id) ON DELETE SET NULL,
                first_seen_session  INTEGER
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Npc> GetAll(int campaignId)
        {
            var list = new List<Npc>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, species, gender, occupation,
                                       description, personality, notes, portrait_path,
                                       status, relationship, faction_id, location_id, first_seen_session
                                FROM npcs WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Npc Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, species, gender, occupation,
                                       description, personality, notes, portrait_path,
                                       status, relationship, faction_id, location_id, first_seen_session
                                FROM npcs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public List<Npc> GetByFaction(int factionId)
        {
            var list = new List<Npc>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, species, gender, occupation,
                                       description, personality, notes, portrait_path,
                                       status, relationship, faction_id, location_id, first_seen_session
                                FROM npcs WHERE faction_id = @fid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@fid", factionId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public int Add(Npc npc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO npcs
                                    (campaign_id, name, species, gender, occupation, description,
                                     personality, notes, portrait_path, status, relationship,
                                     faction_id, location_id, first_seen_session)
                                VALUES
                                    (@cid, @name, @species, @gender, @occ, @desc,
                                     @pers, @notes, @portrait, @status, @rel,
                                     @fid, @lid, @fss);
                                SELECT last_insert_rowid();";
            Bind(cmd, npc);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Npc npc)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE npcs
                                SET name = @name, species = @species, gender = @gender,
                                    occupation = @occ, description = @desc, personality = @pers,
                                    notes = @notes, portrait_path = @portrait,
                                    status = @status, relationship = @rel,
                                    faction_id = @fid, location_id = @lid,
                                    first_seen_session = @fss
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

        private static void Bind(SqliteCommand cmd, Npc n)
        {
            cmd.Parameters.AddWithValue("@cid",     n.CampaignId);
            cmd.Parameters.AddWithValue("@name",    n.Name);
            cmd.Parameters.AddWithValue("@species", n.Species);
            cmd.Parameters.AddWithValue("@gender",  n.Gender);
            cmd.Parameters.AddWithValue("@occ",     n.Occupation);
            cmd.Parameters.AddWithValue("@desc",    n.Description);
            cmd.Parameters.AddWithValue("@pers",    n.Personality);
            cmd.Parameters.AddWithValue("@notes",   n.Notes);
            cmd.Parameters.AddWithValue("@portrait",n.PortraitPath);
            cmd.Parameters.AddWithValue("@status",  n.Status);
            cmd.Parameters.AddWithValue("@rel",     n.Relationship);
            cmd.Parameters.AddWithValue("@fid",     n.FactionId.HasValue    ? n.FactionId.Value        : DBNull.Value);
            cmd.Parameters.AddWithValue("@lid",     n.LocationId.HasValue   ? n.LocationId.Value       : DBNull.Value);
            cmd.Parameters.AddWithValue("@fss",     n.FirstSeenSession.HasValue ? n.FirstSeenSession.Value : DBNull.Value);
        }

        private static Npc Map(SqliteDataReader r) => new Npc
        {
            Id               = r.GetInt32(0),
            CampaignId       = r.GetInt32(1),
            Name             = r.GetString(2),
            Species          = r.GetString(3),
            Gender           = r.GetString(4),
            Occupation       = r.GetString(5),
            Description      = r.GetString(6),
            Personality      = r.GetString(7),
            Notes            = r.GetString(8),
            PortraitPath     = r.GetString(9),
            Status           = r.GetInt32(10),
            Relationship     = r.GetInt32(11),
            FactionId        = r.IsDBNull(12) ? null : r.GetInt32(12),
            LocationId       = r.IsDBNull(13) ? null : r.GetInt32(13),
            FirstSeenSession = r.IsDBNull(14) ? null : r.GetInt32(14),
        };
    }
}
