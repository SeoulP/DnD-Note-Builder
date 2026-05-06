using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eStrikeConditionRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eStrikeConditionRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_strike_conditions (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                ability_id        INTEGER NOT NULL REFERENCES pathfinder_creature_abilities(id) ON DELETE CASCADE,
                condition_type_id INTEGER NOT NULL REFERENCES pathfinder_condition_types(id),
                condition_value   INTEGER NOT NULL DEFAULT 0,
                is_on_crit_only   INTEGER NOT NULL DEFAULT 0,
                save_type_id      INTEGER REFERENCES pathfinder_save_types(id),
                save_dc           INTEGER,
                notes             TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eStrikeCondition> GetForAbility(int abilityId)
        {
            var list = new List<Pf2eStrikeCondition>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ability_id, condition_type_id, condition_value, is_on_crit_only, save_type_id, save_dc, notes FROM pathfinder_strike_conditions WHERE ability_id = @aid";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eStrikeCondition c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_strike_conditions (ability_id, condition_type_id, condition_value, is_on_crit_only, save_type_id, save_dc, notes)
                VALUES (@aid, @ctid, @cval, @crit, @stid, @sdc, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",   c.AbilityId);
            cmd.Parameters.AddWithValue("@ctid",  c.ConditionTypeId);
            cmd.Parameters.AddWithValue("@cval",  c.ConditionValue);
            cmd.Parameters.AddWithValue("@crit",  c.IsOnCritOnly ? 1 : 0);
            cmd.Parameters.AddWithValue("@stid",  c.SaveTypeId.HasValue ? (object)c.SaveTypeId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   c.SaveDc.HasValue     ? (object)c.SaveDc.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", c.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eStrikeCondition c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_strike_conditions
                SET condition_type_id = @ctid, condition_value = @cval, is_on_crit_only = @crit, save_type_id = @stid, save_dc = @sdc, notes = @notes
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@ctid",  c.ConditionTypeId);
            cmd.Parameters.AddWithValue("@cval",  c.ConditionValue);
            cmd.Parameters.AddWithValue("@crit",  c.IsOnCritOnly ? 1 : 0);
            cmd.Parameters.AddWithValue("@stid",  c.SaveTypeId.HasValue ? (object)c.SaveTypeId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   c.SaveDc.HasValue     ? (object)c.SaveDc.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", c.Notes);
            cmd.Parameters.AddWithValue("@id",    c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_strike_conditions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eStrikeCondition Map(SqliteDataReader r) => new Pf2eStrikeCondition
        {
            Id              = r.GetInt32(0),
            AbilityId       = r.GetInt32(1),
            ConditionTypeId = r.GetInt32(2),
            ConditionValue  = r.GetInt32(3),
            IsOnCritOnly    = r.GetInt32(4) == 1,
            SaveTypeId      = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
            SaveDc          = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
            Notes           = r.GetString(7),
        };
    }
}
