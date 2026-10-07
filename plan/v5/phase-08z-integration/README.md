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

## After the review (7-8 Oct)

| Item | Commit |
|---|---|
| Review fixes (goldens, shipped-settings scenarios, new scenario tests, server harness, balance version, session cap) | `afad2196` |
| D20: exact XP payout, Ironclad pooled, session downloads switch, equal level-1 DPS 14.78 | `7bfb9403` |
| XP drops halved (owner) | `a6f61b46` |
| Health bars drawn again; the Beacon's whirlpool stays visible | `5711b244` |
| D21: sparkles, Knockback card, Harpoon tar slow, miniboss slows, one Luck scale, Admiral, Squid Ink Pots, texture cap | `b28a8c3d` |
| Battle Signal and Full Sail powerups; splash logo off | `0ab026cc` |
| Q20 rank milestones for the nine new weapons | `7f84c1da` |
| Close fixes: kill credit for every weapon, Ramming Prow save alias, real Luck and Ironclad assertions, echo validator | M8z close commit |

## Gate

- Tests on the closing code: EditMode 561/561, PlayMode 88/88 (`plan/playtest/tests/*-m8z-close.json`); server 102/102.
- Playable check: desktop WebGL builds served locally; the owner's five recorded voyages in `plan/playtest/sessions/`.
- Independent review: pass with fixes (7 Oct), fixes done; final check of the later changes (8 Oct): no blockers.
- Owner playtest: done (8 Oct, session `session-20261008-001147-rook.json`).
- Carried into M9: the simulator refit on the recorded sessions (C9.1).

## Decisions

D18, D19, D20, D21 (plan section 2). No owner decisions open for M8z.
