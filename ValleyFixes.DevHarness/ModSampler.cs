using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using StardewModdingAPI;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. SMAPI keeps a stack of the mod whose event handler it is running (<c>Context.HeuristicModsRunningCode</c>).
    /// A background thread reads the top of that stack about every millisecond and counts it per mod, so a sample can say how much of
    /// the time the main thread spent inside which mod's handlers, with no change to any mod. Time spent in the game itself, or in a
    /// Harmony patch a mod added to a game method, counts as the game. Statistical, never shipped.
    /// </summary>
    internal static class ModSampler
    {
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        private static readonly Dictionary<string, long> Counts = new();
        private static readonly Dictionary<object, string> Names = new(ReferenceEqualityComparer.Instance);
        private static readonly object Gate = new();
        private static Thread? _thread;
        private static volatile bool _run;
        private static IEnumerable? _stack;
        private static PropertyInfo? _manifest;
        private static PropertyInfo? _uniqueId;
        private static long _samples;
        private static long _failed;
        private static readonly Stopwatch Clock = new();

        public static bool Start(IMonitor monitor)
        {
            if (_thread is not null)
                return true;

            var prop = typeof(Context).GetProperty("HeuristicModsRunningCode", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            _stack = prop?.GetValue(null) as IEnumerable;
            if (_stack is null)
            {
                monitor.Log("[sampler] Context.HeuristicModsRunningCode was not found, so there is no per mod sampling", LogLevel.Warn);
                return false;
            }

            timeBeginPeriod(1);
            _run = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "VFH mod sampler", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
            return true;
        }

        private static void Loop()
        {
            while (_run)
            {
                Thread.Sleep(1);
                if (!Profiler.Active)
                    continue;

                string name = "(game and vanilla code)";
                try
                {
                    foreach (object top in _stack!)
                    {
                        name = NameOf(top);
                        break;
                    }
                }
                catch
                {
                    // the stack changed while it was read, so this sample is dropped
                    Interlocked.Increment(ref _failed);
                    continue;
                }

                lock (Gate)
                {
                    Counts.TryGetValue(name, out long n);
                    Counts[name] = n + 1;
                    _samples++;
                }
            }
        }

        private static string NameOf(object mod)
        {
            lock (Gate)
            {
                if (Names.TryGetValue(mod, out var cached))
                    return cached;
            }

            _manifest ??= mod.GetType().GetProperty("Manifest");
            object? manifest = _manifest?.GetValue(mod);
            _uniqueId ??= manifest?.GetType().GetProperty("UniqueID");
            string name = _uniqueId?.GetValue(manifest) as string ?? mod.GetType().Name;
            lock (Gate)
                Names[mod] = name;
            return name;
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Counts.Clear();
                _samples = 0;
            }
            Interlocked.Exchange(ref _failed, 0);
            Clock.Restart();
        }

        /// <summary>Per mod: share of samples, and the milliseconds a frame that share comes to. The first row is everything outside mod handlers.</summary>
        public static object Snapshot(int frames, double wallSeconds)
        {
            lock (Gate)
            {
                double total = Math.Max(1, _samples);
                double msPerFrameWall = frames <= 0 ? 0 : wallSeconds * 1000.0 / frames;
                return new
                {
                    samples = _samples,
                    droppedSamples = Interlocked.Read(ref _failed),
                    samplesPerSecond = Math.Round(_samples / Math.Max(0.001, wallSeconds), 0),
                    byMod = Counts.OrderByDescending(c => c.Value).Take(25).ToDictionary(
                        c => c.Key,
                        c => (object)new
                        {
                            sharePercent = Math.Round(c.Value * 100.0 / total, 2),
                            msPerFrame = Math.Round(c.Value / total * msPerFrameWall, 3)
                        })
                };
            }
        }
    }
}
