# ValleyFixes audit

Audit date: 2026-11-01. Only VF-001, VF-004, VF-006, VF-007, VF-012, VF-022, VF-030, VF-036, VF-043, VF-044, VF-045, VF-046 (made-up id), VF-047, VF-048, VF-055 and VF-056 were run in the live game (see "Runtime test"). Every other "confirmed" is a code or data reading only, marked `CODE_CONFIRMED_TEST_PENDING`.

## Summary

57 reports were audited (VF-001 to VF-057) against the installed game 1.6.15 build 24356. Thirteen were fixed and tested in the running game and ship in 0.1.0. One more only logs.

| Result | Count | Items |
|---|---|---|
| FIXED_AND_TESTED, shipped | 13 | VF-001, VF-006 (opt-in), VF-007, VF-012, VF-022, VF-030, VF-036, VF-043, VF-044 (opt-in), VF-045, VF-046 (log on, repair opt-in), VF-047, VF-055 |
| CONFIRMED_PENDING_FIX | 2 | VF-004 (logging shipped, no behavior change), VF-003 (harmless, would need a transpiler) |
| CODE_CONFIRMED_TEST_PENDING | 3 | VF-002, VF-013, VF-014 (design or balance calls, or unreproduced) |
| INTENTIONAL_OR_BALANCE | 5 | VF-005, VF-015, VF-016, VF-034, VF-039 |
| UPSTREAM_FIX_PLANNED | 3 | VF-027, VF-028, VF-057 |
| OUT_OF_SCOPE | 5 | VF-029, VF-031, VF-032, VF-037, VF-041 |
| CANNOT_REPRODUCE | 3 | VF-038, VF-048, VF-056 |
| REPORTED_UNCONFIRMED | 23 | Mostly multiplayer, save, desert festival and crash reports with no usable evidence or a risk to saves. VF-048 to VF-056 were added in round three |

Shipped fixes and their settings are in `README.md`. How each one hooks in, and the limits, are in `COMPATIBILITY.md`. How it was tested is in `REPRODUCTION.md`.

Lessons from the audit: three forum claims were wrong or only half right (VF-001 shows a name, not a blank box. VF-003 and VF-022 looked intentional at first and the official patch notes showed otherwise for VF-022. The bomb in VF-004 was not the cause). Reading the code alone was not enough, and running the real game settled each one.

## Environment

| Item | Value |
|---|---|
| Game | Stardew Valley 1.6.15 build 24356 (Steam build id 16826371), `D:\SteamLibrary\steamapps\common\Stardew Valley` |
| SMAPI | 4.5.2 |
| OS | Windows 11 Pro 10.0.26200 |
| Stardew Valley.dll SHA-256 | 7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE |
| StardewValley.GameData.dll SHA-256 | 9C03497C2D2AC24C94E2F25B3C2FC39ECDE1BC97341E514C5F9FDCC1E759CB81 |
| Decompiler | ilspycmd 8.2.0.7535, output kept outside this repo |
| Content data | `Content (unpacked)` beside the game, dated 2025-05-24, read as vanilla XNB output |
| Beta branch | Not checked |

Caveat: the installed `Mods` folder holds roughly 100 mods. All runtime tests need a separate clean profile (SMAPI only). None have been run.

## Table, VF-001 to VF-057

"Decompiled" means I read the installed code or data for that item. "No" means only the forum report exists.

| ID | Decompiled | Verdict | Decision | One line |
|---|---|---|---|---|
| VF-001 | Yes | FIXED_AND_TESTED | SHIP | 3 reaction scripts pass a stray NPC name to `/message`. The box shows only the name. Fixed and verified in game |
| VF-002 | Yes | CODE_CONFIRMED_TEST_PENDING | DEFER | Bounce factor is `count * 0.1`, so two bobbers dampen less than one. Intended rule unknown |
| VF-003 | Yes | CONFIRMED_PENDING_FIX | REJECT | The 1.6.4 patch notes say Red Snapper was removed from the winter fishing quest pool, and Willy's pool follows that, but Demetrius's winter pool still lists it. Red Snapper is catchable in winter per `Data/Locations`, so the quest can be finished. A fix would need a transpiler on `FishingQuest.loadQuestInfo` for no player harm |
| VF-004 | Yes | CONFIRMED_PENDING_FIX | DIAGNOSTIC-ONLY | Freeze reproduced at runtime with a direct interrupt. Bomb and warp refuted as triggers. Organic trigger unknown |
| VF-005 | Yes | INTENTIONAL_OR_BALANCE | DEFER | Getter returns an order-scoped value while a Mines hard order is active. Needs the reporter saves |
| VF-006 | Yes | FIXED_AND_TESTED | SHIP (opt-in, off by default) | Lumberjack got no extra hardwood from mahogany because the code compares `"709"` to the data's `"(O)709"`. Fixed by a data edit behind `FixLumberjackMahoganyBonus` |
| VF-007 | Yes | FIXED_AND_TESTED | SHIP | Text said 200, code awards 250. Fixed and verified in game |
| VF-008 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected |
| VF-009 | No | REPORTED_UNCONFIRMED | DIAGNOSTIC-ONLY | Not inspected |
| VF-010 | No | REPORTED_UNCONFIRMED | DIAGNOSTIC-ONLY | Not inspected |
| VF-011 | No | REPORTED_UNCONFIRMED | DEFER | Needs the report's stack trace |
| VF-012 | Yes | FIXED_AND_TESTED | SHIP (prevention only) | A one-space farm name passes the menu, is saved, and reads back empty, which blocks the farmhand menu. The menu now refuses a blank name. Farms already affected are not repaired |
| VF-013 | Partly | CODE_CONFIRMED_TEST_PENDING | DEFER | The same menu check lets a one-space player name through and then trims it to empty, which is a likely cause. The Egg Festival freeze itself was not reproduced. `FixBlankNames` prevents new cases only |
| VF-014 | Yes | CODE_CONFIRMED_TEST_PENDING | DEFER | 25% monster roll on quarry floors is always discarded. Removing it changes balance |
| VF-015 | Yes | INTENTIONAL_OR_BALANCE | REJECT | `FishingRod.doDoneFishing` breaks out of the tackle loop at the shorts only when its wear roll succeeds, so with a Preserving enchantment the shorts protect later tackle about half the time. Niche, and shorts in a tackle slot look like an Easter egg |
| VF-016 | Yes | INTENTIONAL_OR_BALANCE | REJECT | `Crop.newDay` does not advance an unwatered crop whose data says `NeedsWatering`, and hops (302) do. The report is likely the day-before watering counting. Not run in game |
| VF-017 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected |
| VF-018 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected |
| VF-019 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected |
| VF-020 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected |
| VF-021 | No | REPORTED_UNCONFIRMED | DEFER | Not inspected, likely a tolerated exploit |
| VF-022 | Yes | FIXED_AND_TESTED | SHIP | Trash Bear could ask for a Sardine in summer. The 1.4 and 1.6.4 patch notes removed Sardine from the summer quest pools and both fishing-quest pools omit it, so this list was missed. Reported again in 2026. Fixed by rerolling a summer Sardine pick |
| VF-023 | No | REPORTED_UNCONFIRMED | DEFER | Likely INTENTIONAL_OR_BALANCE |
| VF-024 | No | REPORTED_UNCONFIRMED | DIAGNOSTIC-ONLY | Not inspected |
| VF-025 | No | REPORTED_UNCONFIRMED | DEFER | Sprite asset work, not engine |
| VF-026 | No | REPORTED_UNCONFIRMED | DEFER | Check 1.6.16 overlap first |
| VF-027 | Partly | UPSTREAM_FIX_PLANNED | REJECT | Code path agrees with the report: `Dialogue.cs` calls `Choose` on the item list from `[]` text, and an empty bracket gives an empty list. Developer reply (per the brief) says fixed in the next update. Not run in game |
| VF-028 | No | UPSTREAM_FIX_PLANNED | REJECT | Per the brief. I did not fetch the migration page |
| VF-029 | No | OUT_OF_SCOPE | REJECT | Console and mobile only |
| VF-030 | Yes | FIXED_AND_TESTED | SHIP (new remixed worlds only) | Remixed Chef's bundle gives 1 Pink Cake, the classic gives 3. The only one of 25 same-named remixed rewards that differs. Found from a forum report and a data audit |
| VF-031 | No | OUT_OF_SCOPE | REJECT | Forum memory leak report: the thread's own answer is Discord game detection, so it is not the game. Not reproduced here |
| VF-032 | Yes | OUT_OF_SCOPE | REJECT | Stutter and freeze reports. Measured on a clean profile, the game is steady. See "Performance sample". A community mod the threads mention already covers general hitching |
| VF-033 | No | REPORTED_UNCONFIRMED | DEFER | Entering `[` and `]` with blanks in Marnie's animal naming skips her dialogue. Same bracket parsing as VF-027, so a fix for that may cover it |
| VF-034 | No | INTENTIONAL_OR_BALANCE | REJECT | Level 120 mine switch missing: the replies say it needs the Danger in the Deep quest completed and a day to pass. Not a bug |
| VF-035 | No | REPORTED_UNCONFIRMED | DEFER | Spouse movie ticket follow-up dialogue refers to meeting at the theater after the movie. Dialogue ordering, single report, and 1.6.16 notes list dialogue key fixes |
| VF-036 | Yes | FIXED_AND_TESTED | SHIP (Portuguese only) | In Portuguese, single male farmers show the female "single" text on the social page. The string has no slash so the game's male/female split gives the female text to both. Fixed with a data edit for pt-BR |
| VF-037 | Yes | OUT_OF_SCOPE | REJECT | The Items Lost menu (shown after dying) builds its OK button from a string key that does not exist, but that menu never draws button hover text, so nobody sees it |
| VF-038 | Yes | CANNOT_REPRODUCE | REJECT | Crab pot Crab not counting in Collections. In the game, a crab pot catch of Crab, Lobster and Clam is recorded and the Collections tab reads the same record |
| VF-039 | Yes | INTENTIONAL_OR_BALANCE | REJECT | No message when re-reading a power book. The "learned a few things" text only exists in the consumable skill book branch. The re-read branch gives 100 XP with no message |
| VF-040 | No | REPORTED_UNCONFIRMED | DEFER | Starfruit in a keg sometimes explodes. No bomb or explosion in `Data/Machines`, one report, no log |
| VF-041 | No | OUT_OF_SCOPE | REJECT | `IsFloorableTile` does not do what its name says. It only matters to map makers and mods |
| VF-042 | No | REPORTED_UNCONFIRMED | DEFER | Crash using the incubator, seed maker or mushroom log on GOG. The attached error file is not readable from here |
| VF-043 | Yes | FIXED_AND_TESTED | SHIP (memory only) | Found while measuring memory, not from a report. Every object or terrain feature added to or removed from a location is queued for a network send. In single player that send never happens, and for a multiplayer host it only happens for locations with a network root, so removed objects stay in memory. Queues of locations without a root are cleared once each morning. 82% less memory growth over 40 nights on a late-game style farm, and 44% less over 20 nights as a multiplayer host |
| VF-044 | Yes | FIXED_AND_TESTED | SHIP (opt-in) | Quest cleanup (Qi Beans, Calico Eggs) leaves ground drops that were made from an item id, because the sweep only checks drops that carry an item object. Reproduced, fixed, off by default |
| VF-045 | Yes | FIXED_AND_TESTED | SHIP | Closing the About screen or the load menu on the title screen resets the options and turns the music back on while the mute button still shows muted. Reproduced and fixed |
| VF-046 | Yes | FIXED_AND_TESTED (simulated id) | SHIP (log on, repair opt-in) | A special order is removed only when its participant list is empty, so an id of a deleted farmhand keeps it forever. Found by reading, reproduced with a made-up id, the host logs it and can remove it |
| VF-047 | Yes | FIXED_AND_TESTED | SHIP | George's wheels do not turn when he walks up. Four identical frames in `Characters/George`, two different ones in the winter sheet. Frames copied from the winter sheet at load time |
| VF-048 | Yes | CANNOT_REPRODUCE | REJECT | Desert Festival makeover and dye. Dyed pants and a dyed shirt went into the inventory with their colors unchanged, for a single player and for a joiner as the host sees them |
| VF-049 | Partly | REPORTED_UNCONFIRMED | DEFER | Willy fishing challenge without Calico Eggs after leaving the festival early. Needs a live festival day |
| VF-050 | No | REPORTED_UNCONFIRMED | DEFER | Desert Festival bus entrance blocked by NPCs. Schedule and collision, not inspected |
| VF-051 | No | REPORTED_UNCONFIRMED | DEFER | Farm lighting at a map boundary. Not inspected |
| VF-052 | No | REPORTED_UNCONFIRMED | DEFER | Overnight host crash on Sundays. Needs the stack trace. Over 200 host nights with a joiner online across about 14 soak runs, Sundays included, gave no crash or null reference. The game's own freeze at the new day save (see the multiplayer section) is a different symptom |
| VF-053 | No | REPORTED_UNCONFIRMED | DEFER | Emily bulletin board introduction quest cannot finish while Robin is busy. NPC availability, not inspected |
| VF-054 | Partly | REPORTED_UNCONFIRMED | DEFER | Horse passes through fences. The gate code adds temporary passable tiles to the rider (`Fence.cs`), the likely path. No repro steps available |
| VF-055 | Yes | FIXED_AND_TESTED | SHIP | Separate wallets: gold sent to an offline farmhand, or paid for their shipping bin, is lost at the next save and reload. A saved farmhand has a private team, the save writes its stale wallet. Reproduced and fixed with a real second game |
| VF-056 | Yes | CANNOT_REPRODUCE | REJECT | Multiplayer masteries, powers and upgrades lost. A joiner earned professions, mail (powers), mastery experience, a skill level, recipes, a tool upgrade, a house upgrade and keys. All of it was in the host's save file after the joiner left. Only the report's own trigger is missing |
| VF-057 | No | UPSTREAM_FIX_PLANNED | REJECT | Fruit trees give mixed quality fruit after aging. Developer says 1.6.16 fixes it |

