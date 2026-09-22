# Saltwind verification index (Codex)

Status: **implementation in progress** — see `IMPLEMENTATION_LOG.md` for checkpoint gates.

## Reproduce baseline tests

```powershell
cd "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game"
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
# Close interactive Unity Editor first, or use connected Editor test runner:
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

Connected Editor (while project open):

- Unity MCP `run_tests` with `mode: editor`

## Deliverables map

| Artifact | Path | Status |
| --- | --- | --- |
| Implementation log | `plan/saltwind-verification/IMPLEMENTATION_LOG.md` | Active |
| Protected settings | `plan/saltwind-verification/PROTECTED_SETTINGS.json` | Baseline captured |
| Balance decisions | `plan/saltwind-verification/BALANCE_DECISIONS.md` | Pending |
| Placeholders | `plan/saltwind-verification/PLACEHOLDERS.md` | Pending |
| Playtest report | `plan/saltwind-verification/PLAYTEST_REPORT.md` | Pending (human gate) |
| EditMode XML | `plan/saltwind-verification/editmode-results.xml` | Pending Editor-closed run |
| Solo profile asset | `Pirate Game Unity/Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset` | Created |
| Profile content hash | `ab95c6558aa4f6346753d5f15c9e5913113d08dc3792f4f67411a772c078abf8` (see `profile-export/`) |

## Final commit / profile hash

- Baseline commit: `701bfa7d081b646631b6db15b890d51e46d5ad40`
- Delivered implementation commit: **pending** (work not committed)
- Profile content hash: **pending** export capture after tests pass

## Outstanding issues (initial)

| Severity | Issue | Reproduction |
| --- | --- | --- |
| Medium | PlayMode suite not captured in baseline evidence | MCP async `run_tests` returned 0 tests |
| Low | Pre-existing POI placer test expects 12 chests | Run `V3StagePoiPlacer_PlacesConquestPois` |
| Low | PlayMode smoke tests run under EditMode sweep | Run PlayMode assembly with play mode enabled |

## Codex prompt

Use the handoff prompt in `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §7 when implementation checkpoints C00–C14 are complete or explicitly marked pending.
