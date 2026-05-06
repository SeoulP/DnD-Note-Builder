using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eActionCostRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eActionCostRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_action_costs (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT    NOT NULL UNIQUE,
                sort_order INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eActionCost> GetAll()
        {
            var list = new List<Pf2eActionCost>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_action_costs ORDER BY sort_order";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eActionCost Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, sort_order FROM pathfinder_action_costs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eActionCost Map(SqliteDataReader r) => new Pf2eActionCost
        {
            Id        = r.GetInt32(0),
            Name      = r.GetString(1),
            SortOrder = r.GetInt32(2),
        };
    }
}
