using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class SpeciesRepository
    {
        private readonly SqliteConnection _conn;

        public SpeciesRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS species (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                inactive    INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // species_levels — level progression for a species
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS species_levels (
                id         INTEGER PRIMARY KEY,
                species_id INTEGER NOT NULL REFERENCES species(id) ON DELETE CASCADE,
                level      INTEGER NOT NULL CHECK(level >= 1 AND level <= 20),
                features   TEXT    NOT NULL DEFAULT '',
                class_data TEXT    NOT NULL DEFAULT '',
                UNIQUE(species_id, level)
            )";
            cmd.ExecuteNonQuery();

            var hasInactive = _conn.CreateCommand();
            hasInactive.CommandText = "SELECT COUNT(*) FROM pragma_table_info('species') WHERE name = 'inactive'";
            if ((long)hasInactive.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE species ADD COLUMN inactive INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }

            var hasDescription = _conn.CreateCommand();
            hasDescription.CommandText = "SELECT COUNT(*) FROM pragma_table_info('species') WHERE name = 'description'";
            if ((long)hasDescription.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE species ADD COLUMN description TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }

            var hasNotes = _conn.CreateCommand();
            hasNotes.CommandText = "SELECT COUNT(*) FROM pragma_table_info('species') WHERE name = 'notes'";
            if ((long)hasNotes.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE species ADD COLUMN notes TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }
        }

        public List<Species> GetAll(int campaignId)
        {
            var list = new List<Species>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, notes FROM species WHERE campaign_id = @cid AND inactive = 0 ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Species Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, notes FROM species WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Species species)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO species (campaign_id, name, description, notes) VALUES (@cid, @name, @desc, @notes); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",   species.CampaignId);
            cmd.Parameters.AddWithValue("@name",  species.Name);
            cmd.Parameters.AddWithValue("@desc",  species.Description);
            cmd.Parameters.AddWithValue("@notes", species.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Species species)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE species SET name = @name, description = @desc, notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",    species.Id);
            cmd.Parameters.AddWithValue("@name",  species.Name);
            cmd.Parameters.AddWithValue("@desc",  species.Description);
            cmd.Parameters.AddWithValue("@notes", species.Notes);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE species SET inactive = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Species Map(SqliteDataReader r) => new Species
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            Description = r.GetString(3),
            Notes       = r.GetString(4),
        };

        // ── Level Progression ─────────────────────────────────────────────────

        public List<SpeciesLevel> GetLevelsForSpecies(int speciesId)
        {
            var list = new List<SpeciesLevel>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, species_id, level, features, class_data FROM species_levels WHERE species_id = @sid ORDER BY level ASC";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapLevel(reader));
            return list;
        }

        public void SaveLevel(SpeciesLevel lvl)
        {
            var cmd = _conn.CreateCommand();
            if (lvl.Id == 0)
            {
                cmd.CommandText = @"INSERT INTO species_levels (species_id, level, features, class_data)
                                    VALUES (@sid, @level, @features, @data);
                                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@sid",      lvl.SpeciesId);
                cmd.Parameters.AddWithValue("@level",    lvl.Level);
                cmd.Parameters.AddWithValue("@features", lvl.Features);
                cmd.Parameters.AddWithValue("@data",     lvl.ClassData);
                lvl.Id = (int)(long)cmd.ExecuteScalar();
            }
            else
            {
                cmd.CommandText = "UPDATE species_levels SET features = @features, class_data = @data WHERE id = @id";
                cmd.Parameters.AddWithValue("@id",       lvl.Id);
                cmd.Parameters.AddWithValue("@features", lvl.Features);
                cmd.Parameters.AddWithValue("@data",     lvl.ClassData);
                cmd.ExecuteNonQuery();
            }
        }

        public void InitializeLevels(int speciesId)
        {
            for (int i = 1; i <= 20; i++)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO species_levels (species_id, level, features, class_data)
                    SELECT @sid, @level, '', ''
                    WHERE NOT EXISTS (SELECT 1 FROM species_levels WHERE species_id = @sid AND level = @level)";
                cmd.Parameters.AddWithValue("@sid",   speciesId);
                cmd.Parameters.AddWithValue("@level", i);
                cmd.ExecuteNonQuery();
            }
        }

        private static SpeciesLevel MapLevel(SqliteDataReader r) => new SpeciesLevel
        {
            Id        = r.GetInt32(0),
            SpeciesId = r.GetInt32(1),
            Level     = r.GetInt32(2),
            Features  = r.GetString(3),
            ClassData = r.GetString(4),
        };
    }
}
