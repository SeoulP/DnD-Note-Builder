using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureImmunityRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureImmunityRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_immunities (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id       INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                damage_type_id    INTEGER REFERENCES pathfinder_damage_types(id),
                condition_type_id INTEGER REFERENCES pathfinder_condition_types(id),
                notes             TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureImmunity> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureImmunity>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, damage_type_id, condition_type_id, notes FROM pathfinder_creature_immunities WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureImmunity i)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_immunities (creature_id, damage_type_id, condition_type_id, notes)
                VALUES (@cid, @dtid, @ctid, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   i.CreatureId);
            cmd.Parameters.AddWithValue("@dtid",  i.DamageTypeId.HasValue    ? (object)i.DamageTypeId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ctid",  i.ConditionTypeId.HasValue ? (object)i.ConditionTypeId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", i.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureImmunity i)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_immunities SET notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@notes", i.Notes);
            cmd.Parameters.AddWithValue("@id",    i.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_immunities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureImmunity Map(SqliteDataReader r) => new Pf2eCreatureImmunity
        {
            Id              = r.GetInt32(0),
            CreatureId      = r.GetInt32(1),
            DamageTypeId    = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            ConditionTypeId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
            Notes           = r.GetString(4),
        };
    }
}
