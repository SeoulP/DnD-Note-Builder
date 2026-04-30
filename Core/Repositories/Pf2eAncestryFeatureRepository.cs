using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAncestryFeatureRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAncestryFeatureRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ancestry_features (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                ancestry_id INTEGER NOT NULL REFERENCES pathfinder_ancestries(id) ON DELETE CASCADE,
                level       INTEGER NOT NULL DEFAULT 1,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAncestryFeature> GetForAncestry(int ancestryId)
        {
            var list = new List<Pf2eAncestryFeature>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ancestry_id, level, name, description FROM pathfinder_ancestry_features WHERE ancestry_id = @aid ORDER BY level";
            cmd.Parameters.AddWithValue("@aid", ancestryId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAncestryFeature Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ancestry_id, level, name, description FROM pathfinder_ancestry_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eAncestryFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_ancestry_features (ancestry_id, level, name, description)
                VALUES (@aid, @level, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",   f.AncestryId);
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eAncestryFeature f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_ancestry_features SET level = @level, name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@level", f.Level);
            cmd.Parameters.AddWithValue("@name",  f.Name);
            cmd.Parameters.AddWithValue("@desc",  f.Description);
            cmd.Parameters.AddWithValue("@id",    f.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_ancestry_features WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eAncestryFeature Map(SqliteDataReader r) => new Pf2eAncestryFeature
        {
            Id          = r.GetInt32(0),
            AncestryId  = r.GetInt32(1),
            Level       = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
        };
    }
}
