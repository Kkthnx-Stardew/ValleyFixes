using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-043: every object or terrain feature added to or removed from a location is queued for a network send. The queue keeps each
    /// removed object alive. The game empties a location's queue only when the location has a network root, which it gets once another
    /// player has been sent that location. In single player nothing ever has a root. In multiplayer the host still has locations that
    /// no joiner has visited yet, and those pile up the same way (Forest, Town, Mountain and others in the test).
    /// Once a morning this clears the queues of the locations that have no root, the same way the game does after it sends changes.
    /// A location that has a root is left to the game. A player who visits a location later is sent all of it, not the queue.
    /// Only the main player does this. Joiners never do.
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
            if (!Context.IsMainPlayer)
                return;

            try
            {
                int locations = 0;
                int skipped = 0;
                Utility.ForEachLocation(location =>
                {
                    // a location with a root is being sent to other players, and the game empties its queue itself
                    if (location.Root != null)
                    {
                        skipped++;
                        return true;
                    }

                    foreach (var field in location.NetFields.GetFields())
                        field.MarkClean();
                    locations++;
                    return true;
                }, includeInteriors: true, includeGenerated: true);

                _monitor.Log($"Cleared the unsent change queues of {locations} locations ({skipped} with a network root left to the game).", LogLevel.Trace);
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
