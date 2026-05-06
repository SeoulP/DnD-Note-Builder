using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class DnD5eEncounterCombatantRepository
    {
        private readonly SqliteConnection _conn;

        public DnD5eEncounterCombatantRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS dnd5e_encounter_combatants (
                id                              INTEGER PRIMARY KEY AUTOINCREMENT,
                encounter_id                    INTEGER NOT NULL REFERENCES encounters(id) ON DELETE CASCADE,
                character_id                    INTEGER REFERENCES characters(id) ON DELETE CASCADE,
                creature_id                     INTEGER,
                display_name                    TEXT NOT NULL DEFAULT '',
                initiative                      INTEGER NOT NULL DEFAULT 0,
                current_hp                      INTEGER NOT NULL DEFAULT 0,
                max_hp                          INTEGER NOT NULL DEFAULT 0,
                temp_hp                         INTEGER NOT NULL DEFAULT 0,
                sort_order                      INTEGER NOT NULL DEFAULT 0,
                is_active                       INTEGER NOT NULL DEFAULT 1,
                concentration_spell             TEXT NOT NULL DEFAULT '',
                death_save_successes            INTEGER NOT NULL DEFAULT 0,
                death_save_failures             INTEGER NOT NULL DEFAULT 0,
                legendary_actions_remaining     INTEGER NOT NULL DEFAULT 0,
                legendary_actions_max           INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }
    }
}
