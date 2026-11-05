using System.Text.Json;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// What the host sees when the joiner vanishes. Mode comes from VFH_DEADMODE:
    /// kill       the joiner's process is killed on the farm, no goodbye is sent
    /// blackhole  the link is cut (NetShaper drops everything) while both processes stay alive
    /// killsleep  the host goes to sleep, then the joiner's process is killed before it is in bed, so the night waits for it
    /// Times are taken on the shared PC clock. The host also records its own frame gaps, to show whether the wait freezes the game.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> DeadClientScenario()
        {
            string mode = Environment.GetEnvironmentVariable("VFH_DEADMODE") ?? "kill";
            var rows = new Dictionary<string, object?>();
            _results["dead"] = rows;
            rows["mode"] = mode;
            rows["role"] = Role;

            foreach (int w in WarpAndWait("Farm", IsHost ? 64 : 66, 15)) yield return w;
            foreach (int w in Barrier("dc-onfarm", 14400)) yield return w;
            yield return 180;

            if (IsHost)
            {
                StartGaps();
                int day0 = Game1.Date.TotalDays;
                long? clientId = ClientFarmerAsSeenByHost()?.UniqueMultiplayerID;
                Mark("dc-go");
                long t0 = 0;
                if (mode == "blackhole")
                {
                    t0 = DateTime.UtcNow.Ticks;
                    File.WriteAllText(Environment.GetEnvironmentVariable("VFH_BLACKHOLE")!, "cut");
                }
                rows["clientId"] = clientId;
                var pings = new List<object>();

                if (mode == "killsleep")
                {
                    // the host is asleep before the joiner dies. The night cannot finish until the game stops counting the joiner
                    bool inBed = false;
                    foreach (int w in GoToBed(r => inBed = r)) yield return w;
                    rows["host_in_bed"] = inBed;
                    Game1.player.isInBed.Value = true;
                    var start = typeof(GameLocation).GetMethod("startSleep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    start!.Invoke(Game1.currentLocation, null);
                }
                if (mode != "blackhole")
                {
                    // the joiner writes its time just before it dies
                    for (int t = 0; t < 3000 && (ReadStamp("dc-dying") is null); t++)
                        yield return 1;
                    t0 = ReadStamp("dc-dying") ?? DateTime.UtcNow.Ticks;
                }

                long? detectOthers = null, detectOnline = null, dayChanged = null;
                int limit = mode == "killsleep" ? 14400 : 10800;
                int ticks = 0;
                for (; ticks < limit; ticks += 5)
                {
                    long now = DateTime.UtcNow.Ticks;
                    if (detectOthers is null && Game1.otherFarmers.Count == 0)
                        detectOthers = now;
                    if (detectOnline is null && Game1.getOnlineFarmers().Count <= 1)
                        detectOnline = now;
                    if (dayChanged is null && Game1.Date.TotalDays != day0)
                        dayChanged = now;
                    if (ticks % 300 == 0 && clientId is long id)
                    {
                        try { pings.Add(new { secondsSinceCut = Math.Round((now - t0) / 1e7, 1), pingMs = Math.Round(Game1.server.getPingToClient(id), 1), others = Game1.otherFarmers.Count }); } catch { }
                    }
                    if (mode == "killsleep" ? dayChanged is not null : detectOthers is not null)
                    {
                        // a few more seconds so late effects land in the frame gaps
                        for (int k = 0; k < 600; k += 5)
                            yield return 5;
                        break;
                    }
                    yield return 5;
                }
                double Seconds(long? t) => t is null ? -1 : Math.Round((t.Value - t0) / 1e7, 1);
                rows["seconds_until_host_drops_joiner_from_otherFarmers"] = Seconds(detectOthers);
                rows["seconds_until_host_counts_one_online"] = Seconds(detectOnline);
                rows["seconds_until_new_day"] = Seconds(dayChanged);
                rows["host_gaps_during_wait"] = GapStats();
                rows["ping_samples"] = pings;
                rows["host_alive_ticks"] = ticks;
                Mark("dc-hostdone");
                yield return 60;
            }
            else
            {
                for (int t = 0; t < 3000 && !OtherHas("dc-go"); t++)
                    yield return 1;
                if (mode == "blackhole")
                {
                    long cutAt = DateTime.UtcNow.Ticks;
                    long? lost = null;
                    for (int t = 0; t < 7200 && !OtherHas("dc-hostdone"); t += 5)
                    {
                        if (lost is null && !Context.IsWorldReady)
                            lost = DateTime.UtcNow.Ticks;
                        yield return 5;
                    }
                    rows["seconds_until_joiner_notices"] = lost is null ? -1 : Math.Round((lost.Value - cutAt) / 1e7, 1);
                    rows["joiner_world_ready_at_end"] = Context.IsWorldReady;
                }
                else
                {
                    // a few seconds into the host's step, so the host is already waiting on this player
                    yield return mode == "killsleep" ? 900 : 300;
                    Publish("dc-dying", new { t0 = DateTime.UtcNow.Ticks });
                    yield return 3;
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
            }
        }
    }
}
