using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureWeaknessRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureWeaknessRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_weaknesses (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id    INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                damage_type_id INTEGER NOT NULL REFERENCES pathfinder_damage_types(id),
                value          INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureWeakness> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureWeakness>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, damage_type_id, value FROM pathfinder_creature_weaknesses WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureWeakness w)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_weaknesses (creature_id, damage_type_id, value)
                VALUES (@cid, @dtid, @val);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  w.CreatureId);
            cmd.Parameters.AddWithValue("@dtid", w.DamageTypeId);
            cmd.Parameters.AddWithValue("@val",  w.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureWeakness w)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_weaknesses SET value = @val WHERE id = @id";
            cmd.Parameters.AddWithValue("@val", w.Value);
            cmd.Parameters.AddWithValue("@id",  w.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_weaknesses WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureWeakness Map(SqliteDataReader r) => new Pf2eCreatureWeakness
        {
            Id           = r.GetInt32(0),
            CreatureId   = r.GetInt32(1),
            DamageTypeId = r.GetInt32(2),
            Value        = r.GetInt32(3),
        };
    }
}
