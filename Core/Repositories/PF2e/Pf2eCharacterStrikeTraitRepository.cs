using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterStrikeTraitRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterStrikeTraitRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_strike_traits (
                id                       INTEGER PRIMARY KEY AUTOINCREMENT,
                strike_id                INTEGER NOT NULL REFERENCES pathfinder_character_strikes(id) ON DELETE CASCADE,
                trait_type_id            INTEGER NOT NULL REFERENCES pathfinder_trait_types(id),
                trait_parameter_die_id   INTEGER REFERENCES pathfinder_die_types(id),
                trait_parameter_damage_id INTEGER REFERENCES pathfinder_damage_types(id),
                UNIQUE(strike_id, trait_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterStrikeTrait> GetForStrike(int strikeId)
        {
            var list = new List<Pf2eCharacterStrikeTrait>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, strike_id, trait_type_id, trait_parameter_die_id, trait_parameter_damage_id FROM pathfinder_character_strike_traits WHERE strike_id = @sid";
            cmd.Parameters.AddWithValue("@sid", strikeId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCharacterStrikeTrait t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_strike_traits (strike_id, trait_type_id, trait_parameter_die_id, trait_parameter_damage_id)
                VALUES (@sid, @ttid, @dieid, @dmgid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@sid",   t.StrikeId);
            cmd.Parameters.AddWithValue("@ttid",  t.TraitTypeId);
            cmd.Parameters.AddWithValue("@dieid", t.TraitParameterDieId.HasValue    ? (object)t.TraitParameterDieId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dmgid", t.TraitParameterDamageId.HasValue ? (object)t.TraitParameterDamageId.Value : System.DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterStrikeTrait t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_character_strike_traits SET trait_parameter_die_id = @dieid, trait_parameter_damage_id = @dmgid WHERE id = @id";
            cmd.Parameters.AddWithValue("@dieid", t.TraitParameterDieId.HasValue    ? (object)t.TraitParameterDieId.Value    : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@dmgid", t.TraitParameterDamageId.HasValue ? (object)t.TraitParameterDamageId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@id",    t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_strike_traits WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterStrikeTrait Map(SqliteDataReader r) => new Pf2eCharacterStrikeTrait
        {
            Id                     = r.GetInt32(0),
            StrikeId               = r.GetInt32(1),
            TraitTypeId            = r.GetInt32(2),
            TraitParameterDieId    = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
            TraitParameterDamageId = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
        };
    }
}
