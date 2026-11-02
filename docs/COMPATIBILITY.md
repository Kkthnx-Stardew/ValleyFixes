# Compatibility

Tested against Stardew Valley 1.6.15 build 24356 with SMAPI 4.5.2 on Windows 11. Nothing else has been tested. The manifest asks for game 1.6.15 or newer and SMAPI 4.2.1 or newer.

## How each fix hooks in

| Setting | Hook | Touches | Default |
|---|---|---|---|
| `FixFairThirdPlaceText` | `AssetRequested`, late | One key in `Strings/StringsFromCSFiles`, every language | on |
| `FixMovieReactionScripts` | `AssetRequested`, late | Three scripts in `Data/MoviesReactions` | on |
| `FixChefBundleReward` | `AssetRequested`, late | One reward in `Data/RandomBundles` | on |
| `FixPortugueseSingleStatus` | `AssetRequested`, late | One key in `Strings/StringsFromCSFiles` for pt-BR only | on |
| `FixLumberjackMahoganyBonus` | `AssetRequested`, late | Two `ChopItems` entries in `Data/WildTrees` | **off** |
| `TrimLocationChangeBacklog` | `DayStarted` event, calls the game's own `MarkClean` on each location's net fields | The unsent change queues of every location, single player only | on |
| `FixGeorgeWheelchair` | `AssetRequested`, late, reads `Characters/George_Winter` | Frames 2 and 4 of the up row of `Characters/George`, only when the sheets match vanilla | on |
| `FixTitleMusicMute` | Harmony postfix | `TitleMenu.ReturnToMainTitleScreen` and `LoadGameMenu.saveFileScanComplete` | on |
| `CheckStaleOrderParticipants` | `DayStarted` event, host only, log only | Reads `team.specialOrders` | on |
| `RepairStaleOrderParticipants` | Same event, removes ids that belong to no player | `participants` and `unclaimedRewards` of special orders | **off** |
| `FixQuestItemGroundDrops` | Harmony postfix | `Game1._RecursiveRemoveThisNormalItemLocation` | **off** |
| `FixOfflineFarmhandWallets` | `Saving` event, host only | The wallet of the saved copy of each farmhand who is not in the game | on |
| `FixBlankNames` | Harmony postfix | `CharacterCustomization.canLeaveMenu` | on |
| `FixTrashBearSardine` | Harmony postfix | `Utility.getRandomPureSeasonalItem` | on |
| `LogEatInterrupts` | Harmony prefix, log only | `Farmer.completelyStopAnimatingOrDoingAction` | on |

No transpilers. No per-tick work. The title screen and ground drop patches run only when those menus close or a quest ends, and the order check runs once a morning on the host. The only repeating work is the morning queue clear, about 2 ms across 80 locations. No data is written to saves or to disk beyond `config.json`.

## Failure behavior

- Each fix starts on its own. If one fails to start, the log shows one error and the others still run.
- The asset edits only change an entry when it still matches what was expected. If the game or another mod changed it, the edit does nothing and logs at trace level.
- If a Harmony target is missing after a game update, that fix logs one warning and stays off.

## Other mods

- Edits run late, so a Content Patcher pack that changes the same entry first is judged by what it left behind. A pack that rewrites the Fair third place line without a lone 200, or the Chef's reward to something other than `1 Pink Cake`, makes the fix skip itself.
- `FixLumberjackMahoganyBonus` changes the id in `Data/WildTrees` that other mods may match on (`(O)709`). That is why it is off by default.
- Another mod patching the same Harmony targets should work. Harmony runs the postfixes in turn. The Trash Bear postfix only changes a result that is a summer Sardine.
- Not tested with any other mod. The test profile held only SMAPI's ConsoleCommands, this mod, and a test tool.

## Multiplayer

Tested with a host and one joining player, each a separate game process on the same PC (see `REPRODUCTION.md`). Not tested: split screen, three or more players, a joiner on another PC or over the internet, a dedicated server. By design:

- Text and menu fixes are local to each player. Install on every player who should see the corrected text.
- `FixChefBundleReward` decides the reward when a host creates a new remixed world, so only the host needs it at that moment. Existing worlds keep the bundles they were created with.
- `FixTrashBearSardine` is deterministic for a given world id, so players with and without the mod could see different Trash Bear requests in summer in 1 case of about 16.
- `FixLumberjackMahoganyBonus` changes drops on the machine that fells the tree.
- `CheckStaleOrderParticipants` runs on the host only. `RepairStaleOrderParticipants` edits state that every player shares, was only tested with a made-up id and is off by default.
- `FixQuestItemGroundDrops` and `FixTitleMusicMute` run on each machine for what that machine sees.
- `TrimLocationChangeBacklog` runs on the main player only. It skips any location that has a network root, because the game empties that queue itself. The ones without a root (locations no joiner has been sent yet, and every location in single player) are cleared. A joiner who walks into one later is sent the whole location. After 20 nights the joiner saw the same object and feature counts as the host in seven visited locations, with and without the fix.
- `FixOfflineFarmhandWallets` runs on the main player only and only when the world uses separate wallets. It sets the saved copy's wallet to the shared value, it never adds, so a second run changes nothing.

## Known limits

- Farms already saved with a space-only farm name stay stuck. `FixBlankNames` only prevents new ones.
- Italian and Russian show the female "single" text to everyone on the social page. The game never reads the male string for those languages, and a data edit cannot fix that.
- Worlds already created keep the 1 Pink Cake Chef's bundle.
- `TrimLocationChangeBacklog` only covers locations. The queues on the player, world state and team were not measured as a problem and are left alone. It was measured over 12 nights on a starter farm and 40 nights on a synthetic late-game style farm, not on a real played save or a session of many hours.
- `RepairStaleOrderParticipants` removes only ids that belong to no player in the world, but a real deleted farmhand was never tested.
- `FixGeorgeWheelchair` does nothing if another mod changed either George sheet, by design.
- A host and a joiner sometimes freeze in the game's own new day save (seen twice in about 20 two-player runs, once with every ValleyFixes fix turned off). Not caused by this mod and not fixed by it.
- The eating freeze (VF-004) is not fixed. The logger only names the cause when it happens.
