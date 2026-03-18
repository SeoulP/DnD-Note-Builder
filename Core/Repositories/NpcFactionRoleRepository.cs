using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class NpcFactionRoleRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string Name, string Description)[] Defaults =
        {
            ("Allied",      "Actively supports and cooperates with the faction"),
            ("Enemy",       "Opposed to the faction; in open or hidden conflict"),
            ("Neutral",     "Neither allied nor hostile; indifferent to the faction"),
            ("Unaware Of",  "Does not know this faction exists"),
            ("Member",      "A rank-and-file member of the faction"),
            ("Leader",      "Leads or commands the faction or a division of it"),
            ("Agent",       "Covert operative acting on behalf of the faction"),
            ("Informant",   "Secretly feeds information to the faction"),
            ("Defector",    "Formerly a member; now estranged or turned against them"),
            ("Captive",     "Held by the faction against their will"),
            ("Sympathiser", "Not a member but broadly shares the faction's goals"),
            ("Rival",       "Competes with the faction without outright hostility"),
        };

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

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, desc) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO npc_faction_roles (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM npc_faction_roles WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
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
