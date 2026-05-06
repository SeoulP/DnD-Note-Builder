using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureTraitRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureTraitRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_traits (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id   INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                trait_type_id INTEGER NOT NULL REFERENCES pathfinder_trait_types(id),
                UNIQUE(creature_id, trait_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureTrait> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureTrait>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, trait_type_id FROM pathfinder_creature_traits WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureTrait t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_traits (creature_id, trait_type_id)
                VALUES (@cid, @ttid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CreatureId);
            cmd.Parameters.AddWithValue("@ttid", t.TraitTypeId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public Dictionary<int, List<int>> GetTraitTypeIdsByCreature(int campaignId)
        {
            var map = new Dictionary<int, List<int>>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT t.creature_id, t.trait_type_id
                                FROM pathfinder_creature_traits t
                                JOIN pathfinder_creatures c ON c.id = t.creature_id
                                WHERE c.campaign_id = @cid";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int cid = r.GetInt32(0), tid = r.GetInt32(1);
                if (!map.ContainsKey(cid)) map[cid] = new List<int>();
                map[cid].Add(tid);
            }
            return map;
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_traits WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureTrait Map(SqliteDataReader r) => new Pf2eCreatureTrait
        {
            Id          = r.GetInt32(0),
            CreatureId  = r.GetInt32(1),
            TraitTypeId = r.GetInt32(2),
        };
    }
}
