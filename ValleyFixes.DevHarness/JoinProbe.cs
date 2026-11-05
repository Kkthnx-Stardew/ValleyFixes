using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using HarmonyLib;
using StardewModdingAPI;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Times the steps of a multiplayer join on the game's main thread, and for every step also records how much of it was
    /// JIT compiling, how many garbage collections ran, how many bytes were allocated and how many page faults the process took.
    /// A frame gap watcher records every main thread gap over 100 ms with the same counters, so a stall that no wrapped step covers is still
    /// attributed to JIT, GC, paging or none of them. Never shipped.
    /// </summary>
    internal static class JoinProbe
    {
        internal sealed class Call
        {
            public string At { get; set; } = "";
            public string Label { get; set; } = "";
            public int Depth { get; set; }
            public int Thread { get; set; }
            public double Ms { get; set; }
            public double JitMs { get; set; }
            public long JitMethods { get; set; }
            public int Gc0 { get; set; }
            public int Gc1 { get; set; }
            public int Gc2 { get; set; }
            public double AllocKB { get; set; }
            public long PageFaults { get; set; }
        }

        internal sealed class Gap
        {
            public string At { get; set; } = "";
            public double Ms { get; set; }
            public double JitMs { get; set; }
            public long JitMethods { get; set; }
            public int Gc0 { get; set; }
            public int Gc1 { get; set; }
            public int Gc2 { get; set; }
            public long PageFaults { get; set; }
            public double AllocKB { get; set; }
            public int AvailableMB { get; set; }
            public long WorkingSetMB { get; set; }
            public string Location { get; set; } = "";
        }

        private struct State
        {
            public long Ticks;
            public long JitTicks;
            public long JitMethods { get; set; }
            public int Gc0 { get; set; }
            public int Gc1 { get; set; }
            public int Gc2 { get; set; }
            public long Alloc;
            public long Faults;
            public int Depth { get; set; }
        }

        [ThreadStatic] private static int _depth;
        private static readonly object Gate = new();
        private static readonly List<Call> Calls = new();
        private static readonly Dictionary<string, (long Count, double Ms)> Totals = new();
        private static readonly List<Gap> Gaps = new();
        private static readonly Dictionary<MethodBase, string> Labels = new();
        private static readonly double ToMs = 1000.0 / Stopwatch.Frequency;
        public static double MinRecordMs = 3;

        // Windows process counters, read with one call
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public int cb;
            public int PageFaultCount;
            public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }

        [DllImport("psapi.dll")]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        private static long Faults()
        {
            return GetProcessMemoryInfo(new IntPtr(-1), out var c, Marshal.SizeOf<ProcessMemoryCounters>()) ? c.PageFaultCount : 0;
        }

        private static int AvailableMB()
        {
            var s = new MemoryStatus { dwLength = (uint)Marshal.SizeOf<MemoryStatus>() };
            return GlobalMemoryStatusEx(ref s) ? (int)(s.ullAvailPhys / 1048576) : -1;
        }

        public static void Install(Harmony harmony, IMonitor monitor)
        {
            var asm = typeof(StardewValley.Game1).Assembly;
            Type? T(string n) => asm.GetType(n);
            var server = T("StardewValley.Network.GameServer");
            var multi = T("StardewValley.Multiplayer");
            var game = typeof(StardewValley.Game1);
            var worldState = T("StardewValley.Network.NetWorldState");
            var saveSer = T("StardewValley.SaveSerialization.SaveSerializer");
            var farmer = typeof(StardewValley.Farmer);
            var farmerRoot = T("StardewValley.Network.NetFarmerRoot");

            Wrap(harmony, monitor, server, "checkFarmhandRequest");
            Wrap(harmony, monitor, server, "sendAvailableFarmhands");
            Wrap(harmony, monitor, server, "sendLocation");
            Wrap(harmony, monitor, server, "sendServerIntroduction");
            Wrap(harmony, monitor, server, "updateLobbyData");
            Wrap(harmony, monitor, server, "IsFarmhandAvailable");
            Wrap(harmony, monitor, server, "processIncomingMessage");
            Wrap(harmony, monitor, server, "receiveMessages");
            Wrap(harmony, monitor, server, "onConnect");
            Wrap(harmony, monitor, server, "sendMessage", new[] { typeof(long), T("StardewValley.Network.OutgoingMessage")! });
            Wrap(harmony, monitor, multi, "processIncomingMessage");
            Wrap(harmony, monitor, multi, "addPlayer");
            Wrap(harmony, monitor, multi, "broadcastPlayerIntroduction");
            Wrap(harmony, monitor, multi, "readFarmer");
            // generic methods (writeObjectFullBytes, NetRoot<T>.CreateConnectionPacket) are not wrapped: patching them failed and
            // left the save load broken in run 1. Their cost shows inside sendLocation, sendServerIntroduction and addPlayer.
            Wrap(harmony, monitor, game, "updateCellarAssignments");
            Wrap(harmony, monitor, worldState, "TryAssignFarmhandHome");
            Wrap(harmony, monitor, saveSer, "GetSerializer");
            Wrap(harmony, monitor, farmerRoot, ".ctor");
        }

        private static void Wrap(Harmony harmony, IMonitor monitor, Type? type, string name, Type[]? args = null)
        {
            if (type is null)
            {
                monitor.Log($"[joinprobe] type for {name} not found", LogLevel.Warn);
                return;
            }
            try
            {
                MethodBase? m = name == ".ctor" ? AccessTools.Constructor(type, Type.EmptyTypes) : (args is null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args));
                if (m is null)
                {
                    monitor.Log($"[joinprobe] {type.Name}.{name} not found", LogLevel.Warn);
                    return;
                }
                Patch(harmony, m, $"{type.Name}.{name}");
            }
            catch (Exception ex)
            {
                monitor.Log($"[joinprobe] {type.Name}.{name} skipped: {ex.GetType().Name} {ex.Message}", LogLevel.Warn);
            }
        }

        private static void WrapAny(Harmony harmony, IMonitor monitor, Type type, string name)
        {
            foreach (var m in AccessTools.GetDeclaredMethods(type).Where(x => x.Name == name))
            {
                try
                {
                    Patch(harmony, m, $"{type.Name}.{name}");
                }
                catch (Exception ex)
                {
                    monitor.Log($"[joinprobe] {type.Name}.{name} skipped: {ex.GetType().Name} {ex.Message}", LogLevel.Warn);
                }
            }
        }

        private static void Patch(Harmony harmony, MethodBase m, string label)
        {
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(JoinProbe), nameof(Start)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(JoinProbe), nameof(Stop)) { priority = Priority.Last });
            lock (Gate)
                Labels[m] = label;
        }

        private static void Start(out State __state)
        {
            __state = new State
            {
                Ticks = Stopwatch.GetTimestamp(),
                JitTicks = JitInfo.GetCompilationTime(currentThread: true).Ticks,
                JitMethods = JitInfo.GetCompiledMethodCount(currentThread: true),
                Gc0 = GC.CollectionCount(0),
                Gc1 = GC.CollectionCount(1),
                Gc2 = GC.CollectionCount(2),
                Alloc = GC.GetAllocatedBytesForCurrentThread(),
                Faults = Faults(),
                Depth = _depth++
            };
        }

        private static void Stop(MethodBase __originalMethod, State __state, object[] __args)
        {
            _depth = __state.Depth;
            double ms = (Stopwatch.GetTimestamp() - __state.Ticks) * ToMs;
            string label;
            lock (Gate)
                label = Labels.TryGetValue(__originalMethod, out var l) ? l : __originalMethod.Name;
            if (__args is not null && __args.Length > 0 && __args[0] is StardewValley.Network.IncomingMessage msg)
                label += "#" + msg.MessageType;
            lock (Gate)
            {
                Totals.TryGetValue(label, out var t);
                Totals[label] = (t.Count + 1, t.Ms + ms);
            }
            bool always = label.StartsWith("GameServer.checkFarmhandRequest") || label.StartsWith("GameServer.sendAvailableFarmhands");
            if (ms < MinRecordMs && !always)
                return;
            var call = new Call
            {
                At = DateTime.Now.AddMilliseconds(-ms).ToString("HH:mm:ss.fff"),
                Label = label,
                Depth = __state.Depth,
                Thread = Environment.CurrentManagedThreadId,
                Ms = Math.Round(ms, 1),
                JitMs = Math.Round((JitInfo.GetCompilationTime(currentThread: true).Ticks - __state.JitTicks) / 10000.0, 1),
                JitMethods = JitInfo.GetCompiledMethodCount(currentThread: true) - __state.JitMethods,
                Gc0 = GC.CollectionCount(0) - __state.Gc0,
                Gc1 = GC.CollectionCount(1) - __state.Gc1,
                Gc2 = GC.CollectionCount(2) - __state.Gc2,
                AllocKB = Math.Round((GC.GetAllocatedBytesForCurrentThread() - __state.Alloc) / 1024.0, 0),
                PageFaults = Faults() - __state.Faults
            };
            lock (Gate)
            {
                if (Calls.Count < 4000)
                    Calls.Add(call);
            }
        }

        // ---- frame gaps with process wide counters ------------------------------

        private static long _lastTicks;
        private static long _lastJit, _lastJitMethods, _lastFaults, _lastAlloc;
        private static int _lastGc0, _lastGc1, _lastGc2;
        public static bool GapsOn;

        /// <summary>Call once per update tick on the main thread.</summary>
        public static void Tick(string? location)
        {
            long now = Stopwatch.GetTimestamp();
            long jit = JitInfo.GetCompilationTime(currentThread: false).Ticks;
            long jitMethods = JitInfo.GetCompiledMethodCount(currentThread: false);
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            long alloc = GC.GetTotalAllocatedBytes(false);
            // the pseudo handle -1 is the current process, so the fault count is one cheap call a tick
            long faults = GapsOn ? Faults() : 0;
            double gap = _lastTicks == 0 ? 0 : (now - _lastTicks) * ToMs;
            if (GapsOn && gap > 100)
            {
                var ws = Process.GetCurrentProcess().WorkingSet64 / 1048576;
                lock (Gate)
                {
                    if (Gaps.Count < 400)
                        Gaps.Add(new Gap
                        {
                            At = DateTime.Now.AddMilliseconds(-gap).ToString("HH:mm:ss.fff"),
                            Ms = Math.Round(gap, 0),
                            JitMs = Math.Round((jit - _lastJit) / 10000.0, 0),
                            JitMethods = jitMethods - _lastJitMethods,
                            Gc0 = g0 - _lastGc0,
                            Gc1 = g1 - _lastGc1,
                            Gc2 = g2 - _lastGc2,
                            PageFaults = faults - _lastFaults,
                            AllocKB = Math.Round((alloc - _lastAlloc) / 1024.0, 0),
                            AvailableMB = AvailableMB(),
                            WorkingSetMB = ws,
                            Location = location ?? ""
                        });
                }
            }
            _lastTicks = now;
            _lastJit = jit;
            _lastJitMethods = jitMethods;
            _lastGc0 = g0;
            _lastGc1 = g1;
            _lastGc2 = g2;
            _lastAlloc = alloc;
            _lastFaults = faults;
        }

        public static object Snapshot()
        {
            lock (Gate)
            {
                return new
                {
                    totals = Totals.OrderByDescending(r => r.Value.Ms).Take(40).Select(r => new { label = r.Key, calls = r.Value.Count, totalMs = Math.Round(r.Value.Ms, 1) }).ToList(),
                    calls = Calls.ToList(),
                    gaps = Gaps.ToList()
                };
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Calls.Clear();
                Totals.Clear();
                Gaps.Clear();
            }
        }
    }
}
