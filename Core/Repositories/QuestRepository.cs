using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class QuestRepository
    {
        private readonly SqliteConnection _conn;

        public QuestRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS quests (
                id             INTEGER PRIMARY KEY,
                campaign_id    INTEGER NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                name           TEXT    NOT NULL DEFAULT '',
                status_id      INTEGER REFERENCES quest_statuses(id) ON DELETE SET NULL,
                description    TEXT    NOT NULL DEFAULT '',
                notes          TEXT    NOT NULL DEFAULT '',
                quest_giver_id INTEGER REFERENCES characters(id) ON DELETE SET NULL,
                location_id    INTEGER REFERENCES locations(id)  ON DELETE SET NULL,
                reward         TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Quest> GetAll(int campaignId)
        {
            var list = new List<Quest>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, status_id, description, notes, quest_giver_id, location_id, reward
                                FROM quests WHERE campaign_id = @cid ORDER BY name ASC";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public Quest Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, campaign_id, name, status_id, description, notes, quest_giver_id, location_id, reward
                                FROM quests WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public int Add(Quest quest)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO quests (campaign_id, name, status_id, description, notes, quest_giver_id, location_id, reward)
                                VALUES (@cid, @name, @sid, @desc, @notes, @qgid, @lid, @reward);
                                SELECT last_insert_rowid();";
            Bind(cmd, quest);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Quest quest)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE quests
                                SET name = @name, status_id = @sid, description = @desc,
                                    notes = @notes, quest_giver_id = @qgid, location_id = @lid, reward = @reward
                                WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", quest.Id);
            Bind(cmd, quest);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM quests WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void Bind(SqliteCommand cmd, Quest q)
        {
            cmd.Parameters.AddWithValue("@cid",    q.CampaignId);
            cmd.Parameters.AddWithValue("@name",   q.Name);
            cmd.Parameters.AddWithValue("@sid",    q.StatusId.HasValue      ? q.StatusId.Value      : DBNull.Value);
            cmd.Parameters.AddWithValue("@desc",   q.Description);
            cmd.Parameters.AddWithValue("@notes",  q.Notes);
            cmd.Parameters.AddWithValue("@qgid",   q.QuestGiverId.HasValue  ? q.QuestGiverId.Value  : DBNull.Value);
            cmd.Parameters.AddWithValue("@lid",    q.LocationId.HasValue    ? q.LocationId.Value    : DBNull.Value);
            cmd.Parameters.AddWithValue("@reward", q.Reward);
        }

        private static Quest Map(SqliteDataReader r) => new Quest
        {
            Id            = r.GetInt32(0),
            CampaignId    = r.GetInt32(1),
            Name          = r.GetString(2),
            StatusId      = r.IsDBNull(3) ? null : r.GetInt32(3),
            Description   = r.GetString(4),
            Notes         = r.GetString(5),
            QuestGiverId  = r.IsDBNull(6) ? null : r.GetInt32(6),
            LocationId    = r.IsDBNull(7) ? null : r.GetInt32(7),
            Reward        = r.GetString(8),
        };
    }
}
