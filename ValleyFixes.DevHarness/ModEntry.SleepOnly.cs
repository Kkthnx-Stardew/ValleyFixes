using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>Two players join, each walks to their bed, both sleep one night. Nothing else, so a stall can only come from the sleep itself.</summary>
    public partial class ModEntry
    {
        private IEnumerable<int> SleepOnlyScenario()
        {
            var rows = new List<object>();
            _results["sleeponly"] = rows;
            yield return 120;
            if (IsHost && Environment.GetEnvironmentVariable("VFH_SLEEPTIME") is { Length: > 0 } st)
                Game1.timeOfDay = int.Parse(st);
            yield return 120;
            string home = Game1.player.homeLocation.Value;
            var bed = FindBedTile(Game1.getLocationFromName(home));
            Game1.warpFarmer(home, bed.X, bed.Y, false);
            for (int t = 0; t < 600 && !(Game1.currentLocation?.NameOrUniqueName == home && !Game1.fadeToBlack && !Game1.isWarping); t++)
                yield return 1;
            yield return 60;
            rows.Add(new { tag = "before-sleep-" + Role, location = Game1.currentLocation?.NameOrUniqueName, tile = $"{Game1.player.TilePoint.X},{Game1.player.TilePoint.Y}", bedTile = $"{bed.X},{bed.Y}", time = Game1.timeOfDay, isInBed = Game1.player.isInBed.Value });
            foreach (int w in Barrier("sleepready")) yield return w;
            foreach (int w in SleepThroughNight()) yield return w;
            rows.Add(new { tag = "after-sleep-" + Role, date = $"{Game1.currentSeason} {Game1.dayOfMonth}", time = Game1.timeOfDay });
            foreach (int w in Barrier("sleepdone")) yield return w;
        }
    }
}
