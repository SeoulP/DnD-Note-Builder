using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterTraitRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterTraitRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_traits (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id  INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                trait_type_id INTEGER NOT NULL REFERENCES pathfinder_trait_types(id),
                UNIQUE(character_id, trait_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterTrait> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterTrait>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, trait_type_id FROM pathfinder_character_traits WHERE character_id = @cid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCharacterTrait t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_traits (character_id, trait_type_id)
                VALUES (@cid, @ttid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CharacterId);
            cmd.Parameters.AddWithValue("@ttid", t.TraitTypeId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_traits WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterTrait Map(SqliteDataReader r) => new Pf2eCharacterTrait
        {
            Id          = r.GetInt32(0),
            CharacterId = r.GetInt32(1),
            TraitTypeId = r.GetInt32(2),
        };
    }
}