## Evidence for the inspected items

### VF-001, movie reactions

- `Data/MoviesReactions` has 34 NPC rows. Three `DuringMovie.Script` values pass an extra token to `/message`:
  - Sam, `summer_movie_1`: `/message Sam "[LocalizedText ...]"`
  - Sam, `dislike`: `/message Sam "[LocalizedText ...]"`
  - Demetrius, `spring_movie_1`: `/message Demetrius "[LocalizedText ...]"`
- All other scripts use `/message "[LocalizedText ...]"`.
- `Event.DefaultCommands.Message` reads only `args[1]`. With the stray token, `args[1]` is the NPC name, so the box should show the bare name. The report says blank. Runtime will settle which.
- Dispatch checked: `MovieTheaterScreeningEvent` (line 715) appends the parsed `DuringMovie.Script` into the screening event script, so the same `message` command parsing applies.
- Fix shape: `AssetRequested` on `Data/MoviesReactions`, exact-match replace on those three scripts, skip if the script differs. No locale impact because the text lives in `Strings/MovieReactions`.

### VF-002, Lead Bobber

- `BobberBar.cs:461` multiplies the bottom-bounce speed by `getStringCountInList(bobbers, "(O)692") * 0.1f`. One bobber gives 0.1, two give 0.2 (weaker).
- Only the bottom bounce is dampened. The top bounce at line 470 is not.
- The Barbed Hook code at lines 438 to 446 uses a diminishing step for extra copies (0.2 then 0.05), so the developer does stack some tackle deliberately. The Lead Bobber intent is not established. No formula is proposed.

### VF-003, winter Red Snapper

- `FishingQuest.loadQuestInfo`: the Demetrius branch winter pool includes `(O)150`. The Willy branch winter pool does not.
- `Data/Locations`, Beach: `(O)150` condition is `LOCATION_SEASON Here summer fall winter`. It is catchable in winter, so the request can be completed.
- The two pools disagree with each other. That is inconsistency, not an unsatisfiable quest.

### VF-004, eating freeze

- `eatObject` sets `freezePause = 20000` and `isEating = true`. `Update` forces `CanMove = false` every tick while `freezePause > 0`.
- The normal exit is the animation end in `FarmerSprite` (line 863): `if (owner.isEating) owner.doneEating()`, which calls `forceCanMove` and zeroes `freezePause`.
- `completelyStopAnimatingOrDoingAction` sets `isEating = false` and halts the sprite but does not touch `freezePause`. Any call to it mid-animation orphans a 20 second freeze.
- `CanBeDamaged` returns false while `isEating`, so bomb damage itself is skipped.

Trace of the explosion path (second pass):

- Detonation is `TemporaryAnimatedSprite` (bomb timer) calling `GameLocation.explode` on the master game only. `explode` fires `damagePlayersEvent`, which runs `takeDamage` for farmers inside the blast box. `takeDamage` never calls `completelyStopAnimatingOrDoingAction`, `Halt`, or touches `freezePause` or `isEating`. Nothing else in `explode` touches the farmer's animation state. Nothing in the bomb path orphans the eat state. The reporter's bomb link is unsupported by the code I read.
- Reporter path A (`animateOnce` returns early so `doneEating` never fires): the early return needs `PauseForSingleAnimation` already true for the same animation id, or `freezeUntilDialogueIsOver`. `eatObject` calls `forceCanMove` then `completelyStopAnimatingOrDoingAction` first, and both clear `PauseForSingleAnimation`. `NetEvent1Field.Fire` runs `Poll` at once, so `performEatAnimation` runs synchronously for the local farmer. `freezeUntilDialogueIsOver` is only set by `showFrameUntilDialogueOver`, which has no callers in the game. Path A is not reachable for the local farmer on vanilla code.
- Reporter path B (`completelyStopAnimatingOrDoingAction` mid-animation) holds. It clears `isEating`, so `FarmerSprite` skips `doneEating`, and `freezePause` stays at 20000 and keeps `CanMove` false each tick.
- Callers that might land inside the eat window (see the runtime test below, a plain warp is not one because `performWarpFarmer` only calls it when `UsingTool`): `Multiplayer` forced-event warp, `FarmerTeam.OnKickOutOfMinesEvent` (fired only on the Mines and Skull Cavern hard order start, `SpecialOrder.cs` lines 994 and 1000), `holdUpItemThenMessage` (overwrites `freezePause` to 4000, so it gives a 4 second stall, not 20), `Game1.eventFinished`, new-day start. `performTenMinuteClockUpdate` only fires it when `UsingTool`. The Esc path at `Game1.cs:5568` is a debug key chord.
- Still unknown: which caller fired in the reporter's three sightings. Code alone cannot say.

