# Saltwind verification index (Codex)

Status: **delivery ready for Codex review** — checkpoints **C00–C14** passed or explicitly pending below. See `IMPLEMENTATION_LOG.md`.

## Reproduce baseline tests

```powershell
cd "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game"
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
# Close interactive Unity Editor first for full-suite XML:
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

Connected Editor (while project open):

```powershell
unity command run_tests --mode EditMode --filter Saltwind --filter_type testName
```

Step 14 slice log: `step14-regression-evidence.md` (**79/79** passed on listed filters, 2026-09-23).

## Deliverables map

| Artifact | Path | Status |
| --- | --- | --- |
| Implementation log | `plan/saltwind-verification/IMPLEMENTATION_LOG.md` | **Complete** (C00–C14) |
| Protected settings | `plan/saltwind-verification/PROTECTED_SETTINGS.json` | Baseline captured |
| Balance decisions | `plan/saltwind-verification/BALANCE_DECISIONS.md` | **v1 + v2 playtest tuning** |
| Placeholders | `plan/saltwind-verification/PLACEHOLDERS.md` | Index → `PLACEHOLDER_VISUAL_CATALOG.md` |
| Playtest report | `plan/saltwind-verification/PLAYTEST_REPORT.md` | Directional complete; perf + external **pending** |
| Step 14 regression log | `plan/saltwind-verification/step14-regression-evidence.md` | **79/79** EditMode (connected Editor) |
| EditMode XML | `plan/saltwind-verification/editmode-results.xml` | **Pending** — Editor must be closed for CLI batch |
| Solo profile asset | `Pirate Game Unity/Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset` | Created |
| Profile content hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` | `profile-export/` (Step 14 export) |
| Balance Lab CSV | `plan/saltwind-verification/balance-lab/` | From Step 12 menu (optional attach) |

## Git / profile pins

| Pin | Value |
| --- | --- |
| Baseline commit (Step 00) | `701bfa7d081b646631b6db15b890d51e46d5ad40` |
| Branch at Step 14 | `game-balance-experiment` |
| HEAD at Step 14 automation | `eddbf53efb6aa22dee759c4ce1740b62540b1d2b` (local gameplay fixes may be **uncommitted**) |
| Delivered implementation commit | **Pending** — owner git commit when ready |
| Profile content hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` |

## Checkpoint summary (C00–C14)

| Gate | Result |
| --- | --- |
| C00–C12 | **Passed** (automated + lab); see log |
| C13 | **Passed (directional)** — developer playtests; perf + newcomers **pending** |
| C14 | **Passed (automated + docs)** — regression log + index; XML / PlayMode CLI / git commit **pending** |

## Outstanding issues

| Severity | Issue | Reproduction / mitigation |
| --- | --- | --- |
| Medium | Full EditMode XML not captured | Close Editor; run `unity test ... --output editmode-results.xml` |
| Medium | PlayMode suite not run via Pipeline | Async `run_tests` returned 0 tests; run `BossEncounterPlayModeTests` in Test Runner (2 tests) |
| Low | C13 desktop perf + external panel | Documented pending in `PLAYTEST_REPORT.md` |
| Low | Optional spawn telemetry sign-off | Seed **12345** per `spawn-telemetry-note.md` |
| Low | Powder keg tuning | Requires fresh run after asset changes |

## Codex prompt

Use the handoff prompt in `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §7.

**Prominent pending gates (not hidden):** human perf trace, external newcomer playtests, full EditMode XML, PlayMode CLI evidence, git delivery commit.
