using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAttackCategoryRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly string[] Defaults =
        {
            "Unarmed", "Simple", "Martial", "Advanced", "Spell",
        };

        public Pf2eAttackCategoryRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_attack_categories (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT    NOT NULL UNIQUE
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults()
        {
            foreach (var name in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_attack_categories (name) VALUES (@name)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eAttackCategory> GetAll()
        {
            var list = new List<Pf2eAttackCategory>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_attack_categories ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAttackCategory Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_attack_categories WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eAttackCategory Map(SqliteDataReader r) => new Pf2eAttackCategory
        {
            Id   = r.GetInt32(0),
            Name = r.GetString(1),
        };
    }
}
