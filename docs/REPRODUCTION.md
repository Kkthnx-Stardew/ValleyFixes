# Reproduction and testing

## Setup

- Game: Stardew Valley 1.6.15 build 24356, SMAPI 4.5.2, Windows 11, English, single player.
- A clean Mods folder holds only SMAPI's ConsoleCommands, ValleyFixes, and `ValleyFixes.DevHarness` (a test tool that is never shipped).
- Nothing in your live Mods folder is read or changed.

## Commands

```bash
dotnet build ValleyFixes.slnx
dotnet test ValleyFixes.Tests/ValleyFixes.Tests.csproj
pwsh tools/Run-Harness.ps1 -Label baseline
pwsh tools/Run-Harness.ps1 -Label defaults
pwsh tools/Run-Harness.ps1 -Label fixed
```

## The three passes

| Label | `config.json` | Used for |
|---|---|---|
| `baseline` | every fix off | The unfixed result |
| `defaults` | file deleted, so the shipped defaults apply | What a new install does |
| `fixed` | every fix on, including the opt-in one | The fixed result |

Each pass starts the game, creates a throwaway farm called "VFTest" with no menu clicking, runs every scenario inside the real game, writes JSON, and quits. The runner deletes the `VFTest_*` saves afterwards and warns if the saved game language changed. Saves cannot be redirected, so they briefly sit in the normal Saves folder.

## What the harness checks

| Scenario | How |
|---|---|
| Fair speech | Loads the speech through the game's string loader, plus the first, second and fourth place speeches as controls |
| Movie reactions | Parses each script with `Event.ParseCommands`, splits it with the game's splitter, runs the real `message` command and reads the dialogue box. Penny and Kent are controls |
| Eating | Eats real food, samples the farmer's state each tick, then interrupts, blasts with `GameLocation.explode`, and warps |
| Hardwood | Places and fells 60 mahogany trees with and without the Lumberjack profession and counts hardwood |
| Trash Bear | Calls the game's own helper for 20,000 world ids per season |
| Bundles | Runs the game's remixed bundle generator for 120 worlds |
| Names | Pushes names through the game's save serializer, and asks the real new-farm, new-farmhand and dye menus whether OK is allowed |
| Portuguese | Switches the running game to Portuguese and back and reads the Fair speech and the single status |
| Crab | Records a Crab, Lobster and Clam catch and checks the record |
| Performance | 30 seconds idle on the Farm: allocation, collections, frame times |
| Stress | Update and render cost timed separately plus whole-process CPU, on an empty farm, a farm with about 1,570 objects, the Town and a deep mine level, then a day update and a save of the loaded farm |
| Memory soak | 24 warps, 60 menu opens, then 40 day updates, with memory read after a full collection. Heap dumps (`-HeapDumps`) before and after, compared with `dotnet-gcdump report` |
| Overnights | `-Only natural -Days 12`: 12 real overnight transitions on a default farm. Records the location change queue size and retained memory each morning, reads the nightly save back from disk and compares it with the live farm, then clears the queues and measures what comes back. Add `-Heavy` to fill the farm with about 1,560 crops, fences, kegs, sprinklers and trees first (used with `-Days 40`) |
| Skull Cavern | `-Only skull`: floors 121, 150, 200 and 300, 15 seconds each with the floor's own monsters, then 15 seconds after adding 40 monsters from the floor's generator |
| Quest items | `-Only qibeans`: Qi Beans in the inventory, a chest and four kinds of ground drops, then a real overnight with the Qi Bean quest items queued. Counts what is left and a control item |
| Title screen | `-Only title`: clicks the real mute button, opens the About screen and the load menu and closes them, reading the music volume, the button icon and the saved setting each time. Your `startup_preferences` file is saved first and put back afterwards |
| Special orders | `-Only stale`: two finished special orders, one with a made-up participant id and one with none, then the game's updates and a real overnight |
| George | Part of the full run: compares the four walking up frames of George's wheel area as the game loads the sheet |
| Network and crowd | `-Scenario netcond` (position and object latency, both ways), `discovery` (join attempts under loss), `deadclient` (`VFH_DEADMODE` kill, blackhole, killsleep), `crowd` (`-Clients 2` or `3`), with `-DelayMs`, `-JitterMs`, `-LossPct`, `-Shape` and `-JoinProbe` through `toolsNetShaper`. `toolsNew-RealSaveCopy.ps1` makes the real save copy |
| Two players | `tools/Run-Multiplayer.ps1`: one game process hosts a throwaway farm, a second one joins as a farmhand through the real farmhand menu. They wait for each other at named points through small files. Scenarios: `join`, `perf`, `soak` (nights with both online, add `-Visit` to have the joiner walk into locations afterwards, `-HeapDumps` for dumps), `roundtrip` (progress, nights, the joiner leaves, the file is read), `gold`, `goldreload` (a kept world is loaded in a fresh game and the farmhand joins), `makeover`. The runner stops a run that makes no progress for 4 minutes |
| Eating states | `-Only eatstates`: eats while an emote plays, after and during a monster hit, while sitting, during a clock update, with an item pick-up message and during a warp, and records which ones orphan the freeze |

## Stress and memory results are machine specific

Frame times, CPU and memory above depend on this PC (2560 by 1440, 60 fps). The comparisons inside one run (off versus on, queue size, retained memory) are what carries over.

## What was not tested

- A human playing: no controller, no keyboard play-through, no window focus changes.
- Split screen, three or more players, a joiner on another PC or over the internet, another operating system, any mod beyond the test tool.
- Languages other than English and Portuguese in the running game.
- A real Grange award, a full movie screening, a real axe on a tree, a real Trash Bear conversation, the real OK click in the menus.
- Real fights in Skull Cavern, a real played late-game save, sessions of many hours.

## Raw results

`docs/evidence/` holds the JSON and SMAPI log from the last `baseline`, `defaults` and `fixed` runs. The `defaults` file is from before the Portuguese and blank-name scenarios were added.
