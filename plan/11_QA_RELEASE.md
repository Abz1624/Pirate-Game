# 11 — QA, Analytics, Operations and Release

## Test pyramid

- Unit: formulas, RNG, damage, statuses, offers, holdings, economy, time and ranking.
- Property: map reachability, no negative balances, idempotency, proc termination and valid content weights.
- Replay: golden native/WebAssembly/server parity and old-manifest resumption.
- Integration: account linking, run lease, checkpoints, raid transaction, Crew boundary, ad/purchase receipt and event finalization.
- PlayMode: complete tutorial, boss-to-death, portal carryover, modal priority and presentation reset.
- Device: frame/memory/thermal/download/input/safe-area/browser lifecycle.
- Security: forged commands, replay tampering, auth/rate limits, operator permissions, purchase/ad spoofing and economy abuse.
- Load: API/database/verifier/event close at 2× forecast peak.

## Analytics

Events cover boot/auth/tutorial steps; run start/stage/boss/portal/death/retry; offers/build/damage/death source; POIs/chests; currency source/sink/overflow/theft; upgrades/contracts/holdings; ads/IAP; Crew/event/standings; performance/crash/network/replay divergence. Include manifest, platform, device tier, acquisition, progression and payer cohort. Exclude unnecessary sensitive payloads.

Dashboards: onboarding funnel; D1/D7/D30; runs/retry/session; build diversity; boss/portal/overtime; economy P10/P50/P90; raids; ads/IAP; Crews/events; verifier/ledger integrity; crashes/performance; support/refunds. Every experiment predeclares hypothesis, primary/guardrail metrics, allocation, duration and rollback.

## Operations

Dev/staging/prod; CI build/test/content validation; signed artifacts; feature flags and kill switches for raids, boards, ads, purchases and events. Alerts for crash spikes, API errors, verifier lag/divergence, ledger imbalance, duplicate keys, stale leases, failed callbacks/grants, abnormal generation and spend. Assign incident commander, gameplay, economy, backend, release, community/support and prize owners. Maintain recovery, refund, entitlement, moderation, dispute and partner-prize runbooks.

## Release gate

All named platforms pass functionality, privacy, rating, store, payment/ad and performance review. Complete an accelerated staging season, restore drill, rollback drill and launch rehearsal. Freeze client/server/schema/content versions. Publish known eligibility and support terms. Monitor by severity during rollout; never hot-change an active run's rules.

