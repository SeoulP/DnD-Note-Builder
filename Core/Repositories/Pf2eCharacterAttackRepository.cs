using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterAttackRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterAttackRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_attacks (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id        INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                attack_category_id  INTEGER NOT NULL REFERENCES pathfinder_attack_categories(id),
                proficiency_rank_id INTEGER NOT NULL REFERENCES pathfinder_proficiency_ranks(id),
                UNIQUE(character_id, attack_category_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterAttack> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterAttack>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, attack_category_id, proficiency_rank_id FROM pathfinder_character_attacks WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCharacterAttack Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, attack_category_id, proficiency_rank_id FROM pathfinder_character_attacks WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCharacterAttack a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_attacks (character_id, attack_category_id, proficiency_rank_id)
                VALUES (@cid, @acid, @prid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  a.CharacterId);
            cmd.Parameters.AddWithValue("@acid", a.AttackCategoryId);
            cmd.Parameters.AddWithValue("@prid", a.ProficiencyRankId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterAttack a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_character_attacks SET proficiency_rank_id = @prid WHERE id = @id";
            cmd.Parameters.AddWithValue("@prid", a.ProficiencyRankId);
            cmd.Parameters.AddWithValue("@id",   a.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_attacks WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterAttack Map(SqliteDataReader r) => new Pf2eCharacterAttack
        {
            Id                = r.GetInt32(0),
            CharacterId       = r.GetInt32(1),
            AttackCategoryId  = r.GetInt32(2),
            ProficiencyRankId = r.GetInt32(3),
        };
    }
}
