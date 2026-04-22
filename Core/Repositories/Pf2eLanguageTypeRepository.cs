using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eLanguageTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly string[] Defaults =
        {
            "Common", "Draconic", "Dwarven", "Elven", "Fey", "Gnomish", "Goblin", "Halfling",
            "Jotun", "Orcish", "Sakvroth", "Thalassic", "Shadowtongue", "Petran", "Pyric",
            "Sussuran", "Talican", "Tengu", "Diabolic", "Empyrean", "Protean", "Requian", "Utopian",
        };

        public Pf2eLanguageTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_language_types (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var name in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO pathfinder_language_types (campaign_id, name)
                    SELECT @cid, @name WHERE NOT EXISTS
                        (SELECT 1 FROM pathfinder_language_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eLanguageType> GetAll(int campaignId)
        {
            var list = new List<Pf2eLanguageType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name FROM pathfinder_language_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eLanguageType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name FROM pathfinder_language_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eLanguageType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_language_types (campaign_id, name) VALUES (@cid, @name);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CampaignId);
            cmd.Parameters.AddWithValue("@name", t.Name);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eLanguageType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_language_types SET name = @name WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@id",   t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_language_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eLanguageType Map(SqliteDataReader r) => new Pf2eLanguageType
        {
            Id         = r.GetInt32(0),
            CampaignId = r.GetInt32(1),
            Name       = r.GetString(2),
        };
    }
}
