using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterStrikeDamageRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterStrikeDamageRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_strike_damage (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                strike_id      INTEGER NOT NULL REFERENCES pathfinder_character_strikes(id) ON DELETE CASCADE,
                damage_type_id INTEGER NOT NULL REFERENCES pathfinder_damage_types(id),
                dice_count     INTEGER NOT NULL DEFAULT 1,
                die_type_id    INTEGER NOT NULL REFERENCES pathfinder_die_types(id),
                bonus          INTEGER NOT NULL DEFAULT 0,
                is_primary     INTEGER NOT NULL DEFAULT 1,
                notes          TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterStrikeDamage> GetForStrike(int strikeId)
        {
            var list = new List<Pf2eCharacterStrikeDamage>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, strike_id, damage_type_id, dice_count, die_type_id, bonus, is_primary, notes FROM pathfinder_character_strike_damage WHERE strike_id = @sid";
            cmd.Parameters.AddWithValue("@sid", strikeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCharacterStrikeDamage d)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_strike_damage (strike_id, damage_type_id, dice_count, die_type_id, bonus, is_primary, notes)
                VALUES (@sid, @dtid, @dc, @dtypid, @bonus, @prim, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@sid",    d.StrikeId);
            cmd.Parameters.AddWithValue("@dtid",   d.DamageTypeId);
            cmd.Parameters.AddWithValue("@dc",     d.DiceCount);
            cmd.Parameters.AddWithValue("@dtypid", d.DieTypeId);
            cmd.Parameters.AddWithValue("@bonus",  d.Bonus);
            cmd.Parameters.AddWithValue("@prim",   d.IsPrimary ? 1 : 0);
            cmd.Parameters.AddWithValue("@notes",  d.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterStrikeDamage d)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_character_strike_damage
                SET damage_type_id = @dtid, dice_count = @dc, die_type_id = @dtypid, bonus = @bonus, is_primary = @prim, notes = @notes
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@dtid",   d.DamageTypeId);
            cmd.Parameters.AddWithValue("@dc",     d.DiceCount);
            cmd.Parameters.AddWithValue("@dtypid", d.DieTypeId);
            cmd.Parameters.AddWithValue("@bonus",  d.Bonus);
            cmd.Parameters.AddWithValue("@prim",   d.IsPrimary ? 1 : 0);
            cmd.Parameters.AddWithValue("@notes",  d.Notes);
            cmd.Parameters.AddWithValue("@id",     d.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_strike_damage WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterStrikeDamage Map(SqliteDataReader r) => new Pf2eCharacterStrikeDamage
        {
            Id           = r.GetInt32(0),
            StrikeId     = r.GetInt32(1),
            DamageTypeId = r.GetInt32(2),
            DiceCount    = r.GetInt32(3),
            DieTypeId    = r.GetInt32(4),
            Bonus        = r.GetInt32(5),
            IsPrimary    = r.GetInt32(6) == 1,
            Notes        = r.GetString(7),
        };
    }
}
