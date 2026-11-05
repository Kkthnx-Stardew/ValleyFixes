using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Tools;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Disconnect and rejoin scenarios for brief items 36 to 43. Each one puts the farmhand in a state, then lets the joiner leave to the
    /// title screen and join again, or lets the host quit to the title and load its save again, and compares the farmhand's state before and after.
    /// Run on a clean profile with a new farm. Names: rj36, rj38, rj39, rj40, rj41, rj42.
    /// </summary>
    public partial class ModEntry
    {
        private readonly Dictionary<string, int> _menusSeen = new();
        private int _sleepCount;

        private static string SaveFolderName() => SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;

        // ---- state snapshots ------------------------------------------------

        private static Dictionary<string, object?> FarmerState(Farmer f)
        {
            var home = Utility.getHomeOfFarmer(f);
            var d = new Dictionary<string, object?>
            {
                ["name"] = f.Name,
                ["money"] = f.Money,
                ["levels"] = new[] { f.FarmingLevel, f.FishingLevel, f.ForagingLevel, f.MiningLevel, f.CombatLevel, f.LuckLevel },
                ["xp"] = Enumerable.Range(0, 6).Select(i => f.experiencePoints[i]).ToList(),
                ["newLevels"] = f.newLevels.Select(p => p.X + ":" + p.Y).ToList(),
                ["craftingRecipes"] = f.craftingRecipes.Keys.OrderBy(x => x).ToList(),
                ["cookingRecipes"] = f.cookingRecipes.Keys.OrderBy(x => x).ToList(),
                ["mailReceived"] = f.mailReceived.OrderBy(x => x).ToList(),
                ["mailForTomorrow"] = f.mailForTomorrow.ToList(),
                ["mailbox"] = f.mailbox.ToList(),
                ["basicShipped"] = f.basicShipped.Pairs.ToDictionary(p => p.Key, p => p.Value),
                ["toolBeingUpgraded"] = f.toolBeingUpgraded.Value is Tool t ? t.QualifiedItemId + " upgrade " + t.UpgradeLevel : null,
                ["daysLeftForToolUpgrade"] = f.daysLeftForToolUpgrade.Value,
                ["toolsInInventory"] = f.Items.OfType<Tool>().Select(x => x.QualifiedItemId + ":" + x.UpgradeLevel).ToList(),
                ["houseUpgradeLevel"] = f.houseUpgradeLevel.Value,
                ["daysUntilHouseUpgrade"] = f.daysUntilHouseUpgrade.Value,
                ["questLog"] = f.questLog.Select(q => q.id.Value).ToList(),
                ["fridge"] = home?.fridge.Value?.Items.Where(i => i != null).Select(i => i.QualifiedItemId + " x" + i.Stack).ToList(),
                ["homeName"] = home?.NameOrUniqueName,
                ["homeUpgradeLevel"] = home?.upgradeLevel
            };
            return d;
        }

        /// <summary>Lists the keys that differ between two snapshots, with both values.</summary>
        private static List<string> Diff(Dictionary<string, object?> a, Dictionary<string, object?> b)
        {
            var list = new List<string>();
            foreach (string key in a.Keys.Union(b.Keys))
            {
                string va = a.TryGetValue(key, out var x) ? JsonSerializer.Serialize(x) : "(missing)";
                string vb = b.TryGetValue(key, out var y) ? JsonSerializer.Serialize(y) : "(missing)";
                if (va != vb)
                    list.Add($"{key}: {va} -> {vb}");
            }
            return list;
        }

        private Farmer? ClientFarmerAsSeenByHost() => Game1.getAllFarmhands().FirstOrDefault(f => f.UniqueMultiplayerID != Game1.MasterPlayer.UniqueMultiplayerID);

        private Farmer? MyFarmer() => IsHost ? ClientFarmerAsSeenByHost() : Game1.player;

        // ---- leaving, joining, sleeping --------------------------------------

        private IEnumerable<int> LeaveToTitle()
        {
            Game1.ExitToTitle();
            for (int t = 0; t < 1800 && !(Game1.activeClickableMenu is TitleMenu); t += 5)
                yield return 5;
            yield return 180;
        }

        private IEnumerable<int> JoinFromTitle()
        {
            var title = Game1.activeClickableMenu as TitleMenu;
            if (title is null)
            {
                _results["join_error"] = "no title menu";
                yield break;
            }
            title.skipToTitleButtons();
            yield return 60;
            var multiplayer = Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            var client = multiplayer.InitClient(new LidgrenClient(Environment.GetEnvironmentVariable("VFH_JOINADDR") ?? "localhost"));
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
            yield return 240;
        }

        private IEnumerable<int> HostLoadAgain(string folder)
        {
            foreach (int w in LeaveToTitle()) yield return w;
            var title = (TitleMenu)Game1.activeClickableMenu;
            LoadHostFarm(title, folder);
            int waited = 0;
            while (!(Context.IsWorldReady && Context.CanPlayerMove && !Game1.fadeToBlack))
            {
                DismissMenus();
                if (++waited > 6000)
                {
                    _results["host_reload"] = "never became ready";
                    yield break;
                }
                yield return 5;
            }
            yield return 180;
        }

        private void DismissMenus()
        {
            var m = Game1.activeClickableMenu;
            if (m is null || m is TitleMenu || m is ReadyCheckDialog)
                return;
            string kind = m.GetType().Name;
            _menusSeen[kind] = _menusSeen.GetValueOrDefault(kind) + 1;
            switch (m)
            {
                case ShippingMenu s:
                    s.receiveLeftClick(s.okButton.bounds.Center.X, s.okButton.bounds.Center.Y, false);
                    break;
                case LevelUpMenu l:
                    // the menu ignores clicks, the OK button does this: it adds the perk and takes the level off the list the farmer saves
                    l.okButtonClicked();
                    break;
                case SaveGameMenu:
                    break;
                default:
                    m.exitThisMenu(false);
                    break;
            }
        }

        /// <summary>Goes to bed (each player to their own bed), sleeps and returns once the new day runs. End of night menus are clicked through and counted.</summary>
        private IEnumerable<int> SleepNight()
        {
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            foreach (int w in Barrier("sleep-home-" + (++_sleepCount), 14400)) yield return w;

            int day0 = Game1.Date.TotalDays;
            var p = Game1.player;
            p.isInBed.Value = true;
            typeof(GameLocation).GetMethod("startSleep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(Game1.currentLocation, null);
            for (int t = 0; t < 12000 && Game1.Date.TotalDays == day0; t += 5)
            {
                DismissMenus();
                yield return 5;
            }
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !Context.CanPlayerMove) && guard++ < 6000)
            {
                DismissMenus();
                yield return 2;
            }
            yield return 180;
        }

        /// <summary>The joiner leaves to the title screen and joins again. The host records what it holds for the farmhand while the joiner is away.</summary>
        private IEnumerable<int> JoinerLeavesAndReturns(Dictionary<string, object?> rows, string tag)
        {
            if (!IsHost)
            {
                foreach (int w in LeaveToTitle()) yield return w;
            }
            else
            {
                for (int t = 0; t < 1200 && Game1.otherFarmers.Count > 0; t += 5) yield return 5;
                yield return 120;
                var f = ClientFarmerAsSeenByHost();
                rows[tag + "_host_view_while_away"] = f is null ? null : FarmerState(f);
            }
            foreach (int w in Barrier(tag + "-away", 14400)) yield return w;
            if (!IsHost)
            {
                foreach (int w in JoinFromTitle()) yield return w;
            }
            else
            {
                for (int t = 0; t < 9000 && Game1.otherFarmers.Count == 0; t += 15) yield return 15;
                yield return 300;
            }
            foreach (int w in Barrier(tag + "-back", 14400)) yield return w;
        }

        /// <summary>The host quits to the title and loads its save again while the joiner is online. The joiner is then sent to the title and joins again.</summary>
        private IEnumerable<int> HostQuitsAndReloads(string folder, string tag)
        {
            if (IsHost)
            {
                foreach (int w in HostLoadAgain(folder)) yield return w;
                Mark(tag + "-hostup");
            }
            else
            {
                for (int t = 0; t < 3600 && Context.IsWorldReady && Game1.activeClickableMenu is not TitleMenu; t += 5)
                {
                    // the game shows a lost connection message and returns to the title by itself. If it has not within 15 seconds the harness does it
                    if (t > 900)
                        break;
                    yield return 5;
                }
                if (Game1.activeClickableMenu is not TitleMenu)
                {
                    foreach (int w in LeaveToTitle()) yield return w;
                }
                for (int t = 0; t < 14400 && !OtherHas(tag + "-hostup"); t += 15) yield return 15;
                yield return 240;
                foreach (int w in JoinFromTitle()) yield return w;
            }
            foreach (int w in Barrier(tag + "-back", 14400)) yield return w;
        }

        // ---- scenarios -------------------------------------------------------

        private IEnumerable<int> RejoinScenario(string name)
        {
            var rows = new Dictionary<string, object?>();
            _results[name] = rows;
            string folder = "";
            if (IsHost)
                rows["start"] = $"{Game1.currentSeason} {Game1.dayOfMonth} y{Game1.year} {Game1.timeOfDay}";

            IEnumerable<int> body = name switch
            {
                "rj36" => ProgressScenario(rows),
                "rj38" => KegScenario(rows),
                "rj39" => ClintScenario(rows),
                "rj40" => CabinScenario(rows),
                "rj41" => ConstructionScenario(rows),
                "rj42" => QuestScenario(rows),
                _ => Enumerable.Empty<int>()
            };
            foreach (int w in body) yield return w;
            rows["menusSeen_" + Role] = _menusSeen;
            rows["end"] = $"{Game1.currentSeason} {Game1.dayOfMonth} y{Game1.year}";
        }

        private void Record(Dictionary<string, object?> rows, string tag)
        {
            var f = IsHost ? ClientFarmerAsSeenByHost() : Game1.player;
            rows[tag + "_" + (IsHost ? "hostView" : "self")] = f is null ? null : FarmerState(f);
        }

        private void RecordDiff(Dictionary<string, object?> rows, string a, string b, string who)
        {
            if (rows.TryGetValue(a + "_" + who, out var x) && rows.TryGetValue(b + "_" + who, out var y) && x is Dictionary<string, object?> da && y is Dictionary<string, object?> db)
                rows["DIFF_" + a + "_to_" + b + "_" + who] = Diff(da, db);
        }

        private string HostFolder()
        {
            if (IsHost)
                Publish("folder", SaveFolderName());
            return IsHost ? SaveFolderName() : "";
        }

        // 36, 37: skill up, shipped item, unique mail earned on day 1. The host quits at the start of day 2 and loads the save. Then a second night.
        private IEnumerable<int> ProgressScenario(Dictionary<string, object?> rows)
        {
            var p = Game1.player;
            if (!IsHost)
            {
                p.gainExperience(1, 380); // fishing to level 2
                Game1.getFarm().getShippingBin(p).Add(ItemRegistry.Create("(O)24", 5));
                Game1.addMail("harness_unique_letter", noLetter: false, sendToEveryone: false);
                p.mailReceived.Add("harness_flag_now");
            }
            yield return 60;
            Record(rows, "d1_before_sleep");
            foreach (int w in Barrier("rj36-d1", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "d2_morning");
            string folder = HostFolder();
            foreach (int w in Barrier("rj36-d2", 14400)) yield return w;
            folder = IsHost ? folder : (Fetch("folder")?.GetString() ?? "");
            foreach (int w in HostQuitsAndReloads(folder, "rj36-reload")) yield return w;
            Record(rows, "d2_after_host_reload");
            foreach (int w in Barrier("rj36-recorded", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "d3_morning");
            string who = IsHost ? "hostView" : "self";
            RecordDiff(rows, "d1_before_sleep", "d2_morning", who);
            RecordDiff(rows, "d2_morning", "d2_after_host_reload", who);
            RecordDiff(rows, "d2_after_host_reload", "d3_morning", who);
        }

        // 38: a keg working overnight, then the host quits and loads the save again
        private IEnumerable<int> KegScenario(Dictionary<string, object?> rows)
        {
            var farm = Game1.getFarm();
            StardewValley.Object? keg = null;
            if (IsHost)
            {
                var tile = new Vector2(60, 30);
                keg = ItemRegistry.Create<StardewValley.Object>("(BC)12");
                keg.TileLocation = tile;
                farm.objects.Remove(tile);
                farm.objects.Add(tile, keg);
                var input = ItemRegistry.Create<StardewValley.Object>("(O)24", 1);
                rows["dropIn"] = keg.performObjectDropInAction(input, false, Game1.player);
            }
            yield return 60;
            object KegNow() => keg is null ? new { } : new { minutes = keg.MinutesUntilReady, held = keg.heldObject.Value?.QualifiedItemId, ready = keg.readyForHarvest.Value };
            rows["before_sleep"] = KegNow();
            foreach (int w in Barrier("rj38-d1", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            rows["d2_morning"] = KegNow();
            string folder = HostFolder();
            foreach (int w in Barrier("rj38-d2", 14400)) yield return w;
            folder = IsHost ? folder : (Fetch("folder")?.GetString() ?? "");
            foreach (int w in HostQuitsAndReloads(folder, "rj38-reload")) yield return w;
            if (IsHost)
            {
                var kegAgain = Game1.getFarm().objects.Values.FirstOrDefault(o => o.QualifiedItemId == "(BC)12");
                rows["after_reload"] = kegAgain is null ? new { missing = true } : new { minutes = kegAgain.MinutesUntilReady, held = kegAgain.heldObject.Value?.QualifiedItemId, ready = kegAgain.readyForHarvest.Value };
            }
            foreach (int w in Barrier("rj38-end", 14400)) yield return w;
        }

        // 39: the joiner hands a tool to Clint, then leaves and joins again on the same day, then sleeps
        private IEnumerable<int> ClintScenario(Dictionary<string, object?> rows)
        {
            var p = Game1.player;
            if (!IsHost)
            {
                // the same steps as Tool.cs takes when the player pays for an upgrade
                var pick = p.Items.OfType<Pickaxe>().First();
                var upgraded = (Tool)pick.getOne();
                upgraded.UpgradeLevel = 1;
                p.removeItemFromInventory(pick);
                p.Money -= 2000;
                p.toolBeingUpgraded.Value = upgraded;
                p.daysLeftForToolUpgrade.Value = 2;
            }
            yield return 60;
            Record(rows, "handoff");
            foreach (int w in Barrier("rj39-a", 14400)) yield return w;
            foreach (int w in JoinerLeavesAndReturns(rows, "leave1")) yield return w;
            Record(rows, "after_rejoin_same_day");
            foreach (int w in Barrier("rj39-b", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "night1");
            foreach (int w in Barrier("rj39-c", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "night2");
            string who = IsHost ? "hostView" : "self";
            RecordDiff(rows, "handoff", "after_rejoin_same_day", who);
            RecordDiff(rows, "after_rejoin_same_day", "night1", who);
            RecordDiff(rows, "night1", "night2", who);
        }

        // 40, 41 (cabin): the joiner's cabin upgrade is ordered, then it is stocked after the upgrade, then rejoin, host quit and reload
        private IEnumerable<int> CabinScenario(Dictionary<string, object?> rows)
        {
            var p = Game1.player;
            if (!IsHost)
            {
                p.Money = 50000;
                p.daysUntilHouseUpgrade.Value = 1; // ordered; the next night finishes it
                p.Money -= 10000;
            }
            yield return 60;
            Record(rows, "ordered");
            foreach (int w in Barrier("rj40-a", 14400)) yield return w;
            foreach (int w in JoinerLeavesAndReturns(rows, "leave1")) yield return w;
            Record(rows, "ordered_after_rejoin");
            foreach (int w in Barrier("rj40-b", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "d2_upgraded");
            if (!IsHost)
            {
                var home = Utility.getHomeOfFarmer(Game1.player);
                home.fridge.Value.addItem(ItemRegistry.Create("(O)194", 20));
                home.fridge.Value.addItem(ItemRegistry.Create("(O)213", 5));
            }
            yield return 60;
            Record(rows, "d2_stocked");
            foreach (int w in Barrier("rj40-c", 14400)) yield return w;
            foreach (int w in JoinerLeavesAndReturns(rows, "leave2")) yield return w;
            Record(rows, "d2_after_rejoin");
            foreach (int w in Barrier("rj40-d", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "d3_morning");
            string folder = HostFolder();
            foreach (int w in Barrier("rj40-e", 14400)) yield return w;
            folder = IsHost ? folder : (Fetch("folder")?.GetString() ?? "");
            foreach (int w in HostQuitsAndReloads(folder, "rj40-reload")) yield return w;
            Record(rows, "d3_after_host_reload");
            string who = IsHost ? "hostView" : "self";
            RecordDiff(rows, "ordered", "ordered_after_rejoin", who);
            RecordDiff(rows, "ordered_after_rejoin", "d2_upgraded", who);
            RecordDiff(rows, "d2_stocked", "d2_after_rejoin", who);
            RecordDiff(rows, "d2_after_rejoin", "d3_morning", who);
            RecordDiff(rows, "d3_morning", "d3_after_host_reload", who);
            foreach (int w in Barrier("rj40-end", 14400)) yield return w;
        }

        // 41 (barn): the host orders a building, sleeps, quits and loads again
        private IEnumerable<int> ConstructionScenario(Dictionary<string, object?> rows)
        {
            if (IsHost)
            {
                bool ok = false;
                StardewValley.Buildings.Building? barn = null;
                for (int y = 18; y < 50 && !ok; y += 5)
                    for (int x = 20; x < 80 && !ok; x += 6)
                        ok = Game1.getFarm().buildStructure("Barn", new Vector2(x, y), Game1.player, out barn, magicalConstruction: false, skipSafetyChecks: true);
                rows["ordered"] = ok;
                rows["barnDaysLeft"] = barn?.daysOfConstructionLeft.Value;
                rows["underConstruction"] = Game1.IsThereABuildingUnderConstruction();
            }
            yield return 60;
            foreach (int w in Barrier("rj41-a", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            object BuildingNow() => Game1.getFarm().buildings.Select(b => new { b.buildingType.Value, left = b.daysOfConstructionLeft.Value, upgrade = b.daysUntilUpgrade.Value }).ToList();
            if (IsHost)
            {
                rows["d2_buildings"] = BuildingNow();
                rows["d2_underConstruction"] = Game1.IsThereABuildingUnderConstruction();
                rows["d2_robin"] = Game1.getCharacterFromName("Robin")?.currentLocation?.NameOrUniqueName;
            }
            string folder = HostFolder();
            foreach (int w in Barrier("rj41-b", 14400)) yield return w;
            folder = IsHost ? folder : (Fetch("folder")?.GetString() ?? "");
            foreach (int w in HostQuitsAndReloads(folder, "rj41-reload")) yield return w;
            if (IsHost)
            {
                rows["after_reload_buildings"] = BuildingNow();
                rows["after_reload_underConstruction"] = Game1.IsThereABuildingUnderConstruction();
            }
            foreach (int w in Barrier("rj41-c", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            if (IsHost)
            {
                rows["d3_buildings"] = BuildingNow();
                rows["d3_underConstruction"] = Game1.IsThereABuildingUnderConstruction();
            }
            foreach (int w in Barrier("rj41-end", 14400)) yield return w;
        }

        // 42: the joiner accepts quests on day 1, then leaves and joins again on the same day, then sleeps
        private IEnumerable<int> QuestScenario(Dictionary<string, object?> rows)
        {
            var p = Game1.player;
            if (!IsHost)
            {
                foreach (string id in new[] { "6", "9", "13" })
                    p.addQuest(id);
            }
            yield return 60;
            Record(rows, "accepted");
            foreach (int w in Barrier("rj42-a", 14400)) yield return w;
            foreach (int w in JoinerLeavesAndReturns(rows, "leave1")) yield return w;
            Record(rows, "after_rejoin_same_day");
            foreach (int w in Barrier("rj42-b", 14400)) yield return w;
            foreach (int w in SleepNight()) yield return w;
            Record(rows, "d2_morning");
            string who = IsHost ? "hostView" : "self";
            RecordDiff(rows, "accepted", "after_rejoin_same_day", who);
            RecordDiff(rows, "after_rejoin_same_day", "d2_morning", who);
        }
    }
}
