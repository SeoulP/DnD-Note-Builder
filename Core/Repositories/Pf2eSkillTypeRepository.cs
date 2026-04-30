using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eSkillTypeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eSkillTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_skill_types (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id      INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name             TEXT    NOT NULL,
                ability_score_id INTEGER NOT NULL REFERENCES pathfinder_ability_scores(id),
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eSkillType> GetAll(int campaignId)
        {
            var list = new List<Pf2eSkillType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, ability_score_id FROM pathfinder_skill_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eSkillType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, ability_score_id FROM pathfinder_skill_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eSkillType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_skill_types (campaign_id, name, ability_score_id) VALUES (@cid, @name, @attr);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CampaignId);
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@attr", t.AbilityScoreId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eSkillType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_skill_types SET name = @name, ability_score_id = @attr WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@attr", t.AbilityScoreId);
            cmd.Parameters.AddWithValue("@id",   t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_skill_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eSkillType Map(SqliteDataReader r) => new Pf2eSkillType
        {
            Id             = r.GetInt32(0),
            CampaignId     = r.GetInt32(1),
            Name           = r.GetString(2),
            AbilityScoreId = r.GetInt32(3),
        };
    }
}
