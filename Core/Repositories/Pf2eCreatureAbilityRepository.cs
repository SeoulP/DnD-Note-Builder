using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureAbilityRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureAbilityRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_abilities (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id     INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                ability_type_id INTEGER NOT NULL REFERENCES pathfinder_ability_types(id),
                action_cost_id  INTEGER NOT NULL REFERENCES pathfinder_action_costs(id),
                name            TEXT    NOT NULL,
                trigger         TEXT    NOT NULL DEFAULT '',
                is_melee        INTEGER,
                attack_bonus    INTEGER,
                attack_bonus_2  INTEGER,
                attack_bonus_3  INTEGER,
                area_type_id    INTEGER REFERENCES pathfinder_area_types(id),
                area_size_feet  INTEGER,
                range_feet      INTEGER,
                tradition_id    INTEGER REFERENCES pathfinder_traditions(id),
                spell_dc        INTEGER,
                spell_attack    INTEGER,
                effect_text     TEXT    NOT NULL DEFAULT '',
                sort_order      INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureAbility> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureAbility>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, creature_id, ability_type_id, action_cost_id, name, trigger,
                is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                area_type_id, area_size_feet, range_feet, tradition_id, spell_dc, spell_attack,
                effect_text, sort_order
                FROM pathfinder_creature_abilities WHERE creature_id = @cid ORDER BY sort_order";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCreatureAbility Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, creature_id, ability_type_id, action_cost_id, name, trigger,
                is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                area_type_id, area_size_feet, range_feet, tradition_id, spell_dc, spell_attack,
                effect_text, sort_order
                FROM pathfinder_creature_abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCreatureAbility a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_abilities
                (creature_id, ability_type_id, action_cost_id, name, trigger,
                 is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                 area_type_id, area_size_feet, range_feet, tradition_id, spell_dc, spell_attack,
                 effect_text, sort_order)
                VALUES (@cid, @atid, @acid, @name, @trig,
                        @melee, @ab, @ab2, @ab3,
                        @arid, @arsz, @range, @trad, @sdc, @satk,
                        @eff, @sort);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  a.CreatureId);
            cmd.Parameters.AddWithValue("@atid", a.AbilityTypeId);
            cmd.Parameters.AddWithValue("@acid", a.ActionCostId);
            cmd.Parameters.AddWithValue("@name", a.Name);
            cmd.Parameters.AddWithValue("@trig", a.Trigger);
            cmd.Parameters.AddWithValue("@melee", a.IsMelee.HasValue      ? (object)a.IsMelee.Value      : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab",    a.AttackBonus.HasValue  ? (object)a.AttackBonus.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab2",   a.AttackBonus2.HasValue ? (object)a.AttackBonus2.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab3",   a.AttackBonus3.HasValue ? (object)a.AttackBonus3.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arid",  a.AreaTypeId.HasValue   ? (object)a.AreaTypeId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  a.AreaSizeFeet.HasValue ? (object)a.AreaSizeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", a.RangeFeet.HasValue    ? (object)a.RangeFeet.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@trad",  a.TraditionId.HasValue  ? (object)a.TraditionId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   a.SpellDc.HasValue      ? (object)a.SpellDc.Value      : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@satk",  a.SpellAttack.HasValue  ? (object)a.SpellAttack.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@eff",   a.EffectText);
            cmd.Parameters.AddWithValue("@sort",  a.SortOrder);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureAbility a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_creature_abilities SET
                ability_type_id = @atid, action_cost_id = @acid, name = @name, trigger = @trig,
                is_melee = @melee, attack_bonus = @ab, attack_bonus_2 = @ab2, attack_bonus_3 = @ab3,
                area_type_id = @arid, area_size_feet = @arsz, range_feet = @range,
                tradition_id = @trad, spell_dc = @sdc, spell_attack = @satk,
                effect_text = @eff, sort_order = @sort
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@atid", a.AbilityTypeId);
            cmd.Parameters.AddWithValue("@acid", a.ActionCostId);
            cmd.Parameters.AddWithValue("@name", a.Name);
            cmd.Parameters.AddWithValue("@trig", a.Trigger);
            cmd.Parameters.AddWithValue("@melee", a.IsMelee.HasValue      ? (object)a.IsMelee.Value      : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab",    a.AttackBonus.HasValue  ? (object)a.AttackBonus.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab2",   a.AttackBonus2.HasValue ? (object)a.AttackBonus2.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@ab3",   a.AttackBonus3.HasValue ? (object)a.AttackBonus3.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arid",  a.AreaTypeId.HasValue   ? (object)a.AreaTypeId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  a.AreaSizeFeet.HasValue ? (object)a.AreaSizeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", a.RangeFeet.HasValue    ? (object)a.RangeFeet.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@trad",  a.TraditionId.HasValue  ? (object)a.TraditionId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sdc",   a.SpellDc.HasValue      ? (object)a.SpellDc.Value      : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@satk",  a.SpellAttack.HasValue  ? (object)a.SpellAttack.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@eff",   a.EffectText);
            cmd.Parameters.AddWithValue("@sort",  a.SortOrder);
            cmd.Parameters.AddWithValue("@id",    a.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_abilities WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureAbility Map(SqliteDataReader r) => new Pf2eCreatureAbility
        {
            Id             = r.GetInt32(0),
            CreatureId     = r.GetInt32(1),
            AbilityTypeId  = r.GetInt32(2),
            ActionCostId   = r.GetInt32(3),
            Name           = r.GetString(4),
            Trigger        = r.GetString(5),
            IsMelee        = r.IsDBNull(6)  ? (int?)null : r.GetInt32(6),
            AttackBonus    = r.IsDBNull(7)  ? (int?)null : r.GetInt32(7),
            AttackBonus2   = r.IsDBNull(8)  ? (int?)null : r.GetInt32(8),
            AttackBonus3   = r.IsDBNull(9)  ? (int?)null : r.GetInt32(9),
            AreaTypeId     = r.IsDBNull(10) ? (int?)null : r.GetInt32(10),
            AreaSizeFeet   = r.IsDBNull(11) ? (int?)null : r.GetInt32(11),
            RangeFeet      = r.IsDBNull(12) ? (int?)null : r.GetInt32(12),
            TraditionId    = r.IsDBNull(13) ? (int?)null : r.GetInt32(13),
            SpellDc        = r.IsDBNull(14) ? (int?)null : r.GetInt32(14),
            SpellAttack    = r.IsDBNull(15) ? (int?)null : r.GetInt32(15),
            EffectText     = r.GetString(16),
            SortOrder      = r.GetInt32(17),
        };
    }
}
