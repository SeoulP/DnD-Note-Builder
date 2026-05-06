using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eArchetypeFeatureRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eArchetypeFeatureRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_archetype_features (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                archetype_id INTEGER NOT NULL REFERENCES pathfinder_archetypes(id) ON DELETE CASCADE,
                level        INTEGER NOT NULL DEFAULT 1,
                name         TEXT    NOT NULL,
                description  TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eArchetypeFeature> GetForArchetype(int archetypeId)
        {
            var list = new List<Pf2eArchetypeFeature>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, archetype_id, level, name, description FROM pathfinder_archetype_features WHERE archetype_id = @aid ORDER BY level";
            cmd.Parameters.AddWithValue("@aid", archetypeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eArchetypeFeature Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, archetype_id, level, name, description FROM pathfinder_archetype_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eArchetypeFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_archetype_features (archetype_id, level, name, description)
                VALUES (@aid, @level, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",   f.ArchetypeId);
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eArchetypeFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_archetype_features SET level = @level, name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            cmd.Parameters.AddWithValue("@id",    f.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_archetype_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eArchetypeFeature Map(SqliteDataReader r) => new Pf2eArchetypeFeature
        {
            Id          = r.GetInt32(0),
            ArchetypeId = r.GetInt32(1),
            Level       = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
        };
    }
}
