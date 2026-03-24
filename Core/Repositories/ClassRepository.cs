using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class ClassRepository
    {
        private readonly SqliteConnection _conn;

        public ClassRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            // ── classes table ─────────────────────────────────────────────────
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS classes (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // Additive columns — original
            AddColumnIfMissing("classes", "subclass_unlock_level", "INTEGER NOT NULL DEFAULT 3");

            // Additive columns — comprehensive class data
            AddColumnIfMissing("classes", "hit_die",               "INTEGER NOT NULL DEFAULT 8");
            AddColumnIfMissing("classes", "primary_ability",       "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "saving_throw_profs",    "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "armor_profs",           "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "weapon_profs",          "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "tool_profs",            "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "skill_choices_count",   "INTEGER NOT NULL DEFAULT 2");
            AddColumnIfMissing("classes", "skill_choices_options", "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "starting_equip_a",      "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "starting_equip_b",      "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "spellcasting_ability",  "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("classes", "is_ritual_caster",      "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("classes", "is_prepared_caster",    "INTEGER NOT NULL DEFAULT 0");

            // ── subclasses table ──────────────────────────────────────────────
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS subclasses (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                class_id    INTEGER NOT NULL REFERENCES classes(id)   ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // ── class_levels table ────────────────────────────────────────────
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS class_levels (
                id         INTEGER PRIMARY KEY,
                class_id   INTEGER NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
                level      INTEGER NOT NULL CHECK(level >= 1 AND level <= 20),
                features   TEXT    NOT NULL DEFAULT '',
                class_data TEXT    NOT NULL DEFAULT '',
                UNIQUE(class_id, level)
            )";
            cmd.ExecuteNonQuery();

            // prof_bonus column — additive; seed formula values for any rows that still have the default 2
            bool profColNew = false;
            var profCheck = _conn.CreateCommand();
            profCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('class_levels') WHERE name = 'prof_bonus'";
            if ((long)profCheck.ExecuteScalar() == 0)
            {
                var addCol = _conn.CreateCommand();
                addCol.CommandText = "ALTER TABLE class_levels ADD COLUMN prof_bonus INTEGER NOT NULL DEFAULT 2";
                addCol.ExecuteNonQuery();
                profColNew = true;
            }
            if (profColNew)
            {
                // Set formula defaults: 2+(level-1)/4 gives 2/2/2/2/3/3/3/3/4/4/4/4/5/5/5/5/6/6/6/6
                var setProf = _conn.CreateCommand();
                setProf.CommandText = "UPDATE class_levels SET prof_bonus = 2 + (level - 1) / 4";
                setProf.ExecuteNonQuery();
            }
        }

        private void AddColumnIfMissing(string table, string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
                alter.ExecuteNonQuery();
            }
        }

        public void SeedDefaults(int campaignId)
        {
            // TEMPORARY SEED DATA — DELETE BEFORE COMMITTING
            // Source: D&D 5e 2024 Player's Handbook. Revert this method after exporting to .dndx.
            var classes = new (string name, string description, int subclassLevel, string hitDie, string primary,
                               string savingThrows, string armor, string weapons, string tools,
                               int skillCount, string skillOptions,
                               string equipA, string equipB,
                               string spellcasting, bool ritual, bool prepared,
                               string[] subclasses)[]
            {
                ("Barbarian",
                 "A fierce warrior of primitive background who can enter a battle rage.",
                 3, "d12", "Strength",
                 "Strength, Constitution",
                 "Light armor, Medium armor, Shields",
                 "Simple weapons, Martial weapons",
                 "None",
                 2, "Animal Handling, Athletics, Intimidation, Nature, Perception, Survival",
                 "Explorer's Pack, 4 Handaxes",
                 "75 GP",
                 "", false, false,
                 new[] { "Path of the Berserker", "Path of the Wild Heart", "Path of the World Tree", "Path of the Zealot" }),

                ("Bard",
                 "An inspiring magician whose power echoes the music of creation.",
                 3, "d8", "Charisma",
                 "Dexterity, Charisma",
                 "Light armor",
                 "Simple weapons, Hand crossbows, Longswords, Rapiers, Shortswords",
                 "Three Musical Instruments of your choice",
                 3, "Any three skills",
                 "Leather Armor, 2 Daggers, Musical Instrument, Entertainer's Pack",
                 "90 GP",
                 "Charisma", false, false,
                 new[] { "College of Dance", "College of Glamour", "College of Lore", "College of Valor" }),

                ("Cleric",
                 "A priestly champion who wields divine magic in service of a higher power.",
                 3, "d8", "Wisdom",
                 "Wisdom, Charisma",
                 "Light armor, Medium armor, Shields",
                 "Simple weapons",
                 "None",
                 2, "History, Insight, Medicine, Persuasion, Religion",
                 "Chain Shirt, Shield, Holy Symbol, Priest's Pack",
                 "110 GP",
                 "Wisdom", true, true,
                 new[] { "Life Domain", "Light Domain", "Trickery Domain", "War Domain" }),

                ("Druid",
                 "A priest of the Old Faith, wielding the powers of nature and adopting animal forms.",
                 3, "d8", "Wisdom",
                 "Intelligence, Wisdom",
                 "Light armor, Medium armor, Shields (non-metal)",
                 "Clubs, Daggers, Javelins, Maces, Quarterstaffs, Scimitars, Sickles, Slings, Spears",
                 "Herbalism Kit",
                 2, "Arcana, Animal Handling, Insight, Medicine, Nature, Perception, Religion, Survival",
                 "Leather Armor, Shield, Druidic Focus, Explorer's Pack, Scimitar",
                 "50 GP",
                 "Wisdom", true, true,
                 new[] { "Circle of the Land", "Circle of the Moon", "Circle of the Sea", "Circle of the Stars" }),

                ("Fighter",
                 "A master of martial combat, skilled with a variety of weapons and armor.",
                 3, "d10", "Strength or Dexterity",
                 "Strength, Constitution",
                 "All armor, Shields",
                 "Simple weapons, Martial weapons",
                 "None",
                 2, "Acrobatics, Animal Handling, Athletics, History, Insight, Intimidation, Perception, Survival",
                 "Chain Mail, 1 Martial Melee Weapon, Handaxe (x2), Dungeoneer's Pack",
                 "155 GP",
                 "", false, false,
                 new[] { "Battle Master", "Champion", "Eldritch Knight", "Psi Warrior" }),

                ("Monk",
                 "A master of martial arts, harnessing the power of the body in pursuit of physical and spiritual perfection.",
                 3, "d8", "Strength or Dexterity",
                 "Strength, Dexterity",
                 "None",
                 "Simple weapons, Shortswords",
                 "One Artisan's Tool or Musical Instrument of your choice",
                 2, "Acrobatics, Athletics, History, Insight, Religion, Stealth",
                 "Spear, 5 Daggers, Artisan's Tools or Musical Instrument, Explorer's Pack",
                 "50 GP",
                 "", false, false,
                 new[] { "Warrior of the Elements", "Warrior of Mercy", "Warrior of Shadow", "Warrior of the Open Hand" }),

                ("Paladin",
                 "A holy warrior bound to a sacred oath.",
                 3, "d10", "Strength and Charisma",
                 "Wisdom, Charisma",
                 "All armor, Shields",
                 "Simple weapons, Martial weapons",
                 "None",
                 2, "Athletics, Insight, Intimidation, Medicine, Persuasion, Religion",
                 "Chain Mail, Shield, Longsword, 6 Javelins, Holy Symbol, Priest's Pack",
                 "150 GP",
                 "Charisma", false, true,
                 new[] { "Oath of Devotion", "Oath of Glory", "Oath of the Ancients", "Oath of Vengeance" }),

                ("Ranger",
                 "A warrior who uses martial prowess and nature magic to combat threats on the edges of civilization.",
                 3, "d10", "Dexterity and Wisdom",
                 "Strength, Dexterity",
                 "Light armor, Medium armor, Shields",
                 "Simple weapons, Martial weapons",
                 "None",
                 3, "Animal Handling, Athletics, Insight, Investigation, Nature, Perception, Stealth, Survival",
                 "Studded Leather Armor, Shortsword (x2), Longbow, Quiver of 20 Arrows, Dungeoneer's Pack",
                 "150 GP",
                 "Wisdom", false, true,
                 new[] { "Beast Master", "Fey Wanderer", "Gloom Stalker", "Hunter" }),

                ("Rogue",
                 "A scoundrel who uses stealth and trickery to overcome obstacles and enemies.",
                 3, "d8", "Dexterity",
                 "Dexterity, Intelligence",
                 "Light armor",
                 "Simple weapons, Hand crossbows, Longswords, Rapiers, Shortswords",
                 "Thieves' Tools",
                 4, "Acrobatics, Athletics, Deception, Insight, Intimidation, Investigation, Perception, Persuasion, Sleight of Hand, Stealth",
                 "Leather Armor, 2 Daggers, Thieves' Tools, Burglar's Pack",
                 "100 GP",
                 "", false, false,
                 new[] { "Arcane Trickster", "Assassin", "Soulknife", "Thief" }),

                ("Sorcerer",
                 "A spellcaster who draws on inherent magic from a gift or bloodline.",
                 1, "d6", "Charisma",
                 "Constitution, Charisma",
                 "None",
                 "Daggers, Quarterstaffs, Light crossbows",
                 "None",
                 2, "Arcana, Deception, Insight, Intimidation, Persuasion, Religion",
                 "Spellcasting Focus (Crystal), 2 Daggers, Dungeoneer's Pack",
                 "50 GP",
                 "Charisma", false, false,
                 new[] { "Aberrant Sorcery", "Clockwork Sorcery", "Draconic Sorcery", "Wild Magic Sorcery" }),

                ("Warlock",
                 "A wielder of magic derived from a bargain with an extraplanar entity.",
                 1, "d8", "Charisma",
                 "Wisdom, Charisma",
                 "Light armor",
                 "Simple weapons",
                 "None",
                 2, "Arcana, Deception, History, Intimidation, Investigation, Nature, Religion",
                 "Leather Armor, Arcane Focus (Orb), Dagger (x2), Scholar's Pack",
                 "100 GP",
                 "Charisma", false, false,
                 new[] { "Archfey Patron", "Celestial Patron", "Fiend Patron", "Great Old One Patron" }),

                ("Wizard",
                 "A scholarly magic-user capable of manipulating the structures of reality.",
                 2, "d6", "Intelligence",
                 "Intelligence, Wisdom",
                 "None",
                 "Daggers, Quarterstaffs, Light crossbows",
                 "None",
                 2, "Arcana, History, Insight, Investigation, Medicine, Religion",
                 "Spellbook, Spellcasting Focus (Arcane), 2 Daggers, Scholar's Pack",
                 "55 GP",
                 "Intelligence", true, true,
                 new[] { "Abjurer", "Diviner", "Evoker", "Illusionist" }),
            };

            for (int i = 0; i < classes.Length; i++)
            {
                var (name, description, subclassLevel, hitDie, primary,
                     savingThrows, armor, weapons, tools,
                     skillCount, skillOptions,
                     equipA, equipB,
                     spellcasting, ritual, prepared,
                     subclassNames) = classes[i];

                // Parse hit die string (e.g. "d12" → 12)
                int hitDieVal = int.TryParse(hitDie.TrimStart('d'), out int hd) ? hd : 8;

                var insertCmd = _conn.CreateCommand();
                insertCmd.CommandText = @"INSERT INTO classes
                    (campaign_id, name, description, sort_order, subclass_unlock_level,
                     hit_die, primary_ability, saving_throw_profs, armor_profs, weapon_profs, tool_profs,
                     skill_choices_count, skill_choices_options, starting_equip_a, starting_equip_b,
                     spellcasting_ability, is_ritual_caster, is_prepared_caster)
                    SELECT @cid, @name, @desc, @sort, @unlock,
                           @hitdie, @primary, @saving, @armor, @weapon, @tool,
                           @skillcount, @skillopts, @equipa, @equipb,
                           @spell, @ritual, @prepared
                    WHERE NOT EXISTS (SELECT 1 FROM classes WHERE campaign_id = @cid AND name = @name)";
                insertCmd.Parameters.AddWithValue("@cid",       campaignId);
                insertCmd.Parameters.AddWithValue("@name",      name);
                insertCmd.Parameters.AddWithValue("@desc",      description);
                insertCmd.Parameters.AddWithValue("@sort",      i);
                insertCmd.Parameters.AddWithValue("@unlock",    subclassLevel);
                insertCmd.Parameters.AddWithValue("@hitdie",    hitDieVal);
                insertCmd.Parameters.AddWithValue("@primary",   primary);
                insertCmd.Parameters.AddWithValue("@saving",    savingThrows);
                insertCmd.Parameters.AddWithValue("@armor",     armor);
                insertCmd.Parameters.AddWithValue("@weapon",    weapons);
                insertCmd.Parameters.AddWithValue("@tool",      tools);
                insertCmd.Parameters.AddWithValue("@skillcount",skillCount);
                insertCmd.Parameters.AddWithValue("@skillopts", skillOptions);
                insertCmd.Parameters.AddWithValue("@equipa",    equipA);
                insertCmd.Parameters.AddWithValue("@equipb",    equipB);
                insertCmd.Parameters.AddWithValue("@spell",     spellcasting);
                insertCmd.Parameters.AddWithValue("@ritual",    ritual ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@prepared",  prepared ? 1 : 0);
                insertCmd.ExecuteNonQuery();

                var idCmd = _conn.CreateCommand();
                idCmd.CommandText = "SELECT id FROM classes WHERE campaign_id = @cid AND name = @name";
                idCmd.Parameters.AddWithValue("@cid",  campaignId);
                idCmd.Parameters.AddWithValue("@name", name);
                var classId = (int)(long)idCmd.ExecuteScalar();

                for (int j = 0; j < subclassNames.Length; j++)
                {
                    var subCmd = _conn.CreateCommand();
                    subCmd.CommandText = @"INSERT INTO subclasses (campaign_id, class_id, name, sort_order)
                        SELECT @cid, @clid, @name, @sort
                        WHERE NOT EXISTS (SELECT 1 FROM subclasses WHERE campaign_id = @cid AND class_id = @clid AND name = @name)";
                    subCmd.Parameters.AddWithValue("@cid",  campaignId);
                    subCmd.Parameters.AddWithValue("@clid", classId);
                    subCmd.Parameters.AddWithValue("@name", subclassNames[j]);
                    subCmd.Parameters.AddWithValue("@sort", j);
                    subCmd.ExecuteNonQuery();
                }
            }

            SeedClassLevels(campaignId, "Barbarian", new (string features, string classData)[]
            {
                ("Rage\nUnarmored Defense\nWeapon Mastery",       "Rages: 2, Rage Damage: +2"),       // 1
                ("Danger Sense\nReckless Attack",                  "Rages: 2, Rage Damage: +2"),       // 2
                ("Barbarian Subclass\nPrimal Knowledge",           "Rages: 3, Rage Damage: +2"),       // 3
                ("Ability Score Improvement",                      "Rages: 3, Rage Damage: +2"),       // 4
                ("Extra Attack\nFast Movement",                    "Rages: 3, Rage Damage: +2"),       // 5
                ("Subclass Feature",                               "Rages: 4, Rage Damage: +2"),       // 6
                ("Feral Instinct\nInstinctive Pounce",            "Rages: 4, Rage Damage: +2"),       // 7
                ("Ability Score Improvement",                      "Rages: 4, Rage Damage: +2"),       // 8
                ("Brutal Strike",                                  "Rages: 4, Rage Damage: +3"),       // 9
                ("Subclass Feature",                               "Rages: 4, Rage Damage: +3"),       // 10
                ("Relentless Rage",                                "Rages: 4, Rage Damage: +3"),       // 11
                ("Ability Score Improvement",                      "Rages: 5, Rage Damage: +3"),       // 12
                ("Improved Brutal Strike",                         "Rages: 5, Rage Damage: +3"),       // 13
                ("Subclass Feature",                               "Rages: 5, Rage Damage: +3"),       // 14
                ("Persistent Rage",                                "Rages: 5, Rage Damage: +3"),       // 15
                ("Ability Score Improvement",                      "Rages: 5, Rage Damage: +4"),       // 16
                ("Improved Brutal Strike",                         "Rages: 6, Rage Damage: +4"),       // 17
                ("Indomitable Might",                              "Rages: 6, Rage Damage: +4"),       // 18
                ("Ability Score Improvement",                      "Rages: 6, Rage Damage: +4"),       // 19
                ("Primal Champion",                                "Rages: Unlimited, Rage Damage: +4"), // 20
            });

            SeedClassLevels(campaignId, "Fighter", new (string features, string classData)[]
            {
                ("Fighting Style\nSecond Wind\nWeapon Mastery",    "Second Wind: 1, Action Surge: —, Indomitable: —"),  // 1
                ("Action Surge\nTactical Mind",                    "Second Wind: 1, Action Surge: 1, Indomitable: —"),  // 2
                ("Fighter Subclass",                               "Second Wind: 1, Action Surge: 1, Indomitable: —"),  // 3
                ("Ability Score Improvement",                      "Second Wind: 1, Action Surge: 1, Indomitable: —"),  // 4
                ("Extra Attack\nTactical Shift",                   "Second Wind: 1, Action Surge: 1, Indomitable: —"),  // 5
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 1, Indomitable: —"),  // 6
                ("Subclass Feature",                               "Second Wind: 2, Action Surge: 1, Indomitable: —"),  // 7
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 1, Indomitable: —"),  // 8
                ("Indomitable (1 use)\nMaster of Armaments",      "Second Wind: 2, Action Surge: 1, Indomitable: 1"),  // 9
                ("Subclass Feature",                               "Second Wind: 2, Action Surge: 1, Indomitable: 1"),  // 10
                ("Two Extra Attacks",                              "Second Wind: 2, Action Surge: 1, Indomitable: 1"),  // 11
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 1, Indomitable: 2"),  // 12
                ("Studied Attacks",                                "Second Wind: 2, Action Surge: 1, Indomitable: 2"),  // 13
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 1, Indomitable: 2"),  // 14
                ("Subclass Feature",                               "Second Wind: 2, Action Surge: 1, Indomitable: 2"),  // 15
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 1, Indomitable: 2"),  // 16
                ("Action Surge (2 uses)\nIndomitable (3 uses)",   "Second Wind: 2, Action Surge: 2, Indomitable: 3"),  // 17
                ("Subclass Feature",                               "Second Wind: 2, Action Surge: 2, Indomitable: 3"),  // 18
                ("Ability Score Improvement",                      "Second Wind: 2, Action Surge: 2, Indomitable: 3"),  // 19
                ("Epic Boon",                                      "Second Wind: 2, Action Surge: 2, Indomitable: 3"),  // 20
            });
        }

        private void SeedClassLevels(int campaignId, string className, (string features, string classData)[] levels)
        {
            var idCmd = _conn.CreateCommand();
            idCmd.CommandText = "SELECT id FROM classes WHERE campaign_id = @cid AND name = @name LIMIT 1";
            idCmd.Parameters.AddWithValue("@cid",  campaignId);
            idCmd.Parameters.AddWithValue("@name", className);
            var result = idCmd.ExecuteScalar();
            if (result == null) return;
            int classId = (int)(long)result;

            for (int i = 0; i < levels.Length; i++)
            {
                var (features, classData) = levels[i];
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO class_levels (class_id, level, features, class_data)
                    SELECT @cid, @level, @features, @data
                    WHERE NOT EXISTS (SELECT 1 FROM class_levels WHERE class_id = @cid AND level = @level)";
                cmd.Parameters.AddWithValue("@cid",      classId);
                cmd.Parameters.AddWithValue("@level",    i + 1);
                cmd.Parameters.AddWithValue("@features", features);
                cmd.Parameters.AddWithValue("@data",     classData);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Classes CRUD ──────────────────────────────────────────────────────

        public List<Class> GetAll(int campaignId)
        {
            var list = new List<Class>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, description, notes, sort_order, subclass_unlock_level,
                                       hit_die, primary_ability, saving_throw_profs, armor_profs, weapon_profs, tool_profs,
                                       skill_choices_count, skill_choices_options, starting_equip_a, starting_equip_b,
                                       spellcasting_ability, is_ritual_caster, is_prepared_caster
                                FROM classes WHERE campaign_id = @cid ORDER BY sort_order ASC, name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapClass(reader));
            return list;
        }

        public Class Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, description, notes, sort_order, subclass_unlock_level,
                                       hit_die, primary_ability, saving_throw_profs, armor_profs, weapon_profs, tool_profs,
                                       skill_choices_count, skill_choices_options, starting_equip_a, starting_equip_b,
                                       spellcasting_ability, is_ritual_caster, is_prepared_caster
                                FROM classes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var cls = MapClass(reader);
            reader.Close();
            cls.Subclasses = GetSubclassesForClass(id);
            return cls;
        }

        public int Add(Class cls)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO classes
                (campaign_id, name, description, notes, sort_order, subclass_unlock_level,
                 hit_die, primary_ability, saving_throw_profs, armor_profs, weapon_profs, tool_profs,
                 skill_choices_count, skill_choices_options, starting_equip_a, starting_equip_b,
                 spellcasting_ability, is_ritual_caster, is_prepared_caster)
                VALUES
                (@cid, @name, @desc, @notes, @sort, @unlock,
                 @hitdie, @primary, @saving, @armor, @weapon, @tool,
                 @skillcount, @skillopts, @equipa, @equipb,
                 @spell, @ritual, @prepared);
                SELECT last_insert_rowid();";
            BindClass(cmd, cls);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Class cls)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE classes SET
                name = @name, description = @desc, notes = @notes,
                sort_order = @sort, subclass_unlock_level = @unlock,
                hit_die = @hitdie, primary_ability = @primary,
                saving_throw_profs = @saving, armor_profs = @armor,
                weapon_profs = @weapon, tool_profs = @tool,
                skill_choices_count = @skillcount, skill_choices_options = @skillopts,
                starting_equip_a = @equipa, starting_equip_b = @equipb,
                spellcasting_ability = @spell,
                is_ritual_caster = @ritual, is_prepared_caster = @prepared
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", cls.Id);
            BindClass(cmd, cls);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM classes WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Subclasses CRUD ───────────────────────────────────────────────────

        public List<Subclass> GetSubclassesForClass(int classId)
        {
            var list = new List<Subclass>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, class_id, name, description, notes, sort_order
                                FROM subclasses WHERE class_id = @cid ORDER BY sort_order ASC, name ASC";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapSubclass(reader));
            return list;
        }

        public List<Subclass> GetAllSubclasses(int campaignId)
        {
            var list = new List<Subclass>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT s.id, s.campaign_id, s.class_id, s.name, s.description, s.notes, s.sort_order
                                FROM subclasses s
                                JOIN classes c ON c.id = s.class_id
                                WHERE s.campaign_id = @cid ORDER BY c.sort_order ASC, c.name ASC, s.sort_order ASC, s.name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapSubclass(reader));
            return list;
        }

        public Subclass GetSubclass(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, class_id, name, description, notes, sort_order
                                FROM subclasses WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapSubclass(reader) : null;
        }

        public int AddSubclass(Subclass sub)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO subclasses (campaign_id, class_id, name, description, notes, sort_order)
                                VALUES (@cid, @clid, @name, @desc, @notes, @sort);
                                SELECT last_insert_rowid();";
            BindSubclass(cmd, sub);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void EditSubclass(Subclass sub)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE subclasses SET name = @name, description = @desc, notes = @notes, sort_order = @sort WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", sub.Id);
            BindSubclass(cmd, sub);
            cmd.ExecuteNonQuery();
        }

        public void DeleteSubclass(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM subclasses WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── ClassLevels CRUD ──────────────────────────────────────────────────

        public List<ClassLevel> GetLevelsForClass(int classId)
        {
            var list = new List<ClassLevel>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, class_id, level, features, class_data, prof_bonus
                                FROM class_levels WHERE class_id = @cid ORDER BY level ASC";
            cmd.Parameters.AddWithValue("@cid", classId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapLevel(reader));
            return list;
        }

        public void SaveLevel(ClassLevel lvl)
        {
            var cmd = _conn.CreateCommand();
            if (lvl.Id == 0)
            {
                cmd.CommandText = @"INSERT INTO class_levels (class_id, level, features, class_data, prof_bonus)
                                    VALUES (@cid, @level, @features, @data, @prof);
                                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@cid",      lvl.ClassId);
                cmd.Parameters.AddWithValue("@level",    lvl.Level);
                cmd.Parameters.AddWithValue("@features", lvl.Features);
                cmd.Parameters.AddWithValue("@data",     lvl.ClassData);
                cmd.Parameters.AddWithValue("@prof",     lvl.ProfBonus);
                lvl.Id = (int)(long)cmd.ExecuteScalar();
            }
            else
            {
                cmd.CommandText = "UPDATE class_levels SET features = @features, class_data = @data, prof_bonus = @prof WHERE id = @id";
                cmd.Parameters.AddWithValue("@id",       lvl.Id);
                cmd.Parameters.AddWithValue("@features", lvl.Features);
                cmd.Parameters.AddWithValue("@data",     lvl.ClassData);
                cmd.Parameters.AddWithValue("@prof",     lvl.ProfBonus);
                cmd.ExecuteNonQuery();
            }
        }

        public void InitializeLevels(int classId)
        {
            for (int i = 1; i <= 20; i++)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO class_levels (class_id, level, features, class_data, prof_bonus)
                    SELECT @cid, @level, '', '', @prof
                    WHERE NOT EXISTS (SELECT 1 FROM class_levels WHERE class_id = @cid AND level = @level)";
                cmd.Parameters.AddWithValue("@cid",   classId);
                cmd.Parameters.AddWithValue("@level", i);
                cmd.Parameters.AddWithValue("@prof",  2 + (i - 1) / 4);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void BindClass(SqliteCommand cmd, Class c)
        {
            cmd.Parameters.AddWithValue("@cid",       c.CampaignId);
            cmd.Parameters.AddWithValue("@name",      c.Name);
            cmd.Parameters.AddWithValue("@desc",      c.Description);
            cmd.Parameters.AddWithValue("@notes",     c.Notes);
            cmd.Parameters.AddWithValue("@sort",      c.SortOrder);
            cmd.Parameters.AddWithValue("@unlock",    c.SubclassUnlockLevel);
            cmd.Parameters.AddWithValue("@hitdie",    c.HitDie);
            cmd.Parameters.AddWithValue("@primary",   c.PrimaryAbility);
            cmd.Parameters.AddWithValue("@saving",    c.SavingThrowProfs);
            cmd.Parameters.AddWithValue("@armor",     c.ArmorProfs);
            cmd.Parameters.AddWithValue("@weapon",    c.WeaponProfs);
            cmd.Parameters.AddWithValue("@tool",      c.ToolProfs);
            cmd.Parameters.AddWithValue("@skillcount",c.SkillChoicesCount);
            cmd.Parameters.AddWithValue("@skillopts", c.SkillChoicesOptions);
            cmd.Parameters.AddWithValue("@equipa",    c.StartingEquipA);
            cmd.Parameters.AddWithValue("@equipb",    c.StartingEquipB);
            cmd.Parameters.AddWithValue("@spell",     c.SpellcastingAbility);
            cmd.Parameters.AddWithValue("@ritual",    c.IsRitualCaster   ? 1 : 0);
            cmd.Parameters.AddWithValue("@prepared",  c.IsPreparedCaster ? 1 : 0);
        }

        private static void BindSubclass(SqliteCommand cmd, Subclass s)
        {
            cmd.Parameters.AddWithValue("@cid",   s.CampaignId);
            cmd.Parameters.AddWithValue("@clid",  s.ClassId);
            cmd.Parameters.AddWithValue("@name",  s.Name);
            cmd.Parameters.AddWithValue("@desc",  s.Description);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@sort",  s.SortOrder);
        }

        private static Class MapClass(SqliteDataReader r) => new Class
        {
            Id                  = r.GetInt32(0),
            CampaignId          = r.GetInt32(1),
            Name                = r.GetString(2),
            Description         = r.GetString(3),
            Notes               = r.GetString(4),
            SortOrder           = r.GetInt32(5),
            SubclassUnlockLevel = r.IsDBNull(6)  ? 3  : r.GetInt32(6),
            HitDie              = r.IsDBNull(7)  ? 8  : r.GetInt32(7),
            PrimaryAbility      = r.IsDBNull(8)  ? "" : r.GetString(8),
            SavingThrowProfs    = r.IsDBNull(9)  ? "" : r.GetString(9),
            ArmorProfs          = r.IsDBNull(10) ? "" : r.GetString(10),
            WeaponProfs         = r.IsDBNull(11) ? "" : r.GetString(11),
            ToolProfs           = r.IsDBNull(12) ? "" : r.GetString(12),
            SkillChoicesCount   = r.IsDBNull(13) ? 2  : r.GetInt32(13),
            SkillChoicesOptions = r.IsDBNull(14) ? "" : r.GetString(14),
            StartingEquipA      = r.IsDBNull(15) ? "" : r.GetString(15),
            StartingEquipB      = r.IsDBNull(16) ? "" : r.GetString(16),
            SpellcastingAbility = r.IsDBNull(17) ? "" : r.GetString(17),
            IsRitualCaster      = !r.IsDBNull(18) && r.GetInt32(18) == 1,
            IsPreparedCaster    = !r.IsDBNull(19) && r.GetInt32(19) == 1,
        };

        private static Subclass MapSubclass(SqliteDataReader r) => new Subclass
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            ClassId     = r.GetInt32(2),
            Name        = r.GetString(3),
            Description = r.GetString(4),
            Notes       = r.GetString(5),
            SortOrder   = r.GetInt32(6),
        };

        private static ClassLevel MapLevel(SqliteDataReader r) => new ClassLevel
        {
            Id        = r.GetInt32(0),
            ClassId   = r.GetInt32(1),
            Level     = r.GetInt32(2),
            Features  = r.GetString(3),
            ClassData = r.GetString(4),
            ProfBonus = r.IsDBNull(5) ? 2 + (r.GetInt32(2) - 1) / 4 : r.GetInt32(5),
        };
    }
}
