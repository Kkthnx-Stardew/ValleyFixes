using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Three and four player runs: a host and VFH_CLIENTS joiners (roles client, client2, client3). The host lets the joiners in one at a
    /// time, then everybody idles together, walks together and sleeps one night. Each process records its own frame cost and gaps, and
    /// the host also records the bytes it writes. Run it with 1, 2 and 3 joiners to see what each extra player costs.
    /// </summary>
    public partial class ModEntry
    {
        private static int CrowdN => int.TryParse(Environment.GetEnvironmentVariable("VFH_CLIENTS"), out int n) ? Math.Max(1, n) : 1;

        private static string[] CrowdRoles()
        {
            var roles = new List<string> { "host", "client" };
            for (int i = 2; i <= CrowdN; i++)
                roles.Add("client" + i);
            return roles.ToArray();
        }

        private static int CrowdIndex => Role == "host" ? 0 : Role == "client" ? 1 : int.Parse(Role.Substring("client".Length));

        /// <summary>Everybody marks the name and waits until every role has.</summary>
        private IEnumerable<int> CrowdBarrier(string name, int limitTicks = 18000)
        {
            Mark(name);
            var roles = CrowdRoles();
            for (int t = 0; t < limitTicks && !roles.All(r => File.Exists(SyncFile(name, r))); t += 5)
                yield return 5;
            if (!roles.All(r => File.Exists(SyncFile(name, r))))
                _results["barrier_timeout_" + name] = true;
        }

        /// <summary>The host lets one joiner in at a time and records how long each join took on its side.</summary>
        private IEnumerable<int> CrowdJoin()
        {
            var joins = new List<object>();
            for (int i = 1; i <= CrowdN; i++)
            {
                string role = i == 1 ? "client" : "client" + i;
                Mark("go-" + role);
                double started = _clock.Elapsed.TotalMilliseconds;
                // the next joiner is released only when this one reports its world is ready, because a game window that is not in front cannot finish loading
                for (int t = 0; t < 12000 && !(Game1.otherFarmers.Count >= i && File.Exists(SyncFile("ready", role))); t += 15)
                    yield return 15;
                joins.Add(new { role, joined = Game1.otherFarmers.Count >= i, seconds = Math.Round((_clock.Elapsed.TotalMilliseconds - started) / 1000, 1) });
                yield return 300;
            }
            _results["crowd_joins"] = joins;
            _results["crowd_gaps_during_joins"] = GapStats();
            _gapOn = false;
        }

        /// <summary>Makes sure there are n cabins, each with a customized farmhand nobody has played, the way a host who invited friends would have.</summary>
        private int PrepareCabins(int n)
        {
            var farm = Game1.getFarm();
            var tiles = new[] { new Vector2(58, 20), new Vector2(52, 20), new Vector2(46, 20), new Vector2(40, 22), new Vector2(60, 28), new Vector2(66, 28), new Vector2(72, 28), new Vector2(34, 22) };
            foreach (var tile in tiles)
            {
                if (farm.buildings.Count(b => b.GetIndoors() is Cabin) >= n)
                    break;
                farm.buildStructure("Cabin", tile, Game1.player, out _, magicalConstruction: true, skipSafetyChecks: true);
            }
            int ready = 0;
            foreach (var cabin in farm.buildings.Select(b => b.GetIndoors() as Cabin).Where(c => c is not null))
            {
                if (!cabin!.HasOwner)
                    cabin.CreateFarmhand();
                var hand = cabin.owner;
                if (hand is not null && !hand.isCustomized.Value)
                {
                    hand.isCustomized.Value = true;
                    hand.Name = "Client" + (ready + 1);
                    hand.displayName = hand.Name;
                    hand.farmName.Value = Game1.player.farmName.Value;
                    hand.hair.Value = 2;
                    hand.skin.Value = 1;
                }
                ready++;
            }
            return ready;
        }

        private IEnumerable<int> CrowdPhase(Dictionary<string, object?> into, string label, IEnumerable<int> body)
        {
            foreach (int w in CrowdBarrier(label + "-start")) yield return w;
            Profiler.ResetNet();
            StartGaps();
            var clock = Stopwatch.StartNew();
            var inner = new Dictionary<string, object?>();
            foreach (int w in SampleBody(inner, "sample", body)) yield return w;
            _gapOn = false;
            double seconds = clock.Elapsed.TotalSeconds;
            into[label] = new
            {
                seconds = Math.Round(seconds, 1),
                online = Game1.getOnlineFarmers().Count,
                net = Profiler.SnapshotNet(seconds),
                gaps = GapStats(),
                sample = inner["sample"],
                memory = MemoryLight()
            };
            foreach (int w in CrowdBarrier(label + "-end")) yield return w;
        }

        private IEnumerable<int> CrowdScenario()
        {
            var rows = new Dictionary<string, object?>();
            _results["crowd"] = rows;
            rows["role"] = Role;
            rows["players"] = CrowdN + 1;
            Profiler.Active = true;

            foreach (int w in WarpAndWait("Farm", 60 + 3 * CrowdIndex, 15)) yield return w;
            foreach (int w in CrowdBarrier("cr-onfarm")) yield return w;
            yield return 180;
            rows["online_seen"] = Game1.getOnlineFarmers().Count;

            foreach (int w in CrowdPhase(rows, "idle_20s", Idle(1200))) yield return w;
            foreach (int w in CrowdPhase(rows, "walk_20s", WalkAbout(1200))) yield return w;

            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            rows["in_bed"] = inBed;
            foreach (int w in CrowdBarrier("cr-home")) yield return w;
            string before = $"{Game1.currentSeason} {Game1.dayOfMonth}";
            foreach (int w in CrowdPhase(rows, "one_night", SleepThroughNight())) yield return w;
            rows["date_before_night"] = before;
            rows["date_after_night"] = $"{Game1.currentSeason} {Game1.dayOfMonth}";
            rows["memory_end"] = MemoryLight();
            foreach (int w in CrowdBarrier("cr-done")) yield return w;
        }
    }
}
