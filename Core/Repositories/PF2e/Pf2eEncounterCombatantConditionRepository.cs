using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eEncounterCombatantConditionRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eEncounterCombatantConditionRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pf2e_encounter_combatant_conditions (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                combatant_id      INTEGER NOT NULL REFERENCES pf2e_encounter_combatants(id) ON DELETE CASCADE,
                condition_type_id INTEGER NOT NULL REFERENCES pathfinder_condition_types(id) ON DELETE CASCADE,
                condition_value   INTEGER NOT NULL DEFAULT 0,
                UNIQUE(combatant_id, condition_type_id)
            )";
            cmd.ExecuteNonQuery();
        }

        public void Upsert(int combatantId, int conditionTypeId, int value)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pf2e_encounter_combatant_conditions (combatant_id, condition_type_id, condition_value)
                                VALUES (@cid, @tid, @val)
                                ON CONFLICT(combatant_id, condition_type_id) DO UPDATE SET condition_value=@val";
            cmd.Parameters.AddWithValue("@cid", combatantId);
            cmd.Parameters.AddWithValue("@tid", conditionTypeId);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int combatantId, int conditionTypeId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pf2e_encounter_combatant_conditions WHERE combatant_id=@cid AND condition_type_id=@tid";
            cmd.Parameters.AddWithValue("@cid", combatantId);
            cmd.Parameters.AddWithValue("@tid", conditionTypeId);
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eEncounterCombatantCondition> GetForCombatant(int combatantId)
        {
            var list = new List<Pf2eEncounterCombatantCondition>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, combatant_id, condition_type_id, condition_value FROM pf2e_encounter_combatant_conditions WHERE combatant_id=@cid";
            cmd.Parameters.AddWithValue("@cid", combatantId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(new Pf2eEncounterCombatantCondition
            {
                Id              = r.GetInt32(0),
                CombatantId     = r.GetInt32(1),
                ConditionTypeId = r.GetInt32(2),
                ConditionValue  = r.GetInt32(3),
            });
            return list;
        }
    }
}
