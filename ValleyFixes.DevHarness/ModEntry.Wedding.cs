using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Wedding scenario. Builds the real wedding event for a farmer with a spouse and records every command that mentions Lewis, split the
    /// way the event system splits it, so a missing or broken line shows up as an empty or malformed argument.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> WeddingScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["wedding"] = info;
            yield return 120;

            var farmer = Game1.player;
            var savedSpouse = farmer.spouse;
            farmer.spouse = "Abigail";
            // the way the game has them on the day of the wedding, engaged with the countdown finished
            if (!farmer.friendshipData.TryGetValue("Abigail", out var friendship))
                farmer.friendshipData.Add("Abigail", friendship = new Friendship());
            friendship.Status = FriendshipStatus.Engaged;
            friendship.WeddingDate = WorldDate.Now();
            info["isEngaged"] = farmer.isEngaged();
            info["isMarriedOrRoommates"] = farmer.isMarriedOrRoommates();
            info["getSpouseIsNull"] = farmer.getSpouse() is null;
            var rows = new List<object>();
            info["lewisCommands"] = rows;
            try
            {
                // the game's own wedding day path, which queues the farmer when engaged with the countdown finished
                friendship.WeddingDate = WorldDate.Now();
                friendship.Status = FriendshipStatus.Engaged;
                Game1.queueWeddingsForToday();
                info["weddingsQueuedToday"] = Game1.weddingsToday.Count;
                var evt = Game1.getAvailableWeddingEvent() ?? Utility.getWeddingEvent(farmer);
                info["eventFromGameEntryPoint"] = evt is not null;
                var commands = evt.eventCommands;
                info["commandCount"] = commands.Length;
                info["commands18to24"] = commands.Skip(18).Take(7).ToList();
                info["commandsAll"] = commands.ToList();
                info["leftoverTokens"] = commands.Count(c => c.Contains('[') && c.Contains("Spouse"));
                for (int i = 0; i < commands.Length; i++)
                {
                    string command = commands[i];
                    if (!command.Contains("Lewis"))
                        continue;
                    string[] args = ArgUtility.SplitBySpaceQuoteAware(command);
                    rows.Add(new
                    {
                        index = i,
                        argCount = args.Length,
                        verb = args.Length > 0 ? args[0] : "",
                        actor = args.Length > 1 ? args[1] : "",
                        text = args.Length > 2 ? args[2] : "",
                        raw = command.Length > 160 ? command.Substring(0, 160) : command
                    });
                }
            }
            catch (Exception ex)
            {
                info["error"] = ex.ToString();
            }
            farmer.spouse = savedSpouse;
        }
    }
}
