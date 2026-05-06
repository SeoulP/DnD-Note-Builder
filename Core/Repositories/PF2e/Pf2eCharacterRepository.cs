using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCharacterRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCharacterRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_characters (
                id            INTEGER PRIMARY KEY REFERENCES characters(id) ON DELETE CASCADE,
                ancestry_id   INTEGER REFERENCES pathfinder_ancestries(id),
                heritage_id   INTEGER REFERENCES pathfinder_heritages(id),
                background_id INTEGER REFERENCES pathfinder_backgrounds(id),
                class_id      INTEGER REFERENCES pathfinder_classes(id),
                archetype_id  INTEGER REFERENCES pathfinder_archetypes(id),
                level         INTEGER NOT NULL DEFAULT 1,
                strength      INTEGER NOT NULL DEFAULT 10,
                dexterity     INTEGER NOT NULL DEFAULT 10,
                constitution  INTEGER NOT NULL DEFAULT 10,
                intelligence  INTEGER NOT NULL DEFAULT 10,
                wisdom        INTEGER NOT NULL DEFAULT 10,
                charisma      INTEGER NOT NULL DEFAULT 10,
                max_hp        INTEGER NOT NULL DEFAULT 0,
                current_hp    INTEGER NOT NULL DEFAULT 0,
                hero_points   INTEGER NOT NULL DEFAULT 1
            )";
            cmd.ExecuteNonQuery();

            foreach (var (col, def) in new[]
            {
                ("ac",              "INTEGER NOT NULL DEFAULT 10"),
                ("speed_feet",      "INTEGER NOT NULL DEFAULT 25"),
                ("fortitude_rank",  "INTEGER NOT NULL DEFAULT 0"),
                ("reflex_rank",     "INTEGER NOT NULL DEFAULT 0"),
                ("will_rank",       "INTEGER NOT NULL DEFAULT 0"),
                ("perception_rank", "INTEGER NOT NULL DEFAULT 0"),
            })
            {
                var check = _conn.CreateCommand();
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('pathfinder_characters') WHERE name='{col}'";
                if ((long)check.ExecuteScalar() == 0)
                {
                    var alter = _conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE pathfinder_characters ADD COLUMN {col} {def}";
                    alter.ExecuteNonQuery();
                }
            }
        }

        public int Add(Pf2eCharacter c)
        {
            var baseCmd = _conn.CreateCommand();
            baseCmd.CommandText = @"INSERT INTO characters (campaign_id, name, description, notes, portrait_path, gender, occupation, personality)
                                    VALUES (@cid, @name, @desc, @notes, '', '', '', '');
                                    SELECT last_insert_rowid();";
            baseCmd.Parameters.AddWithValue("@cid",   c.CampaignId);
            baseCmd.Parameters.AddWithValue("@name",  c.Name);
            baseCmd.Parameters.AddWithValue("@desc",  c.Description);
            baseCmd.Parameters.AddWithValue("@notes", c.Notes);
            c.Id = (int)(long)baseCmd.ExecuteScalar();

            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_characters
                (id, ancestry_id, heritage_id, background_id, class_id, archetype_id,
                 level, strength, dexterity, constitution, intelligence, wisdom, charisma,
                 max_hp, current_hp, hero_points, ac, speed_feet, fortitude_rank, reflex_rank, will_rank, perception_rank)
                VALUES (@id, @anc, @her, @bg, @cls, @arch,
                        @lvl, @str, @dex, @con, @int, @wis, @cha,
                        @maxhp, @curhp, @hero, @ac, @spd, @fort, @ref, @will, @perc)";
            BindPf2e(cmd, c);
            cmd.ExecuteNonQuery();
            return c.Id;
        }

        public Pf2eCharacter Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = SelectJoin + " WHERE c.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<Pf2eCharacter> GetAll(int campaignId)
        {
            var list = new List<Pf2eCharacter>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = SelectJoin + " WHERE c.campaign_id = @cid ORDER BY c.name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public void Edit(Pf2eCharacter c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_characters SET
                ancestry_id=@anc, heritage_id=@her, background_id=@bg, class_id=@cls, archetype_id=@arch,
                level=@lvl, strength=@str, dexterity=@dex, constitution=@con,
                intelligence=@int, wisdom=@wis, charisma=@cha,
                max_hp=@maxhp, current_hp=@curhp, hero_points=@hero,
                ac=@ac, speed_feet=@spd, fortitude_rank=@fort, reflex_rank=@ref, will_rank=@will, perception_rank=@perc
                WHERE id=@id";
            BindPf2e(cmd, c);
            cmd.ExecuteNonQuery();
        }

        public void EditBase(Pf2eCharacter c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE characters SET name=@name, description=@desc, notes=@notes WHERE id=@id";
            cmd.Parameters.AddWithValue("@name",  c.Name);
            cmd.Parameters.AddWithValue("@desc",  c.Description);
            cmd.Parameters.AddWithValue("@notes", c.Notes);
            cmd.Parameters.AddWithValue("@id",    c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM characters WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private const string SelectJoin = @"
            SELECT c.id, c.campaign_id, c.name, c.description, c.notes,
                   pc.ancestry_id, pc.heritage_id, pc.background_id, pc.class_id, pc.archetype_id,
                   pc.level, pc.strength, pc.dexterity, pc.constitution, pc.intelligence, pc.wisdom, pc.charisma,
                   pc.max_hp, pc.current_hp, pc.hero_points,
                   pc.ac, pc.speed_feet, pc.fortitude_rank, pc.reflex_rank, pc.will_rank, pc.perception_rank
            FROM characters c JOIN pathfinder_characters pc ON pc.id = c.id";

        private static void BindPf2e(SqliteCommand cmd, Pf2eCharacter c)
        {
            cmd.Parameters.AddWithValue("@id",    c.Id);
            cmd.Parameters.AddWithValue("@anc",   c.AncestryId.HasValue   ? (object)c.AncestryId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@her",   c.HeritageId.HasValue   ? (object)c.HeritageId.Value   : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@bg",    c.BackgroundId.HasValue ? (object)c.BackgroundId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@cls",   c.ClassId.HasValue      ? (object)c.ClassId.Value      : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@arch",  c.ArchetypeId.HasValue  ? (object)c.ArchetypeId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@lvl",   c.Level);
            cmd.Parameters.AddWithValue("@str",   c.Strength);
            cmd.Parameters.AddWithValue("@dex",   c.Dexterity);
            cmd.Parameters.AddWithValue("@con",   c.Constitution);
            cmd.Parameters.AddWithValue("@int",   c.Intelligence);
            cmd.Parameters.AddWithValue("@wis",   c.Wisdom);
            cmd.Parameters.AddWithValue("@cha",   c.Charisma);
            cmd.Parameters.AddWithValue("@maxhp", c.MaxHp);
            cmd.Parameters.AddWithValue("@curhp", c.CurrentHp);
            cmd.Parameters.AddWithValue("@hero",  c.HeroPoints);
            cmd.Parameters.AddWithValue("@ac",    c.Ac);
            cmd.Parameters.AddWithValue("@spd",   c.SpeedFeet);
            cmd.Parameters.AddWithValue("@fort",  c.FortitudeRank);
            cmd.Parameters.AddWithValue("@ref",   c.ReflexRank);
            cmd.Parameters.AddWithValue("@will",  c.WillRank);
            cmd.Parameters.AddWithValue("@perc",  c.PerceptionRank);
        }

        private static Pf2eCharacter Map(SqliteDataReader r) => new Pf2eCharacter
        {
            Id             = r.GetInt32(0),
            CampaignId     = r.GetInt32(1),
            Name           = r.GetString(2),
            Description    = r.GetString(3),
            Notes          = r.GetString(4),
            AncestryId     = r.IsDBNull(5)  ? (int?)null : r.GetInt32(5),
            HeritageId     = r.IsDBNull(6)  ? (int?)null : r.GetInt32(6),
            BackgroundId   = r.IsDBNull(7)  ? (int?)null : r.GetInt32(7),
            ClassId        = r.IsDBNull(8)  ? (int?)null : r.GetInt32(8),
            ArchetypeId    = r.IsDBNull(9)  ? (int?)null : r.GetInt32(9),
            Level          = r.GetInt32(10),
            Strength       = r.GetInt32(11),
            Dexterity      = r.GetInt32(12),
            Constitution   = r.GetInt32(13),
            Intelligence   = r.GetInt32(14),
            Wisdom         = r.GetInt32(15),
            Charisma       = r.GetInt32(16),
            MaxHp          = r.GetInt32(17),
            CurrentHp      = r.GetInt32(18),
            HeroPoints     = r.GetInt32(19),
            Ac             = r.GetInt32(20),
            SpeedFeet      = r.GetInt32(21),
            FortitudeRank  = r.GetInt32(22),
            ReflexRank     = r.GetInt32(23),
            WillRank       = r.GetInt32(24),
            PerceptionRank = r.GetInt32(25),
        };
    }
}
