using ValleyFixes.Fixes;

namespace ValleyFixes.Tests
{
    public class SyncDelayLimitsTests
    {
        [Theory]
        [InlineData(15, 15)]
        [InlineData(8, 8)]
        [InlineData(2, 2)]
        [InlineData(1, 15)]
        [InlineData(0, 15)]
        [InlineData(-4, 15)]
        [InlineData(16, 15)]
        [InlineData(600, 15)]
        public void Out_of_range_settings_fall_back_to_the_games_own_value(int setting, int expected)
        {
            Assert.Equal(expected, SyncDelayLimits.Clamp(setting));
        }
    }
}
