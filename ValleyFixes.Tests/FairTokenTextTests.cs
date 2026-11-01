using System.Text.Json;
using ValleyFixes.Fixes;
using Xunit.Abstractions;

namespace ValleyFixes.Tests
{
    public class FairTokenTextTests
    {
        private readonly ITestOutputHelper _output;

        public FairTokenTextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Swaps_a_single_200()
        {
            Assert.True(FairTokenText.TryFix("Your prize is 200 star tokens!", out string fixedText));
            Assert.Equal("Your prize is 250 star tokens!", fixedText);
        }

        [Theory]
        [InlineData("Your prize is 250 star tokens!")]
        [InlineData("Your prize is 1200 star tokens!")]
        [InlineData("Your prize is 2000 star tokens!")]
        [InlineData("Won 200 or 200 tokens")]
        [InlineData("")]
        public void Leaves_other_text_alone(string text)
        {
            Assert.False(FairTokenText.TryFix(text, out string fixedText));
            Assert.Equal(text, fixedText);
        }

        [Fact]
        public void Handles_null()
        {
            Assert.False(FairTokenText.TryFix(null, out string fixedText));
            Assert.Equal("", fixedText);
        }

        [Fact]
        public void Is_idempotent()
        {
            FairTokenText.TryFix("a 200 b", out string once);
            Assert.False(FairTokenText.TryFix(once, out _));
        }

        // Runs the rule over the installed game's own text for each language. This checks the data, not the running game.
        [Fact]
        public void Installed_locale_files()
        {
            string? dir = GameContent.Find("Strings");
            if (dir is null)
            {
                _output.WriteLine("Game content not found, skipped.");
                return;
            }

            var mustFix = new[] { "", ".de-DE", ".es-ES", ".fr-FR", ".it-IT", ".pt-BR" };
            foreach (string file in Directory.GetFiles(dir, "StringsFromCSFiles*.json"))
            {
                string suffix = Path.GetFileNameWithoutExtension(file).Replace("StringsFromCSFiles", "");
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
                bool fixedIt = FairTokenText.TryFix(data[FairTokenText.Key], out string result);
                _output.WriteLine($"{(suffix == "" ? "en" : suffix.TrimStart('.')),-6} fixed={fixedIt}");
                if (fixedIt)
                    Assert.Single(System.Text.RegularExpressions.Regex.Matches(result, "250"));
                if (mustFix.Contains(suffix))
                    Assert.True(fixedIt, $"locale '{suffix}' was expected to change");
            }
        }
    }
}
