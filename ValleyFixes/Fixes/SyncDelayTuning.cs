using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-065: the game shows other players, monsters and every shared change about 250 ms late on purpose. Each machine keeps a
    /// 15 tick (a tick is 1/60 s) buffer so movement stays smooth when packets arrive unevenly. In the test, how late things look
    /// followed that number and nothing else (15 ticks gave 258 ms, 8 gave 141 ms, 4 gave 72 ms). How often changes are sent made
    /// no difference to it. On a link with 15 ms jitter and 1% loss, 4 ticks froze up to 3.7% of frames, against up to 1.8% at 15 and at 8.
    /// This sets the buffer on this machine only, so each player can pick their own. It does nothing at the game's own value of 15,
    /// and it leaves the value alone if another mod has already changed it.
    /// </summary>
    internal sealed class SyncDelayTuning
    {

        private readonly IMonitor _monitor;
        private readonly int _ticks;

        public SyncDelayTuning(IMonitor monitor, int ticks)
        {
            _monitor = monitor;
            _ticks = ticks;
        }

        public void Register(IModEvents events)
        {
            events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            try
            {
                var multiplayer = Game1.Multiplayer;
                if (multiplayer.defaultInterpolationTicks != SyncDelayLimits.VanillaTicks)
                {
                    _monitor.Log($"Multiplayer delay is left alone: something else already set it to {multiplayer.defaultInterpolationTicks} ticks.", LogLevel.Info);
                    return;
                }

                multiplayer.defaultInterpolationTicks = _ticks;
                _monitor.Log($"Multiplayer delay is {_ticks} ticks ({_ticks * 1000 / 60} ms) instead of {SyncDelayLimits.VanillaTicks} ({SyncDelayLimits.VanillaTicks * 1000 / 60} ms).", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not set the multiplayer delay, so the game's own value stays. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
