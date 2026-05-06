using GdUnit4;
using DndBuilder.Core;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.DnD5e
{
    [TestSuite]
    public class DnD5eUsesFormulaTest
    {
        // ── Evaluate ──────────────────────────────────────────────────────────

        [TestCase]
        public void Evaluate_Empty_ReturnsZero() =>
            AssertThat(DnD5eUsesFormula.Evaluate("", 5, 10, 10, 10, 10, 10, 10)).IsEqual(0);

        [TestCase]
        public void Evaluate_DoubleDash_ReturnsZero() =>
            AssertThat(DnD5eUsesFormula.Evaluate("--", 5, 10, 10, 10, 10, 10, 10)).IsEqual(0);

        [TestCase]
        public void Evaluate_FlatInt_ReturnsThatInt() =>
            AssertThat(DnD5eUsesFormula.Evaluate("5", 1, 10, 10, 10, 10, 10, 10)).IsEqual(5);

        [TestCase]
        public void Evaluate_BaseOnly_ReturnsBase() =>
            AssertThat(DnD5eUsesFormula.Evaluate("3|||", 5, 10, 10, 10, 10, 10, 10)).IsEqual(3);

        [TestCase]
        public void Evaluate_FullLevel_AddsLevel()
        {
            int result = DnD5eUsesFormula.Evaluate("2|full||", 3, 10, 10, 10, 10, 10, 10);
            AssertThat(result).IsEqual(5);
        }

        [TestCase]
        public void Evaluate_HalfLevel_FloorDivides()
        {
            int result = DnD5eUsesFormula.Evaluate("0|half||", 5, 10, 10, 10, 10, 10, 10);
            AssertThat(result).IsEqual(2);
        }

        [TestCase]
        public void Evaluate_CeilLevel_CeilDivides()
        {
            int result = DnD5eUsesFormula.Evaluate("0|ceil||", 5, 10, 10, 10, 10, 10, 10);
            AssertThat(result).IsEqual(3);
        }

        [TestCase]
        public void Evaluate_DoubleLevel_DoublesLevel()
        {
            int result = DnD5eUsesFormula.Evaluate("0|double||", 3, 10, 10, 10, 10, 10, 10);
            AssertThat(result).IsEqual(6);
        }

        [TestCase]
        public void Evaluate_ProfBonus_AddsProfAtLevel5()
        {
            int result = DnD5eUsesFormula.Evaluate("0||prof|", 5, 10, 10, 10, 10, 10, 10);
            AssertThat(result).IsEqual(3);
        }

        [TestCase]
        public void Evaluate_WisMod_AddsWisdomMod()
        {
            int result = DnD5eUsesFormula.Evaluate("0|||wis", 1, 10, 10, 10, 10, 14, 10);
            AssertThat(result).IsEqual(2);
        }

        [TestCase]
        public void Evaluate_AllComponents_SumsCorrectly()
        {
            int result = DnD5eUsesFormula.Evaluate("1|full|prof|wis", 4, 10, 10, 10, 10, 14, 10);
            AssertThat(result).IsEqual(9);
        }

        [TestCase]
        public void Evaluate_NeverReturnsNegative()
        {
            int result = DnD5eUsesFormula.Evaluate("0|||cha", 1, 10, 10, 10, 10, 10, 1);
            AssertThat(result).IsEqual(0);
        }

        [TestCase]
        public void Evaluate_MalformedFormula_ReturnsZero() =>
            AssertThat(DnD5eUsesFormula.Evaluate("not|valid", 5, 10, 10, 10, 10, 10, 10)).IsEqual(0);

        // ── FormatForDisplay ──────────────────────────────────────────────────

        [TestCase]
        public void Format_Empty_ReturnsDash() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("")).IsEqual("--");

        [TestCase]
        public void Format_DoubleDash_ReturnsDash() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("--")).IsEqual("--");

        [TestCase]
        public void Format_FlatInt_ReturnsThatInt() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("7")).IsEqual("7");

        [TestCase]
        public void Format_BaseOnly_ReturnsBase() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("3|||")).IsEqual("3");

        [TestCase]
        public void Format_FullLevel_ReturnsLvl() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("0|full||")).IsEqual("Lvl");

        [TestCase]
        public void Format_BaseAndProf_RendersCorrectly() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("2||prof|")).IsEqual("2+Prof");

        [TestCase]
        public void Format_AllComponents_RendersCorrectly() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("2|half|prof|wis")).IsEqual("2+½Lvl↓+Prof+WisMod");

        [TestCase]
        public void Format_ZeroBaseNoOther_ReturnsZero() =>
            AssertThat(DnD5eUsesFormula.FormatForDisplay("0|||")).IsEqual("0");
    }
}
