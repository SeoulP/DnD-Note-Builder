using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureResistanceRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureResistanceRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_resistances (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id      INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                damage_type_id   INTEGER NOT NULL REFERENCES pathfinder_damage_types(id),
                value            INTEGER NOT NULL DEFAULT 0,
                exception_note   TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureResistance> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureResistance>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, damage_type_id, value, exception_note FROM pathfinder_creature_resistances WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureResistance r)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_resistances (creature_id, damage_type_id, value, exception_note)
                VALUES (@cid, @dtid, @val, @note);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  r.CreatureId);
            cmd.Parameters.AddWithValue("@dtid", r.DamageTypeId);
            cmd.Parameters.AddWithValue("@val",  r.Value);
            cmd.Parameters.AddWithValue("@note", r.ExceptionNote);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureResistance r)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_resistances SET value = @val, exception_note = @note WHERE id = @id";
            cmd.Parameters.AddWithValue("@val",  r.Value);
            cmd.Parameters.AddWithValue("@note", r.ExceptionNote);
            cmd.Parameters.AddWithValue("@id",   r.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_resistances WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureResistance Map(SqliteDataReader r) => new Pf2eCreatureResistance
        {
            Id            = r.GetInt32(0),
            CreatureId    = r.GetInt32(1),
            DamageTypeId  = r.GetInt32(2),
            Value         = r.GetInt32(3),
            ExceptionNote = r.GetString(4),
        };
    }
}
