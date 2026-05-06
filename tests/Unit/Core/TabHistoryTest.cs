using GdUnit4;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Unit.Core
{
    [TestSuite]
    public class TabHistoryTest
    {
        [TestCase]
        public void NewInstance_HasNoCurrent()
        {
            var h = new TabHistory();
            AssertThat(h.HasCurrent).IsFalse();
            AssertThat(h.CanGoBack).IsFalse();
            AssertThat(h.CanGoForward).IsFalse();
        }

        [TestCase]
        public void Push_FirstEntry_HasCurrentNoBack()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            AssertThat(h.HasCurrent).IsTrue();
            AssertThat(h.CanGoBack).IsFalse();
            AssertThat(h.CanGoForward).IsFalse();
            AssertThat(h.Current).IsEqual(("npc", 1));
        }

        [TestCase]
        public void Push_TwoEntries_CanGoBack()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("faction", 2);
            AssertThat(h.CanGoBack).IsTrue();
            AssertThat(h.CanGoForward).IsFalse();
            AssertThat(h.Current).IsEqual(("faction", 2));
        }

        [TestCase]
        public void Back_ReturnsFirstEntry()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("faction", 2);
            var result = h.Back();
            AssertThat(result).IsEqual(("npc", 1));
            AssertThat(h.CanGoForward).IsTrue();
        }

        [TestCase]
        public void Forward_AfterBack_ReturnsSecondEntry()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("faction", 2);
            h.Back();
            var result = h.Forward();
            AssertThat(result).IsEqual(("faction", 2));
        }

        [TestCase]
        public void Push_SameAsCurrent_NoDuplicate()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("npc", 1);
            AssertThat(h.CanGoBack).IsFalse();
        }

        [TestCase]
        public void Push_AfterBack_ClearsForwardHistory()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("faction", 2);
            h.Back();
            h.Push("location", 3);
            AssertThat(h.CanGoForward).IsFalse();
            AssertThat(h.Current).IsEqual(("location", 3));
        }

        [TestCase]
        public void Back_AtBeginning_StaysAtFirst()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Back(); // can't go back, stays at index 0
            AssertThat(h.Current).IsEqual(("npc", 1));
        }

        [TestCase]
        public void Forward_AtEnd_StaysAtLast()
        {
            var h = new TabHistory();
            h.Push("npc", 1);
            h.Push("faction", 2);
            h.Forward(); // can't go forward, stays at last
            AssertThat(h.Current).IsEqual(("faction", 2));
        }
    }
}
