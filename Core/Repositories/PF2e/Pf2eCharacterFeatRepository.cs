using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterFeatRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterFeatRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_character_feats (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                feat_id      INTEGER NOT NULL REFERENCES pathfinder_feats(id),
                level_taken  INTEGER NOT NULL DEFAULT 1,
                UNIQUE(character_id, feat_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCharacterFeat> GetForCharacter(int characterId)
        {
            var list = new List<Pf2eCharacterFeat>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, feat_id, level_taken FROM pathfinder_character_feats WHERE character_id = @cid ORDER BY level_taken";
            cmd.Parameters.AddWithValue("@cid", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCharacterFeat Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, character_id, feat_id, level_taken FROM pathfinder_character_feats WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCharacterFeat f)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_character_feats (character_id, feat_id, level_taken)
                VALUES (@cid, @fid, @lvl);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid", f.CharacterId);
            cmd.Parameters.AddWithValue("@fid", f.FeatId);
            cmd.Parameters.AddWithValue("@lvl", f.LevelTaken);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_character_feats WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacterFeat Map(SqliteDataReader r) => new Pf2eCharacterFeat
        {
            Id          = r.GetInt32(0),
            CharacterId = r.GetInt32(1),
            FeatId      = r.GetInt32(2),
            LevelTaken  = r.GetInt32(3),
        };
    }
}
