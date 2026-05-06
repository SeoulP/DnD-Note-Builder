using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eBackgroundRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eBackgroundRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_backgrounds (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id     INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name            TEXT    NOT NULL,
                description     TEXT    NOT NULL DEFAULT '',
                skill_1_id      INTEGER REFERENCES pathfinder_skill_types(id),
                skill_2_id      INTEGER REFERENCES pathfinder_skill_types(id),
                lore_skill_name TEXT    NOT NULL DEFAULT '',
                granted_feat_id INTEGER REFERENCES pathfinder_feats(id),
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eBackground> GetAll(int campaignId)
        {
            var list = new List<Pf2eBackground>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, skill_1_id, skill_2_id, lore_skill_name, granted_feat_id FROM pathfinder_backgrounds WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eBackground Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, skill_1_id, skill_2_id, lore_skill_name, granted_feat_id FROM pathfinder_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eBackground b)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_backgrounds (campaign_id, name, description, skill_1_id, skill_2_id, lore_skill_name, granted_feat_id)
                VALUES (@cid, @name, @desc, @sk1, @sk2, @lore, @feat);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  b.CampaignId);
            cmd.Parameters.AddWithValue("@name", b.Name);
            cmd.Parameters.AddWithValue("@desc", b.Description);
            cmd.Parameters.AddWithValue("@sk1",  b.Skill1Id.HasValue ? (object)b.Skill1Id.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sk2",  b.Skill2Id.HasValue ? (object)b.Skill2Id.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@lore", b.LoreSkillName);
            cmd.Parameters.AddWithValue("@feat", b.GrantedFeatId.HasValue ? (object)b.GrantedFeatId.Value : System.DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eBackground b)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_backgrounds
                SET name = @name, description = @desc, skill_1_id = @sk1, skill_2_id = @sk2, lore_skill_name = @lore, granted_feat_id = @feat
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", b.Name);
            cmd.Parameters.AddWithValue("@desc", b.Description);
            cmd.Parameters.AddWithValue("@sk1",  b.Skill1Id.HasValue ? (object)b.Skill1Id.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@sk2",  b.Skill2Id.HasValue ? (object)b.Skill2Id.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@lore", b.LoreSkillName);
            cmd.Parameters.AddWithValue("@feat", b.GrantedFeatId.HasValue ? (object)b.GrantedFeatId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@id",   b.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eBackground Map(SqliteDataReader r) => new Pf2eBackground
        {
            Id            = r.GetInt32(0),
            CampaignId    = r.GetInt32(1),
            Name          = r.GetString(2),
            Description   = r.GetString(3),
            Skill1Id      = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
            Skill2Id      = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
            LoreSkillName = r.GetString(6),
            GrantedFeatId = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),
        };
    }
}
