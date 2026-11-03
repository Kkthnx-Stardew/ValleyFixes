using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-059: the stardrop's energy bonus is applied only when the eating animation ends (<c>Farmer.doneEating</c>). The stardrop
    /// is already used up by then. If anything calls <c>completelyStopAnimatingOrDoingAction</c> in between, the animation never
    /// ends and the bonus is lost for good. Here, when that call cuts a stardrop short, the finish is run right away.
    /// Other food is left alone.
    /// </summary>
    internal static class StardropInterruptFix
    {
        private const string StardropId = "(O)434";

        private static IMonitor _monitor = null!; // set in Apply
        private static bool _finishing;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var stop = AccessTools.Method(typeof(Farmer), nameof(Farmer.completelyStopAnimatingOrDoingAction));
            var done = AccessTools.Method(typeof(Farmer), nameof(Farmer.doneEating));
            if (stop is null || done is null)
            {
                monitor.Log("Stardrop interrupt fix is off: Farmer.completelyStopAnimatingOrDoingAction or Farmer.doneEating was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(stop, prefix: new HarmonyMethod(typeof(StardropInterruptFix), nameof(Before)), postfix: new HarmonyMethod(typeof(StardropInterruptFix), nameof(After)));
        }

        private static void Before(Farmer __instance, out bool __state)
        {
            __state = !_finishing && __instance.IsLocalPlayer && __instance.isEating && __instance.itemToEat?.QualifiedItemId == StardropId;
        }

        private static void After(Farmer __instance, bool __state)
        {
            if (!__state)
                return;

            try
            {
                _finishing = true;
                float before = __instance.maxStamina.Value;
                __instance.mostRecentlyGrabbedItem ??= __instance.itemToEat;
                __instance.doneEating();
                _monitor.Log($"A stardrop was cut short while eating, so its bonus was applied now. Maximum energy {before} to {__instance.maxStamina.Value}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not finish a stardrop that was cut short. {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _finishing = false;
            }
        }
    }
}
