using Microsoft.Data.Sqlite;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eEncounterCombatantHpLogRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eEncounterCombatantHpLogRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_encounter_combatant_hp_log (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                combatant_id INTEGER NOT NULL REFERENCES dnd5e_encounter_combatants(id) ON DELETE CASCADE,
                delta        INTEGER NOT NULL DEFAULT 0,
                reason_text  TEXT NOT NULL DEFAULT '',
                logged_at    TEXT NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }
    }
}
