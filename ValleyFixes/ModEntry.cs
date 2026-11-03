using HarmonyLib;
using StardewModdingAPI;
using ValleyFixes.Diagnostics;
using ValleyFixes.Fixes;
using ValleyFixes.Integration;

namespace ValleyFixes
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            ModConfig config;
            try
            {
                config = helper.ReadConfig<ModConfig>() ?? new ModConfig();
            }
            catch (Exception ex)
            {
                // a config.json that cannot be read must not stop the mod from loading, and the file is left alone
                config = new ModConfig();
                Monitor.Log($"Could not read config.json, so the default settings are used. Fix it or delete it. ({ex.Message})", LogLevel.Warn);
            }

            helper.Events.GameLoop.GameLaunched += (_, _) => ConfigMenu.Register(helper, ModManifest, config, Monitor);

            // each fix starts on its own, so one that fails to start never takes the others down with it
            if (config.FixFairThirdPlaceText)
                Start("Fair third place text", () => new FairTokenTextFix(Monitor).Register(helper.Events));

            if (config.FixMovieReactionScripts)
                Start("Movie reaction scripts", () => new MovieReactionFix(Monitor).Register(helper.Events));

            if (config.TrimLocationChangeBacklog)
                Start("Location change queue trim", () => new LocationBacklogTrim(Monitor).Register(helper.Events));

            if (config.CheckStaleOrderParticipants)
                Start("Special order participant check", () => new StaleOrderParticipantsCheck(Monitor, config.RepairStaleOrderParticipants).Register(helper.Events));

            if (config.FixGeorgeWheelchair)
                Start("George wheelchair", () => new GeorgeWheelchairFix(Monitor, helper.GameContent).Register(helper.Events));

            if (config.FixOfflineFarmhandWallets)
                Start("Offline farmhand wallets", () => new OfflineFarmhandWalletFix(Monitor).Register(helper.Events));

            if (config.FixPortugueseSingleStatus)
                Start("Portuguese single status", () => new PortugueseSingleStatusFix(Monitor).Register(helper.Events));

            if (config.FixChefBundleReward)
                Start("Chef's bundle reward", () => new ChefBundleRewardFix(Monitor).Register(helper.Events));

            if (config.FixLumberjackMahoganyBonus)
                Start("Lumberjack mahogany bonus", () => new LumberjackHardwoodFix(Monitor).Register(helper.Events));

            // one Harmony instance for every code patch, created only if a patching fix is on
            Harmony? harmony = config.FixBlankNames || config.FixTrashBearSardine || config.LogEatInterrupts || config.FixQuestItemGroundDrops || config.FixTitleMusicMute || config.SyncCoalCarts || config.FixStardropInterrupt ? new Harmony(ModManifest.UniqueID) : null;

            if (config.FixBlankNames)
                Start("Blank name check", () => BlankNameFix.Apply(harmony!, Monitor));

            if (config.FixTrashBearSardine)
                Start("Trash Bear Sardine", () => TrashBearSardineFix.Apply(harmony!, Monitor));

            if (config.FixQuestItemGroundDrops)
                Start("Quest item ground drops", () => { QuestItemDebrisFix.ModLog = Monitor; QuestItemDebrisFix.Apply(harmony!, Monitor); });

            if (config.FixTitleMusicMute)
                Start("Title screen mute", () => TitleMusicMuteFix.Apply(harmony!, Monitor));

            if (config.SyncCoalCarts)
                Start("Coal cart sync", () => new CoalCartSyncFix(Monitor, helper, ModManifest.UniqueID).Register(harmony!));

            if (config.FixStardropInterrupt)
                Start("Stardrop interrupt", () => StardropInterruptFix.Apply(harmony!, Monitor));

            if (config.LogEatInterrupts)
                Start("Eat interrupt logging", () => EatInterruptLogger.Apply(harmony!, Monitor));
        }

        private void Start(string name, Action register)
        {
            try
            {
                register();
            }
            catch (Exception ex)
            {
                Monitor.Log($"{name} could not start and is off. The other fixes are unaffected. {ex.Message}", LogLevel.Error);
            }
        }
    }
}
