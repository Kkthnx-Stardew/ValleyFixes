using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Mini-Shipping Bin scenario. With separate wallets, a farmhand's part of a Mini-Shipping Bin holds an empty slot between two items.
    /// Then a real overnight. Without the fix the game throws while ending the day. Records whether the day started.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> MiniBinScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["mini_bin"] = info;
            yield return 120;

            var team = Game1.player.team;
            team.useSeparateWallets.Value = true;
            var farm = Game1.getFarm();
            var tile = FindOpenTile(farm);
            var bin = new Chest(true, new Vector2(tile.X, tile.Y), "248");
            farm.objects[new Vector2(tile.X, tile.Y)] = bin;
            info["specialChestType"] = bin.SpecialChestType.ToString();

            const long otherPlayer = 999999999999L;
            var part = new Inventory();
            part.Add(ItemRegistry.Create("(O)24", 3));
            part.Add(null);
            part.Add(ItemRegistry.Create("(O)24", 2));
            bin.separateWalletItems[otherPlayer] = part;
            info["slotsBefore"] = part.Count;
            info["nullSlotsBefore"] = part.Count(i => i is null);

            int dayBefore = Game1.dayOfMonth;
            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !StardewModdingAPI.Context.CanPlayerMove) && guard++ < 3000)
                yield return 1;
            yield return 120;

            info["dayBefore"] = dayBefore;
            info["dayAfter"] = Game1.dayOfMonth;
            info["dayStarted"] = Game1.dayOfMonth != dayBefore;
            info["stillInNewDay"] = Game1.newDay;
            info["waitedTicks"] = guard;
        }
    }
}
