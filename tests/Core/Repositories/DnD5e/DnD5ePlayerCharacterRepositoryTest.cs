using GdUnit4;
using Microsoft.Data.Sqlite;
using DndBuilder.Core.Models;
using DndBuilder.Core.Repositories;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.Repositories.DnD5e
{
    [TestSuite]
    public class DnD5ePlayerCharacterRepositoryTest
    {
        private SqliteConnection                    _conn;
        private DnD5ePlayerCharacterRepository     _repo;
        private const int PcId = 1;

        [BeforeTest]
        public void Setup()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            using (var p = _conn.CreateCommand()) { p.CommandText = "PRAGMA foreign_keys = OFF"; p.ExecuteNonQuery(); }

            // Base table that DnD5ePlayerCharacterRepository.Migrate() adds columns to
            var create = _conn.CreateCommand();
            create.CommandText = "CREATE TABLE IF NOT EXISTS player_characters (id INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();

            _repo = new DnD5ePlayerCharacterRepository(_conn);
            _repo.Migrate();
        }

        [AfterTest]
        public void Teardown() => _conn?.Close();

        // ── Weapon CRUD ───────────────────────────────────────────────────────

        [TestCase]
        public void GetWeapons_Empty_ReturnsEmpty() =>
            AssertThat(_repo.GetWeapons(PcId).Count).IsEqual(0);

        [TestCase]
        public void AddWeapon_GetWeapons_RoundTrip()
        {
            var w = new DnD5ePlayerCharacterWeapon
            {
                PlayerCharacterId = PcId,
                Name              = "Longsword",
                DamageDice        = "1d8",
                DamageType        = "slashing",
                DeriveFromAbility = true,
                DeriveAbility     = "str",
                IsProficient      = true,
                DeriveDamageMod   = true,
                AttackBonus       = 1,
                DamageBonus       = 0,
            };
            int id = _repo.AddWeapon(w);

            var list = _repo.GetWeapons(PcId);
            AssertThat(list.Count).IsEqual(1);
            AssertThat(list[0].Id).IsEqual(id);
            AssertThat(list[0].Name).IsEqual("Longsword");
            AssertThat(list[0].DamageDice).IsEqual("1d8");
            AssertBool(list[0].IsProficient).IsTrue();
            AssertThat(list[0].AttackBonus).IsEqual(1);
        }

        [TestCase]
        public void EditWeapon_UpdatesAllFields()
        {
            var w = new DnD5ePlayerCharacterWeapon { PlayerCharacterId = PcId, Name = "Dagger" };
            w.Id = _repo.AddWeapon(w);

            w.Name              = "Rapier";
            w.DamageDice        = "1d6";
            w.DamageType        = "piercing";
            w.DeriveAbility     = "dex";
            w.IsProficient      = true;
            w.DeriveDamageMod   = false;
            w.AttackBonus       = 2;
            w.DamageBonus       = 1;
            _repo.EditWeapon(w);

            var updated = _repo.GetWeapons(PcId)[0];
            AssertThat(updated.Name).IsEqual("Rapier");
            AssertThat(updated.DamageDice).IsEqual("1d6");
            AssertThat(updated.DeriveAbility).IsEqual("dex");
            AssertBool(updated.IsProficient).IsTrue();
            AssertBool(updated.DeriveDamageMod).IsFalse();
            AssertThat(updated.AttackBonus).IsEqual(2);
            AssertThat(updated.DamageBonus).IsEqual(1);
        }

        [TestCase]
        public void DeleteWeapon_RemovesRow()
        {
            int id = _repo.AddWeapon(new DnD5ePlayerCharacterWeapon { PlayerCharacterId = PcId, Name = "Axe" });
            _repo.DeleteWeapon(id);
            AssertThat(_repo.GetWeapons(PcId).Count).IsEqual(0);
        }

        [TestCase]
        public void ReorderWeapons_UpdatesSortOrder()
        {
            int id1 = _repo.AddWeapon(new DnD5ePlayerCharacterWeapon { PlayerCharacterId = PcId, Name = "A", SortOrder = 0 });
            int id2 = _repo.AddWeapon(new DnD5ePlayerCharacterWeapon { PlayerCharacterId = PcId, Name = "B", SortOrder = 1 });

            _repo.ReorderWeapons(PcId, new System.Collections.Generic.List<int> { id2, id1 });

            var list = _repo.GetWeapons(PcId);
            AssertThat(list[0].Name).IsEqual("B");
            AssertThat(list[1].Name).IsEqual("A");
        }

        [TestCase]
        public void Migrate_Idempotent()
        {
            _repo.Migrate();
            AssertThat(_repo.GetWeapons(PcId).Count).IsEqual(0);
        }
    }
}
