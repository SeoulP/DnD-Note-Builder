using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class SubspeciesRepository
    {
        private readonly SqliteConnection _conn;

        public SubspeciesRepository(SqliteConnection conn) => _conn = conn;

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

        public void SeedDefaults(int campaignId)
        {
            // TEMPORARY SEED DATA — DELETE BEFORE COMMITTING
            // Source: D&D 5e 2024 Player's Handbook. Revert this method after exporting to .dndx.
            var data = new (string speciesName, (string name, string description)[] subs)[]
            {
                ("Dragonborn", new[]
                {
                    ("Chromatic Dragonborn", "Descended from the chromatic dragons — black, blue, green, red, and white. Their breath weapon reflects their draconic ancestor's type."),
                    ("Gem Dragonborn",       "Descended from the gem dragons — amethyst, crystal, emerald, sapphire, and topaz. They possess a unique psionic resonance."),
                    ("Metallic Dragonborn",  "Descended from the metallic dragons — brass, bronze, copper, gold, and silver. Known for their diplomatic temperaments."),
                }),
                ("Elf", new[]
                {
                    ("Drow",     "Drow, or dark elves, have lived for millennia in the Underdark. They are stealthy, quick, and possess powerful innate spellcasting."),
                    ("High Elf", "High Elves have a keen mind and a mastery of at least the basics of magic, inherited from their long study of the arcane."),
                    ("Wood Elf", "Wood Elves are fleet of foot and stealthy, masters of moving through the natural world without leaving a trace."),
                }),
                ("Gnome", new[]
                {
                    ("Forest Gnome", "Forest Gnomes have an innate talent with illusions and an instinctive quickness and stealth that belies their non-threatening appearance."),
                    ("Rock Gnome",   "Rock Gnomes have an extraordinary aptitude for invention, tinkering with ordinary objects and tools to craft magical gadgets and devices."),
                }),
            };

            foreach (var (speciesName, subs) in data)
            {
                var speciesId = LookupSpeciesId(campaignId, speciesName);
                if (speciesId == null) continue;

                foreach (var (subName, subDesc) in subs)
                {
                    var cmd = _conn.CreateCommand();
                    cmd.CommandText = @"INSERT INTO subspecies (campaign_id, species_id, name, description)
                        SELECT @cid, @sid, @name, @desc
                        WHERE NOT EXISTS
                            (SELECT 1 FROM subspecies WHERE campaign_id = @cid AND species_id = @sid AND name = @name)";
                    cmd.Parameters.AddWithValue("@cid",  campaignId);
                    cmd.Parameters.AddWithValue("@sid",  speciesId.Value);
                    cmd.Parameters.AddWithValue("@name", subName);
                    cmd.Parameters.AddWithValue("@desc", subDesc);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private int? LookupSpeciesId(int campaignId, string name)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM species WHERE campaign_id = @cid AND name = @name AND inactive = 0 LIMIT 1";
            cmd.Parameters.AddWithValue("@cid",  campaignId);
            cmd.Parameters.AddWithValue("@name", name);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (int)(long)result;
        }

        public List<Subspecies> GetAllForSpecies(int speciesId)
        {
            var list = new List<Subspecies>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE species_id = @sid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@sid", speciesId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public List<Subspecies> GetAll(int campaignId)
        {
            var list = new List<Subspecies>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Subspecies Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, species_id, name, description, notes
                                FROM subspecies WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Subspecies sub)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO subspecies (campaign_id, species_id, name, description, notes)
                                VALUES (@cid, @sid, @name, @desc, @notes);
                                SELECT last_insert_rowid();";
            Bind(cmd, sub);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Subspecies sub)
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

        private static void Bind(SqliteCommand cmd, Subspecies s)
        {
            cmd.Parameters.AddWithValue("@cid",   s.CampaignId);
            cmd.Parameters.AddWithValue("@sid",   s.SpeciesId);
            cmd.Parameters.AddWithValue("@name",  s.Name);
            cmd.Parameters.AddWithValue("@desc",  s.Description);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
        }

        private static Subspecies Map(SqliteDataReader r) => new Subspecies
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
