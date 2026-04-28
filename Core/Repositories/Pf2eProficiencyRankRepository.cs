using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eProficiencyRankRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eProficiencyRankRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_proficiency_ranks (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT    NOT NULL UNIQUE,
                rank_value INTEGER NOT NULL UNIQUE
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eProficiencyRank> GetAll()
        {
            var list = new List<Pf2eProficiencyRank>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, rank_value FROM pathfinder_proficiency_ranks ORDER BY rank_value";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eProficiencyRank Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, rank_value FROM pathfinder_proficiency_ranks WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eProficiencyRank Map(SqliteDataReader r) => new Pf2eProficiencyRank
        {
            Id        = r.GetInt32(0),
            Name      = r.GetString(1),
            RankValue = r.GetInt32(2),
        };
    }
}
