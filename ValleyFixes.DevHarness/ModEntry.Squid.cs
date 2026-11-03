using StardewValley;
using StardewValley.Locations;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Squidfest scenario. Both players stand on the Beach on Winter 13, then both sleep. On Winter 14 each side reads the booth tile
    /// on its own Beach before and after walking onto the Beach.
    /// </summary>
    public partial class ModEntry
    {
        private object CacheLook()
        {
            var multiplayer = Helper.Reflection.GetField<StardewValley.Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            var beach = Game1.getLocationFromName("Beach");
            multiplayer.cachedMultiplayerMaps.TryGetValue("Beach", out var cached);
            return new
            {
                beachObject = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(beach),
                beachMap = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(beach.map),
                cacheEntry = cached is not null,
                cacheMap = cached is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cached.map),
                cacheOverrides = cached?.appliedMapOverrides?.OrderBy(s => s).ToList(),
                cacheMapPath = cached?.mapPath,
                beachMapPath = beach.mapPath.Value,
                cachedLocations = multiplayer.cachedMultiplayerMaps.Count
            };
        }

        private object BeachLook(string tag)
        {
            var beach = Game1.getLocationFromName("Beach");
            var applied = typeof(GameLocation).GetField("_appliedMapOverrides", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(beach) as HashSet<string>;
            return new
            {
                tag,
                role = Role,
                date = $"{Game1.currentSeason} {Game1.dayOfMonth}",
                boothTile = beach?.getTileIndexAt(11, 7, "Buildings", "16"),
                boothTileAnySheet = beach?.getTileIndexAt(11, 7, "Buildings"),
                boothTileAddedSheet = beach?.getTileIndexAt(11, 7, "Buildings", GameLocation.GetAddedMapOverrideTilesheetId("Beach_SquidFest", "16")),
                signTile = beach?.getTileIndexAt(15, 6, "Buildings"),
                overrides = applied?.OrderBy(s => s).ToList(),
                squidContestants = beach?.characters.Count(c => c.Name.Contains("derby_contestent")),
                farmersHere = beach?.farmers.Count,
                cache = IsHost ? null : CacheLook()
            };
        }

        private IEnumerable<int> SquidScenario()
        {
            var rows = new List<object>();
            _results["squid"] = rows;

            if (IsHost && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VFH_LOAD")))
            {
                Game1.currentSeason = "winter";
                Game1.dayOfMonth = 13;
                Game1.timeOfDay = 900;
            }
            foreach (int w in Barrier("squiddate")) yield return w;
            yield return 120;
            rows.Add(new { tag = "date-" + Role, date = $"{Game1.currentSeason} {Game1.dayOfMonth}" });

            foreach (int w in WarpAndWait("Beach", IsHost ? 30 : 32, 22)) yield return w;
            yield return 60;
            rows.Add(BeachLook("day13-on-beach"));
            foreach (int w in Barrier("squidbeach13")) yield return w;

            // each player goes to their own bed: the host's farmhouse or the joiner's cabin
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            rows.Add(new { tag = "home-" + Role, location = Game1.currentLocation?.NameOrUniqueName, inBed });
            foreach (int w in Barrier("squidhome")) yield return w;

            foreach (int w in SleepThroughNight()) yield return w;
            rows.Add(BeachLook("day14-before-entry"));
            foreach (int w in Barrier("squidwoke")) yield return w;

            foreach (int w in WarpAndWait("Beach", IsHost ? 30 : 32, 22)) yield return w;
            yield return 60;
            rows.Add(BeachLook("day14-after-entry"));
            foreach (int w in Barrier("squidbeach14")) yield return w;
        }
    }
}
