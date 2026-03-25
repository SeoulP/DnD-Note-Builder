using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eBackgroundRepository
    {
        private readonly SqliteConnection _conn;

        // 2024 PHB backgrounds: (name, skillNames, description)
        private static readonly (string name, string skillNames, string description)[] Defaults =
        {
            ("Acolyte",     "Insight,Religion",           "You have spent your life in service to a temple, learning the rituals and prayers of your faith."),
            ("Artisan",     "Investigation,Persuasion",   "You are trained in a craft and have spent years working under a master artisan, learning the tricks of the trade."),
            ("Charlatan",   "Deception,Sleight of Hand",  "You have always had a knack for making people believe what you want them to believe."),
            ("Criminal",    "Deception,Stealth",          "You have a history of breaking the law, whether working as a thief, a smuggler, or a hired killer."),
            ("Entertainer", "Acrobatics,Performance",     "You thrive in front of an audience. You know how to entrance them, entertain them, and even inspire them."),
            ("Farmer",      "Animal Handling,Nature",     "You grew up close to the land, raising crops and tending livestock far from the clamour of city life."),
            ("Guard",       "Athletics,Perception",       "Your experience as a guard has sharpened your senses and honed your body for the rigors of watch duty."),
            ("Guide",       "Stealth,Survival",           "You grew up in the wilderness, learning its ways so you could guide others through its dangers."),
            ("Hermit",      "Medicine,Religion",          "You lived in seclusion for a formative part of your life, away from society and its noise."),
            ("Merchant",    "Animal Handling,Insight",    "You worked in trade, haggling for goods and learning to read the people on the other side of the counter."),
            ("Noble",       "History,Persuasion",         "You understand wealth, power, and privilege. You carry yourself with poise and know how to navigate courtly intrigue."),
            ("Sage",        "Arcana,History",             "You spent years learning the lore of the multiverse — history, magic, and the secrets of the cosmos."),
            ("Sailor",      "Acrobatics,Perception",      "You sailed the seas, learning every creak of your ship and keeping a sharp eye on the horizon."),
            ("Scribe",      "Investigation,Perception",   "You spent time in a scriptorium, copying texts and cataloguing knowledge for those who needed it."),
            ("Soldier",     "Athletics,Intimidation",     "War has been your life for as long as you care to remember. You trained as a youth, studied the use of weapons and armor, and watched your heroes fall."),
            ("Wayfarer",    "Insight,Stealth",            "You grew up on the road, surviving by your wits and your ability to read people and situations quickly."),
        };

        public DnD5eBackgroundRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_backgrounds (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                skill_count INTEGER NOT NULL DEFAULT 2,
                skill_names TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, skillNames, description) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description)
                    SELECT @cid, @name, 2, @skills, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM dnd5e_backgrounds WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",    campaignId);
                cmd.Parameters.AddWithValue("@name",   name);
                cmd.Parameters.AddWithValue("@skills", skillNames);
                cmd.Parameters.AddWithValue("@desc",   description);
                cmd.ExecuteNonQuery();
            }
        }

        public List<DnD5eBackground> GetAll(int campaignId)
        {
            var list = new List<DnD5eBackground>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description FROM dnd5e_backgrounds WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public DnD5eBackground Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description FROM dnd5e_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description)
                VALUES (@cid, @name, @count, @skills, @desc); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",    bg.CampaignId);
            cmd.Parameters.AddWithValue("@name",   bg.Name);
            cmd.Parameters.AddWithValue("@count",  bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",   bg.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE dnd5e_backgrounds SET name = @name, skill_count = @count, skill_names = @skills, description = @desc WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",     bg.Id);
            cmd.Parameters.AddWithValue("@name",   bg.Name);
            cmd.Parameters.AddWithValue("@count",  bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",   bg.Description);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM dnd5e_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static DnD5eBackground Map(SqliteDataReader r) => new DnD5eBackground
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            SkillCount  = r.GetInt32(3),
            SkillNames  = r.GetString(4),
            Description = r.GetString(5),
        };
    }
}
