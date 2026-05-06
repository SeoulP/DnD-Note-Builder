using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class NpcFactionRoleRepository
    {
        private readonly SqliteConnection _conn;

        public NpcFactionRoleRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS npc_faction_roles (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                inactive    INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            var hasInactive = _conn.CreateCommand();
            hasInactive.CommandText = "SELECT COUNT(*) FROM pragma_table_info('npc_faction_roles') WHERE name = 'inactive'";
            if ((long)hasInactive.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE npc_faction_roles ADD COLUMN inactive INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }

        public List<NpcFactionRole> GetAll(int campaignId)
        {
            var list = new List<NpcFactionRole>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM npc_faction_roles WHERE campaign_id = @cid AND inactive = 0 ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new NpcFactionRole
                {
                    Id          = reader.GetInt32(0),
                    CampaignId  = reader.GetInt32(1),
                    Name        = reader.GetString(2),
                    Description = reader.GetString(3),
                });
            return list;
        }

        public int Add(NpcFactionRole role)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO npc_faction_roles (campaign_id, name, description) VALUES (@cid, @name, @desc); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  role.CampaignId);
            cmd.Parameters.AddWithValue("@name", role.Name);
            cmd.Parameters.AddWithValue("@desc", role.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE npc_faction_roles SET inactive = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
