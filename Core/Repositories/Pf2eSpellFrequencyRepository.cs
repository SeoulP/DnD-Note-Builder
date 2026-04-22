using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eSpellFrequencyRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, int sortOrder)[] Defaults =
        {
            ("At Will",  0),
            ("Constant", 1),
            ("1/Round",  2),
            ("3/Day",    3),
            ("2/Day",    4),
            ("1/Day",    5),
            ("1/Hour",   6),
            ("1/Week",   7),
        };

        public Pf2eSpellFrequencyRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_spell_frequencies (
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
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_spell_frequencies (name, sort_order) VALUES (@name, @sort)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@sort", sortOrder);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eSpellFrequency> GetAll()
        {
            var list = new List<Pf2eSpellFrequency>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_spell_frequencies ORDER BY sort_order";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eSpellFrequency Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_spell_frequencies WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eSpellFrequency Map(SqliteDataReader r) => new Pf2eSpellFrequency
        {
            Id        = r.GetInt32(0),
            Name      = r.GetString(1),
            SortOrder = r.GetInt32(2),
        };
    }
}
