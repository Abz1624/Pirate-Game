# 08 — Accounts, Backend, Data and APIs

## Services and identity

ASP.NET Core modular monolith + replay worker; PostgreSQL; Railway; Cloudflare CDN/R2. Environments: dev/staging/prod with separate secrets/economies. Guest identity warns about recovery. Direct web uses email magic link; mobile Apple/Google; Steam ticket; portals approved identity. Link only after proving both identities; never merge two established economies automatically.

## Run verification

Server creates run ID, seed, manifest, frozen progression/affiliation and fenced one-run lease. Client sends sequenced inputs/choices/lifecycle every two active seconds and boundaries. Worker replays from last checkpoint. Client hashes detect divergence but prove nothing alone. Boss receipt settles immediately; death produces final rewards/score. Lost connection freezes at safe tick. Another-device takeover revokes old lease. Client never submits trusted kills, RNG or balances.

## Data records

Accounts/identities/sessions/recovery; currency accounts/ledger; Territories/sections/defenses/jobs; Item holdings/cosmetic instances/loadouts; runs/leases/input batches/checkpoints/receipts; raid quotes/settlements/shields; Crews/membership history/locks/queued leaves; events/periods/scores/standings/allocations; ads/purchases/LiveOps/operator audit/outbox.

Use UUID/ULID stable IDs, UTC timestamps, integer smallest currency units, nonnegative constraints, foreign keys and unique source keys. Lock accounts in ascending ID order for two-account raid transactions. API results are `success|pending|conflict|expired|unavailable|validation_failed`; retry the same idempotency key.

## API groups

`/auth`, `/accounts`, `/content`, `/runs`, `/ledger`, `/collection`, `/territory`, `/defenses`, `/raids`, `/crews`, `/events`, `/leaderboards`, `/cosmetics`, `/catalogue`, `/purchases`, `/ads`, `/liveops`, `/operator`. Version public routes and error codes. Rate-limit auth, target refresh, run creation, ad claims and operator mutations separately.

## Content schema

All definitions include ID/version/localization/modes/tags/unlocks/behavior/numbers/assets/tests. Export deterministic JSON and manifest hash. Reject missing references, duplicate IDs, invalid weights, unreachable unlocks, incompatible slots and unbounded effects. Keep old manifests for resumable/reviewed runs; migrate persistent accounts separately.

## Security and recovery

Short-lived access tokens, revocable refresh sessions, platform secure storage, least-privilege operator roles, private DB, encrypted transport/backups, audit log and secret rotation. Daily snapshot, pre-migration backup, off-host encrypted copy and monthly restore drill. Use expand/migrate/contract schema rollout. Retain ordinary replays 30 days and prize/disputed 90 days pending privacy review.

