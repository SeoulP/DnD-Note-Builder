using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class EncounterRepository
    {
        private readonly SqliteConnection _conn;

        public EncounterRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS encounters (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id  INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                session_id   INTEGER REFERENCES sessions(id) ON DELETE SET NULL,
                name         TEXT NOT NULL DEFAULT 'New Encounter',
                started_at   TEXT NOT NULL DEFAULT '',
                is_resolved  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public int Add(Encounter e)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO encounters (campaign_id, session_id, name, started_at, is_resolved)
                                VALUES (@cid, @sid, @name, @sat, @res);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  e.CampaignId);
            cmd.Parameters.AddWithValue("@sid",  e.SessionId.HasValue ? (object)e.SessionId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@name", e.Name);
            cmd.Parameters.AddWithValue("@sat",  e.StartedAt);
            cmd.Parameters.AddWithValue("@res",  e.IsResolved ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Encounter e)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE encounters SET session_id=@sid, name=@name, started_at=@sat, is_resolved=@res WHERE id=@id";
            cmd.Parameters.AddWithValue("@sid",  e.SessionId.HasValue ? (object)e.SessionId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@name", e.Name);
            cmd.Parameters.AddWithValue("@sat",  e.StartedAt);
            cmd.Parameters.AddWithValue("@res",  e.IsResolved ? 1 : 0);
            cmd.Parameters.AddWithValue("@id",   e.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM encounters WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetResolved(int id, bool resolved)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE encounters SET is_resolved=@r WHERE id=@id";
            cmd.Parameters.AddWithValue("@r",  resolved ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public Encounter Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, session_id, name, started_at, is_resolved FROM encounters WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<Encounter> GetAll(int campaignId)
        {
            var list = new List<Encounter>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, session_id, name, started_at, is_resolved FROM encounters WHERE campaign_id=@cid ORDER BY started_at DESC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<Encounter> GetBySession(int sessionId)
        {
            var list = new List<Encounter>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, session_id, name, started_at, is_resolved FROM encounters WHERE session_id=@sid ORDER BY started_at DESC";
            cmd.Parameters.AddWithValue("@sid", sessionId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        private static Encounter Map(SqliteDataReader r) => new Encounter
        {
            Id         = r.GetInt32(0),
            CampaignId = r.GetInt32(1),
            SessionId  = r.IsDBNull(2) ? null : r.GetInt32(2),
            Name       = r.GetString(3),
            StartedAt  = r.GetString(4),
            IsResolved = r.GetInt32(5) == 1,
        };
    }
}
