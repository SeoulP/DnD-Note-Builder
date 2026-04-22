using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using DndBuilder.Core.Models;

namespace DndBuilder.Core.Repositories
{
    public class Pf2eInnateSpellRepository
    {
        private readonly SqliteConnection _conn;

        public Pf2eInnateSpellRepository(SqliteConnection conn) => _conn = conn;

        public void Migrate()
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS pathfinder_innate_spells (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                ability_id          INTEGER NOT NULL REFERENCES pathfinder_creature_abilities(id) ON DELETE CASCADE,
                spell_name          TEXT    NOT NULL,
                spell_rank          INTEGER NOT NULL DEFAULT 0,
                spell_frequency_id  INTEGER NOT NULL REFERENCES pathfinder_spell_frequencies(id),
                notes               TEXT    NOT NULL DEFAULT ''
            )";
            cmd.ExecuteNonQuery();
        }

        public List<Pf2eInnateSpell> GetForAbility(int abilityId)
        {
            var list = new List<Pf2eInnateSpell>();
            var cmd  = _conn.CreateCommand();
            cmd.CommandText = "SELECT id, ability_id, spell_name, spell_rank, spell_frequency_id, notes FROM pathfinder_innate_spells WHERE ability_id = @aid";
            cmd.Parameters.AddWithValue("@aid", abilityId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));
            return list;
        }

        public int Add(Pf2eInnateSpell s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO pathfinder_innate_spells (ability_id, spell_name, spell_rank, spell_frequency_id, notes)
                VALUES (@aid, @name, @rank, @freq, @notes);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@aid",   s.AbilityId);
            cmd.Parameters.AddWithValue("@name",  s.SpellName);
            cmd.Parameters.AddWithValue("@rank",  s.SpellRank);
            cmd.Parameters.AddWithValue("@freq",  s.SpellFrequencyId);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            return (int)(long)cmd.ExecuteScalar();
        }

        public void Edit(Pf2eInnateSpell s)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE pathfinder_innate_spells
                SET spell_name = @name, spell_rank = @rank, spell_frequency_id = @freq, notes = @notes
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@name",  s.SpellName);
            cmd.Parameters.AddWithValue("@rank",  s.SpellRank);
            cmd.Parameters.AddWithValue("@freq",  s.SpellFrequencyId);
            cmd.Parameters.AddWithValue("@notes", s.Notes);
            cmd.Parameters.AddWithValue("@id",    s.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pathfinder_innate_spells WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Pf2eInnateSpell Map(SqliteDataReader r) => new Pf2eInnateSpell
        {
            Id               = r.GetInt32(0),
            AbilityId        = r.GetInt32(1),
            SpellName        = r.GetString(2),
            SpellRank        = r.GetInt32(3),
            SpellFrequencyId = r.GetInt32(4),
            Notes            = r.GetString(5),
        };
    }
}
