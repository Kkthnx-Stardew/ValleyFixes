# Handoff for the next session

Updated 2026-11-05 after round seven. The state lines below were written at 0.1.0, so read docs/CHANGELOG.md for what shipped since (now 0.2.0-alpha.8). Read `docs/AUDIT.md` (Summary, VF-004, VF-043, Performance sample) and `README.md` first.

## State

- Mod: ValleyFixes 0.1.0 for Stardew Valley 1.6.15 build 24356, SMAPI 4.5.2, repo `C:\Users\Administrator\Documents\GitHub\ValleyFixes`. Now a git repo with a GitHub remote (Kkthnx-Stardew/ValleyFixes). The user wants to be asked before any commit or push.
- Game: `D:\SteamLibrary\steamapps\common\Stardew Valley`. The live Mods folder has about 100 mods. Never touch it. Test only through `testprofile/Mods`.
- Settings in the mod: `FixFairThirdPlaceText`, `FixMovieReactionScripts`, `FixChefBundleReward`, `FixPortugueseSingleStatus`, `FixBlankNames`, `FixTrashBearSardine`, `TrimLocationChangeBacklog`, `FixTitleMusicMute`, `FixGeorgeWheelchair`, `CheckStaleOrderParticipants`, `FixOfflineFarmhandWallets` (all on), `FixLumberjackMahoganyBonus`, `FixQuestItemGroundDrops`, `RepairStaleOrderParticipants` (off, opt-in), `LogEatInterrupts` (log only).
- Thirteen fixes were tested in the running game: VF-001, 006, 007, 012, 022, 030, 036, 043, 044 (opt-in), 045, 046 (log on, repair opt-in, made-up id), 047 and 055 (offline farmhand gold, tested with a real second game). VF-004 (eating freeze) is only logged, its real trigger is still unknown.
- The Release DLL (SHA-256 `14d33078...`) passed a full `defaults` run. Evidence for all passes is in `docs/evidence/`. Unit tests: 69 pass.

## Done in session two

- VF-043 is in the audit (ledger row, runtime section, module table). Late-game style farm over 40 nights: retained growth 44.7 MB without the fix and 8.2 MB with it, queues cleared every morning, nightly save reads back equal.
- Full `fixed` and `baseline` passes are clean (no `_error` keys, no ValleyFixes errors). One earlier `fixed` run was slow and stalled (frame median 20.8 ms, eat freeze never cleared, later scenarios cascaded). It was rerun. Kept as `testprofile/results/stalled/fixed-stalled.json`.
- Skull Cavern floors 121, 150, 200 and 300 with 40 added monsters: no stutter, update under 0.25 ms mean.
- Eating freeze: all 73 call sites of `completelyStopAnimatingOrDoingAction` triaged, seven eating states tried. Only `holdUpItemThenMessage` orphans the freeze (4 seconds). Also found: a cut-short eat loses the food effect, because stamina, health and buffs are applied only in `doneEating`.
- Harness additions: `-Only skull`, `-Only eatstates`, `-Heavy`, `-OutName`.

## Done in round three

- Triaged two more research briefs against the audit. New fixes: VF-044 Qi Bean ground drops (opt-in), VF-045 title screen music, VF-046 stale special order participants, VF-047 George's wheels. New unconfirmed reports VF-048 to VF-056 and VF-057 (upstream) are in the ledger.
- New harness scenarios: `-Only qibeans`, `-Only title`, `-Only stale`. The runner now saves and restores `startup_preferences`.

## Done in round four (multiplayer)

