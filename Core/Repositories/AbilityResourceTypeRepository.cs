using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class AbilityResourceTypeRepository
    {
        private readonly SqliteConnection _conn;

        public AbilityResourceTypeRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ability_resource_types (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                inactive    INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            AddColumnIfMissing("inactive",             "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("resource_kind",        "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("recovery_type",        "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("tracks_value_text",    "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("recovery_amount",      "TEXT    NOT NULL DEFAULT 'All'");
            AddColumnIfMissing("recovery_fixed_count", "INTEGER NOT NULL DEFAULT 0");
        }

        private void AddColumnIfMissing(string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('ability_resource_types') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE ability_resource_types ADD COLUMN {column} {definition}";
                alter.ExecuteNonQuery();
            }
        }

        public List<AbilityResourceType> GetAll(int campaignId)
        {
            var list = new List<AbilityResourceType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, description, resource_kind, recovery_type, tracks_value_text, recovery_amount, recovery_fixed_count
                                FROM ability_resource_types
                                WHERE campaign_id = @cid AND inactive = 0
                                ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(AbilityResourceType type)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ability_resource_types (campaign_id, name, description, resource_kind, recovery_type, tracks_value_text, recovery_amount, recovery_fixed_count)
                                VALUES (@cid, @name, @desc, @kind, @recovery, @tracks, @recoveryAmount, @recoveryFixed);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",           type.CampaignId);
            cmd.Parameters.AddWithValue("@name",          type.Name);
            cmd.Parameters.AddWithValue("@desc",          type.Description);
            cmd.Parameters.AddWithValue("@kind",          type.ResourceKind);
            cmd.Parameters.AddWithValue("@recovery",      type.RecoveryType);
            cmd.Parameters.AddWithValue("@tracks",        type.TracksValueText ? 1 : 0);
            cmd.Parameters.AddWithValue("@recoveryAmount", type.RecoveryAmount);
            cmd.Parameters.AddWithValue("@recoveryFixed",  type.RecoveryFixedCount);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(AbilityResourceType type)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE ability_resource_types
                                SET name = @name, description = @desc, resource_kind = @kind,
                                    recovery_type = @recovery, tracks_value_text = @tracks,
                                    recovery_amount = @recoveryAmount, recovery_fixed_count = @recoveryFixed
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",            type.Id);
            cmd.Parameters.AddWithValue("@name",          type.Name);
            cmd.Parameters.AddWithValue("@desc",          type.Description);
            cmd.Parameters.AddWithValue("@kind",          type.ResourceKind);
            cmd.Parameters.AddWithValue("@recovery",      type.RecoveryType);
            cmd.Parameters.AddWithValue("@tracks",        type.TracksValueText ? 1 : 0);
            cmd.Parameters.AddWithValue("@recoveryAmount", type.RecoveryAmount);
            cmd.Parameters.AddWithValue("@recoveryFixed",  type.RecoveryFixedCount);
            cmd.ExecuteNonQuery();
        }

        private static AbilityResourceType Map(System.Data.Common.DbDataReader r) => new AbilityResourceType
        {
            Id                 = r.GetInt32(0),
            CampaignId         = r.GetInt32(1),
            Name               = r.GetString(2),
            Description        = r.GetString(3),
            ResourceKind       = r.IsDBNull(4) ? "" : r.GetString(4),
            RecoveryType       = r.IsDBNull(5) ? "" : r.GetString(5),
            TracksValueText    = !r.IsDBNull(6) && r.GetInt32(6) != 0,
            RecoveryAmount     = r.IsDBNull(7) ? "All" : r.GetString(7),
            RecoveryFixedCount = r.IsDBNull(8) ? 0 : r.GetInt32(8),
        };

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE ability_resource_types SET inactive = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int? GetIdByName(int campaignId, string name)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM ability_resource_types WHERE campaign_id = @cid AND name = @name AND inactive = 0 LIMIT 1";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            cmd.Parameters.AddWithValue("@name", name);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (int)(long)result;
        }
    }
}
