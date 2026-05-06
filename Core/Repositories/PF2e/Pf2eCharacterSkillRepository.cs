using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterSkillRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterSkillRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_skills (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id        INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                skill_type_id       INTEGER NOT NULL REFERENCES pathfinder_skill_types(id),
                proficiency_rank_id INTEGER NOT NULL REFERENCES pathfinder_proficiency_ranks(id),
                UNIQUE(character_id, skill_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterSkill> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterSkill>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, skill_type_id, proficiency_rank_id FROM pathfinder_character_skills WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCharacterSkill Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, skill_type_id, proficiency_rank_id FROM pathfinder_character_skills WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCharacterSkill s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_skills (character_id, skill_type_id, proficiency_rank_id)
                VALUES (@cid, @skid, @prid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  s.CharacterId);
            cmd.Parameters.AddWithValue("@skid", s.SkillTypeId);
            cmd.Parameters.AddWithValue("@prid", s.ProficiencyRankId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterSkill s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_character_skills SET proficiency_rank_id = @prid WHERE id = @id";
            cmd.Parameters.AddWithValue("@prid", s.ProficiencyRankId);
            cmd.Parameters.AddWithValue("@id",   s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_skills WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterSkill Map(SqliteDataReader r) => new Pf2eCharacterSkill
        {
            Id                = r.GetInt32(0),
            CharacterId       = r.GetInt32(1),
            SkillTypeId       = r.GetInt32(2),
            ProficiencyRankId = r.GetInt32(3),
        };
    }
}
