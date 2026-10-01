# V5 M3d: pressure from enemy count, peaks and valleys, distinct enemies

Owner's notes e, h, k and the sketch (1 Oct 2026): the voyage is dull until 3-4 min and too hard after 6; difficulty
should rise in peaks and valleys like Survivor.io (~60 s waves; the sketch: small close peaks early, a bigger one at
3-4 min, a long valley, two big peaks to the 10:00 climax); harder from more enemies, never from time-scaled HP or
damage; an enemy that looks the same always behaves the same. Answers: stronger, visually distinct types later and a
higher cap (the cap is M3e). Built while the owner was away (decisions D15-D22 in `plan/v5/DECISIONS-WHILE-AWAY.md`,
questions 9-13). Start commit: `06696011`.

| Task | What | Where | Status |
|---|---|---|---|
| I3d.1 | Fixed enemy stats: the per-minute and ordinary-time HP and damage curves are flat; the director's toughness, cap-HP and focus levers are off (it keeps its spawn-rate and floor lever and its elite chance); M3c's interim ×0.85 scale is gone. Later minutes are harder from more enemies, from the late types at their own fixed stats (manta ray, boarding barge, kraken spawn, shield frigate, powder barge: ×2 HP and damage over their old base; the repair tender keeps damage 8) and from a larger specialist share in bands 3 and 4 (55% and 65%, was 35% and 40%) | `Data/Difficulty/*`, `Data/Enemies/*`, `Data/Variants/*`, `Data/Run/ExperimentConfig.asset`, `Timelines/SaltwindTimeline.asset` | done |
| I3d.1 | Fixed HP for the set pieces: the Barnacle King 2800 (fight 68 s median), the Reef Crab ×4.5, the elites ×5.5 (hammerhead) and ×12 (kraken). Early and late summons measured in `model-report.md`: 119 s from 4:30 (known miss, target 45-90; D18, question 13), 64 s from 8:30 | `Enemies/barnacle_king.asset`, `Spawners/*`, the timeline | done |
| I3d.1 | Overtime (after 10:00, D8/D19): the drowned fleet's HP ×6 at 10:00 to ×25 at 15:00 (base 3 × 2^(0.25/min) × a ramp from 2 to 3.5 over 5 min; was ×1.25 to ×4.5), damage ×2 to ×5.6. Most runs end there, so the run-length targets rest on it (question 10) | `SaltwindTimeline.asset` (`overtime`), `SaltwindDifficulty.asset` | done |
| I3d.2 | Keyframed flow: `FlowSettings.smoothWaves` makes the floor and spawn rate follow a line between keyframes (`FlowSettings.Sample`); 22 keyframes from the sketch, final values under one scale so the order holds: small peaks at 0:25, 1:20, 2:20; the big early peak at 3:35-3:45 above the whole valley after it; the valley rising in ~60 s waves with lower spawn rates (4:10-7:00); peaks at 7:30 and 9:35, nothing clamped at the cap. Hordes on the peaks (0:35, 3:35, 7:20, 9:20; 10, 16, 20, 28 ships) | `Config/Schemas/WaveTimelineConfig.cs`, `Runtime/Spawning/FlowDirector.cs`, `DevTools/Balance/SimRun.cs`, `CountPressure.Keyframes` | done |
| I3d.3 | Distinct enemies: `spawnerCombatOverrides` off (the per-band overrides that gave the same ship 1 or 3 shots); hordes are the ordinary ships (speed, HP and damage ×1); a look = model + tint + view size, on archetypes (`visual.tint`, `visual.viewSize`) and variants (`look`); a larger drawn ship has a larger hitbox; the gunner cog keeps its cog model in crimson at 1.15× (it drew the cannon boat's meshes while firing three shots); `EnemyViewTint` (owned by `EnemyViewPresenter`) shares one tinted copy per (material, tint) so the SRP Batcher keeps its batches, records each view's own materials on first use and destroys a burn-flash instance when the tint changes | `EnemyArchetypeConfig.cs`, `EnemyVariantConfig.cs`, `EnemyStatResolver`, `V4RuntimeDatabase`, `EnemySimulation.cs`, `Presentation/Effects/EnemyViewTint.cs`, `EnemyViewPresenter` | done |
| G3d.1 | Retune with the simulator (14 rounds, `tuning.md`; the crowd measured in 15 s bins since round 11). Per-minute targets from the design (`CountPressure.TargetEdits`, D21): alive share at least 0.75× the keyframes' floor (hordes' lulls and release windows included) with M2's ceilings; kill bands unchanged; hull lost ≥ 0.05 of 100 in each minute from 1:00 to 4:00; ≤ 0.6 in any minute from 5:00 to 10:00 | `DevTools/Balance/CountPressure.cs`, `Editor/V5CountPressure.cs`, `Data/Balance/BalanceTargets.asset` | done |
| - | v3-parity preset: the curves, levers, step waves, overrides, the ramp, band shares, white tints and size 1 (gunner cog included), the late types' and variants' V3 stats, the repair tender's inherited damage, V3's boss; the flow (and its hordes) is off under it | `plan/v4-verification/presets/v3-parity.json` | done |
| T3d | EditMode: `V5PressureTests` (the data incl. late types, band shares, horde sizes, repair tender; HP and damage the same at 1 and 8 min; the levers; keyframe maths; the sketch's order; hordes as ordinary ships; targets from the keyframes; the look rule over every enemy that can spawn, by meshes, tint distance and size step, and a check that it catches the owner's cannon-boat example; every keyframe in the data; the crowd (15 s bins, 60 runs) peaking at 3:45-4:15 over the 4:30-6:15 valley and rising to 7:30 and the climax; parity; the boss fight at a 4:30 and an 8:30 summon); `V5EnemyViewTintTests` (shared copies, reuse back to white, burn-flash instance, clear). Older tests now expect the M3d data (`V4AdaptiveM11` switches the levers on to test them, `V4EnemyM2`, `V4SpawnM3`, `V5BalanceM2` reverts M3d through `CountPressure.Revert` for its pre-V5 calibration, `V5DemoBaselineTests`, `V5FlowM1Tests` read the keyframes through `Sample`, `V5LevelRhythmTests`). PlayMode: `V5PressurePlayModeTests` (the floor on the keyframes' line with the clock jumped; a cannon boat's HP and damage at 1:00 and 8:00: 12 HP both), `V5FlowPlayModeTests` and `V4AdaptivePlayModeTests` updated | `Tests/V4/` | pass |

