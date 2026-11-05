using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-068: a fruit tree makes better fruit as it ages (silver after 112 days, gold after 224, iridium after 336). In 1.5 the fruit
    /// still hanging on the tree improved with it. Since 1.6 each fruit is an item that got its quality on the day it grew, and shaking
    /// or chopping the tree passes the tree's current quality to the drop, but the item's own quality wins. So fruit grown before a
    /// birthday keeps the old quality. A forum reply from the game's developers called this an unintended change in 1.6 and said 1.6.16 fixes it.
    /// Here, just before the tree is shaken or chopped, the fruit on it is set to the tree's current quality. The tree's own quality rule
    /// is untouched. Off by default because it raises the quality of some fruit.
    /// </summary>
    internal static class FruitTreeQualityFix
    {
        private static IMonitor _monitor = null!; // set in Apply
        private static long _raised;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var shake = AccessTools.Method(typeof(FruitTree), nameof(FruitTree.shake));
            var tool = AccessTools.Method(typeof(FruitTree), nameof(FruitTree.performToolAction));
            if (shake is null || tool is null || AccessTools.Method(typeof(FruitTree), nameof(FruitTree.GetQuality)) is null)
            {
                monitor.Log("Fruit tree quality fix is off: FruitTree.shake, performToolAction or GetQuality was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var prefix = new HarmonyMethod(typeof(FruitTreeQualityFix), nameof(Before));
            harmony.Patch(shake, prefix: prefix);
            harmony.Patch(tool, prefix: prefix);
        }

        private static void Before(FruitTree __instance)
        {
            try
            {
                if (__instance.struckByLightningCountdown.Value > 0)
                    return;

                int quality = __instance.GetQuality();
                foreach (Item? fruit in __instance.fruit)
                {
                    if (fruit is not null && fruit.Quality < quality)
                    {
                        fruit.Quality = quality;
                        if (_raised++ == 0)
                            _monitor.Log("Fruit that grew before its tree got older was raised to the tree's current quality.", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Fruit tree quality fix failed, so the game's own code runs. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
