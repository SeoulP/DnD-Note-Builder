using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core;
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
            ProfBonus = r.IsDBNull(5) ? DnD5eMath.ProfBonus(r.GetInt32(2)) : r.GetInt32(5),
        };
    }
}
