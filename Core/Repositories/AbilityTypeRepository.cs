using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class AbilityTypeRepository
    {
        private readonly SqliteConnection _conn;

        public AbilityTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_types (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                inactive    INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<AbilityType> GetAll(int campaignId)
        {
            var list = new List<AbilityType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name FROM ability_types
                                WHERE campaign_id = @cid AND inactive = 0
                                ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new AbilityType { Id = reader.GetInt32(0), CampaignId = reader.GetInt32(1), Name = reader.GetString(2) });
            return list;
        }

        public int Add(AbilityType type)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_types (campaign_id, name) VALUES (@cid, @name);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  type.CampaignId);
            cmd.Parameters.AddWithValue("@name", type.Name);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_types SET inactive = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
