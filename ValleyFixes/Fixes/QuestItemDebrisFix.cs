using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-044: when a quest ends the game removes its items from the world overnight (Qi Beans, Calico Eggs and so on).
    /// The sweep of a location's ground drops only looks at debris that carries an item object, but drops made from an item id
    /// (a broken crate, a felled tree, a dropped bean) carry only the id. Those survive and can be picked up the next day.
    /// This runs after the game's own sweep of each location and removes the drops that carry only the matching id.
    /// </summary>
    internal static class QuestItemDebrisFix
    {
        private static IMonitor _monitor = null!; // set in Apply

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var target = AccessTools.Method(typeof(Game1), "_RecursiveRemoveThisNormalItemLocation", new[] { typeof(GameLocation), typeof(string) });
            if (target is null)
            {
                monitor.Log("Quest item ground drop fix is off: Game1._RecursiveRemoveThisNormalItemLocation was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(QuestItemDebrisFix), nameof(Postfix)));
        }

        private static void Postfix(GameLocation l, string itemId)
        {
            try
            {
                if (l?.debris is null || string.IsNullOrEmpty(itemId))
                    return;

                string qualified = ItemRegistry.QualifyItemId(itemId) ?? itemId;
                l.debris.RemoveWhere(d => d.item is null && d.itemId.Value == qualified);
            }
            catch (Exception ex)
            {
                // this only tidies leftovers, so a failure must never break the overnight sweep
                _monitor.Log($"Quest item ground drop cleanup failed. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
