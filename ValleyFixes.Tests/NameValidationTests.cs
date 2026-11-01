using ValleyFixes.Fixes;

namespace ValleyFixes.Tests
{
    public class NameValidationTests
    {
        [Theory]
        [InlineData(true, true, "Sam", "Farm", true)]
        [InlineData(true, true, " ", "Farm", false)]
        [InlineData(true, true, "Sam", " ", false)]
        [InlineData(true, true, "Sam", "   ", false)]
        [InlineData(true, true, "", "Farm", false)]
        [InlineData(true, true, " Sam ", " Farm ", true)]
        [InlineData(true, false, "Sam", " ", true)]
        [InlineData(true, false, " ", " ", false)]
        [InlineData(false, true, "Sam", "Farm", false)]
        [InlineData(false, false, "Sam", "Farm", false)]
        [InlineData(true, true, null, "Farm", false)]
        [InlineData(true, true, "Sam", null, false)]
        [InlineData(true, false, "Sam", null, true)]
        public void Allows_only_when_the_game_does_and_the_names_are_not_blank(bool gameAllows, bool checkFarm, string? name, string? farm, bool expected)
        {
            Assert.Equal(expected, NameValidation.Allows(gameAllows, checkFarm, name, farm));
        }
    }
}
