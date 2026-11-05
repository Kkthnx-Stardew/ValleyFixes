using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-071: the wedding script writes the spouse into the event with two text tokens (<c>SpouseFarmerText</c> and
    /// <c>SpouseGenderedText</c>) that ask the game for the farmer's spouse. The game queues a wedding for a farmer who is engaged, but
    /// <c>Farmer.getSpouse</c> only answers for a farmer who is married, so on the day of the wedding both tokens fail with "the target
    /// player isn't married". Lewis's matrimonial speech comes out as the fragment "[SpouseGenderedText", the spouse's position text is
    /// left as an unresolved token, and the game logs an error for each. The game's developers list "Lewis skipping his matrimonial
    /// dialogue at the farmer's wedding" as fixed in 1.6.16.
    /// While the wedding event is being built, and only then, <c>getSpouse</c> also answers with the engaged spouse. Nothing else in the
    /// game sees a different answer. A farmhand's wedding to another player does not use this path.
    /// </summary>
    internal static class WeddingSpouseFix
    {
        [ThreadStatic]
        private static bool _building;

        private static IMonitor _monitor = null!; // set in Apply
        private static long _answered;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var build = AccessTools.Method(typeof(Utility), nameof(Utility.getWeddingEvent), new[] { typeof(Farmer) });
            var spouse = AccessTools.Method(typeof(Farmer), nameof(Farmer.getSpouse));
            if (build is null || spouse is null)
            {
                monitor.Log("Wedding spouse fix is off: Utility.getWeddingEvent or Farmer.getSpouse was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(build).Concat(HarmonyOwners.Foreign(spouse)).Distinct().ToList();
            if (others.Count > 0)
            {
                monitor.Log($"Wedding spouse fix is off: {string.Join(", ", others)} already patches Utility.getWeddingEvent or Farmer.getSpouse, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(build, prefix: new HarmonyMethod(typeof(WeddingSpouseFix), nameof(BeforeBuild)), finalizer: new HarmonyMethod(typeof(WeddingSpouseFix), nameof(AfterBuild)));
            harmony.Patch(spouse, postfix: new HarmonyMethod(typeof(WeddingSpouseFix), nameof(AfterGetSpouse)));
        }

        private static void BeforeBuild() => _building = true;

        private static void AfterBuild() => _building = false;

        private static void AfterGetSpouse(Farmer __instance, ref NPC __result)
        {
            if (!_building || __result is not null || __instance.spouse is null)
                return;

            try
            {
                if (!__instance.isEngaged())
                    return;

                __result = Game1.getCharacterFromName(__instance.spouse);
                if (__result is not null && _answered++ == 0)
                    _monitor.Log($"The wedding script asked for the spouse of an engaged farmer, so {__instance.spouse} was given to it. Lewis's speech and the spouse's place in the wedding use it.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Wedding spouse fix failed, so the wedding is built as the game builds it. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
