using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eCreatureLanguageRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eCreatureLanguageRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_creature_languages (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                creature_id      INTEGER NOT NULL REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                language_type_id INTEGER NOT NULL REFERENCES pathfinder_language_types(id),
                UNIQUE(creature_id, language_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eCreatureLanguage> GetForCreature(int creatureId)
        {
            var list = new List<Pf2eCreatureLanguage>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, creature_id, language_type_id FROM pathfinder_creature_languages WHERE creature_id = @cid";
            cmd.Parameters.AddWithValue("@cid", creatureId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eCreatureLanguage l)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_creature_languages (creature_id, language_type_id)
                VALUES (@cid, @ltid);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cid",  l.CreatureId);
            cmd.Parameters.AddWithValue("@ltid", l.LanguageTypeId);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_creature_languages WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eCreatureLanguage Map(SqliteDataReader r) => new Pf2eCreatureLanguage
        {
            Id             = r.GetInt32(0),
            CreatureId     = r.GetInt32(1),
            LanguageTypeId = r.GetInt32(2),
        };
    }
}
