using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eEncounterCombatantRepository
    {
        private readonly SqliteConnection                     _conn;
        private readonly Pf2eEncounterCombatantHpLogRepository _hpLog;

        public Pf2eEncounterCombatantRepository(SqliteConnection conn, Pf2eEncounterCombatantHpLogRepository hpLog)
        {
            _conn  = conn;
            _hpLog = hpLog;
        }

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pf2e_encounter_combatants (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                encounter_id      INTEGER NOT NULL REFERENCES encounters(id) ON DELETE CASCADE,
                character_id      INTEGER REFERENCES characters(id) ON DELETE CASCADE,
                creature_id       INTEGER REFERENCES pathfinder_creatures(id) ON DELETE CASCADE,
                display_name      TEXT NOT NULL DEFAULT '',
                initiative        INTEGER NOT NULL DEFAULT 0,
                current_hp        INTEGER NOT NULL DEFAULT 0,
                max_hp            INTEGER NOT NULL DEFAULT 0,
                ac                INTEGER NOT NULL DEFAULT 10,
                sort_order        INTEGER NOT NULL DEFAULT 0,
                is_active         INTEGER NOT NULL DEFAULT 1,
                hero_points       INTEGER NOT NULL DEFAULT 0,
                actions_remaining INTEGER NOT NULL DEFAULT 3
            )";
            cmd.ExecuteNonQuery();
        }

        public int Add(Pf2eEncounterCombatant c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pf2e_encounter_combatants
                (encounter_id, character_id, creature_id, display_name, initiative, current_hp, max_hp, ac, sort_order, is_active, hero_points, actions_remaining)
                VALUES (@eid, @cid, @crid, @name, @init, @chp, @mhp, @ac, @sort, @active, @hero, @actions);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@eid",     c.EncounterId);
            cmd.Parameters.AddWithValue("@cid",     c.CharacterId.HasValue ? (object)c.CharacterId.Value : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@crid",    c.CreatureId.HasValue  ? (object)c.CreatureId.Value  : System.DBNull.Value);
            cmd.Parameters.AddWithValue("@name",    c.DisplayName);
            cmd.Parameters.AddWithValue("@init",    c.Initiative);
            cmd.Parameters.AddWithValue("@chp",     c.CurrentHp);
            cmd.Parameters.AddWithValue("@mhp",     c.MaxHp);
            cmd.Parameters.AddWithValue("@ac",      c.Ac);
            cmd.Parameters.AddWithValue("@sort",    c.SortOrder);
            cmd.Parameters.AddWithValue("@active",  c.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@hero",    c.HeroPoints);
            cmd.Parameters.AddWithValue("@actions", c.ActionsRemaining);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eEncounterCombatant c)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pf2e_encounter_combatants SET
                display_name=@name, initiative=@init, current_hp=@chp, max_hp=@mhp, ac=@ac,
                sort_order=@sort, is_active=@active, hero_points=@hero, actions_remaining=@actions
                WHERE id=@id";
            cmd.Parameters.AddWithValue("@name",    c.DisplayName);
            cmd.Parameters.AddWithValue("@init",    c.Initiative);
            cmd.Parameters.AddWithValue("@chp",     c.CurrentHp);
            cmd.Parameters.AddWithValue("@mhp",     c.MaxHp);
            cmd.Parameters.AddWithValue("@ac",      c.Ac);
            cmd.Parameters.AddWithValue("@sort",    c.SortOrder);
            cmd.Parameters.AddWithValue("@active",  c.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@hero",    c.HeroPoints);
            cmd.Parameters.AddWithValue("@actions", c.ActionsRemaining);
            cmd.Parameters.AddWithValue("@id",      c.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pf2e_encounter_combatants WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetDefeated(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pf2e_encounter_combatants SET is_active=0 WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetActive(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pf2e_encounter_combatants SET is_active=1 WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetSortOrder(int id, int sortOrder)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pf2e_encounter_combatants SET sort_order=@s WHERE id=@id";
            cmd.Parameters.AddWithValue("@s",  sortOrder);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetActionsRemaining(int id, int count)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pf2e_encounter_combatants SET actions_remaining=@a WHERE id=@id";
            cmd.Parameters.AddWithValue("@a",  count);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetHeroPoints(int id, int count)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE pf2e_encounter_combatants SET hero_points=@h WHERE id=@id";
            cmd.Parameters.AddWithValue("@h",  count);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // Writes new HP to pf2e_encounter_combatants, syncs to pathfinder_characters if PC, logs delta.
        public void UpdateHp(int combatantId, int newHp, string reason = "")
        {
            var combatant = Get(combatantId);
            if (combatant == null) return;
            int delta = newHp - combatant.CurrentHp;

            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE pf2e_encounter_combatants SET current_hp=@hp WHERE id=@id";
                cmd.Parameters.AddWithValue("@hp", newHp);
                cmd.Parameters.AddWithValue("@id", combatantId);
                cmd.ExecuteNonQuery();
            }

            if (combatant.CharacterId.HasValue)
            {
                using var cmd2 = _conn.CreateCommand();
                cmd2.CommandText = "UPDATE pathfinder_characters SET current_hp=@hp WHERE id=@id";
                cmd2.Parameters.AddWithValue("@hp", newHp);
                cmd2.Parameters.AddWithValue("@id", combatant.CharacterId.Value);
                cmd2.ExecuteNonQuery();
            }

            _hpLog.Add(combatantId, delta, reason);
        }

        public void UndoLastHpChange(int combatantId)
        {
            var last = _hpLog.GetLast(combatantId);
            if (last == null) return;
            var combatant = Get(combatantId);
            if (combatant == null) return;
            UpdateHp(combatantId, combatant.CurrentHp - last.Delta, "Undo");
            _hpLog.Delete(last.Id);
            // Remove the "Undo" log entry too so undo doesn't stack
            var undoEntry = _hpLog.GetLast(combatantId);
            if (undoEntry != null && undoEntry.ReasonText == "Undo")
                _hpLog.Delete(undoEntry.Id);
        }

        public Pf2eEncounterCombatant Get(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, encounter_id, character_id, creature_id, display_name, initiative,
                                current_hp, max_hp, ac, sort_order, is_active, hero_points, actions_remaining
                                FROM pf2e_encounter_combatants WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        // Returns active combatants first (sort_order ASC), defeated at bottom.
        public List<Pf2eEncounterCombatant> GetAll(int encounterId)
        {
            var list = new List<Pf2eEncounterCombatant>();
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT id, encounter_id, character_id, creature_id, display_name, initiative,
                                current_hp, max_hp, ac, sort_order, is_active, hero_points, actions_remaining
                                FROM pf2e_encounter_combatants
                                WHERE encounter_id=@eid
                                ORDER BY is_active DESC, sort_order ASC";
            cmd.Parameters.AddWithValue("@eid", encounterId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        private static Pf2eEncounterCombatant Map(SqliteDataReader r) => new Pf2eEncounterCombatant
        {
            Id               = r.GetInt32(0),
            EncounterId      = r.GetInt32(1),
            CharacterId      = r.IsDBNull(2) ? null : r.GetInt32(2),
            CreatureId       = r.IsDBNull(3) ? null : r.GetInt32(3),
            DisplayName      = r.GetString(4),
            Initiative       = r.GetInt32(5),
            CurrentHp        = r.GetInt32(6),
            MaxHp            = r.GetInt32(7),
            Ac               = r.GetInt32(8),
            SortOrder        = r.GetInt32(9),
            IsActive         = r.GetInt32(10) == 1,
            HeroPoints       = r.GetInt32(11),
            ActionsRemaining = r.GetInt32(12),
        };
    }
}
