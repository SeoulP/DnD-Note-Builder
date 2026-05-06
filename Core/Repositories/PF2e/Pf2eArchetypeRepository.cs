using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eArchetypeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eArchetypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_archetypes (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                class_id    INTEGER NOT NULL REFERENCES pathfinder_classes(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, class_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eArchetype> GetAll(int campaignId)
        {
            var list = new List<Pf2eArchetype>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, class_id, name, description FROM pathfinder_archetypes WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public List<Pf2eArchetype> GetForClass(int classId)
        {
            var list = new List<Pf2eArchetype>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, class_id, name, description FROM pathfinder_archetypes WHERE class_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eArchetype Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, class_id, name, description FROM pathfinder_archetypes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eArchetype a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_archetypes (campaign_id, class_id, name, description)
                VALUES (@cid, @clid, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  a.CampaignId);
            cmd.Parameters.AddWithValue("@clid", a.ClassId);
            cmd.Parameters.AddWithValue("@name", a.Name);
            cmd.Parameters.AddWithValue("@desc", a.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eArchetype a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_archetypes SET name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", a.Name);
            cmd.Parameters.AddWithValue("@desc", a.Description);
            cmd.Parameters.AddWithValue("@id",   a.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_archetypes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eArchetype Map(SqliteDataReader r) => new Pf2eArchetype
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            ClassId     = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
        };
    }
}
