using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eBackgroundRepository
    {
        private readonly SqliteConnection _conn;

        // 2024 PHB backgrounds: (name, skillNames, description, toolOptions, abilityScoreOptions)
        private static readonly (string name, string skillNames, string description, string toolOptions, string abilityScoreOptions)[] Defaults =
        {
            ("Acolyte",     "Insight,Religion",           "You devoted yourself to service in a temple, either nestled in a town or secluded in a sacred grove. There you performed hallowed rites in honor of a god or pantheon. You served under a priest and studied religion. Thanks to your priest's instruction and your own devotion, you also learned how to channel a modicum of divine power in service to your place of worship and the people who prayed there.",                                                                                                                                                         "Calligrapher's Supplies",          "int,wis,cha"),
            ("Artisan",     "Investigation,Persuasion",   "You began mopping floors and scrubbing counters in an artisan's workshop for a few coppers per day as soon as you were strong enough to carry a bucket. When you were finally old enough to apprentice, you learned to create basic crafts of your own, as well as how to sweet-talk the occasional demanding customer. As part of your studies, you picked up Gnomish, the tongue from which so many of the artisan's terms of art are derived.",                                                                                                               "Artisan's Tools (one of your choice)", "str,dex,int"),
            ("Charlatan",   "Deception,Sleight of Hand",  "Soon after you were old enough to order an ale, you already had a favorite stool in every tavern within ten miles of where you were born. As you traveled the circuit from public house to watering hole, you learned to prey on the unfortunates who were in the market for a comforting lie or two—perhaps a sham potion or forged ancestry records.",                                                                                                                                                                                                       "Forgery Kit",                      "dex,con,cha"),
            ("Criminal",    "Sleight of Hand,Stealth",    "You learned to earn your coin in dark alleyways, cutting purses or burgling shops. Perhaps you were part of a small gang of like-minded wrongdoers, who looked out for each other. Or maybe you were a lone wolf, fending for yourself against the local thieves' guild and older, more fearsome lawbreakers.",                                                                                                                                                                                                                                              "Thieves' Tools",                   "dex,con,int"),
            ("Entertainer", "Acrobatics,Performance",     "You spent much of your youth following roving fairs and carnivals, performing odd jobs for musicians and acrobats in exchange for lessons. You may have learned how to walk a tightrope, how to double pick a lute, or how to recite Elvish poetry with the impeccable trills of an elf poet. To this day, you thrive on applause and long for the stage.",                                                                                                                                                                                                   "Musical Instrument (one of your choice)", "str,dex,cha"),
            ("Farmer",      "Animal Handling,Nature",     "You grew up close to the land. Years tending animals and cultivating the earth rewarded you with patience and good health. You have a keen appreciation for nature's bounty alongside a healthy respect for nature's wrath. Like any farmers, you made frequent use of the agricultural almanacs produced by the greatest halfling farmers.",                                                                                                                                                                                                                   "Carpenter's Tools",                "str,con,wis"),
            ("Guard",       "Athletics,Perception",       "Your feet begin to ache when you remember the countless hours you spent at your post in the tower. You were trained to keep one eye outside the wall, watching for marauders sweeping from the nearby forest, and your other eye inside the wall, searching for cutpurses and troublemakers. At the end of each shift, you bunked in the mayor's barracks alongside your fellow sentries and the dwarven smiths who kept your armor snug and your weapons sharp.",                                                                                              "Gaming Set (one of your choice)",  "str,int,wis"),
            ("Guide",       "Stealth,Survival",           "You came of age in the outdoors, far from settled lands. Your home? Anywhere you chose to unfurl your bedroll. There are wonders on the frontier—strange monsters, pristine forests and streams, overgrown ruins of great halls once trod by giants—and you learned to fend for yourself as you explored them. From time to time, you traveled with a pair of friendly druids who were kind enough to instruct you in the fundamentals of channeling the magic of the wild.",                                                                                    "Cartographer's Tools",             "dex,con,wis"),
            ("Hermit",      "Medicine,Religion",          "You spent your early years secluded in a hut or monastery located well beyond the outskirts of the nearest settlement. In those days, your only companions were the creatures of the forest, who would occasionally visit to bring news of the outside world and supplies. The quiet and solitude you found in your time outside society allowed you to spend many hours pondering the mysteries of creation, attuning your mind to the magical energy flowing through the natural world.",                                                                          "Herbalism Kit",                    "con,wis,cha"),
            ("Merchant",    "Animal Handling,Persuasion", "You were apprenticed to a trader, caravan master, or shopkeeper, learning the fundamentals of commerce. You traveled broadly, and you earned a living by buying and selling the raw materials artisans need to practice their craft or finished work from such crafters. You might have transported goods from one place to another or bought them from traveling traders and sold them in your own shop.",                                                                                                                                                       "Navigator's Tools",                "con,int,cha"),
            ("Noble",       "History,Persuasion",         "You were raised in a castle as a creature of wealth, power, and privilege—none of it earned. Your family are minor aristocrats who saw to it that you received a first-class education, some of which you appreciated and some of which you resented. Your time in the castle, especially the many hours you spent observing your family at court, also taught you a great deal about leadership.",                                                                                                                                                             "Gaming Set (one of your choice)",  "str,int,cha"),
            ("Sage",        "Arcana,History",             "You spent your formative years traveling between manors and monasteries, performing various odd jobs and services in exchange for access to their libraries. You wiled away many a long evening with your nose buried in books and scrolls, learning the lore of the multiverse—even the rudiments of magic—and your mind only yearns for more.",                                                                                                                                                                                                              "Calligrapher's Supplies",          "con,int,wis"),
            ("Sailor",      "Acrobatics,Perception",      "Thus far, you've spent most of your days living the life of a seafarer, wind at your back and decks swaying beneath your feet, as you sailed toward your next adventure. You've perched on barstools in more ports of call than you can remember, faced down mighty storms, and swapped stories with the folk who live beneath the waves.",                                                                                                                                                                                                                   "Navigator's Tools",                "str,dex,wis"),
            ("Scribe",      "Investigation,Perception",   "You spent time in a scriptorium, copying texts and cataloguing knowledge for those who needed it. The careful, precise work of scribing honed your eye for detail and gave you an appreciation for the written word.",                                                                                                                                                                                                                                                                                                                                        "Calligrapher's Supplies",          ""),
            ("Soldier",     "Athletics,Intimidation",     "You began training for war at such an early age that you carry only a precious few memories of what life was like before you took up arms. Battle is in your blood. Sometimes you catch yourself reflexively performing the basic fighting exercises you learned as a youth. Eventually, you put that training to use on the battlefield, protecting the realm by waging war and studying the strategies of goblinoid generals.",                                                                                                                                 "Gaming Set (one of your choice)",  "str,dex,con"),
            ("Wayfarer",    "Insight,Stealth",            "You grew up on the streets, surrounded by similarly ill-fated castoffs, a few of them friends and a few of them rivals. You slept where you could and did odd jobs for food. At times, when the hunger became unbearable, you resorted to theft. Still, you never lost your pride and never abandoned hope. Fate is not yet finished with you.",                                                                                                                                                                                                              "Thieves' Tools",                   "dex,wis,cha"),
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

            AddColumnIfMissing("feat_ability_id",       "INTEGER REFERENCES abilities(id) ON DELETE SET NULL DEFAULT NULL");
            AddColumnIfMissing("tool_options",          "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("language_count",        "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing("is_custom",             "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("ability_score_options", "TEXT    NOT NULL DEFAULT ''");
        }

        private void AddColumnIfMissing(string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('dnd5e_backgrounds') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() > 0) return;
            var alter = _conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE dnd5e_backgrounds ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, skillNames, description, toolOptions, abilityScoreOptions) in Defaults)
            {
                var insert = _conn.CreateCommand();
                insert.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description, tool_options, ability_score_options)
                    SELECT @cid, @name, 2, @skills, @desc, @tools, @attrs WHERE NOT EXISTS
                        (SELECT 1 FROM dnd5e_backgrounds WHERE campaign_id = @cid AND name = @name)";
                insert.Parameters.AddWithValue("@cid",   campaignId);
                insert.Parameters.AddWithValue("@name",  name);
                insert.Parameters.AddWithValue("@skills", skillNames);
                insert.Parameters.AddWithValue("@desc",  description);
                insert.Parameters.AddWithValue("@tools", toolOptions);
                insert.Parameters.AddWithValue("@attrs", abilityScoreOptions);
                insert.ExecuteNonQuery();

                // Backfill tool_options + description for existing campaigns (independent of ability_score_options)
                var backfillTools = _conn.CreateCommand();
                backfillTools.CommandText = @"UPDATE dnd5e_backgrounds
                    SET tool_options = @tools, description = @desc
                    WHERE campaign_id = @cid AND name = @name AND is_custom = 0 AND tool_options = ''";
                backfillTools.Parameters.AddWithValue("@cid",   campaignId);
                backfillTools.Parameters.AddWithValue("@name",  name);
                backfillTools.Parameters.AddWithValue("@desc",  description);
                backfillTools.Parameters.AddWithValue("@tools", toolOptions);
                backfillTools.ExecuteNonQuery();

                // Backfill ability_score_options independently (may run after tool_options already set)
                var backfillAttrs = _conn.CreateCommand();
                backfillAttrs.CommandText = @"UPDATE dnd5e_backgrounds
                    SET ability_score_options = @attrs
                    WHERE campaign_id = @cid AND name = @name AND is_custom = 0 AND ability_score_options = ''";
                backfillAttrs.Parameters.AddWithValue("@cid",   campaignId);
                backfillAttrs.Parameters.AddWithValue("@name",  name);
                backfillAttrs.Parameters.AddWithValue("@attrs", abilityScoreOptions);
                backfillAttrs.ExecuteNonQuery();
            }
        }

        // Background name → seeded Origin Feat name (must match ability names seeded in AbilityRepository)
        private static readonly (string background, string feat)[] BackgroundFeatLinks =
        {
            ("Acolyte",     "Magic Initiate (Cleric)"),
            ("Artisan",     "Crafter"),
            ("Charlatan",   "Skilled"),
            ("Criminal",    "Alert"),
            ("Entertainer", "Musician"),
            ("Farmer",      "Tough"),
            ("Guard",       "Alert"),
            ("Guide",       "Magic Initiate (Druid)"),
            ("Hermit",      "Healer"),
            ("Merchant",    "Lucky"),
            ("Noble",       "Skilled"),
            ("Sage",        "Magic Initiate (Wizard)"),
            ("Sailor",      "Tavern Brawler"),
            ("Soldier",     "Savage Attacker"),
            ("Wayfarer",    "Lucky"),
        };

        /// <summary>
        /// Resolves feat_ability_id for all seeded standard backgrounds.
        /// Must be called after AbilityRepository.SeedDefaults so the feat IDs exist.
        /// Only sets feat_ability_id where it is currently NULL — never overwrites a user choice.
        /// </summary>
        public void LinkBackgroundFeats(int campaignId)
        {
            foreach (var (background, feat) in BackgroundFeatLinks)
            {
                var lookupCmd = _conn.CreateCommand();
                lookupCmd.CommandText = "SELECT id FROM abilities WHERE campaign_id = @cid AND name = @feat LIMIT 1";
                lookupCmd.Parameters.AddWithValue("@cid",  campaignId);
                lookupCmd.Parameters.AddWithValue("@feat", feat);
                var result = lookupCmd.ExecuteScalar();
                if (result == null) continue;
                int featId = (int)(long)result;

                var updateCmd = _conn.CreateCommand();
                updateCmd.CommandText = @"UPDATE dnd5e_backgrounds
                    SET feat_ability_id = @featId
                    WHERE campaign_id = @cid AND name = @bg AND is_custom = 0 AND feat_ability_id IS NULL";
                updateCmd.Parameters.AddWithValue("@featId", featId);
                updateCmd.Parameters.AddWithValue("@cid",    campaignId);
                updateCmd.Parameters.AddWithValue("@bg",     background);
                updateCmd.ExecuteNonQuery();
            }
        }

        public List<DnD5eBackground> GetAll(int campaignId)
        {
            var list = new List<DnD5eBackground>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options FROM dnd5e_backgrounds WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public DnD5eBackground Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options FROM dnd5e_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options)
                VALUES (@cid, @name, @count, @skills, @desc, @feat, @tools, @lang, @custom, @attrs); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",   bg.CampaignId);
            cmd.Parameters.AddWithValue("@name",  bg.Name);
            cmd.Parameters.AddWithValue("@count", bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",  bg.Description);
            cmd.Parameters.AddWithValue("@feat",  (object)bg.FeatAbilityId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@tools", bg.ToolOptions);
            cmd.Parameters.AddWithValue("@lang",  bg.LanguageCount);
            cmd.Parameters.AddWithValue("@custom", bg.IsCustom ? 1 : 0);
            cmd.Parameters.AddWithValue("@attrs", bg.AbilityScoreOptions);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE dnd5e_backgrounds SET name = @name, skill_count = @count, skill_names = @skills, description = @desc, feat_ability_id = @feat, tool_options = @tools, language_count = @lang, is_custom = @custom, ability_score_options = @attrs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",    bg.Id);
            cmd.Parameters.AddWithValue("@name",  bg.Name);
            cmd.Parameters.AddWithValue("@count", bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",  bg.Description);
            cmd.Parameters.AddWithValue("@feat",  (object)bg.FeatAbilityId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@tools", bg.ToolOptions);
            cmd.Parameters.AddWithValue("@lang",  bg.LanguageCount);
            cmd.Parameters.AddWithValue("@custom", bg.IsCustom ? 1 : 0);
            cmd.Parameters.AddWithValue("@attrs", bg.AbilityScoreOptions);
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
            Id            = r.GetInt32(0),
            CampaignId    = r.GetInt32(1),
            Name          = r.GetString(2),
            SkillCount    = r.GetInt32(3),
            SkillNames    = r.GetString(4),
            Description   = r.GetString(5),
            FeatAbilityId        = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
            ToolOptions          = r.GetString(7),
            LanguageCount        = r.GetInt32(8),
            IsCustom             = r.GetInt32(9) != 0,
            AbilityScoreOptions  = r.GetString(10),
        };
    }
}
