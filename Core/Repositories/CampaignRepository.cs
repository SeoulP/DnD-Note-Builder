using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class CampaignRepository
    {
        private readonly SqliteConnection _conn;

        public CampaignRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS campaigns (
                id           INTEGER PRIMARY KEY,
                name         TEXT    NOT NULL,
                system       TEXT    NOT NULL DEFAULT 'dnd5e_2024',
                description  TEXT,
                date_started TEXT,
                created_at   TEXT    NOT NULL DEFAULT (datetime('now')),
                archived     INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Campaign> GetAll()
        {
            var list = new List<Campaign>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, system, description, date_started FROM campaigns WHERE archived = 0 ORDER BY created_at DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public Campaign Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, system, description, date_started FROM campaigns WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Campaign campaign)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO campaigns (name, system, description, date_started)
                                VALUES (@name, @system, @desc, @date);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name",   campaign.Name);
            cmd.Parameters.AddWithValue("@system", campaign.System);
            cmd.Parameters.AddWithValue("@desc",   campaign.Description);
            cmd.Parameters.AddWithValue("@date",   campaign.DateStarted);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Campaign campaign)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE campaigns
                                SET name = @name, system = @system, description = @desc, date_started = @date
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",     campaign.Id);
            cmd.Parameters.AddWithValue("@name",   campaign.Name);
            cmd.Parameters.AddWithValue("@system", campaign.System);
            cmd.Parameters.AddWithValue("@desc",   campaign.Description);
            cmd.Parameters.AddWithValue("@date",   campaign.DateStarted);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM campaigns WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Campaign Map(SqliteDataReader r) => new Campaign
        {
            Id          = r.GetInt32(0),
            Name        = r.GetString(1),
            System      = r.GetString(2),
            Description = r.IsDBNull(3) ? "" : r.GetString(3),
            DateStarted = r.IsDBNull(4) ? "" : r.GetString(4),
        };
    }
}