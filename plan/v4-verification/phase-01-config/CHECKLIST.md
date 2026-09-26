# M1 — Config assets

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `ef5c7175` |
| Status | Implemented, reviewed, awaiting playtest |

## What M1 delivers

**Config core** (`V4/Config/Core`, assembly `RoguePirates.V4.Config`, references Simulation only):
- `V4ConfigAsset`: id, schema version, balance version and validation hook.
- `ConfigFieldWalker`: the single field enumerator, following Unity serialization rules. Hashing, presets, the console and the coverage test all use it.
- `CanonicalConfigWriter`: declaration-order JSON, floats as round-trip "R", invariant culture, references by id, then SHA-256.
- `ConfigSet`, whose hash is the SHA-256 of the sorted `id:hash` lines.
- `ConfigSetRuntime`: per-run copies with references remapped to the copies, edits by path (`phases[2].ratePerSecond`) through structs and lists, a change log, the `ConfigChanged` event, and reset for one field or one config.
- `PresetCodec`: presets are JSON diffs against the defaults; list length changes are stored as `path#count`.
- `ConfigMigrations`: schema migrations for presets; older presets are rejected when no migration exists.
- `ConfigValidator`: rules for ids, dangling references, weights, caps vs bursts, stat ids, negative timings, trigger cycles and phase-deadline consistency.

**Schemas** (`V4/Config/Schemas`):

| Config | Holds | Live in M1? |
|---|---|---|
| `WaveTimelineConfig` | Stage limits; 14 phases (5 bands, Reef Crab, 2 elite beats, 2 swarm pulses, boss, Powder Cache, overtime, storm) with trigger, duration, rate, pools, warning and flags; tag caps; overtime and storm settings | **Yes**: profile fields, band rates and schedule values. Pools apply from M3. |
| `EnemySpawnerConfig` ×12 | Anchor archetype, spawn mode, layout, timing, placement, elite, movement and attack blocks | **Yes**, through per-run spawn profile copies. Special-behavior modules pass through until M8. |
| `EnemyArchetypeConfig` ×14 | Identity, stats, rewards, visuals, tags, concurrency tags, combat profile | Stored. Applies from M2. |
| `DifficultyCurveConfig` | GameMath HP/damage curves, tier/stage multipliers, global 2/3, ordinary-time curves, overtime, director clamps | Overtime values **yes**, through the profile. The rest from M3/M8. |
| `WeaponConfig` ×10 | Every field of `WeaponDefinition` after the runtime catalog retunes | Stored. Applies from M5. |
| `RunConfig`, `ExperimentConfig` | Tick rate and step cap; the built-in `D_FULL_COMBAT_V2` experiment values | Run config **yes** (V4 clock). Experiment from M3/M5/M9. |

Fields that are not live yet carry `[AppliesFrom(n)]`, and the console shows "(Mn)" next to them.

**Generated data:** `V4/Data/**` (40 configs plus `ConfigSets/SaltwindDefault.asset`), produced by `RoguePirates/V4/Migrate V3 Data to Configs` (`V3ConfigMigrator`). The migrator:
- reads the V3 profile, the Resources schedule (the copy the spawner loads) and its spawn profiles;
- applies the enemy and weapon catalogs to a deep clone of a clean database, so no V3 asset is mutated;
- reads the combat-profile registry, the composition tables (by reflection) and `DemoPacingSettings`;
- keeps existing V4 assets unless forced.

**Legacy bridge:**
- `LegacyConfigMapping` owns every stage-profile field or lists it as pass-through (with the phase that will own it), and a test enforces this. One mapping serves both the migrator and the runtime.
- `LegacyConfigRuntime` builds the per-run copies:
  - the stage profile, from the V3 asset plus V4-owned fields, pointing at the schedule copy;
  - the Resources encounter schedule;
  - all 12 spawn profiles.
- `V4LegacySpawnBridge.InstallSchedule` gives the spawner the schedule copy before the first reset.
- On live edits it patches the copies and rebuilds the profile snapshot, replaying the chest purchase count so prices don't reset.
- No V3 asset is ever modified.

**Scene:** `V4SaltwindRun` no longer has `StageBalanceProfileHost` (`V4SceneSetup.ApplyM1`). `V4Game` now holds `configSet` and `legacyBaseProfile`.

