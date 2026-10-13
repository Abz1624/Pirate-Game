# V5 M9h — live-API safety (server part)

Start commit: `8c0205b8` (plan rev 18). Branch `v5-live-game`. Plan: `plan/v5/V5_PLAN.md`, M9h, "Status (rev 19)".

## Checklist

- [x] H2 answered (owner, 13 Oct: "don't switch off anything for now"), recorded as D31; switches as config flags
      defaulting on: `WAGERS_HUMAN_ENABLED`, `RAID_LOOT_ENABLED`, `ISLAND_SALE_CARRIES_RUM`, `DAILY_RACE_FIXED_SALT`;
      `GET /api/switches`; preserved in `.railway/railway.ts` (`457cadab`).
- [x] 1. Refused wager deposits (too small, another memo or none, stakes off) recorded and refunded once
      (`wager_refund`); only transfers made after the wager (`tx_too_old`) (`457cadab`, review fix).
- [x] 2. Island NFT mint: address, signature and last valid block height stored before the send; chain checked before
      any new mint (150-block margin); the sweep resolves stuck pending mints (`f63f859e`, review fix).
- [x] 3. Sessions: 30 days sliding (`SESSION_LIFETIME_DAYS`, a lowered value also shortens issued sessions),
      `POST /api/auth/sessions/revoke-all`, guest token rotated on a wallet link; migration `AddSessionExpiry` (`0c41dff7`).
- [x] 4. Treasury bounds: one claim per wallet ever, `MINT_DAILY_BUDGET`, `TREASURY_MIN_LAMPORTS` float and alarm,
      `GET /api/admin/treasury`, `GET /api/admin/report`; migration `AddMintSafety` (`f63f859e`).
- [x] 5. `GET /api/wallet/balance` rate limited (endpoint policy, per token or IP); `/api/treasury` cached 30 s; island
      and raid reads lock-free and write-free (`46603027`, review fix).
- [x] Server suite twice (258/260, then 260/262 after the review fixes; `server-tests-m9h.trx`); deployed with
      `server/deploy.ps1`; live checks (`live-check-2026-10-13.md`); payouts and mints since 12 Oct
      (`payouts-and-mints-since-12-oct.md`); README, plan, status, TODO.
- [x] Independent review (v5-reviewer) on the diff: no blockers, 5 majors, 8 minors (table below).
- [ ] M9h gate: full EditMode and PlayMode suites, owner check.

## Review (v5-reviewer, 14 Oct)

| # | Sev | Finding | Done |
|---|---|---|---|
| 1 | Major | The `AddSessionExpiry` migration overwrites `Players.TokenHash` of wallet profiles (not reversible) and went out without a fresh backup | Recorded; owner question (TODO-FOR-YOU): approve after the fact, and treat overwriting a column as data-destroying from now on |
| 2 | Major | No test-run evidence in the phase folder | `server-tests-m9h.trx` and this file |
| 3 | Major | A wager deposit took any earlier transfer (an old top-up), which was then refunded | Fixed: `notBefore` = wager created - 2 min, `400 tx_too_old`, nothing recorded; test |
| 4 | Major | The RPC limit keys on the IP from Railway's `X-Forwarded-For`, which is not documented | Partition by the bearer token's hash when sent, else IP; README notes the IP caveat; a two-network probe is still open |
| 5 | Major | `/api/wallet/balance/` (trailing slash) skipped the path compare | Fixed: an endpoint rate-limit policy; test |
| 6 | Minor | A typo in a switch keeps it on silently; reward flags parsed differently | on/off, yes/no accepted; unrecognised values logged at startup; one parser for all |
| 7 | Minor | Claims racing at the budget can pass and wait for the next day's mint | Documented in the README (kept) |
| 8 | Minor | One lagging RPC node could make a landable mint look expired | 150-block margin past the deadline; test |
| 9 | Minor | Lowering the lifetime did not shorten issued sessions | Expiry = earlier of stored and last use + lifetime; test |
| 10 | Minor | Mint reconcile, treasury alarm and raid expiry shared one try block | Separate steps |
| 11 | Minor | Demo reset deletes minted islands from the report and leaves the claim used | Owner question (TODO-FOR-YOU); kept |
| 12 | Minor | Admin view counted only open mint attempts | `islandsNotMinted` added |
| 13 | Minor | The lock-free read test holds the shared economy lock | Kept: the test host's sweep is off (`SWEEP_SECONDS` 3600) and the lock is held for three reads |
