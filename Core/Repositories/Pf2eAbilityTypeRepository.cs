using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAbilityTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly string[] Defaults =
        {
            "Strike", "Active", "Reactive", "Passive", "Innate Spell", "Aura",
        };

        public Pf2eAbilityTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ability_types (
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
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_ability_types (name) VALUES (@name)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eAbilityType> GetAll()
        {
            var list = new List<Pf2eAbilityType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_ability_types ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAbilityType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_ability_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eAbilityType Map(SqliteDataReader r) => new Pf2eAbilityType
        {
            Id   = r.GetInt32(0),
            Name = r.GetString(1),
        };
    }
}
