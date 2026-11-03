using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-060: a farmhand keeps seeing the SquidFest booth and sign after the festival is over. The game removes a festival's map
    /// changes only when the location's own list of applied changes (<c>_appliedMapOverrides</c>) says they were applied, or when a
    /// booth tile it looks for is on a tilesheet called "16". The changes put that tile on a tilesheet called
    /// "zzzzz_Beach_SquidFest_16", so the second check never matches. A farmhand's Beach is rebuilt overnight with an empty list,
    /// around a map object the game shares and has already changed, so neither check passes and the booth stays.
    /// Here, before the game decides what to apply or remove, the booth tile is looked up on the right tilesheet. If it is there,
    /// the change is added to the list and the game's own code takes it away. Nothing else is touched.
    /// </summary>
    internal static class StaleMapOverrideFix
    {
        // location, the override's key, a tile that only that override puts there, its layer, the original tilesheet id, the tile index
        private static readonly (string Location, string Key, int X, int Y, string Layer, string TileSheetKey, int Index)[] Known =
        {
            ("Beach", "Beach_SquidFest", 11, 7, "Buildings", "16", 45)
        };

        private static IMonitor _monitor = null!; // set in Apply
        private static readonly HashSet<string> Logged = new();

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var target = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.MakeMapModifications), new[] { typeof(bool) });
            var set = AccessTools.Field(typeof(GameLocation), "_appliedMapOverrides");
            var sheetId = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.GetAddedMapOverrideTilesheetId));
            if (target is null || set is null || sheetId is null)
            {
                monitor.Log("Stale map change fix is off: GameLocation.MakeMapModifications, its applied changes set or the added tilesheet name was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(StaleMapOverrideFix), nameof(Before)));
        }

        private static void Before(GameLocation __instance, bool force, HashSet<string> ____appliedMapOverrides)
        {
            try
            {
                if (force || ____appliedMapOverrides is null)
                    return;

                foreach (var known in Known)
                {
                    if (__instance.NameOrUniqueName != known.Location || ____appliedMapOverrides.Contains(known.Key))
                        continue;

                    string sheet = GameLocation.GetAddedMapOverrideTilesheetId(known.Key, known.TileSheetKey);
                    if (__instance.getTileIndexAt(known.X, known.Y, known.Layer, sheet) != known.Index)
                        continue;

                    ____appliedMapOverrides.Add(known.Key);
                    if (Logged.Add(known.Key))
                        _monitor.Log($"{known.Location} still shows {known.Key} although its list of applied map changes did not have it, so it was added and the game will take it away if the festival is over.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Stale map change check failed, so the game's own rule applies. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