Fix shape if a repro confirms it: Harmony prefix and postfix on `Farmer.completelyStopAnimatingOrDoingAction`. Prefix records `isEating`. Postfix sets `freezePause = 0` only when the prefix saw `isEating == true` and `freezePause > 0` (the runtime test showed it reads 19952 shortly after the eat starts, so it is not exactly 20000). `eatObject` itself calls the method before it sets `isEating`, so normal eating is untouched. Not the reporter's 1000 ms timeout and no per-tick watchdog.

Diagnostic first: the same prefix can log a one-line stack trace whenever `isEating` is true on entry. That changes no behavior and would name the real caller on the next occurrence.

### VF-005, deepest floor display

- `SkillsPage.cs:575` draws `MineShaft.lowestLevelReached`.
- That getter returns `LowestMineLevelForOrder` whenever it is 0 or higher. The Mines hard special rule sets it to 0 on start and back to -1 at end of day (`SpecialOrder.cs`).
- The setter writes the order value for floors up to 120 and the real record otherwise.
- So the display is order-scoped while the order is active. The permanent value is not overwritten in this code. The reporter saves are needed to see anything else.

### VF-006, tree hardwood

- `Tree.tickUpdate` line 557 tests `chopItem.ItemId == "709"`.
- Mahogany (types 8 and 13) store `"ItemId": "(O)709"` in `Data/WildTrees`. The test is never true on vanilla data.
- Result: those drops go through `createMultipleItemDebris` and the Lumberjack 25% bonus at line 568 never applies. The profession text only promises "a chance to drop hardwood", so the bonus may be undocumented.
- Matching on the generated `item.ItemId` (unqualified `"709"`) would activate the bonus. That raises yield, so it is a balance change and should be opt-in.
- Unverified: whether the loaded `ItemId` is normalized at runtime. Check with a real chop.

### VF-007, fair third place

- `Event.cs` lines 12525 to 12554 award 1000, 500, 250, 750 (secret), 50.
- `Strings\StringsFromCSFiles:Event.cs.1729` says 200 in English, German, Spanish, French, Italian and Portuguese. The other locales were not read (console encoding error).
- Fix shape: `AssetRequested` on that strings asset, replace the number 200 with 250 inside that one entry only if present. Works in every locale without per-language text.

### VF-012, farm name

- `CharacterCustomization` trims the player name (line 1696) and favorite thing (line 1698). The farm name is set from `farmnameBox.Text` at line 2851 with no trim.
- A single space passes the length check. After save and reload the XML element comes back empty, which fits the reported softlock. Not yet reproduced.

### VF-014, quarry monster roll

- `MineShaft.cs` lines 2808 to 2814 roll a 25% chance to set `isMonsterArea` on a quarry floor.
- Lines 2816 to 2822 then clear `isMonsterArea` for any quarry floor. The roll is dead code.
- Whether the clear is intended is unknown. Removing it adds monsters and loot, so no change without a decision.

### VF-022, Trash Bear

- `TrashBear.cs:88` picks via `Utility.getRandomPureSeasonalItem`.
- The summer array includes `"131"` (Sardine). `Data/Locations` allows Sardine only in spring, fall, winter.
- Whether a given save ever rolls it depends on `uniqueIDForThisGame`. Deleting the entry would shift every pick for existing saves. A narrow fix would reroll only when summer yields 131. Reachability not tested.

## Implementation status (0.1.0)

Built: `ValleyFixes/ValleyFixes.csproj`, net6.0, ModBuildConfig 4.4.0, 0 warnings, 0 errors. The zip holds only `manifest.json` and `ValleyFixes.dll`. 69 unit tests pass.

## Release build 0.1.0

| Item | Value |
|---|---|
| Zip | `ValleyFixes/bin/Release/net6.0/ValleyFixes 0.1.0.zip` (manifest.json and ValleyFixes.dll only) |
| ValleyFixes.dll SHA-256 | 14d330781a5a7748037b9318493f637c8be0f58d80b69250a32c0da92e852986 |
| Zip SHA-256 | 48043a7c810120e571fdf92d43721da9ca719090d8a317121a8f1b0cf001777a (the zip stores file times, so every build gives a new zip hash for the same DLL. Compare the DLL hash) |
| Unit tests | 69 pass |
| Last in-game pass | `defaults` (the config a new install gets) using this exact DLL (checked by hash against the test profile copy), 2026-11-01 19:05, with George's wheel frames fixed, the Special order check logging and leaving the stale id alone, and the earlier results below unchanged. After it, a 12 night single player run (queues 0 every morning, 5.1 MB growth) and the offline farmhand gold test (3,950g in the file) were run with this DLL: Fair 250, movie text fixed, Trash Bear summer Sardine 0, remixed Chef's reward `O 221 3` in all 55 worlds with the bundle, blank names blocked, Portuguese `(solteiro)` and `(solteira)`, no mahogany bonus applied (opt-in off, 9.17 and 10.07 per tree, within the roll noise of the baseline 8.95 and 9.93), normal eating clean, no ValleyFixes errors |

## Runtime test, 2026-11-01

Setup: Windows 11, game 1.6.15.24356, SMAPI 4.5.2, English, single player, new throwaway farm "VFTest" in the Farmhouse. Mods loaded: SMAPI ConsoleCommands, ValleyFixes, and `ValleyFixes.DevHarness` (a test tool in `ValleyFixes.DevHarness/`, never part of the shipped zip). The harness starts the farm by itself, runs the scenarios inside the real game and writes JSON. Two passes: `baseline` (all ValleyFixes toggles off) and `fixed` (all on). Raw results and SMAPI logs are in `docs/evidence/`. Steam was not running, which only disables achievements.

The game loaded both passes with no ValleyFixes errors. The `fixed` log shows `Movie reaction fix changed 3 of 3 expected scripts.`

### VF-007, Fair third place text

| | Baseline | Fixed |
|---|---|---|
| Third place speech | "Your prize is 200 star tokens!" | "Your prize is 250 star tokens!" |
| First, second, fourth place speeches | unchanged | identical to baseline |

Not run: the Grange scoring itself. The reward value 250 comes from reading `Event.cs`, not from a live award. Languages other than English were checked against the installed data files only (all 12 change to a single 250), not in the running game.

### VF-001, movie reactions

Each reaction's script was parsed with the game's own `Event.ParseCommands`, split with the game's own argument splitter, and the real `Event.DefaultCommands.Message` was called. The dialogue box text was read back.

| Reaction | Baseline shows | Fixed shows |
|---|---|---|
| Sam, `summer_movie_1` | "Sam" | "Sam can't contain his laughter!" |
| Sam, `dislike` | "Sam" | "Sam is playing his handheld video game system." |
| Demetrius, `spring_movie_1` | "Demetrius" | "Demetrius is scratching his chin..." |
| Penny, `family` (control) | "(Penny is smiling)" | unchanged |
| Kent, `like` (control) | "Kent has a rare smile on his face." | unchanged |

Correction to the forum report: the box is not blank, it shows only the NPC name. Not run: playing a full movie screening.

### VF-004, eating freeze

Normal eating takes about 145 ticks (2.4 s), not the 640 ms assumed earlier. It ends cleanly: `isEating` false, `freezePause` 0, `CanMove` true.

| Scenario | Result (identical in both passes) |
|---|---|
| Normal eat | Ends cleanly after about 145 ticks |
| `completelyStopAnimatingOrDoingAction` called 3 ticks into the eat | `isEating` goes false at once, `freezePause` stays at 19952 and `CanMove` is forced false every tick. Control returns after 1246 ticks, about 21 s. This reproduces the report |
| Explosion (`GameLocation.explode`, radius 3, damage on) 3 ticks into the eat | No damage, no interruption, ends cleanly |
| Explosion first, then eat on the same tick | Took 8 damage, then ate and ended cleanly |
| Real warp (`Game1.warpFarmer`) 20 ticks into the eat | No freeze. Control returned |

Findings:

