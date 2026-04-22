using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eSenseTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, int isPrecise)[] Defaults =
        {
            ("Darkvision",       1),
            ("Low-Light Vision", 1),
            ("Scent",            0),
            ("Tremorsense",      0),
            ("Wavesense",        0),
            ("Echolocation",     1),
            ("Lifesense",        0),
            ("Thoughtsense",     0),
            ("Spiritsense",      0),
        };

        public Pf2eSenseTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_sense_types (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                is_precise  INTEGER NOT NULL DEFAULT 1,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, isPrecise) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO pathfinder_sense_types (campaign_id, name, is_precise, description)
                    SELECT @cid, @name, @prec, '' WHERE NOT EXISTS
                        (SELECT 1 FROM pathfinder_sense_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@prec", isPrecise);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eSenseType> GetAll(int campaignId)
        {
            var list = new List<Pf2eSenseType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, is_precise, description FROM pathfinder_sense_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eSenseType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, is_precise, description FROM pathfinder_sense_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eSenseType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_sense_types (campaign_id, name, is_precise, description)
                VALUES (@cid, @name, @prec, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CampaignId);
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@prec", t.IsPrecise ? 1 : 0);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eSenseType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_sense_types SET name = @name, is_precise = @prec, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@prec", t.IsPrecise ? 1 : 0);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            cmd.Parameters.AddWithValue("@id",   t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_sense_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eSenseType Map(SqliteDataReader r) => new Pf2eSenseType
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            IsPrecise   = r.GetInt32(3) == 1,
            Description = r.GetString(4),
        };
    }
}
