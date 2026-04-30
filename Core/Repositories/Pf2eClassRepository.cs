using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eClassRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eClassRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_classes (
                id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id          INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name                 TEXT    NOT NULL,
                key_ability_score_id INTEGER NOT NULL REFERENCES pathfinder_ability_scores(id),
                hp_per_level         INTEGER NOT NULL DEFAULT 8,
                description          TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eClass> GetAll(int campaignId)
        {
            var list = new List<Pf2eClass>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, key_ability_score_id, hp_per_level, description FROM pathfinder_classes WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eClass Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, key_ability_score_id, hp_per_level, description FROM pathfinder_classes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eClass c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_classes (campaign_id, name, key_ability_score_id, hp_per_level, description)
                VALUES (@cid, @name, @key, @hp, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  c.CampaignId);
            cmd.Parameters.AddWithValue("@name", c.Name);
            cmd.Parameters.AddWithValue("@key",  c.KeyAbilityScoreId);
            cmd.Parameters.AddWithValue("@hp",   c.HpPerLevel);
            cmd.Parameters.AddWithValue("@desc", c.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eClass c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_classes
                SET name = @name, key_ability_score_id = @key, hp_per_level = @hp, description = @desc
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", c.Name);
            cmd.Parameters.AddWithValue("@key",  c.KeyAbilityScoreId);
            cmd.Parameters.AddWithValue("@hp",   c.HpPerLevel);
            cmd.Parameters.AddWithValue("@desc", c.Description);
            cmd.Parameters.AddWithValue("@id",   c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_classes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eClass Map(SqliteDataReader r) => new Pf2eClass
        {
            Id                = r.GetInt32(0),
            CampaignId        = r.GetInt32(1),
            Name              = r.GetString(2),
            KeyAbilityScoreId = r.GetInt32(3),
            HpPerLevel        = r.GetInt32(4),
            Description       = r.GetString(5),
        };
    }
}
