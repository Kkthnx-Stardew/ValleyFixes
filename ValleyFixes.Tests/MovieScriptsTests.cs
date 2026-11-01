using System.Text.Json;
using ValleyFixes.Fixes;
using Xunit.Abstractions;

namespace ValleyFixes.Tests
{
    public class MovieScriptsTests
    {
        private const string SamBad = "/playSound reward/emote Sam 32/message Sam \"[LocalizedText Strings\\MovieReactions:Sam_summer_movie_1_DuringMovie_2]\"";
        private const string SamGood = "/playSound reward/emote Sam 32/message \"[LocalizedText Strings\\MovieReactions:Sam_summer_movie_1_DuringMovie_2]\"";

        private readonly ITestOutputHelper _output;

        public MovieScriptsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Fixes_a_known_reaction_and_keeps_the_rest_of_the_script()
        {
            Assert.True(MovieScripts.TryFix("Sam", "summer_movie_1", SamBad, out string fixedScript));
            Assert.Equal(SamGood, fixedScript);
        }

        [Fact]
        public void Is_idempotent()
        {
            Assert.False(MovieScripts.TryFix("Sam", "summer_movie_1", SamGood, out string fixedScript));
            Assert.Equal(SamGood, fixedScript);
        }

        [Fact]
        public void Ignores_a_reaction_that_is_not_on_the_list()
        {
            Assert.False(MovieScripts.TryFix("Sam", "love", SamBad, out _));
            Assert.False(MovieScripts.TryFix("Abigail", "summer_movie_1", SamBad, out _));
        }

        [Fact]
        public void Ignores_a_script_with_two_matches()
        {
            string twice = SamBad + SamBad;
            Assert.False(MovieScripts.TryFix("Sam", "summer_movie_1", twice, out string fixedScript));
            Assert.Equal(twice, fixedScript);
        }

        [Fact]
        public void Handles_null_and_empty()
        {
            Assert.False(MovieScripts.TryFix("Sam", "dislike", null, out string a));
            Assert.False(MovieScripts.TryFix("Sam", "dislike", "", out string b));
            Assert.Equal("", a);
            Assert.Equal("", b);
        }

        // Runs the rule over the installed game's own reactions. This checks the data, not the running game.
        [Fact]
        public void Installed_data_changes_exactly_three_scripts()
        {
            string? dir = GameContent.Find("Data");
            if (dir is null)
            {
                _output.WriteLine("Game content not found, skipped.");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "MoviesReactions.json")));
            int changed = 0;
            int seen = 0;
            foreach (var character in doc.RootElement.EnumerateArray())
            {
                string npc = character.GetProperty("NPCName").GetString()!;
                foreach (var reaction in character.GetProperty("Reactions").EnumerateArray())
                {
                    string tag = reaction.GetProperty("Tag").GetString()!;
                    if (!reaction.TryGetProperty("SpecialResponses", out var special) || special.ValueKind != JsonValueKind.Object)
                        continue;
                    if (!special.TryGetProperty("DuringMovie", out var during) || during.ValueKind != JsonValueKind.Object)
                        continue;

                    string? script = during.GetProperty("Script").GetString();
                    seen++;
                    if (MovieScripts.TryFix(npc, tag, script, out string fixedScript))
                    {
                        changed++;
                        _output.WriteLine($"{npc} / {tag}: {script}  ->  {fixedScript}");
                        Assert.DoesNotContain($"/message {npc} \"", fixedScript);
                        Assert.Contains("/message \"[LocalizedText", fixedScript);
                    }
                }
            }

            _output.WriteLine($"{seen} DuringMovie scripts read, {changed} changed.");
            Assert.Equal(3, changed);
        }
    }
}
