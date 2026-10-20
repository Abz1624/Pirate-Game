# M9j live checks (20 Oct 2026, Railway, devnet)

Deploys with `server/deploy.ps1`: `cf59109b` (step 1) at about 00:18 UTC and the review fixes at about 00:30 UTC
(migration `AddServerResultChecks`: four nullable columns, one table, one index; nothing overwritten or dropped).

| Check | Result |
|---|---|
| `GET /health` (both deploys) | `{"ok":true,"db":"postgres"}` |
| `GET /api/switches` | `relayHullEnforce:false`, `relayHullMargin:0.25`, `raidPairLimitPerDay:0`, `raidRecordOnePerDay:false`; the M9h switches unchanged (all on) |
| `POST /api/race/daily/start` with a guest token | 200, `starts:1`, keyed to the 20 Oct race; anonymous: 401 |
| `POST /api/race/daily/entry` 2 s after the start, time 120 s | accepted, `raceCheck:"too_fast"`, `serverSeconds:2.2`, `checked:false` (the test entry "M9jCheck" stays on the 20 Oct board; no value) |
| `POST /api/raids/{id}/result` with a guest token | 403 `wallet_required` (the route is live) |
| An instant guardian claim on a real raid | not run live: it needs a wallet profile, an island and 25 RUM on the live database; covered by `M9jServerResultTests.Raid_InstantGuardianClaim_IsRefused_...` and the boundary tests |

No SOL moved: no wager, payout or mint was made by these checks.
