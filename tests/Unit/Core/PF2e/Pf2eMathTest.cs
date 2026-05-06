using GdUnit4;
using DndBuilder.Core;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Unit.Core.PF2e
{
    [TestSuite]
    public class Pf2eMathTest
    {
        // ── ParseScore ────────────────────────────────────────────────────────

        [TestCase]
        public void ParseScore_ValidInt_ReturnsIt() =>
            AssertThat(Pf2eMath.ParseScore("16")).IsEqual(16);

        [TestCase]
        public void ParseScore_InvalidText_Returns10() =>
            AssertThat(Pf2eMath.ParseScore("xyz")).IsEqual(10);

        [TestCase]
        public void ParseScore_BelowMin_ClampsTo1() =>
            AssertThat(Pf2eMath.ParseScore("0")).IsEqual(1);

        [TestCase]
        public void ParseScore_AboveMax_ClampsTo30() =>
            AssertThat(Pf2eMath.ParseScore("99")).IsEqual(30);

        // ── AbilityMod ────────────────────────────────────────────────────────

        [TestCase]
        public void AbilityMod_10_IsZero() =>
            AssertThat(Pf2eMath.AbilityMod(10)).IsEqual(0);

        [TestCase]
        public void AbilityMod_11_IsZero() =>
            AssertThat(Pf2eMath.AbilityMod(11)).IsEqual(0);

        [TestCase]
        public void AbilityMod_12_IsPlus1() =>
            AssertThat(Pf2eMath.AbilityMod(12)).IsEqual(1);

        [TestCase]
        public void AbilityMod_9_IsMinus1() =>
            AssertThat(Pf2eMath.AbilityMod(9)).IsEqual(-1);

        [TestCase]
        public void AbilityMod_18_IsPlus4() =>
            AssertThat(Pf2eMath.AbilityMod(18)).IsEqual(4);

        // ── ProfBonus ─────────────────────────────────────────────────────────
        // Pf2e formula: rank 0 = 0; otherwise level + rank*2

        [TestCase]
        public void ProfBonus_Untrained_IsZero() =>
            AssertThat(Pf2eMath.ProfBonus(rankValue: 0, level: 10)).IsEqual(0);

        [TestCase]
        public void ProfBonus_Trained_Rank1_Level5_Is7() =>
            AssertThat(Pf2eMath.ProfBonus(rankValue: 1, level: 5)).IsEqual(7); // 5 + 1*2

        [TestCase]
        public void ProfBonus_Expert_Rank2_Level5_Is9() =>
            AssertThat(Pf2eMath.ProfBonus(rankValue: 2, level: 5)).IsEqual(9); // 5 + 2*2

        [TestCase]
        public void ProfBonus_Master_Rank3_Level10_Is16() =>
            AssertThat(Pf2eMath.ProfBonus(rankValue: 3, level: 10)).IsEqual(16); // 10 + 3*2

        [TestCase]
        public void ProfBonus_Legendary_Rank4_Level10_Is18() =>
            AssertThat(Pf2eMath.ProfBonus(rankValue: 4, level: 10)).IsEqual(18); // 10 + 4*2

        // ── SignStr ───────────────────────────────────────────────────────────

        [TestCase]
        public void SignStr_Positive_HasPlus() =>
            AssertThat(Pf2eMath.SignStr(4)).IsEqual("+4");

        [TestCase]
        public void SignStr_Zero_HasPlus() =>
            AssertThat(Pf2eMath.SignStr(0)).IsEqual("+0");

        [TestCase]
        public void SignStr_Negative_HasMinus() =>
            AssertThat(Pf2eMath.SignStr(-3)).IsEqual("-3");

        // ── ModStr ────────────────────────────────────────────────────────────

        [TestCase]
        public void ModStr_Score14_IsPlus2() =>
            AssertThat(Pf2eMath.ModStr(14)).IsEqual("+2");

        [TestCase]
        public void ModStr_Score8_IsMinus1() =>
            AssertThat(Pf2eMath.ModStr(8)).IsEqual("-1");
    }
}
