using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Fruit tree scenario. A cherry tree grows three fruit while it is 100 days past maturity (normal quality), then it ages to 120 days
    /// (silver). The tree is shaken, and the quality of the fruit that drops is read.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> FruitScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["fruit_tree"] = info;
            yield return 120;

            var farm = Game1.getFarm();
            var spot = FindOpenTile(farm);
            var tile = new Vector2(spot.X, spot.Y);
            var tree = new FruitTree("628", 4);
            tree.daysUntilMature.Value = -100;
            farm.terrainFeatures[tile] = tree;
            tree.Location = farm;
            tree.fruit.Clear();
            for (int i = 0; i < 60 && tree.fruit.Count < 3; i++)
                tree.TryAddFruit();
            info["fruitGrown"] = tree.fruit.Count;
            info["qualityWhenGrown"] = tree.fruit.Select(f => f?.Quality).ToList();

            tree.daysUntilMature.Value = -120;
            info["treeQualityNow"] = tree.GetQuality();

            farm.debris.Clear();
            Game1.player.Position = (tile + new Vector2(0, 2)) * 64f;
            tree.shake(tile, true);
            yield return 5;
            info["dropped"] = farm.debris.Select(d => new { item = d.item?.QualifiedItemId, quality = d.item?.Quality, passed = d.itemQuality }).ToList();
        }
    }
}
