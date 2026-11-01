using System.Text.Json;
using ValleyFixes.Fixes;
using Xunit.Abstractions;

namespace ValleyFixes.Tests
{
    public class WildTreeHardwoodTests
    {
        private readonly ITestOutputHelper _output;

        public WildTreeHardwoodTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Changes_a_standing_tree_hardwood_entry()
        {
            Assert.True(WildTreeHardwood.TryFix("(O)709", forStump: false, out string fixedId));
            Assert.Equal("709", fixedId);
        }

        [Theory]
        [InlineData("(O)709", true)]
        [InlineData("709", false)]
        [InlineData("(O)92", false)]
        [InlineData("(O)420", false)]
        [InlineData("", false)]
        public void Leaves_everything_else_alone(string id, bool forStump)
        {
            Assert.False(WildTreeHardwood.TryFix(id, forStump, out string fixedId));
            Assert.Equal(id, fixedId);
        }

        [Fact]
        public void Handles_null()
        {
            Assert.False(WildTreeHardwood.TryFix(null, false, out string fixedId));
            Assert.Equal("", fixedId);
        }

        [Fact]
        public void Is_idempotent()
        {
            WildTreeHardwood.TryFix("(O)709", false, out string once);
            Assert.False(WildTreeHardwood.TryFix(once, false, out _));
        }

        // Runs the rule over the installed game's own tree data. This checks the data, not the running game.
        [Fact]
        public void Installed_data_changes_exactly_two_entries()
        {
            string? dir = GameContent.Find("Data");
            if (dir is null)
            {
                _output.WriteLine("Game content not found, skipped.");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "WildTrees.json")));
            int changed = 0;
            int seen = 0;
            foreach (var tree in doc.RootElement.EnumerateObject())
            {
                if (!tree.Value.TryGetProperty("ChopItems", out var chops) || chops.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var chop in chops.EnumerateArray())
                {
                    seen++;
                    string? id = chop.GetProperty("ItemId").GetString();
                    bool forStump = chop.GetProperty("ForStump").GetBoolean();
                    if (WildTreeHardwood.TryFix(id, forStump, out _))
                    {
                        changed++;
                        _output.WriteLine($"tree type {tree.Name}: {id} -> {WildTreeHardwood.ComparedId}");
                    }
                }
            }

            _output.WriteLine($"{seen} ChopItems entries read, {changed} changed.");
            Assert.Equal(2, changed);
        }
    }
}
