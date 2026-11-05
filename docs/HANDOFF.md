# Handoff for the next session

Updated 2026-11-05 at the first beta, `0.9.0-beta.1`. Read `README.md`, `docs/CHANGELOG.md` and `docs/AUDIT.md` (the last round, Round eight, first) before anything else.

## State

- Mod: ValleyFixes 0.9.0-beta.1 for Stardew Valley 1.6.15 build 24356, SMAPI 4.5.2, repo `C:\Users\Administrator\Documents\GitHub\ValleyFixes`, GitHub `Kkthnx-Stardew/ValleyFixes`. 1.0 is reserved for the release after beta testing.
- 27 settings, listed with defaults in `ValleyFixes/ModConfig.cs` and the README. Three are off by default and change gameplay (`FixLumberjackMahoganyBonus`, `FixQuestItemGroundDrops`, `FixFruitTreeQuality`), one repairs shared state (`RepairStaleOrderParticipants`), and `MultiplayerInterpolationTicks` is 15 (no change).
- Game: `D:\SteamLibrary\steamapps\common\Stardew Valley`. The live Mods folder has about 100 mods. Never touch it. Test through `testprofile/Mods` (clean) and `testprofile/ModsReal` (a copy of the live Mods folder, made with robocopy, ignored by git).
- Unit tests: 81 pass. Full harness runs on a clean profile with `defaults` and `fixed`, the real save copy with settings off and on, and a host with one joining player were clean on the Release build.
- The writing rules of the user: no em dashes, no semicolons in prose, no emojis, short sentences, plain words. No attribution lines in commits.

## Ideas worth doing next

- Test `FixMiniShippingBinCrash` with a real second player putting items in a bin and moving them. The test used a made-up second player id on the host alone, which is the same code path but not the same session.
- Test `FixFruitTreeQuality` with an axe and with a second player.
- Stardropium claims not tested (see the end of Round eight): UDP buffer size, save stream, item query index, string cache, collision grid, culling. Measure each on the real save before building anything.
- Find the organic trigger of the eating freeze (VF-004). All 73 call sites of `completelyStopAnimatingOrDoingAction` were triaged in an earlier round and only the item hold-up orphans the freeze for 4 seconds.
- The two host freezes of the game's own new day save (seen twice in about 20 two-player runs). A stack dump of the stuck host would show the cause.
- Ask the author of the blank special order board report for a save or log. The host log now names held orders.
- VF-013 Egg Festival freeze with a blank name, VF-048 Desert Festival makeover, VF-054 horse through fences, Lead Bobber stacking (VF-002, a design call).
- Split screen, three and four players with the real mod list, other languages.
- Watch for Stardew Valley 1.6.16. The developers said it fixes the Mini-Shipping Bin crash and the fruit tree quality. When it is out, re-run the reproductions in `docs/AUDIT.md` Round eight and retire whatever it fixed.

## How to run things

```bash
dotnet build ValleyFixes.slnx -c Release
dotnet test ValleyFixes.Tests/ValleyFixes.Tests.csproj
pwsh tools/Run-Harness.ps1 -Label defaults -Configuration Release -TimeoutSeconds 600
pwsh tools/Run-Harness.ps1 -Label fixed -Configuration Release -TimeoutSeconds 600
pwsh tools/New-RealSaveCopy.ps1 -Source LazyRooster_402395808
pwsh tools/Run-Harness.ps1 -Label fixed -Only realsp -Profile -ModsName ModsReal -LoadSave VFReal_LazyRooster_402395808 -TimeoutSeconds 1500
pwsh tools/Run-Multiplayer.ps1 -Label fixed -Scenario join -Interp 8
pwsh tools/Summarize-NetTune.ps1 -Pattern 'nt-*'
```

`-Only` takes `shadows`, `configshot`, `specialorders`, `emptyboard`, `minibin`, `fruit`, `realmem`, `realclock`, `realsp`, `natural`, `skull`, `eatstates`, `qibeans`, `title`, `stale`. A full run takes 5 to 8 minutes, so start it in the background with a timeout of at least 600 seconds. Every run rebuilds from source, so do not edit code while a chain of runs is going. `-Enable A,B` turns on only the named settings, so one change can be measured by itself. Make a copy of the Mods folder with `robocopy <live Mods> testprofile\ModsReal /E /XD ValleyFixes ValleyFixes.DevHarness`.

## Gotchas learned

- The game starts without Steam. Saves cannot be redirected, so the runners delete their own `VFTest_*` and `VFReal_*` saves. Check `%APPDATA%\StardewValley\Saves` still has only your own saves afterwards.
- Every run bumps `timesPlayed` and replaces `SMAPI-latest.txt`. The runner warns if the saved game language changes.
- Touching or moving the game window, or a busy PC, can stall a run. Rerun.
- Only one game can run at a time on this PC for timing runs. Two real mod processes of 6 GB do not fit 15 GB of RAM.
- A harness scene that renders to an off-screen target needs the equal depth sort control (`VFH_DUMMY=1`) when comparing images, because adding any sprite can reorder sprites of the same depth.
- The wiki returns 403 to scripted fetches. Read it through the built-in browser (`get_page_text` or javascript). The forum works the same way.
- `Game1.getFarm().objects` is an overlay wrapper. The real network dictionary is `netObjects`.
- The decompile is not kept. `ilspycmd -p -o <dir> "<game>\Stardew Valley.dll"` takes under a minute. SMAPI's bundled TMX library is `smapi-internal\TMXTile.dll`.
- Python is not installed. In bash heredocs, apostrophes can break the tool. Use the Write tool for text files, or a PowerShell script file.
- Verdicts from code reading alone were wrong several times (VF-001, VF-022) and a rendered look found overlapping labels that code reading had not. Run it in the game.
