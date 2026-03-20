using Microsoft.Data.Sqlite;

namespace DndBuilder.Core.Repositories
{
    public class SettingsRepository
    {
        private readonly SqliteConnection _conn;

        public SettingsRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS app_settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            )";
            cmd.ExecuteNonQuery();
        }

        public string Get(string key, string defaultValue = "")
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result is string s ? s : defaultValue;
        }

        public void Set(string key, string value)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO app_settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value";
            cmd.Parameters.AddWithValue("@key",   key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }
    }
}
