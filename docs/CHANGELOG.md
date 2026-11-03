# Changelog

## 0.2.0-alpha.1

- VF-058: in multiplayer, a coal cart a farmhand empties now stays empty for the host, for every other player, and for that farmhand after they leave the floor and return (`SyncCoalCarts`). The host keeps the list, shares it, and saves it with the world. Needs the mod on every player's machine. Tested with a host and a joining player: three visits gave coal once instead of three times.
- VF-059: a stardrop whose eating is cut short now still gives its energy bonus (`FixStardropInterrupt`). Tested by cutting the eat short in game: maximum energy rose by 0 before and by 34 after.
- VF-060: a farmhand no longer keeps seeing the SquidFest booth and sign the day after the festival (`FixStaleMapOverrides`, farmhand side only). Tested from a real winter 13 save with a joining player.
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
