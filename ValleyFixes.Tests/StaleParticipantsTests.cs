using ValleyFixes.Fixes;

namespace ValleyFixes.Tests
{
    public class StaleParticipantsTests
    {
        [Fact]
        public void Keeps_every_known_player()
        {
            var stale = StaleParticipants.Find(new long[] { 1, 2, 3 }, new HashSet<long> { 1, 2, 3, 4 });
            Assert.Empty(stale);
        }

        [Fact]
        public void Finds_only_the_ids_nobody_owns()
        {
            var stale = StaleParticipants.Find(new long[] { 1, 99, 3, -5 }, new HashSet<long> { 1, 3 });
            Assert.Equal(new long[] { 99, -5 }, stale);
        }

        [Fact]
        public void An_empty_list_finds_nothing()
        {
            Assert.Empty(StaleParticipants.Find(Array.Empty<long>(), new HashSet<long>()));
        }

        [Fact]
        public void Nobody_known_means_every_id_is_stale()
        {
            Assert.Equal(2, StaleParticipants.Find(new long[] { 7, 8 }, new HashSet<long>()).Count);
        }
    }
}
