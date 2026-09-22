# Step 14 — automated regression evidence (2026-09-23)

Unity **6000.3.11f1**. Repo root: `$env:UNITY_PROJECT_PATH = '...\Pirate Game Unity'`.

## EditMode — connected Editor (`unity command run_tests`)

| Filter (`testName`) | Passed | Failed |
| --- | ---: | ---: |
| `Saltwind` | 56 | 0 |
| `RunEncounterTracker` | 7 | 0 |
| `StageBalanceProfile` | 7 | 0 |
| `EncounterScheduleRunLogic` | 4 | 0 |
| `V3StagePoiPlacer` | 1 | 0 |
| `AmbientSpawnBudget` | 2 | 0 |
| `SpawnModifier` | 2 | 0 |
| **Saltwind delivery slice** | **79** | **0** |

## EditMode — full suite XML (Editor closed)

```powershell
unity close "Pirate Game Unity"   # after save_all if needed
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

**Latest run (2026-09-23):** `editmode-results.xml` — **124/124** `HighSeas.Tests.EditMode.dll` **passed**; **148/150** total (2 failures are PlayMode fixtures executed in EditMode context — not Saltwind gates). Fixed `LevelOfferGeneratorTests.FirstLevelUp_*` for `startingWeaponId` alignment.

## PlayMode — boss gate

| Source | Result |
| --- | --- |
| `BossEncounterPlayModeTests` inside `editmode-results.xml` | **2/2 passed** |
| `unity test ... --mode PlayMode` → `playmode-results.xml` | **0 tests discovered** (tooling) |

See `playmode-boss-evidence.md`.

## Profile export

```text
unity command eval_file C:/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/plan/saltwind-verification/export-profile.eval.cs
```

Hash: `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` → `profile-export/`

## C02 telemetry excerpt

`c02-telemetry-excerpt-seed1622933822.json` — `ambient_spawn`, `spawn_checkpoint`, `boss_summoned` from developer run (full JSON under `%LocalAppData%/../LocalLow/DefaultCompany/Pirate Game/telemetry/`).

## Still pending (not git)

| Item | Doc |
| --- | --- |
| Desktop perf trace | `PERF_TRACE_PENDING.md` |
| External newcomer panel | `PLAYTEST_REPORT.md` |
| Optional seed **12345** full JSON | `spawn-telemetry-note.md` |

## Manual smoke

| Flow | Status |
| --- | --- |
| Menu → Saltwind solo → death/results → retry | **Directional** — `PLAYTEST_REPORT.md` |
| Protected movement/camera | `PROTECTED_SETTINGS.json` unchanged |

## Delivery commit

Owner committed locally (**`0ee6b0fc66dc79e87765517e6d75a7d6e8075fe1`** at automation time); not re-verified after `LevelOfferGeneratorTests` fix in this pass.
