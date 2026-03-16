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
                name        TEXT    NOT NULL
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var name in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT INTO species (campaign_id, name) VALUES (@cid, @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Species> GetAll(int campaignId)
        {
            var list = new List<Species>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name FROM species WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new Species { Id = reader.GetInt32(0), CampaignId = reader.GetInt32(1), Name = reader.GetString(2) });
            return list;
        }

        public int Add(Species species)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO species (campaign_id, name) VALUES (@cid, @name); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  species.CampaignId);
            cmd.Parameters.AddWithValue("@name", species.Name);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM species WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}