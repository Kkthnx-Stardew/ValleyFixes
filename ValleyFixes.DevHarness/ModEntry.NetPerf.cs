using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Multiplayer traffic scenario for the performance research. Counts bytes written per message type on each side during the
    /// join, an idle half minute together, a walk, a burst of building on the farm and a night. Needs -Profile.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> NetPerfScenario()
        {
            var rows = new Dictionary<string, object?>();
            _results["netperf"] = rows;
            Profiler.Active = true;

            // everything since the process started: the join itself
            rows["join_and_load"] = new { bytesByType = Profiler.SnapshotNet(1) };
            foreach (int w in WarpAndWait("Farm", IsHost ? 64 : 66, 15)) yield return w;
            foreach (int w in Barrier("netfarm")) yield return w;
            yield return 120;

            Profiler.ResetNet();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            yield return 1800;
            rows["idle_together_30s"] = new { seconds = Math.Round(clock.Elapsed.TotalSeconds, 1), bytesByType = Profiler.SnapshotNet(clock.Elapsed.TotalSeconds) };
            foreach (int w in Barrier("netidle")) yield return w;

            Profiler.ResetNet();
            clock.Restart();
            for (int i = 0; i < 900; i += 6)
            {
                Game1.player.faceDirection((i / 6) % 4);
                Game1.player.Position += new Vector2(((i / 6) % 2 == 0) ? 12f : -12f, 0);
                yield return 6;
            }
            rows["walking_together_15s"] = new { seconds = Math.Round(clock.Elapsed.TotalSeconds, 1), bytesByType = Profiler.SnapshotNet(clock.Elapsed.TotalSeconds) };
            foreach (int w in Barrier("netwalk")) yield return w;

            // the host fills the farm while the joiner stands on it, the way a big build day or machine batch would
            Profiler.ResetNet();
            clock.Restart();
            if (IsHost)
                rows["heavy_objects"] = PopulateHeavyFarm(Game1.getFarm());
            yield return 600;
            rows["host_builds_farm_10s"] = new { seconds = Math.Round(clock.Elapsed.TotalSeconds, 1), bytesByType = Profiler.SnapshotNet(clock.Elapsed.TotalSeconds) };
            foreach (int w in Barrier("netbuilt")) yield return w;

            // the joiner walks onto the heavy farm from elsewhere: the cost of loading a big location over the wire
            if (!IsHost)
            {
                foreach (int w in WarpAndWait("Town", 52, 55)) yield return w;
            }
            foreach (int w in Barrier("netaway")) yield return w;
            Profiler.ResetNet();
            clock.Restart();
            if (!IsHost)
            {
                foreach (int w in WarpAndWait("Farm", 66, 15)) yield return w;
            }
            yield return 300;
            rows["joiner_enters_heavy_farm"] = new { seconds = Math.Round(clock.Elapsed.TotalSeconds, 1), bytesByType = Profiler.SnapshotNet(clock.Elapsed.TotalSeconds) };
            foreach (int w in Barrier("netentered")) yield return w;

            // each player goes to their own bed first: the host's farmhouse or the joiner's cabin
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            rows["in_bed_" + Role] = inBed;
            foreach (int w in Barrier("nethome")) yield return w;

            Profiler.ResetNet();
            clock.Restart();
            foreach (int w in SleepThroughNight()) yield return w;
            rows["one_night"] = new { seconds = Math.Round(clock.Elapsed.TotalSeconds, 1), bytesByType = Profiler.SnapshotNet(clock.Elapsed.TotalSeconds) };
            foreach (int w in Barrier("netnight")) yield return w;
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        /// <summary>The game sets the in-bed flag every tick from the "Bed" property of the tile the player stands on, so a sleep test must stand on such a tile.</summary>
        private static Point FindBedTile(GameLocation house)
        {
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (house.doesTileHaveProperty(x, y, "Bed", "Back") != null)
                        return new Point(x, y);
                }
            }
            return (house as StardewValley.Locations.FarmHouse)?.GetPlayerBedSpot() ?? new Point(3, 11);
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        /// <summary>
        /// Gets the player into their bed. A warp from outside straight onto a bed tile is refused by the game, which leaves the
        /// player in the doorway, so this enters first and then steps onto the bed. Returns whether the game counts the player as in bed.
        /// </summary>
        private IEnumerable<int> GoToBed(Action<bool> result)
        {
            string home = Game1.player.homeLocation.Value;
            var house = Game1.getLocationFromName(home);
            var bed = FindBedTile(house);
            if (Game1.currentLocation?.NameOrUniqueName != home)
            {
                var door = (house as StardewValley.Locations.FarmHouse)?.getEntryLocation() ?? new Point(3, 11);
                Game1.warpFarmer(home, door.X, door.Y, false);
                for (int t = 0; t < 600 && !(Game1.currentLocation?.NameOrUniqueName == home && !Game1.fadeToBlack && !Game1.isWarping); t++)
                    yield return 1;
                yield return 60;
            }
            Game1.player.setTileLocation(new Vector2(bed.X, bed.Y));
            yield return 30;
            result(Game1.player.isInBed.Value);
        }
    }
}
