using System.Runtime.CompilerServices;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-062: the game runs the background update of every location every frame, even places nobody is in. In a big mod list that is
    /// hundreds of locations (467 on a real save), most of them holding nothing, and some holding hundreds of monsters from an
    /// expansion mod. Measured on that save, empty locations cost about 1 ms a frame and the monsters about another 1 ms.
    /// Two separate rules, both host or single player only and both only for locations that have had no player for 60 frames in a row
    /// (so events queued by a player who just left are still handled):
    /// <list type="bullet">
    /// <item>A location with no players, no characters, no animals, no buildings, no temporary sprites, no running event and no remote viewer skips
    /// its background update. The game's "was updated" flag is cleared the way the original does.</item>
    /// <item>Monsters in an outdoor location nobody is in are not updated. Villagers, animals and everything else still are. Never done on the
    /// farm, inside buildings, in mines or in the volcano.</item>
    /// </list>
    /// Skipped entirely, with a log line, if another mod already patches the same game method.
    /// </summary>
    internal static class IdleLocationFix
    {
        private const int IdleFramesNeeded = 60;

        private sealed class Counter
        {
            public int IdleFrames;
        }

        private static readonly ConditionalWeakTable<GameLocation, Counter> Counters = new();
        private static IMonitor _monitor = null!; // set in Apply
        private static bool _skipEmpty;
        private static bool _skipMonsters;
        private static long _skippedEmpty;
        private static long _skippedMonsters;

        public static void Apply(Harmony harmony, IMonitor monitor, bool skipEmpty, bool skipMonsters)
        {
            _monitor = monitor;
            _skipEmpty = skipEmpty;
            _skipMonsters = skipMonsters;

            var update = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.updateEvenIfFarmerIsntHere), new[] { typeof(Microsoft.Xna.Framework.GameTime), typeof(bool) });
            var monsterUpdate = AccessTools.Method(typeof(Monster), nameof(Monster.update), new[] { typeof(Microsoft.Xna.Framework.GameTime), typeof(GameLocation) });
            if (update is null || monsterUpdate is null)
            {
                monitor.Log("Idle location updates are off: GameLocation.updateEvenIfFarmerIsntHere or Monster.update was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(update);
            if (others.Count > 0)
            {
                monitor.Log($"Idle location updates are off: {string.Join(", ", others)} already patches GameLocation.updateEvenIfFarmerIsntHere, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(update, prefix: new HarmonyMethod(typeof(IdleLocationFix), nameof(BeforeLocationUpdate)));

            if (skipMonsters)
            {
                var monsterOthers = HarmonyOwners.Foreign(monsterUpdate);
                if (monsterOthers.Count > 0)
                    monitor.Log($"Idle monster updates are off: {string.Join(", ", monsterOthers)} already patches Monster.update.", LogLevel.Info);
                else
                    harmony.Patch(monsterUpdate, prefix: new HarmonyMethod(typeof(IdleLocationFix), nameof(BeforeMonsterUpdate)));
            }
        }

        /// <summary>Counts how many frames in a row a location has been without a player. Called once per location per frame, before the game's own update.</summary>
        private static int Track(GameLocation location)
        {
            var counter = Counters.GetOrCreateValue(location);
            if (location.farmers.Count != 0 || location.currentEvent != null || location.CanBeRemotedlyViewed() || ReferenceEquals(location, Game1.currentLocation))
                counter.IdleFrames = 0;
            else if (counter.IdleFrames < int.MaxValue)
                counter.IdleFrames++;
            return counter.IdleFrames;
        }

        private static bool BeforeLocationUpdate(GameLocation __instance, bool ignoreWasUpdatedFlush)
        {
            try
            {
                if (!Game1.IsMasterGame)
                    return true;

                int idle = Track(__instance);
                if (!_skipEmpty || idle < IdleFramesNeeded)
                    return true;

                if (__instance.characters.Count != 0 || __instance.animals.Length != 0 || __instance.buildings.Count != 0 || __instance.temporarySprites.Count != 0 || __instance.IsTemporary)
                    return true;

                if (!ignoreWasUpdatedFlush)
                    __instance.wasUpdated = false;
                if (_skippedEmpty++ == 0)
                    _monitor.Log($"Background updates of empty locations are being skipped (first: {__instance.NameOrUniqueName}). A location is only skipped after 60 frames with nobody in it, nothing in it and no event.", LogLevel.Info);
                return false;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Idle location check failed, so the game's own update runs. {ex.Message}", LogLevel.Warn);
                return true;
            }
        }

        private static bool BeforeMonsterUpdate(Monster __instance, GameLocation location)
        {
            try
            {
                if (!_skipMonsters || !Game1.IsMasterGame || location is null || Game1.eventUp)
                    return true;
                if (location is Farm or MineShaft or VolcanoDungeon || location.isStructure.Value)
                    return true;
                if (!Counters.TryGetValue(location, out var counter) || counter.IdleFrames < IdleFramesNeeded)
                    return true;

                if (_skippedMonsters++ == 0)
                    _monitor.Log($"Monsters in locations nobody is in are no longer updated (first: {location.NameOrUniqueName}). They move again as soon as a player is there.", LogLevel.Info);
                return false;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Idle monster check failed, so the game's own update runs. {ex.Message}", LogLevel.Warn);
                return true;
            }
        }

        /// <summary>How many background updates and monster updates were skipped so far. For the log and the tests.</summary>
        internal static (long Locations, long Monsters) Skipped => (_skippedEmpty, _skippedMonsters);
    }
}
