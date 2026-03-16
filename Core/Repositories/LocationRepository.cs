using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class LocationRepository
    {
        private readonly SqliteConnection _conn;

        public LocationRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS locations (
                id                 INTEGER PRIMARY KEY,
                campaign_id        INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name               TEXT    NOT NULL,
                type               TEXT    NOT NULL DEFAULT '',
                description        TEXT    NOT NULL DEFAULT '',
                notes              TEXT    NOT NULL DEFAULT '',
                map_ref            TEXT    NOT NULL DEFAULT '',
                parent_location_id INTEGER REFERENCES locations(id) ON DELETE SET NULL
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Location> GetAll(int campaignId)
        {
            var list = new List<Location>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, map_ref, parent_location_id
                                FROM locations WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Location Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, map_ref, parent_location_id
                                FROM locations WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Location location)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO locations (campaign_id, name, type, description, notes, map_ref, parent_location_id)
                                VALUES (@cid, @name, @type, @desc, @notes, @mapref, @parent);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",    location.CampaignId);
            cmd.Parameters.AddWithValue("@name",   location.Name);
            cmd.Parameters.AddWithValue("@type",   location.Type);
            cmd.Parameters.AddWithValue("@desc",   location.Description);
            cmd.Parameters.AddWithValue("@notes",  location.Notes);
            cmd.Parameters.AddWithValue("@mapref", location.MapRef);
            cmd.Parameters.AddWithValue("@parent", location.ParentLocationId.HasValue ? location.ParentLocationId.Value : DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Location location)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE locations
                                SET name = @name, type = @type, description = @desc,
                                    notes = @notes, map_ref = @mapref, parent_location_id = @parent
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",     location.Id);
            cmd.Parameters.AddWithValue("@name",   location.Name);
            cmd.Parameters.AddWithValue("@type",   location.Type);
            cmd.Parameters.AddWithValue("@desc",   location.Description);
            cmd.Parameters.AddWithValue("@notes",  location.Notes);
            cmd.Parameters.AddWithValue("@mapref", location.MapRef);
            cmd.Parameters.AddWithValue("@parent", location.ParentLocationId.HasValue ? location.ParentLocationId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM locations WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Location Map(SqliteDataReader r) => new Location
        {
            Id               = r.GetInt32(0),
            CampaignId       = r.GetInt32(1),
            Name             = r.GetString(2),
            Type             = r.GetString(3),
            Description      = r.GetString(4),
            Notes            = r.GetString(5),
            MapRef           = r.GetString(6),
            ParentLocationId = r.IsDBNull(7) ? null : r.GetInt32(7),
        };
    }
}
