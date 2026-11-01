using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-045: on the title screen, closing the About screen (the ? button) or finishing the load menu scan resets the game options
    /// to their defaults, which turns the music volume back on. The mute button still shows muted, so the music plays against the button
    /// and the next click on it does the opposite of what the icon says.
    /// This runs after those two resets and mutes again, only when the button shows muted and the music is not already off.
    /// </summary>
    internal static class TitleMusicMuteFix
    {
        // the muted icon is drawn from this x position of the cursors sheet (128 is the unmuted one)
        private const int MutedIconX = 137;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            var back = AccessTools.Method(typeof(TitleMenu), nameof(TitleMenu.ReturnToMainTitleScreen));
            var scan = AccessTools.Method(typeof(LoadGameMenu), "saveFileScanComplete");
            if (back is null || scan is null)
            {
                monitor.Log("Title screen mute fix is off: TitleMenu.ReturnToMainTitleScreen or LoadGameMenu.saveFileScanComplete was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var postfix = new HarmonyMethod(typeof(TitleMusicMuteFix), nameof(Postfix));
            harmony.Patch(back, postfix: postfix);
            harmony.Patch(scan, postfix: postfix);
        }

        private static void Postfix()
        {
            try
            {
                if (Game1.activeClickableMenu is TitleMenu title && title.muteMusicButton?.sourceRect.X == MutedIconX && Game1.options.musicVolumeLevel != 0f)
                    Utility.disableMusic();
            }
            catch
            {
                // a cosmetic fix, so it never gets in the way of the menu
            }
        }
    }
}
