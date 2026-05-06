using GdUnit4;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.DnD5e
{
    [TestSuite]
    public class DnD5eMathTest
    {
        // ── ParseScore ────────────────────────────────────────────────────────

        [TestCase]
        public void ParseScore_ValidInt_ReturnsIt() =>
            AssertThat(DnD5eMath.ParseScore("14")).IsEqual(14);

        [TestCase]
        public void ParseScore_InvalidText_Returns10() =>
            AssertThat(DnD5eMath.ParseScore("abc")).IsEqual(10);

        [TestCase]
        public void ParseScore_BelowMin_ClampsTo1() =>
            AssertThat(DnD5eMath.ParseScore("0")).IsEqual(1);

        [TestCase]
        public void ParseScore_AboveMax_ClampsTo30() =>
            AssertThat(DnD5eMath.ParseScore("31")).IsEqual(30);

        // ── AbilityMod ────────────────────────────────────────────────────────

        [TestCase]
        public void AbilityMod_10_IsZero() =>
            AssertThat(DnD5eMath.AbilityMod(10)).IsEqual(0);

        [TestCase]
        public void AbilityMod_11_IsZero() =>
            AssertThat(DnD5eMath.AbilityMod(11)).IsEqual(0);

        [TestCase]
        public void AbilityMod_12_IsPlus1() =>
            AssertThat(DnD5eMath.AbilityMod(12)).IsEqual(1);

        [TestCase]
        public void AbilityMod_9_IsMinus1() =>
            AssertThat(DnD5eMath.AbilityMod(9)).IsEqual(-1);

        [TestCase]
        public void AbilityMod_8_IsMinus1() =>
            AssertThat(DnD5eMath.AbilityMod(8)).IsEqual(-1);

        [TestCase]
        public void AbilityMod_1_IsMinus5() =>
            AssertThat(DnD5eMath.AbilityMod(1)).IsEqual(-5);

        [TestCase]
        public void AbilityMod_20_IsPlus5() =>
            AssertThat(DnD5eMath.AbilityMod(20)).IsEqual(5);

        // ── ModLabel ─────────────────────────────────────────────────────────

        [TestCase]
        public void ModLabel_Score10_IsPlus0() =>
            AssertThat(DnD5eMath.ModLabel(10)).IsEqual("(+0)");

        [TestCase]
        public void ModLabel_Score16_IsPlus3() =>
            AssertThat(DnD5eMath.ModLabel(16)).IsEqual("(+3)");

        [TestCase]
        public void ModLabel_Score8_IsMinus1() =>
            AssertThat(DnD5eMath.ModLabel(8)).IsEqual("(-1)");

        // ── ProfBonus ─────────────────────────────────────────────────────────

        [TestCase]
        public void ProfBonus_Level1_Is2() =>
            AssertThat(DnD5eMath.ProfBonus(1)).IsEqual(2);

        [TestCase]
        public void ProfBonus_Level4_Is2() =>
            AssertThat(DnD5eMath.ProfBonus(4)).IsEqual(2);

        [TestCase]
        public void ProfBonus_Level5_Is3() =>
            AssertThat(DnD5eMath.ProfBonus(5)).IsEqual(3);

        [TestCase]
        public void ProfBonus_Level9_Is4() =>
            AssertThat(DnD5eMath.ProfBonus(9)).IsEqual(4);

        [TestCase]
        public void ProfBonus_Level20_Is6() =>
            AssertThat(DnD5eMath.ProfBonus(20)).IsEqual(6);

        // ── SkillBonus ────────────────────────────────────────────────────────

        [TestCase]
        public void SkillBonus_Untrained_IsJustAttrMod()
        {
            var pc = new DnD5ePlayerCharacter { Wisdom = 14 };
            int bonus = DnD5eMath.SkillBonus("wis", pc, profBonus: 2, isProficient: false, isExpertise: false);
            AssertThat(bonus).IsEqual(2);
        }

        [TestCase]
        public void SkillBonus_Proficient_AddsProf()
        {
            var pc = new DnD5ePlayerCharacter { Dexterity = 16 };
            int bonus = DnD5eMath.SkillBonus("dex", pc, profBonus: 3, isProficient: true, isExpertise: false);
            AssertThat(bonus).IsEqual(6);
        }

        [TestCase]
        public void SkillBonus_Expertise_DoublesProf()
        {
            var pc = new DnD5ePlayerCharacter { Dexterity = 16 };
            int bonus = DnD5eMath.SkillBonus("dex", pc, profBonus: 3, isProficient: false, isExpertise: true);
            AssertThat(bonus).IsEqual(9);
        }

        [TestCase]
        public void SkillBonus_UnknownAttr_Uses10AsScore()
        {
            var pc = new DnD5ePlayerCharacter();
            int bonus = DnD5eMath.SkillBonus("xyz", pc, profBonus: 2, isProficient: false, isExpertise: false);
            AssertThat(bonus).IsEqual(0);
        }

        // ── SignStr ───────────────────────────────────────────────────────────

        [TestCase]
        public void SignStr_Positive_HasPlus() =>
            AssertThat(DnD5eMath.SignStr(3)).IsEqual("+3");

        [TestCase]
        public void SignStr_Zero_HasPlus() =>
            AssertThat(DnD5eMath.SignStr(0)).IsEqual("+0");

        [TestCase]
        public void SignStr_Negative_HasMinus() =>
            AssertThat(DnD5eMath.SignStr(-2)).IsEqual("-2");

        // ── ScoreFor ─────────────────────────────────────────────────────────

        [TestCase]
        public void ScoreFor_ReturnsCorrectScore()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 18, Dexterity = 14, Constitution = 12,
                                                Intelligence = 10, Wisdom = 8, Charisma = 16 };
            AssertThat(DnD5eMath.ScoreFor(pc, "str")).IsEqual(18);
            AssertThat(DnD5eMath.ScoreFor(pc, "dex")).IsEqual(14);
            AssertThat(DnD5eMath.ScoreFor(pc, "con")).IsEqual(12);
            AssertThat(DnD5eMath.ScoreFor(pc, "int")).IsEqual(10);
            AssertThat(DnD5eMath.ScoreFor(pc, "wis")).IsEqual(8);
            AssertThat(DnD5eMath.ScoreFor(pc, "cha")).IsEqual(16);
        }

        [TestCase]
        public void ScoreFor_Unknown_Returns10() =>
            AssertThat(DnD5eMath.ScoreFor(new DnD5ePlayerCharacter(), "xyz")).IsEqual(10);

        // ── SaveBonus ─────────────────────────────────────────────────────────

        [TestCase]
        public void SaveBonus_NotProficient_IsJustMod()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 16 };
            AssertThat(DnD5eMath.SaveBonus(pc, "str", profBonus: 3)).IsEqual(3);
        }

        [TestCase]
        public void SaveBonus_Proficient_AddsProfBonus()
        {
            var pc = new DnD5ePlayerCharacter { Constitution = 14, SaveProfCon = true };
            AssertThat(DnD5eMath.SaveBonus(pc, "con", profBonus: 3)).IsEqual(5);
        }

        // ── InitiativeBonus ───────────────────────────────────────────────────

        [TestCase]
        public void InitiativeBonus_DexModPlusMisc()
        {
            var pc = new DnD5ePlayerCharacter { Dexterity = 16, InitiativeMisc = 2 };
            AssertThat(DnD5eMath.InitiativeBonus(pc)).IsEqual(5);
        }

        [TestCase]
        public void InitiativeBonus_NoMisc_IsJustDexMod()
        {
            var pc = new DnD5ePlayerCharacter { Dexterity = 12 };
            AssertThat(DnD5eMath.InitiativeBonus(pc)).IsEqual(1);
        }

        // ── WeaponAttackBonus ─────────────────────────────────────────────────

        [TestCase]
        public void WeaponAttackBonus_DeriveStr_ProfAndMod()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 18 };
            var w  = new DnD5ePlayerCharacterWeapon { DeriveFromAbility = true, DeriveAbility = "str", IsProficient = true };
            AssertThat(DnD5eMath.WeaponAttackBonus(pc, w, profBonus: 3)).IsEqual(7);
        }

        [TestCase]
        public void WeaponAttackBonus_NoDerive_OnlyProfAndManual()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 20 };
            var w  = new DnD5ePlayerCharacterWeapon { DeriveFromAbility = false, IsProficient = true, AttackBonus = 1 };
            AssertThat(DnD5eMath.WeaponAttackBonus(pc, w, profBonus: 2)).IsEqual(3);
        }

        [TestCase]
        public void WeaponAttackBonus_Finesse_UsesHigherMod()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 10, Dexterity = 18 };
            var w  = new DnD5ePlayerCharacterWeapon { DeriveFromAbility = true, DeriveAbility = "fin", IsProficient = false };
            AssertThat(DnD5eMath.WeaponAttackBonus(pc, w, profBonus: 2)).IsEqual(4);
        }

        // ── WeaponDamageMod ───────────────────────────────────────────────────

        [TestCase]
        public void WeaponDamageMod_DeriveStrAndMod_ReturnsAttrMod()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 18 };
            var w  = new DnD5ePlayerCharacterWeapon { DeriveFromAbility = true, DeriveDamageMod = true, DeriveAbility = "str" };
            AssertThat(DnD5eMath.WeaponDamageMod(pc, w)).IsEqual(4);
        }

        [TestCase]
        public void WeaponDamageMod_NoDeriveOrNoMod_OnlyManual()
        {
            var pc = new DnD5ePlayerCharacter { Strength = 20 };
            var w  = new DnD5ePlayerCharacterWeapon { DeriveFromAbility = false, DeriveDamageMod = true, DamageBonus = 2 };
            AssertThat(DnD5eMath.WeaponDamageMod(pc, w)).IsEqual(2);
        }
    }
}
