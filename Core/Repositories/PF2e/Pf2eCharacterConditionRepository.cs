using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterConditionRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterConditionRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_conditions (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id      INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                condition_type_id INTEGER NOT NULL REFERENCES pathfinder_condition_types(id),
                condition_value   INTEGER NOT NULL DEFAULT 0,
                UNIQUE(character_id, condition_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterCondition> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterCondition>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, condition_type_id, condition_value FROM pathfinder_character_conditions WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCharacterCondition Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, condition_type_id, condition_value FROM pathfinder_character_conditions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCharacterCondition c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_conditions (character_id, condition_type_id, condition_value)
                VALUES (@cid, @ctid, @val);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  c.CharacterId);
            cmd.Parameters.AddWithValue("@ctid", c.ConditionTypeId);
            cmd.Parameters.AddWithValue("@val",  c.ConditionValue);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCharacterCondition c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_character_conditions SET condition_value = @val WHERE id = @id";
            cmd.Parameters.AddWithValue("@val", c.ConditionValue);
            cmd.Parameters.AddWithValue("@id",  c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_conditions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterCondition Map(SqliteDataReader r) => new Pf2eCharacterCondition
        {
            Id              = r.GetInt32(0),
            CharacterId     = r.GetInt32(1),
            ConditionTypeId = r.GetInt32(2),
            ConditionValue  = r.GetInt32(3),
        };
    }
}
