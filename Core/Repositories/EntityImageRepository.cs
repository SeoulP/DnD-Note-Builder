using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class EntityImageRepository
    {
        private readonly SqliteConnection _conn;

        public EntityImageRepository(SqliteConnection conn)
        {
            _conn = conn;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS entity_images (
                id          INTEGER PRIMARY KEY,
                entity_type INTEGER NOT NULL,
                entity_id   INTEGER NOT NULL,
                path        TEXT    NOT NULL DEFAULT '',
                sort_order  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        public List<EntityImage> GetAll(EntityType entityType, int entityId)
        {
            var list = new List<EntityImage>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, entity_type, entity_id, path, sort_order
                                FROM entity_images
                                WHERE entity_type = @et AND entity_id = @eid
                                ORDER BY sort_order ASC";
            cmd.Parameters.AddWithValue("@et",  (int)entityType);
            cmd.Parameters.AddWithValue("@eid", entityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public int Add(EntityImage img)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO entity_images (entity_type, entity_id, path, sort_order)
                                VALUES (@et, @eid, @path, @order);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@et",    (int)img.EntityType);
            cmd.Parameters.AddWithValue("@eid",   img.EntityId);
            cmd.Parameters.AddWithValue("@path",  img.Path);
            cmd.Parameters.AddWithValue("@order", img.SortOrder);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM entity_images WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Migrates a single legacy portrait_path string into entity_images if not already present.
        /// Safe to call every startup — no-ops if a row already exists.
        /// </summary>
        public void MigrateLegacyPortrait(EntityType entityType, int entityId, string portraitPath)
        {
            if (string.IsNullOrEmpty(portraitPath)) return;

            var check = _conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM entity_images WHERE entity_type = @et AND entity_id = @eid";
            check.Parameters.AddWithValue("@et",  (int)entityType);
            check.Parameters.AddWithValue("@eid", entityId);
            if ((long)check.ExecuteScalar() > 0) return;

            Add(new EntityImage { EntityType = entityType, EntityId = entityId, Path = portraitPath, SortOrder = 0 });
        }

        private static EntityImage Map(SqliteDataReader r) => new()
        {
            Id         = r.GetInt32(0),
            EntityType = (EntityType)r.GetInt32(1),
            EntityId   = r.GetInt32(2),
            Path       = r.GetString(3),
            SortOrder  = r.GetInt32(4),
        };
    }
}