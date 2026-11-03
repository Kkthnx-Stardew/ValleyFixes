using System.Diagnostics;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Times and counts allocated bytes of the game's per location background update and ten minute update for each
    /// location by name, and notes what the location holds, so a sample can say whether the cost sits in empty places or busy ones.
    /// </summary>
    internal static class LocationProfiler
    {
        internal struct State
        {
            public long Ticks;
            public long Bytes;
        }

        internal sealed class Row
        {
            public long Ticks, Bytes, Calls, MaxTicks;
            public int Characters, Monsters, Villagers, Sprites, Animals, Buildings, Farmers, Objects;
        }

        private static readonly Dictionary<string, Row> Update = new();
        private static readonly Dictionary<string, Row> Ten = new();
        private static readonly Dictionary<string, Row> Chars = new();
        private static readonly object Gate = new();

        public static void Install(Harmony harmony, IMonitor monitor)
        {
            var update = AccessTools.Method(typeof(GameLocation), "updateEvenIfFarmerIsntHere");
            var ten = AccessTools.Method(typeof(GameLocation), "performTenMinuteUpdate", new[] { typeof(int) });
            if (update is null || ten is null)
            {
                monitor.Log("[locprofiler] a method was not found", LogLevel.Warn);
                return;
            }
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(LocationProfiler), nameof(StartUpdate)), postfix: new HarmonyMethod(typeof(LocationProfiler), nameof(StopUpdate)));
            harmony.Patch(ten, prefix: new HarmonyMethod(typeof(LocationProfiler), nameof(StartTen)), postfix: new HarmonyMethod(typeof(LocationProfiler), nameof(StopTen)));
            var chars = AccessTools.Method(typeof(GameLocation), "updateCharacters", new[] { typeof(Microsoft.Xna.Framework.GameTime) });
            if (chars is not null)
                harmony.Patch(chars, prefix: new HarmonyMethod(typeof(LocationProfiler), nameof(StartChars)), postfix: new HarmonyMethod(typeof(LocationProfiler), nameof(StopChars)));
        }

        private static State Begin() => Profiler.Active ? new State { Ticks = Stopwatch.GetTimestamp(), Bytes = GC.GetAllocatedBytesForCurrentThread() } : default;

        private static void StartUpdate(out State __state) => __state = Begin();
        private static void StartTen(out State __state) => __state = Begin();
        private static void StartChars(out State __state) => __state = Begin();
        private static void StopChars(GameLocation __instance, State __state) => End(Chars, __instance, __state);
        private static void StopUpdate(GameLocation __instance, State __state) => End(Update, __instance, __state);
        private static void StopTen(GameLocation __instance, State __state) => End(Ten, __instance, __state);

        private static void End(Dictionary<string, Row> into, GameLocation loc, State state)
        {
            if (state.Ticks == 0)
                return;
            long ticks = Stopwatch.GetTimestamp() - state.Ticks;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - state.Bytes;
            string name = loc.NameOrUniqueName ?? "?";
            lock (Gate)
            {
                if (!into.TryGetValue(name, out var row))
                    into[name] = row = new Row();
                row.Ticks += ticks;
                row.Bytes += bytes;
                row.Calls++;
                if (ticks > row.MaxTicks)
                    row.MaxTicks = ticks;
                row.Characters = loc.characters.Count;
                row.Monsters = loc.characters.Count(c => c is Monster);
                row.Villagers = loc.characters.Count(c => c.IsVillager);
                row.Sprites = loc.temporarySprites.Count;
                row.Animals = loc.animals.Length;
                row.Buildings = loc.buildings.Count;
                row.Farmers = loc.farmers.Count;
                row.Objects = loc.objects.Count();
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Update.Clear();
                Ten.Clear();
                Chars.Clear();
            }
        }

        private static object Describe(Dictionary<string, Row> rows, int frames, double seconds)
        {
            double toMs = 1000.0 / Stopwatch.Frequency;
            bool Empty(Row r) => r.Characters == 0 && r.Animals == 0 && r.Buildings == 0 && r.Sprites == 0 && r.Farmers == 0;

            object Bucket(IEnumerable<Row> r) => new
            {
                locations = r.Count(),
                msPerFrame = Math.Round(r.Sum(x => x.Ticks) * toMs / Math.Max(1, frames), 3),
                megabytesPerSecond = Math.Round(r.Sum(x => x.Bytes) / 1048576.0 / Math.Max(0.1, seconds), 2),
                calls = r.Sum(x => x.Calls)
            };

            return new
            {
                total = Bucket(rows.Values),
                empty = Bucket(rows.Values.Where(Empty)),
                withCharacters = Bucket(rows.Values.Where(r => r.Characters > 0)),
                withMonsters = Bucket(rows.Values.Where(r => r.Monsters > 0)),
                withAnimals = Bucket(rows.Values.Where(r => r.Animals > 0)),
                withBuildings = Bucket(rows.Values.Where(r => r.Buildings > 0)),
                withSprites = Bucket(rows.Values.Where(r => r.Sprites > 0)),
                topByTime = rows.OrderByDescending(r => r.Value.Ticks).Take(12).Select(r => new
                {
                    location = r.Key,
                    msPerFrame = Math.Round(r.Value.Ticks * toMs / Math.Max(1, frames), 3),
                    meanUs = Math.Round(r.Value.Ticks * toMs * 1000.0 / Math.Max(1, r.Value.Calls), 1),
                    maxMs = Math.Round(r.Value.MaxTicks * toMs, 2),
                    kbPerCall = Math.Round(r.Value.Bytes / 1024.0 / Math.Max(1, r.Value.Calls), 2),
                    r.Value.Characters, r.Value.Monsters, r.Value.Villagers, r.Value.Sprites, r.Value.Animals, r.Value.Buildings, r.Value.Objects
                }).ToList(),
                topByBytes = rows.OrderByDescending(r => r.Value.Bytes).Take(8).Select(r => new
                {
                    location = r.Key,
                    megabytesPerSecond = Math.Round(r.Value.Bytes / 1048576.0 / Math.Max(0.1, seconds), 2),
                    kbPerCall = Math.Round(r.Value.Bytes / 1024.0 / Math.Max(1, r.Value.Calls), 2),
                    r.Value.Characters, r.Value.Monsters, r.Value.Sprites, r.Value.Animals, r.Value.Buildings
                }).ToList()
            };
        }

        public static object Snapshot(int frames, double seconds)
        {
            lock (Gate)
            {
                return new { backgroundUpdate = Describe(Update, frames, seconds), characterLoop = Describe(Chars, frames, seconds), tenMinuteUpdate = Describe(Ten, frames, seconds) };
            }
        }
    }
}
