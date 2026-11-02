using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-055: with separate wallets, gold sent to a farmhand who is not in the game (from the Manor ledger, or paid for their
    /// shipping bin overnight) is added to the host's team wallet list. But a farmhand who has not joined since the world was loaded
    /// is a saved copy that carries a private team of its own, and the save writes THAT copy's wallet. So the gold is in the game until
    /// the next save and reload, then it is gone.
    /// Just before each save the host copies the shared wallet value into the saved copy of every such farmhand. It sets the value
    /// (it never adds), so running it twice changes nothing.
    /// </summary>
    internal sealed class OfflineFarmhandWalletFix
    {
        private readonly IMonitor _monitor;
        private IModEvents? _events;

        public OfflineFarmhandWalletFix(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Register(IModEvents events)
        {
            _events = events;
            events.GameLoop.Saving += OnSaving;
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            try
            {
                Sync();
            }
            catch (Exception ex)
            {
                // a failure here must never block a save, so the fix turns itself off for the rest of the session
                _events!.GameLoop.Saving -= OnSaving;
                _monitor.Log($"Offline farmhand wallet fix turned itself off after an error. {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Returns how many saved farmhand copies were corrected.</summary>
        internal int Sync()
        {
            var host = Game1.player;
            var team = host?.team;
            if (team is null || !team.useSeparateWallets.Value)
                return 0;

            int corrected = 0;
            foreach (Farmer hand in Game1.netWorldState.Value.farmhandData.Values)
            {
                // an online farmhand, or one who joined this session, shares the host's team and is already right
                if (hand is null || hand.isUnclaimedFarmhand || ReferenceEquals(hand.teamRoot, host!.teamRoot))
                    continue;
                if (!team.individualMoney.TryGetValue(hand.UniqueMultiplayerID, out var shared))
                    continue;

                var own = hand.teamRoot.Value.GetMoney(hand);
                if (own.Value == shared.Value)
                    continue;

                _monitor.Log($"Farmhand '{hand.Name}' is offline and their saved wallet said {own.Value}g while the shared wallet has {shared.Value}g. The save gets {shared.Value}g.", LogLevel.Info);
                own.Value = shared.Value;
                corrected++;
            }

            return corrected;
        }
    }
}
