# ValleyFixes

Small, separately switchable fixes for verified Stardew Valley 1.6.15 bugs. Every fix has its own toggle in `config.json`, and each one skips itself when the game data no longer matches what it expects.

Status: 0.2.0-alpha.1 (the table lists every fix, 0.1.0 tested twelve of them). Twelve fixes were tested before and after in the running game (Stardew Valley 1.6.15 build 24356, SMAPI 4.5.2, Windows 11, English and Portuguese, single player, plus a host with one joining player on the same PC for the multiplayer fixes). Split screen, three or more players and other mods were not tested. See `docs/AUDIT.md` for what was and was not covered.

## Install

1. Install SMAPI 4.2.1 or newer.
2. Copy the `ValleyFixes` folder from the zip into your `Mods` folder.
3. Start the game once. A `config.json` appears in the mod folder with every setting listed below.

## Fixes

| Setting | Bug | What it does |
|---|---|---|
| `FixFairThirdPlaceText` | VF-007 | Lewis says 200 star tokens for third place in the Grange display but the game gives 250. The speech is corrected in every language. The reward is not touched. |
| `FixMovieReactionScripts` | VF-001 | Three movie reactions (Sam twice, Demetrius once) pass the NPC name to the message command, which shows the name in place of the reaction text. The stray name is removed. |
| `TrimLocationChangeBacklog` | VF-043 | The game queues every object added to or removed from a location for a network send. In single player that send never happens, and in multiplayer it only happens for locations another player has been sent, so removed objects pile up in memory. Once a morning this clears those queues for locations nobody is syncing. Memory only, nothing you can see changes. The host does it, joiners do not. |
| `FixPortugueseSingleStatus` | VF-036 | In Portuguese, single male farmers show the female "single" text on the social page. Fixed for that language only. |
| `FixChefBundleReward` | VF-030 | The remixed Chef's bundle gives 1 Pink Cake where the classic one gives 3. Fixed to 3. Bundles are saved when a world is created, so only new remixed worlds are affected. |
| `FixTrashBearSardine` | VF-022 | Trash Bear could ask for a Sardine in summer, when it cannot be caught. The request is changed to another summer item. |
| `FixTitleMusicMute` | VF-045 | Mute the music on the title screen, open the ? screen or the load menu, come back, and the music plays while the button still shows muted. The mute is applied again after those two screens. |
| `FixGeorgeWheelchair` | VF-047 | George's wheelchair wheels do not turn when he walks up. The two missing wheel frames are copied from his winter sprite sheet when you have not replaced his sprites. |
| `CheckStaleOrderParticipants` and `RepairStaleOrderParticipants` | VF-046 | A special order can never be removed if a farmhand who was deleted before claiming is still on its list. The host logs such orders each morning (on). Removing the stale ids is a second switch that is **off** and was only tested with a made-up id. |
| `FixQuestItemGroundDrops` | VF-044 | **Off by default.** When a quest ends, Qi Beans and Calico Eggs dropped on the ground as items from crates, trees or monsters are not removed overnight like the ones in bags and chests. Turning this on removes them too. |
| `FixOfflineFarmhandWallets` | VF-055 | With separate wallets, gold sent to a farmhand who is not in the game (or paid for their shipping bin overnight) is lost at the next save and reload. The host writes the shared wallet into the saved copy before each save. Host only. |
| `SyncCoalCarts` | VF-058 | In multiplayer a farmhand can empty the same Mines coal cart again and again, because the game keeps its list of emptied carts on each machine and never shares it. The host now keeps the list, shares it, and saves it with the world. Needs the mod on every player's machine. Does nothing in single player. |
| `FixBlankNames` | VF-012 | The new farm and new farmhand menus let you finish with a name made only of spaces. A space-only farm name reads back empty after saving and then blocks new farmhands from joining. The OK button is now blocked for such names. It does not repair farms already affected. |
| `FixLumberjackMahoganyBonus` | VF-006 | **Off by default.** The tree code gives Lumberjack players 25% extra hardwood from mahogany trees, but a mismatched id means it never applies. Turning this on makes it apply, which raises their mahogany hardwood by about a quarter. |
| `FixStardropInterrupt` | VF-059 | The stardrop's energy bonus is applied only when the eating animation ends, and the stardrop is already used up by then. If something cuts the eating short (a menu, a warp, a stray input), the bonus was lost for good. When that happens the bonus is now applied at once. Other food is not changed. |
| `LogEatInterrupts` | VF-004 | Writes a log line with the calling code when something cuts the eating animation short. This only logs. It changes nothing in the game. |

## What is not fixed

- The eating freeze (VF-004). The cause is not known yet. The logger writes the calling code to the SMAPI log when it happens.
- Farms already saved with a space-only farm name stay stuck. Only new ones are prevented.
- Worlds already created keep the 1 Pink Cake Chef's bundle.
- Italian and Russian show the female "single" text to everyone on the social page.

## More

- `docs/AUDIT.md`: every report, its verdict, and the evidence
- `docs/COMPATIBILITY.md`: how each fix hooks in, other mods, multiplayer, limits
- `docs/REPRODUCTION.md`: how it was tested and what was not
- `docs/CHANGELOG.md`

## Testing in game

```bash
pwsh tools/Run-CleanProfile.ps1
```

This builds the mod, copies it into `testprofile/Mods` beside SMAPI's own ConsoleCommands, and starts SMAPI with `--mods-path` pointing there. Your live Mods folder is not touched. Add `-NoLaunch` to build and copy only. `tools/Run-Harness.ps1 -Label baseline|defaults|fixed` (add `-Only natural -Days 12` for the overnight memory test, `-HeapDumps` for heap dumps) runs the automated in-game scenarios from `ValleyFixes.DevHarness` (a test tool that is never shipped) and saves results under `testprofile/results`. Saves cannot be redirected, so make a new farm for tests and do not load an existing one. The harness removes its own `VFTest_*` saves afterwards. The game starts without Steam, which only turns off achievements. The log is `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. Each launch moves your last SMAPI log to `SMAPI-previous.txt`.

## Building

```bash
dotnet build ValleyFixes/ValleyFixes.csproj
dotnet test ValleyFixes.Tests/ValleyFixes.Tests.csproj
```

The build does not copy into the game's Mods folder. The zip lands in `ValleyFixes/bin/Debug/net6.0/`. The tests read the unpacked game content next to the install (set `STARDEW_GAME_PATH` to point somewhere else) and skip that part if it is missing.
