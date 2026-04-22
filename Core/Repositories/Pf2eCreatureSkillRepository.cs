using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureSkillRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureSkillRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_skills (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id   INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                skill_type_id INTEGER NOT NULL REFERENCES pathfinder_skill_types(id),
                modifier      INTEGER NOT NULL DEFAULT 0,
                notes         TEXT    NOT NULL DEFAULT '',
                UNIQUE(creature_id, skill_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureSkill> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureSkill>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, skill_type_id, modifier, notes FROM pathfinder_creature_skills WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureSkill s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_skills (creature_id, skill_type_id, modifier, notes)
                VALUES (@cid, @skid, @mod, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   s.CreatureId);
            cmd.Parameters.AddWithValue("@skid",  s.SkillTypeId);
            cmd.Parameters.AddWithValue("@mod",   s.Modifier);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreatureSkill s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_creature_skills SET modifier = @mod, notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@mod",   s.Modifier);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@id",    s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_skills WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureSkill Map(SqliteDataReader r) => new Pf2eCreatureSkill
        {
            Id          = r.GetInt32(0),
            CreatureId  = r.GetInt32(1),
            SkillTypeId = r.GetInt32(2),
            Modifier    = r.GetInt32(3),
            Notes       = r.GetString(4),
        };
    }
}
