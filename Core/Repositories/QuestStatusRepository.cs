using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class QuestStatusRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string Name, string Description)[] Defaults =
        {
            ("Active",    "Currently being pursued"),
            ("Completed", "Successfully resolved"),
            ("Failed",    "Abandoned or ended in failure"),
            ("On Hold",   "Paused — not currently being pursued"),
        };

        public QuestStatusRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS quest_statuses (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, desc) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO quest_statuses (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM quest_statuses WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        public List<QuestStatus> GetAll(int campaignId)
        {
            var list = new List<QuestStatus>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM quest_statuses WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new QuestStatus
                {
                    Id          = reader.GetInt32(0),
                    CampaignId  = reader.GetInt32(1),
                    Name        = reader.GetString(2),
                    Description = reader.GetString(3),
                });
            return list;
        }

        public int Add(QuestStatus status)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO quest_statuses (campaign_id, name, description) VALUES (@cid, @name, @desc); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  status.CampaignId);
            cmd.Parameters.AddWithValue("@name", status.Name);
            cmd.Parameters.AddWithValue("@desc", status.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM quest_statuses WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
