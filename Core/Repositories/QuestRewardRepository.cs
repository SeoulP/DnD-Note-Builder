using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class QuestRewardRepository
    {
        private readonly SqliteConnection _conn;

        public QuestRewardRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS quest_rewards (
                id          INTEGER PRIMARY KEY,
                quest_id    INTEGER NOT NULL REFERENCES quests(id) ON DELETE CASCADE,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                description TEXT    NOT NULL DEFAULT '',
                is_claimed  INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();

            // One-time migration: move legacy quests.reward text into quest_rewards rows
            var migInsert = _conn.CreateCommand();
            migInsert.CommandText = @"INSERT INTO quest_rewards (quest_id, sort_order, description, is_claimed)
                SELECT id, 0, reward, 0 FROM quests WHERE reward != '' AND reward IS NOT NULL";
            migInsert.ExecuteNonQuery();

            var migClear = _conn.CreateCommand();
            migClear.CommandText = "UPDATE quests SET reward = '' WHERE reward != '' AND reward IS NOT NULL";
            migClear.ExecuteNonQuery();
        }

        public List<QuestReward> GetAll(int questId)
        {
            var list = new List<QuestReward>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, quest_id, sort_order, description, is_claimed
                                FROM quest_rewards WHERE quest_id = @qid ORDER BY sort_order ASC, id ASC";
            cmd.Parameters.AddWithValue("@qid", questId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new QuestReward
                {
                    Id          = reader.GetInt32(0),
                    QuestId     = reader.GetInt32(1),
                    SortOrder   = reader.GetInt32(2),
                    Description = reader.GetString(3),
                    IsClaimed   = reader.GetInt32(4) != 0,
                });
            return list;
        }

        public int Add(QuestReward reward)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO quest_rewards (quest_id, sort_order, description, is_claimed)
                                VALUES (@qid, @sort, @desc, @claimed);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@qid",     reward.QuestId);
            cmd.Parameters.AddWithValue("@sort",    reward.SortOrder);
            cmd.Parameters.AddWithValue("@desc",    reward.Description);
            cmd.Parameters.AddWithValue("@claimed", reward.IsClaimed ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(QuestReward reward)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE quest_rewards SET description = @desc, is_claimed = @claimed WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",      reward.Id);
            cmd.Parameters.AddWithValue("@desc",    reward.Description);
            cmd.Parameters.AddWithValue("@claimed", reward.IsClaimed ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM quest_rewards WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
