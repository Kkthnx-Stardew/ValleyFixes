using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Wraps a short list of the game's own methods in a stopwatch so a sample shows where the frame time goes.
    /// Times are inclusive: a method that calls another patched method includes its time. Never shipped.
    /// </summary>
    internal static class Profiler
    {
        internal sealed class Row
        {
            public long Ticks;
            public long Calls;
            public long MaxTicks;
        }

        private static readonly Dictionary<string, Row> Rows = new();
        private static readonly object Gate = new();
        public static bool Active;

        public static void Install(Harmony harmony, IMonitor monitor)
        {
            Wrap(harmony, monitor, typeof(GameLocation), "updateEvenIfFarmerIsntHere");
            Wrap(harmony, monitor, typeof(GameLocation), "UpdateWhenCurrentLocation");
            Wrap(harmony, monitor, typeof(GameLocation), "updateCharacters");
            Wrap(harmony, monitor, typeof(GameLocation), "draw", new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) });
            Wrap(harmony, monitor, typeof(NPC), "update", new[] { typeof(Microsoft.Xna.Framework.GameTime), typeof(GameLocation) });
            Wrap(harmony, monitor, typeof(FarmAnimal), "updateWhenNotCurrentLocation");
            Wrap(harmony, monitor, typeof(FarmAnimal), "behaviors");
            Wrap(harmony, monitor, typeof(PathFindController), "findPath", new[] { typeof(Microsoft.Xna.Framework.Point), typeof(Microsoft.Xna.Framework.Point), typeof(PathFindController.isAtEnd), typeof(GameLocation), typeof(Character), typeof(int) });
            Wrap(harmony, monitor, typeof(Game1), "performTenMinuteClockUpdate");
            InstallNet(harmony, monitor);
        }

        private static void Wrap(Harmony harmony, IMonitor monitor, Type type, string name, Type[]? args = null)
        {
            var method = args is null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
            if (method is null)
            {
                monitor.Log($"[profiler] {type.Name}.{name} not found", LogLevel.Warn);
                return;
            }
            string label = $"{type.Name}.{name}";
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Profiler), nameof(Start)), postfix: new HarmonyMethod(typeof(Profiler), nameof(Stop)));
            Labels[method] = label;
        }

        private static readonly Dictionary<MethodBase, string> Labels = new();

        private static void Start(out long __state) => __state = Active ? Stopwatch.GetTimestamp() : 0;

        private static void Stop(MethodBase __originalMethod, long __state)
        {
            if (__state == 0)
                return;
            long elapsed = Stopwatch.GetTimestamp() - __state;
            string label = Labels.TryGetValue(__originalMethod, out var l) ? l : __originalMethod.Name;
            lock (Gate)
            {
                if (!Rows.TryGetValue(label, out var row))
                    Rows[label] = row = new Row();
                row.Ticks += elapsed;
                row.Calls++;
                if (elapsed > row.MaxTicks)
                    row.MaxTicks = elapsed;
            }
        }


        // ---- multiplayer traffic: bytes written per message type ----------------

        private static readonly Dictionary<byte, Row> Net = new();

        public static void InstallNet(Harmony harmony, IMonitor monitor)
        {
            var type = AccessTools.TypeByName("StardewValley.Network.LidgrenMessageUtils");
            var write = type is null ? null : AccessTools.Method(type, "WriteMessage");
            if (write is null)
            {
                monitor.Log("[profiler] LidgrenMessageUtils.WriteMessage not found", LogLevel.Warn);
                return;
            }
            harmony.Patch(write, postfix: new HarmonyMethod(typeof(Profiler), nameof(AfterWrite)));
        }

        private static System.Reflection.PropertyInfo? _length;

        private static void AfterWrite(StardewValley.Network.OutgoingMessage srcMsg, object destMsg)
        {
            if (!Active)
                return;
            lock (Gate)
            {
                int length = (int)(_length ??= destMsg.GetType().GetProperty("LengthBytes")!).GetValue(destMsg)!;
                if (!Net.TryGetValue(srcMsg.MessageType, out var row))
                    Net[srcMsg.MessageType] = row = new Row();
                row.Ticks += length;
                row.Calls++;
                if (length > row.MaxTicks)
                    row.MaxTicks = length;
            }
        }

        private static readonly Dictionary<byte, string> TypeNames = new()
        {
            [0] = "farmerDelta", [1] = "serverIntroduction", [2] = "playerIntroduction", [3] = "locationIntroduction", [5] = "warpFarmer",
            [6] = "locationDelta", [7] = "locationSprites", [8] = "characterWarp", [10] = "chatMessage", [12] = "worldDelta", [13] = "teamDelta",
            [14] = "newDaySync", [15] = "chatInfoMessage", [18] = "serverToClientsMessage", [31] = "readySync", [32] = "chestHitSync", [127] = "compressed"
        };

        public static void ResetNet()
        {
            lock (Gate)
                Net.Clear();
        }

        /// <summary>Per message type: total KB, message count, mean bytes and the biggest single message in KB.</summary>
        public static object SnapshotNet(double seconds)
        {
            lock (Gate)
            {
                return Net.OrderByDescending(r => r.Value.Ticks).ToDictionary(
                    r => TypeNames.TryGetValue(r.Key, out var n) ? n : "type" + r.Key,
                    r => (object)new
                    {
                        kbPerSecond = Math.Round(r.Value.Ticks / 1024.0 / Math.Max(0.1, seconds), 2),
                        totalKB = Math.Round(r.Value.Ticks / 1024.0, 1),
                        messages = r.Value.Calls,
                        meanBytes = r.Value.Calls == 0 ? 0 : r.Value.Ticks / r.Value.Calls,
                        maxKB = Math.Round(r.Value.MaxTicks / 1024.0, 1)
                    });
            }
        }

        public static void Reset()
        {
            lock (Gate)
                Rows.Clear();
        }

        /// <summary>Per label: total ms over the sample, calls, mean microseconds per call and the slowest single call in ms.</summary>
        public static object Snapshot(int frames)
        {
            double toMs = 1000.0 / Stopwatch.Frequency;
            lock (Gate)
            {
                return Rows.OrderByDescending(r => r.Value.Ticks).ToDictionary(
                    r => r.Key,
                    r => (object)new
                    {
                        msPerFrame = Math.Round(r.Value.Ticks * toMs / Math.Max(1, frames), 3),
                        calls = r.Value.Calls,
                        meanUs = Math.Round(r.Value.Ticks * toMs * 1000.0 / Math.Max(1, r.Value.Calls), 1),
                        maxMs = Math.Round(r.Value.MaxTicks * toMs, 2)
                    });
            }
        }
    }
}
