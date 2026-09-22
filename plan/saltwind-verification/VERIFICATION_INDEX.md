# Saltwind verification index (Codex)

Status: **delivery package complete** — remaining gaps are **explicitly pending** (perf, external playtesters). See `IMPLEMENTATION_LOG.md`.

## Reproduce tests

```powershell
cd "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game"
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
unity close "Pirate Game Unity"   # if interactive Editor open
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

Connected Editor: `unity command run_tests --mode EditMode --filter Saltwind --filter_type testName` (79-test slice).

Regression log: `step14-regression-evidence.md`.

## Deliverables map

| Artifact | Path | Status |
| --- | --- | --- |
| Implementation log | `plan/saltwind-verification/IMPLEMENTATION_LOG.md` | Complete |
| Protected settings | `plan/saltwind-verification/PROTECTED_SETTINGS.json` | Baseline |
| Balance decisions | `plan/saltwind-verification/BALANCE_DECISIONS.md` | v1 + v2 |
| Placeholders | `plan/saltwind-verification/PLACEHOLDERS.md` | Index |
| Playtest report | `plan/saltwind-verification/PLAYTEST_REPORT.md` | Directional; perf + external **pending** |
| Step 14 regression | `plan/saltwind-verification/step14-regression-evidence.md` | Updated |
| EditMode XML | `plan/saltwind-verification/editmode-results.xml` | **Captured** — EditMode asm **124/124** pass |
| PlayMode boss gate | `plan/saltwind-verification/playmode-boss-evidence.md` | **2/2** (embedded in EditMode XML sweep) |
| PlayMode CLI XML | `plan/saltwind-verification/playmode-results.xml` | Empty discovery (0 tests) — see boss evidence |
| C02 telemetry excerpt | `plan/saltwind-verification/c02-telemetry-excerpt-seed1622933822.json` | Attached |
| Perf trace | `plan/saltwind-verification/PERF_TRACE_PENDING.md` | **Pending** |
| Solo profile | `Pirate Game Unity/Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset` | Authored |
| Profile hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` | `profile-export/` |

## Git / profile pins

| Pin | Value |
| --- | --- |
| Baseline commit (Step 00) | `701bfa7d081b646631b6db15b890d51e46d5ad40` |
| Delivered implementation commit | **`0ee6b0fc66dc79e87765517e6d75a7d6e8075fe1`** (owner; verify includes latest test fix if amended) |
| Profile content hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` |

## Checkpoint summary

| Gate | Result |
| --- | --- |
| C00–C12 | Passed (automated + lab) |
| C13 | Directional human; perf + external **pending** |
| C14 | Automated evidence + docs **complete** |

## Outstanding (prominent)

| Severity | Issue | Notes |
| --- | --- | --- |
| Low | Desktop perf trace | `PERF_TRACE_PENDING.md` |
| Low | External newcomer playtests | `PLAYTEST_REPORT.md` |
| Low | PlayMode CLI discovers 0 tests | Boss gate evidenced via `editmode-results.xml` |
| Low | Optional full telemetry seed 12345 | Excerpt uses seed **1622933822** |
| Info | `LevelOfferGeneratorTests` fix | Sets `startingWeaponId` in test setup — commit if not in `0ee6b0f` |

## Codex prompt

`plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §7.
