using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eBackgroundRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eBackgroundRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_backgrounds (
                id          INTEGER PRIMARY KEY,
                campaign_id INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL,
                skill_count INTEGER NOT NULL DEFAULT 2,
                skill_names TEXT    NOT NULL DEFAULT '',
                description TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();

            AddColumnIfMissing("feat_ability_id",       "INTEGER REFERENCES abilities(id) ON DELETE SET NULL DEFAULT NULL");
            AddColumnIfMissing("tool_options",          "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfMissing("language_count",        "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing("is_custom",             "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("ability_score_options", "TEXT    NOT NULL DEFAULT ''");
        }

        private void AddColumnIfMissing(string column, string definition)
        {
            var check = _conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('dnd5e_backgrounds') WHERE name = '{column}'";
            if ((long)check.ExecuteScalar() > 0) return;
            var alter = _conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE dnd5e_backgrounds ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }

        public List<DnD5eBackground> GetAll(int campaignId)
        {
            var list = new List<DnD5eBackground>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options FROM dnd5e_backgrounds WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public DnD5eBackground Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options FROM dnd5e_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO dnd5e_backgrounds (campaign_id, name, skill_count, skill_names, description, feat_ability_id, tool_options, language_count, is_custom, ability_score_options)
                VALUES (@cid, @name, @count, @skills, @desc, @feat, @tools, @lang, @custom, @attrs); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid",   bg.CampaignId);
            cmd.Parameters.AddWithValue("@name",  bg.Name);
            cmd.Parameters.AddWithValue("@count", bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",  bg.Description);
            cmd.Parameters.AddWithValue("@feat",  (object)bg.FeatAbilityId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@tools", bg.ToolOptions);
            cmd.Parameters.AddWithValue("@lang",  bg.LanguageCount);
            cmd.Parameters.AddWithValue("@custom", bg.IsCustom ? 1 : 0);
            cmd.Parameters.AddWithValue("@attrs", bg.AbilityScoreOptions);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(DnD5eBackground bg)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE dnd5e_backgrounds SET name = @name, skill_count = @count, skill_names = @skills, description = @desc, feat_ability_id = @feat, tool_options = @tools, language_count = @lang, is_custom = @custom, ability_score_options = @attrs WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",    bg.Id);
            cmd.Parameters.AddWithValue("@name",  bg.Name);
            cmd.Parameters.AddWithValue("@count", bg.SkillCount);
            cmd.Parameters.AddWithValue("@skills", bg.SkillNames);
            cmd.Parameters.AddWithValue("@desc",  bg.Description);
            cmd.Parameters.AddWithValue("@feat",  (object)bg.FeatAbilityId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@tools", bg.ToolOptions);
            cmd.Parameters.AddWithValue("@lang",  bg.LanguageCount);
            cmd.Parameters.AddWithValue("@custom", bg.IsCustom ? 1 : 0);
            cmd.Parameters.AddWithValue("@attrs", bg.AbilityScoreOptions);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM dnd5e_backgrounds WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static DnD5eBackground Map(SqliteDataReader r) => new DnD5eBackground
        {
            Id            = r.GetInt32(0),
            CampaignId    = r.GetInt32(1),
            Name          = r.GetString(2),
            SkillCount    = r.GetInt32(3),
            SkillNames    = r.GetString(4),
            Description   = r.GetString(5),
            FeatAbilityId        = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
            ToolOptions          = r.GetString(7),
            LanguageCount        = r.GetInt32(8),
            IsCustom             = r.GetInt32(9) != 0,
            AbilityScoreOptions  = r.GetString(10),
        };
    }
}
