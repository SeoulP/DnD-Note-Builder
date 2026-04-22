using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eHeritageRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eHeritageRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_heritages (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                ancestry_id INTEGER NOT NULL REFERENCES pathfinder_ancestries(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, ancestry_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eHeritage> GetAll(int campaignId)
        {
            var list = new List<Pf2eHeritage>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, ancestry_id, name, description FROM pathfinder_heritages WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public List<Pf2eHeritage> GetForAncestry(int ancestryId)
        {
            var list = new List<Pf2eHeritage>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, ancestry_id, name, description FROM pathfinder_heritages WHERE ancestry_id = @aid ORDER BY name";
            cmd.Parameters.AddWithValue("@aid", ancestryId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eHeritage Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, ancestry_id, name, description FROM pathfinder_heritages WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eHeritage h)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_heritages (campaign_id, ancestry_id, name, description)
                VALUES (@cid, @aid, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  h.CampaignId);
            cmd.Parameters.AddWithValue("@aid",  h.AncestryId);
            cmd.Parameters.AddWithValue("@name", h.Name);
            cmd.Parameters.AddWithValue("@desc", h.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eHeritage h)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_heritages SET name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", h.Name);
            cmd.Parameters.AddWithValue("@desc", h.Description);
            cmd.Parameters.AddWithValue("@id",   h.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_heritages WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eHeritage Map(SqliteDataReader r) => new Pf2eHeritage
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            AncestryId  = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
        };
    }
}
