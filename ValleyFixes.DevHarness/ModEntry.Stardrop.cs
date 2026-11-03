using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Stardrop scenario. Eats a stardrop three ways and reads the maximum energy each time: left alone, cut short by
    /// completelyStopAnimatingOrDoingAction (what a stray menu or warp does), and cut short with a mail-style input spam.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> EatStardrop(Farmer p, string tag, List<object> rows, bool interrupt)
        {
            ResetPlayer(p);
            yield return 60;
            float maxBefore = p.maxStamina.Value;
            var drop = ItemRegistry.Create("(O)434");
            p.Items[0] = drop;
            p.CurrentToolIndex = 0;
            p.mostRecentlyGrabbedItem = drop;
            p.itemToEat = drop;
            p.eatHeldObject();
            yield return 3;
            var during = SnapDetailed(p);
            if (interrupt)
                p.completelyStopAnimatingOrDoingAction();
            yield return 600;
            var after = SnapDetailed(p);
            yield return 900;
            rows.Add(new { tag, interrupt, maxBefore, maxAfter = p.maxStamina.Value, gained = p.maxStamina.Value - maxBefore, during, after, muchLater = SnapDetailed(p) });
            p.maxStamina.Value = (int)maxBefore;
            p.stamina = p.MaxStamina;
            p.forceCanMove();
            p.freezePause = 0;
            Game1.screenGlow = false;
            p.stopGlowing();
        }

        private IEnumerable<int> StardropScenario()
        {
            var rows = new List<object>();
            _results["stardrop"] = rows;
            Farmer p = Game1.player;
            foreach (int w in EatStardrop(p, "plain", rows, false)) yield return w;
            foreach (int w in EatStardrop(p, "interrupted", rows, true)) yield return w;
            foreach (int w in EatStardrop(p, "plain-again", rows, false)) yield return w;
        }
    }
}
