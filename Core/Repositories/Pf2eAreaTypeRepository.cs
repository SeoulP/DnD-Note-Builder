using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAreaTypeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAreaTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_area_types (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT    NOT NULL UNIQUE,
                description TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAreaType> GetAll()
        {
            var list = new List<Pf2eAreaType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, description, sort_order FROM pathfinder_area_types ORDER BY sort_order";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAreaType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, description, sort_order FROM pathfinder_area_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eAreaType Map(SqliteDataReader r) => new Pf2eAreaType
        {
            Id          = r.GetInt32(0),
            Name        = r.GetString(1),
            Description = r.GetString(2),
            SortOrder   = r.GetInt32(3),
        };
    }
}
