using System.Diagnostics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// How often a join attempt gets through when packets are lost. The joiner stays on the title screen and tries to reach the host
    /// over and over through tools\NetShaper. The loss rate is changed between blocks of attempts through the shaper's control file.
    /// An attempt succeeds when the host's list of farmhands arrives within 20 seconds. The host just waits.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> DiscoveryHost()
        {
            for (int t = 0; t < 216000 && !OtherHas("disc-done"); t += 30)
                yield return 30;
        }

        private IEnumerable<int> DiscoveryClient(TitleMenu title)
        {
            title.skipToTitleButtons();
            for (int t = 0; t < 9000 && !OtherHas("hostready"); t += 30)
                yield return 30;
            yield return 120;

            var multiplayer = Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            string address = Environment.GetEnvironmentVariable("VFH_JOINADDR") ?? "localhost";
            string? control = Environment.GetEnvironmentVariable("VFH_NETCONTROL");
            int per = int.TryParse(Environment.GetEnvironmentVariable("VFH_ATTEMPTS"), out int n) ? n : 25;
            var losses = (Environment.GetEnvironmentVariable("VFH_LOSSES") ?? "0,5,10,20").Split(',').Select(double.Parse).ToList();
            var blocks = new List<object>();
            _results["discovery"] = blocks;

            foreach (double loss in losses)
            {
                if (control is not null)
                    File.WriteAllText(control, $"30 5 {loss} 0");
                yield return 60;

                var times = new List<double>();
                int failed = 0;
                for (int a = 0; a < per; a++)
                {
                    var client = multiplayer.InitClient(new LidgrenClient(address));
                    var menu = new FarmhandMenu(client);
                    TitleMenu.subMenu = menu;
                    var watch = Stopwatch.StartNew();
                    bool ok = false;
                    for (int t = 0; t < 1200; t++)
                    {
                        if (menu.MenuSlots.Count > 0)
                        {
                            ok = true;
                            break;
                        }
                        yield return 1;
                    }
                    if (ok)
                        times.Add(Math.Round(watch.Elapsed.TotalMilliseconds, 0));
                    else
                        failed++;

                    // leave cleanly so the next attempt starts from the title screen with no old connection
                    try
                    {
                        client.disconnect(neatly: false);
                        // Lidgren's own type is not referenced here, so the socket is closed through reflection
                        object? net = typeof(LidgrenClient).GetField("client")?.GetValue(client);
                        net?.GetType().GetMethod("Shutdown", new[] { typeof(string) })?.Invoke(net, new object[] { "attempt over" });
                    }
                    catch
                    {
                    }
                    client.timedOut = false;
                    TitleMenu.subMenu = null;
                    if (Game1.activeClickableMenu is not TitleMenu)
                    {
                        Game1.ExitToTitle();
                        for (int t = 0; t < 600 && Game1.activeClickableMenu is not TitleMenu; t++)
                            yield return 1;
                    }
                    (Game1.activeClickableMenu as TitleMenu)?.skipToTitleButtons();
                    yield return 40;
                }
                var sorted = times.OrderBy(x => x).ToList();
                blocks.Add(new
                {
                    lossPercent = loss,
                    attempts = per,
                    connected = times.Count,
                    failed,
                    successPercent = Math.Round(100.0 * times.Count / per, 0),
                    medianMs = sorted.Count == 0 ? -1 : sorted[sorted.Count / 2],
                    maxMs = sorted.Count == 0 ? -1 : sorted[^1],
                    over3s = times.Count(x => x > 3000),
                    times
                });
                // written after every block, so a run that is stopped early still keeps what it measured
                File.WriteAllText(_out + ".partial", System.Text.Json.JsonSerializer.Serialize(blocks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            Mark("disc-done");
        }
    }
}
