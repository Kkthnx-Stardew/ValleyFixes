# Changelog

## 0.1.0

- VF-007: corrects the Fair third place speech from 200 to 250 star tokens.
- VF-001: removes the stray NPC name from three movie reaction scripts.
- VF-043: clears the unsent network change queues of every location once a morning in single player, so removed objects stop piling up in memory (`TrimLocationChangeBacklog`). About 47% less memory growth over 12 nights on a starter farm and 82% less over 40 nights on a late-game style farm.
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
- Tested in game before and after for VF-007, VF-001, VF-006, VF-012, VF-022, VF-030, VF-036, VF-043, VF-044, VF-045, VF-047 and (with a made-up id) VF-046. Results in `docs/evidence/`.
