using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eDamageTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string name, int isPhysical, int isEnergy, int isPersistent, int isSplash)[] Defaults =
        {
            ("Piercing",             1, 0, 0, 0),
            ("Slashing",             1, 0, 0, 0),
            ("Bludgeoning",          1, 0, 0, 0),
            ("Acid",                 0, 1, 0, 0),
            ("Acid (Persistent)",    0, 1, 1, 0),
            ("Acid (Splash)",        0, 1, 0, 1),
            ("Cold",                 0, 1, 0, 0),
            ("Electricity",          0, 1, 0, 0),
            ("Fire",                 0, 1, 0, 0),
            ("Fire (Persistent)",    0, 1, 1, 0),
            ("Fire (Splash)",        0, 1, 0, 1),
            ("Sonic",                0, 1, 0, 0),
            ("Force",                0, 1, 0, 0),
            ("Poison",               0, 0, 0, 0),
            ("Poison (Persistent)",  0, 0, 1, 0),
            ("Bleed",                0, 0, 0, 0),
            ("Bleed (Persistent)",   0, 0, 1, 0),
            ("Mental",               0, 0, 0, 0),
            ("Spirit",               0, 0, 0, 0),
            ("Void",                 0, 0, 0, 0),
            ("Vitality",             0, 0, 0, 0),
        };

        public Pf2eDamageTypeRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_damage_types (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id   INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name          TEXT    NOT NULL,
                is_physical   INTEGER NOT NULL DEFAULT 0,
                is_energy     INTEGER NOT NULL DEFAULT 0,
                is_persistent INTEGER NOT NULL DEFAULT 0,
                is_splash     INTEGER NOT NULL DEFAULT 0,
                UNIQUE(campaign_id, name)
            )";
            cmd.ExecuteNonQuery();
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, isPhysical, isEnergy, isPersistent, isSplash) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO pathfinder_damage_types (campaign_id, name, is_physical, is_energy, is_persistent, is_splash)
                    SELECT @cid, @name, @phys, @energy, @persist, @splash WHERE NOT EXISTS
                        (SELECT 1 FROM pathfinder_damage_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",     campaignId);
                cmd.Parameters.AddWithValue("@name",    name);
                cmd.Parameters.AddWithValue("@phys",    isPhysical);
                cmd.Parameters.AddWithValue("@energy",  isEnergy);
                cmd.Parameters.AddWithValue("@persist", isPersistent);
                cmd.Parameters.AddWithValue("@splash",  isSplash);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Pf2eDamageType> GetAll(int campaignId)
        {
            var list = new List<Pf2eDamageType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, is_physical, is_energy, is_persistent, is_splash FROM pathfinder_damage_types WHERE campaign_id = @cid ORDER BY name";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public Pf2eDamageType Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, is_physical, is_energy, is_persistent, is_splash FROM pathfinder_damage_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Pf2eDamageType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_damage_types (campaign_id, name, is_physical, is_energy, is_persistent, is_splash)
                VALUES (@cid, @name, @phys, @energy, @persist, @splash);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",     t.CampaignId);
            cmd.Parameters.AddWithValue("@name",    t.Name);
            cmd.Parameters.AddWithValue("@phys",    t.IsPhysical   ? 1 : 0);
            cmd.Parameters.AddWithValue("@energy",  t.IsEnergy     ? 1 : 0);
            cmd.Parameters.AddWithValue("@persist", t.IsPersistent ? 1 : 0);
            cmd.Parameters.AddWithValue("@splash",  t.IsSplash     ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eDamageType t)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_damage_types
                SET name = @name, is_physical = @phys, is_energy = @energy, is_persistent = @persist, is_splash = @splash
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",    t.Name);
            cmd.Parameters.AddWithValue("@phys",    t.IsPhysical   ? 1 : 0);
            cmd.Parameters.AddWithValue("@energy",  t.IsEnergy     ? 1 : 0);
            cmd.Parameters.AddWithValue("@persist", t.IsPersistent ? 1 : 0);
            cmd.Parameters.AddWithValue("@splash",  t.IsSplash     ? 1 : 0);
            cmd.Parameters.AddWithValue("@id",      t.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_damage_types WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eDamageType Map(SqliteDataReader r) => new Pf2eDamageType
        {
            Id           = r.GetInt32(0),
            CampaignId   = r.GetInt32(1),
            Name         = r.GetString(2),
            IsPhysical   = r.GetInt32(3) == 1,
            IsEnergy     = r.GetInt32(4) == 1,
            IsPersistent = r.GetInt32(5) == 1,
            IsSplash     = r.GetInt32(6) == 1,
        };
    }
}
