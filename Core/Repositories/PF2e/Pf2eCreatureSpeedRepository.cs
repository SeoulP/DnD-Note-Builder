using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureSpeedRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureSpeedRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_speeds (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id      INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                movement_type_id INTEGER NOT NULL REFERENCES pathfinder_movement_types(id),
                speed_feet       INTEGER NOT NULL DEFAULT 25,
                notes            TEXT    NOT NULL DEFAULT '',
                UNIQUE(creature_id, movement_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureSpeed> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureSpeed>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, movement_type_id, speed_feet, notes FROM pathfinder_creature_speeds WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureSpeed s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_speeds (creature_id, movement_type_id, speed_feet, notes)
                VALUES (@cid, @mtid, @feet, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   s.CreatureId);
            cmd.Parameters.AddWithValue("@mtid",  s.MovementTypeId);
            cmd.Parameters.AddWithValue("@feet",  s.SpeedFeet);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureSpeed s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_speeds SET speed_feet = @feet, notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@feet",  s.SpeedFeet);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@id",    s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_speeds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureSpeed Map(SqliteDataReader r) => new Pf2eCreatureSpeed
        {
            Id             = r.GetInt32(0),
            CreatureId     = r.GetInt32(1),
            MovementTypeId = r.GetInt32(2),
            SpeedFeet      = r.GetInt32(3),
            Notes          = r.GetString(4),
        };
    }
}
