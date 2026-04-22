using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAbilityScoreRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, string abbreviation)[] Defaults =
        {
            ("Strength",     "STR"),
            ("Dexterity",    "DEX"),
            ("Constitution", "CON"),
            ("Intelligence", "INT"),
            ("Wisdom",       "WIS"),
            ("Charisma",     "CHA"),
        };

        public Pf2eAbilityScoreRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ability_scores (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                name         TEXT    NOT NULL UNIQUE,
                abbreviation TEXT    NOT NULL UNIQUE
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults()
        {
            foreach (var (name, abbreviation) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO pathfinder_ability_scores (name, abbreviation) VALUES (@name, @abbr)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@abbr", abbreviation);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eAbilityScore> GetAll()
        {
            var list = new List<Pf2eAbilityScore>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, abbreviation FROM pathfinder_ability_scores ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAbilityScore Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, abbreviation FROM pathfinder_ability_scores WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eAbilityScore Map(SqliteDataReader r) => new Pf2eAbilityScore
        {
            Id           = r.GetInt32(0),
            Name         = r.GetString(1),
            Abbreviation = r.GetString(2),
        };
    }
}
