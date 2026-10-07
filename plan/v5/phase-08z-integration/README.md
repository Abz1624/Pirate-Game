# M8z — Playtest integration (evidence)

Branch `v5-live-game`. Start: merge `b5936c4d` (playtest rounds 1-10, tag `playtest-r10`). Decisions D18, D19.

## Items

| Item | Status | Commit / evidence |
|---|---|---|
| I8z.1 merge, deploy workflow | done | `b5936c4d`; deploys are manual (`server/deploy.ps1`, D19), CI runs server tests |
| I8z.2 architecture fixes | done | `88b21724`; rule tests `NoV4Code_UsesContentIds`, `Presenters_ReadOnlyViewsAndEvents` pass |
| I8z.3 playtest preset | done, narrowed | `88b21724`; `[UseShippedPlaytestSettings]` and `V5PlaytestPresetPlayModeTests` (4 tests). The switches stay in `DemoJuiceSettings` behind `DemoMode` (not a V4 config preset); scenarios can run them with `playtest: Shipped`. Moving them into a config preset is left for later (recorded in the plan). |
| I8z.4 Scenario Lab | done | `2c281d94`; `V4/DevTools/Scenarios/README.md`; `V5ScenarioLabPlayModeTests` 10/10 |
| I8z.5 test triage | done | `dd5dfb54`; EditMode 474/474, PlayMode 77/77 (`plan/playtest/tests/*-m8z-after.json`) |
| I8z.6 shop and economy data | done | everything unlocked (D18 P3); the shop lists each weapon once (`dd5dfb54`); overtime Rum uncapped (D18 P2) |
| I8z.7 one simulator | done | `88b21724`; JS model on a Unity export (`MonteCarloExport`), `check_export.mjs` 0 differing fields; recalibrated on three sessions. Levels and kills calibrated; survival not yet (the latest session sank at 11:33, the model in 2% of runs). |
| PvP anti-cheat (owner request) | done | server tests 102; deployed 6 Oct; cap kept until Netcode (D19) |

## Gate

- Tests: EditMode 474/474, PlayMode 77/77 (7 Oct); final run after the review fixes in `plan/playtest/tests/*-m8z-final.json`.
- Playable check: the desktop WebGL build (`Builds/Playtest-2026-10-03/`), served at http://localhost:8090; the owner's recorded voyages in `plan/playtest/sessions/`.
- Independent review (7 Oct): pass with fixes, 0 blockers, 7 major, 7 minor (see `plan/v5/status.json` reviews). Fixes in progress.
- Owner playtest: pending.

## Owner decisions open

1. XP per kill depends on the graphics quality and Reduced Effects (the review recommends fixing it in M8z, then refitting the simulator).
2. Two enemy colours changed when they moved into config (Cutthroat Skiff redder, Needle Sloop greener).
3. Ironclad into the Logbook pool now that armor applies in runs.
4. Shipped (non-playtest) web builds downloading a session file at the end of every voyage.
5. The uncommitted WebGL memory and stack-trace settings in `ProjectSettings.asset` (64 MB initial, 1024 MB max; no stack traces for warnings and logs).
