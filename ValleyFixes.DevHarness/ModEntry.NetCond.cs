using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Network condition scenario. Run with the joiner connected through tools\NetShaper to add delay, jitter and loss.
    /// Each trial is one player doing something and the other waiting to see it, timed on the shared PC clock:
    /// a position change and an object placed then removed, both ways. Then a walk with frame gaps and ping, and optionally a night.
    /// A latency of -1 means the other side never saw the change within ten seconds.
    /// </summary>
    public partial class ModEntry
    {
        private static object LatencyStats(List<double> ms)
        {
            var ok = ms.Where(x => x >= 0).OrderBy(x => x).ToList();
            if (ok.Count == 0)
                return new { trials = ms.Count, seen = 0, missed = ms.Count };
            return new
            {
                trials = ms.Count,
                seen = ok.Count,
                missed = ms.Count - ok.Count,
                meanMs = Math.Round(ok.Average(), 0),
                medianMs = Math.Round(ok[ok.Count / 2], 0),
                p90Ms = Math.Round(ok[Math.Min(ok.Count - 1, (int)(ok.Count * 0.9))], 0),
                maxMs = Math.Round(ok[^1], 0),
                all = ms.Select(x => Math.Round(x, 0)).ToList()
            };
        }

        private long? ReadStamp(string name)
        {
            try
            {
                return Fetch(name) is JsonElement e ? e.GetProperty("t0").GetInt64() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>One side acts and publishes the moment it acted, the other watches for the change and times it. Both then wait for each other.</summary>
        private IEnumerable<int> Trial(string name, bool hostActs, Action act, Func<bool> seen, List<double> results)
        {
            bool actor = hostActs == IsHost;
            if (actor)
            {
                long t0 = DateTime.UtcNow.Ticks;
                act();
                Publish(name, new { t0 });
                for (int t = 0; t < 2400 && !OtherHas(name + "-ack"); t += 3)
                    yield return 3;
            }
            else
            {
                long? t0 = null;
                for (int t = 0; t < 3000 && (t0 = ReadStamp(name)) is null; t++)
                    yield return 1;
                int waited = 0;
                while (!seen() && waited < 600)
                {
                    waited++;
                    yield return 1;
                }
                results.Add(t0 is not null && seen() ? (DateTime.UtcNow.Ticks - t0.Value) / 10000.0 : -1);
                Mark(name + "-ack");
            }
            yield return 30;
        }

        private IEnumerable<int> PingWalk(int ticks, List<double> pings)
        {
            long? clientId = IsHost ? ClientFarmerAsSeenByHost()?.UniqueMultiplayerID : null;
            for (int i = 0; i < ticks; i += 6)
            {
                Game1.player.faceDirection((i / 6) % 4);
                Game1.player.Position += new Vector2(((i / 6) % 2 == 0) ? 12f : -12f, 0);
                if (i % 30 == 0)
                {
                    try
                    {
                        float ping = IsHost ? (clientId is long id ? Game1.server.getPingToClient(id) : -1f) : Game1.client.GetPingToHost();
                        pings.Add(Math.Round(ping, 1));
                    }
                    catch
                    {
                    }
                }
                yield return 6;
            }
        }

        private IEnumerable<int> NetCondScenario()
        {
            var rows = new Dictionary<string, object?>();
            _results["netcond"] = rows;
            rows["role"] = Role;
            rows["condition"] = Environment.GetEnvironmentVariable("VFH_NETLABEL") ?? "";
            int trials = int.TryParse(Environment.GetEnvironmentVariable("VFH_TRIALS"), out int n) ? n : 12;

            foreach (int w in WarpAndWait("Farm", IsHost ? 64 : 66, 15)) yield return w;
            foreach (int w in Barrier("nc-onfarm", 14400)) yield return w;
            yield return 180;

            var farm = Game1.getFarm();
            var spot = new Point(64, 20);
            if (IsHost)
            {
                spot = FindOpenTile(farm);
                Publish("nc-spot", new { spot.X, spot.Y });
            }
            foreach (int w in Barrier("nc-spot", 14400)) yield return w;
            if (!IsHost && Fetch("nc-spot") is JsonElement e)
                spot = new Point(e.GetProperty("X").GetInt32(), e.GetProperty("Y").GetInt32());
            var hostTile = new Vector2(spot.X, spot.Y);
            var clientTile = new Vector2(spot.X + 2, spot.Y);

            var hostPos = new List<double>();
            var clientPos = new List<double>();
            var hostObj = new List<double>();
            var hostObjGone = new List<double>();
            var clientObj = new List<double>();
            var clientObjGone = new List<double>();

            Vector2 hostSpotA = new Vector2(60 * 64, 18 * 64), hostSpotB = new Vector2(72 * 64, 18 * 64);
            Vector2 clientSpotA = new Vector2(62 * 64, 23 * 64), clientSpotB = new Vector2(74 * 64, 23 * 64);

            for (int i = 0; i < trials; i++)
            {
                // the host moves, the joiner sees it
                var target = i % 2 == 0 ? hostSpotA : hostSpotB;
                foreach (int w in Trial("nc-hp-" + i, true, () => Game1.player.Position = target, () => Vector2.Distance(Game1.MasterPlayer.Position, target) < 4f, hostPos)) yield return w;
                // the joiner moves, the host sees it
                var target2 = i % 2 == 0 ? clientSpotA : clientSpotB;
                foreach (int w in Trial("nc-cp-" + i, false, () => Game1.player.Position = target2, () => ClientFarmerAsSeenByHost() is Farmer f && Vector2.Distance(f.Position, target2) < 4f, clientPos)) yield return w;

                // the host places an object, then removes it
                foreach (int w in Trial("nc-ho-" + i, true, () => farm.objects[hostTile] = new StardewValley.Object("388", 1), () => farm.objects.ContainsKey(hostTile), hostObj)) yield return w;
                foreach (int w in Trial("nc-hg-" + i, true, () => farm.objects.Remove(hostTile), () => !farm.objects.ContainsKey(hostTile), hostObjGone)) yield return w;
                // the joiner places an object, then removes it
                foreach (int w in Trial("nc-co-" + i, false, () => farm.objects[clientTile] = new StardewValley.Object("388", 1), () => farm.objects.ContainsKey(clientTile), clientObj)) yield return w;
                foreach (int w in Trial("nc-cg-" + i, false, () => farm.objects.Remove(clientTile), () => !farm.objects.ContainsKey(clientTile), clientObjGone)) yield return w;
            }
            rows["host_moves_seen_by_joiner"] = IsHost ? null : LatencyStats(hostPos);
            rows["joiner_moves_seen_by_host"] = IsHost ? LatencyStats(clientPos) : null;
            rows["host_places_object_seen_by_joiner"] = IsHost ? null : LatencyStats(hostObj);
            rows["host_removes_object_seen_by_joiner"] = IsHost ? null : LatencyStats(hostObjGone);
            rows["joiner_places_object_seen_by_host"] = IsHost ? LatencyStats(clientObj) : null;
            rows["joiner_removes_object_seen_by_host"] = IsHost ? LatencyStats(clientObjGone) : null;
            foreach (int w in Barrier("nc-trials-done", 14400)) yield return w;

            var pings = new List<double>();
            foreach (int w in NetPhase(rows, "walk_30s", "both", PingWalk(1800, pings))) yield return w;
            rows["ping_ms_samples"] = pings;
            rows["ping_mean_ms"] = pings.Count == 0 ? -1 : Math.Round(pings.Average(), 1);

            if (Environment.GetEnvironmentVariable("VFH_NIGHT") == "1")
            {
                bool inBed = false;
                foreach (int w in GoToBed(r => inBed = r)) yield return w;
                rows["in_bed_" + Role] = inBed;
                foreach (int w in Barrier("nc-home", 14400)) yield return w;
                string before = $"{Game1.currentSeason} {Game1.dayOfMonth}";
                foreach (int w in NetPhase(rows, "one_night", "both", SleepThroughNight())) yield return w;
                rows["date_before_night"] = before;
                rows["date_after_night"] = $"{Game1.currentSeason} {Game1.dayOfMonth}";
            }
            rows["memory"] = MemoryLight();
        }
    }
}
