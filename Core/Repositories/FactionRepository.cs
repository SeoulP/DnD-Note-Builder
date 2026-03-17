using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class FactionRepository
    {
        private readonly SqliteConnection _conn;

        public FactionRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS factions (
                id           INTEGER PRIMARY KEY,
                campaign_id  INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name         TEXT    NOT NULL,
                type         TEXT    NOT NULL DEFAULT '',
                description  TEXT    NOT NULL DEFAULT '',
                notes        TEXT    NOT NULL DEFAULT '',
                goals        TEXT    NOT NULL DEFAULT '',
                reputation   INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Faction> GetAll(int campaignId)
        {
            var list = new List<Faction>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, goals, reputation
                                FROM factions WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Faction Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, type, description, notes, goals, reputation
                                FROM factions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Faction faction)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO factions (campaign_id, name, type, description, notes, goals, reputation)
                                VALUES (@cid, @name, @type, @desc, @notes, @goals, @rep);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",   faction.CampaignId);
            cmd.Parameters.AddWithValue("@name",  faction.Name);
            cmd.Parameters.AddWithValue("@type",  faction.Type);
            cmd.Parameters.AddWithValue("@desc",  faction.Description);
            cmd.Parameters.AddWithValue("@notes", faction.Notes);
            cmd.Parameters.AddWithValue("@goals", faction.Goals);
            cmd.Parameters.AddWithValue("@rep",   faction.Reputation);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Faction faction)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE factions
                                SET name = @name, type = @type, description = @desc,
                                    notes = @notes, goals = @goals, reputation = @rep
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",    faction.Id);
            cmd.Parameters.AddWithValue("@name",  faction.Name);
            cmd.Parameters.AddWithValue("@type",  faction.Type);
            cmd.Parameters.AddWithValue("@desc",  faction.Description);
            cmd.Parameters.AddWithValue("@notes", faction.Notes);
            cmd.Parameters.AddWithValue("@goals", faction.Goals);
            cmd.Parameters.AddWithValue("@rep",   faction.Reputation);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM factions WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Faction Map(SqliteDataReader r) => new Faction
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            Type        = r.GetString(3),
            Description = r.GetString(4),
            Notes       = r.GetString(5),
            Goals       = r.GetString(6),
            Reputation  = r.GetInt32(7),
        };
    }
}