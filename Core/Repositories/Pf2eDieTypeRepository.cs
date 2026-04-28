using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eDieTypeRepository
    {
        private readonly SqliteConnection _conn;

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
