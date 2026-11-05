# Compatibility

Tested against Stardew Valley 1.6.15 build 24356 with SMAPI 4.5.2 on Windows 11. The manifest asks for game 1.6.15 or newer and SMAPI 4.2.1 or newer. Other platforms, other game builds and other SMAPI versions were not tested.

## How each fix hooks in

| Setting | Hook | Touches | Default |
|---|---|---|---|
| `FixFairThirdPlaceText` | `AssetRequested`, late | One key in `Strings/StringsFromCSFiles`, every language | on |
| `FixMovieReactionScripts` | `AssetRequested`, late | Three scripts in `Data/MoviesReactions` | on |
| `FixChefBundleReward` | `AssetRequested`, late | One reward in `Data/RandomBundles` | on |
| `FixPortugueseSingleStatus` | `AssetRequested`, late | One key in `Strings/StringsFromCSFiles` for pt-BR only | on |
| `FixGeorgeWheelchair` | `AssetRequested`, late, reads `Characters/George_Winter` | Frames 2 and 4 of the up row of `Characters/George`, only when the sheets match vanilla | on |
| `FixLumberjackMahoganyBonus` | `AssetRequested`, late | Two `ChopItems` entries in `Data/WildTrees` | **off** |
| `TrimLocationChangeBacklog` | `DayStarted` event, calls the game's own `MarkClean` on each location's net fields | The unsent change queues of locations that have no network root. Main player only | on |
| `FixTitleMusicMute` | Harmony postfix | `TitleMenu.ReturnToMainTitleScreen` and `LoadGameMenu.saveFileScanComplete` | on |
| `FixBlankNames` | Harmony postfix | `CharacterCustomization.canLeaveMenu` | on |
| `FixTrashBearSardine` | Harmony postfix | `Utility.getRandomPureSeasonalItem` | on |
| `FixStardropInterrupt` | Harmony prefix and postfix | `Farmer.completelyStopAnimatingOrDoingAction`, only for the local player eating a stardrop | on |
| `FixPierreDuplicateSeeds` | Harmony postfix | The stock `ShopBuilder.GetShopStock` builds for `SeedShop`, and only the six wheat, corn and sunflower entries. Skipped if another mod patches the method | on |
| `FixCookingDeliveryQuestCrash` | Harmony transpiler | `ItemDeliveryQuest.loadQuestInfo`, one jump. Skipped if another mod patches it or if the method does not look the way it was read | on |
| `FixWeddingSpouseTokens` | Harmony prefix and finalizer on `Utility.getWeddingEvent`, postfix on `Farmer.getSpouse` | `getSpouse` answers for an engaged farmer, only while the wedding event is being built. Skipped if another mod patches either method | on |
| `FixMenuBoxShadow` | Harmony prefix | `IClickableMenu.drawTextureBox`. Skipped if another mod patches it | on |
| `FixQuestItemGroundDrops` | Harmony postfix | `Game1._RecursiveRemoveThisNormalItemLocation` | **off** |
| `CheckStaleOrderParticipants` | `DayStarted` event, main player only, log only | Reads `team.specialOrders`, `team.availableSpecialOrders` and `Data/SpecialOrders` | on |
| `RepairStaleOrderParticipants` | Same event, removes ids that belong to no player | `participants` and `unclaimedRewards` of special orders | **off** |
| `FixOfflineFarmhandWallets` | `Saving` event, main player only | The wallet of the saved copy of each farmhand who is not in the game | on |
| `SyncCoalCarts` | Harmony postfix on `MineShaft.updateMineLevelData`, prefix on `MineShaft.checkAction`, SMAPI mod messages | A list of emptied cart floors, kept by the host in one SMAPI save data entry (`coal-carts`) | on |
| `FixMiniShippingBinCrash` | Harmony postfix | `Chest.clearNulls`, only while a new day is being processed. Skipped if another mod patches it | on |
| `FixStaleMapOverrides` | Harmony prefix | `GameLocation.MakeMapModifications`, the farmhand's Beach only | on |
| `RetryLostJoinRequests` | Harmony postfix on the `LidgrenClient` constructor, prefix on `attemptConnection` | The retry limit and wait of the join request, and the address text restored before each attempt. Skipped if another mod patches either member | on |
| `MultiplayerInterpolationTicks` | `SaveLoaded` event | `Game1.Multiplayer.defaultInterpolationTicks` on this machine. Does nothing at 15, and leaves the value alone if another mod already changed it | 15 (no change) |
| `CacheAssetNames` | Harmony prefix and postfix | SMAPI's `ContentCoordinator.ParseAssetName`. Skipped if another mod patches it | on |
| `SkipEmptyLocationUpdates` | Harmony prefix | `GameLocation.updateEvenIfFarmerIsntHere`. Skipped if another mod patches it | on |
| `PauseIdleLocationMonsters` | Harmony prefix | `Monster.update`. Skipped if another mod patches it | on |
| `SkipDefaultTileProperties` | Harmony prefix | SMAPI's bundled `TMXTile.TMXExtensions.SetRotationValue` and `SetFlip`. Skipped if another mod patches either | on |
| `FixFruitTreeQuality` | Harmony prefix | `FruitTree.shake` and `FruitTree.performToolAction`, sets the quality of the fruit on the tree before the game drops it | **off** |
| `LogEatInterrupts` | Harmony prefix, log only | `Farmer.completelyStopAnimatingOrDoingAction` | on |

