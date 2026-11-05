using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-067: with separate wallets, each player has their own part of a Mini-Shipping Bin. When the day ends the host's part is cleaned of
    /// empty slots (<c>Chest.clearNulls</c> only cleans the calling player's part), and then every player's items are handed on with
    /// <c>onDetachedFromParent</c>. An empty slot in a farmhand's part is null, so the host throws a NullReferenceException and the new
    /// day never starts. The game's developers confirmed this on the forum and said 1.6.16 will clean every player's part.
    /// Here, while a new day is being processed, the same call also cleans every part of a Mini-Shipping Bin.
    /// </summary>
    internal static class MiniShippingBinFix
    {
        private static IMonitor _monitor = null!; // set in Apply
        private static long _cleaned;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var target = AccessTools.Method(typeof(Chest), nameof(Chest.clearNulls));
            if (target is null)
            {
                monitor.Log("Mini-Shipping Bin fix is off: Chest.clearNulls was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MiniShippingBinFix), nameof(After)));
        }

        private static void After(Chest __instance)
        {
            try
            {
                // only while the day is being ended. At other times a farmhand may have their part of the bin open
                if (__instance.SpecialChestType != Chest.SpecialChestTypes.MiniShippingBin || !Game1.newDaySync.hasInstance())
                    return;

                foreach (Inventory part in __instance.separateWalletItems.Values)
                {
                    int before = part.Count;
                    part.RemoveEmptySlots();
                    if (part.Count != before && _cleaned++ == 0)
                        _monitor.Log("A Mini-Shipping Bin held empty slots in a farmhand's part. They were removed before the day ended, which stops the host from crashing.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Mini-Shipping Bin cleanup failed, so the game's own code runs. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
