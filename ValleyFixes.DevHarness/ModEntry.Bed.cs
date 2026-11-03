using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>Where the game puts the "Bed" tile property in the farmhouse, and whether standing there keeps the in-bed flag.</summary>
    public partial class ModEntry
    {
        private IEnumerable<int> BedScenario()
        {
            var rows = new List<object>();
            _results["bed"] = rows;
            var house = Game1.getLocationFromName("FarmHouse") as StardewValley.Locations.FarmHouse;
            var bedTiles = new List<string>();
            for (int y = 0; y < 30; y++)
                for (int x = 0; x < 30; x++)
                    if (house!.doesTileHaveProperty(x, y, "Bed", "Back") != null)
                        bedTiles.Add($"{x},{y}");
            var spot = house!.GetPlayerBedSpot();
            var bedFurniture = house.GetPlayerBed();
            _results["bed_info"] = new { bedTiles, bedSpot = $"{spot.X},{spot.Y}", furnitureTile = bedFurniture is null ? null : $"{bedFurniture.TileLocation.X},{bedFurniture.TileLocation.Y}", furnitureBox = bedFurniture?.GetBoundingBox().ToString() };

            var candidates = new List<Point> { spot, FindBedTile(house) };
            foreach (var t in bedTiles.Take(6))
            {
                var parts = t.Split(',');
                candidates.Add(new Point(int.Parse(parts[0]), int.Parse(parts[1])));
            }
            // the same look again after a trip outside and then to the Town and back, the way the sleep scenarios arrive
            foreach (string trip in new[] { "Farm", "Town", "Farm" })
            {
                foreach (int w in WarpAndWait(trip, 60, 20)) yield return w;
                yield return 60;
                var t2 = new List<string>();
                for (int y = 0; y < 30; y++)
                    for (int x = 0; x < 30; x++)
                        if (house.doesTileHaveProperty(x, y, "Bed", "Back") != null)
                            t2.Add($"{x},{y}");
                rows.Add(new { afterTrip = trip, bedTilesNow = t2, bedFurniture = house.GetPlayerBed() is null ? "none" : "present", mapNow = house.NameOrUniqueName, houseFromLookup = ReferenceEquals(house, Game1.getLocationFromName("FarmHouse")) });
            }
            foreach (var c in candidates.Distinct())
            {
                foreach (int w in WarpAndWait("FarmHouse", c.X, c.Y)) yield return w;
                yield return 20;
                var p = Game1.player;
                rows.Add(new { target = $"{c.X},{c.Y}", tile = $"{p.TilePoint.X},{p.TilePoint.Y}", pos = $"{p.Position.X},{p.Position.Y}", hasBedProperty = house.doesTileHaveProperty(p.TilePoint.X, p.TilePoint.Y, "Bed", "Back") != null, isInBed = p.isInBed.Value });
            }
        }
    }
}