**Console:**
- The `Config` section has rows for every field, grouped by config type and asset, with typed editors applied on Enter or ✓, and enums that cycle.
- Lists can grow or shrink; fields and whole configs can be reset.
- Search works on field names.
- The header shows the set hash and edit count, and a change log is available.
- Presets can be saved (to persistent data and, in the Editor, `V4/Data/Presets`), loaded, or used to restart the run.
- In the Editor, "Write back" copies edits into the asset.

**Run request:** `RunRequest.RequestPreset(json)` applies a preset before anything reads config, so it takes effect from t=0.

## Legacy reader audit

| Changed | Readers | Classification |
|---|---|---|
| `StageBalanceProfileHost` removed from `V4SaltwindRun` | `StageBalanceProfileResolver.TryResolveForCurrentScene` (called by `V3RunBootstrap` only); `WeaponEnemyPlaytestBootstrap/Director` (Module Lab only); `SaltwindStageBalanceSetup` (Editor, V3 scene) | Adapted: `LegacyProfileAdapter.TryBegin(runtimeProfile)` runs the same validator. The others are absent from the V4 run scene. |
| Spawner schedule replaced by the per-run copy | `EncounterSpawner` (entries, beats, boss, cache, overtime profiles), `StageBalanceProfileValidator`, `EncounterScheduleExportLogic` (profile hash) | Adapted: all read the copy through the spawner or the profile. Copies keep their source names, so the profile hash is unchanged. |
| Stage profile replaced by the per-run copy | Authoring readers (`POIManager`, `V3PressureBeatDirector`, `V3StagePoiPlacementOptions`) and every Export reader (inventory in the M1 notes) | Adapted: Authoring readers see the live copy; Export readers see the rebuilt snapshot. |

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 47 / 47 | `editmode-results.json` |
| V4 PlayMode | 5 / 5 (adds live config edits reaching the legacy run, and a preset applied from the start) | `playmode-results.json` |
| V3 regression, PlayMode | 34 tests, only the known storm-arena failure | `v3-regression-playmode-results.json` |
| V3 regression, EditMode | 226 tests (187 V3 + 39 V4 at the time), only the 3 known failures; your PlayerPrefs snapshotted and restored around the run | `v3-regression-editmode-results.json` |
| Traces: V4 at default config vs V3 goldens, before the arena harness change | Exact on 101 / 202 / 303, replay identical, profile hash unchanged | first M1 batch |
| Traces with arena kills (final, after review fixes) | V4 == V3 exactly on 101 / 202 / 303, replay identical | `player/`, `trace-comparison.txt` |
| Config reaches the run (band 0 rate 0.6 → 1.2/s, preset `band0x`) | Minute 0: 35 → 69 spawns (1.97×) on both seeds; minute 1: 1.64×, as the doubled rate hits the enemy cap by 2:00 | `player/*-band0x.jsonl` |
| Band 2 rate 1.8 → 4 (`band2x`, the plan's example) | No change in spawns in minutes 4–6: the ambient cap (72 alive) is full from about 3:40, so spawns only replace kills (R-76). The criterion is restated: rate edits change density only below the cap | `player/*-band2x.jsonl` |
| Playable check | Menu → run → Config section (search filters to matching fields) → band 2 live edit (interval 0.5556 → 0.25 s) → restart with preset on seed 101 (0.25 s from t=0) → reset; 0 console errors | `playable-check/` |
| Write set | Clean (see `diff-summary.md`; the TMP fallback atlas written during play sessions is reverted) | `diff-summary.md` |

## Harness change found during M1 (R-75)

A miniboss arena pauses ambient spawns until its focus enemy dies. The kiting autopilot never killed the Reef Crab (2:30), so every earlier trace had no ambient spawns after 2:30. The harness now kills an arena's focus enemy 20 s after the arena opens, through the weapon-hit path (header `killArenaAfter`), identically on the V3 and V4 sides. The player V3 goldens were re-recorded with it (`player/README.md`); the M0 player goldens are superseded.

## Independent review (M1)

No blockers. Findings and resolutions:

| # | Finding | Resolution |
|---|---|---|
| 1 | Major: the plan's band-2 density check shows no change | Explained: cap-bound (R-76). Demonstrated instead with band 0 (1.97×). Criterion restated above |
| 2 | Major: some live edits half-apply (band start times overlap or gap; global gap cached) and non-live fields were untagged | Band start times and the global gap are written only when a run starts; live edits skip them. Every non-live field is tagged: "(restart)" for values cached at run start, "(Mn)" for later phases. Identity fields are read-only. Comment corrected: such values need a restart with a preset |
| 3 | Major: Reset / Reset all / preset load did not restore list lengths | `ResetConfig` resizes lists to the asset's lengths before resetting values; test added (reset after resizes, and a preset applied after a resize) |
| 4 | Major: gate evidence files missing, dirty-tree commit stamp, old goldens failing the header check | Evidence written; trace headers mark `+dirty`; comparers treat a missing `killArenaAfter` as -1 (disabled) and a missing `preset` as none |
| 5 | TMP fallback atlas modified | Reverted (it is rewritten by every play session; reverted before each commit) |
| 6 | Tests do not cover every rule/field | Added: remaining timeline rules, StatId helper, enum rejection, reset of list lengths, combat/reward/visual parity, every mirrored weapon field, composition pools and shares, Reef Crab time, mirror completeness in both directions, recursive console coverage |
| 7 | Walker drops some Unity types | The recursive coverage test now fails on any serialized field whose type has no kind (none today) |
| 8 | Curve weights and wrap modes not hashed | Added to the canonical JSON. Unity object references still hash by name (recorded) |
| 9 | A mid-run rebuild changes the profile hash for unrelated reasons | Recorded under R-73 (the snapshot reads weapon stats from a database the catalogs mutate); removed by the M2 runtime database |
| 10 | V4 set hash not stamped in traces | Header `v4SetHash` added (not a compared key, since V3 has none). Per-config hashes and the change log in telemetry arrive with M6 telemetry |
| 11 | Edits are not validated | The Config section validates after every edit and shows the errors; invalid edits stay applied and flagged |
| 12 | Console recomputes the hash and lists files every GUI event | Hash, validation and the preset list are cached and refreshed on change or save |
| 13 | Migrator: id comparisons outside Legacy/, hard-coded overtime time, silent dirty-asset reads, `charge_windup` without a member | Concurrency tags moved to `LegacyConfigMapping`; overtime trigger comes from the stage deadline; dirty assets logged; `ram_raider` (the charge-windup enemy) gets its archetype with the specialist variants in M2 |
| 14 | Schema gaps vs I1.2 | Recorded below |
| 15 | Harness arena kill | Deterministic and identical on both sides; revisit when arenas are ported (M3/M8) |
| Nits | Enum numbers, handler exceptions, preset format version | Undefined enum values rejected; a throwing listener no longer fails an applied edit; presets newer than the build are rejected. Static registries (`ConfigMigrations.Steps`, the cached search text) noted |

## Deviations and gaps, recorded

1. **Live vs restart.** Band start times, the schedule's global gap, spawner start delays and the V4 tick rate apply when a run starts with them (restart with a preset), because V3 or V4 caches them at run start.
2. **Composition pools, tag caps per phase, curves and spawner pressure weights** are stored and validated but apply from M3 (tagged). The profile-level tag caps (cannonboat, area denial, charge windup) are live.
3. **Swarm pulses** share one duration and intensity in V3; the second pulse's own values are ignored until M3 (warning logged, tooltip).
4. **Schema gaps vs plan I1.2:**
   - `DifficultyCurveConfig`: tier-unlock rules (`EnemyTierLogic`) arrive with the spawn controller in M3.
   - `WeaponConfig`: pool keys, damage-share flag and per-pattern blocks arrive in M5.
   - `EnemyArchetypeConfig`: special-behavior module blocks and the pool hint arrive in M2/M8; specialist aliases become variants in M2.
5. **Band 0 = 0.5/s (your decision on R-01, 26 Sep).** The live band-0 rate is now 0.5 enemies/s, matching the doc and the profile's own `ambientRatePerSecond`. This is an intended difference from V3: default V4 runs no longer match V3 event for event. The exact V3 check continues with `plan/v4-verification/presets/v3-parity.json` (band 0 back to 0.6), compared with `compare-traces.py --ignore-preset`. The default-config traces are the new V4 baseline.
6. **Hashes.** Unity object references hash by asset name. The config set hash is not yet in telemetry (M6).
