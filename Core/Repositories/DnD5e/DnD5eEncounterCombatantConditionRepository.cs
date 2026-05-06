using Microsoft.Data.Sqlite;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eEncounterCombatantConditionRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eEncounterCombatantConditionRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_encounter_combatant_conditions (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                combatant_id      INTEGER NOT NULL REFERENCES dnd5e_encounter_combatants(id) ON DELETE CASCADE,
                condition_type_id INTEGER NOT NULL,
                UNIQUE(combatant_id, condition_type_id)
            )";
            cmd.ExecuteNonQuery();
        }
    }
}
