using System;
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

            // faction_relationships join table
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS faction_relationships (
                faction_id           INTEGER NOT NULL REFERENCES factions(id) ON DELETE CASCADE,
                related_faction_id   INTEGER NOT NULL REFERENCES factions(id) ON DELETE CASCADE,
                relationship_type_id INTEGER REFERENCES faction_relationship_types(id) ON DELETE SET NULL,
                PRIMARY KEY (faction_id, related_faction_id)
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
            if (!reader.Read()) return null;
            var faction = Map(reader);
            reader.Close();
            faction.RelatedFactions = GetRelationships(id);
            return faction;
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

        public List<FactionRelationship> GetRelationships(int factionId)
        {
            var list = new List<FactionRelationship>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT faction_id, related_faction_id, relationship_type_id FROM faction_relationships WHERE faction_id = @fid OR related_faction_id = @fid";
            cmd.Parameters.AddWithValue("@fid", factionId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new FactionRelationship
                {
                    FactionId          = reader.GetInt32(0),
                    RelatedFactionId   = reader.GetInt32(1),
                    RelationshipTypeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                });
            return list;
        }

        public void AddRelationship(int factionId, int relatedFactionId, int? typeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO faction_relationships (faction_id, related_faction_id, relationship_type_id) VALUES (@fid, @rfid, @tid)";
            cmd.Parameters.AddWithValue("@fid",  factionId);
            cmd.Parameters.AddWithValue("@rfid", relatedFactionId);
            cmd.Parameters.AddWithValue("@tid",  typeId.HasValue ? typeId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void RemoveRelationship(int factionAId, int factionBId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM faction_relationships
                                WHERE (faction_id = @a AND related_faction_id = @b)
                                   OR (faction_id = @b AND related_faction_id = @a)";
            cmd.Parameters.AddWithValue("@a", factionAId);
            cmd.Parameters.AddWithValue("@b", factionBId);
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