using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Two player scenarios. One game process is the host (VFH_ROLE=host) and one joins it (VFH_ROLE=client). They talk through
    /// small files in the VFH_SYNC folder, so each side runs its half of a scenario and waits at named barriers for the other.
    /// </summary>
    public partial class ModEntry
    {
        private static string Role => Environment.GetEnvironmentVariable("VFH_ROLE") ?? "";
        private static bool IsHost => Role == "host";
        private static bool HostOnly => Environment.GetEnvironmentVariable("VFH_HOSTONLY") == "1";
        private static string OtherRole => IsHost ? "client" : "host";

        private string SyncDir => Environment.GetEnvironmentVariable("VFH_SYNC") ?? Path.GetDirectoryName(_out)!;
        private string SyncFile(string name, string role) => Path.Combine(SyncDir, name + "." + role);

        private void Mark(string name) => File.WriteAllText(SyncFile(name, Role), DateTime.Now.ToString("O"));

        private bool OtherHas(string name) => File.Exists(SyncFile(name, OtherRole));

        /// <summary>Waits until the other process has reached the same named point. Gives up after about two minutes of game time.</summary>
        private IEnumerable<int> Barrier(string name, int limitTicks = 7200)
        {
            Mark(name);
            for (int t = 0; t < limitTicks && !OtherHas(name); t += 5)
                yield return 5;
            if (!OtherHas(name))
                _results["barrier_timeout_" + name] = true;
        }

        private void Publish(string name, object data) => File.WriteAllText(SyncFile(name + "-data", Role), JsonSerializer.Serialize(data));

        private JsonElement? Fetch(string name)
        {
            string file = SyncFile(name + "-data", OtherRole);
            return File.Exists(file) ? JsonDocument.Parse(File.ReadAllText(file)).RootElement.Clone() : null;
        }

        private void StartHostFarm(TitleMenu title)
        {
            Game1.player.Name = "VFTest";
            Game1.player.displayName = "VFTest";
            Game1.player.farmName.Value = "VFTest";
            Game1.player.favoriteThing.Value = "Testing";
            Game1.player.isCustomized.Value = true;
            Game1.player.ConvertClothingOverrideToClothesItems();
            Game1.options.ipConnectionsEnabled = true;
            Game1.multiplayerMode = 2;
            title.createdNewCharacter(skipIntro: true);
        }

        // loads a world an earlier run kept, as a host that other players can join
        private void LoadHostFarm(TitleMenu title, string saveFolder)
        {
            title.skipToTitleButtons();
            Game1.options.ipConnectionsEnabled = true;
            Game1.multiplayerMode = 2;
            SaveGame.Load(saveFolder);
            Game1.exitActiveMenu();
        }

        private IEnumerable<int> HostMain()
        {
            int waited = 0;
            while (!(Context.IsWorldReady && Context.CanPlayerMove && !Game1.fadeToBlack))
            {
                if (++waited > WorldTimeoutTicks)
                {
                    _results["world"] = "host never became ready";
                    yield break;
                }
                yield return 1;
            }
            yield return 120;
            _results["world"] = $"host ready in {Game1.player.currentLocation?.NameOrUniqueName}, multiplayerMode {Game1.multiplayerMode}, server {(Game1.server != null)}";

            Cabin? cabin = PrepareCabin();
            _results["cabin"] = cabin is null ? "none" : "ready for " + cabin.owner?.UniqueMultiplayerID;
            if (cabin is null)
                yield break;
            Mark("hostready");

            if (!HostOnly)
            {
                for (int t = 0; t < 9000 && Game1.otherFarmers.Count == 0; t += 15)
                    yield return 15;
                _results["clientJoined"] = Game1.otherFarmers.Count > 0;
                yield return 300;
            }

            foreach (int w in RunScenario(Environment.GetEnvironmentVariable("VFH_ONLY") ?? "join")) yield return w;
        }

        private IEnumerable<int> ClientMain(TitleMenu title)
        {
            title.skipToTitleButtons();
            for (int t = 0; t < 9000 && !OtherHas("hostready"); t += 30)
                yield return 30;
            yield return 120;

            var multiplayer = Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            var client = multiplayer.InitClient(new LidgrenClient("localhost"));
            var menu = new FarmhandMenu(client);
            TitleMenu.subMenu = menu;

            int sinceTry = 9999;
            for (int t = 0; t < 9000 && !(Context.IsWorldReady && Context.CanPlayerMove); t += 15)
            {
                sinceTry += 15;
                if (menu.MenuSlots.Count > 0 && sinceTry > 300)
                {
                    sinceTry = 0;
                    menu.MenuSlots[0].Activate();
                }
                yield return 15;
            }
            if (!Context.IsWorldReady)
            {
                _results["world"] = "client never reached the world";
                yield break;
            }
            yield return 240;
            _results["world"] = $"client ready in {Game1.player.currentLocation?.NameOrUniqueName}, id {Game1.player.UniqueMultiplayerID}";

            foreach (int w in RunScenario(Environment.GetEnvironmentVariable("VFH_ONLY") ?? "join")) yield return w;
        }

        /// <summary>Builds a cabin and a customized, never joined farmhand in it, the way a host who invited a friend would have.</summary>
        private Cabin? PrepareCabin()
        {
            var farm = Game1.getFarm();
            var cabin = farm.buildings.Select(b => b.GetIndoors() as Cabin).FirstOrDefault(c => c is not null);
            if (cabin is null)
            {
                foreach (var tile in new[] { new Vector2(58, 20), new Vector2(52, 20), new Vector2(46, 20), new Vector2(40, 22), new Vector2(60, 28), new Vector2(66, 28) })
                {
                    if (farm.buildStructure("Cabin", tile, Game1.player, out var built, magicalConstruction: true, skipSafetyChecks: true))
                    {
                        cabin = built.GetIndoors() as Cabin;
                        break;
                    }
                }
            }
            if (cabin is null)
                return null;

            if (!cabin.HasOwner)
                cabin.CreateFarmhand();
            var hand = cabin.owner;
            if (hand is not null && !hand.isCustomized.Value)
            {
                hand.isCustomized.Value = true;
                hand.Name = "Client";
                hand.displayName = "Client";
                hand.farmName.Value = Game1.player.farmName.Value;
                hand.hair.Value = 2;
                hand.skin.Value = 1;
            }
            return cabin;
        }

        private IEnumerable<int> RunScenario(string name)
        {
            switch (name)
            {
                case "join":
                    foreach (int w in JoinScenario()) yield return w;
                    break;
                case "perf":
                    foreach (int w in PerfScenario()) yield return w;
                    break;
                case "soak":
                    foreach (int w in SoakScenario()) yield return w;
                    break;
                case "roundtrip":
                    foreach (int w in RoundTripScenario()) yield return w;
                    break;
                case "gold":
                    foreach (int w in GoldScenario()) yield return w;
                    break;
                case "makeover":
                    foreach (int w in MakeoverScenario()) yield return w;
                    break;
                case "goldreload":
                    foreach (int w in GoldReloadScenario()) yield return w;
                    break;
                case "cart":
                    foreach (int w in CartScenario()) yield return w;
                    break;
                default:
                    _results["scenario_error"] = "unknown scenario " + name;
                    break;
            }
            if (!HostOnly && name != "roundtrip")
            {
                foreach (int w in Barrier("done")) yield return w;
            }
        }

        // ---- shared helpers -------------------------------------------------

        private object MemoryNow()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            return new { retainedMB = Math.Round(GC.GetTotalMemory(true) / 1048576.0, 1), workingSetMB = Math.Round(proc.WorkingSet64 / 1048576.0, 0) };
        }

        private static List<object> QueuedByLocation()
        {
            var rows = new List<object>();
            Utility.ForEachLocation(l =>
            {
                int queued = 0;
                foreach (var field in l.NetFields.GetFields())
                {
                    int q = OutgoingCount(field);
                    if (q > 0)
                        queued += q;
                }
                if (queued > 0)
                    rows.Add(new { name = l.NameOrUniqueName, queued, hasRoot = l.Root != null, structure = l.isStructure.Value, farmersHere = l.farmers.Count });
                return true;
            }, includeInteriors: true, includeGenerated: true);
            return rows;
        }

        private object WorldNow()
        {
            var farm = Game1.getFarm();
            return new
            {
                date = $"{Game1.currentSeason} {Game1.dayOfMonth} y{Game1.year}",
                online = Game1.getOnlineFarmers().Count,
                farmObjects = farm.objects.Count(),
                farmTerrainFeatures = farm.terrainFeatures.Count(),
                objectQueue = OutgoingCount(farm.netObjects),
                terrainQueue = OutgoingCount(farm.terrainFeatures),
                all = AllCounts(),
                queuedByLocation = QueuedByLocation(),
                farmRoot = farm.Root != null
            };
        }

        /// <summary>Goes to bed the way the game does, waits for every player to be ready and the new day to start, and returns once the player can move.</summary>
        private IEnumerable<int> SleepThroughNight()
        {
            int day0 = Game1.Date.TotalDays;
            var p = Game1.player;
            p.isInBed.Value = true;
            var start = typeof(GameLocation).GetMethod("startSleep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            start!.Invoke(Game1.currentLocation, null);

            for (int t = 0; t < 9000 && Game1.Date.TotalDays == day0; t += 5)
            {
                if (t % 600 == 0)
                    Monitor.Log($"[harness] sleeping: t={t} loc={Game1.currentLocation?.NameOrUniqueName} inBed={p.isInBed.Value} time={Game1.timeOfDay} menu={Game1.activeClickableMenu?.GetType().Name} fade={Game1.fadeToBlack} newDay={Game1.newDay} ready={Game1.player.team.sleepAnnounceMode.Value}", LogLevel.Info);
                yield return 5;
            }
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !Context.CanPlayerMove) && guard++ < 6000)
                yield return 2;
            yield return 120;
        }

        // ---- scenarios ------------------------------------------------------

        private IEnumerable<int> JoinScenario()
        {
            _results["join"] = new
            {
                role = Role,
                isMultiplayer = Game1.IsMultiplayer,
                contextIsMultiplayer = Context.IsMultiplayer,
                isMasterGame = Game1.IsMasterGame,
                online = Game1.getOnlineFarmers().Select(f => f.Name).ToList(),
                all = Game1.getAllFarmers().Select(f => f.Name).ToList(),
                world = WorldNow()
            };
            yield return 60;
            foreach (int w in Barrier("joinchecked")) yield return w;
        }

        // idle on the Farm together, then both walk about, with frame and allocation numbers on each side
        private IEnumerable<int> PerfScenario()
        {
            foreach (int w in WarpAndWait("Farm", IsHost ? 64 : 66, 15)) yield return w;
            foreach (int w in Barrier("onfarm")) yield return w;
            yield return 120;
            var stress = new Dictionary<string, object?>();
            _results["perf"] = stress;
            stress["world"] = WorldNow();
            foreach (int w in Sample(stress, "idle_together", 1800)) yield return w;
            foreach (int w in Barrier("idledone")) yield return w;

            // both walk back and forth so positions and animations are sent every frame
            int dir = 0;
            for (int block = 0; block < 12; block++)
            {
                Game1.player.faceDirection(dir % 4);
                Game1.player.setTileLocation(new Vector2((IsHost ? 60 : 70) + (block % 2) * 4, 18 + (block % 3)));
                yield return 10;
                dir++;
            }
            foreach (int w in Barrier("walkstart")) yield return w;
            _sampling = true;
            _updateMs.Clear();
            _renderMs.Clear();
            var walkRow = new Dictionary<string, object?>();
            long bytes0 = GC.GetAllocatedBytesForCurrentThread();
            var wall = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 900; i += 6)
            {
                Game1.player.faceDirection((i / 6) % 4);
                Game1.player.Position += new Vector2(((i / 6) % 2 == 0) ? 12f : -12f, 0);
                yield return 6;
            }
            wall.Stop();
            _sampling = false;
            stress["walking_together"] = new
            {
                seconds = Math.Round(wall.Elapsed.TotalSeconds, 1),
                update = Stats(_updateMs),
                render = Stats(_renderMs),
                allocatedMBPerSecond = Math.Round((GC.GetAllocatedBytesForCurrentThread() - bytes0) / 1048576.0 / wall.Elapsed.TotalSeconds, 2)
            };
            stress["memory"] = MemoryNow();
            foreach (int w in Barrier("walkdone")) yield return w;
        }

        // nights with both players online: queue sizes and memory on each side, Sundays included
        private IEnumerable<int> SoakScenario()
        {
            int nights = int.TryParse(Environment.GetEnvironmentVariable("VFH_DAYS"), out int n) ? n : 14;
            var rows = new List<object>();
            _results["soak"] = rows;
            if (Environment.GetEnvironmentVariable("VFH_HEAVY") == "1" && IsHost)
                _results["heavy_farm_objects"] = PopulateHeavyFarm(Game1.getFarm());
            foreach (int w in Barrier("soakstart")) yield return w;
            foreach (int w in Hold("soak-start")) yield return w;
            rows.Add(new { night = 0, world = WorldNow(), memory = MemoryNow() });

            for (int night = 1; night <= nights; night++)
            {
                foreach (int w in SleepThroughNight()) yield return w;
                rows.Add(new { night, dayOfWeek = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth), world = WorldNow(), memory = MemoryNow() });
                foreach (int w in Barrier("night" + night)) yield return w;
            }
            foreach (int w in Hold("soak-end")) yield return w;

            if (Environment.GetEnvironmentVariable("VFH_VISIT") == "1")
            {
                // the joiner walks into locations the host has changed for many nights while nobody was there
                var visits = new List<object>();
                _results["visits"] = visits;
                foreach (string name in new[] { "Town", "Forest", "Mountain", "Railroad", "BusStop", "Beach", "IslandWest" })
                {
                    if (!IsHost)
                    {
                        Game1.warpFarmer(name, 20, 20, false);
                        for (int t = 0; t < 600 && !(Game1.currentLocation?.Name == name && !Game1.fadeToBlack && !Game1.isWarping); t++)
                            yield return 1;
                        yield return 120;
                    }
                    foreach (int w in Barrier("visit-" + name)) yield return w;
                    var loc = Game1.getLocationFromName(name);
                    visits.Add(new { name, objects = loc?.objects.Count(), features = loc?.terrainFeatures.Count(), large = loc?.largeTerrainFeatures.Count, hasRoot = loc?.Root != null, here = Game1.currentLocation?.Name });
                    foreach (int w in Barrier("visited-" + name)) yield return w;
                }
            }
        }

        private static string FarmerXml(Farmer farmer)
        {
            using var ms = new MemoryStream();
            var writer = System.Xml.XmlWriter.Create(ms, new System.Xml.XmlWriterSettings { CloseOutput = false });
            writer.WriteStartDocument();
            StardewValley.SaveSerialization.SaveSerializer.Serialize(writer, farmer);
            writer.WriteEndDocument();
            writer.Flush();
            writer.Close();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        // top level elements of two farmer XML documents that differ, with a short look at each
        private static List<object> XmlDiff(string leftXml, string rightXml)
        {
            var a = System.Xml.Linq.XDocument.Parse(leftXml.TrimStart('\uFEFF')).Root!.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => string.Concat(g.Select(x => x.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))));
            var b = System.Xml.Linq.XDocument.Parse(rightXml.TrimStart('\uFEFF')).Root!.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => string.Concat(g.Select(x => x.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))));
            string Short(string text) => text.Length <= 160 ? text : text[..160] + "...";
            var rows = new List<object>();
            foreach (string key in a.Keys.Union(b.Keys).OrderBy(k => k))
            {
                a.TryGetValue(key, out string? x);
                b.TryGetValue(key, out string? y);
                if (x == y)
                    continue;

                // lists and dictionaries come back in another order, so compare their entries as sets and show what is only on one side
                var left = System.Xml.Linq.XDocument.Parse(leftXml.TrimStart('﻿')).Root!.Elements().Where(e => e.Name.LocalName == key).SelectMany(e => e.Elements()).Select(e => e.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)).ToList();
                var right = System.Xml.Linq.XDocument.Parse(rightXml.TrimStart('﻿')).Root!.Elements().Where(e => e.Name.LocalName == key).SelectMany(e => e.Elements()).Select(e => e.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)).ToList();
                // when entries pair up by position, show the stretch of text where each pair first differs
                var windows = new List<string>();
                for (int i = 0; i < Math.Min(left.Count, right.Count) && windows.Count < 6; i++)
                {
                    if (left[i] == right[i])
                        continue;
                    int at = 0;
                    while (at < left[i].Length && at < right[i].Length && left[i][at] == right[i][at])
                        at++;
                    int from = Math.Max(0, at - 40);
                    string l = left[i].Substring(from, Math.Min(110, left[i].Length - from));
                    string r = right[i].Substring(from, Math.Min(110, right[i].Length - from));
                    windows.Add($"#{i} client: {l} || file: {r}");
                }
                if (windows.Count > 0 && key == "items")
                    rows.Add(new { element = "items-first-difference", windows });
                var onlyLeft = left.Except(right).Take(2).Select(Short).ToList();
                var onlyRight = right.Except(left).Take(2).Select(Short).ToList();
                if (left.Count > 0 && onlyLeft.Count == 0 && onlyRight.Count == 0 && left.Count == right.Count)
                    continue;
                rows.Add(new { element = key, leftCount = left.Count, rightCount = right.Count, onlyOnClient = onlyLeft, onlyInHostFile = onlyRight, plainLeft = left.Count == 0 ? Short(x ?? "(missing)") : "", plainRight = right.Count == 0 ? Short(y ?? "(missing)") : "" });
            }
            return rows;
        }

        // VF-056: the joiner earns progression, a night saves the world, the joiner leaves, the host sleeps alone and the file is read.
        // The farmer is compared as the game would save it, element by element, so any field that does not survive shows up by name.
        private IEnumerable<int> RoundTripScenario()
        {
            var rt = new Dictionary<string, object?>();
            _results["roundtrip"] = rt;
            if (!IsHost)
            {
                var p = Game1.player;
                p.professions.Add(12);
                p.professions.Add(14);
                p.mailReceived.Add("vf_test_mail");
                p.mailReceived.Add("MasteryCompleted_Farming");
                p.craftingRecipes.TryAdd("Chest", 0);
                p.cookingRecipes.TryAdd("Fried Egg", 0);
                p.stats.Set("MasteryExp", 3000);
                p.stats.Set("vf_test_stat", 777);
                p.farmingLevel.Value = 6;
                p.experiencePoints[0] = 1500;
                p.maxItems.Value = 24;
                p.houseUpgradeLevel.Value = 1;
                p.addItemToInventoryBool(ItemRegistry.Create("(O)388", 25));
                var axe = p.Items.OfType<StardewValley.Tools.Axe>().FirstOrDefault();
                if (axe != null)
                    axe.UpgradeLevel = 2;
                p.hasRustyKey = true;
                p.hasSkullKey = true;
                p.eventsSeen.Add("vf_event");
                p.secretNotesSeen.Add(7);
                p.timesReachedMineBottom = 2;
                p.fishCaught.TryAdd("(O)145", new int[] { 3, 20, 0, 0, 0 });
                p.achievements.Add(5);
                p.stats.Set("monstersKilled", 99);
                yield return 30;
                Publish("xml-before", FarmerXml(p));
                File.WriteAllText(SyncFile("xml-before-text", Role), FarmerXml(p));
                rt["clientBefore"] = Snapshot(p);
            }
            foreach (int w in Barrier("progress")) yield return w;

            foreach (int w in SleepThroughNight()) yield return w;
            if (!IsHost)
            {
                File.WriteAllText(SyncFile("xml-after-text", Role), FarmerXml(Game1.player));
                rt["clientAfterNight"] = Snapshot(Game1.player);
            }
            foreach (int w in Barrier("night1")) yield return w;

            if (IsHost)
            {
                var hand = Game1.getAllFarmers().First(f => f.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID);
                string clientAfter = File.ReadAllText(SyncFile("xml-after-text", "client"));
                string folder = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;

                // the copy the host holds for saving, and the file the night wrote while the joiner was still online
                var live = Game1.netWorldState.Value.farmhandData[hand.UniqueMultiplayerID];
                rt["whileOnline_hostFarmhandDataVsClient"] = XmlDiff(clientAfter, FarmerXml(live));
                var loaded = SaveGame.TryReadSaveFileWithFallback(folder, out string readError, out bool recovered);
                var inFile = loaded?.farmhands?.FirstOrDefault(f => f.UniqueMultiplayerID == hand.UniqueMultiplayerID);
                rt["whileOnline_fileVsClient"] = inFile is null ? "farmhand missing from the file" : XmlDiff(clientAfter, FarmerXml(inFile));
                Mark("hostchecked1");
            }
            else
            {
                // leave: the process ends, which is a disconnect to the host
                yield return 10;
                yield break;
            }

            // the joiner has left. The host sleeps alone and the file is read again
            for (int t = 0; t < 3000 && Game1.otherFarmers.Count > 0; t += 15)
                yield return 15;
            rt["otherFarmersAfterLeave"] = Game1.otherFarmers.Count;
            yield return 120;
            foreach (int w in SleepThroughNight()) yield return w;
            {
                var hand = Game1.getAllFarmers().First(f => f.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID);
                string clientAfter = File.ReadAllText(SyncFile("xml-after-text", "client"));
                string folder = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
                var loaded = SaveGame.TryReadSaveFileWithFallback(folder, out string readError, out bool recovered);
                var inFile = loaded?.farmhands?.FirstOrDefault(f => f.UniqueMultiplayerID == hand.UniqueMultiplayerID);
                rt["afterLeaving_fileVsClient"] = inFile is null ? "farmhand missing from the file" : XmlDiff(clientAfter, FarmerXml(inFile));
                rt["saveFolder"] = folder;
                rt["farmhandId"] = hand.UniqueMultiplayerID;
            }
        }

        private object Snapshot(Farmer p)
        {
            return new
            {
                p.Name,
                id = p.UniqueMultiplayerID,
                money = p.Money,
                professions = p.professions.OrderBy(x => x).ToList(),
                mail = p.mailReceived.Contains("vf_test_mail"),
                recipeChest = p.craftingRecipes.ContainsKey("Chest"),
                masteryExp = p.stats.Get("MasteryExp"),
                testStat = p.stats.Get("vf_test_stat"),
                farmingLevel = p.farmingLevel.Value,
                items388 = p.Items.Where(i => i?.QualifiedItemId == "(O)388").Sum(i => i.Stack),
                itemCount = p.Items.Count(i => i != null),
                tools = p.Items.OfType<StardewValley.Tool>().Select(t => t.Name + ":" + t.UpgradeLevel).ToList(),
                separateWallets = p.useSeparateWallets,
                individualMoney = p.team.individualMoney.TryGetValue(p.UniqueMultiplayerID, out var m) ? m.Value : -1
            };
        }

        private object MoneyView(Farmer hand)
        {
            var host = Game1.player;
            bool sharesTeam = ReferenceEquals(hand.teamRoot, host.teamRoot);
            return new
            {
                handMoneyGetter = hand.Money,
                hostTeamIndividual = host.team.individualMoney.TryGetValue(hand.UniqueMultiplayerID, out var m) ? m.Value : -1,
                handPrivateTeamValue = hand.teamRoot.Value.GetMoney(hand).Value,
                handSharesTheHostTeam = sharesTeam,
                separateWallets = host.useSeparateWallets
            };
        }

        // VF-048: the Desert Festival makeover takes the worn clothes into the inventory. Does a dyed pair of pants keep its dye,
        // here and as the other player sees it?
        private IEnumerable<int> MakeoverScenario()
        {
            var m = new Dictionary<string, object?>();
            _results["makeover"] = m;
            var p = Game1.player;
            var pants = ItemRegistry.Create<StardewValley.Objects.Clothing>("(P)3");
            pants.clothesColor.Value = new Color(0, 200, 0);
            p.Equip(pants, p.pantsItem);
            var shirt = ItemRegistry.Create<StardewValley.Objects.Clothing>("(S)1000");
            shirt.clothesColor.Value = new Color(200, 0, 50);
            p.Equip(shirt, p.shirtItem);
            m["worn_before"] = new { pants = p.pantsItem.Value?.QualifiedItemId, pantsColor = p.pantsItem.Value?.clothesColor.Value.PackedValue, shirt = p.shirtItem.Value?.QualifiedItemId, shirtColor = p.shirtItem.Value?.clothesColor.Value.PackedValue };
            yield return 30;

            var festival = Game1.getLocationFromName("DesertFestival") as StardewValley.Locations.DesertFestival;
            m["locationExists"] = festival != null;
            festival?.ReceiveMakeOver(1);
            yield return 30;
            m["worn_after"] = new { pants = p.pantsItem.Value?.QualifiedItemId, shirt = p.shirtItem.Value?.QualifiedItemId, hat = p.hat.Value?.QualifiedItemId };
            m["inventory_clothes_after"] = p.Items.OfType<StardewValley.Objects.Clothing>().Select(c => new { id = c.QualifiedItemId, color = c.clothesColor.Value.PackedValue, dyeable = c.dyeable.Value }).ToList();
            m["inventory_hats_after"] = p.Items.OfType<StardewValley.Objects.Hat>().Select(h => h.QualifiedItemId).ToList();
            foreach (int w in Barrier("makeoverdone")) yield return w;
            if (IsHost)
            {
                var other = Game1.otherFarmers.Values.FirstOrDefault();
                m["hostSeesClientWorn"] = other is null ? null : new { pants = other.pantsItem.Value?.QualifiedItemId, shirt = other.shirtItem.Value?.QualifiedItemId, hat = other.hat.Value?.QualifiedItemId, inventoryClothes = other.Items.OfType<StardewValley.Objects.Clothing>().Select(c => new { id = c.QualifiedItemId, color = c.clothesColor.Value.PackedValue }).ToList() };
            }
        }

        // VF-055, second half: the kept world is loaded in a fresh game and the farmhand joins. What gold do they have?
        private IEnumerable<int> GoldReloadScenario()
        {
            var g = new Dictionary<string, object?>();
            _results["goldreload"] = g;
            yield return 60;
            if (IsHost)
            {
                var hand = Game1.getAllFarmers().First(f => f.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID);
                g["hostSeparateWallets"] = Game1.player.useSeparateWallets;
                g["hostTeamIndividualForFarmhand"] = Game1.player.team.individualMoney.TryGetValue(hand.UniqueMultiplayerID, out var m) ? m.Value : -1;
                g["hostMoney"] = Game1.player.Money;
            }
            else
            {
                g["clientMoney"] = Game1.player.Money;
                g["clientSeparateWallets"] = Game1.player.useSeparateWallets;
            }
            foreach (int w in Barrier("goldchecked")) yield return w;
        }

        // VF-055: separate wallets, money sent to a farmhand who is not in the game, then a night (which saves) and a read of the save file
        private IEnumerable<int> GoldScenario()
        {
            var g = new Dictionary<string, object?>();
            _results["gold"] = g;
            var host = Game1.player;
            var hand = Game1.getAllFarmers().First(f => f.UniqueMultiplayerID != host.UniqueMultiplayerID);
            g["farmhand"] = hand.Name + " " + hand.UniqueMultiplayerID;
            g["farmhandOnline"] = Game1.otherFarmers.ContainsKey(hand.UniqueMultiplayerID);

            ManorHouse.SeparateWallets();
            g["afterSeparateWallets"] = MoneyView(hand);

            host.Money = 10000;
            var manor = (ManorHouse)Game1.getLocationFromName("ManorHouse");
            manor.sendMoney(hand, 3000);
            g["afterSendMoney"] = new { host = host.Money, view = MoneyView(hand) };

            // goods in the farmhand's shipping bin are sold overnight and paid to them the same way
            var bin = Game1.getFarm().getShippingBin(hand);
            bin.Add(ItemRegistry.Create("(O)24", 20));
            g["shippingBinItems"] = bin.Count;

            yield return 60;
            foreach (int w in SleepThroughNight()) yield return w;
            g["afterNight"] = new { host = host.Money, view = MoneyView(hand), binLeft = Game1.getFarm().getShippingBin(hand).Count };

            g["saveFolder"] = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
            g["farmhandId"] = hand.UniqueMultiplayerID;

            // what the nightly save wrote for the farmhand
            try
            {
                string folder = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
                var loaded = SaveGame.TryReadSaveFileWithFallback(folder, out string readError, out bool recovered);
                var saved = loaded?.farmhands?.FirstOrDefault(f => f.UniqueMultiplayerID == hand.UniqueMultiplayerID);
                g["saveFile"] = new { readError, recovered, savedFarmhandMoney = saved?._money, savedHostMoney = loaded?.player?._money, savedSeparateWallets = saved?.useSeparateWallets };
            }
            catch (Exception ex)
            {
                g["saveFileError"] = ex.GetType().Name + ": " + ex.Message;
            }
        }
    }
}
