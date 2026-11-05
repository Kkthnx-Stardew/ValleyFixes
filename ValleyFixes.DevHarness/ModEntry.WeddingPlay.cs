using StardewValley;
using StardewValley.Menus;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Wedding playback scenario. Builds the wedding event through the game's own wedding day path for an engaged farmer, runs it in the
    /// Town the way the debug wedding command does, closes every dialogue box that opens, and records the dialogue lines that were shown
    /// and how the event ended. The game's own errors for the run are in the SMAPI log.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> WeddingPlayScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["wedding_play"] = info;
            yield return 120;

            var farmer = Game1.player;
            farmer.spouse = "Abigail";
            if (!farmer.friendshipData.TryGetValue("Abigail", out var friendship))
                farmer.friendshipData.Add("Abigail", friendship = new Friendship());
            friendship.Status = FriendshipStatus.Engaged;
            friendship.WeddingDate = WorldDate.Now();
            Game1.queueWeddingsForToday();
            var evt = Game1.getAvailableWeddingEvent();
            info["eventBuilt"] = evt is not null;
            if (evt is null)
                yield break;

            var shown = new List<string>();
            var locationRequest = Game1.getLocationRequest("Town");
            locationRequest.OnLoad += () => Game1.currentLocation.currentEvent = evt;
            int x = 8, y = 8;
            Utility.getDefaultWarpLocation(locationRequest.Name, ref x, ref y);
            Game1.warpFarmer(locationRequest, x, y, Game1.player.FacingDirection);

            int ticks = 0;
            bool started = false;
            int idleAfterEnd = 0;
            while (ticks++ < 9000)
            {
                if (Game1.eventUp)
                    started = true;
                if (Game1.activeClickableMenu is DialogueBox box)
                {
                    try
                    {
                        string line = box.characterDialogue is { } spoken
                            ? $"{box.characterDialogue.speaker?.Name}: " + string.Join(" / ", spoken.dialogues.Select(d => d.Text))
                            : string.Join(" ", box.dialogues ?? new List<string>());
                        if (shown.Count < 40 && (shown.Count == 0 || shown[^1] != line))
                            shown.Add(line);
                    }
                    catch
                    {
                    }
                    // the same call a click on the box makes once its text is fully shown
                    box.receiveLeftClick(box.xPositionOnScreen + 8, box.yPositionOnScreen + 8, playSound: false);
                    if (Game1.activeClickableMenu is DialogueBox still && still == box)
                        box.closeDialogue();
                }
                else if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not DialogueBox)
                {
                    Game1.exitActiveMenu();
                }

                if (started && !Game1.eventUp && !Game1.fadeToBlack)
                {
                    if (++idleAfterEnd > 120)
                        break;
                }
                yield return 1;
            }

            info["eventCurrentCommand"] = evt.CurrentCommand;
            info["eventCommandCount"] = evt.eventCommands.Length;
            info["eventPlayerControl"] = Game1.player.CanMove;
            info["eventFreeze"] = Game1.player.freezePause;
            info["eventStarted"] = started;
            info["eventStillRunning"] = Game1.eventUp;
            info["ticksUsed"] = ticks;
            info["dialogueShown"] = shown;
            info["statusAfter"] = friendship.Status.ToString();
            info["location"] = Game1.currentLocation?.NameOrUniqueName;
        }
    }
}
