using System.Reflection;
using StardewModdingAPI;
using ValleyFixes.Fixes;

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
                api.AddParagraph(manifest, () => "Changes take effect the next time the game starts. Every fix can be switched off on its own. Fixes marked for every player only work fully when each player has ValleyFixes.");

                api.AddSectionTitle(manifest, () => "Multiplayer and connection");
                api.AddNumberOption(manifest, () => config.MultiplayerInterpolationTicks, v => config.MultiplayerInterpolationTicks = v, () => "Multiplayer delay (ticks)",
                    () => "How long this machine holds back what other players and monsters do, so movement stays smooth. The game uses 15 ticks, which shows them about 250 ms late. 8 shows them 141 ms late and 4 shows them 72 ms late, but 4 froze up to 3.7% of frames on a link with jitter, against up to 1.8% at 15. Only you are affected. (VF-065)",
                    min: SyncDelayLimits.MinTicks, max: SyncDelayLimits.VanillaTicks, interval: 1);
                Toggle(api, manifest, config, nameof(ModConfig.RetryLostJoinRequests), "Retry lost join requests",
                    "Joining by IP address or on a local network sends one packet. If it is lost the game waits 45 seconds and times out. This sends it again up to three times. Joining player only. (VF-063)");
                Toggle(api, manifest, config, nameof(ModConfig.SyncCoalCarts), "Sync Mines coal carts",
                    "A coal cart that one player empties stays full for the others. The host now keeps the list and shares it. Needed on every player's machine. (VF-058)");
                Toggle(api, manifest, config, nameof(ModConfig.FixMiniShippingBinCrash), "Mini-Shipping Bin crash",
                    "With separate wallets, an empty slot in a farmhand's part of a Mini-Shipping Bin crashed the host when the day ended. Empty slots are removed first. Host only. (VF-067)");
                Toggle(api, manifest, config, nameof(ModConfig.FixOfflineFarmhandWallets), "Offline farmhand gold",
                    "With separate wallets, gold sent to a farmhand who is not in the game was lost at the next save and reload. Host only. (VF-055)");
                Toggle(api, manifest, config, nameof(ModConfig.FixStaleMapOverrides), "Festival leftovers (farmhand)",
                    "A farmhand kept seeing the SquidFest booth and sign after the festival. Joining player only. (VF-060)");
                Toggle(api, manifest, config, nameof(ModConfig.CheckStaleOrderParticipants), "Log stale special orders",
                    "The host writes a log line when a special order is held open by a farmhand who no longer exists. Changes nothing. (VF-046)");
                Toggle(api, manifest, config, nameof(ModConfig.RepairStaleOrderParticipants), "Repair stale special orders",
                    "Also removes those deleted players from the order. Off by default because it edits shared multiplayer data. (VF-046)");

                api.AddSectionTitle(manifest, () => "Performance");
                Toggle(api, manifest, config, nameof(ModConfig.CacheAssetNames), "Cache asset names",
                    "Some mods ask SMAPI to parse an asset name after every sprite drawn, which makes thousands of short lived objects a frame. The answers are kept. Cuts garbage a lot in big mod lists. No visible change. (VF-061)");
                Toggle(api, manifest, config, nameof(ModConfig.SkipEmptyLocationUpdates), "Skip empty locations",
                    "A location nobody has been in for 60 frames and that holds nothing is not updated every frame. Host or single player. (VF-062)");
                Toggle(api, manifest, config, nameof(ModConfig.PauseIdleLocationMonsters), "Pause monsters nobody can see",
                    "Monsters in outdoor locations nobody is in stand still until a player arrives. Never on the farm, in buildings, mines or the volcano. Host or single player. (VF-062)");
                Toggle(api, manifest, config, nameof(ModConfig.SkipDefaultTileProperties), "Slim map tiles",
                    "SMAPI stores a rotation and a flip of 0 on every tile of every Tiled (TMX) mod map. Not storing them saved 125 MB of memory with 101 mods. Nothing looks different. (VF-066)");
                Toggle(api, manifest, config, nameof(ModConfig.TrimLocationChangeBacklog), "Free removed objects",
                    "The game queues every removed object for a network send that never happens. This clears the queues each morning so memory stops growing. No visible change. (VF-043)");

                api.AddSectionTitle(manifest, () => "Bug fixes");
                Toggle(api, manifest, config, nameof(ModConfig.FixWeddingSpouseTokens), "Lewis's wedding speech",
                    "On the day of your wedding the script could not find your spouse, so Lewis's matrimonial speech came out as a broken fragment. The spouse is now found. The game's developers list it as fixed in 1.6.16. (VF-071)");
                Toggle(api, manifest, config, nameof(ModConfig.FixCookingDeliveryQuestCrash), "Cooking delivery quest crash",
                    "An item delivery quest asking for a cooked dish crashed the game about a third of the time, on days fixed by your save. The game's developers list it as fixed in 1.6.16. (VF-070)");
                Toggle(api, manifest, config, nameof(ModConfig.FixPierreDuplicateSeeds), "Pierre duplicate seeds",
                    "After you give Pierre his stock list, wheat, corn and sunflower seeds were listed twice, once out of season at 1.5 times the price. The dearer copy is dropped. (VF-069)");
                Toggle(api, manifest, config, nameof(ModConfig.FixStardropInterrupt), "Stardrop cut short",
                    "A stardrop whose eating was cut short gave nothing and was already used up. The energy bonus is now applied at once. (VF-059)");
                Toggle(api, manifest, config, nameof(ModConfig.FixTrashBearSardine), "Trash Bear summer Sardine",
                    "Trash Bear could ask for a Sardine in summer, when it cannot be caught. (VF-022)");
                Toggle(api, manifest, config, nameof(ModConfig.FixChefBundleReward), "Chef's bundle reward",
                    "The remixed Chef's bundle gave 1 Pink Cake where the classic one gives 3. New remixed worlds only. (VF-030)");
                Toggle(api, manifest, config, nameof(ModConfig.FixBlankNames), "Block blank names",
                    "The new farm and farmhand menus accepted a name of only spaces, which can leave a farm nobody can join. The OK button is blocked for such names. (VF-012)");
                Toggle(api, manifest, config, nameof(ModConfig.FixTitleMusicMute), "Title screen mute",
                    "The music came back after the About screen while the mute button still showed muted. (VF-045)");
                Toggle(api, manifest, config, nameof(ModConfig.FixGeorgeWheelchair), "George's wheelchair",
                    "George's wheels did not turn when he walks up. The missing frames are copied from his winter sheet. (VF-047)");
                Toggle(api, manifest, config, nameof(ModConfig.FixMenuBoxShadow), "Menu box shadow corner",
                    "Menu boxes such as item tooltips lacked one corner piece of their drop shadow. The piece is drawn. Cosmetic only. (VF-064)");

                api.AddSectionTitle(manifest, () => "Text fixes");
                Toggle(api, manifest, config, nameof(ModConfig.FixFairThirdPlaceText), "Fair third place speech",
                    "Lewis says 200 star tokens for third place but the game gives 250. (VF-007)");
                Toggle(api, manifest, config, nameof(ModConfig.FixMovieReactionScripts), "Movie reactions",
                    "Three movie reactions showed an NPC name in place of the reaction text. (VF-001)");
                Toggle(api, manifest, config, nameof(ModConfig.FixPortugueseSingleStatus), "Portuguese single status",
                    "Single male farmers showed the female single text on the social page in Portuguese. (VF-036)");

                api.AddSectionTitle(manifest, () => "Off by default (changes gameplay)");
                Toggle(api, manifest, config, nameof(ModConfig.FixFruitTreeQuality), "Fruit tree quality",
                    "Fruit that grew before its tree aged to the next quality kept the old quality when picked. This raises it to the tree's current quality, as it did in 1.5. (VF-068)");
                Toggle(api, manifest, config, nameof(ModConfig.FixLumberjackMahoganyBonus), "Lumberjack mahogany bonus",
                    "The extra hardwood the Lumberjack profession is meant to give from mahogany trees never applied. This makes it apply, which raises their mahogany hardwood by about a quarter. (VF-006)");
                Toggle(api, manifest, config, nameof(ModConfig.FixQuestItemGroundDrops), "Clear quest items on ground",
                    "Quest items such as Qi Beans lying on the ground survived the overnight cleanup. This removes them like the ones in bags and chests. (VF-044)");

                api.AddSectionTitle(manifest, () => "Diagnostics");
                Toggle(api, manifest, config, nameof(ModConfig.LogEatInterrupts), "Log eating interrupts",
                    "Writes a log line naming the code that cuts the eating animation short. Changes nothing in the game. (VF-004)");
            }
            catch (Exception ex)
            {
                // the config menu is a convenience, so a failure here never affects the fixes
                monitor.Log($"Could not add ValleyFixes to the Generic Mod Config Menu. The settings in config.json still work. {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Adds one on/off option bound to a <see cref="ModConfig"/> property by name, so a rename is caught by the compiler.</summary>
        private static void Toggle(IGenericModConfigMenuApi api, IManifest manifest, ModConfig config, string property, string name, string tooltip)
        {
            PropertyInfo info = typeof(ModConfig).GetProperty(property) ?? throw new InvalidOperationException($"ModConfig has no setting called {property}.");
            api.AddBoolOption(manifest, () => (bool)info.GetValue(config)!, value => info.SetValue(config, value), () => name, () => tooltip);
        }

        private static void Reset(ModConfig config)
        {
            var defaults = new ModConfig();
            foreach (var property in typeof(ModConfig).GetProperties().Where(p => (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int)) && p.CanWrite))
                property.SetValue(config, property.GetValue(defaults));
        }
    }
}
