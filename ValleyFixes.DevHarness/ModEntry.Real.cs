using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Monsters;


namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Scenarios on a copy of a real played save with the real mod list. Single player and multiplayer share one tour: the vanilla
    /// places plus the biggest mod locations, each sampled for ten seconds with the profiler and the per mod sampler. Then one night is timed.
    /// </summary>
    public partial class ModEntry
    {
        private void LoadSinglePlayer(TitleMenu title, string saveFolder)
        {
            title.skipToTitleButtons();
            Game1.multiplayerMode = 0;
            SaveGame.Load(saveFolder);
            Game1.exitActiveMenu();
        }

        private IEnumerable<int> WaitForWorld(string key)
        {
            int waited = 0;
            while (!(Context.IsWorldReady && Context.CanPlayerMove && !Game1.fadeToBlack))
            {
                if (Game1.activeClickableMenu is DialogueBox || Game1.activeClickableMenu is LevelUpMenu || Game1.activeClickableMenu is LetterViewerMenu)
                    Game1.exitActiveMenu();
                if (++waited > 12000)
                {
                    _results[key] = "world never became ready";
                    yield break;
                }
                yield return 1;
            }
            yield return 180;
        }

        private static Point FindOpenTile(GameLocation loc)
        {
            var layer = loc.map.Layers[0];
            int cx = layer.LayerWidth / 2, cy = layer.LayerHeight / 2;
            for (int r = 0; r < Math.Max(layer.LayerWidth, layer.LayerHeight) / 2; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                            continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 1 || y < 1 || x >= layer.LayerWidth - 1 || y >= layer.LayerHeight - 1)
                            continue;
                        var l = new xTile.Dimensions.Location(x, y);
                        if (loc.isTileOnMap(x, y) && loc.isTilePassable(l, Game1.viewport) && loc.isTileLocationOpen(l) && !loc.isObjectAtTile(x, y))
                            return new Point(x, y);
                    }
                }
            }
            return new Point(cx, cy);
        }

        private object WorldFacts()
        {
            var npcs = Utility.getAllCharacters().ToList();
            return new
            {
                date = $"{Game1.currentSeason} {Game1.dayOfMonth} y{Game1.year}",
                time = Game1.timeOfDay,
                mods = Helper.ModRegistry.GetAll().Count(),
                topLevelLocations = Game1.locations.Count,
                allCharacters = npcs.Count,
                villagers = npcs.Count(c => c is NPC n && n.IsVillager),
                monsters = npcs.Count(c => c is Monster),
                animalsOnFarm = Game1.getFarm().animals.Length,
                farmObjects = Game1.getFarm().objects.Count(),
                farmTerrainFeatures = Game1.getFarm().terrainFeatures.Count(),
                farmBuildings = Game1.getFarm().buildings.Count,
                festivalDay = Utility.isFestivalDay(),
                player = Game1.player.Name,
                farm = Game1.player.farmName.Value
            };
        }

        private List<string> TourStops()
        {
            var stops = new List<string> { "Farm", "Town", "Forest", "Mountain", "Beach" };
            var mod = Game1.locations
                .Where(l => l.IsOutdoors && l.Name.StartsWith("Custom_") && l.map is not null)
                .OrderByDescending(l => l.characters.Count)
                .ThenByDescending(l => l.map.Layers[0].LayerWidth * l.map.Layers[0].LayerHeight)
                .Take(8)
                .Select(l => l.NameOrUniqueName);
            stops.AddRange(mod);
            return stops;
        }

        private IEnumerable<int> RealTour(Dictionary<string, object?> into, Func<string, IEnumerable<int>>? barrier)
        {
            var info = new Dictionary<string, object?>();
            _results["tour_info"] = info;
            into["world"] = WorldFacts();
            RecordPatchMap(into);
            if (!Game1.IsMasterGame)
                yield break;
            Game1.timeOfDay = 900;

            var stops = TourStops();
            info["stops"] = stops;
            for (int i = 0; i < stops.Count; i++)
            {
                string name = stops[i];
                var loc = Game1.getLocationFromName(name);
                if (loc?.map is null)
                    continue;
                var tile = FindOpenTile(loc);
                foreach (int w in WarpAndWait(name, tile.X, tile.Y)) yield return w;
                yield return 90;
                info[name] = new
                {
                    villagersHere = loc.characters.Count(c => c is NPC n && n.IsVillager),
                    charactersHere = loc.characters.Count,
                    animalsHere = loc.animals.Length,
                    objects = loc.objects.Count(),
                    mapSize = $"{loc.map.Layers[0].LayerWidth}x{loc.map.Layers[0].LayerHeight}"
                };
                foreach (int w in Sample(into, "at_" + name, 600)) yield return w;
            }
        }

        private IEnumerable<int> RealNight(Dictionary<string, object?> into)
        {
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            long heap0 = GC.GetTotalMemory(false);
            int g2 = GC.CollectionCount(2);
            _dayEvents.Clear();
            double start = _clock.Elapsed.TotalMilliseconds;
            var wall = System.Diagnostics.Stopwatch.StartNew();
            foreach (int w in SleepThroughNight()) yield return w;
            wall.Stop();
            into["night"] = new
            {
                events = _dayEvents.Select(e => new { e.Name, atSeconds = Math.Round((e.Ms - start) / 1000.0, 1) }).ToList(),
                inBed,
                wallSeconds = Math.Round(wall.Elapsed.TotalSeconds, 1),
                heapBeforeMB = Math.Round(heap0 / 1048576.0, 0),
                heapAfterMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 0),
                gen2Collections = GC.CollectionCount(2) - g2,
                date = $"{Game1.currentSeason} {Game1.dayOfMonth}"
            };
        }

        private IEnumerable<int> RealSinglePlayerMain()
        {
            var into = new Dictionary<string, object?>();
            _results["real"] = into;
            foreach (int w in WaitForWorld("real_error")) yield return w;
            // VFH_NOTOUR=1 goes straight to the night, for comparing the saved world
            if (Environment.GetEnvironmentVariable("VFH_NOTOUR") != "1")
                foreach (int w in RealTour(into, null)) yield return w;
            foreach (int w in RealNight(into)) yield return w;
            yield return 120;
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        private readonly List<(string Name, double Ms)> _dayEvents = new();

        private void WatchDayEvents(IModHelper helper)
        {
            helper.Events.GameLoop.DayEnding += (_, _) => _dayEvents.Add(("DayEnding", _clock.Elapsed.TotalMilliseconds));
            helper.Events.GameLoop.Saving += (_, _) => _dayEvents.Add(("Saving", _clock.Elapsed.TotalMilliseconds));
            helper.Events.GameLoop.Saved += (_, _) => _dayEvents.Add(("Saved", _clock.Elapsed.TotalMilliseconds));
            helper.Events.GameLoop.DayStarted += (_, _) => _dayEvents.Add(("DayStarted", _clock.Elapsed.TotalMilliseconds));
        }

        private IEnumerable<int> RealFocusMain()
        {
            var into = new Dictionary<string, object?>();
            _results["real"] = into;
            foreach (int w in WaitForWorld("real_error")) yield return w;
            into["world"] = WorldFacts();
            RecordPatchMap(into);
            Game1.timeOfDay = 900;

            var farm = Game1.getFarm();
            var tile = FindOpenTile(farm);
            foreach (int w in WarpAndWait("Farm", tile.X, tile.Y)) yield return w;
            yield return 120;
            foreach (int w in Sample(into, "farm_30s", 1800)) yield return w;

            var beach = Game1.getLocationFromName("Beach");
            var bt = FindOpenTile(beach);
            foreach (int w in WarpAndWait("Beach", bt.X, bt.Y)) yield return w;
            yield return 120;
            foreach (int w in Sample(into, "beach_10s", 600)) yield return w;

            if (Environment.GetEnvironmentVariable("VFH_MONSTERTEST") == "1")
            {
                foreach (int w in MonsterResumeTest(into, "Custom_Highlands")) yield return w;
                if (Environment.GetEnvironmentVariable("VFH_NONIGHT") == "1")
                    yield break;
            }

            if (Environment.GetEnvironmentVariable("VFH_NONIGHT") == "1")
                yield break;

            // one timed night with a stamp for each phase
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            _dayEvents.Clear();
            double start = _clock.Elapsed.TotalMilliseconds;
            foreach (int w in SleepThroughNight()) yield return w;
            double end = _clock.Elapsed.TotalMilliseconds;
            into["night_phases_seconds"] = new
            {
                inBed,
                events = _dayEvents.Select(e => new { e.Name, atSeconds = Math.Round((e.Ms - start) / 1000.0, 1) }).ToList(),
                totalSeconds = Math.Round((end - start) / 1000.0, 1)
            };
            yield return 120;
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        private void RecordPatchMap(Dictionary<string, object?> into)
        {
            try
            {
                into["patch_map"] = PatchMap.Build();
            }
            catch (Exception ex)
            {
                into["patch_map_error"] = ex.Message;
            }
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        /// <summary>
        /// Watches the monsters of one monster-heavy location. Phase one: nobody there for a few seconds, how many moved. Phase two: the
        /// player walks in, how many move in the next seconds. Also counts the villagers of every location that moved while nobody watched.
        /// </summary>
        private IEnumerable<int> MonsterResumeTest(Dictionary<string, object?> into, string locationName)
        {
            var loc = Game1.getLocationFromName(locationName);
            if (loc is null)
            {
                into["monster_test_error"] = "no location " + locationName;
                yield break;
            }

            var farm = Game1.getFarm();
            var ft = FindOpenTile(farm);
            foreach (int w in WarpAndWait("Farm", ft.X, ft.Y)) yield return w;
            yield return 300;

            var watched = loc.characters.OfType<Monster>().Take(40).ToList();
            var start = watched.Select(m => m.Position).ToList();
            var villagers = Utility.getAllCharacters().Where(c => c is NPC n && n.IsVillager && c.currentLocation?.farmers.Count == 0).Take(40).ToList();
            var vStart = villagers.Select(v => v.Position).ToList();
            yield return 240;
            int movedWhileEmpty = watched.Where((m, i) => m.Position != start[i]).Count();
            int villagersMoved = villagers.Where((v, i) => v.Position != vStart[i]).Count();

            var tile = FindOpenTile(loc);
            foreach (int w in WarpAndWait(locationName, tile.X, tile.Y)) yield return w;
            var arrive = watched.Select(m => m.Position).ToList();
            yield return 240;
            int movedAfterArrival = watched.Where((m, i) => m.Position != arrive[i]).Count();

            into["monster_resume"] = new
            {
                location = locationName,
                monstersWatched = watched.Count,
                movedWhileNobodyThere = movedWhileEmpty,
                movedSecondsAfterPlayerArrived = movedAfterArrival,
                villagersWatchedInEmptyLocations = villagers.Count,
                villagersThatMovedWhileNobodyWatched = villagersMoved
            };
        }
    }
}
