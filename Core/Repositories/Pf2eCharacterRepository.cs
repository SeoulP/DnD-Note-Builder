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
        }

        public int Add(Pf2eCharacter c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_characters
                (id, ancestry_id, heritage_id, background_id, class_id, archetype_id,
                 level, strength, dexterity, constitution, intelligence, wisdom, charisma,
                 max_hp, current_hp, hero_points)
                VALUES (@id, @anc, @her, @bg, @cls, @arch,
                        @lvl, @str, @dex, @con, @int, @wis, @cha,
                        @maxhp, @curhp, @hero)";
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
            cmd.ExecuteNonQuery();
            return c.Id;
        }

        public Pf2eCharacter Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, ancestry_id, heritage_id, background_id, class_id, archetype_id,
                level, strength, dexterity, constitution, intelligence, wisdom, charisma,
                max_hp, current_hp, hero_points
                FROM pathfinder_characters WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public void Edit(Pf2eCharacter c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_characters SET
                ancestry_id = @anc, heritage_id = @her, background_id = @bg,
                class_id = @cls, archetype_id = @arch, level = @lvl,
                strength = @str, dexterity = @dex, constitution = @con,
                intelligence = @int, wisdom = @wis, charisma = @cha,
                max_hp = @maxhp, current_hp = @curhp, hero_points = @hero
                WHERE id = @id";
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
            cmd.Parameters.AddWithValue("@id",    c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_characters WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCharacter Map(SqliteDataReader r) => new Pf2eCharacter
        {
            Id           = r.GetInt32(0),
            AncestryId   = r.IsDBNull(1)  ? (int?)null : r.GetInt32(1),
            HeritageId   = r.IsDBNull(2)  ? (int?)null : r.GetInt32(2),
            BackgroundId = r.IsDBNull(3)  ? (int?)null : r.GetInt32(3),
            ClassId      = r.IsDBNull(4)  ? (int?)null : r.GetInt32(4),
            ArchetypeId  = r.IsDBNull(5)  ? (int?)null : r.GetInt32(5),
            Level        = r.GetInt32(6),
            Strength     = r.GetInt32(7),
            Dexterity    = r.GetInt32(8),
            Constitution = r.GetInt32(9),
            Intelligence = r.GetInt32(10),
            Wisdom       = r.GetInt32(11),
            Charisma     = r.GetInt32(12),
            MaxHp        = r.GetInt32(13),
            CurrentHp    = r.GetInt32(14),
            HeroPoints   = r.GetInt32(15),
        };
    }
}
