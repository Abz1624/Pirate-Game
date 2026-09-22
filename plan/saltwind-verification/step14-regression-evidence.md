# Step 14 — automated regression evidence (2026-09-23)

Unity Editor **6000.3.11f1**, Pipeline port **7800**, project open (PID varies). Commands from repo root with `$env:UNITY_PROJECT_PATH = '...\Pirate Game Unity'`.

## EditMode (connected Editor — `unity command run_tests`)

| Filter (`testName`) | Passed | Failed |
| --- | ---: | ---: |
| `Saltwind` | 56 | 0 |
| `RunEncounterTracker` | 7 | 0 |
| `StageBalanceProfile` | 7 | 0 |
| `EncounterScheduleRunLogic` | 4 | 0 |
| `V3StagePoiPlacer` | 1 | 0 |
| `AmbientSpawnBudget` | 2 | 0 |
| `SpawnModifier` | 2 | 0 |
| **Total (Saltwind delivery slice)** | **79** | **0** |

## Profile export (post-regression)

```text
unity command eval_file C:/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/plan/saltwind-verification/export-profile.eval.cs
```

Output: `plan/saltwind-verification/profile-export/SaltwindSoloStageBalanceProfile.json` + `.hash.txt`

**Content hash:** `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d`

## PlayMode

Async runs (`--mode playmode --async_tests`) completed with **0 tests discovered** via Pipeline (`test_status` summary empty). **Manual follow-up:** Test Runner → PlayMode → `BossEncounterPlayModeTests` (2 tests), or close Editor and run PlayMode assembly from CLI.

## Full EditMode XML (CI-style)

**Pending** while interactive Editor holds the project lock:

```powershell
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

(Error when Editor open: project already open — close Editor and re-run.)

## Manual smoke (handoff §14.2–14.3)

| Flow | Status |
| --- | --- |
| Menu → fresh Saltwind solo → combat → death/results → retry | **Directional** — see `PLAYTEST_REPORT.md` (developer sessions) |
| Existing save / Marks unlock | Not re-smoked in Step 14 automation |
| Protected movement/camera | Unchanged per `PROTECTED_SETTINGS.json` |
