using System.Text.Json;
using ValleyFixes.Fixes;
using Xunit.Abstractions;

namespace ValleyFixes.Tests
{
    public class ChefBundleRewardTests
    {
        private readonly ITestOutputHelper _output;

        public ChefBundleRewardTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Fixes_only_the_known_bundle_and_reward()
        {
            Assert.True(ChefBundleReward.TryFix("Bulletin Board", "Chef's", "1 Pink Cake", out string fixedReward));
            Assert.Equal("3 Pink Cake", fixedReward);
        }

        [Theory]
        [InlineData("Bulletin Board", "Chef's", "3 Pink Cake")]
        [InlineData("Bulletin Board", "Chef's", "5 Pink Cake")]
        [InlineData("Bulletin Board", "Dye", "1 Pink Cake")]
        [InlineData("Pantry", "Chef's", "1 Pink Cake")]
        [InlineData(null, "Chef's", "1 Pink Cake")]
        [InlineData("Bulletin Board", null, "1 Pink Cake")]
        public void Leaves_everything_else_alone(string? area, string? bundle, string? reward)
        {
            Assert.False(ChefBundleReward.TryFix(area, bundle, reward, out string fixedReward));
            Assert.Equal(reward ?? "", fixedReward);
        }

        // Runs the rule over the installed game's own bundle data. This checks the data, not the running game.
        [Fact]
        public void Installed_data_changes_exactly_one_reward()
        {
            string? dir = GameContent.Find("Data");
            if (dir is null)
            {
                _output.WriteLine("Game content not found, skipped.");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "RandomBundles.json")));
            int changed = 0;
            foreach (var area in doc.RootElement.EnumerateArray())
            {
                string areaName = area.GetProperty("AreaName").GetString()!;
                var bundles = new List<JsonElement>();
                if (area.TryGetProperty("Bundles", out var direct) && direct.ValueKind == JsonValueKind.Array)
                    bundles.AddRange(direct.EnumerateArray());
                if (area.TryGetProperty("BundleSets", out var sets) && sets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var set in sets.EnumerateArray())
                    {
                        if (set.TryGetProperty("Bundles", out var inner) && inner.ValueKind == JsonValueKind.Array)
                            bundles.AddRange(inner.EnumerateArray());
                    }
                }

                foreach (var bundle in bundles)
                {
                    if (ChefBundleReward.TryFix(areaName, bundle.GetProperty("Name").GetString(), bundle.GetProperty("Reward").GetString(), out _))
                        changed++;
                }
            }

            _output.WriteLine($"{changed} reward changed.");
            Assert.Equal(1, changed);
        }
    }

    public class SeasonalPickTests
    {
        [Theory]
        [InlineData(true, "131", true)]
        [InlineData(true, "130", false)]
        [InlineData(true, "1310", false)]
        [InlineData(true, null, false)]
        [InlineData(false, "131", false)]
        public void Rerolls_only_a_summer_sardine(bool isSummer, string? item, bool expected)
        {
            Assert.Equal(expected, SeasonalPick.NeedsReroll(isSummer, item));
        }
    }
}
