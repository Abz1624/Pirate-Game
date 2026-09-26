# M3 — Wave Timeline and Spawn Controller

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `eba369a6` (M2) |
| Status | Implemented, reviewed and committed. Playtest later together with M1 and M2, at your request |

## What M3 delivers

**V4 decides what spawns and when.** `SpawnController` (`Runtime/Spawning`, module id `spawn`) runs the whole Saltwind schedule from the `timeline.saltwind` config:
- **Ambient bands**: phases with `ratePerSecond > 0` and a spawner. Each runs from its trigger until the next ambient phase starts, with V3's interval scheduling (no catch-up), cap retry without debt, 0.35 s gap clamp, 2 s grace, per-spawner caps, and the ambient cap of 72 (80 max − 8 reserved).
- **Composition** from the phase's pools: roll the specialist pool against the ordinary share, pick from a random start and walk the pool, skipping enemies whose tier isn't unlocked or whose concurrency tag is at its cap. Tags and tiers are config (`concurrencyTags`, `spawnTier`, `DifficultyCurve.tierUnlocks`) instead of V3's id tables. With equal pool weights the RNG calls and their order are V3's.
- **Focus phases** (the Reef Crab miniboss, the two scheduled elites): one scripted spawn with a warning, boss-fight rule (skip for good, or wait until it ends), retry window, Marks registration, and the miniboss arena (ambient spawns pause until the focus enemy dies, then every band tries once immediately).
- **Swarm pulses**: timed rate multipliers.
- **Overtime**: after the deadline, pursuer/harrier spawners by weight, interval shrinking every 30 s, overtime HP/damage multipliers.
- **Storm**: warning, contraction of the safe region, damage outside it after the 8 s run grace. One clock replaces V3's two storm components.
- **Rate stack**: the legacy base (Dead Calm), experiment density, pulses, and ×0.5 in the boss fight. Ordinary-time HP and damage growth after 2:00 comes from the difficulty config.
- **Events**: `Spawned`, `PhaseChanged` and `Warning` on CombatEvents. Warnings come from phase templates (`{label}`, `{lead}`), so no text has a fixed time in it.

**The legacy spawner is an encounter executor only (register D4).** The kept `EncounterSpawner` gets a per-run schedule copy with no ambient entries: it runs the Beacon boss and the Powder Cache ambush, and it remains Dead Calm's sink (read back through `LegacyEncounterExecutor`). The scene's serialized schedule is `V4/Data/Legacy/V4EncounterOnlySchedule.asset`, so it cannot run ambient spawns even if the install fails.

**No V3 spawn directors.** V3PressureBeatDirector, V3OvertimeDirector, V3SaltwindStormDirector, V3StormEdgeDamage and V3RouteEventDirector are no longer created. The spawn bridge (`V4LegacySpawnBridge`, tagged for deletion in M3) is deleted. `SpawnDirectorDebugUI` (F9) is no longer created (R-71).

**Frame order (until M8).** The legacy enemy world, weapons and run clock still tick around the spawn logic, so each V4 part runs where the V3 component it replaces ran (`LegacyUpdateSlot`, tagged for deletion in M8):
- overtime: a new GameObject created where V3 created V3OvertimeDirector (before the clock);
- ambient: a scene component on `/Systems`, updated right after `RunManager.Update` and before `EncounterSpawner.Update` and the weapons;
- storm, beats: added to `/Systems` where V3 added its storm and beat directors;
- storm edge: added to the ship where V3 added V3StormEdgeDamage.

Unity updates a loaded scene's components in descending local file-ID order (measured with the profiler), and there is no API to choose a file ID. `V4SceneSetup.ApplyM3` therefore adds the ambient slot through the API, then moves its file ID into the free gap below RunManager's (658786993) with one targeted text replace, and re-imports the scene. Profiler check after the change: `RunManager.Update → LegacyUpdateSlot(ambient) → EncounterSpawner.Update → WeaponController.Update`.

