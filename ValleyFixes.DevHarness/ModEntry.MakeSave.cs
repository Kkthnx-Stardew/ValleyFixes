using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>Makes a real save on a chosen day: the host sets the date to the day before, both players sleep, and the game's own new day processes and saves.</summary>
    public partial class ModEntry
    {
        private IEnumerable<int> MakeWinter13Scenario()
        {
            var rows = new List<object>();
            _results["makesave"] = rows;
            if (IsHost)
            {
                Game1.currentSeason = "winter";
                Game1.dayOfMonth = 12;
                Game1.year = 1;
            }
            foreach (int w in Barrier("winterdate")) yield return w;
            yield return 120;
            bool inBed = false;
            foreach (int w in GoToBed(r => inBed = r)) yield return w;
            rows.Add(new { tag = "bed-" + Role, inBed, date = $"{Game1.currentSeason} {Game1.dayOfMonth}" });
            foreach (int w in Barrier("winterbed")) yield return w;
            foreach (int w in SleepThroughNight()) yield return w;
            rows.Add(new { tag = "woke-" + Role, date = $"{Game1.currentSeason} {Game1.dayOfMonth}", isFestivalDay = Utility.isFestivalDay(), passive = Utility.IsPassiveFestivalDay("SquidFest") });
            foreach (int w in Barrier("winterwoke")) yield return w;
        }
    }
}
