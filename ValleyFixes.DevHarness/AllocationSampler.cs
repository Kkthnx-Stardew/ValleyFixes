using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. The .NET runtime raises an allocation tick about every 100 KB allocated and names the type that crossed the
    /// line. Counting those by type name gives an unbiased picture of what the main thread allocates. Statistical. Never shipped.
    /// </summary>
    internal sealed class AllocationSampler : EventListener
    {
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static AllocationSampler? _instance;
        private static uint _mainThread;
        private readonly Dictionary<string, long> _bytes = new();
        private readonly Dictionary<string, long> _otherThreads = new();
        private readonly object _gate = new();
        private long _ticks;

        public static void Start()
        {
            _mainThread = GetCurrentThreadId();
            _instance ??= new AllocationSampler();
        }

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!Profiler.Active || e.EventName is null || !e.EventName.StartsWith("GCAllocationTick") || e.Payload is null || e.PayloadNames is null)
                return;

            string type = "?";
            long amount = 100_000;
            for (int i = 0; i < e.PayloadNames.Count; i++)
            {
                if (e.PayloadNames[i] == "TypeName")
                    type = e.Payload[i] as string ?? "?";
                else if (e.PayloadNames[i] == "AllocationAmount64" && e.Payload[i] is long l)
                    amount = l;
            }

            lock (_gate)
            {
                var into = e.OSThreadId == _mainThread ? _bytes : _otherThreads;
                into.TryGetValue(type, out long n);
                into[type] = n + amount;
                _ticks++;
            }
        }

        public static void Reset()
        {
            if (_instance is null)
                return;
            lock (_instance._gate)
            {
                _instance._bytes.Clear();
                _instance._otherThreads.Clear();
                _instance._ticks = 0;
            }
        }

        public static object? Snapshot(double seconds)
        {
            if (_instance is null)
                return null;
            lock (_instance._gate)
            {
                double main = Math.Max(1, _instance._bytes.Values.Sum());
                return new
                {
                    ticks = _instance._ticks,
                    mainThreadMegabytesPerSecond = Math.Round(_instance._bytes.Values.Sum() / 1048576.0 / Math.Max(0.1, seconds), 1),
                    otherThreadsMegabytesPerSecond = Math.Round(_instance._otherThreads.Values.Sum() / 1048576.0 / Math.Max(0.1, seconds), 1),
                    mainThreadTopTypes = _instance._bytes.OrderByDescending(t => t.Value).Take(25).Select(t => new
                    {
                        type = t.Key,
                        sharePercent = Math.Round(t.Value * 100.0 / main, 1),
                        megabytesPerSecond = Math.Round(t.Value / 1048576.0 / Math.Max(0.1, seconds), 2)
                    }).ToList()
                };
            }
        }
    }
}
