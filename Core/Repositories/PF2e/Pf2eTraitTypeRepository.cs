using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eTraitTypeRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eTraitTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_trait_types (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                description TEXT    NOT NULL DEFAULT '',
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eTraitType> GetAll(int campaignId)
        {
            var list = new List<Pf2eTraitType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM pathfinder_trait_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eTraitType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM pathfinder_trait_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eTraitType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_trait_types (campaign_id, name, description) VALUES (@cid, @name, @desc);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  t.CampaignId);
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eTraitType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pathfinder_trait_types SET name = @name, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@desc", t.Description);
            cmd.Parameters.AddWithValue("@id",   t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_trait_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eTraitType Map(SqliteDataReader r) => new Pf2eTraitType
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            Description = r.GetString(3),
        };
    }
}
