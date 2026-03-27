using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class SpeciesRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly string[] Defaults =
        {
            // Core PHB races
            "Human", "Elf", "High Elf", "Wood Elf", "Dark Elf (Drow)",
            "Dwarf", "Hill Dwarf", "Mountain Dwarf",
            "Halfling", "Lightfoot Halfling", "Stout Halfling",
            "Gnome", "Rock Gnome", "Forest Gnome",
            "Half-Elf", "Half-Orc", "Tiefling", "Dragonborn",
            // Common NPC / monster races
            "Aasimar", "Orc", "Goblin", "Hobgoblin", "Bugbear",
            "Kobold", "Lizardfolk", "Gnoll", "Yuan-ti",
            "Tabaxi", "Kenku", "Tortle", "Firbolg", "Triton",
            "Undead", "Construct",
        };

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

        public void SeedDefaults(int campaignId)
        {
            // Seed legacy flat name list (keeps NPC species dropdowns working)
            foreach (var name in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO species (campaign_id, name)
                    SELECT @cid, @name WHERE NOT EXISTS
                        (SELECT 1 FROM species WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }

            // TEMPORARY SEED DATA — DELETE BEFORE COMMITTING
            // Source: D&D 5e 2024 Player's Handbook. Revert this method after exporting to .dndx.
            var descriptions = new (string name, string description)[]
            {
                ("Aasimar",    "Touched by divine power, Aasimar carry a spark of the Upper Planes within their souls. They are descended from humans and celestials, bearing a divine charge to protect the innocent."),
                ("Dragonborn", "Born of dragons, Dragonborn walk proudly through a world that greets them with fearful incomprehension. Their draconic ancestry manifests in their appearance, in their behavior, and in their abilities."),
                ("Dwarf",      "Bold and hardy, Dwarves are known as skilled warriors, miners, and workers of stone and metal. They are sturdy, long-lived, and deeply loyal to their clans."),
                ("Elf",        "Elves are a magical people of otherworldly grace, living in places of ethereal beauty. Long-lived and quick to notice threats, they are skilled warriors, mages, and scouts."),
                ("Gnome",      "A gnome's energy and enthusiasm for living shines through every inch of their tiny bodies. They delight in innovation, tinkering, and exploring the world."),
                ("Goliath",    "Goliaths are towering figures built for physical competition. Driven to always push their limits, they thrive on proving themselves through deed."),
                ("Halfling",   "The comfort of home is the goal of most Halflings' lives. Despite their small size, they are remarkably brave, possessed of an uncanny luck that serves them well."),
                ("Human",      "The most adaptable and ambitious people among the common races, Humans are short-lived but driven by a burning ambition that sees them spread across the multiverse."),
                ("Orc",        "Orcs are built for a difficult life, with a quick temper and impressive strength. They are driven by great physical ability and an intensity of emotion that shapes their culture."),
                ("Tiefling",   "To be greeted with stares and whispers, to suffer violence and insult on the street — this is the lot of the Tiefling. Their fiendish heritage is plain to see in their appearance."),
            };

            foreach (var (name, description) in descriptions)
            {
                // Ensure the species exists (top-level 2024 PHB entries)
                var insertCmd = _conn.CreateCommand();
                insertCmd.CommandText = @"INSERT INTO species (campaign_id, name, description)
                    SELECT @cid, @name, @desc
                    WHERE NOT EXISTS (SELECT 1 FROM species WHERE campaign_id = @cid AND name = @name)";
                insertCmd.Parameters.AddWithValue("@cid",  campaignId);
                insertCmd.Parameters.AddWithValue("@name", name);
                insertCmd.Parameters.AddWithValue("@desc", description);
                insertCmd.ExecuteNonQuery();

                // Fill description on existing rows that have none
                var updateCmd = _conn.CreateCommand();
                updateCmd.CommandText = @"UPDATE species SET description = @desc
                    WHERE campaign_id = @cid AND name = @name AND (description IS NULL OR description = '')";
                updateCmd.Parameters.AddWithValue("@cid",  campaignId);
                updateCmd.Parameters.AddWithValue("@name", name);
                updateCmd.Parameters.AddWithValue("@desc", description);
                updateCmd.ExecuteNonQuery();
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
