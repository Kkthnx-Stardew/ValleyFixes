using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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
            }
            catch (Exception ex)
            {
                _monitor.Log($"Special order participant check failed. {ex.Message}", LogLevel.Warn);
            }
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
