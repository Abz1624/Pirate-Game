# Scenario Lab (V5 M8z, I8z.4)

Test one condition by setting it up directly, not by playing a voyage up to it. A scenario is a `ScenarioConfig` asset
(data); the same asset loads from a PlayMode test and from the F1 Tuning Console. Dev tools only (Editor and
development builds).

## Where things are

| What | Where |
|---|---|
| Scenario assets | `Scenarios/` next to this file (`crab_at_10m`, `elite_hammerhead_offscreen`, ...) |
| The asset type | `ScenarioConfig.cs` |
| Loading | `Scenario.cs` (`Scenario.Load`, `Scenario.Find`, JSON in and out) |
| Setup in the run scene | `ScenarioRunner.cs` |
| 1v1 relay without a server | `FakePvpRelay.cs` |
| Console | F1, section **Scenarios** (`Console/ScenariosSection.cs`) |
| Tests | `Assets/_Game/Tests/V4/PlayMode/V5ScenarioLabPlayModeTests.cs` |

**Scene:** scenarios load into the real run scene, `V4SaltwindRun`, so they test what ships. There is no separate
scene and `V4ModuleLab` is not used, because:

- the lab's flag turns off the stage content (no Beacon or points of interest), the scripted phases and Rum;
- the lab cannot run a 1v1 (it always plays a voyage);
- a copy of the run scene would drift from the real one;
- a new scene would have to be added to the build list in ProjectSettings.

What the lab's flag switched off is now a per-scenario setting: ambient spawning, scripted phases and invulnerability.

## Writing a scenario

Create one with **Create > Rogue Pirates > V5 Scenario** in `Scenarios/`, or copy an existing asset. The name of the
file is the scenario's name. Fields:

- **Run:** `seed`; `captain` (a Captain config asset, or empty for the saved choice); `playtest` (`Off` is the regular
  game, as tests run it, and `Shipped` uses the owner's playtest switches, as `[UseShippedPlaytestSettings]` does);
  `invulnerable`; `skipLevelUpCards` (level-ups apply with no cards and no pause).
- **Clock:** `clockSeconds` is a forward jump through `RunManager.JumpClock`, the same as the console's Jump. The bands,
  overtime, storm, curves and director follow from the clock as they do in the game. A jump restarts the kill band.
  - `scriptedPhases`:
    - `SkipPassed` (default) skips the focus and pulse phases already due, because a jump would fire them all at once.
    - `SkipAll` is a quiet sea: no focus, pulse, overtime or storm phases.
    - `AsTheGame` leaves the phases alone.
  - `ambientSpawning` turns the waves on or off (it writes `ExperimentConfig.encounterSpawnEnabled` for this run).
  - `triggerPhases` starts phases at once, the same as the console's Trigger now. An arena fight is a focus phase
    triggered this way.
- **Build:**
  - `loadout.weapons`: `weaponId`, `level` and an optional `evolutionId`. `replaceWeapons` on means exactly this list.
  - `logbooks`: `logbookId` and `rank`.
  - `items`: item ids.
  - `playerLevel` and `xpIntoLevel`. The level is raised without picks.
  - The game's own console hooks do the work: `DevSetWeapons`, `DevRankUp`, `DevEvolve`, `DevRankLogbook` and
    `GrantLevelUp`.
- **Around the ship:**
  - `enemies`: enemy or variant id, `count`, `distance` in metres, `bearing` in degrees clockwise from the top of the
    screen, `spread`, `eliteAffix`, `focus` and `hpMultiplier`. `focus` marks the unit as a scheduled elite, which gets
    the HUD arrow.
  - `pointsOfInterest`: a type, distance and bearing. This moves the stage's first unused point of that type there, or
    adds one if the stage has none.
  - `boss`: `Summon` raises it now; `AtBeacon` moves the ship to the Beacon and uses it.
  - `shipStart: IntoShore` (with `holdIntoShore`) drives the ship into the nearest shore.
  - `sinkOtherEnemies` sinks every enemy that is not the scenario's own as soon as it appears.
- **Config edits:** `configId`, `path` and `value`, applied as live edits the same way as the console's Config section,
  for example `director.saltwind` / `killBandSeconds` / `3`.
- **1v1:** `pvp.enabled` with the player's draft (`weapons` and `logbooks` in priority order), `countdownSeconds`,
  `matchSeconds` and `stormStartSeconds`. The match runs against the practice bot (simulated on this client) over
  `FakePvpRelay`. The relay answers as the server's relay does: it relays hits, ends the match on time (a draw
  when both hulls are even) or on a sinking, and answers pings. It drops no barrels and checks no rules. `Drop()` and
  `Reconnect()` play a lost connection and the server's answer to the reconnect: the match start again (ended, when it
  ended meanwhile), then the stored result (V5 M8z, 7 Oct, review).

Ids are the configs' own ids. An unknown id is reported in `ScenarioRunner.Errors` (and in the console). It is never
guessed.

## Loading a scenario

In a PlayMode test:

```csharp
yield return Scenario.Load("crab_at_10m");          // or Scenario.Load(asset)
var runner = Scenario.Current;                       // Game, Ship, Spawned (enemy id, unit id), Relay, Errors
CollectionAssert.IsEmpty(runner.Errors);
```

Use the test assembly's usual setup and teardown: `V4TestSaveIsolation`, then load `V4Boot` in TearDown. The scenario
ends with its scene. When it ends, the switches it changed go back: `DemoMode`, the saved Captain and the 1v1 relay.

By hand: press F1, open **Scenarios** and press **Load**. The run scene reloads with the scenario set up.

- **Copy loaded as JSON** puts the loaded scenario in the text box.
- **Load JSON** loads a pasted scenario. This also works in a development build, where the asset list is empty unless
  the scenarios are placed in a `Resources/Scenarios` folder (none ship).

Run the tests one class at a time:

```powershell
powershell -NoProfile -File plan/v5/tools/unity-run.ps1 -Mode PlayMode -Filter V5ScenarioLabPlayModeTests
```
