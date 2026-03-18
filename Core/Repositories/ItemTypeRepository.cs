using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class ItemTypeRepository
    {
        private readonly SqliteConnection _conn;

        private static readonly (string Name, string Description)[] Defaults =
        {
            ("Weapon",    "Swords, axes, bows, and other offensive items"),
            ("Armor",     "Worn protective equipment"),
            ("Consumable","Single-use items such as potions and scrolls"),
            ("Trinket",   "Minor keepsakes and curiosities with no mechanical use"),
            ("Document",  "Letters, maps, contracts, and written materials"),
            ("Treasure",  "Coins, gems, art objects, and valuable goods"),
            ("Key Item",  "Plot-critical items tied to quests or story beats"),
            ("Misc",      "Anything that does not fit another category"),
        };

        public ItemTypeRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS item_types (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                inactive    INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            var hasInactive = _conn.CreateCommand();
            hasInactive.CommandText = "SELECT COUNT(*) FROM pragma_table_info('item_types') WHERE name = 'inactive'";
            if ((long)hasInactive.ExecuteScalar() == 0)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE item_types ADD COLUMN inactive INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }

        public void SeedDefaults(int campaignId)
        {
            foreach (var (name, desc) in Defaults)
            {
                var cmd = _conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO item_types (campaign_id, name, description)
                    SELECT @cid, @name, @desc WHERE NOT EXISTS
                        (SELECT 1 FROM item_types WHERE campaign_id = @cid AND name = @name)";
                cmd.Parameters.AddWithValue("@cid",  campaignId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.ExecuteNonQuery();
            }
        }

        public List<ItemType> GetAll(int campaignId)
        {
            var list = new List<ItemType>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description FROM item_types WHERE campaign_id = @cid AND inactive = 0 ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new ItemType
                {
                    Id          = reader.GetInt32(0),
                    CampaignId  = reader.GetInt32(1),
                    Name        = reader.GetString(2),
                    Description = reader.GetString(3),
                });
            return list;
        }

        public int Add(ItemType type)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO item_types (campaign_id, name, description) VALUES (@cid, @name, @desc); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",  type.CampaignId);
            cmd.Parameters.AddWithValue("@name", type.Name);
            cmd.Parameters.AddWithValue("@desc", type.Description);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE item_types SET inactive = 1 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}