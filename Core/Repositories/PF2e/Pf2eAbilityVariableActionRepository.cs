using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAbilityVariableActionRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAbilityVariableActionRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ability_variable_actions (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                ability_id      INTEGER NOT NULL REFERENCES pathfinder_creature_abilities(id) ON DELETE CASCADE,
                action_cost_id  INTEGER NOT NULL REFERENCES pathfinder_action_costs(id),
                effect_text     TEXT    NOT NULL DEFAULT '',
                dice_count      INTEGER,
                die_type_id     INTEGER REFERENCES pathfinder_die_types(id),
                bonus           INTEGER,
                damage_type_id  INTEGER REFERENCES pathfinder_damage_types(id),
                save_type_id    INTEGER REFERENCES pathfinder_save_types(id),
                save_dc         INTEGER,
                area_type_id    INTEGER REFERENCES pathfinder_area_types(id),
                area_size_feet  INTEGER,
                range_feet      INTEGER,
                UNIQUE(ability_id, action_cost_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAbilityVariableAction> GetForAbility(int abilityId)
        {
            var list = new List<Pf2eAbilityVariableAction>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, ability_id, action_cost_id, effect_text, dice_count, die_type_id,
                bonus, damage_type_id, save_type_id, save_dc, area_type_id, area_size_feet, range_feet
                FROM pathfinder_ability_variable_actions WHERE ability_id = @aid ORDER BY action_cost_id";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eAbilityVariableAction a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_ability_variable_actions
                (ability_id, action_cost_id, effect_text, dice_count, die_type_id, bonus, damage_type_id, save_type_id, save_dc, area_type_id, area_size_feet, range_feet)
                VALUES (@aid, @acid, @eff, @dc, @dtid, @bonus, @dmgid, @stid, @sdc, @arid, @arsz, @range);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",   a.AbilityId);
            cmd.Parameters.AddWithValue("@acid",  a.ActionCostId);
            cmd.Parameters.AddWithValue("@eff",   a.EffectText);
            cmd.Parameters.AddWithValue("@dc",    a.DiceCount.HasValue     ? (object)a.DiceCount.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dtid",  a.DieTypeId.HasValue     ? (object)a.DieTypeId.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@bonus", a.Bonus.HasValue         ? (object)a.Bonus.Value         : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dmgid", a.DamageTypeId.HasValue  ? (object)a.DamageTypeId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@stid",  a.SaveTypeId.HasValue    ? (object)a.SaveTypeId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   a.SaveDc.HasValue        ? (object)a.SaveDc.Value        : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arid",  a.AreaTypeId.HasValue    ? (object)a.AreaTypeId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  a.AreaSizeFeet.HasValue  ? (object)a.AreaSizeFeet.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", a.RangeFeet.HasValue     ? (object)a.RangeFeet.Value     : System.DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eAbilityVariableAction a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_ability_variable_actions SET
                effect_text = @eff, dice_count = @dc, die_type_id = @dtid, bonus = @bonus,
                damage_type_id = @dmgid, save_type_id = @stid, save_dc = @sdc,
                area_type_id = @arid, area_size_feet = @arsz, range_feet = @range
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@eff",   a.EffectText);
            cmd.Parameters.AddWithValue("@dc",    a.DiceCount.HasValue     ? (object)a.DiceCount.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dtid",  a.DieTypeId.HasValue     ? (object)a.DieTypeId.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@bonus", a.Bonus.HasValue         ? (object)a.Bonus.Value         : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dmgid", a.DamageTypeId.HasValue  ? (object)a.DamageTypeId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@stid",  a.SaveTypeId.HasValue    ? (object)a.SaveTypeId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   a.SaveDc.HasValue        ? (object)a.SaveDc.Value        : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arid",  a.AreaTypeId.HasValue    ? (object)a.AreaTypeId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  a.AreaSizeFeet.HasValue  ? (object)a.AreaSizeFeet.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", a.RangeFeet.HasValue     ? (object)a.RangeFeet.Value     : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@id",    a.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_ability_variable_actions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eAbilityVariableAction Map(SqliteDataReader r) => new Pf2eAbilityVariableAction
        {
            Id           = r.GetInt32(0),
            AbilityId    = r.GetInt32(1),
            ActionCostId = r.GetInt32(2),
            EffectText   = r.GetString(3),
            DiceCount    = r.IsDBNull(4)  ? (int?)null : r.GetInt32(4),
            DieTypeId    = r.IsDBNull(5)  ? (int?)null : r.GetInt32(5),
            Bonus        = r.IsDBNull(6)  ? (int?)null : r.GetInt32(6),
            DamageTypeId = r.IsDBNull(7)  ? (int?)null : r.GetInt32(7),
            SaveTypeId   = r.IsDBNull(8)  ? (int?)null : r.GetInt32(8),
            SaveDc       = r.IsDBNull(9)  ? (int?)null : r.GetInt32(9),
            AreaTypeId   = r.IsDBNull(10) ? (int?)null : r.GetInt32(10),
            AreaSizeFeet = r.IsDBNull(11) ? (int?)null : r.GetInt32(11),
            RangeFeet    = r.IsDBNull(12) ? (int?)null : r.GetInt32(12),
        };
    }
}
