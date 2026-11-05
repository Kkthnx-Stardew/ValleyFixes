# ValleyFixes

Verified fixes for Stardew Valley 1.6.15 bugs, plus performance and multiplayer fixes that matter most in big mod lists. Every fix is a separate switch, every claim was measured in the running game, and a fix steps aside by itself if the game data or another mod no longer matches what it expects.

**Beta.** This is `0.9.0-beta.2`. 1.0 comes after beta testers report no problems.

## What you get

Measured on a copy of a real played save with 101 mods (Stardew Valley Expanded, Ridgeside Village, East Scarp and others), single player, every ValleyFixes setting off against on.

| | Off | On |
|---|---|---|
| Update plus render time per frame, mean over 13 places | 4.88 ms | 2.83 ms |
| Garbage made per second | 178 MB/s | 33 MB/s |
| Managed memory after loading the save | 1,832 MB | 1,522 MB |
| Process memory | 6,236 MB | 5,874 MB |

Each of these was also reproduced in the game before and after the fix.

- The host crashed when the day ended if a farmhand's part of a Mini-Shipping Bin had an empty slot (separate wallets).
- A cooked dish delivery quest crashed the game in about a third of cases, and the wedding showed Lewis's speech as a broken fragment.
- A farmhand could empty the same Mines coal cart again and again, and gold sent to an offline farmhand was lost at the next save and reload.
- A lost join packet meant 45 seconds of waiting and a timeout. In a test with packet loss, 14 of 100 joins failed before and 1 of 100 after.
- Stardrop energy lost when eating is cut short, a wrong Trash Bear request, a blank farm name that blocks joining, a notch in menu shadows, and more, listed below.

The numbers come from one PC and one save. They show where the cost is, not what every PC will see. Details and the other measurements are in [docs/AUDIT.md](docs/AUDIT.md).

## Install

