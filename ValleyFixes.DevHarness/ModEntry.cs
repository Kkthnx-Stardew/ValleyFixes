using System.Text.Json;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Movies;
using StardewValley.Menus;
using StardewValley.SaveSerialization;
using StardewValley.TerrainFeatures;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Runs the ValleyFixes scenarios inside the real game and writes the results to the file named by VFH_OUT.
    /// It starts a throwaway farm by itself, so no menu clicking is needed. It does nothing when VFH_OUT is not set.
    /// </summary>
    public partial class ModEntry : Mod
    {
        private const int WorldTimeoutTicks = 3600;
        private const int FreezeCapTicks = 1700;

        private string? _out;
        private IEnumerator<int>? _run;
        private int _wait;
        private int _ticks;
        private int _exitAt = -1;
        private bool _muted;
        private readonly Dictionary<string, object?> _results = new();

        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly List<double> _updateMs = new();
        private readonly List<double> _renderMs = new();
        private double _updateStart, _renderStart;
        private bool _sampling;

        public override void Entry(IModHelper helper)
        {
            _out = Environment.GetEnvironmentVariable("VFH_OUT");
            if (string.IsNullOrWhiteSpace(_out))
            {
                Monitor.Log("VFH_OUT is not set, so the harness is idle.", LogLevel.Info);
                return;
            }

            if (Environment.GetEnvironmentVariable("VFH_PROFILE") == "1")
                Profiler.Install(new HarmonyLib.Harmony("Kkthnx.ValleyFixes.DevHarness.Profiler"), Monitor);
            if (Environment.GetEnvironmentVariable("VFH_PROFILE") == "1")
                Profiler.Active = true;
            if (Environment.GetEnvironmentVariable("VFH_PROFILE") == "1")
                ModSampler.Start(Monitor);

            WatchDayEvents(helper);
            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "realnet")
                HookModMessages(helper);
            if (Environment.GetEnvironmentVariable("VFH_JOINPROBE") == "1")
                JoinProbe.Install(new HarmonyLib.Harmony("Kkthnx.ValleyFixes.DevHarness.JoinProbe"), Monitor);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            // per-frame cost: update is UpdateTicking to UpdateTicked, render is Rendering to Rendered
            helper.Events.GameLoop.UpdateTicking += (_, _) => _updateStart = _clock.Elapsed.TotalMilliseconds;
            helper.Events.GameLoop.UpdateTicked += (_, _) =>
            {
                if (_sampling)
                    _updateMs.Add(_clock.Elapsed.TotalMilliseconds - _updateStart);
            };
            helper.Events.Display.Rendering += (_, _) => _renderStart = _clock.Elapsed.TotalMilliseconds;
            helper.Events.Display.Rendered += (_, _) =>
            {
                if (_sampling)
                    _renderMs.Add(_clock.Elapsed.TotalMilliseconds - _renderStart);
            };
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            _ticks++;
            GapTick();
            if (Environment.GetEnvironmentVariable("VFH_JOINPROBE") == "1")
            {
                JoinProbe.GapsOn = Context.IsWorldReady;
                JoinProbe.Tick(Game1.currentLocation?.NameOrUniqueName);
            }
            if (Environment.GetEnvironmentVariable("VFH_PROFILE") == "1" && (_ticks == 10 || _ticks % 600 == 0))
                EventProfiler.Install(Monitor);
            if (_ticks == 12 && Environment.GetEnvironmentVariable("VFH_PROFILE") == "1")
                AllocationSampler.Start();

            // the game skips its whole update while its window is not active, which silently ruins every timed scenario.
            // This is a runtime flag only, it is not written to the saved options.
            if (Game1.options != null && Game1.options.pauseWhenOutOfFocus)
                Game1.options.pauseWhenOutOfFocus = false;

            // volume is set on the audio categories only, so nothing is written to the saved options
            // re-applied every half second, because the game raises the category volumes again when music is switched on
            if ((!_muted || _ticks % 30 == 0) && Game1.musicCategory != null && Game1.soundCategory != null)
            {
                _muted = true;
                Game1.musicCategory.SetVolume(0f);
                Game1.soundCategory.SetVolume(0f);
                Game1.ambientCategory?.SetVolume(0f);
                Game1.footstepCategory?.SetVolume(0f);
            }

            if (_exitAt >= 0)
            {
                if (_ticks >= _exitAt)
                    Environment.Exit(0);
                return;
            }

            if (_run is null)
            {
                if (Game1.activeClickableMenu is TitleMenu title && _ticks > 240)
                {
                    if (Role != "")
                    {
                        if (IsHost)
                        {
                            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VFH_LOAD")))
                                LoadHostFarm(title, Environment.GetEnvironmentVariable("VFH_LOAD")!);
                            else
                                StartHostFarm(title);
                            _run = HostMain().GetEnumerator();
                        }
                        else
                        {
                            _run = ClientMain(title).GetEnumerator();
                        }
                    }
                    else if (Environment.GetEnvironmentVariable("VFH_ONLY") == "realfocus")
                    {
                        LoadSinglePlayer(title, Environment.GetEnvironmentVariable("VFH_LOAD")!);
                        _run = RealFocusMain().GetEnumerator();
                    }
                    else if (Environment.GetEnvironmentVariable("VFH_ONLY") == "realsp")
                    {
                        LoadSinglePlayer(title, Environment.GetEnvironmentVariable("VFH_LOAD")!);
                        _run = RealSinglePlayerMain().GetEnumerator();
                    }
                    else if (Environment.GetEnvironmentVariable("VFH_ONLY") == "title")
                    {
                        _run = TitleScenario(title).GetEnumerator();
                    }
                    else
                    {
                        StartFarm(title);
                        _run = Scenarios();
                    }
                }
                else if (_ticks > 3600)
                    Finish("timeout waiting for the title menu");
                return;
            }

            if (_wait > 0)
            {
                _wait--;
                return;
            }

            try
            {
                if (!_run.MoveNext())
                    Finish(null);
                else
                    _wait = Math.Max(0, _run.Current - 1);
            }
            catch (Exception ex)
            {
                _results["harness_error"] = ex.ToString();
                Finish("harness error");
            }
        }

        private void StartFarm(TitleMenu title)
        {
            Game1.player.Name = "VFTest";
            Game1.player.displayName = "VFTest";
            Game1.player.farmName.Value = "VFTest";
            Game1.player.favoriteThing.Value = "Testing";
            Game1.player.isCustomized.Value = true;
            Game1.player.ConvertClothingOverrideToClothesItems();
            Game1.multiplayerMode = 0;
            title.createdNewCharacter(skipIntro: true);
        }

        private void Finish(string? note)
        {
            _results["note"] = note;
            if (Environment.GetEnvironmentVariable("VFH_JOINPROBE") == "1" && !_results.ContainsKey("joinprobe_end"))
                _results["joinprobe_end"] = JoinProbe.Snapshot();
            _results["mods"] = Helper.ModRegistry.GetAll().Select(m => m.Manifest.UniqueID + " " + m.Manifest.Version).ToList();
            File.WriteAllText(_out!, JsonSerializer.Serialize(_results, new JsonSerializerOptions { WriteIndented = true }));
            Monitor.Log($"Harness results written to {_out}.", LogLevel.Info);
            Game1.quit = true;
            _exitAt = _ticks + 300;
        }

        private IEnumerator<int> Scenarios()
        {
            int waited = 0;
            while (!(Context.IsWorldReady && Context.CanPlayerMove && !Game1.fadeToBlack))
            {
                if (++waited > WorldTimeoutTicks)
                {
                    _results["world"] = "never became ready";
                    yield break;
                }
                yield return 1;
            }
            yield return 120;
            _results["world"] = $"ready in {Game1.player.currentLocation?.NameOrUniqueName}, tick {_ticks}";

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "natural")
            {
                var only = new Dictionary<string, object?>();
                _results["stress"] = only;
                // VFH_HEAVY=1 fills the farm like a late-game one before the first night
                if (Environment.GetEnvironmentVariable("VFH_HEAVY") == "1")
                {
                    only["heavy_farm_objects"] = PopulateHeavyFarm(Game1.getFarm());
                    yield return 30;
                }
                foreach (int w in NaturalDays(only, int.TryParse(Environment.GetEnvironmentVariable("VFH_DAYS"), out int n) ? n : 10)) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "stale")
            {
                var st = new Dictionary<string, object?>();
                _results["stale_participants"] = st;
                foreach (int w in StaleParticipantScenario(st)) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "qibeans")
            {
                var qi = new Dictionary<string, object?>();
                _results["quest_items"] = qi;
                foreach (int w in QuestItemScenario(qi)) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "eatstates")
            {
                var states = new Dictionary<string, object?>();
                _results["eat_states"] = states;
                foreach (int w in EatStatesScenario(states)) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "scale")
            {
                foreach (int w in ScaleScenario()) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "bed")
            {
                foreach (int w in BedScenario()) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "shadows")
            {
                foreach (int w in ShadowScenario()) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "stardrop")
            {
                foreach (int w in StardropScenario()) yield return w;
                yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_ONLY") == "skull")
            {
                var skull = new Dictionary<string, object?>();
                _results["skull"] = skull;
                foreach (int w in SkullScenario(skull)) yield return w;
                yield break;
            }

            Guard("fair", FairScenario);
            Guard("movie", MovieScenario);
            Guard("trash_bear", TrashBearScenario);
            Guard("bundles", BundleScenario);
            Guard("missing_string", MissingStringScenario);
            Guard("names", NameRoundTripScenario);
            Guard("george", GeorgeScenario);

            Farmer p = Game1.player;

            // A: ordinary eating
            ResetPlayer(p);
            Eat(p);
            yield return 3;
            var a = new Dictionary<string, object?> { ["during"] = Snap(p) };
            var series = new List<object>();
            for (int t = 0; t < 25; t++)
            {
                yield return 8;
                series.Add(SnapDetailed(p));
            }
            a["series"] = series;
            a["after"] = Snap(p);
            _results["eat_normal"] = a;

            // B: the animation is cut short by completelyStopAnimatingOrDoingAction, the VF-004 mechanism
            ResetPlayer(p);
            Eat(p);
            yield return 3;
            var b = new Dictionary<string, object?> { ["during"] = Snap(p) };
            p.completelyStopAnimatingOrDoingAction();
            b["justAfterInterrupt"] = Snap(p);
            yield return 60;
            b["after1s"] = Snap(p);
            yield return 300;
            b["after6s"] = Snap(p);
            int recovered = -1;
            for (int t = 0; t < FreezeCapTicks; t++)
            {
                yield return 1;
                if (p.freezePause <= 0 && p.CanMove)
                {
                    recovered = 360 + t;
                    break;
                }
            }
            b["recoveredAfterTicks"] = recovered;
            b["end"] = Snap(p);
            _results["eat_interrupted"] = b;

            // C1: an explosion lands while the farmer is eating
            ResetPlayer(p);
            yield return 70;
            Eat(p);
            yield return 3;
            var c1 = new Dictionary<string, object?> { ["beforeBlast"] = Snap(p) };
            p.currentLocation.explode(p.Tile, 3, p, damageFarmers: true, damage_amount: -1, destroyObjects: false);
            c1["justAfterBlast"] = Snap(p);
            yield return 220;
            c1["after"] = Snap(p);
            _results["eat_blast_while_eating"] = c1;

            // C2: the explosion lands first, then the farmer eats on the same tick
            ResetPlayer(p);
            yield return 70;
            var c2 = new Dictionary<string, object?> { ["beforeBlast"] = Snap(p) };
            p.currentLocation.explode(p.Tile, 3, p, damageFarmers: true, damage_amount: -1, destroyObjects: false);
            Eat(p);
            c2["justAfterBlastAndEat"] = Snap(p);
            yield return 220;
            c2["after"] = Snap(p);
            _results["eat_blast_then_eat"] = c2;

            // D: a real warp starts while the farmer is eating (Game1.performWarpFarmer stops all animation)
            ResetPlayer(p);
            yield return 70;
            Eat(p);
            yield return 20;
            var d = new Dictionary<string, object?> { ["duringEat"] = Snap(p), ["from"] = p.currentLocation.NameOrUniqueName };
            Game1.warpFarmer("Farm", 64, 15, false);
            int warped = -1;
            for (int t = 0; t < 400; t++)
            {
                yield return 1;
                if (p.currentLocation.Name == "Farm" && !Game1.fadeToBlack && !Game1.isWarping)
                {
                    warped = t;
                    break;
                }
            }
            d["warpedAfterTicks"] = warped;
            d["justAfterWarp"] = Snap(p);
            int recoveredD = -1;
            for (int t = 0; t < FreezeCapTicks; t++)
            {
                yield return 1;
                if (p.freezePause <= 0 && p.CanMove)
                {
                    recoveredD = t;
                    break;
                }
            }
            d["recoveredTicksAfterWarp"] = recoveredD;
            d["end"] = Snap(p);
            _results["eat_real_warp"] = d;

            // E: VF-006, mahogany trees felled with and without the Lumberjack profession
            ResetPlayer(p);
            yield return 30;
            var hardwood = new Dictionary<string, object?>();
            foreach (bool lumberjack in new[] { false, true })
            {
                p.professions.Remove(12);
                p.professions.Remove(14);
                if (lumberjack)
                {
                    p.professions.Add(12);
                    p.professions.Add(14);
                }

                GameLocation farm = p.currentLocation;
                farm.debris.Clear();
                int before = CountHardwood(p, farm);
                var placed = PlaceMahoganyTrees(farm, 60);
                foreach (Tree tree in placed)
                {
                    tree.lastPlayerToHit.Value = p.UniqueMultiplayerID;
                    tree.health.Value = 0f;
                    tree.falling.Value = true;
                }

                yield return 420;
                int left = placed.Count(t => farm.terrainFeatures.Values.Contains(t));
                int total = CountHardwood(p, farm) - before;
                hardwood[lumberjack ? "lumberjack" : "no_lumberjack"] = new
                {
                    trees = placed.Count,
                    treesStillStanding = left,
                    hardwoodTotal = total,
                    perTree = placed.Count == 0 ? 0 : Math.Round((double)total / placed.Count, 2)
                };
                farm.debris.Clear();
            }

            p.professions.Remove(12);
            p.professions.Remove(14);
            _results["hardwood"] = hardwood;

            // P: performance sample, idle on the Farm with the player free to move
            ResetPlayer(p);
            yield return 60;
            var frameMs = new List<double>();
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double last = clock.Elapsed.TotalMilliseconds;
            for (int t = 0; t < 1800; t++)
            {
                yield return 1;
                double now = clock.Elapsed.TotalMilliseconds;
                frameMs.Add(now - last);
                last = now;
            }

            double seconds = clock.Elapsed.TotalSeconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            frameMs.Sort();
            _results["perf_idle_farm"] = new
            {
                ticks = frameMs.Count,
                seconds = Math.Round(seconds, 1),
                allocatedMBPerSecond = Math.Round(bytes / 1048576.0 / seconds, 2),
                gen0 = GC.CollectionCount(0) - g0,
                gen1 = GC.CollectionCount(1) - g1,
                gen2 = GC.CollectionCount(2) - g2,
                frameMsMedian = Math.Round(frameMs[frameMs.Count / 2], 2),
                frameMsP99 = Math.Round(frameMs[(int)(frameMs.Count * 0.99)], 2),
                frameMsMax = Math.Round(frameMs[^1], 2),
                framesOver25ms = frameMs.Count(x => x > 25),
                framesOver50ms = frameMs.Count(x => x > 50),
                workingSetMB = Math.Round(Environment.WorkingSet / 1048576.0, 0),
                gcHeapMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 0)
            };

            // S: stress samples. Update and render cost are timed separately, so vsync cannot hide a slow frame.
            var stress = new Dictionary<string, object?>();
            _results["stress"] = stress;

            foreach (int w in Sample(stress, "farm_empty", 900)) yield return w;

            int heavyCount = 0;
            Guard("stress_populate", () => heavyCount = PopulateHeavyFarm(Game1.getFarm()));
            stress["heavy_farm_objects"] = heavyCount;
            yield return 30;
            foreach (int w in Sample(stress, "farm_heavy", 900)) yield return w;

            Game1.warpFarmer("Town", 50, 56, 2);
            for (int t = 0; t < 600 && !(Game1.currentLocation?.Name == "Town" && !Game1.fadeToBlack && !Game1.isWarping); t++) yield return 1;
            yield return 120;
            stress["town_location"] = Game1.currentLocation?.Name;
            stress["town_characters"] = Game1.currentLocation?.characters.Count;
            foreach (int w in Sample(stress, "town", 900)) yield return w;

            Game1.warpFarmer("Mine", 18, 4, 2);
            for (int t = 0; t < 600 && !(Game1.currentLocation?.Name == "Mine" && !Game1.fadeToBlack && !Game1.isWarping); t++) yield return 1;
            yield return 60;
            Game1.enterMine(130);
            for (int t = 0; t < 900 && !(Game1.currentLocation is StardewValley.Locations.MineShaft && !Game1.fadeToBlack && !Game1.isWarping); t++) yield return 1;
            yield return 240;
            stress["mine_location"] = Game1.currentLocation?.Name;
            stress["mine_monsters"] = Game1.currentLocation?.characters.Count(c => c is StardewValley.Monsters.Monster);
            foreach (int w in Sample(stress, "mine_130", 900)) yield return w;

            Game1.warpFarmer("Farm", 64, 15, 2);
            for (int t = 0; t < 900 && !(Game1.currentLocation?.Name == "Farm" && !Game1.fadeToBlack && !Game1.isWarping); t++) yield return 1;
            yield return 120;

            Guard("stress_dayupdate", () =>
            {
                var farm = Game1.getFarm();
                long b = GC.GetAllocatedBytesForCurrentThread();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                farm.DayUpdate(Game1.dayOfMonth);
                sw.Stop();
                stress["farm_heavy_DayUpdate"] = new { ms = Math.Round(sw.Elapsed.TotalMilliseconds, 1), allocatedMB = Math.Round((GC.GetAllocatedBytesForCurrentThread() - b) / 1048576.0, 1) };
            });

            // L: leak check. Many warps, menu opens and day updates, then compare memory after a full collection.
            {
                var farm = Game1.getFarm();
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                long retained0 = GC.GetTotalMemory(true);
                proc.Refresh();
                long ws0 = proc.WorkingSet64;
                var samples = new List<double>();

                for (int round = 0; round < 12; round++)
                {
                    foreach (int w in WarpAndWait("Town", 50, 56)) yield return w;
                    foreach (int w in WarpAndWait("Farm", 64, 15)) yield return w;
                    if (round % 3 == 2)
                        samples.Add(Math.Round(GC.GetTotalMemory(true) / 1048576.0, 1));
                }

                for (int i = 0; i < 60; i++)
                {
                    Game1.activeClickableMenu = new GameMenu();
                    yield return 3;
                    Game1.exitActiveMenu();
                    yield return 3;
                }
                samples.Add(Math.Round(GC.GetTotalMemory(true) / 1048576.0, 1));

                foreach (int w in Hold("before_dayupdates")) yield return w;
                var dayRows = new List<object>();
                for (int i = 0; i < 40; i++)
                {
                    farm.DayUpdate(Game1.dayOfMonth);
                    // let the game run a few frames between updates, as it does overnight, so its per-tick cleanup runs
                    yield return 4;
                    if (i % 10 == 9)
                    {
                        dayRows.Add(new
                        {
                            afterUpdates = i + 1,
                            retainedMB = Math.Round(GC.GetTotalMemory(true) / 1048576.0, 1),
                            objects = farm.objects.Count(),
                            terrainFeatures = farm.terrainFeatures.Count(),
                            debris = farm.debris.Count,
                            temporarySprites = farm.temporarySprites.Count,
                            characters = farm.characters.Count
                        });
                    }
                }
                stress["farm_dayupdate_growth"] = dayRows;
                foreach (int w in Hold("after_dayupdates")) yield return w;

                long retained1 = GC.GetTotalMemory(true);
                proc.Refresh();
                stress["leak_check"] = new
                {
                    warps = 24,
                    menuOpens = 60,
                    farmDayUpdates = 40,
                    retainedMBBefore = Math.Round(retained0 / 1048576.0, 1),
                    retainedMBAfter = Math.Round(retained1 / 1048576.0, 1),
                    retainedMBGrowth = Math.Round((retained1 - retained0) / 1048576.0, 1),
                    retainedMBCheckpoints = samples,
                    workingSetMBBefore = Math.Round(ws0 / 1048576.0, 0),
                    workingSetMBAfter = Math.Round(proc.WorkingSet64 / 1048576.0, 0)
                };
            }

            // N: is the location network change queue ever drained in single player, and what does it hold on to
            {
                var farm = Game1.getFarm();
                var net = new Dictionary<string, object?>();
                stress["net_queue"] = net;
                net["multiplayerMode"] = Game1.multiplayerMode;
                net["isMultiplayer"] = Game1.IsMultiplayer;
                net["farmRootExists"] = farm.Root != null;
                net["farmRootDirty"] = farm.Root?.Dirty;
                net["farmNetFieldsDirty"] = farm.NetFields.Dirty;

                long retained0 = GC.GetTotalMemory(true);
                int queue0 = OutgoingCount(farm.netObjects);
                net["queueObjects_start"] = queue0;

                // churn like the game and the player do: objects appear and are removed again, with frames in between
                var churnTile = new Microsoft.Xna.Framework.Vector2(2, 2);
                for (int round = 0; round < 20; round++)
                {
                    for (int k = 0; k < 100; k++)
                    {
                        var obj = ItemRegistry.Create<StardewValley.Object>("(O)388");
                        obj.TileLocation = churnTile;
                        farm.objects.Add(churnTile, obj);
                        farm.objects.Remove(churnTile);
                    }
                    yield return 2;
                }

                yield return 600; // ten seconds of ordinary frames, so any cleanup the game does on its own has the chance to run
                long retained1 = GC.GetTotalMemory(true);
                int queue1 = OutgoingCount(farm.netObjects);
                net["queueObjects_afterChurnAndTenSeconds"] = queue1;
                net["retainedMB_start"] = Math.Round(retained0 / 1048576.0, 1);
                net["retainedMB_afterChurn"] = Math.Round(retained1 / 1048576.0, 1);

                // the game's own call for "these changes have been sent"
                var sw = System.Diagnostics.Stopwatch.StartNew();
                farm.NetFields.MarkClean();
                sw.Stop();
                net["farmRootMarkCleanMs"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                long retained2 = GC.GetTotalMemory(true);
                net["queueObjects_afterMarkClean"] = OutgoingCount(farm.netObjects);
                net["retainedMB_afterMarkClean"] = Math.Round(retained2 / 1048576.0, 1);
                net["freedMB"] = Math.Round((retained1 - retained2) / 1048576.0, 1);

                var swAll = System.Diagnostics.Stopwatch.StartNew();
                int roots = 0;
                Utility.ForEachLocation(loc =>
                {
                    loc.NetFields.MarkClean();
                    roots++;
                    return true;
                }, includeInteriors: true, includeGenerated: true);
                swAll.Stop();
                net["allLocationsMarkClean"] = new { roots, ms = Math.Round(swAll.Elapsed.TotalMilliseconds, 2) };
            }

            Guard("stress_save", () =>
            {
                long b = GC.GetAllocatedBytesForCurrentThread();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var save = SaveGame.Save();
                while (save.MoveNext()) { }
                sw.Stop();
                stress["farm_heavy_SaveGame_Save"] = new { ms = Math.Round(sw.Elapsed.TotalMilliseconds, 0), allocatedMB = Math.Round((GC.GetAllocatedBytesForCurrentThread() - b) / 1048576.0, 1) };
            });

            // F: VF-012, the real menu's OK check with blank names (built last, it replaces the player's look settings)
            Guard("name_menu", NameMenuScenario);
            Guard("crab_collection", CrabScenario);
            // last, because it switches the game language and back
            Guard("portuguese", LanguageScenario);
        }

        // with VFH_HOLD=1 the run pauses here so a heap dump can be taken from outside, then continues when the go file appears
        private IEnumerable<int> Hold(string name)
        {
            if (Environment.GetEnvironmentVariable("VFH_HOLD") != "1")
                yield break;

            File.WriteAllText(_out + ".hold-" + name, "waiting");
            for (int t = 0; t < 7200 && !File.Exists(_out + ".go-" + name); t++)
                yield return 1;
        }

        // real overnight transitions on a default farm: does the object change queue grow day after day, and does memory follow
        private IEnumerable<int> NaturalDays(Dictionary<string, object?> into, int days)
        {
            var rows = new List<object>();
            into["natural_days"] = rows;

            object Snapshot(int day)
            {
                var farm = Game1.getFarm();
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                return new
                {
                    day,
                    date = $"{Game1.currentSeason} {Game1.dayOfMonth}",
                    farmObjects = farm.objects.Count(),
                    farmTerrainFeatures = farm.terrainFeatures.Count(),
                    objectQueue = OutgoingCount(farm.netObjects),
                    terrainQueue = OutgoingCount(farm.terrainFeatures),
                    all = AllCounts(),
                    retainedMB = Math.Round(GC.GetTotalMemory(true) / 1048576.0, 1),
                    workingSetMB = Math.Round(proc.WorkingSet64 / 1048576.0, 0)
                };
            }

            rows.Add(Snapshot(0));
            for (int day = 1; day <= days; day++)
            {
                Game1.player.isInBed.Value = true;
                Game1.NewDay(0f);
                int guard = 0;
                while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !Context.CanPlayerMove) && guard++ < 4000)
                    yield return 1;
                yield return 180;
                rows.Add(Snapshot(day));
            }

            // save integrity: read back the file the last overnight save wrote and compare the farm with the live one
            var farmLive = Game1.getFarm();
            try
            {
                string folder = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
                var loaded = SaveGame.TryReadSaveFileWithFallback(folder, out string readError, out bool recovered);
                var savedFarm = loaded?.locations.OfType<StardewValley.Farm>().FirstOrDefault();
                into["save_check"] = new
                {
                    readError,
                    recoveredFromBackup = recovered,
                    savedFarmObjects = savedFarm?.objects.Count(),
                    liveFarmObjects = farmLive.objects.Count(),
                    savedFarmTerrainFeatures = savedFarm?.terrainFeatures.Count(),
                    liveFarmTerrainFeatures = farmLive.terrainFeatures.Count(),
                    savedDay = loaded?.dayOfMonth,
                    liveDay = Game1.dayOfMonth
                };
            }
            catch (Exception ex)
            {
                into["save_check_error"] = ex.GetType().Name + ": " + ex.Message;
            }

            // now clear the queues the way the game does after it has "sent" changes, one group at a time to see where the memory is
            var farmNow = Game1.getFarm();
            int objectsBefore = OutgoingCount(farmNow.netObjects), terrainBefore = OutgoingCount(farmNow.terrainFeatures);
            long start = GC.GetTotalMemory(true);

            var swLoc = System.Diagnostics.Stopwatch.StartNew();
            int locations = 0;
            Utility.ForEachLocation(loc =>
            {
                foreach (var field in loc.NetFields.GetFields())
                    field.MarkClean();
                locations++;
                return true;
            }, includeInteriors: true, includeGenerated: true);
            swLoc.Stop();
            long afterLocations = GC.GetTotalMemory(true);

            var swPlayers = System.Diagnostics.Stopwatch.StartNew();
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                foreach (var field in farmer.NetFields.GetFields())
                    field.MarkClean();
            }
            swPlayers.Stop();
            long afterPlayers = GC.GetTotalMemory(true);

            var swWorld = System.Diagnostics.Stopwatch.StartNew();
            foreach (var field in Game1.netWorldState.Value.NetFields.GetFields())
                field.MarkClean();
            foreach (var field in Game1.player.team.NetFields.GetFields())
                field.MarkClean();
            swWorld.Stop();
            long afterWorld = GC.GetTotalMemory(true);

            into["clear_queues"] = new
            {
                locations,
                farmObjectQueue = new { before = objectsBefore, after = OutgoingCount(farmNow.netObjects) },
                farmTerrainQueue = new { before = terrainBefore, after = OutgoingCount(farmNow.terrainFeatures) },
                retainedMBBefore = Math.Round(start / 1048576.0, 1),
                locationsFreedMB = Math.Round((start - afterLocations) / 1048576.0, 1),
                locationsMs = Math.Round(swLoc.Elapsed.TotalMilliseconds, 2),
                playersFreedMB = Math.Round((afterLocations - afterPlayers) / 1048576.0, 1),
                playersMs = Math.Round(swPlayers.Elapsed.TotalMilliseconds, 2),
                worldAndTeamFreedMB = Math.Round((afterPlayers - afterWorld) / 1048576.0, 1),
                worldAndTeamMs = Math.Round(swWorld.Elapsed.TotalMilliseconds, 2),
                retainedMBAfter = Math.Round(afterWorld / 1048576.0, 1)
            };
            rows.Add(Snapshot(-1));
        }

        // title screen: mute the music, open the About screen (the ? button), come back, and see whether the music and the button agree
        private IEnumerable<int> TitleScenario(TitleMenu title)
        {
            var rows = new List<object>();
            _results["title_music"] = rows;

            object Snap(string step) => new
            {
                step,
                musicVolumeLevel = Game1.options.musicVolumeLevel,
                musicPlayerVolume = Game1.musicPlayerVolume,
                buttonIconX = title.muteMusicButton.sourceRect.X,
                buttonShowsMuted = title.muteMusicButton.sourceRect.X == 137,
                startMutedSaved = title.startupPreferences.startMuted,
                subMenu = TitleMenu.subMenu?.GetType().Name,
                song = Game1.currentSong?.Name
            };

            // wait for the title screen to finish its intro
            for (int t = 0; t < 1800 && !(title.HasActiveUser && title.muteMusicButton != null && TitleMenu.subMenu == null && !title.isTransitioningButtons); t++)
                yield return 1;
            yield return 300;

            bool startedMuted = Game1.options.musicVolumeLevel == 0f;
            rows.Add(Snap("start"));
            var mute = title.muteMusicButton.bounds.Center;
            var about = title.aboutButton.bounds.Center;

            // 1. end up muted by clicking the real button (one click first if the game started muted)
            if (startedMuted)
            {
                title.receiveLeftClick(mute.X, mute.Y, playSound: false);
                yield return 30;
                rows.Add(Snap("unmuted first"));
            }
            title.receiveLeftClick(mute.X, mute.Y, playSound: false);
            yield return 30;
            rows.Add(Snap("after mute click"));

            // 2. open About, wait, close it with the back path
            title.receiveLeftClick(about.X, about.Y, playSound: false);
            yield return 30;
            // the real click is ignored until the title buttons finish sliding in, so the menu is opened the way the click does if needed
            bool clickOpened = TitleMenu.subMenu is AboutMenu;
            if (!clickOpened)
                TitleMenu.subMenu = new AboutMenu();
            _results["title_about_opened_by_click"] = clickOpened;
            yield return 90;
            rows.Add(Snap("About open"));
            title.backButtonPressed();
            yield return 180;
            rows.Add(Snap("after About closed"));

            // 3. the same trip once more, to see if it flips again
            TitleMenu.subMenu = new AboutMenu();
            yield return 120;
            title.backButtonPressed();
            yield return 180;
            rows.Add(Snap("after second About trip"));

            // 4. the load menu: its save scan resets the game state the same way. First get back to a state where music and icon agree
            if (Game1.options.musicVolumeLevel != 0f && title.muteMusicButton.sourceRect.X == 137)
                Utility.disableMusic();
            yield return 30;
            rows.Add(Snap("before load menu"));
            TitleMenu.subMenu = new LoadGameMenu();
            yield return 420;
            rows.Add(Snap("load menu open"));
            title.backButtonPressed();
            yield return 180;
            rows.Add(Snap("after load menu closed"));

            // put things back the way they were, through the same real button
            bool nowMuted = title.muteMusicButton.sourceRect.X == 137;
            if (nowMuted != startedMuted)
            {
                title.receiveLeftClick(mute.X, mute.Y, playSound: false);
                yield return 30;
            }
            rows.Add(Snap("restored"));
            _results["title_started_muted"] = startedMuted;
        }

        // VF-046: two finished special orders after their rewards were claimed. One still lists a player id this world does not have
        // (made up, a real deleted farmhand needs a multiplayer farm). The other lists nobody and is the control.
        private IEnumerable<int> StaleParticipantScenario(Dictionary<string, object?> into)
        {
            var team = Game1.player.team;
            var keys = DataLoader.SpecialOrders(Game1.content).Keys.ToList();
            var made = new List<StardewValley.SpecialOrders.SpecialOrder>();
            foreach (string key in keys)
            {
                if (made.Count == 2)
                    break;
                var order = StardewValley.SpecialOrders.SpecialOrder.GetSpecialOrder(key, 1234 + made.Count);
                if (order != null)
                    made.Add(order);
            }
            into["ordersMade"] = made.Select(o => o.questKey.Value).ToList();

            const long fakeId = 999999999999L;
            var stale = made[0];
            var control = made[1];
            foreach (var o in new[] { stale, control })
            {
                o.questState.Value = StardewValley.SpecialOrders.SpecialOrderStatus.Complete;
                o.participants.Clear();
                o.unclaimedRewards.Clear();
            }
            stale.participants[fakeId] = true;
            stale.unclaimedRewards[fakeId] = true;
            team.specialOrders.Add(stale);
            team.specialOrders.Add(control);

            into["before"] = new { staleListed = team.specialOrders.Contains(stale), staleParticipants = stale.participants.Length, controlListed = team.specialOrders.Contains(control), controlParticipants = control.participants.Length };

            // let the game's own per-tick order update run for a while, then a real overnight
            yield return 120;
            into["afterTicks"] = new { staleListed = team.specialOrders.Contains(stale), controlListed = team.specialOrders.Contains(control) };

            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !Context.CanPlayerMove) && guard++ < 4000)
                yield return 1;
            yield return 240;

            into["afterNight"] = new { staleListed = team.specialOrders.Contains(stale), staleParticipants = stale.participants.Length, controlListed = team.specialOrders.Contains(control) };
        }

        // VF-044: Qi Beans in every place the game sweeps, plus ground drops made two ways, then a real overnight
        private IEnumerable<int> QuestItemScenario(Dictionary<string, object?> into)
        {
            Farmer p = Game1.player;
            GameLocation farm = Game1.getFarm();

            int GroundBeans() => farm.debris.Count(d => (d.item?.QualifiedItemId ?? d.itemId.Value) == "(O)890");
            int ItemBeans() => p.Items.Where(i => i?.QualifiedItemId == "(O)890").Sum(i => i.Stack);

            // what is on the ground before: drops made the way crates, trees, monsters and fishing make them
            Game1.createObjectDebris("(O)890", 40, 30, -1, 0, 1f, farm);
            Game1.createMultipleObjectDebris("(O)890", 42, 30, 3, p.UniqueMultiplayerID, farm);
            farm.debris.Add(new Debris(ItemRegistry.Create("(O)890", 2), new Microsoft.Xna.Framework.Vector2(44 * 64, 30 * 64)));
            // control: another item on the ground that must stay
            Game1.createObjectDebris("(O)388", 46, 30, -1, 0, 1f, farm);

            p.addItemToInventoryBool(ItemRegistry.Create("(O)890", 5));
            var chest = new StardewValley.Objects.Chest(true);
            chest.Items.Add(ItemRegistry.Create("(O)890", 4));
            var chestTile = Enumerable.Range(20, 40).Select(x => new Microsoft.Xna.Framework.Vector2(x, 36)).First(t => !farm.objects.ContainsKey(t) && !farm.terrainFeatures.ContainsKey(t));
            chest.TileLocation = chestTile;
            farm.objects.Add(chestTile, chest);

            var shapes = farm.debris.Select(d => new { id = d.itemId.Value, hasItem = d.item != null, stack = d.item?.Stack }).ToList();
            into["groundDebrisBefore"] = shapes;
            into["groundBeansBefore"] = GroundBeans();
            into["inventoryBeansBefore"] = ItemBeans();
            into["chestBeansBefore"] = chest.Items.Sum(i => i?.QualifiedItemId == "(O)890" ? i.Stack : 0);
            into["controlBefore"] = farm.debris.Count(d => (d.item?.QualifiedItemId ?? d.itemId.Value) == "(O)388");

            // the same list the end of the Qi Bean quest adds
            Game1.player.team.itemsToRemoveOvernight.Add("890");
            Game1.player.team.itemsToRemoveOvernight.Add("889");
            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !Context.CanPlayerMove) && guard++ < 4000)
                yield return 1;
            yield return 120;

            farm = Game1.getFarm();
            into["groundBeansAfter"] = GroundBeans();
            into["inventoryBeansAfter"] = ItemBeans();
            into["chestBeansAfter"] = (farm.objects.Values.OfType<StardewValley.Objects.Chest>().SelectMany(c => c.Items).Sum(i => i?.QualifiedItemId == "(O)890" ? i.Stack : 0));
            into["controlAfter"] = farm.debris.Count(d => (d.item?.QualifiedItemId ?? d.itemId.Value) == "(O)388");
            into["groundDebrisAfter"] = farm.debris.Select(d => new { id = d.itemId.Value, hasItem = d.item != null }).ToList();
        }

        // VF-004: eats in states a real player can be in, and reports which ones orphan the 20 second freeze
        private IEnumerable<int> EatStatesScenario(Dictionary<string, object?> into)
        {
            Farmer p = Game1.player;
            GameLocation home = p.currentLocation;

            IEnumerable<int> Run(string name, Action? setup, Action? whileEating = null)
            {
                ResetPlayer(p);
                p.StopSitting(animate: false);
                yield return 60;
                var row = new Dictionary<string, object?>();
                string? setupError = null;
                try { setup?.Invoke(); } catch (Exception ex) { setupError = ex.GetType().Name + ": " + ex.Message; }
                yield return 30;
                row["setupError"] = setupError;
                row["beforeEat"] = SnapDetailed(p);
                Eat(p);
                yield return 3;
                row["duringEat"] = SnapDetailed(p);
                try { whileEating?.Invoke(); } catch (Exception ex) { row["whileEatingError"] = ex.GetType().Name + ": " + ex.Message; }
                yield return 3;
                row["afterAction"] = SnapDetailed(p);
                yield return 200;
                row["after3s"] = SnapDetailed(p);
                int freeAfter = -1;
                for (int t = 0; t < 1500; t++)
                {
                    if (p.freezePause <= 0 && p.CanMove) { freeAfter = 200 + t; break; }
                    yield return 1;
                }
                row["ticksUntilFree"] = freeAfter;
                into[name] = row;
            }

            foreach (int w in Run("plain", null)) yield return w;

            foreach (int w in Run("emote_playing", () => p.doEmote(16))) yield return w;

            foreach (int w in Run("just_hit", () =>
            {
                var slime = new StardewValley.Monsters.GreenSlime(p.Position + new Microsoft.Xna.Framework.Vector2(64, 0), 0);
                p.takeDamage(5, false, slime);
            })) yield return w;

            foreach (int w in Run("hit_while_eating", null, () =>
            {
                var slime = new StardewValley.Monsters.GreenSlime(p.Position + new Microsoft.Xna.Framework.Vector2(64, 0), 0);
                p.takeDamage(5, false, slime);
            })) yield return w;

            foreach (int w in Run("sitting", () =>
            {
                var data = Game1.content.Load<Dictionary<string, string>>("Data/Furniture");
                string id = data.First(kv => kv.Value.Split('/')[1] == "chair").Key;
                var chair = (StardewValley.Objects.Furniture)ItemRegistry.Create("(F)" + id);
                chair.TileLocation = p.Tile + new Microsoft.Xna.Framework.Vector2(0, 1);
                home.furniture.Add(chair);
                chair.AddSittingFarmer(p);
            })) yield return w;
            p.StopSitting(animate: false);
            foreach (var f in home.furniture.ToList()) home.furniture.Remove(f);

            foreach (int w in Run("clock_tick", null, () => { Game1.performTenMinuteClockUpdate(); })) yield return w;

            foreach (int w in Run("item_hold_up", null, () => p.holdUpItemThenMessage(ItemRegistry.Create("(O)388", 1)))) yield return w;

            foreach (int w in Run("warp_started", null, () => Game1.warpFarmer("Farm", 64, 15, false))) yield return w;
            ResetPlayer(p);
            for (int t = 0; t < 600 && Game1.isWarping; t++) yield return 1;
            yield return 120;
        }

        // Skull Cavern floors with their own monsters hunting the player. The player has a huge health bar so the sample is not cut short.
        private IEnumerable<int> SkullScenario(Dictionary<string, object?> into)
        {
            Farmer p = Game1.player;
            p.maxHealth = 20000;
            p.health = 20000;
            p.stamina = p.MaxStamina;
            var floors = new List<object>();
            into["floors"] = floors;

            foreach (int level in new[] { 121, 150, 200, 300 })
            {
                Game1.enterMine(level);
                for (int t = 0; t < 900 && !(Game1.currentLocation is StardewValley.Locations.MineShaft && !Game1.fadeToBlack && !Game1.isWarping); t++)
                    yield return 1;
                yield return 240;

                var shaft = Game1.currentLocation as StardewValley.Locations.MineShaft;
                if (shaft is null)
                {
                    floors.Add(new { level, error = "did not arrive" });
                    continue;
                }

                var kinds = shaft.characters.OfType<StardewValley.Monsters.Monster>().GroupBy(m => m.GetType().Name).ToDictionary(g => g.Key, g => g.Count());
                int healthBefore = p.health;
                var row = new Dictionary<string, object?>();
                foreach (int w in Sample(row, "sample", 900)) yield return w;
                int naturalEnd = shaft.characters.Count(c => c is StardewValley.Monsters.Monster);
                int naturalLost = healthBefore - p.health;

                // a crowd: the floor's own monster generator, 40 of them on open ground within 14 tiles of the player
                var spawn = typeof(StardewValley.Locations.MineShaft).GetMethod("getMonsterForThisLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                int spawned = 0;
                if (spawn != null)
                {
                    var rng = new Random(level);
                    for (int tries = 0; tries < 2000 && spawned < 40; tries++)
                    {
                        var tile = new Microsoft.Xna.Framework.Vector2(p.TilePoint.X + rng.Next(-14, 15), p.TilePoint.Y + rng.Next(-14, 15));
                        if (!shaft.isTileOnMap(tile) || !shaft.isTileLocationOpen(tile) || !shaft.CanSpawnCharacterHere(tile))
                            continue;
                        if (spawn.Invoke(shaft, new object[] { level, (int)tile.X, (int)tile.Y }) is StardewValley.Monsters.Monster monster)
                        {
                            monster.currentLocation = shaft;
                            shaft.characters.Add(monster);
                            spawned++;
                        }
                    }
                }

                yield return 60;
                var crowdKinds = shaft.characters.OfType<StardewValley.Monsters.Monster>().GroupBy(m => m.GetType().Name).ToDictionary(g => g.Key, g => g.Count());
                int healthCrowd = p.health;
                var crowd = new Dictionary<string, object?>();
                foreach (int w in Sample(crowd, "sample", 900)) yield return w;

                floors.Add(new
                {
                    level,
                    location = shaft.Name,
                    monstersAtStart = kinds,
                    monstersAtEnd = naturalEnd,
                    healthLost = naturalLost,
                    sample = row["sample"],
                    crowdSpawned = spawned,
                    crowdMonsters = crowdKinds,
                    crowdHealthLost = healthCrowd - p.health,
                    crowdSample = crowd["sample"]
                });
                p.health = p.maxHealth;
            }

            p.maxHealth = 100;
            p.health = 100;
        }

        // everything the world holds, summed over every location the game knows, and how many unsent change entries wait in all of them
        private static object AllCounts()
        {
            int locations = 0, objects = 0, features = 0, large = 0, debris = 0, queued = 0;
            Utility.ForEachLocation(l =>
            {
                locations++;
                objects += l.objects.Count();
                features += l.terrainFeatures.Count();
                large += l.largeTerrainFeatures.Count;
                debris += l.debris.Count;
                foreach (var field in l.NetFields.GetFields())
                {
                    int q = OutgoingCount(field);
                    if (q > 0)
                        queued += q;
                }
                return true;
            }, includeInteriors: true, includeGenerated: true);
            return new { locations, objects, features, large, debris, queued };
        }

        private static int OutgoingCount(object dictionary)
        {
            for (Type? t = dictionary.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField("outgoingChanges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                    return ((System.Collections.ICollection)field.GetValue(dictionary)!).Count;
            }
            return -1;
        }

        private static IEnumerable<int> WarpAndWait(string location, int x, int y)
        {
            Game1.warpFarmer(location, x, y, 2);
            for (int t = 0; t < 600 && !(Game1.currentLocation?.Name == location && !Game1.fadeToBlack && !Game1.isWarping); t++)
                yield return 1;
            yield return 20;
        }

        private IEnumerable<int> Sample(Dictionary<string, object?> into, string label, int ticks) => SampleBody(into, label, WaitTicks(ticks));

        private static IEnumerable<int> WaitTicks(int ticks)
        {
            yield return ticks;
        }

        /// <summary>Same as <see cref="Sample"/>, but what happens during the sample is any scenario step, not only waiting.</summary>
        private IEnumerable<int> SampleBody(Dictionary<string, object?> into, string label, IEnumerable<int> body)
        {
            _updateMs.Clear();
            _renderMs.Clear();
            Profiler.Reset();
            ModSampler.Reset();
            LocationProfiler.Reset();
            EventProfiler.Reset();
            AllocationSampler.Reset();
            AssetTracer.Reset();
            Profiler.Active = Environment.GetEnvironmentVariable("VFH_PROFILE") == "1";
            long bytes0 = GC.GetAllocatedBytesForCurrentThread();
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var cpu0 = proc.TotalProcessorTime;
            var wall = System.Diagnostics.Stopwatch.StartNew();
            _sampling = true;
            foreach (int w in body)
                yield return w;
            _sampling = false;
            wall.Stop();
            proc.Refresh();
            double cpuMs = (proc.TotalProcessorTime - cpu0).TotalMilliseconds;

            double seconds = wall.Elapsed.TotalSeconds;
            into[label] = new
            {
                seconds = Math.Round(seconds, 1),
                processCpuMsPerFrame = Math.Round(cpuMs / Math.Max(1, _updateMs.Count), 2),
                processCpuPercentOfOneCore = Math.Round(cpuMs / (seconds * 1000.0) * 100, 0),
                update = Stats(_updateMs),
                render = Stats(_renderMs),
                allocatedMBPerSecond = Math.Round((GC.GetAllocatedBytesForCurrentThread() - bytes0) / 1048576.0 / seconds, 2),
                gen0 = GC.CollectionCount(0) - g0,
                gen1 = GC.CollectionCount(1) - g1,
                gen2 = GC.CollectionCount(2) - g2,
                heapMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 0),
                profile = Profiler.Active ? Profiler.Snapshot(_updateMs.Count) : null,
                mods = Profiler.Active ? ModSampler.Snapshot(_updateMs.Count, seconds) : null,
                locations = Profiler.Active ? LocationProfiler.Snapshot(_updateMs.Count, seconds) : null,
                events = Profiler.Active ? EventProfiler.Snapshot(_updateMs.Count, seconds) : null,
                allocTypes = Profiler.Active ? AllocationSampler.Snapshot(seconds) : null,
                assets = Profiler.Active ? AssetTracer.Snapshot(_updateMs.Count, seconds) : null
            };
        }

        private static object Stats(List<double> ms)
        {
            if (ms.Count == 0)
                return new { frames = 0 };
            var sorted = ms.OrderBy(x => x).ToList();
            return new
            {
                frames = sorted.Count,
                meanMs = Math.Round(sorted.Average(), 2),
                medianMs = Math.Round(sorted[sorted.Count / 2], 2),
                p99Ms = Math.Round(sorted[(int)(sorted.Count * 0.99)], 2),
                maxMs = Math.Round(sorted[^1], 2),
                over8ms = sorted.Count(x => x > 8),
                over16ms = sorted.Count(x => x > 16.7)
            };
        }

        // a late-game style farm: crops, fences, machines, sprinklers and trees on every open tile it can find
        private static int PopulateHeavyFarm(GameLocation farm)
        {
            int placed = 0;
            var tiles = new List<Microsoft.Xna.Framework.Vector2>();
            for (int y = 8; y < 62; y++)
            {
                for (int x = 16; x < 78; x++)
                {
                    var tile = new Microsoft.Xna.Framework.Vector2(x, y);
                    if (!farm.terrainFeatures.ContainsKey(tile) && !farm.objects.ContainsKey(tile) && farm.CanItemBePlacedHere(tile))
                        tiles.Add(tile);
                }
            }

            int i = 0;
            for (; i < tiles.Count && placed < 1600; i++)
            {
                var dirt = new HoeDirt(1, farm);
                farm.terrainFeatures.Add(tiles[i], dirt);
                dirt.plant("472", Game1.player, false);
                placed++;
            }
            int fences = 0;
            for (; i < tiles.Count && fences < 700; i++, fences++, placed++)
                farm.objects.Add(tiles[i], new Fence(tiles[i], "322", false));
            int machines = 0;
            for (; i < tiles.Count && machines < 180; i++, machines++, placed++)
            {
                var keg = ItemRegistry.Create<StardewValley.Object>("(BC)12");
                keg.TileLocation = tiles[i];
                farm.objects.Add(tiles[i], keg);
            }
            int sprinklers = 0;
            for (; i < tiles.Count && sprinklers < 250; i++, sprinklers++, placed++)
            {
                var sp = ItemRegistry.Create<StardewValley.Object>("(O)599");
                sp.TileLocation = tiles[i];
                farm.objects.Add(tiles[i], sp);
            }
            int trees = 0;
            for (; i < tiles.Count && trees < 150; i++, trees++, placed++)
                farm.terrainFeatures.Add(tiles[i], new Tree("1", 5));
            return placed;
        }

        private void Guard(string name, Action scenario)
        {
            try
            {
                scenario();
            }
            catch (Exception ex)
            {
                _results[name + "_error"] = ex.ToString();
            }
        }

        // VF-012: asks the real CharacterCustomization menu whether OK is allowed for different typed names
        private void NameMenuScenario()
        {
            Farmer p = Game1.player;
            string name = p.Name, farm = p.farmName.Value, fav = p.favoriteThing.Value;
            var rows = new List<object>();
            try
            {
                var cases = new (string Name, string Farm, string Fav, string Label)[]
                {
                    ("Sam", "Sunny", "Pie", "normal names"),
                    (" ", "Sunny", "Pie", "player name is one space"),
                    ("Sam", " ", "Pie", "farm name is one space"),
                    ("Sam", "Sunny", " ", "favorite thing is one space"),
                    ("", "Sunny", "Pie", "player name empty"),
                    (" Sam ", " Sunny ", "Pie", "names with outer spaces")
                };
                foreach (var source in new[] { CharacterCustomization.Source.NewGame, CharacterCustomization.Source.NewFarmhand, CharacterCustomization.Source.ClothesDye })
                {
                    CharacterCustomization menu;
                    try
                    {
                        menu = new CharacterCustomization(source);
                    }
                    catch (Exception ex)
                    {
                        rows.Add(new { source = source.ToString(), error = ex.GetType().Name + ": " + ex.Message });
                        continue;
                    }

                    foreach (var c in cases)
                    {
                        p.Name = c.Name;
                        p.farmName.Value = c.Farm;
                        p.favoriteThing.Value = c.Fav;
                        rows.Add(new { source = source.ToString(), c.Label, canLeave = menu.canLeaveMenu() });
                    }
                }
            }
            finally
            {
                p.Name = name;
                p.farmName.Value = farm;
                p.favoriteThing.Value = fav;
            }

            _results["name_menu"] = rows;
        }

        // VF-012 and VF-013: pushes names through the game's own save serializer and reads them back
        private void NameRoundTripScenario()
        {
            Farmer p = Game1.player;
            string originalName = p.Name;
            string originalFarm = p.farmName.Value;
            var rows = new List<object>();
            try
            {
                foreach (string typed in new[] { " ", "   ", "", "Test Farm", " Test Farm ", "A B" })
                {
                    // what the menu does: the length check reads the typed text, then the player name is trimmed and the farm name is not
                    bool menuAllowsFarm = typed.Length > 0;
                    string playerNameAfterTrim = typed.Trim();

                    p.farmName.Value = typed;
                    p.Name = playerNameAfterTrim;

                    string xml;
                    using (var ms = new MemoryStream())
                    {
                        var writer = System.Xml.XmlWriter.Create(ms, new System.Xml.XmlWriterSettings { CloseOutput = false });
                        writer.WriteStartDocument();
                        SaveSerializer.Serialize(writer, p);
                        writer.WriteEndDocument();
                        writer.Flush();
                        writer.Close();
                        xml = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                    }

                    var farmTag = Regex.Match(xml, @"<farmName[^>]*?(/>|>[^<]*</farmName>)").Value;
                    var nameTag = Regex.Match(xml, @"<name[ >/][^<]*(</name>)?").Value;

                    Farmer loaded;
                    using (var read = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
                        loaded = SaveSerializer.Deserialize<Farmer>(read);

                    rows.Add(new
                    {
                        typed = $"[{typed}]",
                        menuAllowsFarm,
                        playerNameAfterTrim = $"[{playerNameAfterTrim}]",
                        farmTagWritten = farmTag,
                        farmNameLoadedBack = $"[{loaded.farmName.Value}]",
                        farmNameLoadedLength = loaded.farmName.Value?.Length ?? -1,
                        farmhandCanLeaveMenu = (loaded.farmName.Value?.Length ?? 0) > 0
                    });
                }
            }
            finally
            {
                p.Name = originalName;
                p.farmName.Value = originalFarm;
            }

            _results["name_roundtrip"] = rows;
        }

        private static int CountHardwood(Farmer p, GameLocation loc)
        {
            int inBag = p.Items.Where(i => i?.QualifiedItemId == "(O)709").Sum(i => i.Stack);
            int onGround = loc.debris.Count(d => (d.item?.QualifiedItemId ?? d.itemId.Value) == "(O)709" || d.itemId.Value == "709");
            return inBag + onGround;
        }

        private static List<Tree> PlaceMahoganyTrees(GameLocation loc, int wanted)
        {
            var trees = new List<Tree>();
            for (int y = 22; y <= 58 && trees.Count < wanted; y += 3)
            {
                for (int x = 30; x <= 76 && trees.Count < wanted; x += 3)
                {
                    var tile = new Microsoft.Xna.Framework.Vector2(x, y);
                    if (loc.terrainFeatures.ContainsKey(tile) || !loc.CanItemBePlacedHere(tile))
                        continue;

                    var tree = new Tree("8", 5);
                    loc.terrainFeatures.Add(tile, tree);
                    trees.Add(tree);
                }
            }
            return trees;
        }

        // Collections report: does a crab pot catch of a Crab get recorded the way other crab pot catches are
        private void CrabScenario()
        {
            Farmer p = Game1.player;
            var rows = new List<object>();
            foreach (string id in new[] { "(O)717", "(O)715", "(O)372" })
            {
                bool before = p.fishCaught.ContainsKey(id);
                bool recordedNow = p.caughtFish(id, 5, false, 1);
                bool after = p.fishCaught.ContainsKey(id);
                rows.Add(new { id, before, after, count = p.fishCaught.TryGetValue(id, out var v) ? v[0] : 0 });
                p.fishCaught.Remove(id);
            }
            _results["crab_collection"] = rows;
        }

        // VF-036: switches to Portuguese and reads the same texts the game would, then switches back
        private void LanguageScenario()
        {
            var original = LocalizedContentManager.CurrentLanguageCode;
            try
            {
                LocalizedContentManager.CurrentLanguageCode = LocalizedContentManager.LanguageCode.pt;
                bool gendered = Game1.content.ShouldUseGenderedCharacterTranslations();
                string female = Game1.content.LoadString("Strings\\StringsFromCSFiles:SocialPage_Relationship_Single_Female");
                // the same expression SocialPage.drawFarmerSlot uses
                string shownForMale = gendered ? female.Split('/')[0] : female;
                string shownForFemale = gendered ? female.Split('/').Last() : female;
                string fair = Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1729", 70);
                var data = DataLoader.MoviesReactions(Game1.content);
                string sam = data.First(c => c.NPCName == "Sam").Reactions.First(r => r.Tag == "dislike").SpecialResponses.DuringMovie.Script;
                _results["portuguese"] = new
                {
                    language = LocalizedContentManager.CurrentLanguageCode.ToString(),
                    gendered,
                    singleFemaleKey = female,
                    shownForMaleFarmer = shownForMale,
                    shownForFemaleFarmer = shownForFemale,
                    fairThirdPlace = fair,
                    samDislikeScript = sam
                };
            }
            finally
            {
                LocalizedContentManager.CurrentLanguageCode = original;
            }
        }

        // VF-036: the Items Lost menu builds its OK button from a string key that is not in the string file
        private void MissingStringScenario()
        {
            string missing = Game1.content.LoadString("Strings\\StringsFromCSFiles:ShippingMenu.cs.11382");
            var menu = new ItemListMenu("Items Lost:", new List<Item> { ItemRegistry.Create("(O)195") });
            _results["missing_string"] = new
            {
                loadStringResult = missing,
                okButtonName = menu.okButton.name,
                okButtonHoverText = menu.okButton.hoverText,
                shippingMenuOkKnownGood = Game1.content.LoadString("Strings\\StringsFromCSFiles:ShippingMenu.cs.11382".Replace("11382", "11389"))
            };
        }

        // VF-030: builds many remixed worlds with the game's own generator and records the Chef's bundle reward
        private void BundleScenario()
        {
            var seen = new Dictionary<string, int>();
            int worlds = 120;
            int withChef = 0;
            var classic = new Dictionary<string, int>();
            for (int i = 0; i < worlds; i++)
            {
                Game1.GenerateBundles(Game1.BundleType.Remixed, use_seed: false);
                foreach (var pair in Game1.netWorldState.Value.BundleData)
                {
                    string[] fields = pair.Value.Split('/');
                    if (fields.Length > 1 && fields[0] == "Chef's")
                    {
                        withChef++;
                        seen[fields[1]] = seen.GetValueOrDefault(fields[1]) + 1;
                    }
                }
            }

            Game1.GenerateBundles(Game1.BundleType.Default, use_seed: false);
            foreach (var pair in Game1.netWorldState.Value.BundleData)
            {
                string[] fields = pair.Value.Split('/');
                if (fields.Length > 1 && fields[0] == "Chef's")
                    classic[fields[1]] = 1;
            }

            _results["bundles"] = new { remixedWorlds = worlds, worldsWithChefBundle = withChef, remixedChefRewards = seen, classicChefReward = classic.Keys.ToList() };
        }

        // VF-022: asks the game's own helper for the Trash Bear item in each season across many different worlds
        private void TrashBearScenario()
        {
            ulong original = Game1.uniqueIDForThisGame;
            var sardine = new Dictionary<string, int>();
            var sequences = new Dictionary<string, string>();
            int worlds = 20000;
            try
            {
                foreach (StardewValley.Season season in new[] { StardewValley.Season.Spring, StardewValley.Season.Summer, StardewValley.Season.Fall, StardewValley.Season.Winter })
                {
                    int hits = 0;
                    int draws = 0;
                    var sequence = new List<string>();
                    for (ulong id = 1; id <= (ulong)worlds; id++)
                    {
                        Game1.uniqueIDForThisGame = id * 2654435761UL;
                        // stages 0 and 1 use the seasonal item, later stages switch to a cooked dish (TrashBear.updateItemWanted)
                        for (int stage = 0; stage <= 1; stage++)
                        {
                            string item = Utility.getRandomPureSeasonalItem(season, Utility.CreateRandomSeed(777111.0, stage));
                            draws++;
                            sequence.Add(item);
                            if (item == "131")
                                hits++;
                        }
                    }
                    sardine[season.ToString()] = hits;
                    sardine[season + "_draws"] = draws;
                    if (season == StardewValley.Season.Spring || season == StardewValley.Season.Summer)
                        sequences[season.ToString()] = string.Join(",", sequence);
                }
            }
            finally
            {
                Game1.uniqueIDForThisGame = original;
            }

            _results["trash_bear_sardine"] = sardine;
            _results["trash_bear_sequences"] = sequences;
        }

        // VF-047: how many pixels of George's wheel area in each of the four walking up frames differ from the first frame, as the game loads the sheet
        private void GeorgeScenario()
        {
            var tex = Game1.content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("Characters\\George");
            Microsoft.Xna.Framework.Color[] Read(int column)
            {
                var area = new Microsoft.Xna.Framework.Rectangle(column * 16, 2 * 32 + 18, 16, 14);
                var data = new Microsoft.Xna.Framework.Color[area.Width * area.Height];
                tex.GetData(0, area, data, 0, data.Length);
                return data;
            }

            var first = Read(0);
            var counts = new List<int>();
            for (int c = 0; c < 4; c++)
                counts.Add(Read(c).Where((px, i) => px != first[i]).Count());
            _results["george_up_wheel_pixels_different_from_frame0"] = counts;
        }

        private void FairScenario()
        {
            _results["fair_third_place_text"] = Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1729", 70);

            // regression control: the neighbouring places must not change
            _results["fair_first_place_text"] = Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1723", 95);
            _results["fair_second_place_text"] = Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1726", 80);
            _results["fair_fourth_place_text"] = Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1732", 40);
        }

        private void MovieScenario()
        {
            var data = DataLoader.MoviesReactions(Game1.content);
            var cases = new (string Npc, string Tag, bool Target)[]
            {
                ("Sam", "summer_movie_1", true),
                ("Sam", "dislike", true),
                ("Demetrius", "spring_movie_1", true),
                ("Penny", "family", false),
                ("Kent", "like", false)
            };

            var rows = new List<object>();
            foreach (var (npc, tag, target) in cases)
            {
                var reaction = data.FirstOrDefault(c => c.NPCName == npc)?.Reactions.FirstOrDefault(r => r.Tag == tag && r.SpecialResponses?.DuringMovie != null);
                string? script = reaction?.SpecialResponses?.DuringMovie?.Script;
                if (script is null)
                {
                    rows.Add(new { npc, tag, target, error = "reaction not found" });
                    continue;
                }

                string? shown = null;
                foreach (string command in Event.ParseCommands(script))
                {
                    string[] args = ArgUtility.SplitBySpaceQuoteAware(command);
                    if (args.Length == 0 || args[0] != "message")
                        continue;

                    Game1.exitActiveMenu();
                    Game1.dialogueUp = false;
                    var evt = new Event();
                    Event.DefaultCommands.Message(evt, args, new EventContext(evt, Game1.currentLocation, Game1.currentGameTime, args));
                    shown = (Game1.activeClickableMenu as DialogueBox)?.dialogues.FirstOrDefault();
                    Game1.exitActiveMenu();
                    Game1.dialogueUp = false;
                }

                string? key = Regex.Match(script, @"\[LocalizedText (?<k>[^\]]+)\]").Groups["k"].Value;
                string expected = Game1.content.LoadString(key);
                // the dialogue box wraps text word by word, so compare with the spacing collapsed
                string Flat(string? text) => Regex.Replace(text ?? "", @"\s+", " ").Trim();
                rows.Add(new { npc, tag, target, script, shown = Flat(shown), expected = Flat(expected), shownIsExpected = Flat(shown) == Flat(expected) });
            }

            _results["movie"] = rows;
        }

        private void ResetPlayer(Farmer p)
        {
            p.forceCanMove();
            p.temporarilyInvincible = false;
            p.health = p.maxHealth;
            p.stamina = p.MaxStamina;
        }

        private void Eat(Farmer p)
        {
            // the game sets these when the player confirms "Eat?", and eatHeldObject needs them
            var food = ItemRegistry.Create("(O)195");
            p.Items[0] = food;
            p.CurrentToolIndex = 0;
            p.mostRecentlyGrabbedItem = food;
            p.itemToEat = food;
            p.eatHeldObject();
        }

        private object SnapDetailed(Farmer p)
        {
            return new
            {
                tick = _ticks,
                p.isEating,
                p.freezePause,
                p.CanMove,
                pauseForSingle = p.FarmerSprite.PauseForSingleAnimation,
                frame = p.FarmerSprite.CurrentFrame,
                heldItem = p.mostRecentlyGrabbedItem?.Name,
                activeItem = p.ActiveObject?.Name,
                menu = Game1.activeClickableMenu?.GetType().Name,
                p.UsingTool,
                eventUp = Game1.eventUp
            };
        }

        private object Snap(Farmer p)
        {
            return new { tick = _ticks, p.isEating, p.freezePause, p.CanMove, p.health, p.temporarilyInvincible };
        }
    }
}
