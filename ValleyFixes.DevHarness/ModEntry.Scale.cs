using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Scaling scenario for the performance research. Measures the same Farm with more and more of one thing on it: animals walking
    /// outside by day, then torches lit at night, then the other locations as they are. Run with -Profile to get a per method split.
    /// </summary>
    public partial class ModEntry
    {
        private const int ScaleSampleTicks = 600;

        private static IEnumerable<int> PutAnimalsOutside(Farm farm, Building barn, int count, List<FarmAnimal> made)
        {
            var house = (AnimalHouse)barn.GetIndoors();
            for (int i = 0; i < count; i++)
            {
                string type = (made.Count % 3) switch { 0 => "White Cow", 1 => "White Chicken", _ => "Goat" };
                var animal = new FarmAnimal(type, (DateTime.UtcNow.Ticks + made.Count), Game1.player.UniqueMultiplayerID);
                animal.growFully();
                house.adoptAnimal(animal);
                house.animals.Remove(animal.myID.Value);
                farm.animals.TryAdd(animal.myID.Value, animal);
                animal.currentLocation = farm;
                animal.Position = new Vector2(Game1.random.Next(30, 70) * 64, Game1.random.Next(14, 50) * 64);
                made.Add(animal);
                if (i % 25 == 24)
                    yield return 1;
            }
        }

        private IEnumerable<int> ScaleScenario()
        {
            var into = new Dictionary<string, object?>();
            _results["scale"] = into;
            Farm farm = Game1.getFarm();
            Farmer p = Game1.player;

            foreach (int w in WarpAndWait("Farm", 64, 15)) yield return w;
            Game1.timeOfDay = 900;
            Game1.isRaining = false;
            Game1.isSnowing = false;
            yield return 60;
            foreach (int w in Sample(into, "farm_day_0_animals", ScaleSampleTicks)) yield return w;

            // one barn is the home of every animal
            Building? barn = null;
            if (!farm.buildStructure("Deluxe Barn", new Vector2(40, 20), p, out barn, magicalConstruction: true, skipSafetyChecks: true) || barn is null)
            {
                _results["scale_error"] = "could not place a barn";
                yield break;
            }
            barn.daysOfConstructionLeft.Value = 0;
            yield return 30;

            var animals = new List<FarmAnimal>();
            foreach (int target in new[] { 25, 100, 200 })
            {
                foreach (int w in PutAnimalsOutside(farm, barn, target - animals.Count, animals)) yield return w;
                Game1.timeOfDay = 900;
                yield return 60;
                foreach (int w in Sample(into, $"farm_day_{target}_animals", ScaleSampleTicks)) yield return w;
            }

            foreach (var a in animals)
                farm.animals.Remove(a.myID.Value);
            animals.Clear();

            // torches, lit at night
            Game1.timeOfDay = 2100;
            Game1.isRaining = false;
            yield return 90;
            foreach (int w in Sample(into, "farm_night_0_torches", ScaleSampleTicks)) yield return w;
            int torches = 0;
            foreach (int target in new[] { 100, 300 })
            {
                for (int y = 8; y < 62 && torches < target; y++)
                {
                    for (int x = 16; x < 78 && torches < target; x += 3)
                    {
                        var tile = new Vector2(x, y);
                        if (farm.objects.ContainsKey(tile) || farm.terrainFeatures.ContainsKey(tile) || !farm.CanItemBePlacedHere(tile))
                            continue;
                        var torch = ItemRegistry.Create<StardewValley.Object>("(BC)93");
                        torch.TileLocation = tile;
                        farm.objects.Add(tile, torch);
                        torches++;
                    }
                }
                Game1.timeOfDay = 2100;
                yield return 90;
                foreach (int w in Sample(into, $"farm_night_{torches}_torches", ScaleSampleTicks)) yield return w;
            }

            // the same game, other places, by day
            Game1.timeOfDay = 1200;
            foreach (var place in new (string Name, int X, int Y)[] { ("Town", 52, 55), ("Beach", 30, 22), ("Mountain", 60, 40) })
            {
                foreach (int w in WarpAndWait(place.Name, place.X, place.Y)) yield return w;
                Game1.timeOfDay = 1200;
                yield return 60;
                foreach (int w in Sample(into, $"{place.Name.ToLowerInvariant()}_noon", ScaleSampleTicks)) yield return w;
            }
        }
    }
}
