using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-043: in single player, every object or terrain feature added to or removed from a location is queued for a network send
    /// that never happens, because nothing is connected. The queue keeps each removed object alive for the rest of the session.
    /// Once a morning this clears those queues the same way the game does after it sends changes to other players.
    /// It does nothing in multiplayer, where the game drains the queues itself.
    /// </summary>
    internal sealed class LocationBacklogTrim
    {
        private readonly IMonitor _monitor;
        private IModEvents? _events;

        public LocationBacklogTrim(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Register(IModEvents events)
        {
            _events = events;
            events.GameLoop.DayStarted += OnDayStarted;
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (Context.IsMultiplayer)
                return;

            try
            {
                int locations = 0;
                Utility.ForEachLocation(location =>
                {
                    foreach (var field in location.NetFields.GetFields())
                        field.MarkClean();
                    locations++;
                    return true;
                }, includeInteriors: true, includeGenerated: true);

                _monitor.Log($"Cleared the unsent change queues of {locations} locations.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                // this only saves memory, so on any trouble it switches itself off for the rest of the session
                _events!.GameLoop.DayStarted -= OnDayStarted;
                _monitor.Log($"Location change queue trimming turned itself off after an error. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