- The freeze mechanism is real and reproduced, but only with a direct call to `completelyStopAnimatingOrDoingAction`.
- The Mega Bomb link is refuted for single player vanilla. Both orders of blast and eat finish cleanly. The timed bomb sprite was not used, only the `explode` method it calls.
- A warp is not a trigger. `Game1.performWarpFarmer` calls the method only when `UsingTool` is true. The earlier caller list in this file was too loose.
- The real trigger in the reporter's three sightings is still unknown. Multiplayer, a dialogue box or a level up were not tested.
- The `fixed` log named the test harness as the caller, so the logger works: `Eating was cut short (freezePause 19952, time 600, in FarmHouse). Called from: <Scenarios>d__13.MoveNext <- ModEntry.OnUpdateTicked <- ...`. It logged once, only for the deliberate interrupt, and stayed silent for normal eating, both explosion cases and the warp.
- Caller triage (round three, from a fresh decompile): the method has 73 call sites. Almost all need input the farmer cannot give while `CanMove` is false (tool use, gifts, doors, shop and menu actions, mounting, fishing, melee), or they start an event or cutscene (the 20 second freeze runs out inside it), or are debug and test commands. Two paths are not input driven and can land inside the 2.4 second eat window: `Farmer.holdUpItemThenMessage` (an item is added to the inventory, runs `completelyStop...` and then sets `freezePause` to 4000) and `FarmerTeam.OnKickOutOfMinesEvent`. `FarmerSprite.currentAnimationTick` also calls it, but only in a branch that normal animations never reach.
- `eatObject` sets `freezePause = 20000` and `isEating = true` after it fires the animation. It also has an unguarded path: an object that is neither a drink nor edible (edibility -300) falls through to the same two assignments with no animation to end them. The game only offers food that passes the edibility check, so this is not reachable from the normal Eat prompt. Not tested.
- The food effect (stamina, health, buffs) is applied in `doneEating`, which only the animation end calls. A cut-short eat therefore also loses the food's effect, not just 20 seconds of control. That is a second reason a fix should not just ignore the interrupt.
- Correction to the fix idea earlier in this file: `freezePause` counts down from 20000, so a reset keyed on exactly 20000 would miss it (it read 19952 after 3 ticks). The condition should be "was eating on entry and `freezePause` is above 0". Not built.

Round three, eating while in different states (harness scenario `eatstates`, fix on, Release DLL). Each row eats real food and watches 3 to 25 seconds:

| State when the farmer eats | Result |
|---|---|
| Plain | Ends cleanly, `freezePause` 0 and `CanMove` true after about 145 ticks |
| An emote is playing | Ends cleanly |
| Hit by a monster just before | Ends cleanly (the setup threw before the hit landed, see below, so only the eat ran) |
| Hit by a monster while eating | Ends cleanly. The hit is ignored while eating, as `CanBeDamaged` says |
| Sitting on a chair | Ends cleanly |
| Ten minute clock update while eating | Ends cleanly |
| Item picked up while eating (`holdUpItemThenMessage`) | Interrupted. `isEating` false, `freezePause` 3952, control held by the item message. The logger named `Farmer.holdUpItemThenMessage` as the caller. This is a 4 second stall, not 20 |
| Warp started while eating | Ends cleanly, no freeze |

None of these reproduces the 20 second freeze. The only organic interrupt found is the item hold-up, and it gives 4 seconds. The first monster-hit row had a setup error (a null reference thrown before the eat, not investigated), so it proves nothing beyond the plain eat. The real trigger in the reporter's sightings is still unknown. A chair counts as sitting here only by the absence of an error, the pose was not read back.

### VF-006, mahogany hardwood and the Lumberjack profession

Method: 60 mature mahogany trees (type 8) were placed on the Farm and felled by setting them to fall, so the game's own `Tree.tickUpdate` made the drops. Hardwood on the ground and in the bag was counted. Each pass ran once without the profession and once with Foraging professions 12 and 14 (Lumberjack).

| Pass | Config | No Lumberjack | Lumberjack | Lumberjack / own control |
|---|---|---|---|---|
| baseline | all toggles off | 9.08 | 10.07 | 1.11 |
| defaults | shipped defaults (module off) | 8.93 | 10.15 | 1.14 |
| fixed | every toggle on | 8.53 | 12.73 | 1.49 |

