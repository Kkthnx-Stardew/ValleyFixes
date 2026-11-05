using StardewModdingAPI;

namespace ValleyFixes.Integration
{
    /// <summary>Adds ValleyFixes to the Generic Mod Config Menu, when that mod is installed.</summary>
    internal static class ConfigMenu
    {
        private const string GmcmId = "spacechase0.GenericModConfigMenu";

        public static void Register(IModHelper helper, IManifest manifest, ModConfig config, IMonitor monitor)
        {
            try
            {
                var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmId);
                if (api is null)
                    return;

                api.Register(manifest, reset: () => Reset(config), save: () => helper.WriteConfig(config));
                api.AddParagraph(manifest, () => "Changes take effect the next time the game starts. Fixes that sync players need the mod on every player's machine.");

                api.AddSectionTitle(manifest, () => "Multiplayer");
                Add(api, manifest, "Sync coal carts", "VF-058. A coal cart a farmhand empties stays full for everyone else, and for that farmhand after they leave the floor. The host keeps the list of emptied carts and shares it.", () => config.SyncCoalCarts, v => config.SyncCoalCarts = v);
                Add(api, manifest, "Retry lost join requests", "VF-063. Joining by IP address or on a local network starts with one UDP packet. If it is lost the game never sends it again and you wait 45 seconds. This sends it again up to three times. Farmhand side only.", () => config.RetryLostJoinRequests, v => config.RetryLostJoinRequests = v);
                Add(api, manifest, "Offline farmhand wallets", "VF-055. With separate wallets, gold sent to a farmhand who is not in the game is lost at the next save and reload. Host only.", () => config.FixOfflineFarmhandWallets, v => config.FixOfflineFarmhandWallets = v);
                Add(api, manifest, "Check stale special order players", "VF-046. The host logs special orders held open by a player who no longer exists in this world.", () => config.CheckStaleOrderParticipants, v => config.CheckStaleOrderParticipants = v);
                Add(api, manifest, "Repair stale special order players", "VF-046. Also remove those stale players from the order. Off by default because it edits shared state.", () => config.RepairStaleOrderParticipants, v => config.RepairStaleOrderParticipants = v);

                api.AddSectionTitle(manifest, () => "Text and data fixes");
                Add(api, manifest, "Fair third place text", "VF-007. Lewis says 200 star tokens for third place but the game gives 250.", () => config.FixFairThirdPlaceText, v => config.FixFairThirdPlaceText = v);
                Add(api, manifest, "Movie reaction scripts", "VF-001. Three movie reactions show an NPC name in place of the reaction text.", () => config.FixMovieReactionScripts, v => config.FixMovieReactionScripts = v);
                Add(api, manifest, "Chef's bundle reward", "VF-030. The remixed Chef's bundle gives 1 Pink Cake where the classic one gives 3. New remixed worlds only.", () => config.FixChefBundleReward, v => config.FixChefBundleReward = v);
                Add(api, manifest, "Trash Bear summer Sardine", "VF-022. Trash Bear can ask for a Sardine in summer, when it cannot be caught.", () => config.FixTrashBearSardine, v => config.FixTrashBearSardine = v);
                Add(api, manifest, "Portuguese single status", "VF-036. Single male farmers show the female single text on the social page in Portuguese.", () => config.FixPortugueseSingleStatus, v => config.FixPortugueseSingleStatus = v);
                Add(api, manifest, "George's wheelchair", "VF-047. George's wheels do not turn when he walks up. Copies the missing frames from his winter sheet.", () => config.FixGeorgeWheelchair, v => config.FixGeorgeWheelchair = v);

                api.AddSectionTitle(manifest, () => "Menus and memory");
                Add(api, manifest, "Block blank names", "VF-012. The new farm and farmhand menus accept a name made only of spaces. This blocks the OK button for such names.", () => config.FixBlankNames, v => config.FixBlankNames = v);
                Add(api, manifest, "Title screen mute", "VF-045. The music comes back after the About screen while the mute button still shows muted.", () => config.FixTitleMusicMute, v => config.FixTitleMusicMute = v);
                Add(api, manifest, "Trim location change queues", "VF-043. Clears network queues nobody reads so removed objects do not pile up in memory. No visible change.", () => config.TrimLocationChangeBacklog, v => config.TrimLocationChangeBacklog = v);

                api.AddSectionTitle(manifest, () => "Balance changes (off by default)");
                Add(api, manifest, "Lumberjack mahogany bonus", "VF-006. Makes the Lumberjack extra hardwood from mahogany trees apply. Raises their mahogany hardwood by about a quarter.", () => config.FixLumberjackMahoganyBonus, v => config.FixLumberjackMahoganyBonus = v);
                Add(api, manifest, "Remove quest items from the ground", "VF-044. Qi Beans and Calico Eggs dropped on the ground are removed overnight like the ones in bags and chests.", () => config.FixQuestItemGroundDrops, v => config.FixQuestItemGroundDrops = v);

                api.AddSectionTitle(manifest, () => "Logging");
                Add(api, manifest, "Stardrop cut short", "VF-059. A stardrop whose eating is cut short gave nothing and was used up. The energy bonus is now applied at once.", () => config.FixStardropInterrupt, v => config.FixStardropInterrupt = v);
                Add(api, manifest, "Festival map leftovers", "VF-060. A farmhand keeps seeing festival booths and signs after the festival ends. Farmhand side only.", () => config.FixStaleMapOverrides, v => config.FixStaleMapOverrides = v);
                Add(api, manifest, "Cache asset names", "VF-061. SMAPI builds new strings and objects every time a mod parses an asset name, and some mods do it for every sprite drawn. The answers are kept. Removes most of the garbage in big mod lists, no visible change.", () => config.CacheAssetNames, v => config.CacheAssetNames = v);
                Add(api, manifest, "Skip empty location updates", "VF-062. Locations nobody has been in for 60 frames that hold nothing are not updated every frame. Host or single player only.", () => config.SkipEmptyLocationUpdates, v => config.SkipEmptyLocationUpdates = v);
                Add(api, manifest, "Pause idle location monsters", "VF-062. Monsters in outdoor locations nobody is in are not updated. They move again when a player arrives. Host or single player only.", () => config.PauseIdleLocationMonsters, v => config.PauseIdleLocationMonsters = v);
                Add(api, manifest, "Log eating interrupts", "VF-004. Writes a log line when something cuts the eating animation short. Changes nothing in the game.", () => config.LogEatInterrupts, v => config.LogEatInterrupts = v);
            }
            catch (Exception ex)
            {
                // the config menu is a convenience, so a failure here never affects the fixes
                monitor.Log($"Could not add ValleyFixes to the Generic Mod Config Menu. The settings in config.json still work. {ex.Message}", LogLevel.Warn);
            }
        }

        private static void Add(IGenericModConfigMenuApi api, IManifest manifest, string name, string tooltip, Func<bool> get, Action<bool> set)
        {
            api.AddBoolOption(manifest, get, set, () => name, () => tooltip);
        }

        private static void Reset(ModConfig config)
        {
            var defaults = new ModConfig();
            foreach (var property in typeof(ModConfig).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
                property.SetValue(config, property.GetValue(defaults));
        }
    }
}
