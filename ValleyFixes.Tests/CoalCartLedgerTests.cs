using ValleyFixes.Fixes;

namespace ValleyFixes.Tests
{
    public class CoalCartLedgerTests
    {
        [Fact]
        public void Marking_a_floor_twice_reports_it_only_once()
        {
            var ledger = new CoalCartLedger();
            Assert.True(ledger.Mark(12));
            Assert.False(ledger.Mark(12));
            Assert.True(ledger.Contains(12));
        }

        [Fact]
        public void Replace_swaps_the_whole_list_and_the_year()
        {
            var ledger = new CoalCartLedger();
            ledger.Mark(3);
            ledger.Replace(2, new[] { 12, 7 });
            Assert.Equal(new[] { 7, 12 }, ledger.Levels);
            Assert.Equal(2, ledger.Year);
            Assert.False(ledger.Contains(3));
        }

        [Fact]
        public void A_new_year_clears_floors_that_are_not_multiples_of_five()
        {
            var ledger = new CoalCartLedger();
            ledger.Replace(1, new[] { 5, 12, 40, 41, 120, 121, 130 });
            Assert.True(ledger.RollOverTo(2));
            Assert.Equal(new[] { 5, 40, 120 }, ledger.Levels);
        }

        [Fact]
        public void The_same_year_changes_nothing()
        {
            var ledger = new CoalCartLedger();
            ledger.Replace(2, new[] { 12 });
            Assert.False(ledger.RollOverTo(2));
            Assert.Equal(new[] { 12 }, ledger.Levels);
        }
    }
}