One transpiler, for the delivery quest, which patches nothing if the method has changed. Patches that run often are the four performance ones and the menu box shadow. Each does a few comparisons and returns. They are also the ones that step aside when another mod patches the same method, and they say so in the SMAPI log. The only data written is `config.json` and, in multiplayer, the small coal cart list in the save's SMAPI data.

## Failure behavior

- Each fix starts on its own. If one fails to start, the log shows one error and the others still run.
- The asset edits only change an entry when it still matches what was expected. If the game or another mod changed it, the edit does nothing and logs at trace level.
- If a Harmony target is missing after a game or SMAPI update, that fix logs one warning and stays off.
- Patches catch their own errors, so the game's own code still runs if one of them fails.

## Other mods

- Edits run late, so a Content Patcher pack that changes the same entry first is judged by what it left behind. A pack that rewrites the Fair third place line without a lone 200, or the Chef's reward to something other than `1 Pink Cake`, makes the fix skip itself.
- `FixLumberjackMahoganyBonus` changes the id in `Data/WildTrees` that other mods may match on (`(O)709`). That is why it is off by default.
- Mods that do the same job as one of these fixes make it step aside, without any setting to change. Fix Hover Shadows covers `FixMenuBoxShadow`. Mods that patch the same SMAPI or game methods as the performance fixes do the same.
- The barn animal double shadow that Barn Animals' Double Shadows Fix repairs does not happen in 1.6.15. That mod can be removed.
- Tested together with a copy of a real 101 mod list (Stardew Valley Expanded, Ridgeside Village, East Scarp, Content Patcher, SpaceCore and others) on one save, single player and with one joining player. Every other mod combination is untested.

## Multiplayer

Tested with a host and one joining player, each a separate game process on the same PC (see `REPRODUCTION.md`), with two joining players on a clean profile, and with delay, jitter and packet loss added by a relay. Not tested: split screen, four players, a joiner on another PC or over the internet, a Steam invite, a dedicated server. By design:

- Text and menu fixes are local to each player. Install on every player who should see the corrected text.
- `FixChefBundleReward` decides the reward when a host creates a new remixed world, so only the host needs it at that moment. Existing worlds keep the bundles they were created with.
- `FixTrashBearSardine` is deterministic for a given world id, so players with and without the mod could see different Trash Bear requests in summer in 1 case of about 16.
- `FixLumberjackMahoganyBonus` changes drops on the machine that fells the tree.
- `CheckStaleOrderParticipants` runs on the host only. `RepairStaleOrderParticipants` edits state that every player shares, was only tested with a made-up id and is off by default.
- `FixQuestItemGroundDrops` and `FixTitleMusicMute` run on each machine for what that machine sees.
- `TrimLocationChangeBacklog` runs on the main player only. It skips any location that has a network root, because the game empties that queue itself. A joiner who walks into a cleared location later is sent the whole location. After 20 nights the joiner saw the same object and feature counts as the host in seven visited locations, with and without the fix.
- `FixOfflineFarmhandWallets` runs on the main player only and only when the world uses separate wallets. It sets the saved copy's wallet to the shared value, it never adds, so a second run changes nothing.
- `SyncCoalCarts` needs the mod on every player. Mixed setups were not tested.
- `RetryLostJoinRequests`, `FixStaleMapOverrides` and `MultiplayerInterpolationTicks` act on the machine they are installed on. The host does not need them.
- `FixMiniShippingBinCrash` runs where the day is processed, which is the host. A farmhand does not need it.
- `FixPierreDuplicateSeeds` acts on the machine that opens the shop. `FixCookingDeliveryQuestCrash` and `FixWeddingSpouseTokens` act on the machine that builds the quest or the wedding event. A wedding between two farmers uses another path and was not tested.
- `FixFruitTreeQuality` acts on the machine that shakes or chops the tree. It was tested in single player only.
- `SkipEmptyLocationUpdates` and `PauseIdleLocationMonsters` only act on the host or in single player. A joiner never skips anything.

## Known limits

- Farms already saved with a space-only farm name stay stuck. `FixBlankNames` only prevents new ones.
- Italian and Russian show the female "single" text to everyone on the social page. The game never reads the male string for those languages, and a data edit cannot fix that.
- Worlds already created keep the 1 Pink Cake Chef's bundle.
- `TrimLocationChangeBacklog` only covers locations. The queues on the player, world state and team were not measured as a problem and are left alone.
- `RepairStaleOrderParticipants` removes only ids that belong to no player in the world, but a real deleted farmhand was never tested.
- `FixGeorgeWheelchair` does nothing if another mod changed either George sheet, by design.
- A host and a joiner sometimes freeze in the game's own new day save (seen twice in about 20 two-player runs, once with every ValleyFixes fix turned off). Not caused by this mod and not fixed by it.
- The eating freeze (VF-004) is not fixed. The logger only names the cause when it happens.
- Memory and speed numbers come from one PC and one real save. They show where the cost is, not what every PC will see.
