using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-012: stops the character creation menu from accepting a player or farm name made only of spaces.
    /// A space-only farm name reads back empty after a save, which leaves the farmhand menu unable to continue.
    /// Prevention only: it changes no saves and does not touch farms that are already affected.
    /// </summary>
    internal static class BlankNameFix
    {
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            var target = AccessTools.Method(typeof(CharacterCustomization), nameof(CharacterCustomization.canLeaveMenu));
            if (target is null)
            {
                monitor.Log("Blank name fix is off: CharacterCustomization.canLeaveMenu was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(BlankNameFix), nameof(Postfix)));
        }

        private static void Postfix(CharacterCustomization __instance, ref bool __result)
        {
            // only the menus where the names are typed. The dye and dresser menus must keep working on farms that already have odd names.
            var source = __instance.source;
            bool nameIsTyped = source == CharacterCustomization.Source.NewGame || source == CharacterCustomization.Source.HostNewFarm || source == CharacterCustomization.Source.NewFarmhand;
            if (!__result || !nameIsTyped)
                return;

            // a farmhand cannot edit the farm name, it comes from the host, so only the two menus that type one check it
            bool farmNameIsTyped = source != CharacterCustomization.Source.NewFarmhand;
            __result = NameValidation.Allows(__result, farmNameIsTyped, Game1.player?.Name, Game1.player?.farmName.Value);
        }
    }
}