1. Install [SMAPI](https://smapi.io) 4.2.1 or newer. Stardew Valley 1.6.15 or newer is required.
2. Copy the `ValleyFixes` folder from the zip into your `Mods` folder.
3. Start the game once. A `config.json` appears in the mod folder.

Changes to settings apply the next time the game starts. If you have [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) installed, every setting is on the ValleyFixes page there, with a short description each. Without it, edit `config.json`.

For multiplayer, install it on every player. Settings marked for one side only say so below.

## Settings

Defaults are on unless marked **off**. Names are the keys in `config.json`.

### Multiplayer and connection

| Setting | What it does |
|---|---|
| `RetryLostJoinRequests` | Joining by IP address or on a local network starts with one UDP packet that the game never repeats. This repeats it up to three times and keeps a typed port. Joining player only. Skipped if another mod patches the same game methods. |
| `SyncCoalCarts` | The Mines coal cart list is shared through the host and saved with the world, so a cart one player emptied stays empty for everyone. Needed on every player. |
| `FixOfflineFarmhandWallets` | With separate wallets, gold sent to a farmhand who is not in the game was lost at the next save and reload. Host only. |
| `FixMiniShippingBinCrash` | With separate wallets, an empty slot in a farmhand's part of a Mini-Shipping Bin crashed the host at the end of the day. Host only. The game's developers have said 1.6.16 fixes it. |
| `FixStaleMapOverrides` | A farmhand kept seeing the SquidFest booth and sign after the festival. Joining player only. |
| `MultiplayerInterpolationTicks` | **15, the game's own value.** How long this machine holds back what other players and monsters do. At 15 they look about 250 ms late. 8 shows them 141 ms late and 4 shows them 72 ms late. On a link with jitter, 4 froze up to 3.7% of frames and 15 up to 1.8%. Allowed 2 to 15. Only you are affected. |
| `CheckStaleOrderParticipants` | The host writes a log line when a special order is held open by a farmhand who no longer exists, and when the town board comes up empty with held orders. Changes nothing. |
| `RepairStaleOrderParticipants` | **Off.** Also removes those deleted farmhands from the order. Only tested with a made-up id. |

### Performance

| Setting | What it does |
|---|---|
| `SkipDefaultTileProperties` | SMAPI stores a rotation and a flip of 0 on every tile of every Tiled (TMX) mod map. Not storing them saved 311 MB of managed memory on the test save. Every tile reads back the same. |
| `CacheAssetNames` | Some mods ask SMAPI to parse an asset name after every sprite drawn. The answers are kept, which removes most of the garbage in big mod lists. |
| `SkipEmptyLocationUpdates` | A location nobody has been in for 60 frames that holds nothing is not updated every frame. Host or single player. |
| `PauseIdleLocationMonsters` | Monsters in outdoor locations nobody is in stand still until a player arrives. Never on the farm, in buildings, mines or the volcano. Host or single player. |
| `TrimLocationChangeBacklog` | The game queues every removed object for a network send that never happens. This clears the queues each morning so memory stops growing. |

### Bug fixes

| Setting | What it does |
|---|---|
| `FixCookingDeliveryQuestCrash` | An item delivery quest asking for a cooked dish, for any NPC but the Wizard, crashed the game about a third of the time, on days fixed by your save. The game's developers list it as fixed in 1.6.16. |
| `FixWeddingSpouseTokens` | On the day of the wedding the script could not find your spouse while you are engaged, so Lewis's matrimonial speech came out as the fragment "[SpouseGenderedText" and the game logged 61 errors. The spouse is now found. The game's developers list it as fixed in 1.6.16. |
| `FixPierreDuplicateSeeds` | After you give Pierre his missing stock list, wheat, corn and sunflower seeds were listed twice, one of them out of season at 1.5 times the price. The dearer copy is dropped. |
| `FixStardropInterrupt` | A stardrop whose eating is cut short gave nothing and was already used up. The energy bonus is now applied at once. |
| `FixTrashBearSardine` | Trash Bear could ask for a Sardine in summer, when it cannot be caught. |
| `FixChefBundleReward` | The remixed Chef's bundle gave 1 Pink Cake, the classic one gives 3. New remixed worlds only. |
| `FixBlankNames` | The new farm and farmhand menus accepted a name of only spaces, which can leave a farm nobody can join. Prevention only. |
| `FixTitleMusicMute` | The music came back after the About screen while the mute button still showed muted. |
| `FixGeorgeWheelchair` | George's wheels did not turn when he walks up. The missing frames are copied from his winter sheet when his sprites are the original ones. |
| `FixMenuBoxShadow` | Menu boxes such as item tooltips lacked one corner piece of their drop shadow, which left a notch. Skipped if another mod patches the same method, such as Fix Hover Shadows. |

### Text fixes

| Setting | What it does |
|---|---|
| `FixFairThirdPlaceText` | Lewis says 200 star tokens for third place and the game gives 250. Every language. |
| `FixMovieReactionScripts` | Three movie reactions showed an NPC name in place of the reaction text. |
| `FixPortugueseSingleStatus` | Single male farmers showed the female "single" text on the social page in Portuguese. |

### Off by default, these change gameplay

| Setting | What it does |
|---|---|
| `FixLumberjackMahoganyBonus` | The extra hardwood the Lumberjack profession should give from mahogany trees never applied. This makes it apply, which raises their mahogany hardwood by about a quarter. |
| `FixQuestItemGroundDrops` | Quest items such as Qi Beans lying on the ground survived the overnight cleanup. This removes them like the ones in bags and chests. |
| `FixFruitTreeQuality` | Fruit that grew before its tree aged to the next quality kept the old quality. This raises it to the tree's current quality, as it did in 1.5. The game's developers called the old behaviour unintended. |

### Diagnostics

| Setting | What it does |
|---|---|
| `LogEatInterrupts` | Writes a log line naming the code that cuts the eating animation short. Changes nothing in the game. |

## Other mods

- A fix that patches a game or SMAPI method steps aside if another mod already patches it, and says so in the SMAPI log. No setting needs changing.
- The barn animal double shadow that Barn Animals' Double Shadows Fix repairs does not happen in 1.6.15. That mod can be removed.
- Tested with a copy of a real 101 mod list on one save, single player and with one joining player. Other mod combinations are untested. See [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

## What is not fixed

- The eating freeze. The cause is known for one trigger and the usual one is not. The logger names the caller when it happens.
- Farms already saved with a space-only farm name stay stuck, and worlds already created keep the 1 Pink Cake Chef's bundle.
- Italian and Russian show the female "single" text to everyone on the social page. A data edit cannot fix that.

## Tested and not tested

Tested on Stardew Valley 1.6.15 build 24356, SMAPI 4.5.2, Windows 11, English and Portuguese, single player, a host with one joining player on one PC, two joining players on a clean profile, and with delay, jitter and packet loss added by a relay. Not tested were split screen, four players, another PC or the internet, Steam invites, dedicated servers, Mac and Linux. [docs/AUDIT.md](docs/AUDIT.md) lists every report, what was run, and what was not.

## Reporting a problem

Open an issue on GitHub. Include your SMAPI log (upload it at [smapi.io/log](https://smapi.io/log)), the game and SMAPI versions, and which ValleyFixes settings you changed. Turning one setting off at a time shows which fix is involved.

## Documentation

- [docs/AUDIT.md](docs/AUDIT.md), every report, its verdict, and the evidence
- [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md), how each fix hooks in, other mods, multiplayer, limits
- [docs/REPRODUCTION.md](docs/REPRODUCTION.md), how it was tested and what was not
- [docs/CHANGELOG.md](docs/CHANGELOG.md)

## Building and testing

```bash
dotnet build ValleyFixes.slnx -c Release
dotnet test ValleyFixes.Tests/ValleyFixes.Tests.csproj
```

The build does not copy into the game's Mods folder. The release zip lands in `ValleyFixes/bin/Release/net6.0/`. The tests read the unpacked game content next to the install (set `STARDEW_GAME_PATH` to point somewhere else) and skip that part if it is missing.

To run the game with only this mod and a test tool, in a Mods folder of its own, use these.

```bash
pwsh tools/Run-CleanProfile.ps1
pwsh tools/Run-Harness.ps1 -Label defaults -Configuration Release
```

`tools/Run-Harness.ps1` starts the game, makes a throwaway farm, runs the scenarios in `ValleyFixes.DevHarness` (a test tool that is never shipped) and writes results under `testprofile/results`. `tools/Run-Multiplayer.ps1` does the same with a host and a joining player. Your live Mods folder is not touched. Saves cannot be redirected, so the runners delete their own `VFTest_*` saves afterwards. The game starts without Steam, which only turns off achievements. See [docs/REPRODUCTION.md](docs/REPRODUCTION.md) for every scenario.
