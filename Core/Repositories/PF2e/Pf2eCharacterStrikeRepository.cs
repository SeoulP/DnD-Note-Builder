using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterStrikeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterStrikeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_strikes (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id   INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                name           TEXT    NOT NULL,
                is_melee       INTEGER NOT NULL DEFAULT 1,
                attack_bonus   INTEGER NOT NULL DEFAULT 0,
                attack_bonus_2 INTEGER NOT NULL DEFAULT 0,
                attack_bonus_3 INTEGER NOT NULL DEFAULT 0,
                area_type_id   INTEGER REFERENCES pathfinder_area_types(id),
                area_size_feet INTEGER,
                range_feet     INTEGER,
                notes          TEXT    NOT NULL DEFAULT '',
                sort_order     INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterStrike> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterStrike>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, character_id, name, is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                area_type_id, area_size_feet, range_feet, notes, sort_order
                FROM pathfinder_character_strikes WHERE character_id = @cid ORDER BY sort_order";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCharacterStrike Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, character_id, name, is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                area_type_id, area_size_feet, range_feet, notes, sort_order
                FROM pathfinder_character_strikes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCharacterStrike s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_strikes
                (character_id, name, is_melee, attack_bonus, attack_bonus_2, attack_bonus_3,
                 area_type_id, area_size_feet, range_feet, notes, sort_order)
                VALUES (@cid, @name, @melee, @ab, @ab2, @ab3, @arid, @arsz, @range, @notes, @sort);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   s.CharacterId);
            cmd.Parameters.AddWithValue("@name",  s.Name);
            cmd.Parameters.AddWithValue("@melee", s.IsMelee ? 1 : 0);
            cmd.Parameters.AddWithValue("@ab",    s.AttackBonus);
            cmd.Parameters.AddWithValue("@ab2",   s.AttackBonus2);
            cmd.Parameters.AddWithValue("@ab3",   s.AttackBonus3);
            cmd.Parameters.AddWithValue("@arid",  s.AreaTypeId.HasValue   ? (object)s.AreaTypeId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  s.AreaSizeFeet.HasValue ? (object)s.AreaSizeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", s.RangeFeet.HasValue    ? (object)s.RangeFeet.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@sort",  s.SortOrder);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterStrike s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_character_strikes SET
                name = @name, is_melee = @melee, attack_bonus = @ab, attack_bonus_2 = @ab2, attack_bonus_3 = @ab3,
                area_type_id = @arid, area_size_feet = @arsz, range_feet = @range, notes = @notes, sort_order = @sort
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",  s.Name);
            cmd.Parameters.AddWithValue("@melee", s.IsMelee ? 1 : 0);
            cmd.Parameters.AddWithValue("@ab",    s.AttackBonus);
            cmd.Parameters.AddWithValue("@ab2",   s.AttackBonus2);
            cmd.Parameters.AddWithValue("@ab3",   s.AttackBonus3);
            cmd.Parameters.AddWithValue("@arid",  s.AreaTypeId.HasValue   ? (object)s.AreaTypeId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arsz",  s.AreaSizeFeet.HasValue ? (object)s.AreaSizeFeet.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@range", s.RangeFeet.HasValue    ? (object)s.RangeFeet.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@sort",  s.SortOrder);
            cmd.Parameters.AddWithValue("@id",    s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_strikes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterStrike Map(SqliteDataReader r) => new Pf2eCharacterStrike
        {
            Id           = r.GetInt32(0),
            CharacterId  = r.GetInt32(1),
            Name         = r.GetString(2),
            IsMelee      = r.GetInt32(3) == 1,
            AttackBonus  = r.GetInt32(4),
            AttackBonus2 = r.GetInt32(5),
            AttackBonus3 = r.GetInt32(6),
            AreaTypeId   = r.IsDBNull(7)  ? (int?)null : r.GetInt32(7),
            AreaSizeFeet = r.IsDBNull(8)  ? (int?)null : r.GetInt32(8),
            RangeFeet    = r.IsDBNull(9)  ? (int?)null : r.GetInt32(9),
            Notes        = r.GetString(10),
            SortOrder    = r.GetInt32(11),
        };
    }
}
