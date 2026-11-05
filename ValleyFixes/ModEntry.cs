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

            // one Harmony instance for every code patch. It patches nothing until a fix applies a patch
            var harmony = new Harmony(ModManifest.UniqueID);

            if (config.FixBlankNames)
                Start("Blank name check", () => BlankNameFix.Apply(harmony, Monitor));

            if (config.FixTrashBearSardine)
                Start("Trash Bear Sardine", () => TrashBearSardineFix.Apply(harmony, Monitor));

            if (config.FixQuestItemGroundDrops)
                Start("Quest item ground drops", () => QuestItemDebrisFix.Apply(harmony, Monitor));

            if (config.FixTitleMusicMute)
                Start("Title screen mute", () => TitleMusicMuteFix.Apply(harmony, Monitor));

            if (config.SyncCoalCarts)
                Start("Coal cart sync", () => new CoalCartSyncFix(Monitor, helper, ModManifest.UniqueID).Register(harmony));

            if (config.FixStardropInterrupt)
                Start("Stardrop interrupt", () => StardropInterruptFix.Apply(harmony, Monitor));

            if (config.FixStaleMapOverrides)
                Start("Stale map changes", () => StaleMapOverrideFix.Apply(harmony, Monitor));

            if (config.CacheAssetNames)
                Start("Asset name cache", () => AssetNameCacheFix.Apply(harmony, Monitor));

            if (config.SkipEmptyLocationUpdates || config.PauseIdleLocationMonsters)
                Start("Idle location updates", () => IdleLocationFix.Apply(harmony, Monitor, config.SkipEmptyLocationUpdates, config.PauseIdleLocationMonsters));

            if (config.RetryLostJoinRequests)
                Start("Join request retry", () => LidgrenDiscoveryRetryFix.Apply(harmony, Monitor));

            if (SyncDelayLimits.Clamp(config.MultiplayerInterpolationTicks) != SyncDelayLimits.VanillaTicks)
                Start("Multiplayer delay", () => new SyncDelayTuning(Monitor, SyncDelayLimits.Clamp(config.MultiplayerInterpolationTicks)).Register(helper.Events));

            if (config.SkipDefaultTileProperties)
                Start("Default tile properties", () => TileDefaultPropertiesFix.Apply(harmony, Monitor));

            if (config.FixMiniShippingBinCrash)
                Start("Mini-Shipping Bin crash", () => MiniShippingBinFix.Apply(harmony, Monitor));

            if (config.FixFruitTreeQuality)
                Start("Fruit tree quality", () => FruitTreeQualityFix.Apply(harmony, Monitor));

            if (config.FixPierreDuplicateSeeds)
                Start("Pierre duplicate seeds", () => PierreSeedDuplicatesFix.Apply(harmony, Monitor));

            if (config.FixWeddingSpouseTokens)
                Start("Wedding spouse", () => WeddingSpouseFix.Apply(harmony, Monitor));

            if (config.FixCookingDeliveryQuestCrash)
                Start("Cooking delivery quest crash", () => DeliveryQuestCookingFix.Apply(harmony, Monitor));

            if (config.FixMenuBoxShadow)
                Start("Menu box shadow", () => MenuBoxShadowFix.Apply(harmony, Monitor));

            if (config.LogEatInterrupts)
                Start("Eat interrupt logging", () => EatInterruptLogger.Apply(harmony, Monitor));
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
