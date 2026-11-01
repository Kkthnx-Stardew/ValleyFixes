using System.Text.Json;
using ValleyFixes.Fixes;
using Xunit.Abstractions;

namespace ValleyFixes.Tests
{
    public class GenderedSingleStatusTests
    {
        private readonly ITestOutputHelper _output;

        public GenderedSingleStatusTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Joins_male_and_female_in_the_order_the_game_reads_them()
        {
            Assert.True(GenderedSingleStatus.TryFix("(solteira)", "(solteiro)", out string combined));
            Assert.Equal("(solteiro)/(solteira)", combined);
            string[] parts = combined.Split('/');
            Assert.Equal("(solteiro)", parts[0]);
            Assert.Equal("(solteira)", parts[^1]);
        }

        [Theory]
        [InlineData("(single)", "(single)")]
        [InlineData("(solteiro)/(solteira)", "(solteiro)")]
        [InlineData("(solteira)", "(a/b)")]
        [InlineData("(solteira)", "")]
        [InlineData("", "(solteiro)")]
        [InlineData(null, "(solteiro)")]
        [InlineData("(solteira)", null)]
        public void Leaves_other_strings_alone(string? female, string? male)
        {
            Assert.False(GenderedSingleStatus.TryFix(female, male, out string combined));
            Assert.Equal(female ?? "", combined);
        }

        // Runs the rule over the installed game's own strings. This checks the data, not the running game.
        [Fact]
        public void Installed_portuguese_strings_change_and_english_does_not()
        {
            string? dir = GameContent.Find("Strings");
            if (dir is null)
            {
                _output.WriteLine("Game content not found, skipped.");
                return;
            }

            var pt = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir, "StringsFromCSFiles.pt-BR.json")))!;
            Assert.True(GenderedSingleStatus.TryFix(pt[GenderedSingleStatus.FemaleKey], pt[GenderedSingleStatus.MaleKey], out string combined));
            _output.WriteLine(combined);

            var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir, "StringsFromCSFiles.json")))!;
            Assert.False(GenderedSingleStatus.TryFix(en[GenderedSingleStatus.FemaleKey], en[GenderedSingleStatus.MaleKey], out _));
        }
    }
}
