using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAbilityTraitRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAbilityTraitRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ability_traits (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                ability_id    INTEGER NOT NULL REFERENCES pathfinder_creature_abilities(id) ON DELETE CASCADE,
                trait_type_id INTEGER NOT NULL REFERENCES pathfinder_trait_types(id),
                UNIQUE(ability_id, trait_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAbilityTrait> GetForAbility(int abilityId)
        {
            var list = new List<Pf2eAbilityTrait>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ability_id, trait_type_id FROM pathfinder_ability_traits WHERE ability_id = @aid";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eAbilityTrait t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_ability_traits (ability_id, trait_type_id)
                VALUES (@aid, @ttid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",  t.AbilityId);
            cmd.Parameters.AddWithValue("@ttid", t.TraitTypeId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_ability_traits WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eAbilityTrait Map(SqliteDataReader r) => new Pf2eAbilityTrait
        {
            Id          = r.GetInt32(0),
            AbilityId   = r.GetInt32(1),
            TraitTypeId = r.GetInt32(2),
        };
    }
}
