using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class EntityAliasRepository
    {
        private readonly SqliteConnection _conn;

        public EntityAliasRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS entity_aliases (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                entity_type TEXT    NOT NULL,
                entity_id   INTEGER NOT NULL,
                alias       TEXT    NOT NULL,
                UNIQUE (campaign_id, alias)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<EntityAlias> GetAll(int campaignId)
        {
            var list = new List<EntityAlias>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, entity_type, entity_id, alias
                                FROM entity_aliases WHERE campaign_id = @cid ORDER BY alias ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EntityAlias> GetForEntity(string entityType, int entityId)
        {
            var list = new List<EntityAlias>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, entity_type, entity_id, alias
                                FROM entity_aliases WHERE entity_type = @type AND entity_id = @eid ORDER BY alias ASC";
            cmd.Parameters.AddWithValue("@type", entityType);
            cmd.Parameters.AddWithValue("@eid",  entityId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        /// <summary>
        /// Inserts the alias if unique within the campaign.
        /// Returns the id of the row (new or existing on conflict).
        /// Returns 0 if the alias text is blank.
        /// </summary>
        public int Add(EntityAlias alias)
        {
            if (string.IsNullOrWhiteSpace(alias.Alias)) return 0;
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO entity_aliases (campaign_id, entity_type, entity_id, alias)
                                VALUES (@cid, @type, @eid, @alias);
                                SELECT id FROM entity_aliases WHERE campaign_id = @cid AND alias = @alias";
            cmd.Parameters.AddWithValue("@cid",   alias.CampaignId);
            cmd.Parameters.AddWithValue("@type",  alias.EntityType);
            cmd.Parameters.AddWithValue("@eid",   alias.EntityId);
            cmd.Parameters.AddWithValue("@alias", alias.Alias.Trim());
            var result = cmd.ExecuteScalar();
            return result is long id ? (int)id : 0;
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM entity_aliases WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static EntityAlias Map(SqliteDataReader r) => new()
        {
            Id         = r.GetInt32(0),
            CampaignId = r.GetInt32(1),
            EntityType = r.GetString(2),
            EntityId   = r.GetInt32(3),
            Alias      = r.GetString(4),
        };
    }
}