**Console.** A Wave Timeline section shows every phase with its live state, Trigger now / Skip, duplicate, delete and move up/down. Structural edits are ordinary logged config edits, so presets record them. The header shows the current phase. Config references (a phase's spawner, a focus archetype) can now be edited in the Config section (click to cycle) and are carried in presets by `configId`.

**Other**
- `V4Phase.Current = 3`.
- Scripted-phase data set by `V3ConfigMigrator.ApplyM3`: Reef Crab 22 m ring, loot-stream angle, Rich, HP ×4, damage ×1.15, waits out the boss fight and any arena, retries until it spawns. Elites use their beat spawner, are skipped for good during the boss fight, have a 20 s retry window and Marks index 0/1. Beat warnings use the stage's 5 s. The storm warning text is V3's.
- Tests reload the V3 database *and* re-resolve V3's experiment profile before comparing (R-82).

## Legacy reader audit

| Removed / replaced | Readers in V4 scenes | Classification |
|---|---|---|
| V3PressureBeatDirector, V3OvertimeDirector, V3SaltwindStormDirector, V3StormEdgeDamage, V3RouteEventDirector (not created) | Created only by the bootstrap; no other runtime code looks them up | Replaced by the spawn controller |
| V3MinibossArena (static) | EnemyWorld kill path (`NotifyAgentKilled`), kept spawner (`BlocksAmbientSpawning`), trace harness | Never begun in V4: the kill path's call is a no-op, and the kept spawner is not blocked (intended difference below). Reset at run start (R-68). The harness reads the V4 arena through `ISpawnController` |
| EncounterSpawner ambient path | POIManager (boss, powder cache), V3FloorPowerupWorld (Dead Calm base rate), EnemyWorld despawn notify, V3RunPlaytestHotkeys | Adapted: the kept spawner runs encounters from an encounter-only schedule; Dead Calm is read back into the V4 rate stack; despawn counts are polled by V4 |
| SpawnDirectorDebugUI | Created by the presentation adapter only | Not created (R-71) |
| `EnemyWorld.SetExperimentMultipliers`, `encounterSpawnEnabled` (were set by `EncounterSpawner.ApplyCombatExperiment`) | EnemyWorld | V4 sets them from `ExperimentConfig` at run start and on edits |
| `SaltwindSpawnTelemetryLogic` per-run reset (was in `EncounterSpawner.ResetForNewRun`) | Balance-lab telemetry | Reset by `LegacyEncounterExecutor.ResetForRun`; V4 does not write ambient spawn records (CombatEvents `Spawned` replaces them) |

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 80 / 80, also right after a PlayMode run. New: grace, band interval, cap retry without debt, gap clamp, ambient cap 72 (reserved slots), boss rate, pulses, Reef Crab arena and immediate resume, elite Marks and boss skip, overtime, storm contraction, storm-edge formula and grace (fake harness in V3's frame order); live edits (a band added mid-run starts on time and ends the previous band; a band retimed before it starts keeps the bands sequential); condition, event and after-cleared triggers; Trigger now and Skip; Module Lab runs no timeline spawns or pulses; Dead Calm probe; overtime, storm and ordinary-time formulas equal V3's functions; composition picks what V3 picks on 4,000 draws with the same seed; warnings from templates; config references edit and travel in presets; scripted phases carry V3's values; only named adapters reference the legacy spawner/weapons; no V4 code uses the V3 spawn directors | `editmode-results.json` |
| V4 PlayMode | 10 / 10. New: ambient spawns come from the V4 controller (legacy schedule has no ambient entries, all five slots bound, no V3 beat/overtime directors); the Powder Cache still spawns through the encounter executor; every spawn slot ticks each frame | `playmode-results.json` |
| V3 regression | EditMode 258 (187 V3 + 71 V4 at the time): only the 3 known balance-lab matrix failures (includes the production matrix run, plan D14). PlayMode 37: only the known storm-arena failure. No new failures; your PlayerPrefs snapshotted and restored (First Conquest and home seed still absent) | `v3-regression-editmode-results.json`, `v3-regression-playmode-results.json` |
| Traces | Final, Windows player, after the review fixes: V4 with the v3-parity preset == V3 goldens on seeds 101/202/303 (MATCH ×3, exact); replay identical; default == M2 baseline (MATCH ×3). New long runs to 900 s with an invulnerable ship, V3 and V4: MATCH ×3, each reaching overtime (23–27 spawns after 10:00) and the storm (11–14 spawns after 12:00) | `trace-comparison.txt`, `player/` |
| Playable check | After the review fixes: ambient from V4 (header "Band 0", legacy ambient entries 0); Wave Timeline section; Trigger now on the Reef Crab → arena on → killed → arena off, phase Done; Skip on the elite hammerhead → Skipped; band 0 at 3/s → 25 spawns in about 5 s (5 before); duplicate copies band_2 with its spawner reference. Console: 0 errors | `playable-check/` |

## Intended differences (V3 → V4)

1. **Queued encounters are not blocked by miniboss arenas.** V3's arena also stopped the spawner's trigger queue; V4 arenas only pause ambient spawns. This affects the Powder Cache ambush and summoner minions (`EnemySpecialBehaviorSystem`), which use the queue; the Beacon boss spawns immediately in both.
2. **Warning texts are generated** from phase templates ("WARNING — Swarm pulse incoming!" instead of "…at 4:00!"). No effect on play.
3. **Overlapping pulses stack.** V3 used one modifier key for both pulses; V4 keys each pulse phase. The default timeline's pulses (4:00 and 7:00, 30 s each) never overlap.
4. **An arena ends when its focus enemy is gone for any reason**, not only on a kill (V3 `NotifyAgentKilled`). The default focus enemies (Reef Crab, elite hammerhead/kraken) only leave by dying. Tied to the open R-75 decision.
5. **Text and telemetry labels come from the timeline**: arena toast/telemetry "MINI BOSS — Reef Crab miniboss" / "Elite Hammerhead" (V3: "Reef Crab" / "Hammerhead elite"), pulse telemetry `beat` = phase id (`swarm_pulse_a`; V3 `swarm_a`). The balance-lab `spawn_checkpoint` telemetry still comes from the kept spawner and now logs its own multiplier (1) and carry (0), not V4's rate.
6. **Dead Calm multiplies with experiment density** instead of replacing it (V3 overwrote the density base and reset it to 1 afterwards). Equal while density is 1 (default).
7. **V4 stops overtime spawns when the run ends**; V3's overtime director kept spawning after death.
8. **Frame-level detail kept as V3**, including the ×0.5 boss rate, per-frame storm damage throttled by i-frames (R-57), the scheduled elites being plain units (R-03), the Reef Crab at 150 s (R-02), and overtime spawning shark/cannonboat stats (R-05). Those stay open register decisions.

## Deviations, recorded

1. **Route events are not ported.** They are off in Saltwind (`disableAutomaticRouteEvents`, a pass-through profile field until M7), so V3 never runs them here. If they are wanted, they become timeline phases.
2. **The V4 fixed tick is not used for spawning yet.** The spawn controller runs in the legacy update slots until the enemy world is V4 (M8), for exact trace parity (see Frame order).
3. **The ambient slot's file ID is set by a text edit** of the two V4 scenes, done by the scene setup tool, because Unity has no API for it. Re-saving keeps IDs; the tool is idempotent and re-checks the order.
4. **Kept spawner and V4 ambient now run in the opposite order within a frame.** The encounter-only spawner runs right after the V4 ambient slot instead of before V3's ambient code. They share the director RNG stream (placement) when a queued encounter attempt (Powder Cache, summoner minions, boss retries; the first boss attempt is immediate) and an ambient spawn fall in the same frame.
5. **Timeline edits are live, bands run in time order.** A time-triggered ambient phase added mid-run gets its run, a band that has not started follows its edited trigger time, and each band ends when the next band by start time begins (V3: the next schedule entry, which is also the next by time, so the default timeline is unchanged). Trigger now restarts a band. Duplicated focus phases are not scheduled elites (Marks index cleared).
6. **Structural console edits are logged field by field** (only fields that change), and each edit rebuilds the legacy stage profile, so duplicating or moving a phase adds a run of entries to the tuning log.
7. **Traces that reach overtime and the storm use an invulnerable ship** (`invulnerable` in the trace request, same in V3 and V4). Storm-edge damage is therefore not visible in them; its formula, grace and gating are covered by EditMode tests.

## Frame order in Unity's test runner

Profiler in play sessions (scene opened in the Editor, and the scene reloaded at runtime): `overtime slot → … → RunManager.Update → ambient slot → EncounterSpawner.Update → WeaponController.Update → … → storm slot, beats slot`, matching V3. Unity's PlayMode test runner orders them differently (measured: the ambient slot before the clock, the overtime slot after it), so the PlayMode test checks that every slot ticks each frame, and exact trace parity in the player is the proof of the order.

## Independent review

A read-only reviewer checked M3 against the plan and the ported V3 code. No blockers; it confirmed the ambient path, composition (RNG calls and order), beats, overtime, storm and storm-edge ports, the Dead Calm probe, the encounter executor, director creation order, the scene edit and the write set. Findings and what was done:

| # | Finding | Resolution |
|---|---|---|
| M1 | Major: live edits of ambient phases (duplicate, retime, reorder) left phases waiting forever, gaps or overlaps | Time ambient phases added mid-run get their run; unstarted bands follow their edited trigger time; bands end at the next band by start time; Trigger now restarts a band; runs past their window are marked done even while spawning is paused. EditMode tests for an added band and a retimed band |
| M2 | Major: evidence incomplete (V3 regression pending; no trace reaches overtime or the storm; no Dead Calm test) | V3 regression run; long invulnerable V3/V4 traces to 900 s match exactly; Dead Calm probe test |
| 1 | Encounter-order disclosure inaccurate (Powder Cache and minions are queued) | Deviation 4 and intended difference 1 corrected |
| 2 | Live edits no longer reached the encounter executor's schedule | Timeline and spawner edits also update the encounter-only schedule |
| 3 | Module Lab still ran swarm pulses | The lab runs no scripted phases at all; test |
| 4 | Arena ends on any removal, V3 only on a kill | Intended difference 4, tied to R-75 |
| 5 | Text and telemetry label drift | Intended difference 5 |
| 6 | Dead Calm and density compose differently | Intended difference 6 |
| 7 | Test gaps (triggers, storm-edge formula, reserved cap, Powder Cache, stale assertions) | Tests added; stale assertions now check the V4 phase rate |
| 8 | File-ID ordering: no guard, docs disagreed | Slots record the clock they saw; order measured with the profiler in both load paths; docs corrected; test-runner difference recorded above |
| 9 | Register out of date (R-71, R-68, R-75) | Updated |
| 10 | Console trigger edge cases | Trigger now restarts a finished band; duplicated focus phases are not scheduled elites; ambient runs complete after their window |
| 11 | Structural edits flood the tuning log | Recorded (deviation 6) |
| Nits | Double PhaseChanged; overtime after run end; others | Double event removed; overtime-after-end recorded (intended difference 7); remaining nits noted, no behavior change |

## Incident during M3

The Editor crashed once (Windows error 0x5AF, out of commit memory) while an Editor play session ran at the same time as a batch-mode trace-player build; the build took 2.2 h under that pressure. No work was lost. From then on, Editor play sessions are not run during batch builds. The trace runner's interrupted batch left the standalone player's PlayerPrefs key in place; it was restored to its state before the batch (absent).
