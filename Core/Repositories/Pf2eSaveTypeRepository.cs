using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eSaveTypeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eSaveTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_save_types (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT    NOT NULL UNIQUE
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eSaveType> GetAll()
        {
            var list = new List<Pf2eSaveType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_save_types ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eSaveType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, name FROM pathfinder_save_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static Pf2eSaveType Map(SqliteDataReader r) => new Pf2eSaveType
        {
            Id   = r.GetInt32(0),
            Name = r.GetString(1),
        };
    }
}
