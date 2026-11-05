using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-046: a special order (Qi's orders, the town board) is only removed once its participant list is empty, and each player
    /// removes themselves when they claim their reward. A farmhand who was deleted before claiming leaves an id nobody can ever remove,
    /// so the order stays in the team list and the same order cannot be offered again.
    /// Every morning the host compares the participant ids with the players this world still has. It always logs what it finds.
    /// With repair on it also removes only those ids, and the game then removes an emptied order by itself.
    /// </summary>
    internal sealed class StaleOrderParticipantsCheck
    {
        private readonly IMonitor _monitor;
        private readonly bool _repair;

        public StaleOrderParticipantsCheck(IMonitor monitor, bool repair)
        {
            _monitor = monitor;
            _repair = repair;
        }

        public void Register(IModEvents events)
        {
            events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            try
            {
                Run();
                LogEmptyTownBoard();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Special order participant check failed. {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// The game fills the town board on days 1, 8, 15 and 22 from the orders that can start. An order whose id is still in the team list
        /// can not start again. When the board comes up empty this names the orders held that way, so a report can show why.
        /// Returns how many orders were named. It only writes to the log.
        /// </summary>
        internal int LogEmptyTownBoard()
        {
            if (Game1.dayOfMonth is not (1 or 8 or 15 or 22))
                return 0;

            var team = Game1.player?.team;
            if (team is null || team.availableSpecialOrders.Any(o => o.orderType.Value == ""))
                return 0;

            var held = new List<string>();
            foreach (var pair in DataLoader.SpecialOrders(Game1.content))
            {
                if (pair.Value.OrderType != "")
                    continue;
                var entry = team.specialOrders.FirstOrDefault(o => o.questKey.Value == pair.Key);
                if (entry is not null)
                    held.Add($"{pair.Key} ({entry.questState.Value}, {entry.participants.Length} participant(s))");
            }

            _monitor.Log(held.Count > 0
                ? $"The town special order board is empty today. {held.Count} order(s) cannot start because an entry for them is still in the team list: {string.Join(", ", held)}."
                : "The town special order board is empty today and no order is held by the team list, so the game found nothing eligible.", LogLevel.Warn);
            return held.Count;
        }

        /// <summary>Returns how many stale ids were found, and removes them when repair is on.</summary>
        internal int Run()
        {
            var team = Game1.player?.team;
            if (team?.specialOrders is null)
                return 0;

            var known = new HashSet<long>(Game1.getAllFarmers().Select(f => f.UniqueMultiplayerID));
            int total = 0;
            foreach (SpecialOrder order in team.specialOrders.ToList())
            {
                var stale = StaleParticipants.Find(order.participants.Keys, known);
                var staleRewards = StaleParticipants.Find(order.unclaimedRewards.Keys, known);
                if (stale.Count == 0 && staleRewards.Count == 0)
                    continue;

                total += stale.Count;
                _monitor.Log($"Special order '{order.questKey.Value}' (state {order.questState.Value}) lists {stale.Count} participant(s) and {staleRewards.Count} unclaimed reward(s) for players this world no longer has. {(_repair ? "Removing them." : "Left alone, repair is off.")}", _repair ? LogLevel.Info : LogLevel.Warn);

                if (!_repair)
                    continue;

                foreach (long id in stale)
                    order.participants.Remove(id);
                foreach (long id in staleRewards)
                    order.unclaimedRewards.Remove(id);
                order.MarkForRemovalIfEmpty();
            }

            return total;
        }
    }
}
