using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class NpcRelationshipTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string Name, string Description)[] Defaults =
        {
            ("Hostile",    "Actively hostile; will attack or obstruct the party"),
            ("Unfriendly", "Distrustful or uncooperative"),
            ("Neutral",    "No strong feelings either way"),
            ("Friendly",   "Cooperative and well-disposed toward the party"),
            ("Allied",     "Actively supportive; will assist the party when possible"),
        };

        public NpcRelationshipTypeRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS npc_relationship_types (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, desc) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT INTO npc_relationship_types (campaign_id, name, description) VALUES (@cid, @name, @desc)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        public List<NpcRelationshipType> GetAll(int campaignId)
        {
            var list = new List<NpcRelationshipType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM npc_relationship_types WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new NpcRelationshipType
                {
                    Id          = reader.GetInt32(0),
                    CampaignId  = reader.GetInt32(1),
                    Name        = reader.GetString(2),
                    Description = reader.GetString(3),
                });
            return list;
        }

        public int Add(NpcRelationshipType type)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO npc_relationship_types (campaign_id, name, description) VALUES (@cid, @name, @desc); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  type.CampaignId);
            cmd.Parameters.AddWithValue("@name", type.Name);
            cmd.Parameters.AddWithValue("@desc", type.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM npc_relationship_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}