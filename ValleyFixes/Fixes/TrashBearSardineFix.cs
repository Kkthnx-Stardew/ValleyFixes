using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-022: stops Trash Bear from asking for a Sardine in summer. The pick is redone with the next seed, so the result is
    /// stable for a given save and every other pick is untouched.
    /// </summary>
    internal static class TrashBearSardineFix
    {
        private const int MaxRerolls = 8;

        [ThreadStatic]
        private static bool _rerolling;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            var target = AccessTools.Method(typeof(Utility), nameof(Utility.getRandomPureSeasonalItem), new[] { typeof(Season), typeof(int) });
            if (target is null)
            {
                monitor.Log("Trash Bear Sardine fix is off: Utility.getRandomPureSeasonalItem was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(TrashBearSardineFix), nameof(Postfix)));
        }

        private static void Postfix(Season season, int randomSeedAddition, ref string __result)
        {
            if (_rerolling || !SeasonalPick.NeedsReroll(season == Season.Summer, __result))
                return;

            _rerolling = true;
            try
            {
                for (int i = 1; i <= MaxRerolls; i++)
                {
                    string next = Utility.getRandomPureSeasonalItem(season, randomSeedAddition + i);
                    if (!SeasonalPick.NeedsReroll(true, next))
                    {
                        __result = next;
                        return;
                    }
                }
            }
            finally
            {
                _rerolling = false;
            }
        }
    }
}
