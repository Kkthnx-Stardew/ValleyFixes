using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Sync tuning scenario. One side moves a player or a monster at a constant speed, back and forth, as a function of the shared PC clock.
    /// The other side reads where it sees the thing every tick and turns the difference to where it should be into a lag in milliseconds.
    /// VFH_NETTUNE="period,interpolation" sets the game's broadcast period and interpolation ticks on this process before the run.
    /// The numbers per phase are mean lag, spread of the lag (how jerky the movement looks), the share of ticks where a moving thing
    /// did not change position, and the bytes each side sent.
    /// </summary>
    public partial class ModEntry
    {
        private const double TuneSpeed = 256.0;   // pixels a second, about a run
        private const double TunePeriod = 8.0;    // seconds for out and back

        private static double TuneOffset(double seconds, out int direction)
        {
            double t = seconds % TunePeriod;
            direction = t < TunePeriod / 2 ? 1 : -1;
            return TuneSpeed * (t < TunePeriod / 2 ? t : TunePeriod - t);
        }

        private static object TuneStats(List<double> lag, int moving, int still)
        {
            if (lag.Count == 0)
                return new { samples = 0 };
            var s = lag.OrderBy(x => x).ToList();
            double mean = s.Average();
            double sd = Math.Sqrt(s.Sum(x => (x - mean) * (x - mean)) / s.Count);
            return new
            {
                samples = s.Count,
                meanLagMs = Math.Round(mean, 0),
                medianLagMs = Math.Round(s[s.Count / 2], 0),
                p05LagMs = Math.Round(s[(int)(s.Count * 0.05)], 0),
                p95LagMs = Math.Round(s[Math.Min(s.Count - 1, (int)(s.Count * 0.95))], 0),
                spreadMs = Math.Round(sd, 1),
                stillTickPercent = moving == 0 ? 0 : Math.Round(100.0 * still / moving, 1)
            };
        }

        /// <summary>Drives a thing every tick when this side is the actor, reads it when this side is the watcher.</summary>
        private IEnumerable<int> TuneBody(Dictionary<string, object?> into, string label, bool hostDrives, string kind, int ticks, Vector2 origin)
        {
            bool drives = hostDrives == IsHost;
            var farm = Game1.getFarm();
            Monster? monster = null;
            if (kind == "monster" && IsHost)
            {
                monster = new GreenSlime(origin);
                monster.currentLocation = farm;
                farm.characters.Add(monster);
            }
            // give a watcher time to receive the monster before timing starts
            for (int i = 0; i < 180; i++)
            {
                if (drives)
                    SetTunePosition(kind, monster, origin, DateTime.UtcNow);
                yield return 1;
            }

            var lag = new List<double>();
            int moving = 0, still = 0, sameRun = 0;
            Vector2? last = null;
            for (int i = 0; i < ticks; i++)
            {
                var now = DateTime.UtcNow;
                if (drives)
                    SetTunePosition(kind, monster, origin, now);
                else
                {
                    Vector2? seen = ReadTunePosition(kind, farm, hostDrives);
                    if (seen is Vector2 p)
                    {
                        double seconds = now.Ticks / 1e7;
                        double offset = TuneOffset(seconds, out int dir);
                        double phase = seconds % TunePeriod;
                        bool nearTurn = Math.Abs(phase - TunePeriod / 2) < 0.4 || phase < 0.4 || phase > TunePeriod - 0.4;
                        if (!nearTurn)
                        {
                            double expected = origin.X + offset;
                            lag.Add((expected - p.X) * dir / TuneSpeed * 1000.0);
                            moving++;
                            if (last is Vector2 l && l == p)
                                sameRun++;
                            else
                                sameRun = 0;
                            if (sameRun >= 3)
                                still++;
                        }
                        last = p;
                    }
                }
                yield return 1;
            }

            if (monster is not null)
                farm.characters.Remove(monster);
            into[label + "_" + (drives ? "actor" : "watcher")] = drives ? null : TuneStats(lag, moving, still);
        }

        private void SetTunePosition(string kind, Monster? monster, Vector2 origin, DateTime now)
        {
            double offset = TuneOffset(now.Ticks / 1e7, out _);
            var pos = new Vector2((float)(origin.X + offset), origin.Y);
            if (kind == "monster" && monster is not null)
                monster.Position = pos;
            else if (kind == "player")
                Game1.player.Position = pos;
        }

        private Vector2? ReadTunePosition(string kind, StardewValley.GameLocation farm, bool hostDrives)
        {
            if (kind == "monster")
                return farm.characters.OfType<GreenSlime>().FirstOrDefault()?.Position;
            return hostDrives ? Game1.MasterPlayer.Position : ClientFarmerAsSeenByHost()?.Position;
        }

        private IEnumerable<int> NetTuneScenario()
        {
            var rows = new Dictionary<string, object?>();
            _results["nettune"] = rows;
            rows["role"] = Role;
            rows["condition"] = Environment.GetEnvironmentVariable("VFH_NETLABEL") ?? "";

            string tune = Environment.GetEnvironmentVariable("VFH_NETTUNE") ?? "";
            var mp = Game1.Multiplayer;
            if (tune.Split(',') is { Length: 2 } parts && int.TryParse(parts[0], out int period) && int.TryParse(parts[1], out int interp))
            {
                mp.farmerDeltaBroadcastPeriod = period;
                mp.locationDeltaBroadcastPeriod = period;
                mp.worldStateDeltaBroadcastPeriod = period;
                mp.defaultInterpolationTicks = interp;
            }
            rows["broadcastPeriod"] = mp.farmerDeltaBroadcastPeriod;
            rows["interpolationTicks"] = mp.defaultInterpolationTicks;
            int ticks = int.TryParse(Environment.GetEnvironmentVariable("VFH_TICKS"), out int n) ? n : 900;

            foreach (int w in WarpAndWait("Farm", IsHost ? 64 : 66, 15)) yield return w;
            foreach (int w in Barrier("nt-onfarm", 14400)) yield return w;
            yield return 180;

            var origin = new Vector2(58 * 64, 17 * 64);
            foreach (int w in NetPhase(rows, "host_player", "both", TuneBody(rows, "host_player", true, "player", ticks, origin))) yield return w;
            foreach (int w in NetPhase(rows, "host_monster", "both", TuneBody(rows, "host_monster", true, "monster", ticks, origin + new Vector2(0, 5 * 64)))) yield return w;
            foreach (int w in NetPhase(rows, "client_player", "both", TuneBody(rows, "client_player", false, "player", ticks, origin + new Vector2(0, 2 * 64)))) yield return w;
            rows["memory"] = MemoryLight();
        }
    }
}
