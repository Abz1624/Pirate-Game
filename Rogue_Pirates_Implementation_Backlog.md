# Rogue Pirates — Milestone Backlog

13 September 2026 • planning only; all tasks below are unimplemented unless separately verified later

Use with [technical design](Rogue_Pirates_Technical_Design.md) and the current GDD. Milestones describe internal development order, not separate browser/mobile release dates. Owner requested first-run wireframes as a later TODO. Do not block the initial movement sandbox on them; complete them before polished onboarding integration.

Start development with the [Cursor handoff](Rogue_Pirates_Cursor_Implementation.md); use the [readiness audit](Rogue_Pirates_Readiness_Audit.md) for unresolved gates and the [asset checklist](Rogue_Pirates_Asset_Checklist.md) for production. All `references/` assets are excluded by owner instruction. The selected initial home catalogue is the newer three mastery presets. Score deadlines use receipt of a complete submission, with verification allowed afterward.

## M0 — Feasibility and foundations

- [ ] Pin Unity supported LTS patch and packages after testing native Android/iOS and desktop/mobile Web builds.
- [ ] Validate one representative 3D water scene, pooled swarm and projectile stress case on mobile Safari and Android Chrome as well as native builds.
- [ ] Establish pure-C# simulation boundaries; prove deterministic movement/hit/RNG/event parity in Unity native, WebAssembly and server replay.
- [ ] Record numeric precision/rounding/RNG contract and refine the existing top-down camera/40-by-30-meter baseline for orientation, aspect ratios and touch controls.
- [ ] Benchmark verification CPU, peak memory and initial downloads; replace assumed infrastructure figures with measurements.
- [ ] Resolve Steam blockchain/ads compatibility direction and portal account/monetization requirements before claiming channel parity.
- [ ] Confirm desktop OS scope, minimum reference devices, target frame rate and build-signing resources.

Exit: test builds run on all target classes; a replay fixture matches exactly; identified channel conflicts have an explicit decision or remain clearly gated. No live economy required.

## M1 — First combat sandbox

- [ ] Movement, camera, deterministic collision, input adapters and default ship appearance.
- [ ] Deck Cannon, representative enemy, XP/level choice and bounded damage/proc pipeline.
- [ ] Atomic combat tick ordering, lethal-hit prevention, simultaneous boss/player death fixture.
- [ ] Pause reason stack, focus/suspend behavior and consistent active/wall-clock tracking.
- [ ] Data authoring/export/validation pipeline and placeholder VFX/audio.
- [ ] Revised Logbook increments, capped-offer filtering and explicit proc source/receiver whitelist from the Megabonk polish pass; preserve prior values only as a named comparison profile.

Exit: combat is readable and responsive; frame rate does not alter outcomes; closing one pause layer cannot resume beneath another.

## M2 — Saltwind playable slice

- [ ] Four slice Weapons, eight baseline Logbooks, eight starter plus four selected specialized Items, standard enemy roster and Barnacle King.
- [ ] Finite chests/POIs with themed shrine interactions and complete allocation rules.
- [ ] Beacon/minimap, ten-minute soft timer, distinct overtime enemy family and cap-safe spawn replacement.
- [ ] Boss victory secured followed by survival until death and understandable results.
- [ ] Rook/Brine/Flint Captain profiles, minute-3/4/7/8 pressure beats and four temporary powerups with stage limits.
- [ ] First conquest and local home preview; temporary data is clearly separated from public accounts.
- [ ] **TODO — first-run wireframes:** start, choice, chest, Beacon, boss victory, continuing survival, death, results and first home/upgrade. Owner explicitly deferred these; finish before final onboarding UI.

Exit: players understand the objective and can describe why they died; weak-build retry intent and post-boss survival are tested. No requirement to finish all cosmetics/story content first.

## M3 — Accounts and reliable persistence

