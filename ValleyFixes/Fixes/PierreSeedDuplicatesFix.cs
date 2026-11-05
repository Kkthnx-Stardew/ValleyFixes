using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-069: once a player has given Pierre his missing stock list, the game lets his shop sell out of season crops at 1.5 times the
    /// price by ignoring the season in each entry's condition. Wheat, corn and sunflower seeds each have a Summer entry and a Fall entry,
    /// so both pass and each seed is listed twice, one at the normal price and one at the higher price. The game's developers list
    /// "Pierre's shop selling duplicate corn/sunflower/wheat seeds" as fixed in 1.6.16.
    /// After the stock is built, for those six entries only, the dearest of each pair is dropped. A seed that is in season keeps its normal
    /// price. Out of season it keeps one entry at the higher price. Nothing else in any shop is touched, so shop mods are not affected.
    /// </summary>
    internal static class PierreSeedDuplicatesFix
    {
        private const string ShopId = "SeedShop";
        private const string StockListMail = "PierreStocklist";

        private static readonly HashSet<string> EntryIds = new()
        {
            "WheatSeeds_Summer", "WheatSeeds_Fall", "CornSeeds_Summer", "CornSeeds_Fall", "SunflowerSeeds_Summer", "SunflowerSeeds_Fall"
        };

        private static IMonitor _monitor = null!; // set in Apply
        private static long _removed;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            // the method has overloads, so the two parameter types name the one that builds a shop's stock
            var target = AccessTools.Method(typeof(ShopBuilder), nameof(ShopBuilder.GetShopStock), new[] { typeof(string), typeof(ShopData) });
            if (target is null)
            {
                monitor.Log("Pierre seed duplicates fix is off: ShopBuilder.GetShopStock was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(target);
            if (others.Count > 0)
            {
                monitor.Log($"Pierre seed duplicates fix is off: {string.Join(", ", others)} already patches ShopBuilder.GetShopStock, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PierreSeedDuplicatesFix), nameof(After)));
        }

        private static void After(string shopId, Dictionary<ISalable, ItemStockInformation> __result)
        {
            try
            {
                if (shopId != ShopId || __result is null || !Game1.MasterPlayer.hasOrWillReceiveMail(StockListMail))
                    return;

                var pairs = __result
                    .Where(p => p.Value.SyncedKey is not null && EntryIds.Contains(p.Value.SyncedKey))
                    .GroupBy(p => p.Key.QualifiedItemId);

                foreach (var group in pairs)
                {
                    var listed = group.ToList();
                    if (listed.Count < 2)
                        continue;

                    // the cheapest stays, and the first one stays when prices tie
                    var keep = listed.OrderBy(p => p.Value.Price).First();
                    foreach (var extra in listed.Where(p => !ReferenceEquals(p.Key, keep.Key)))
                    {
                        __result.Remove(extra.Key);
                        if (_removed++ == 0)
                            _monitor.Log("Pierre's shop listed a seed twice because of his stock list, so the dearer copy was dropped.", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Pierre seed duplicates fix failed, so the shop is left as the game built it. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
