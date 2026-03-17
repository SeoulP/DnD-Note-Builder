using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class ItemRepository
    {
        private readonly SqliteConnection _conn;

        public ItemRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            // items — system-agnostic identity and flavor
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS items (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id)  ON DELETE CASCADE,
                name        TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT '',
                notes       TEXT    NOT NULL DEFAULT '',
                is_unique   INTEGER NOT NULL DEFAULT 0,
                type_id     INTEGER REFERENCES item_types(id) ON DELETE SET NULL
            )";
            cmd.ExecuteNonQuery();

            // system_items — bridge between Item and game-system mechanics
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS system_items (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                item_id     INTEGER NOT NULL REFERENCES items(id)     ON DELETE CASCADE,
                system      TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();

            // dnd5e_item_mechanics — stub; full columns defined in Phase 2
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_item_mechanics (
                id             INTEGER PRIMARY KEY,
                system_item_id INTEGER NOT NULL REFERENCES system_items(id) ON DELETE CASCADE
            )";
            cmd.ExecuteNonQuery();

            // character_items — links items to characters (NPCs or PCs)
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS character_items (
                character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                item_id      INTEGER NOT NULL REFERENCES items(id)      ON DELETE CASCADE,
                PRIMARY KEY (character_id, item_id)
            )";
            cmd.ExecuteNonQuery();

            // location_items — links items to locations
            cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS location_items (
                location_id INTEGER NOT NULL REFERENCES locations(id) ON DELETE CASCADE,
                item_id     INTEGER NOT NULL REFERENCES items(id)     ON DELETE CASCADE,
                PRIMARY KEY (location_id, item_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Item> GetAll(int campaignId)
        {
            var list = new List<Item>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, notes, is_unique, type_id FROM items WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Item Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, description, notes, is_unique, type_id FROM items WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Item item)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO items (campaign_id, name, description, notes, is_unique, type_id)
                                VALUES (@cid, @name, @desc, @notes, @unique, @typeid);
                                SELECT last_insert_rowid();";
            Bind(cmd, item);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Item item)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE items
                                SET name = @name, description = @desc, notes = @notes,
                                    is_unique = @unique, type_id = @typeid
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", item.Id);
            Bind(cmd, item);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM items WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void AddCharacterItem(int characterId, int itemId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO character_items (character_id, item_id) VALUES (@cid, @iid)";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@iid", itemId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveCharacterItem(int characterId, int itemId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_items WHERE character_id = @cid AND item_id = @iid";
            cmd.Parameters.AddWithValue("@cid", characterId);
            cmd.Parameters.AddWithValue("@iid", itemId);
            cmd.ExecuteNonQuery();
        }

        public void AddLocationItem(int locationId, int itemId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO location_items (location_id, item_id) VALUES (@lid, @iid)";
            cmd.Parameters.AddWithValue("@lid", locationId);
            cmd.Parameters.AddWithValue("@iid", itemId);
            cmd.ExecuteNonQuery();
        }

        public void RemoveLocationItem(int locationId, int itemId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM location_items WHERE location_id = @lid AND item_id = @iid";
            cmd.Parameters.AddWithValue("@lid", locationId);
            cmd.Parameters.AddWithValue("@iid", itemId);
            cmd.ExecuteNonQuery();
        }

        private static void Bind(SqliteCommand cmd, Item i)
        {
            cmd.Parameters.AddWithValue("@cid",    i.CampaignId);
            cmd.Parameters.AddWithValue("@name",   i.Name);
            cmd.Parameters.AddWithValue("@desc",   i.Description);
            cmd.Parameters.AddWithValue("@notes",  i.Notes);
            cmd.Parameters.AddWithValue("@unique", i.IsUnique ? 1 : 0);
            cmd.Parameters.AddWithValue("@typeid", i.TypeId.HasValue ? i.TypeId.Value : DBNull.Value);
        }

        private static Item Map(SqliteDataReader r) => new Item
        {
            Id          = r.GetInt32(0),
            CampaignId  = r.GetInt32(1),
            Name        = r.GetString(2),
            Description = r.GetString(3),
            Notes       = r.GetString(4),
            IsUnique    = r.GetInt32(5) == 1,
            TypeId      = r.IsDBNull(6) ? null : r.GetInt32(6),
        };
    }
}