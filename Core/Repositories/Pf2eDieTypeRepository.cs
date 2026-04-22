using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eDieTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, int sides)[] Defaults =
        {
            ("d4",   4),
            ("d6",   6),
            ("d8",   8),
            ("d10",  10),
            ("d12",  12),
            ("d20",  20),
            ("d100", 100),
        };

        public Pf2eDieTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_die_types (
                id    INTEGER PRIMARY KEY AUTOINCREMENT,
                name  TEXT    NOT NULL UNIQUE,
                sides INTEGER NOT NULL UNIQUE
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults()
        {
            foreach (var (name, sides) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_die_types (name, sides) VALUES (@name, @sides)";
                cmd.Parameters.AddWithValue("@name",  name);
                cmd.Parameters.AddWithValue("@sides", sides);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eDieType> GetAll()
        {
            var list = new List<Pf2eDieType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sides FROM pathfinder_die_types ORDER BY sides";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eDieType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sides FROM pathfinder_die_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eDieType Map(SqliteDataReader r) => new Pf2eDieType
        {
            Id    = r.GetInt32(0),
            Name  = r.GetString(1),
            Sides = r.GetInt32(2),
        };
    }
}
