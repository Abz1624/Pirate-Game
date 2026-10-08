# M9a — Server foundation and run checks (evidence)

Branch `v5-live-game`. Decisions D22, D23, D24, D25.

## Items

| Item | Status | Commit / evidence |
|---|---|---|
| C9a.0 .NET 10 and EF Core migrations | done, deployed 8 Oct | `3eef8749`; live Postgres recognised as the EnsureCreated schema and baselined, data kept (no backup possible, owner's call) |
| C9a.1 simulator refit | done on today's balance (D25) | `29e8dc12`; `plan/playtest/montecarlo/report.html`; older builds miss on early kills |
| I9a.1 run log | done | `3a6997b2`; `rp-runlog` v1, about 9-12 KB for 15-20 min, structural checks |
| I9a.2 seeding | done | `3a6997b2`; source-scan test, same seed and inputs give the same per-minute log |
| I9a.3 run checks, shadow mode | done | `60922664`; rules `server/RoguePirates.Api/Plausibility/plausibility-rules.json` (v1); 5 of 10,000 strongest-personality runs flagged (0.05%, budget 0.1%) |
| Review fixes | done | `03a72b9f`; bad logs accepted as `log_rejected` (D25), stub endpoints retired, 96 KB body limit, run log closed by the runtime, stricter seeding scan, shared edge-case fixture, damage-on voyage, 8-week log retention |

## Gate

- Tests on the closing code: server 153/153 (`plan/playtest/tests/server-m9a.trx`), EditMode 582/582
  (`editmode-m9a-final.json`), PlayMode 92/92 (`playmode-m9a-final.json`).
- Deploy (8 Oct): migrations AddRunLogs, AddRunVerdicts, AddRunLogRejection applied on Railway; the weekly board serves
  the existing entries, marked unchecked (no log, older builds).
- Independent review: pass with fixes (8 Oct), no blockers; fixes done.
- Playable check: desktop WebGL build with the run log; a voyage uploads and shows "checked" on the weekly board.
- Owner playtest: pending.

## Notes

- Any balance change means regenerating the rules (`plan/playtest/montecarlo/plausibility.mjs`, see the server README);
  `V5PlausibilityRulesTests` fails until then.
- The enemy leash draws now follow the run seed (they used a fixed seed), so leash behaviour for a given seed changed by design.
- Before enforcement: a dispute endpoint, a set of the owner's voyages on the current build passing the shipped rules
  (D25), and the decision on runs with no log (D25 d).
