# Changelog

## 0.9.0-beta.1

First beta. 1.0 is for the release after beta testing.

- VF-067: with separate wallets, an empty slot in a farmhand's part of a Mini-Shipping Bin made the host throw a NullReferenceException when the day ended, and the new day never started (`FixMiniShippingBinCrash`). The game's developers confirmed it on the forum and said 1.6.16 fixes it. Reproduced in the game, where the day stayed stuck after 3,001 ticks without the fix and started after 40 ticks with it.
- VF-068: opt-in (`FixFruitTreeQuality`, off by default). Fruit that grew on a tree before it aged to the next quality kept the old quality when picked. The game's developers called this an unintended change in 1.6. In the test the tree was at quality 1 and its fruit dropped at quality 0 without the fix and at quality 1 with it.
- VF-066: SMAPI writes a rotation and a flip property of 0 on every tile of every Tiled (TMX) mod map, and nothing needs them (`SkipDefaultTileProperties`). On a copy of a real save with 101 mods that was 2,771,099 property entries, 99% of them default. Not storing them cut the managed heap after load from 1,832 to 1,522 MB and the process private memory from 6,236 to 5,874 MB. A hash of the rotation, flip and other properties of all 2,017,674 tiles in 461 maps is identical with and without the fix, so nothing looks different.
- VF-065: opt-in setting `MultiplayerInterpolationTicks` (default 15, the game's own value). The game shows other players and monsters about 250 ms late on purpose. How late follows this number and nothing else, so 15 gave 258 ms, 8 gave 141 ms, 4 gave 72 ms. How often changes are sent made no difference to it. On a link with 15 ms jitter and 1% loss, 4 froze up to 3.7% of frames and 15 froze up to 1.8%. Only the machine that sets it is affected.
- VF-064: menu boxes no longer have a notch in their drop shadow under the top left corner (`FixMenuBoxShadow`). The game draws three of the four shadow corner pieces. 112 pixels change per box, all in the notch, and the result is identical to the Fix Hover Shadows mod. It steps aside if another mod patches the same method.
- The Barn Animals' Double Shadows Fix report is not present in 1.6.15 build 24356. The game data already hides the extra shadow for cows, goats, sheep, pigs and ostriches.
- Special orders: when the town board comes up empty on a refresh day (1, 8, 15, 22), the host now logs which orders are held by entries still in the team list (`CheckStaleOrderParticipants`). The game's own rules do not empty the board when every one-time order is done, because at least 5 repeatable town orders stay eligible in every season. An empty board needs held entries, and the log now names them.
- The Generic Mod Config Menu page is grouped by purpose, with plain names that fit next to their checkbox. Four labels used to run into the checkboxes.
- `manifest.json` now carries a GitHub update key so SMAPI can tell you about new releases.
- Cleanup: one shared check for other mods patching the same method, one Harmony instance always created, no stray static fields.
- Test tools: harness scenarios `shadows`, `configshot`, `nettune`, `realmem`, `realclock`, `specialorders`, `emptyboard`, `minibin`, `fruit`, and `tools\Summarize-NetTune.ps1`.

## 0.2.0-alpha.7

- VF-063: a lost join request is now sent again (`RetryLostJoinRequests`, farmhand side only). Joining by IP address or on a local network starts with one UDP packet, and the game never repeated it, so one lost packet meant 45 seconds of waiting and a timeout. With 25 join attempts at each loss rate, 14 of 100 failed before and 1 of 100 after (5, 10 and 20 percent loss each way). Nothing changes at 0 percent loss. The retry also keeps a port typed after the address.
- Test tools: a join probe for the host's join steps, `tools\NetShaper` (a UDP relay that adds delay, jitter and loss), and harness scenarios for network conditions, join attempts under loss, a vanished player and three or four players. See `docs/AUDIT.md`, round six.

## 0.2.0-alpha.6

- VF-058: in multiplayer, a coal cart a farmhand empties now stays empty for the host, for every other player, and for that farmhand after they leave the floor and return (`SyncCoalCarts`). The host keeps the list, shares it, and saves it with the world. Needs the mod on every player's machine. Tested with a host and a joining player: three visits gave coal once instead of three times.
- VF-059: a stardrop whose eating is cut short now still gives its energy bonus (`FixStardropInterrupt`). Tested by cutting the eat short in game: maximum energy rose by 0 before and by 34 after.
- VF-060: a farmhand no longer keeps seeing the SquidFest booth and sign the day after the festival (`FixStaleMapOverrides`, farmhand side only). Tested from a real winter 13 save with a joining player.
- VF-061: SMAPI asset names are cached (`CacheAssetNames`). On a real save with 101 mods, garbage fell from 97 to 36 MB/s and render time from 3.14 to 1.56 ms.
- VF-062: locations nobody is in are no longer updated every frame (`SkipEmptyLocationUpdates`), and neither are monsters in them (`PauseIdleLocationMonsters`). Update time fell from 5.05 to 3.79 ms on the same save.
- Optional Generic Mod Config Menu page with every setting.

## 0.1.0

- VF-007: corrects the Fair third place speech from 200 to 250 star tokens.
- VF-001: removes the stray NPC name from three movie reaction scripts.
- VF-043: clears the unsent network change queues of every location once a morning in single player, so removed objects stop piling up in memory (`TrimLocationChangeBacklog`). About 47% less memory growth over 12 nights on a starter farm and 82% less over 40 nights on a late-game style farm.
- VF-055: with separate wallets, gold sent to an offline farmhand is no longer lost at the next save and reload (`FixOfflineFarmhandWallets`, host only). Tested with a real second game joining a reloaded world.
- VF-043 now also trims the locations a multiplayer host has no network root for. Over 20 nights memory grew 8.3 MB instead of 14.8 MB, and the joining player sees the same world.
- VF-045: closing the About screen or the load menu on the title screen no longer turns the music back on while the mute button shows muted (`FixTitleMusicMute`).
- VF-047: George's wheelchair wheels now turn when he walks up. Missing frames are copied from his winter sheet when the sheets are unmodified (`FixGeorgeWheelchair`).
- VF-046: the host logs special orders that list players who are no longer in the world (`CheckStaleOrderParticipants`). Removing those ids is a separate switch, off by default (`RepairStaleOrderParticipants`).
- VF-044: opt-in (`FixQuestItemGroundDrops`, off by default) removes quest items that sit on the ground as id-made drops, which the overnight cleanup misses.
- VF-036: Portuguese social page shows the male "single" text to single male farmers (`FixPortugueseSingleStatus`).
- VF-030: the remixed Chef's bundle now gives 3 Pink Cake like the classic bundle (`FixChefBundleReward`, new worlds only).
- VF-022: Trash Bear no longer asks for a Sardine in summer (`FixTrashBearSardine`).
- VF-012: the new farm and new farmhand menus no longer accept a player or farm name made only of spaces (`FixBlankNames`). Prevention only.
- VF-006: opt-in (`FixLumberjackMahoganyBonus`, off by default) makes the Lumberjack hardwood bonus apply to mahogany trees.
- VF-004: logging only, names the code that cuts the eating animation short.
- Tested with a host and a joining player (two game processes) for VF-043, VF-055 and the VF-056 and VF-048 checks.
- Tested in game before and after for VF-007, VF-001, VF-006, VF-012, VF-022, VF-030, VF-036, VF-043, VF-044, VF-045, VF-047 and (with a made-up id) VF-046. Results in `docs/evidence/`.
