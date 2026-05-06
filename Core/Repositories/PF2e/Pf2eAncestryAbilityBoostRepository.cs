using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAncestryAbilityBoostRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAncestryAbilityBoostRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ancestry_ability_boosts (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                ancestry_id      INTEGER NOT NULL REFERENCES pathfinder_ancestries(id) ON DELETE CASCADE,
                ability_score_id INTEGER NOT NULL REFERENCES pathfinder_ability_scores(id),
                is_flaw          INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAncestryAbilityBoost> GetForAncestry(int ancestryId)
        {
            var list = new List<Pf2eAncestryAbilityBoost>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ancestry_id, ability_score_id, is_flaw FROM pathfinder_ancestry_ability_boosts WHERE ancestry_id = @aid";
            cmd.Parameters.AddWithValue("@aid", ancestryId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eAncestryAbilityBoost b)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_ancestry_ability_boosts (ancestry_id, ability_score_id, is_flaw)
                VALUES (@aid, @attr, @flaw);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",  b.AncestryId);
            cmd.Parameters.AddWithValue("@attr", b.AbilityScoreId);
            cmd.Parameters.AddWithValue("@flaw", b.IsFlaw ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_ancestry_ability_boosts WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eAncestryAbilityBoost Map(SqliteDataReader r) => new Pf2eAncestryAbilityBoost
        {
            Id             = r.GetInt32(0),
            AncestryId     = r.GetInt32(1),
            AbilityScoreId = r.GetInt32(2),
            IsFlaw         = r.GetInt32(3) == 1,
        };
    }
}
