using StardewValley;
using StardewValley.Internal;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Pierre's shop scenario. Builds the real seed shop stock for each season, without and with his missing stock list, and records every
    /// wheat, corn and sunflower seed entry with its price, plus a hash of the whole stock so a normal shop can be compared.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> PierreScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["pierre"] = info;
            yield return 120;

            var shop = DataLoader.Shops(Game1.content)["SeedShop"];
            // random sale items are seeded by the farm's id, which is new every run, so it is pinned to compare runs
            var savedId = Game1.uniqueIDForThisGame;
            Game1.uniqueIDForThisGame = 987654321UL;
            var savedSeason = Game1.season;
            var rows = new List<object>();
            info["rows"] = rows;
            foreach (bool stockList in new[] { false, true })
            {
                if (stockList)
                    Game1.player.mailReceived.Add("PierreStocklist");
                else
                    Game1.player.mailReceived.Remove("PierreStocklist");

                foreach (var season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
                {
                    Game1.season = season;
                    var stock = ShopBuilder.GetShopStock("SeedShop", shop);
                    ulong hash = 14695981039346656037UL;
                    foreach (var pair in stock.OrderBy(p => p.Key.QualifiedItemId).ThenBy(p => p.Value.Price))
                    {
                        foreach (char c in $"{pair.Key.QualifiedItemId}:{pair.Value.Price}:{pair.Value.SyncedKey};")
                        {
                            hash ^= c;
                            hash *= 1099511628211UL;
                        }
                    }

                    var crops = stock.Where(p => p.Key.QualifiedItemId is "(O)483" or "(O)487" or "(O)431")
                        .OrderBy(p => p.Key.QualifiedItemId).ThenBy(p => p.Value.Price)
                        .Select(p => $"{p.Key.QualifiedItemId} {p.Value.SyncedKey} {p.Value.Price}g").ToList();
                    rows.Add(new { stockList, season = season.ToString(), entries = stock.Count, hash = hash.ToString("x16"), crops });
                }
            }

            Game1.player.mailReceived.Remove("PierreStocklist");
            Game1.season = savedSeason;
            Game1.uniqueIDForThisGame = savedId;
        }
    }
}