- [ ] API/database migrations, verified platform identity adapters, guest recovery and safe account linking.
- [ ] One active run lease, ordered input upload, incremental verification, checkpoint/resume and durable reward receipts.
- [ ] Ledger, production/storage, Item holding arithmetic, contracts and inventory queries.
- [ ] Background/disconnect/second-device and duplicate-request tests; backup/restore exercise.

Exit: same account resumes safely across approved platforms; forged client balances are rejected; no repeated grant after reconnect. Replay verifier passes before public reward-bearing runs.

## M4 — Full run progression and Territory systems

- [ ] Remaining launch Weapons/Logbooks/Items, three biomes/bosses and multi-stage carryover.
- [ ] Resolve per-quest mode eligibility, trait/boss compatibility, Kraken stationary-versus-pursuit exception, home-preset biome/boss records and former-Crew raid exclusion duration before their dependent systems are accepted.
- [ ] Remaining three Captain profiles if initial tests support them; three home mastery presets and personal overtime milestones.
- [ ] Persistent concentric home expansion, section-bound decorations and valid raid permutations.
- [ ] Defense authoring/self-tests; raid quotes, once-only theft/protection, concurrency and refund tests.
- [ ] Economy cohorts, overflow, upgrade affordability and repeatable reacquisition playtests.

Exit: finite exploration, overtime pressure and portal decisions work over three stages; an asynchronous raid never duplicates theft or postpones protection for survival time.

## M5 — Crews, events and collaboration inventory

- [ ] Crew identities/rosters and configurable partner capacity.
- [ ] Event-duration membership locks, leave-at-event-end queue, leadership fallback and exact boundary ordering.
- [ ] Global weekly plus Crew/community daily/weekly views, score verification, ties, late submission and finalization.
- [ ] Operator event setup, winner manifest export, manual grant/delivery tracking and retries.
- [ ] Ship & Cosmetics inventory, reusable collaboration variants, equip/list locks and provenance.
- [ ] TODO — Crew/event/inventory wireframes and first partner art approval.

Exit: run a complete simulated daily and weekly partner event; changing rosters cannot move scores; queued exit runs before the next lock; an operator cannot accidentally pay one allocation twice.

## M6 — Channel integration and commerce gates

- [ ] Complete Steam/mobile/portal adapter testing, lifecycle callbacks, signing and SDK review.
- [ ] Approved cosmetic catalogue, purchase receipts/restoration, refunds and optional marketplace sandbox.
- [ ] Resolve tradeable entitlement behavior per approved channel; preserve explicit product decisions rather than hidden workarounds.
- [ ] Ads only where supported with approved rewards/placement; no assumption that Steam accepts an ad model.
- [ ] Confirm audience, ratings, consent, licensing, support and manual prize operations for actual launch markets.

Exit: approved feature matrix and end-to-end sandbox purchases/recovery. A channel or transaction that fails approval remains gated; do not label it launch-ready.

## M7 — Launch validation

- [ ] Real device/browser performance, long-run stability, load tests and budget alerts.
- [ ] Cross-platform account/run contention, rollout/rollback and content-version resumption.
- [ ] Moderation, incident/support playbooks, analytics and prize dispute exercise.
- [ ] Final art/audio/readability pass, storefront assets and narrative beats as approved.
- [ ] Release checklist for simultaneous browser/mobile availability; partner/store launch timing depends on their acceptance.

Exit: measurable performance/reliability criteria met, outstanding risks assigned, operating cost model updated from tested workloads.

## Still requires an owner or technical decision

- Owner-selected non-blockchain builds and optional permitted trading links: confirm acceptance and exact cross-channel entitlement behavior before promising parity.
- Hosting/database recommendation approval before deployment; no need to purchase during planning.
- Minimum devices and partner Crew sizes informed by M0 tests and partner needs.
- Narrative proposal, launch cosmetic catalogue and audience age range.

No calendar estimates are promised until team capacity, art availability and the M0 verification benchmark are known. Wireframes remain explicit TODOs; structured content export is an engineering/design task already in M1, not an extra document the owner must supply.
