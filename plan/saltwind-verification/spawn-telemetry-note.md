# Spawn scheduling telemetry (C02)

Prepared: 2026-09-22. Updated after live telemetry wiring.

## Scope

Seeded Saltwind solo run evidence for checkpoint **C02** spawn scheduling:

1. Run `V3SaltwindRun` with playtest seed **12345** (or the locked home seed).
2. Confirm `RunCombatTelemetry` JSON includes:
   - `pressure_elite_spawned` / `pressure_pulse_started` (from `V3PressureBeatDirector`)
   - `spawn_checkpoint` at **120s**, **240s**, **360s** (`SaltwindSpawnTelemetryLogic`)
   - `ambient_spawn` rows with `band`, `enemyId`, `ambientBudgetCarry`, `activeEnemies` (`EncounterSpawner`)
3. Verify `ambientBudgetCarry` in checkpoints and ambient rows stays **≤ 3** (default `AmbientSpawnBudget.MaxCarry`).

## Runtime wiring (code)

| Event | Source |
| --- | --- |
| `ambient_spawn` | `EncounterSpawner.TryExecuteSpawnBurst` when profile id is `saltwind_ambient_band_*` |
| `spawn_checkpoint` | `EncounterSpawner.Tick` → `SaltwindSpawnTelemetryLogic.MaybeRecordCheckpoint` |
| `pressure_*` | `V3PressureBeatDirector` (unchanged) |

Export path after death or results screen:

`%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/telemetry/combat_<profileId>_<seed>.json`

## Capture procedure (human smoke)

1. Open **V3SaltwindRun**, ensure `StageBalanceProfileHost` + telemetry-enabled experiment profile (default Saltwind bootstrap).
2. Start run with seed **12345**; play or idle-cheats until at least **6:00** (checkpoints at 2/4/6 min).
3. Die or open results so `RunCombatTelemetry.WriteExportFile()` runs.
4. Grep the JSON for `"spawn_checkpoint"`, `"ambient_spawn"`, `"pressure_elite_spawned"`, `"pressure_pulse_started"`.
5. Optional: in play mode, watch `EncounterSpawner.DebugAmbientBudgetCarry` in spawn debug UI (must not exceed **3**).

## Status

| Item | Status |
| --- | --- |
| EditMode gates (modifiers, budget, schedule logic) | **Green** — `SpawnSchedulingGateTests`, `EncounterScheduleRunLogicTests` |
| Composition bands (Step 04) | **Green** — `SaltwindAmbientCompositionLogic` + `SaltwindAmbientCompositionGateTests` |
| Live telemetry events wired | **Done** — excerpt `c02-telemetry-excerpt-seed1622933822.json` (developer run seed **1622933822**, not 12345) |
| Human playtest at normal speed | **Directional** — `PLAYTEST_REPORT.md` |

## Unit-test coverage

- `EncounterScheduleRunLogicTests`
- `SpawnSchedulingGateTests` (modifiers + ambient budget)
- `SaltwindAmbientCompositionGateTests`
- `SaltwindScheduleGateTests` (schedule + band anchor assets after Author Step 04)
