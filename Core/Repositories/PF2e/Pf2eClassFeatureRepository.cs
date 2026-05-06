using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eClassFeatureRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eClassFeatureRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_class_features (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                class_id    INTEGER NOT NULL REFERENCES pathfinder_classes(id) ON DELETE CASCADE,
                level       INTEGER NOT NULL DEFAULT 1,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eClassFeature> GetForClass(int classId)
        {
            var list = new List<Pf2eClassFeature>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, class_id, level, name, description FROM pathfinder_class_features WHERE class_id = @cid ORDER BY level";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eClassFeature Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, class_id, level, name, description FROM pathfinder_class_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eClassFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_class_features (class_id, level, name, description)
                VALUES (@cid, @level, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   f.ClassId);
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eClassFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_class_features SET level = @level, name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            cmd.Parameters.AddWithValue("@id",    f.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_class_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eClassFeature Map(SqliteDataReader r) => new Pf2eClassFeature
        {
            Id          = r.GetInt32(0),
            ClassId     = r.GetInt32(1),
            Level       = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
        };
    }
}
