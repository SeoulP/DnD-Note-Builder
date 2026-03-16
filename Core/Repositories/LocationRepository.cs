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
                parent_location_id INTEGER REFERENCES locations(id) ON DELETE SET NULL
            )";
            cmd.ExecuteNonQuery();

            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS location_factions (
                location_id INTEGER NOT NULL REFERENCES locations(id) ON DELETE CASCADE,
                faction_id  INTEGER NOT NULL REFERENCES factions(id)  ON DELETE CASCADE,
                PRIMARY KEY (location_id, faction_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Location> GetAll(int campaignId)
        {
            var list = new List<Location>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, parent_location_id
                                FROM locations WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public List<Location> GetTopLevel(int campaignId)
        {
            var list = new List<Location>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, parent_location_id
                                FROM locations WHERE campaign_id = @cid AND parent_location_id IS NULL ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public List<Location> GetChildren(int parentLocationId)
        {
            var list = new List<Location>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, parent_location_id
                                FROM locations WHERE parent_location_id = @pid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@pid", parentLocationId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Location Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, parent_location_id
                                FROM locations WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var location = Map(reader);
            reader.Close();
            location.SubLocations = GetChildren(location.Id);
            location.FactionIds   = GetFactionIds(location.Id);
            return location;
        }

        public void AddFaction(int locationId, int factionId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO location_factions (location_id, faction_id) VALUES (@lid, @fid)";
            cmd.Parameters.AddWithValue("@lid", locationId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveFaction(int locationId, int factionId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM location_factions WHERE location_id = @lid AND faction_id = @fid";
            cmd.Parameters.AddWithValue("@lid", locationId);
            cmd.Parameters.AddWithValue("@fid", factionId);
            cmd.ExecuteNonQuery();
        }

        private List<int> GetFactionIds(int locationId)
        {
            var list = new List<int>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT faction_id FROM location_factions WHERE location_id = @lid";
            cmd.Parameters.AddWithValue("@lid", locationId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        public int Add(Location location)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO locations (campaign_id, name, type, description, notes, parent_location_id)
                                VALUES (@cid, @name, @type, @desc, @notes, @parent);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",    location.CampaignId);
            cmd.Parameters.AddWithValue("@name",   location.Name);
            cmd.Parameters.AddWithValue("@type",   location.Type);
            cmd.Parameters.AddWithValue("@desc",   location.Description);
            cmd.Parameters.AddWithValue("@notes",  location.Notes);
            cmd.Parameters.AddWithValue("@parent", location.ParentLocationId.HasValue ? location.ParentLocationId.Value : DBNull.Value);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Location location)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE locations
                                SET name = @name, type = @type, description = @desc,
                                    notes = @notes, parent_location_id = @parent
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",     location.Id);
            cmd.Parameters.AddWithValue("@name",   location.Name);
            cmd.Parameters.AddWithValue("@type",   location.Type);
            cmd.Parameters.AddWithValue("@desc",   location.Description);
            cmd.Parameters.AddWithValue("@notes",  location.Notes);
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
            ParentLocationId = r.IsDBNull(6) ? null : r.GetInt32(6),
        };
    }
}