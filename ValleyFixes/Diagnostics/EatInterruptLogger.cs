using System.Diagnostics;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Diagnostics
{
    /// <summary>
    /// VF-004: names whatever cuts the eating animation short. eatObject sets a 20 second freeze that only the end of the
    /// animation clears, and completelyStopAnimatingOrDoingAction clears the eating flag without clearing the freeze.
    /// This only writes to the log. It never changes what the game does.
    /// </summary>
    internal static class EatInterruptLogger
    {
        private const int MaxEntries = 10;
        private const int MaxFrames = 10;

        private static IMonitor _monitor = null!; // set in Apply
        private static int _logged;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var target = AccessTools.Method(typeof(Farmer), nameof(Farmer.completelyStopAnimatingOrDoingAction));
            if (target is null)
            {
                monitor.Log("Eat interrupt logging is off: Farmer.completelyStopAnimatingOrDoingAction was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(EatInterruptLogger), nameof(Prefix)));
        }

        private static void Prefix(Farmer __instance)
        {
            if (_logged >= MaxEntries || !__instance.isEating || !__instance.IsLocalPlayer)
                return;

            _logged++;
            // the first frames are Harmony's own wrapper, which says nothing about who called
            var callers = new StackTrace(1, false).GetFrames().Where(f => f.GetMethod()?.Name.Contains("_PatchedBy") != true);
            string frames = string.Join(" <- ", callers.Take(MaxFrames).Select(Describe));
            _monitor.Log($"Eating was cut short (freezePause {__instance.freezePause}, time {Game1.timeOfDay}, in {__instance.currentLocation?.NameOrUniqueName}). Called from: {frames}", LogLevel.Warn);
        }

        private static string Describe(StackFrame frame)
        {
            var method = frame.GetMethod();
            return method is null ? "?" : $"{method.DeclaringType?.Name}.{method.Name}";
        }
    }
}
