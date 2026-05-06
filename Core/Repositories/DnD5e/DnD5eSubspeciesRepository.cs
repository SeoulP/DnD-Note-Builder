using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eSubspeciesRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eSubspeciesRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subspecies (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                species_id  INTEGER NOT NULL REFERENCES species(id)   ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<DnD5eSubspecies> GetAllForSpecies(int speciesId)
        {
            var list = new List<DnD5eSubspecies>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE species_id = @sid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public List<DnD5eSubspecies> GetAll(int campaignId)
        {
            var list = new List<DnD5eSubspecies>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public DnD5eSubspecies Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(DnD5eSubspecies sub)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO subspecies (campaign_id, species_id, name, description, notes)
                                VALUES (@cid, @sid, @name, @desc, @notes);
                                SELECT last_insert_rowid();";
            Bind(cmd, sub);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(DnD5eSubspecies sub)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE subspecies SET name = @name, description = @desc, notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", sub.Id);
            Bind(cmd, sub);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subspecies WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void Bind(SqliteCommand cmd, DnD5eSubspecies s)
        {
            cmd.Parameters.AddWithValue("@cid",   s.CampaignId);
            cmd.Parameters.AddWithValue("@sid",   s.SpeciesId);
            cmd.Parameters.AddWithValue("@name",  s.Name);
            cmd.Parameters.AddWithValue("@desc",  s.Description);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
        }

        private static DnD5eSubspecies Map(SqliteDataReader r) => new DnD5eSubspecies
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            SpeciesId   = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
            Notes       = r.GetString(5),
        };
    }
}
