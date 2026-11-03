using StardewValley;
using StardewValley.Locations;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Coal cart scenario. The joiner empties a coal cart, leaves the Mines and walks back in, the way the forum report does.
    /// Then the host looks at the same floor. Every look records the cart tile, the world's cart counter and the coal on the ground.
    /// </summary>
    public partial class ModEntry
    {
        private const int CartFullTile = 194;
        private const int CartEmptyTile = 195;

        private static IEnumerable<int> EnterFloor(int level)
        {
            Game1.enterMine(level);
            for (int t = 0; t < 900 && !(Game1.currentLocation is MineShaft shaft && shaft.mineLevel == level && !Game1.fadeToBlack && !Game1.isWarping); t++)
                yield return 1;
            yield return 120;
        }

        // the first Buildings tile on the floor that is a coal cart, full or empty
        private static (int x, int y, int index)? FindCart(MineShaft shaft)
        {
            var layer = shaft.map.GetLayer("Buildings");
            for (int x = 0; x < layer.LayerWidth; x++)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    int index = layer.Tiles[x, y]?.TileIndex ?? -1;
                    if (index == CartFullTile || index == CartEmptyTile)
                        return (x, y, index);
                }
            }
            return null;
        }

        private object CartLook(string tag, MineShaft shaft, bool interact)
        {
            var cart = FindCart(shaft);
            int before = cart?.index ?? -1;
            bool acted = false;
            if (interact && cart is { index: CartFullTile } found)
                acted = shaft.checkAction(new xTile.Dimensions.Location(found.x, found.y), Game1.viewport, Game1.player);
            var after = FindCart(shaft);
            int coalOnGround = shaft.debris.Count(d => d.itemId.Value == "382" || d.item?.QualifiedItemId == "(O)382");
            MineShaft.permanentMineChanges.TryGetValue(shaft.mineLevel, out var info);
            return new
            {
                tag,
                role = Role,
                level = shaft.mineLevel,
                cartTile = cart is null ? null : $"{cart.Value.x},{cart.Value.y}",
                tileBefore = before,
                interacted = acted,
                tileAfter = after?.index ?? -1,
                coalCartsLeft = info?.coalCartsLeft,
                coalOnGround
            };
        }

        private IEnumerable<int> CartScenario()
        {
            var rows = new List<object>();
            _results["cart"] = rows;

            if (IsHost)
            {
                int found = -1;
                foreach (int level in new[] { 12, 2, 3, 4, 6, 7, 8, 9, 11, 13, 14, 16, 17, 18, 19, 21, 22, 23, 24, 26, 27, 28, 29, 31, 32, 33, 34, 36, 37, 38, 39 })
                {
                    foreach (int w in EnterFloor(level)) yield return w;
                    if (Game1.currentLocation is MineShaft shaft && FindCart(shaft) is not null)
                    {
                        found = level;
                        break;
                    }
                }
                _results["cartFloor"] = found;
                Publish("cartfloor", new { level = found });
                foreach (int w in WarpAndWait("Farm", 64, 15)) yield return w;
            }
            foreach (int w in Barrier("cartfloor")) yield return w;
            int floor = IsHost ? (int)_results["cartFloor"]! : (Fetch("cartfloor")?.GetProperty("level").GetInt32() ?? -1);
            if (floor < 0)
            {
                _results["cart_error"] = "no coal cart floor found";
                yield break;
            }

            // the joiner first: take the coal, leave, come back twice
            if (!IsHost)
            {
                for (int round = 0; round < 3; round++)
                {
                    foreach (int w in EnterFloor(floor)) yield return w;
                    rows.Add(CartLook("client-visit-" + round, (MineShaft)Game1.currentLocation, interact: true));
                    yield return 60;
                    rows.Add(CartLook("client-after-pick-" + round, (MineShaft)Game1.currentLocation, interact: false));
                    foreach (int w in WarpAndWait("Farm", 66, 15)) yield return w;
                }
            }
            foreach (int w in Barrier("clientpicked")) yield return w;

            // then the host walks onto the same floor
            if (IsHost)
            {
                foreach (int w in EnterFloor(floor)) yield return w;
                rows.Add(CartLook("host-visit-after-client", (MineShaft)Game1.currentLocation, interact: false));
                foreach (int w in WarpAndWait("Farm", 64, 15)) yield return w;
            }
            foreach (int w in Barrier("hostlooked")) yield return w;
        }
    }
}
