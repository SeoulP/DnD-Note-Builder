using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eEncounterCombatantHpLogRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eEncounterCombatantHpLogRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pf2e_encounter_combatant_hp_log (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                combatant_id INTEGER NOT NULL REFERENCES pf2e_encounter_combatants(id) ON DELETE CASCADE,
                delta        INTEGER NOT NULL DEFAULT 0,
                reason_text  TEXT NOT NULL DEFAULT '',
                logged_at    TEXT NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public void Add(int combatantId, int delta, string reasonText)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pf2e_encounter_combatant_hp_log (combatant_id, delta, reason_text, logged_at)
                                VALUES (@cid, @delta, @reason, @at)";
            cmd.Parameters.AddWithValue("@cid",    combatantId);
            cmd.Parameters.AddWithValue("@delta",  delta);
            cmd.Parameters.AddWithValue("@reason", reasonText);
            cmd.Parameters.AddWithValue("@at",     System.DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public Pf2eEncounterCombatantHpLogEntry GetLast(int combatantId)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, combatant_id, delta, reason_text, logged_at FROM pf2e_encounter_combatant_hp_log WHERE combatant_id=@cid ORDER BY id DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@cid", combatantId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<Pf2eEncounterCombatantHpLogEntry> GetRecent(int combatantId, int limit = 10)
        {
            var list = new List<Pf2eEncounterCombatantHpLogEntry>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, combatant_id, delta, reason_text, logged_at FROM pf2e_encounter_combatant_hp_log WHERE combatant_id=@cid ORDER BY id DESC LIMIT @lim";
            cmd.Parameters.AddWithValue("@cid", combatantId);
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pf2e_encounter_combatant_hp_log WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eEncounterCombatantHpLogEntry Map(SqliteDataReader r) => new Pf2eEncounterCombatantHpLogEntry
        {
            Id          = r.GetInt32(0),
            CombatantId = r.GetInt32(1),
            Delta       = r.GetInt32(2),
            ReasonText  = r.GetString(3),
            LoggedAt    = r.GetString(4),
        };
    }
}