- Built a two-process multiplayer harness: `tools/Run-Multiplayer.ps1` (host plus joiner on one PC, host-only mode for tests that need the farmhand offline). Scenarios in `ValleyFixes.DevHarness/ModEntry.Multiplayer.cs`. It stops a run with no progress for 4 minutes.
- VF-055 reproduced and fixed. VF-043 extended to a multiplayer host (locations without a network root). VF-056 and VF-048 could not be reproduced. VF-052 not reproduced in over 200 host nights. VF-049, 050, 051, 053, 054 untouched (need a festival day, schedules, rendering or the report's steps).
- Multiplayer performance measured: a second player costs the host under 0.1 ms a frame. The only cost found was the queue leak.
- Open: the game's own new day save froze twice in about 20 two-player runs (once with every fix off). Cause unknown. A stack dump of the stuck host (dotnet-dump) would show it.

## Done in round seven (2026-11-05)

- Checked Nexus 31087 (Fix Hover Shadows) and 25428 (Barn Animals Double Shadows Fix). VF-064 menu box shadow corner fixed and tested. The barn animal double shadow is not present in 1.6.15. New harness scenario `-Only shadows` (set `VFH_TAG` for the file names, `VFH_DUMMY=1` for the sort order control). Version 0.2.0-alpha.8, uncommitted.

## Ideas worth doing next

- VF-046 with a real deleted farmhand now needs only a scenario: build the farmhand, finish a special order, delete the cabin through the game's own call, sleep and read the log.
- VF-056: ask for the report's exact steps. The plain join, earn, leave, save flow keeps everything.
- Split screen and three players are untested.
- VF-048 (Desert Festival makeover): check what happens to the old clothes and dye by running `DesertFestival.ReceiveMakeOver` in the harness.
- VF-054 horse through fences: read how `Fence` gates add `TemporaryPassableTiles` to the rider and whether a horse's wider box slips through. Needs the report's steps.
- The Lead Bobber (VF-002) is still a design call. A bottom bounce postfix that never lets two bobbers bounce more than one would be small, but it changes balance.

- Eating freeze: decide whether to ship a reset. The mechanism is proven, the organic trigger is not. A postfix that clears `freezePause` when the prefix saw `isEating` would remove any stall. A fuller fix would also finish the eat (apply the food) when it is cut short. Neither is built. The audit policy was to wait for a logged caller.
- The `just_hit` row in `eatstates` threw a null reference in setup. Fix the test (probably the slime needs a location) and rerun.
- Measure VF-043 on a real played late-game save, if the user can give one. Play with the logger and VF-043 on for a long session and compare memory.
- VF-013 Egg Festival freeze with a blank player name, now that `FixBlankNames` blocks new ones.
- Multiplayer and split screen tests. None have been done, and VF-043 steps aside there.
- More languages in the running game (the harness can switch language, see the `Portuguese` scenario).

## How to run things

```bash
dotnet build ValleyFixes.slnx
dotnet test ValleyFixes.Tests/ValleyFixes.Tests.csproj
pwsh tools/Run-Harness.ps1 -Label baseline -Configuration Release -TimeoutSeconds 540
pwsh tools/Run-Harness.ps1 -Label fixed -Configuration Release -TimeoutSeconds 540
pwsh tools/Run-Harness.ps1 -Label fixed -Configuration Release -Only natural -Days 40 -Heavy -OutName heavy40-fixed
pwsh tools/Run-Harness.ps1 -Label fixed -Configuration Release -Only skull -OutName skull-fixed
pwsh tools/Run-Harness.ps1 -Label fixed -Configuration Release -Only eatstates -OutName eatstates-fixed
```

A full run takes 6 to 8 minutes, so start it with `run_in_background` and pass `-TimeoutSeconds 540` (the default of 240 is too short). Every run rebuilds the Release zip, so take the zip hash after the last run.

## Gotchas learned

- The game starts without Steam. Saves cannot be redirected: the runner deletes its own `VFTest_*` saves. Check `%APPDATA%\StardewValley\Saves` still has only the user's four saves (LazyRooster, TheBigDill, lamp, test).
- Every run bumps `timesPlayed` and replaces `SMAPI-latest.txt`. The runner warns if the saved game language changes.
- Touching or moving the game window, or a busy PC, can stall a run. Signs: idle frame median above 16.7 ms and `freezePause` falling slower than 16 ms a tick. Rerun. Idle frame time and whole-process CPU vary between runs for no reason tied to the mod, so compare update and render times instead.
- The soak test in the full run calls `DayUpdate` directly, so it never raises `DayStarted` and does not show VF-043. The `-Only natural` runs do.
- The wiki returns 403 to scripted fetches. Read it through the built-in browser (`get_page_text` or javascript).
- Python in bash heredocs with apostrophes or backslashes broke several times. Write script files with the Write tool, or use a quoted heredoc (`<<'EOF'`).
- Verdicts from code reading alone were wrong several times (VF-001, VF-022). Run it in the game.
- `Game1.getFarm().objects` is an overlay wrapper. The real network dictionary is `netObjects`.
- The decompile is not kept. `ilspycmd -p -o <dir> "<game>\Stardew Valley.dll"` takes under a minute.
- The user's writing rules: no em dashes, no semicolons, no emojis, short sentences, plain words.
