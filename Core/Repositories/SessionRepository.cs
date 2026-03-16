using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class SessionRepository
    {
        private readonly SqliteConnection _conn;

        public SessionRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS sessions (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                number      INTEGER NOT NULL,
                title       TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                played_on   TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, number)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Session> GetAll(int campaignId)
        {
            var list = new List<Session>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, number, title, notes, played_on
                                FROM sessions WHERE campaign_id = @cid ORDER BY number ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Session Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, number, title, notes, played_on
                                FROM sessions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Session session)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO sessions (campaign_id, number, title, notes, played_on)
                                VALUES (@cid, @num, @title, @notes, @played);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",    session.CampaignId);
            cmd.Parameters.AddWithValue("@num",    session.Number);
            cmd.Parameters.AddWithValue("@title",  session.Title);
            cmd.Parameters.AddWithValue("@notes",  session.Notes);
            cmd.Parameters.AddWithValue("@played", session.PlayedOn);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Session session)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE sessions
                                SET number = @num, title = @title, notes = @notes, played_on = @played
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",     session.Id);
            cmd.Parameters.AddWithValue("@num",    session.Number);
            cmd.Parameters.AddWithValue("@title",  session.Title);
            cmd.Parameters.AddWithValue("@notes",  session.Notes);
            cmd.Parameters.AddWithValue("@played", session.PlayedOn);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM sessions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Session Map(SqliteDataReader r) => new Session
        {
            Id         = r.GetInt32(0),
            CampaignId = r.GetInt32(1),
            Number     = r.GetInt32(2),
            Title      = r.GetString(3),
            Notes      = r.GetString(4),
            PlayedOn   = r.GetString(5),
        };
    }
}