(Hardwood per tree, 60 trees per cell. The code's stack range is 7 to 11, mean 9, so one cell has a standard error of about 0.2 to 0.3. An earlier vanilla run gave 9.07 and 9.62.)

Reading: if the Lumberjack bonus applied in vanilla the Lumberjack run would be about 1.49 times its control. It is about 1.1, which is what the Lumberjack loop alone gives. With the fix on it is 1.49. The report is correct and player-facing: Lumberjack players got less mahogany hardwood than the tree code intends.

The fix (`LumberjackHardwoodFix`, setting `FixLumberjackMahoganyBonus`, default off): an `AssetRequested` edit of `Data/WildTrees` that changes the `ItemId` of the two standing-tree hardwood entries (tree types 8 and 13) from `(O)709` to `709`. Stump entries are left alone because the comparison at `Tree.cs` line 557 is the only code that reads `ItemId`, and the other `ChopItems` readers use `QualifiedItemId` or do not compare. The log line `Lumberjack hardwood fix changed 2 of 2 expected tree entries.` appears only in the `fixed` pass. The shipped defaults changed no hardwood.

Effect on players: about 26 to 28% more mahogany hardwood for Lumberjack players when turned on. That is a balance change, which is why it is opt-in. Other mods that match on `(O)709` in `Data/WildTrees` could be affected. Not run: trees chopped with a real axe, multiplayer, mystic trees (type 13 shares the data shape and is edited, but was not felled).

### VF-022, Trash Bear Sardine

Method: the game's own `Utility.getRandomPureSeasonalItem` was called for stages 0 and 1 (later stages switch to a cooked dish in `TrashBear.updateItemWanted`) across 20,000 different world ids per season.

| Season | Sardine picks of 40000 | Share |
|---|---|---|
| Spring | 2666 | 6.7% |
| Summer | 2495 | 6.2% |
| Fall | 2375 | 5.9% |
| Winter | 2375 | 5.9% |

Data check across every fish in all four seasonal pools, against the actual catch rules in `Data/Locations`: Sardine in summer is the only mismatch. Its only catch rule (Beach) allows spring, fall and winter. But `Data/Fish` gives it `spring summer fall winter`, so the game data disagrees with itself and I cannot say which is intended.

Effect: about 1 in 16 summer requests could ask for a Sardine that cannot be caught until fall. Players can still hand one over from earlier stock and there is no deadline, so the harm is small.

Revised after more research (later the same day): the official history says "removed sardine from Summer quests" in 1.4 and again in 1.6.4, and both fishing quest pools (Demetrius and Willy) omit Sardine in summer. Trash Bear's list, added later, is the one place it was missed. A second forum report (Aug 2026, "Trash Bear still requests Sardine in Summer") reached the same code. So this is a missed removal, not a design choice. My earlier INTENTIONAL_OR_BALANCE verdict leaned too much on `Data/Fish` listing summer, which the catch rules and the patch notes both contradict.

The fix (`TrashBearSardineFix`, setting `FixTrashBearSardine`, default on): a Harmony postfix on `Utility.getRandomPureSeasonalItem`, whose only caller is `TrashBear.cs` line 88. If it returns Sardine in summer it asks again with the next seed (up to 8 times), so the result is stable for a given save.

Runtime result (same 20,000 worlds, stages 0 and 1, the game's own helper, fixed versus unfixed):

| | Unfixed | Fixed |
|---|---|---|
| Summer Sardine picks of 40000 | 2495 | 0 |
| Summer draws that changed | | 2495, exactly the Sardine ones |
| Summer draws that changed but were not Sardine | | 0 |
| Spring draws | | identical |
| Replacement items | | spread across the other 15 summer items (about 175 each) |

Not run: the live Trash Bear interaction. The bear recalculates its wanted item on each interaction, so a save that already shows a Sardine will simply switch to the new item.

### VF-012 and VF-013, whitespace names

Code reading (`CharacterCustomization`): the menu checks the typed text by length, so one space passes. On OK the player name and favorite thing are trimmed (a one-space player name becomes empty), but the farm name is taken from the text box as typed. A new farmhand cannot edit the farm name, it is copied from the host, and OK needs it non-empty.

Round trip through the game's own save serializer (`SaveSerializer`), in the running game:

| Farm name typed | Written | Read back | Farmhand menu OK |
|---|---|---|---|
| one space | `<farmName> </farmName>` | empty (length 0) | blocked |
| three spaces | `<farmName>   </farmName>` | empty (length 0) | blocked |
| empty | `<farmName />` | empty | blocked |
| `Test Farm` | `<farmName>Test Farm</farmName>` | `Test Farm` | allowed |
| ` Test Farm ` | spaces kept | spaces kept | allowed |

So the report is correct: a space-only farm name is saved but loads back empty, and the farmhand creation menu then cannot be finished. The reporter's `<farmName />` is what the next save writes after that load.

The real menu's OK check (`canLeaveMenu`), built in the live game and asked with different names:

| Case | New farm and new farmhand menus, fix off | Fix on |
|---|---|---|
| normal names | allowed | allowed |
| player name one space | allowed | blocked |
| farm name one space | allowed (new farm), allowed (farmhand) | blocked (new farm), allowed (farmhand) |
| favorite thing one space | allowed | allowed |
| player name empty | blocked | blocked |
| names with outer spaces | allowed | allowed |
| dye menu, any of the above | allowed | allowed |

The farmhand menu does not check the farm name because the host's value is not editable there, and blocking it would only worsen farms already affected.

The fix (`BlankNameFix`, setting `FixBlankNames`, default on): a Harmony postfix on `CharacterCustomization.canLeaveMenu` that can only turn a "yes" into a "no", and only for the new farm, host new farm and new farmhand menus. It changes no saves. It does not repair a farm whose name already reads back empty. For such a farm the workaround is to put a name back in the save file's `<farmName />` element by hand, with the save backed up.

Not run: clicking the OK button in the real UI with a controller or keyboard, a full farmhand join, and the Egg Festival (VF-013). VF-013's reported freeze is unproven. The likely link is that a one-space player name is trimmed to empty after the menu allowed it, so an NPC line that uses the name has nothing to show. The same check now blocks that name.

### VF-030, remixed Chef's bundle

Source: forum thread "Only getting one pink cake in remixed Chef bundle" (Sep 2026). The poster says the wiki and patch notes had it at 3.

Data audit: every remixed bundle in `Data/RandomBundles` whose name matches a classic bundle in `Data/Bundles` was compared on reward. Of 25 matches, 24 agree. The Chef's bundle differs: the classic gives `O 221 3` (3 Pink Cake) and the remixed gives `1 Pink Cake`. The developer's own 1.6 note (Specialty fish bundle: "reward 5 Dish O' The Sea, to make it consistent with the classic bundle") shows the same kind of correction was done for one remix and missed for this one. I did not find a patch note about Chef's specifically, so the poster's claim is not confirmed.

Runtime: the game's own `Game1.GenerateBundles(Remixed)` was run for 120 worlds in each pass and the Chef's bundle entry read from the world's bundle data.

| | Worlds with Chef's | Reward stored |
|---|---|---|
| Unfixed | 59 | `O 221 1` in all 59 |
| Fixed | 65 | `O 221 3` in all 65 |
| Classic bundles | | `O 221 3` |

The fix (`ChefBundleRewardFix`, setting `FixChefBundleReward`, default on): an `AssetRequested` edit of `Data/RandomBundles` that changes the one reward string. Item names in that data are internal English names, so it works in every language. Bundles are saved into each world when it is created, so existing worlds keep the old reward. A fix cannot change those without editing the save. Not run: completing the bundle in a real new game.

### VF-043, location change queues (memory)

How it was found: the memory soak showed retained memory growing with every day update even though nothing visible changed. The heap dump comparison (`-HeapDumps`, `dotnet-gcdump report`) listed `OutgoingChange<...>` entries for `Object`, `TerrainFeature` and `Debris` among the largest groups, which led to the outgoing change lists of the location net dictionaries (`outgoingChanges`).

Code: `NetDictionary` and `NetCollection` record every add and remove in `outgoingChanges` so that the multiplayer sender can ship them. The sender then calls `MarkClean` on the location's net fields, which empties the list. In single player nothing is connected, the location has no `Root`, and nothing ever calls `MarkClean`. So each object or terrain feature that is added to or removed from a location is queued for a send that never happens. A removed object stays reachable from that queue for the rest of the session.

Runtime, real overnight transitions (`Game1.NewDay` through the game's own save), Release DLL, fix off versus on:

| | Starter farm, 12 nights | | Late-game style farm, 40 nights | |
|---|---|---|---|---|
| | Fix off | Fix on | Fix off | Fix on |
| Farm object queue after the last night | 1,367 | 0 | 2,418 | 0 |
| Farm terrain queue after the last night | 803 | 0 | 2,641 | 0 |
| Retained memory, first morning | 111.8 MB | 111.0 MB | 126.4 MB | 125.8 MB |
| Retained memory, last morning | 121.6 MB | 116.2 MB | 171.1 MB | 134.0 MB |
| Growth | 9.8 MB | 5.2 MB | 44.7 MB | 8.2 MB (after the first trimmed morning) |
| Freed by clearing the queues by hand at the end | 5.4 MB | 0 | 36.9 MB | 0 |
| Nightly save read back from disk | matches the live farm | matches | matches | matches |

The late-game style farm is the harness `PopulateHeavyFarm` (about 1,560 crops, fences, kegs, sprinklers and trees) before the first night, then 40 untouched nights. It is a synthetic farm, not a real late-game save, and the nights have no player activity, so a played farm should queue more, not less. In the fix-on run the queues read 0 after nearly every morning. A few mornings show a small residue (40 objects and 10 features on one day) from changes made in the first seconds after the player wakes. They are cleared the next morning. The first row of the fix-on 40-night run still shows the 1,558 features the harness placed before any morning had run.

Cost of the clear: 80 locations in 0.4 to 2.8 ms.

The fix (`LocationBacklogTrim`, setting `TrimLocationChangeBacklog`, default on): on `DayStarted`, when `Context.IsMultiplayer` is false, call the game's own `MarkClean` on every net field of every location (including interiors and generated ones). It turns itself off for the session on any exception. It skips multiplayer and split screen entirely, where the game drains the queues itself. Player, world state and team queues were not trimmed because they held about 0.1 to 0.3 MB.

Not covered: multiplayer (the fix steps aside), a real played late-game save, sessions longer than 40 in-game days, and mods that read `outgoingChanges` (none known). The memory soak in the stress section calls `DayUpdate` directly, which does not raise `DayStarted`, so it does not show this fix. The overnight runs do.

### VF-044, quest items left on the ground

Code: the end of a quest queues item ids in `team.itemsToRemoveOvernight` (`RemoveSpecialRule` adds `890` and `889` for the Qi Bean order, the Desert Festival adds `CalicoEgg`). Overnight `Game1._PerformRemoveNormalItemFromWorldOvernight` sweeps every location, and for ground drops it runs `l.debris.RemoveWhere(d => d.item != null && ...)`. A drop made from an id (`Game1.createObjectDebris`, crates, trees, monsters) is created through `Debris.InitializeItem`, which for an object-type item stores only `itemId` and leaves `item` null. So the sweep skips it.

Runtime, a real overnight with `890` and `889` queued, Release DLL, fix off versus on (`-Only qibeans`):

| Where the beans were | Before | After, fix off | After, fix on |
|---|---|---|---|
| Inventory | 5 | 0 | 0 |
| Chest | 4 | 0 | 0 |
| Ground, made from an item object (`new Debris(item)`) | 1 drop | 0 | 0 |
| Ground, made from an id (`createObjectDebris`, `createMultipleObjectDebris`) | 4 drops | **4 (survive)** | 0 |
| Control, a Sardine drop that must stay | 1 | 1 | 1 |

The fix (`QuestItemDebrisFix`, setting `FixQuestItemGroundDrops`, **off by default**): a Harmony postfix on `Game1._RecursiveRemoveThisNormalItemLocation` that removes drops with no item object whose `itemId` matches the queued id. It is off because it takes away items a player could otherwise pick up and keep, which some players treat as a harmless quirk. Not run: a real Qi Bean quest, a real crate or monster drop (the test uses the same game calls they use), multiplayer.

### VF-045, title screen music

Code: `TitleMenu.applyPreferences` mutes with `Utility.toggleMuteMusic`, and the mute button sets `startupPreferences.startMuted`. Closing the About screen calls `TitleMenu.ReturnToMainTitleScreen`, which calls `Game1.ResetGameStateOnTitleScreen`, which does `options = new Options()` and so sets the music volume back to 0.75. Nothing re-applies the mute. The load menu does the same in `LoadGameMenu.saveFileScanComplete`. The exit-to-title path is fine because it calls `applyPreferences` after the reset.

Runtime on the real title screen (`-Only title`, the harness clicks the real mute button, and puts your `startup_preferences` back afterwards):

| Step | Volume, fix off | Icon | Volume, fix on | Icon |
|---|---|---|---|---|
| Mute clicked | 0 | muted | 0 | muted |
| About open | 0 | muted | 0 | muted |
| About closed | **0.75** | muted | 0 | muted |
| Second About trip | 0.75 | muted | 0 | muted |
| Load menu open | **0.75** | muted | 0 | muted |

So the report is right: the music comes back while the button says muted, and the next click on it does the opposite of the icon. The fix (`TitleMusicMuteFix`, setting `FixTitleMusicMute`, default on): a postfix on `ReturnToMainTitleScreen` and `saveFileScanComplete` that mutes again only when the title screen is showing and the button shows muted and the volume is not already 0. The About open step was done by assigning the sub menu the way the click does, because the real click is ignored while the buttons slide in. Not run: gamepad, other languages, the 1.6.16 behavior (the forum mentions a related fix there).

### VF-046, special order participants

Code: `SpecialOrder.participants` (saved as `participantsIDs`) holds the ids of players who still have to claim. Each player removes themselves in `RemoveFromParticipants` or `OnMoneyRewardClaimed`. The host's per-tick `Update` removes an order only when `participants.Length == 0` (`MarkForRemovalIfEmpty`). `FarmerTeam.cs` refuses to add an order whose key is already in `specialOrders`. A farmhand deleted before claiming leaves an id nobody can remove, so the order stays in the team list and the same order cannot come back. This matches the forum report with two attached saves (not opened here).

Runtime (`-Only stale`): two finished orders, one with a made-up participant id (the world has no such player) and a control with nobody. The game's own update and a real overnight ran.

| | Control order | Order with the stale id, fix off | Order with the stale id, fix on |
|---|---|---|---|
| Still listed after the game's update | removed | **listed** | listed |
| Still listed after the next morning | removed | **listed, 1 participant** | removed |

The module (`StaleOrderParticipantsCheck`): each morning the host compares every order's participant and unclaimed reward ids with `Game1.getAllFarmers()`. `CheckStaleOrderParticipants` (on) only logs a warning with the order key. `RepairStaleOrderParticipants` (**off**) removes only those ids and leaves the game to remove an emptied order. Four unit tests cover the id comparison. Not run: a real multiplayer farm with a deleted cabin, the Walnut Room board refusing the order, split screen. The id was made up, so this proves the logic and the game's removal, not that the report's saves hit exactly this path.

### VF-047, George's wheelchair

Data: `Characters/George.png` is 4 by 6 frames of 16 by 32. In the up row all four frames are pixel identical. `Characters/George_Winter.png` has the wheel in two positions in the same row (36 pixels differ between neighbouring frames). For the frames that should match, the wheel art in both sheets is the same (frames 1 and 3 of the up row are the only ones that differ). The down and side rows turn in both sheets.

| Up row, pixels of the wheel area that differ from frame 1 | Frame 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| Fix off (as the game loads it) | 0 | 0 | 0 | 0 |
| Fix on | 0 | 36 | 0 | 36 |

The fix (`GeorgeWheelchairFix`, setting `FixGeorgeWheelchair`, default on): at load, copy the wheel rows of frames 2 and 4 from the winter sheet, but only when the normal sheet is still frozen, frames 1 and 3 match the winter sheet exactly and the winter sheet moves. Anything else (a sprite replacement pack) is left alone. The mod ships no art. Six unit tests cover the decision. Not run: seeing George walk up in the town, the iOS report.

### Round three triage, 2026-11-01

Two more research briefs (about 25 reports, many repeats of items already above) were checked against this audit. Already covered: VF-001, VF-002, VF-003, VF-004, VF-005, VF-007, VF-012, VF-013, VF-014, VF-015, VF-016, VF-022, VF-027. New and fixed above: VF-044 to VF-047. New and not fixed: VF-048 to VF-057 in the table. VF-005 was read again: `MineShaft.lowestLevelReached` returns the order-scoped value while a hard Mines order runs and the order sets it to 0 when it ends, so the Skills page shows 0 until the order is removed at the end of the day. `deepestMineLevel` is never touched. That is the reported symptom, and it is transient.

### Multiplayer test setup and results, 2026-11-01

Setup: `tools/Run-Multiplayer.ps1` starts two real game processes on this PC. The first creates a throwaway farm in multiplayer mode, builds a cabin and a customized farmhand, and hosts. The second connects to localhost and joins through the real farmhand menu. Each side runs its half of a scenario and the two wait for each other at named points. Both ran the same ValleyFixes build and setting. Not covered: split screen, three or more players, another PC or internet play, a dedicated server.

#### VF-055, gold sent to an offline farmhand

Code: with separate wallets the host keeps each player's gold in `team.individualMoney`. `ManorHouse.sendMoney` and the overnight payment for a farmhand's shipping bin both add to that list. A farmhand who has not joined since the world was loaded is a saved copy with a private team of its own. The save writes the private copy's wallet (`Farmer._money` reads `teamRoot`), and on the next load the world reads that value back into the shared list. So the gold lives in the game until the next save and reload.

Runtime, host only with the farmhand offline (`-Only gold`): separate wallets on, 3,000g sent from the Manor ledger, 20 parsnips in the farmhand's shipping bin (700g), one night. Then a kept world was loaded in a fresh game and the farmhand joined (`goldreload`):

| | Fix off | Fix on |
|---|---|---|
| Shared wallet for the farmhand after the night | 3,950g | 3,950g |
| Farmhand's own copy after the night | 500g | 3,950g |
| Wallet written to the save file | **500g** | 3,950g |
| Gold the joining player has after a fresh load | **500g** | 3,950g |

So 3,450g vanished, including the shipping bin money. The fix (`OfflineFarmhandWalletFix`, setting `FixOfflineFarmhandWallets`, default on): before each save the host sets the saved copy's wallet to the shared value for every farmhand who does not share the host's team. It only sets, never adds, so running it twice changes nothing. Not run: three players, a farmhand who joined and left in the same session (they share the host team and were already right).

#### VF-043 in multiplayer, memory

In single player no location has a network root. A multiplayer host still has locations no joiner has been sent, and the game empties a location's queue only when it has a root. The host's queues for Forest, IslandWest, Town, BugLand, Railroad, Mountain, IslandNorth and others kept growing (`hasRoot` false in every one).

| Host with one joiner online | Fix off | Fix on |
|---|---|---|
| 12 nights, three runs: memory growth | 10.0 and 9.8 MB | 5.8, 6.4 and 6.2 MB |
| 12 nights: unsent entries, first to last night | 1,182 to 2,268 | 0 to 0 |
| 20 nights: memory growth | 14.8 MB | 8.3 MB |
| 20 nights: unsent entries | 1,178 to 2,989 | 0 |
| 28 nights before the change, for comparison | 20.8 MB, 1,223 to 3,823 | (single player with the fix: 9.2 MB) |

The joiner's own copy grew 5.8 MB over 28 nights and its queue stayed at 1,183 to 1,193. The fix (`TrimLocationChangeBacklog` now clears every location that has no network root, main player only): 76 of 81 locations cleared, 5 with a root left to the game. Integrity check after 20 nights: the joiner walked into Town, Forest, Mountain, Railroad, BusStop, Beach and IslandWest and counted what it saw. Objects, terrain features and large features matched the host in all seven, with the fix on and off (Town read 2 objects higher on the joiner in both runs, so that is not from the fix).

#### VF-056, character progression across sessions

A joiner earned two professions, a power-style mail flag, a mastery experience stat, a skill level and experience, a crafting and a cooking recipe, an Axe upgrade to level 2, a house upgrade, an inventory size, the Rusty and Skull keys, a seen event, a secret note, a fish record and an achievement. A night saved the world with the joiner online, the joiner left, the host slept alone, and the file was read. The farmer was serialized the way the game saves it and compared element by element, as sets.

The only differences were values that change on their own: play time, the swing counters of tools, `songsHeard` and `triggerActionsRun`, the disconnect position, and recipes and a pending level-up that the host's night granted for the skill level the test set. Every progression field was in the file. The upgrade level 2 on the Axe, the mastery experience, the professions and the mail all matched. Not run: a joiner who joins an older save, a host crash during a night, a farmhand deleted and recreated. The report's own trigger is unknown.

#### VF-048, makeover

Dyed pants `(P)3` (green) and a dyed shirt were worn, `DesertFestival.ReceiveMakeOver` was called, and the old pieces were found in the inventory with the same colors (`4278241280` and `4281467080` both before and after). The host, watching the joiner, saw the same two items with the same colors. Not run: the full event on a festival day, starter shorts from a very old save.

#### Multiplayer performance

Idle on the Farm for 30 seconds, then walking, host and joiner together, fix on. Single player on the same machine for comparison (empty farm, fix on):

| | Update mean / p99 (ms) | Render mean / p99 (ms) | Allocation |
|---|---|---|---|
| Single player, empty farm | 0.12 / 0.33 | 0.54 / 0.68 | 3.6 MB/s |
| Host, idle | 0.13 / 0.33 | 0.62 / 0.85 | 3.7 MB/s |
| Host, both walking | 0.13 / 0.34 | 0.65 / 0.87 | 3.8 MB/s |
| Joiner, idle | 0.10 / 0.21 | 0.56 / 0.74 | 3.7 MB/s |
| Joiner, both walking | 0.10 / 0.28 | 0.59 / 0.81 | 3.8 MB/s |

A second player adds under 0.1 ms per frame on the host. No stutter or allocation problem in these samples. The only multiplayer cost found was the memory leak above.

#### A freeze in the game's own new day save

Twice in about 20 two-player runs the host stopped in the new day save for good (log ends at "before save", no error, joiner carried on). One of the two was a run with every ValleyFixes fix turned off, at the same night (7), so it is not caused by this mod. It may come from the test (both players call the sleep routine at the same moment) or from the game. It is not fixed and not proven to be a game bug. The runner now stops a run that makes no progress for four minutes.

### Performance sample

One idle 30 second sample on the Farm, clean profile (SMAPI, this mod and the test tool), Windows 11, 60 fps:

| Measure | Value |
|---|---|
| Allocation | 3.5 MB per second |
| Collections in 30 s | 3 gen0, 3 gen1, 0 gen2 |
| Frame time | median 16.67 ms, 99th percentile 16.76 ms, worst 27.5 ms |
| Frames over 25 ms | 1 of 1800 |
| Frames over 50 ms | 0 |
| Process memory | working set 1055 MB, managed heap 152 MB |

Stress and soak, same clean profile, Release DLL, fix off versus all fixes on. Update and render are timed inside the game loop and are the comparable numbers. Whole-process CPU and idle frame time varied a lot between runs on this PC (the idle median frame was 16.67 ms in one run and 21 ms in another, with no change to the mod), so do not read the mod into them.

| Case | Update mean / p99 / max (ms), off | on | Render mean / p99 / max (ms), off | on |
|---|---|---|---|---|
| Empty farm | 0.12 / 0.24 / 4.6 | 0.12 / 0.33 / 4.4 | 0.58 / 0.81 / 0.9 | 0.54 / 0.68 / 1.0 |
| About 1,570 objects on the farm | 0.14 / 0.67 / 15.1 | 0.13 / 0.51 / 0.9 | 0.88 / 1.15 / 1.3 | 0.83 / 1.06 / 1.2 |
| Town | 0.11 / 0.19 / 2.7 | 0.11 / 0.19 / 2.5 | 0.53 / 0.75 / 3.6 | 0.49 / 0.60 / 3.6 |
| Mine level 130 | 0.05 / 0.08 / 0.3 | 0.10 / 0.17 / 2.1 | 0.20 / 0.29 / 0.3 | 0.25 / 0.31 / 0.4 |
| Day update of the loaded farm | 20.9 ms, 1.8 MB | 21.8 ms, 1.7 MB | | |
| Full save of the loaded farm | 62 ms, 34.1 MB | 58 ms, 34.0 MB | | |
| 24 warps, 60 menu opens, 40 day updates | retained +28.4 MB | +28.3 MB | | |

The growth in the last row is the objects the farm gains plus the unsent queues. It is the same with the fix on because that test calls `DayUpdate` directly and never raises `DayStarted`. Warps and menu opens added nothing (checkpoints 160.0, 160.0, 160.0, 160.0, 160.2 MB).

Skull Cavern, fix on, floors 121, 150, 200 and 300. Each floor was sampled for 15 seconds with the floor's own monsters (0 to 3), then again after adding 40 monsters from the floor's own generator on open ground near the player. The player had a huge health bar so the sample was not cut short.

| Floor | Monsters | Update mean / p99 / max (ms) | Render mean / p99 / max (ms) | Frames over 16.7 ms | Allocation |
|---|---|---|---|---|---|
| 121 | 40 (20 big slimes, 7 green slimes, 12 bugs, 7 serpents) | 0.22 / 1.36 / 20.8 | 0.48 / 0.61 / 1.2 | 1 of 900 | 4.9 MB/s |
| 150 | 40 (slimes, bugs, rock crabs, serpents) | 0.19 / 0.95 / 11.0 | 0.42 / 0.52 / 0.6 | 0 | 4.2 MB/s |
| 200 | 40 (32 bats, slimes, bugs, rock crab) | 0.14 / 0.51 / 2.2 | 0.28 / 0.36 / 0.4 | 0 | 4.0 MB/s |
| 300 | 40 (21 bats, slimes, rock crabs, bugs) | 0.18 / 0.53 / 1.4 | 0.37 / 0.46 / 0.7 | 0 | 4.4 MB/s |

Forty monsters at once cost under 0.25 ms of update on average. The only slow frames are single spikes of 11 to 21 ms (one per floor at most, and none after the first seconds). That does not show the stutter the forum threads describe (VF-032 stays OUT_OF_SCOPE). Not covered: real fights with the weapon swinging, ranged enemies, a spawn wave over a long play session, and monsters from the deepest real floors.

The idle sample above says: the game is steady and its garbage collection is light in this case. That does not match a vanilla cause for stutter. The forum threads point at the system (an overlay such as Discord game detection, vsync, multiple monitors, controller polling), and one thread says a community stabilizer mod fixed it. Not measured: a real late-game save, multiplayer, or sessions of many hours.

### VF-036, Portuguese single status

Source: forum thread "[UI/Localization] Single Male Farmers in SocialPage display female relationship status in gendered languages" (Jul 2026), which cites `SocialPage.drawFarmerSlot`.

Code: for the single status the social page reads only `SocialPage_Relationship_Single_Female`. When the language uses gendered translations it shows the part before a slash to men and the part after the last slash to women. `SocialPage_Relationship_Single_Male` is never read by the game.

Data: no language file puts a slash in the female string. The male string differs from the female one in Portuguese (`(solteiro)` and `(solteira)`), Italian (`(scapolo)` and `(scapola)`) and Russian. In `LocalizedContentManager.ShouldUseGenderedCharacterTranslations` only Portuguese (and mod languages that opt in) takes the gendered path. So in vanilla, only Portuguese men are mislabeled by the split. Italian and Russian show the female text to everyone, because their path never splits, and a data edit cannot fix that without code.

Runtime, switching the running game to Portuguese (and back):

| | Unfixed | Fixed |
|---|---|---|
| Gendered path active | yes | yes |
| `Single_Female` string | `(solteira)` | `(solteiro)/(solteira)` |
| Shown for a male farmer | `(solteira)` | `(solteiro)` |
| Shown for a female farmer | `(solteira)` | `(solteira)` |

The fix (`PortugueseSingleStatusFix`, setting `FixPortugueseSingleStatus`, default on): an `AssetRequested` edit on the pt-BR strings only, joining the male and female strings with a slash in the order the game reads them. It skips itself if either string already has a slash or the two are equal. Not run: opening the actual social page, and Italian or Russian, which this does not fix.

Second language check, same run: the VF-007 Fair speech in Portuguese changes from `200 fichas-estrela` to `250 fichas-estrela`, and the VF-001 movie script (not language dependent) is fixed. The saved game language was unchanged (`en`) after the run.

### Data and code audits (no findings)

Automated checks run on 2026-11-01 against the installed game, to find bugs nobody has reported yet:

| Check | Result |
|---|---|
| Qualified item ids written in game code (`(O)`, `(BC)`, `(F)` and so on) that do not exist in the data | 8 hits, all prefixes, old-save migrations or a debug command. No bug |
| Translation keys loaded by code that are missing from the English string files | 3074 keys checked. All but two are keys with a runtime suffix. The two real ones are `DiaryEvent.cs.6672` (cut content) and `ShippingMenu.cs.11382` (VF-037, invisible) |
| Qualified item ids anywhere in 123 vanilla data files that do not exist | 0 |
| Recipe, bundle, gift taste, fish, crop and machine item ids | 0 problems |
| Qualified versus unqualified item id comparisons in game code | Only `Tree.cs` line 557 (VF-006) |
| Fish seasons in `Data/Fish` versus catch rules in `Data/Locations` | Too noisy to use, because the `Data/Fish` season field is legacy. Only Sardine in summer (VF-022) is backed by patch notes |

### Status per module

| ID | Module | Verdict | Before and after | Normal-behavior check | Not covered |
|---|---|---|---|---|---|
| VF-007 | `FairTokenTextFix` | FIXED_AND_TESTED | 200 then 250 in the live game | Places 1, 2 and 4 identical. Toggle off restores 200 | Grange award in a live festival. Non-English in game. A text mod editing the same key |
| VF-001 | `MovieReactionFix` | FIXED_AND_TESTED | Bare name then the real text, for all 3 reactions | Penny and Kent controls identical. Toggle off restores the bare name | A full screening. A mod editing `Data/MoviesReactions` |
| VF-004 | `EatInterruptLogger` | CONFIRMED_PENDING_FIX (logging shipped, no behavior change) | Mechanism reproduced with a direct call | Silent during normal eat, blasts and warp | The organic trigger. Multiplayer. Any reset patch |
| VF-006 | `LumberjackHardwoodFix` (opt-in) | FIXED_AND_TESTED | 10.07 then 12.73 per tree with Lumberjack | No-profession control unchanged. Default config changed nothing | Real axe. Multiplayer. Type 13 felled |
| VF-022 | `TrashBearSardineFix` | FIXED_AND_TESTED | 2495 summer Sardine picks, then 0 | No other pick changed, spring identical | Live Trash Bear interaction |
| VF-030 | `ChefBundleRewardFix` | FIXED_AND_TESTED | Remixed Chef's reward `O 221 1` in 59 of 59 worlds, then `O 221 3` in 65 of 65 | Classic bundle unchanged, no other bundle touched | A real remixed new game. Existing worlds are not changed |
| VF-036 | `PortugueseSingleStatusFix` | FIXED_AND_TESTED | Male farmer shown `(solteira)`, then `(solteiro)` | Female farmer text unchanged. The fix skips itself if the strings change | Social page itself. Italian and Russian |
| VF-043 | `LocationBacklogTrim` | FIXED_AND_TESTED | Queues of 2,418 and 2,641 entries and 44.7 MB growth over 40 nights, then queues near 0 and 8.2 MB | Save read back from disk matches the live farm. No visible change. Skips itself in multiplayer | Multiplayer. A real played late-game save. Sessions over 40 days |
| VF-044 | `QuestItemDebrisFix` (opt-in) | FIXED_AND_TESTED | 4 id-made bean drops survived a real overnight, then 0 | Inventory, chest and item-object drops already cleared. A non-quest drop stays | Real quest. Multiplayer |
| VF-045 | `TitleMusicMuteFix` | FIXED_AND_TESTED | Volume 0.75 with the icon muted after About and the load menu, then 0 | Mute and unmute by the real button unchanged. Prefs file restored | Gamepad. Other languages |
| VF-046 | `StaleOrderParticipantsCheck` (log on, repair opt-in) | FIXED_AND_TESTED (made-up id) | Order kept forever, then removed the next morning | Control order removed by the game as before | A real deleted farmhand. Multiplayer |
| VF-047 | `GeorgeWheelchairFix` | FIXED_AND_TESTED | Wheel frames 0, 0, 0, 0 then 0, 36, 0, 36 | Skips when the sheets are not vanilla | George walking in town |
| VF-055 | `OfflineFarmhandWalletFix` | FIXED_AND_TESTED | Save file said 500g and the joiner had 500g after a fresh load, then 3,950g | Shared wallet and the host's own money unchanged. Only sets, never adds | Three players. A farmhand who left in the same session |
| VF-012 | `BlankNameFix` | FIXED_AND_TESTED | OK allowed for a space-only name, then blocked | Normal names, outer spaces, blank favorite thing and the dye menu unchanged | Real OK click. Farmhand join. Repairing farms already affected |

## Ordered shortlist

| # | Item | Worth fixing | Why |
|---|---|---|---|
| 1 | VF-007 fair text | Done | Data edit only, no gameplay change |
| 2 | VF-001 movie scripts | Done | Three exact-match data edits |
| 3 | VF-004 eating freeze | Logging only for now | Mechanism proven, bomb refuted, real trigger unknown. Wait for a logged caller before adding a reset |
| 4 | VF-022 Trash Bear | Done | A missed removal from the patch notes, reproduced and fixed, small harm |
| 5 | VF-012 blank names | Done | Reproduced through the real serializer and menu. Prevention only, existing broken farms are not repaired |
| 6 | VF-006 hardwood | Done, opt-in | Reproduced and fixed by a data edit. Off by default because it raises Lumberjack hardwood by about a quarter |
| 7 | VF-002 Lead Bobber | No for now | Math looks odd, correct stacking rule is a design call |
| 8 | VF-014 quarry monsters | No | Balance change |
| 9 | VF-003, VF-005 | No | Not bugs on the evidence read |
| 10 | VF-008 to VF-011 | No | Save, money and multiplayer risk. Diagnostics only if ever |

## Open proof

1. Done 2026-11-01: the 1.6.16 migration page (read in a browser, the wiki blocks scripted fetches) calls 1.6.16 a potential patch with no release date. Nothing on it mentions the Fair speech, movie reaction scripts, `Data/WildTrees` hardwood, the eating freeze, Lead Bobbers or bracket names. It is a mod-author guide, not a full changelog, so that is not proof the fixes will not ship. It does list an inventory add-item overhaul that covers the partial-collection class of bugs (VF-019, VF-024).
2. VF-004: find the organic trigger. Play normally with the logger on and read the SMAPI log after any freeze. Round three (all 73 call sites triaged, eating tried in seven states) found only the item hold-up, which gives 4 seconds. See the VF-004 runtime section.
4. Done 2026-11-01 for Portuguese (VF-007 and VF-036). Other languages were only checked against the data files.
5. Pattern scan done 2026-11-01: a search of the whole decompiled game for qualified versus unqualified item id comparisons found only `Tree.cs` line 557 (VF-006). The other hits (`Farmer.cs` line 2617, `TV.cs` line 387) are correct.
6. Community scan 2026-11-01: the first 8 pages of the non-modded bug forum were read and filtered to PC reports. New items found are VF-030 to VF-035. Most other recent threads are console or mobile only (out of scope).
7. Deeper forum scan 2026-11-01 (pages 9 to 30, PC reports from 2026): new items VF-036 to VF-042. Other recent PC threads are crashes with no logs, hardware or driver problems, rendering at unusual zoom levels, and the reports already in this audit.

## Round five (2026-11-03): the verified research brief

The brief is `Stardew bug and performance fix research, verified.md`. Its own rule is that every item needs a repro on build 24356 before code. These rows were run in the game, before and after. Evidence is in `docs/evidence/`.

| ID | Brief item | Verdict | Decision | One line |
|---|---|---|---|---|
| VF-058 | 28 | FIXED_AND_TESTED | SHIP | A farmhand took coal from the same Mines cart on every visit. Counter went to -2 and the host still saw a full cart. The host now keeps and shares the list of emptied carts. Needs the mod on every player |
| VF-059 | 4 | FIXED_AND_TESTED (mechanism) | SHIP | The stardrop's energy bonus is applied only when the eating animation ends, and the stardrop is already used up. Cutting the eat short gave +0 before and +34 after. The report's own trigger (tabbing out, then spamming input) was not reproduced |
| VF-060 | 29 | FIXED_AND_TESTED | SHIP | A farmhand keeps seeing the SquidFest booth and sign on winter 14. Reproduced from a real winter 13 save with a joining player. The game's own revert looks for the booth on tilesheet "16" but the override puts it on "zzzzz_Beach_SquidFest_16", and the farmhand's rebuilt Beach has an empty applied list. The fix looks on the right tilesheet. Farmhand side only |

How VF-060 was found, because it is not the first idea. A date set by hand on a new farm did not show the bug, because the farmhand never got the festival map. A real winter 13 save loaded by the host, with the joiner entering later, did. Probes then showed three facts on the farmhand overnight. The Beach object is replaced. The map object is the same shared one that already holds the festival tiles. The object's applied list is empty. A first fix that restored the list from the game's own map cache did not work, because the cache entry is already gone when the game applies the changes. The tile itself is the evidence that holds.

Not run, so no verdict: items 2 (rarecrow hats), 3 (child bed), 13, 17, 23, 27 and 53. Items 8, 19 and 21 wait for the announced 1.6.16 fixes.

### Performance research (measured, clean profile, this PC)

Frame budget at 60 fps is 16.7 ms. Update and render are timed inside the game loop. A profiler wraps the game's own update, NPC, animal, pathfinding and draw methods. The numbers include its small overhead.

| Case, 10 second samples | Update mean (ms) | Render mean (ms) | Allocation |
|---|---|---|---|
| Farm by day, no animals | 0.79 | 0.71 | 4.5 MB/s |
| 100 animals outside | 1.19 | 0.94 | 5.6 MB/s |
| 200 animals outside | 1.47 | 1.13 | 6.5 MB/s |
| Farm at night, no torches | 0.95 | 0.75 | 4.6 MB/s |
| 300 torches at night | 0.99 | 1.00 | 9.7 MB/s |
| Town at noon | 0.64 | 0.57 | 4.6 MB/s |

What it says:
- All vanilla NPC updates together cost about 0.05 ms a frame, around 1.2 microseconds each. An NPC throttler cannot save a visible amount here (brief items 55 and the gap it names).
- Animals cost about 3 microseconds each a frame. `PathFindController.findPath` never ran while animals wandered by day, so animal pathfinding is not shown to be a cost (brief item 56). Animals walking home at 5 pm were not measured.
- 300 torches add 0.2 ms of draw time and double the garbage, 4.6 to 9.7 MB/s. This is the only candidate with a measurable cost. Not built.
- The game's ten minute clock update takes 1 to 6 ms. Its first call took 26 ms, the only spike in the runs.

Multiplayer traffic, two real game processes, bytes the host writes:

| Phase | Host sends |
|---|---|
| Join and load (fresh farm) | about 57 KB, mostly 4 location messages and 1 server message |
| Both idle, 30 s | about 0.01 KB/s |
| Both walking, 15 s | 0.8 KB/s, about 10 messages a second of 80 bytes |
| Host places 1,559 objects at once | one 27 KB message |
| One night | about 20 KB |

Bandwidth is not a bottleneck here, so a bigger network buffer (brief item 71) answers no measured problem. Not measured: a join onto a farm that is already heavy, other PCs, a real played late-game save, an HDD. These numbers come from a fast machine and show where cost scales, not what a low end PC feels.

A harness fact worth keeping: a warp from outside straight onto a bed tile is refused by the game and leaves the player in the doorway. Then the game starts the new day without the screen fade and the day never begins. The sleep scenarios now enter first and step onto the bed.