## Results (simulator, 200 runs per personality, seed 1: `model-report.md`)

- Targets missed: 2 of 76, both bounded in the tests and listed for the owner (D22): the First-Timer's run 9.5 min
  (band 5.5-9; question 9) and minute 6 kills 20 (30-140, the baseline's boss fight, since M2). M3c's and M3's known
  misses (the Glass Cannon's run, the Build Planner's boss wins) are cleared. The Thumb Sailor 7.3 min (7-12).
- Baseline crowd per minute (alive share of the cap, 0-9): 0.20, 0.26, 0.36, 0.31, 0.48, 0.41, 0.44, 0.76, 0.47, 0.75
  (M3c: 0.09, 0.21, 0.23, 0.16, 0.51, 0.66, 0.70, 0.85, 0.66, 0.85): busier from the first minute, lighter from 5:00
  to 7:00. In 15 s bins: small peaks at 0:30, 1:15 and 2:15, about 44 alive at 3:45-4:00 against about 34 through
  4:30-6:15, about 65 at 7:30 and 69 at 9:30 (`tuning.md`).
- Hull lost per minute (of 100): 0.05, 0.07, 0.27, 0.22, 0.14, 0.14, 0.18, 0.24, 0.14, 0.23 (M3c: none before 2:00,
  up to 0.36 at 6:00).
- Median run (players weighted by share) 11.0 min; the Barnacle King fight 68 s; the scheduled elite 7.7 s; the Reef
  Crab 24 s. The larger hitboxes of the bigger ships are not in the simulator (the playtest checks them).
- Late fodder dies fast (fixed HP against a growing build); the pressure there is count and the late types, as asked.
- In the Editor (`playable/`, clock jumped to 0:45, 3:30, 5:00, 7:30, 9:30, ship invulnerable and standing): the crowd
  and enemy mix at each moment with screenshots, 0 game errors (`steps.txt`). At 3:30 only the Reef Crab and the
  elite hammerhead are up: jumping past 2:30 and 3:00 opens both arenas, which pause ambient spawning (a check
  artifact; the simulator and the PlayMode floor test cover that moment).

Not done here: the enemy cap (M3e); the per-band attack overrides stay in the data, switched off.

Review: 1 blocker, 10 majors, 5 minors; then a second pass: 1 major (the crowd, not only the floor, must show the
sketch's peak: the simulator now records the crowd in 15 s bins and a test checks the peak over the valley) and 6
minors (tint walks only a view's own renderers; every keyframe checked against the data; minute 6 kills bounded; the
hitbox not in the simulator, recorded; a comment; the 4:30 summon, owner question 13). All fixed or recorded.

Evidence: `editmode.json` (397/397), `playmode.json` (58/59: the known intermittent `PowderCache_StillSpawnsThroughTheEncounterExecutor`, then 24/24 three times in a row for its class), `tuning.md`, `model-report.md`, `playable/`.
