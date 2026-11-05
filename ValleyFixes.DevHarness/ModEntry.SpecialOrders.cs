using StardewValley;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Special order board scenario. Dumps the game's own order data, then runs the game's own weekly refresh for each season and for
    /// the four refresh days, once on a fresh world and once on a world where every order that cannot be repeated is already done.
    /// It reads which orders the board would offer, to show whether a board can come up empty by the game's own rules.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> SpecialOrdersScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["special_orders"] = info;
            yield return 120;

            var data = DataLoader.SpecialOrders(Game1.content);
            info["orders"] = data.Select(kv => new
            {
                id = kv.Key,
                type = kv.Value.OrderType,
                repeatable = kv.Value.Repeatable,
                duration = kv.Value.Duration.ToString(),
                tags = kv.Value.RequiredTags,
                condition = kv.Value.Condition
            }).ToList();

            var team = Game1.player.team;
            var savedSeason = Game1.season;
            int savedDay = Game1.dayOfMonth;
            var states = new List<object>();
            info["states"] = states;

            foreach (string state in new[] { "fresh", "all_non_repeatable_done" })
            {
                team.completedSpecialOrders.Clear();
                team.availableSpecialOrders.Clear();
                if (state != "fresh")
                    foreach (var kv in data.Where(kv => !kv.Value.Repeatable))
                        team.completedSpecialOrders.Add(kv.Key);

                foreach (var season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
                {
                    foreach (int day in new[] { 1, 8, 15, 22 })
                    {
                        Game1.season = season;
                        Game1.dayOfMonth = day;
                        var row = new Dictionary<string, object?> { ["state"] = state, ["season"] = season.ToString(), ["day"] = day };
                        foreach (string board in new[] { "", "Qi" })
                        {
                            int eligible = data.Count(kv => kv.Value.OrderType == board && SpecialOrder.CanStartOrderNow(kv.Key, kv.Value));
                            SpecialOrder.UpdateAvailableSpecialOrders(board, forceRefresh: true);
                            row[board == "" ? "town_eligible" : "qi_eligible"] = eligible;
                            row[board == "" ? "town_offered" : "qi_offered"] = team.availableSpecialOrders.Where(o => o.orderType.Value == board).Select(o => o.questKey.Value).ToList();
                        }
                        states.Add(row);
                    }
                }
            }

            team.completedSpecialOrders.Clear();
            team.availableSpecialOrders.Clear();
            Game1.season = savedSeason;
            Game1.dayOfMonth = savedDay;
            yield return 10;
        }
    }
}

namespace ValleyFixes.DevHarness
{
    public partial class ModEntry
    {
        /// <summary>
        /// Empty board scenario. Puts an unfinished entry for every town board order in the team list (the way an order left behind by a
        /// deleted farmhand would), then plays a real overnight into day 8, a board refresh day, and reads what the board offers.
        /// </summary>
        private IEnumerable<int> EmptyBoardScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["empty_board"] = info;
            yield return 120;

            var team = Game1.player.team;
            const long fakeId = 999999999999L;
            var data = DataLoader.SpecialOrders(Game1.content);
            int seed = 500;
            foreach (var pair in data.Where(p => p.Value.OrderType == ""))
            {
                var order = SpecialOrder.GetSpecialOrder(pair.Key, seed++);
                if (order is null)
                    continue;
                order.questState.Value = SpecialOrderStatus.Complete;
                order.participants.Clear();
                order.unclaimedRewards.Clear();
                order.participants[fakeId] = true;
                team.specialOrders.Add(order);
            }
            info["heldEntries"] = team.specialOrders.Count;

            Game1.dayOfMonth = 7;
            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            int guard = 0;
            while ((Game1.newDay || Game1.fadeToBlack || Game1.activeClickableMenu != null || !StardewModdingAPI.Context.CanPlayerMove) && guard++ < 4000)
                yield return 1;
            yield return 240;

            info["dayAfter"] = Game1.dayOfMonth;
            info["townOffered"] = team.availableSpecialOrders.Count(o => o.orderType.Value == "");
            info["townEligibleByGameRule"] = data.Count(p => p.Value.OrderType == "" && SpecialOrder.CanStartOrderNow(p.Key, p.Value));
        }
    }
}
