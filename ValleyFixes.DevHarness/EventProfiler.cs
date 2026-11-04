using System.Collections;
using System.Diagnostics;
using System.Reflection;
using StardewModdingAPI;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Once every mod has registered its handlers, each SMAPI event handler is wrapped in a timer and an allocation
    /// counter, so a sample can say exactly how many milliseconds and how many bytes each mod's handlers cost per frame, per event.
    /// Nothing in any mod is changed. Never shipped.
    /// </summary>
    internal static class EventProfiler
    {
        private sealed class Row
        {
            public long Ticks, Bytes, Calls;
        }

        private static readonly Dictionary<(string Mod, string Event), Row> Rows = new();
        private static readonly object Gate = new();
        private static int _wrapped;

        public static void Install(IMonitor monitor)
        {
            try
            {
                var coreType = Type.GetType("StardewModdingAPI.Framework.SCore, StardewModdingAPI");
                object? core = coreType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
                object? events = coreType?.GetField("EventManager", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(core);
                if (events is null)
                {
                    monitor.Log("[eventprofiler] SMAPI's event manager was not found", LogLevel.Warn);
                    return;
                }

                int added = 0;
                foreach (var field in events.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    object? managed = field.GetValue(events);
                    if (managed is null || !field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition().Name != "ManagedEvent`1")
                        continue;

                    Type argType = field.FieldType.GetGenericArguments()[0];
                    var list = field.FieldType.GetField("Handlers", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(managed) as IEnumerable;
                    if (list is null)
                        continue;

                    lock (list)
                    {
                        foreach (object h in list)
                        {
                            var backing = h.GetType().GetField("<Handler>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                            var original = backing?.GetValue(h) as Delegate;
                            if (backing is null || original is null || original.Target?.GetType().DeclaringType == typeof(EventProfiler))
                                continue;

                            object? mod = h.GetType().GetProperty("SourceMod")?.GetValue(h);
                            object? manifest = mod?.GetType().GetProperty("Manifest")?.GetValue(mod);
                            string id = manifest?.GetType().GetProperty("UniqueID")?.GetValue(manifest) as string ?? "?";
                            var make = typeof(EventProfiler).GetMethod(nameof(Make), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(argType);
                            backing.SetValue(h, make.Invoke(null, new object[] { original, id, field.Name }));
                            added++;
                        }
                    }
                }

                _wrapped += added;
                if (added > 0)
                    monitor.Log($"[eventprofiler] wrapped {added} event handlers ({_wrapped} in all)", LogLevel.Info);
            }
            catch (Exception ex)
            {
                monitor.Log("[eventprofiler] failed: " + ex.Message, LogLevel.Warn);
            }
        }

        private static EventHandler<T> Make<T>(EventHandler<T> inner, string mod, string eventName)
        {
            var key = (mod, eventName);
            return (sender, args) =>
            {
                if (!Profiler.Active)
                {
                    inner(sender, args);
                    return;
                }
                long ticks = Stopwatch.GetTimestamp();
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    inner(sender, args);
                }
                finally
                {
                    ticks = Stopwatch.GetTimestamp() - ticks;
                    bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                    double elapsedMs = ticks * 1000.0 / Stopwatch.Frequency;
                    lock (Gate)
                    {
                        // one slow call is a hitch the player feels, even when the mean over a sample looks fine
                        if (elapsedMs > 40 && Slow.Count < 80)
                            Slow.Add(new { at = DateTime.Now.ToString("HH:mm:ss.fff"), mod, @event = eventName, ms = Math.Round(elapsedMs, 0) });
                        if (!Rows.TryGetValue(key, out var row))
                            Rows[key] = row = new Row();
                        row.Ticks += ticks;
                        row.Bytes += bytes;
                        row.Calls++;
                    }
                }
            };
        }

        private static readonly List<object> Slow = new();

        public static void Reset()
        {
            lock (Gate)
            {
                Rows.Clear();
                Slow.Clear();
            }
        }

        public static object Snapshot(int frames, double seconds)
        {
            double toMs = 1000.0 / Stopwatch.Frequency;
            double f = Math.Max(1, frames);
            lock (Gate)
            {
                var perMod = Rows.GroupBy(r => r.Key.Mod).Select(g => new
                {
                    mod = g.Key,
                    msPerFrame = Math.Round(g.Sum(r => r.Value.Ticks) * toMs / f, 3),
                    kbPerFrame = Math.Round(g.Sum(r => r.Value.Bytes) / 1024.0 / f, 2),
                    megabytesPerSecond = Math.Round(g.Sum(r => r.Value.Bytes) / 1048576.0 / Math.Max(0.1, seconds), 2),
                    calls = g.Sum(r => r.Value.Calls)
                }).ToList();

                return new
                {
                    handlersWrapped = _wrapped,
                    slowCalls = Slow.ToList(),
                    totalMsPerFrame = Math.Round(Rows.Values.Sum(r => r.Ticks) * toMs / f, 3),
                    totalMegabytesPerSecond = Math.Round(Rows.Values.Sum(r => r.Bytes) / 1048576.0 / Math.Max(0.1, seconds), 2),
                    topModsByTime = perMod.OrderByDescending(m => m.msPerFrame).Take(12).ToList(),
                    topModsByBytes = perMod.OrderByDescending(m => m.megabytesPerSecond).Take(12).ToList(),
                    topHandlers = Rows.OrderByDescending(r => r.Value.Ticks).Take(12).Select(r => new
                    {
                        r.Key.Mod,
                        r.Key.Event,
                        msPerFrame = Math.Round(r.Value.Ticks * toMs / f, 3),
                        meanUs = Math.Round(r.Value.Ticks * toMs * 1000.0 / Math.Max(1, r.Value.Calls), 1),
                        kbPerCall = Math.Round(r.Value.Bytes / 1024.0 / Math.Max(1, r.Value.Calls), 2),
                        r.Value.Calls
                    }).ToList()
                };
            }
        }
    }
}
