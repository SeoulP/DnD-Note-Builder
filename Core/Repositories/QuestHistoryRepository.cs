using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class QuestHistoryRepository
    {
        private readonly SqliteConnection _conn;

        public QuestHistoryRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS quest_history (
                id         INTEGER PRIMARY KEY,
                quest_id   INTEGER NOT NULL REFERENCES quests(id) ON DELETE CASCADE,
                session_id INTEGER REFERENCES sessions(id) ON DELETE SET NULL,
                note       TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<QuestHistory> GetAll(int questId)
        {
            var list = new List<QuestHistory>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, quest_id, session_id, note FROM quest_history WHERE quest_id = @qid ORDER BY id ASC";
            cmd.Parameters.AddWithValue("@qid", questId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(new QuestHistory
                {
                    Id        = reader.GetInt32(0),
                    QuestId   = reader.GetInt32(1),
                    SessionId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Note      = reader.GetString(3),
                });
            return list;
        }

        public int Add(QuestHistory entry)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO quest_history (quest_id, session_id, note)
                                VALUES (@qid, @sid, @note);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@qid",  entry.QuestId);
            cmd.Parameters.AddWithValue("@sid",  entry.SessionId.HasValue ? entry.SessionId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@note", entry.Note);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(QuestHistory entry)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE quest_history SET session_id = @sid, note = @note WHERE id = @id";
            cmd.Parameters.AddWithValue("@id",   entry.Id);
            cmd.Parameters.AddWithValue("@sid",  entry.SessionId.HasValue ? entry.SessionId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@note", entry.Note);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM quest_history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
