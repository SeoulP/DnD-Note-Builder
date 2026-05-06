using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creatures (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id      INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name             TEXT    NOT NULL,
                creature_type_id INTEGER NOT NULL REFERENCES pathfinder_creature_types(id),
                level            INTEGER NOT NULL DEFAULT 0,
                size_id          INTEGER NOT NULL REFERENCES pathfinder_sizes(id),
                str_mod          INTEGER NOT NULL DEFAULT 0,
                dex_mod          INTEGER NOT NULL DEFAULT 0,
                con_mod          INTEGER NOT NULL DEFAULT 0,
                int_mod          INTEGER NOT NULL DEFAULT 0,
                wis_mod          INTEGER NOT NULL DEFAULT 0,
                cha_mod          INTEGER NOT NULL DEFAULT 0,
                ac               INTEGER NOT NULL DEFAULT 0,
                max_hp           INTEGER NOT NULL DEFAULT 0,
                fortitude        INTEGER NOT NULL DEFAULT 0,
                reflex           INTEGER NOT NULL DEFAULT 0,
                will             INTEGER NOT NULL DEFAULT 0,
                perception       INTEGER NOT NULL DEFAULT 0,
                source           TEXT    NOT NULL DEFAULT '',
                source_page      INTEGER,
                notes            TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();

            // One-time: drop is_seeded column removed during SeedingService rollout
            try
            {
                var drop = _conn.CreateCommand();
                drop.CommandText = "ALTER TABLE pathfinder_creatures DROP COLUMN is_seeded";
                drop.ExecuteNonQuery();
            }
            catch { /* column already gone or never existed */ }
        }

        public List<Pf2eCreature> GetAll(int campaignId)
        {
            var list = new List<Pf2eCreature>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, creature_type_id, level, size_id,
                str_mod, dex_mod, con_mod, int_mod, wis_mod, cha_mod,
                ac, max_hp, fortitude, reflex, will, perception,
                source, source_page, notes
                FROM pathfinder_creatures WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eCreature Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, creature_type_id, level, size_id,
                str_mod, dex_mod, con_mod, int_mod, wis_mod, cha_mod,
                ac, max_hp, fortitude, reflex, will, perception,
                source, source_page, notes
                FROM pathfinder_creatures WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eCreature c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creatures
                (campaign_id, name, creature_type_id, level, size_id,
                 str_mod, dex_mod, con_mod, int_mod, wis_mod, cha_mod,
                 ac, max_hp, fortitude, reflex, will, perception,
                 source, source_page, notes)
                VALUES (@cid, @name, @ctid, @lvl, @szid,
                        @str, @dex, @con, @int, @wis, @cha,
                        @ac, @hp, @fort, @ref, @will, @perc,
                        @src, @srcpg, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",   c.CampaignId);
            cmd.Parameters.AddWithValue("@name",  c.Name);
            cmd.Parameters.AddWithValue("@ctid",  c.CreatureTypeId);
            cmd.Parameters.AddWithValue("@lvl",   c.Level);
            cmd.Parameters.AddWithValue("@szid",  c.SizeId);
            cmd.Parameters.AddWithValue("@str",   c.StrMod);
            cmd.Parameters.AddWithValue("@dex",   c.DexMod);
            cmd.Parameters.AddWithValue("@con",   c.ConMod);
            cmd.Parameters.AddWithValue("@int",   c.IntMod);
            cmd.Parameters.AddWithValue("@wis",   c.WisMod);
            cmd.Parameters.AddWithValue("@cha",   c.ChaMod);
            cmd.Parameters.AddWithValue("@ac",    c.Ac);
            cmd.Parameters.AddWithValue("@hp",    c.MaxHp);
            cmd.Parameters.AddWithValue("@fort",  c.Fortitude);
            cmd.Parameters.AddWithValue("@ref",   c.Reflex);
            cmd.Parameters.AddWithValue("@will",  c.Will);
            cmd.Parameters.AddWithValue("@perc",  c.Perception);
            cmd.Parameters.AddWithValue("@src",   c.Source);
            cmd.Parameters.AddWithValue("@srcpg", c.SourcePage.HasValue ? (object)c.SourcePage.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", c.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eCreature c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_creatures SET
                name = @name, creature_type_id = @ctid, level = @lvl, size_id = @szid,
                str_mod = @str, dex_mod = @dex, con_mod = @con, int_mod = @int, wis_mod = @wis, cha_mod = @cha,
                ac = @ac, max_hp = @hp, fortitude = @fort, reflex = @ref, will = @will, perception = @perc,
                source = @src, source_page = @srcpg, notes = @notes
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",  c.Name);
            cmd.Parameters.AddWithValue("@ctid",  c.CreatureTypeId);
            cmd.Parameters.AddWithValue("@lvl",   c.Level);
            cmd.Parameters.AddWithValue("@szid",  c.SizeId);
            cmd.Parameters.AddWithValue("@str",   c.StrMod);
            cmd.Parameters.AddWithValue("@dex",   c.DexMod);
            cmd.Parameters.AddWithValue("@con",   c.ConMod);
            cmd.Parameters.AddWithValue("@int",   c.IntMod);
            cmd.Parameters.AddWithValue("@wis",   c.WisMod);
            cmd.Parameters.AddWithValue("@cha",   c.ChaMod);
            cmd.Parameters.AddWithValue("@ac",    c.Ac);
            cmd.Parameters.AddWithValue("@hp",    c.MaxHp);
            cmd.Parameters.AddWithValue("@fort",  c.Fortitude);
            cmd.Parameters.AddWithValue("@ref",   c.Reflex);
            cmd.Parameters.AddWithValue("@will",  c.Will);
            cmd.Parameters.AddWithValue("@perc",  c.Perception);
            cmd.Parameters.AddWithValue("@src",   c.Source);
            cmd.Parameters.AddWithValue("@srcpg", c.SourcePage.HasValue ? (object)c.SourcePage.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", c.Notes);
            cmd.Parameters.AddWithValue("@id",    c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creatures WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreature Map(SqliteDataReader r) => new Pf2eCreature
        {
            Id             = r.GetInt32(0),
            CampaignId     = r.GetInt32(1),
            Name           = r.GetString(2),
            CreatureTypeId = r.GetInt32(3),
            Level          = r.GetInt32(4),
            SizeId         = r.GetInt32(5),
            StrMod         = r.GetInt32(6),
            DexMod         = r.GetInt32(7),
            ConMod         = r.GetInt32(8),
            IntMod         = r.GetInt32(9),
            WisMod         = r.GetInt32(10),
            ChaMod         = r.GetInt32(11),
            Ac             = r.GetInt32(12),
            MaxHp          = r.GetInt32(13),
            Fortitude      = r.GetInt32(14),
            Reflex         = r.GetInt32(15),
            Will           = r.GetInt32(16),
            Perception     = r.GetInt32(17),
            Source         = r.GetString(18),
            SourcePage     = r.IsDBNull(19) ? (int?)null : r.GetInt32(19),
            Notes          = r.GetString(20),
        };
    }
}
