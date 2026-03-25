using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eCharacterSkillRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eCharacterSkillRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_character_skills (
                id                  INTEGER PRIMARY KEY,
                player_character_id INTEGER NOT NULL REFERENCES player_characters(id) ON DELETE CASCADE,
                skill_id            INTEGER NOT NULL REFERENCES dnd5e_skills(id) ON DELETE CASCADE,
                source              TEXT    NOT NULL DEFAULT 'custom',
                source_id           INTEGER,
                is_expertise        INTEGER NOT NULL DEFAULT 0,
                UNIQUE (player_character_id, skill_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<DnD5eCharacterSkill> GetForCharacter(int playerCharacterId)
        {
            var list = new List<DnD5eCharacterSkill>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, player_character_id, skill_id, source, source_id, is_expertise FROM dnd5e_character_skills WHERE player_character_id = @pcid";
            cmd.Parameters.AddWithValue("@pcid", playerCharacterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public void Upsert(DnD5eCharacterSkill skill)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO dnd5e_character_skills (player_character_id, skill_id, source, source_id, is_expertise)
                VALUES (@pcid, @sid, @source, @sourceId, @expertise)
                ON CONFLICT(player_character_id, skill_id) DO UPDATE SET
                    source       = excluded.source,
                    source_id    = excluded.source_id,
                    is_expertise = excluded.is_expertise";
            cmd.Parameters.AddWithValue("@pcid",     skill.PlayerCharacterId);
            cmd.Parameters.AddWithValue("@sid",      skill.SkillId);
            cmd.Parameters.AddWithValue("@source",   skill.Source);
            cmd.Parameters.AddWithValue("@sourceId", skill.SourceId.HasValue ? (object)skill.SourceId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@expertise", skill.IsExpertise ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int playerCharacterId, int skillId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM dnd5e_character_skills WHERE player_character_id = @pcid AND skill_id = @sid";
            cmd.Parameters.AddWithValue("@pcid", playerCharacterId);
            cmd.Parameters.AddWithValue("@sid",  skillId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllForCharacter(int playerCharacterId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM dnd5e_character_skills WHERE player_character_id = @pcid";
            cmd.Parameters.AddWithValue("@pcid", playerCharacterId);
            cmd.ExecuteNonQuery();
        }

        private static DnD5eCharacterSkill Map(SqliteDataReader r) => new DnD5eCharacterSkill
        {
            Id                = r.GetInt32(0),
            PlayerCharacterId = r.GetInt32(1),
            SkillId           = r.GetInt32(2),
            Source            = r.GetString(3),
            SourceId          = r.IsDBNull(4) ? null : (int?)r.GetInt32(4),
            IsExpertise       = r.GetInt32(5) == 1,
        };
    }
}
