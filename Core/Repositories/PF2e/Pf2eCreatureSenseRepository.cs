using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureSenseRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureSenseRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_senses (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id   INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                sense_type_id INTEGER NOT NULL REFERENCES pathfinder_sense_types(id),
                is_precise    INTEGER NOT NULL DEFAULT 1,
                range_feet    INTEGER,
                notes         TEXT    NOT NULL DEFAULT '',
                UNIQUE(creature_id, sense_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureSense> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureSense>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, sense_type_id, is_precise, range_feet, notes FROM pathfinder_creature_senses WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureSense s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_senses (creature_id, sense_type_id, is_precise, range_feet, notes)
                VALUES (@cid, @stid, @prec, @range, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   s.CreatureId);
            cmd.Parameters.AddWithValue("@stid",  s.SenseTypeId);
            cmd.Parameters.AddWithValue("@prec",  s.IsPrecise ? 1 : 0);
            cmd.Parameters.AddWithValue("@range", s.RangeFeet.HasValue ? (object)s.RangeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureSense s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_senses SET is_precise = @prec, range_feet = @range, notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@prec",  s.IsPrecise ? 1 : 0);
            cmd.Parameters.AddWithValue("@range", s.RangeFeet.HasValue ? (object)s.RangeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@id",    s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_senses WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureSense Map(SqliteDataReader r) => new Pf2eCreatureSense
        {
            Id          = r.GetInt32(0),
            CreatureId  = r.GetInt32(1),
            SenseTypeId = r.GetInt32(2),
            IsPrecise   = r.GetInt32(3) == 1,
            RangeFeet   = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
            Notes       = r.GetString(5),
        };
    }
}
