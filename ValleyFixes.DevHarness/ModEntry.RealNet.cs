using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Multiplayer on a copy of a real played save with the real mod list. Needs -Profile for the byte counts.
    /// Phases, each timed on both sides with bytes written per message type, frame cost and frame gaps:
    /// the join, the joiner's first walk onto the heavy farm, idle together, walking together, leaving and re-entering the farm, one night.
    /// </summary>
    public partial class ModEntry
    {
        // wall clock gap between two update ticks, in ms. A normal 60 fps frame is about 16.7
        private readonly List<float> _gaps = new();
        private readonly List<object> _bigGaps = new();
        private double _lastGapAt = -1;
        private bool _gapOn;

        private void GapTick()
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (_gapOn && _lastGapAt >= 0)
            {
                float gap = (float)(now - _lastGapAt);
                _gaps.Add(gap);
                if (gap > 60 && _bigGaps.Count < 60)
                    _bigGaps.Add(new { at = DateTime.Now.ToString("HH:mm:ss.fff"), ms = Math.Round(gap, 0), location = Game1.currentLocation?.NameOrUniqueName });
            }
            _lastGapAt = now;
        }

        // mod messages the other side sent to this process, by sender mod and message type. Size is the JSON text length, so it is near the bytes on the wire
        private readonly Dictionary<string, (int Count, long Chars)> _modMsgs = new();

        private void HookModMessages(IModHelper helper)
        {
            helper.Events.Multiplayer.ModMessageReceived += (_, e) =>
            {
                long chars = 0;
                try { chars = e.ReadAs<Newtonsoft.Json.Linq.JToken>().ToString(Newtonsoft.Json.Formatting.None).Length; } catch { }
                string key = e.FromModID + " | " + e.Type;
                lock (_modMsgs)
                {
                    _modMsgs.TryGetValue(key, out var row);
                    _modMsgs[key] = (row.Count + 1, row.Chars + chars);
                }
            };
        }

        private object ModMsgSnapshot(bool reset)
        {
            lock (_modMsgs)
            {
                var rows = _modMsgs.OrderByDescending(r => r.Value.Chars).Take(12).Select(r => new { message = r.Key, count = r.Value.Count, totalKB = Math.Round(r.Value.Chars / 1024.0, 1), meanChars = r.Value.Count == 0 ? 0 : r.Value.Chars / r.Value.Count }).ToList();
                if (reset)
                    _modMsgs.Clear();
                return rows;
            }
        }

        /// <summary>Memory without forcing a collection, so a sample is not disturbed. The forced version stalls the game for a second on a heap this size.</summary>
        private object MemoryLight()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var info = GC.GetGCMemoryInfo();
            return new
            {
                retainedMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                gcHeapMB = Math.Round(info.HeapSizeBytes / 1048576.0, 0),
                gcCommittedMB = Math.Round(info.TotalCommittedBytes / 1048576.0, 0),
                gcFragmentedMB = Math.Round(info.FragmentedBytes / 1048576.0, 0),
                privateMB = Math.Round(proc.PrivateMemorySize64 / 1048576.0, 0),
                workingSetMB = Math.Round(proc.WorkingSet64 / 1048576.0, 0)
            };
        }

        private object MemoryForced()
        {
            var forced = MemoryNow();
            _lastGapAt = -1; // the collection above is not a hitch of the game, so the next gap is not counted
            return new { forced, light = MemoryLight() };
        }

        private void StartGaps()
        {
            _gaps.Clear();
            _bigGaps.Clear();
            _gapOn = true;
        }

        private object GapStats()
        {
            if (_gaps.Count == 0)
                return new { frames = 0 };
            var sorted = _gaps.OrderBy(x => x).ToList();
            return new
            {
                frames = sorted.Count,
                meanMs = Math.Round(sorted.Average(), 2),
                medianMs = Math.Round(sorted[sorted.Count / 2], 2),
                p99Ms = Math.Round(sorted[(int)(sorted.Count * 0.99)], 2),
                maxMs = Math.Round(sorted[^1], 1),
                over40ms = sorted.Count(x => x > 40),
                over100ms = sorted.Count(x => x > 100),
                over500ms = sorted.Count(x => x > 500),
                top5 = sorted.TakeLast(5).Reverse().Select(x => Math.Round(x, 0)).ToList(),
                bigGaps = _bigGaps.ToList()
            };
        }

        /// <summary>One side does the step, the other waits for it. Both then stand still for a few seconds so late messages land inside the phase.</summary>
        private IEnumerable<int> ActOrWatch(string name, bool actor, IEnumerable<int> body)
        {
            if (actor)
            {
                foreach (int w in body)
                    yield return w;
                yield return 300;
                Mark(name + "-done");
            }
            else
            {
                for (int t = 0; t < 14400 && !OtherHas(name + "-done"); t += 5)
                    yield return 5;
                yield return 300;
            }
        }

        private IEnumerable<int> NetPhase(Dictionary<string, object?> into, string label, string actorRole, IEnumerable<int> actorBody)
        {
            Profiler.ResetNet();
            ModMsgSnapshot(reset: true);
            StartGaps();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var inner = new Dictionary<string, object?>();
            foreach (int w in SampleBody(inner, "sample", ActOrWatch(label, actorRole == "both" || actorRole == Role, actorBody)))
                yield return w;
            _gapOn = false;
            double seconds = clock.Elapsed.TotalSeconds;
            into[label] = new
            {
                actor = actorRole,
                seconds = Math.Round(seconds, 1),
                net = Profiler.SnapshotNet(seconds),
                modNet = Profiler.SnapshotModNet(seconds),
                gaps = GapStats(),
                modMessagesReceived = ModMsgSnapshot(reset: true),
                sample = inner["sample"],
                memory = MemoryLight()
            };
            foreach (int w in Barrier(label + "-end", 14400)) yield return w;
        }

        private static IEnumerable<int> Idle(int ticks)
        {
            yield return ticks;
        }

        private IEnumerable<int> WalkAbout(int ticks)
        {
            for (int i = 0; i < ticks; i += 6)
            {
                Game1.player.faceDirection((i / 6) % 4);
                Game1.player.Position += new Vector2(((i / 6) % 2 == 0) ? 12f : -12f, 0);
                yield return 6;
            }
        }

        private IEnumerable<int> RealNetScenario()
        {
            var rows = new Dictionary<string, object?>();
            _results["realnet"] = rows;
            Profiler.Active = true;

            rows["role"] = Role;
            rows["world"] = WorldFacts();
            // the host's frame gaps cover everything since its world was ready, so they include the other process starting up and joining
            rows["gaps_since_host_ready"] = IsHost ? GapStats() : null;
            _gapOn = false;
            // everything since the process started: for the host that is the join, for the joiner the join as seen from its side
            rows["join_and_load"] = new { bytesByType = Profiler.SnapshotNet(1), modNet = Profiler.SnapshotModNet(1), modMessagesReceived = ModMsgSnapshot(reset: true), slowHandlers = EventProfiler.Snapshot(1, 1) };
            if (Environment.GetEnvironmentVariable("VFH_JOINPROBE") == "1")
                rows["joinprobe"] = JoinProbe.Snapshot();
            rows["memory_light_after_join"] = MemoryLight();
            rows["memory_after_join"] = MemoryForced();
            foreach (int w in Barrier("rn-joined", 14400)) yield return w;

            // the joiner's first walk out of the cabin onto the real farm: the farm is sent over the wire for the first time
            // the host picks the open tile, because the joiner's Farm map may not exist yet
            Point spot = new Point(64, 15);
            if (IsHost)
            {
                spot = FindOpenTile(Game1.getFarm());
                Publish("rn-spot", new { spot.X, spot.Y });
                foreach (int w in WarpAndWait("Farm", spot.X, spot.Y)) yield return w;
            }
            foreach (int w in Barrier("rn-hostonfarm", 14400)) yield return w;
            if (!IsHost && Fetch("rn-spot") is System.Text.Json.JsonElement e)
                spot = new Point(e.GetProperty("X").GetInt32(), e.GetProperty("Y").GetInt32());
            rows["spot"] = $"{spot.X},{spot.Y}";

            foreach (int w in NetPhase(rows, "first_farm_entry", "client", IsHost ? Idle(1) : WarpAndWait("Farm", spot.X + 1, spot.Y))) yield return w;
            foreach (int w in NetPhase(rows, "idle_together_30s", "both", Idle(1800))) yield return w;
            foreach (int w in NetPhase(rows, "walking_together_15s", "both", WalkAbout(900))) yield return w;
            foreach (int w in NetPhase(rows, "client_to_town", "client", IsHost ? Idle(1) : WarpAndWait("Town", 52, 55))) yield return w;
            foreach (int w in NetPhase(rows, "client_back_to_farm", "client", IsHost ? Idle(1) : WarpAndWait("Farm", spot.X + 1, spot.Y))) yield return w;

            // each player goes to their own bed: the host's farmhouse or the joiner's cabin
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            rows["in_bed_" + Role] = inBed;
            foreach (int w in Barrier("rn-home", 14400)) yield return w;

            foreach (int w in NetPhase(rows, "one_night", "both", SleepThroughNight())) yield return w;
            rows["date_after_night"] = $"{Game1.currentSeason} {Game1.dayOfMonth} y{Game1.year}";
            rows["memory_end"] = MemoryForced();
            var types = new Dictionary<string, int>();
            foreach (var f in typeof(StardewValley.Multiplayer).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (f.IsLiteral && f.FieldType == typeof(byte) && f.GetRawConstantValue() is byte b)
                    types[f.Name] = b;
            }
            rows["message_type_numbers"] = types;
            rows["mod_message_heads"] = Profiler.ModMessageHeads();
        }
    }
}
