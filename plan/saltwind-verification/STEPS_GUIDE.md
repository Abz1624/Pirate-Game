# Saltwind handoff — what to do each step

Reference: `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md`. Use checkpoints **C00–C14** as gates; update `IMPLEMENTATION_LOG.md` after each.

Legend: **You** = manual (Editor, device, playtest). **Agent** = Cursor + Unity MCP/CLI.

| Step | Goal | What to do | Who | Evidence |
| ---: | --- | --- | --- | --- |
| **00** | Baseline | Record git, protected settings, EditMode/PlayMode without bootstrap; identify solo boot route | Done | `PROTECTED_SETTINGS.json`, baseline test log |
| **01** | Solo profile | `StageBalanceProfile` asset, host on `V3SaltwindRun`, export/hash, solo-only resolver, tests | Mostly done | Menu **HighSeas → Saltwind → Export Profile Evidence (Codex)**; `StageBalanceProfileTests` |
| **02** | Spawn scheduling | Schedule state, loops, partial counts, caps, modifiers, placement validation tests | Done (auto + telemetry wired) | EditMode gates; live capture in `spawn-telemetry-note.md` |
| **03** | Encounters / boss | Encounter IDs; no empty-boss win; cache lifecycle; PlayMode false-win fix | Done (auto) | `RunEncounterTrackerTests` + `BossEncounterPlayModeTests` |
| **04** | Roster + timeline | Fill schedule assets; composition bands; elites; warnings; `enforceCompleteScheduleReferences=true` | Done | `SaltwindScheduleGateTests`; `SaltwindAmbientCompositionGateTests`; Author Step 04 menu |
| **05** | Boss + overtime | Barnacle King patterns; OT/storm from profile; safe region | Done (auto gates) | `SaltwindFinaleGateTests`; storm ring + boss director in play |
| **06** | Finite POIs | 12 chests + 5 specials at start; no gold-spawn chests; layout seeds | Done (auto) | `SaltwindPoiLayoutGateTests`; 100-seed layout |
| **07** | Six-slot build | Starter pool, XP curve, offers, evolutions, proc bounds | Done (auto) | `SaltwindProgressionGateTests` |
| **08** | Gold + powerups | No hidden gold scale (solo); chest atomic buy; 4 powerups | Done (auto) | `SaltwindEconomyGateTests` |
| **09** | Marks + saves | Migration, settlement, unlock purchase; no run-gold → Marks | Done (auto) | `SaltwindSettlementGateTests` |
| **10** | Reset + snapshot | Retry order; snapshot restore; mode isolation | Done (auto) | `SaltwindResetGateTests` |
| **11** | Results + UX | Victory vs death; Marks breakdown; telemetry | Done (auto) | `SaltwindResultsGateTests` |
| **12** | Balance Lab | 1200+ sim runs; lab/runtime parity | Done (auto + menu) | `SaltwindBalanceLabGateTests`; `balance-lab/` CSV |
| **13** | Perf + humans | Desktop trace; **real** newcomer playtests | **You** | `PLAYTEST_REPORT.md` or **pending** |
| **14** | Delivery | Full regression; `VERIFICATION_INDEX.md` complete | Agent + You verify | Codex package |

## Quick commands

**While Unity Editor is open** (this repo):

- Tests: Cursor Unity MCP `run_tests` mode `editor`, or Test Runner in Editor.
- Export profile: **HighSeas → Saltwind → Export Profile Evidence (Codex)**.

**With Editor closed** (CI-style):

```powershell
cd "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game"
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

## What Cursor can vs cannot do

| Can automate | Needs you |
| --- | --- |
| Code, tests, profile assets, scene wiring via MCP | Close Editor for CLI batch tests |
| Simulation batches (Step 12) | Human fun/readability (Step 13) |
| Editor menu export when MCP connected | Play on phone/console for mobile FPS |

## Suggested order right now

1. **Step 13** — Performance trace and human playtests (handoff C13).
2. Optional: export C02 telemetry JSON on seed **12345** (see `spawn-telemetry-note.md`).
