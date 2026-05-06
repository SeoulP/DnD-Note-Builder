using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eAncestryRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eAncestryRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_ancestries (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                base_hp     INTEGER NOT NULL DEFAULT 8,
                size_id     INTEGER NOT NULL REFERENCES pathfinder_sizes(id),
                speed_feet  INTEGER NOT NULL DEFAULT 25,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eAncestry> GetAll(int campaignId)
        {
            var list = new List<Pf2eAncestry>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, base_hp, size_id, speed_feet, description FROM pathfinder_ancestries WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eAncestry Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, base_hp, size_id, speed_feet, description FROM pathfinder_ancestries WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eAncestry a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_ancestries (campaign_id, name, base_hp, size_id, speed_feet, description)
                VALUES (@cid, @name, @hp, @size, @speed, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   a.CampaignId);
            cmd.Parameters.AddWithValue("@name",  a.Name);
            cmd.Parameters.AddWithValue("@hp",    a.BaseHp);
            cmd.Parameters.AddWithValue("@size",  a.SizeId);
            cmd.Parameters.AddWithValue("@speed", a.SpeedFeet);
            cmd.Parameters.AddWithValue("@desc",  a.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eAncestry a)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_ancestries
                SET name = @name, base_hp = @hp, size_id = @size, speed_feet = @speed, description = @desc
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",  a.Name);
            cmd.Parameters.AddWithValue("@hp",    a.BaseHp);
            cmd.Parameters.AddWithValue("@size",  a.SizeId);
            cmd.Parameters.AddWithValue("@speed", a.SpeedFeet);
            cmd.Parameters.AddWithValue("@desc",  a.Description);
            cmd.Parameters.AddWithValue("@id",    a.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_ancestries WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eAncestry Map(SqliteDataReader r) => new Pf2eAncestry
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            BaseHp      = r.GetInt32(3),
            SizeId      = r.GetInt32(4),
            SpeedFeet   = r.GetInt32(5),
            Description = r.GetString(6),
        };
    }
}
