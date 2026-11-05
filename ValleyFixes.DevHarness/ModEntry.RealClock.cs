using System.Diagnostics;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Ten minute clock scenario on a copy of a real save with the real mod list. Runs the game's own clock update ten times with the
    /// profiler on, then times the two per location steps directly on the locations that hold no objects, animals or buildings,
    /// to show how much of the update they take. Run with -Only realclock -Profile.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> RealClockMain()
        {
            var into = new Dictionary<string, object?>();
            _results["realclock"] = into;
            foreach (int w in WaitForWorld("realclock_error")) yield return w;
            into["world"] = WorldFacts();
            Game1.timeOfDay = 900;
            yield return 120;

            // the game's own update, wall time of each call
            var calls = new List<double>();
            Profiler.Reset();
            Profiler.Active = true;
            for (int i = 0; i < 12; i++)
            {
                var watch = Stopwatch.StartNew();
                Game1.performTenMinuteClockUpdate();
                watch.Stop();
                calls.Add(watch.Elapsed.TotalMilliseconds);
                yield return 20;
            }
            Profiler.Active = false;
            into["clockUpdateMs"] = calls.Select(c => Math.Round(c, 1)).ToList();
            into["profile"] = Profiler.Snapshot(1);

            // the two per location steps on locations that hold nothing, timed directly
            var empty = new List<GameLocation>();
            int all = 0;
            Utility.ForEachLocation(loc =>
            {
                all++;
                if (loc.objects.Length == 0 && loc.animals.Length == 0 && loc.buildings.Count == 0)
                    empty.Add(loc);
                return true;
            }, includeInteriors: true, includeGenerated: true);
            var objectsWatch = new Stopwatch();
            var timeWatch = new Stopwatch();
            for (int round = 0; round < 10; round++)
            {
                foreach (var loc in empty)
                {
                    objectsWatch.Start();
                    loc.passTimeForObjects(10);
                    objectsWatch.Stop();
                    timeWatch.Start();
                    loc.timeUpdate(10);
                    timeWatch.Stop();
                }
            }
            into["emptyLocations"] = new
            {
                locationsTotal = all,
                locationsEmpty = empty.Count,
                passTimeForObjectsMsPerUpdate = Math.Round(objectsWatch.Elapsed.TotalMilliseconds / 10, 3),
                timeUpdateMsPerUpdate = Math.Round(timeWatch.Elapsed.TotalMilliseconds / 10, 3)
            };
            yield return 60;
        }
    }
}
