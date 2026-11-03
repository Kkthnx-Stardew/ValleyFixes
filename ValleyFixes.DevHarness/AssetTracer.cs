using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Counts how often SMAPI turns an asset name string into an AssetName object (which every content lookup does),
    /// which names are asked for, and every 100th call who asked, by the first calling method outside SMAPI and the runtime.
    /// </summary>
    internal static class AssetTracer
    {
        private static readonly Dictionary<string, long> Names = new();
        private static readonly Dictionary<string, long> Callers = new();
        private static readonly object Gate = new();
        private static long _calls;
        private const int SampleEvery = 100;

        public static void Install(Harmony harmony, IMonitor monitor)
        {
            var type = Type.GetType("StardewModdingAPI.Framework.Content.AssetName, StardewModdingAPI");
            var parse = type?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
            if (parse is null)
            {
                monitor.Log("[assettracer] AssetName.Parse was not found", LogLevel.Warn);
                return;
            }
            harmony.Patch(parse, prefix: new HarmonyMethod(typeof(AssetTracer), nameof(Before)));
        }

        private static bool Skip(string ns) =>
            ns.StartsWith("StardewModdingAPI.Framework", StringComparison.Ordinal) || ns.StartsWith("System", StringComparison.Ordinal)
            || ns.StartsWith("HarmonyLib", StringComparison.Ordinal) || ns.StartsWith("DMD", StringComparison.Ordinal)
            || ns.StartsWith("ValleyFixes.DevHarness", StringComparison.Ordinal);

        private static void Before(string rawName)
        {
            if (!Profiler.Active)
                return;

            long n = Interlocked.Increment(ref _calls);
            string caller = "";
            if (n % SampleEvery == 0)
            {
                var frames = new StackTrace(2, false).GetFrames();
                var chain = new List<string>();
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    string ns = m?.DeclaringType?.FullName ?? "";
                    if (m is null || (chain.Count == 0 && Skip(ns)))
                        continue;
                    chain.Add($"{m.DeclaringType?.Name}.{m.Name}");
                    if (chain.Count >= 4)
                        break;
                }
                caller = string.Join(" <- ", chain);
            }

            lock (Gate)
            {
                string key = rawName ?? "";
                Names.TryGetValue(key, out long c);
                Names[key] = c + 1;
                if (caller.Length > 0)
                {
                    Callers.TryGetValue(caller, out long k);
                    Callers[caller] = k + 1;
                }
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Names.Clear();
                Callers.Clear();
                _calls = 0;
            }
        }

        public static object Snapshot(int frames, double seconds)
        {
            lock (Gate)
            {
                return new
                {
                    callsPerFrame = Math.Round(_calls / Math.Max(1.0, frames), 1),
                    callsPerSecond = Math.Round(_calls / Math.Max(0.1, seconds), 0),
                    distinctNames = Names.Count,
                    topNames = Names.OrderByDescending(n => n.Value).Take(15).Select(n => new { name = n.Key, perFrame = Math.Round(n.Value / Math.Max(1.0, frames), 2) }).ToList(),
                    topCallers = Callers.OrderByDescending(c => c.Value).Take(15).Select(c => new { caller = c.Key, sharePercent = Math.Round(c.Value * 100.0 / Math.Max(1, Callers.Values.Sum()), 1) }).ToList()
                };
            }
        }
    }
}
