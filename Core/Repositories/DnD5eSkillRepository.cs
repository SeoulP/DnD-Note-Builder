using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eSkillRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, string attr)[] Defaults =
        {
            ("Acrobatics",     "dex"),
            ("Animal Handling","wis"),
            ("Arcana",         "int"),
            ("Athletics",      "str"),
            ("Deception",      "cha"),
            ("History",        "int"),
            ("Insight",        "wis"),
            ("Intimidation",   "cha"),
            ("Investigation",  "int"),
            ("Medicine",       "wis"),
            ("Nature",         "int"),
            ("Perception",     "wis"),
            ("Performance",    "cha"),
            ("Persuasion",     "cha"),
            ("Religion",       "int"),
            ("Sleight of Hand","dex"),
            ("Stealth",        "dex"),
            ("Survival",       "wis"),
        };

        public DnD5eSkillRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_skills (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                attribute   TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, attr) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO dnd5e_skills (campaign_id, name, attribute)
                    SELECT @cid, @name, @attr WHERE NOT EXISTS
                        (SELECT 1 FROM dnd5e_skills WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@attr", attr);
                cmd.ExecuteNonQuery();
            }
        }

        public List<DnD5eSkill> GetAll(int campaignId)
        {
            var list = new List<DnD5eSkill>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, attribute FROM dnd5e_skills WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public DnD5eSkill Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, attribute FROM dnd5e_skills WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        private static DnD5eSkill Map(SqliteDataReader r) => new DnD5eSkill
        {
            Id         = r.GetInt32(0),
            CampaignId = r.GetInt32(1),
            Name       = r.GetString(2),
            Attribute  = r.GetString(3),
        };
    }
}
