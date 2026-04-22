using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eSizeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, int sortOrder)[] Defaults =
        {
            ("Tiny",       1),
            ("Small",      2),
            ("Medium",     3),
            ("Large",      4),
            ("Huge",       5),
            ("Gargantuan", 6),
        };

        public Pf2eSizeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_sizes (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT    NOT NULL UNIQUE,
                sort_order INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults()
        {
            foreach (var (name, sortOrder) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_sizes (name, sort_order) VALUES (@name, @sort)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@sort", sortOrder);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eSize> GetAll()
        {
            var list = new List<Pf2eSize>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_sizes ORDER BY sort_order";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eSize Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_sizes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eSize Map(SqliteDataReader r) => new Pf2eSize
        {
            Id        = r.GetInt32(0),
            Name      = r.GetString(1),
            SortOrder = r.GetInt32(2),
        };
    }
}
