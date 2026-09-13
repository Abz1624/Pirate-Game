# Rogue Pirates — Cursor implementation handoff

13 September 2026. Start document for staged development, not a claim that the existing project is unimplemented or release-ready.

## Read first

1. [Readiness audit](Rogue_Pirates_Readiness_Audit.md): unresolved gates and owner decisions.
2. [Main GDD](Rogue_Pirates_GDD_v3_1.md): product commitments, modes, geometry and scope.
3. [Combat content](Rogue_Pirates_Combat_Content_v3_1.md): exact prototype combat rules and content.
4. [Economy systems](Rogue_Pirates_Economy_Systems_v3_1.md): progression, ownership, rewards and transactions.
5. [Captains/mastery](Rogue_Pirates_Captains_and_Mastery.md) and [Crews/events/skins](Rogue_Pirates_Crews_Events_Skins.md).
6. [Technical design](Rogue_Pirates_Technical_Design.md), [milestone backlog](Rogue_Pirates_Implementation_Backlog.md) and [asset checklist](Rogue_Pirates_Asset_Checklist.md).

Use [Megabonk review](Rogue_Pirates_Megabonk_Design_Review.md) for rationale/source conflicts and [narrative/revenue proposal](Rogue_Pirates_Narrative_Retention_Production_Proposal.md) for explicitly proposed later work. Do not implement external reference-game rules instead of Rogue Pirates rules. v3.0/v2.1 are historical. All numerical balance remains prototype data.

Authority: explicit owner decisions first; main for product, combat for combat, economy for balances/transactions, Crew extension for events/skins, Captain extension for passives and the selected three mastery presets. Report unresolved conflicts before implementing their dependent behavior. Engineering details may be proposed and tested without asking the owner to design schemas.

## Non-negotiable behavior

- Unity 3D; native Android/iOS plus desktop/mobile browser targets. Test mobile web early. Other named channels use approved adapters; simultaneous browser/mobile release does not require building every feature in milestone one.
- Level 0 starts, movement and auto-attacks, randomized builds. No reliable in-run exchange or guaranteed perfect build.
- Ten-minute **stage** soft timer, distinct Drowned Pursuit overtime enemies. Boss success does not start overtime early.
- Boss defeat secures the objective; keep fighting until death. No Return Home, extraction, paid revive or auto-finish on boss victory. Intermediate portals carry the same build; final boss opens no exit.
- Finite stage-start exploration chests/POIs. One-time boss/encounter rewards and enemy drops remain their own defined sources.
- Persistent concentric home; per-raid section permutations preserve attached decorations/defenses. Ledger upgrades require no functional building placement.
- Run Gold is separate from treasury Gold. Raid theft commits once and protection begins at authoritative settlement; later farming does not steal again.
- One aggregate per specialized Item type; transfer total progress, derive rank, preserve surplus. Small permanent improvement and no duplicate drop weighting. Baseline pool and repeatable reacquisition persist.
- One global weekly best-single-run Voyage kill board across tiers/biomes/account power; additional daily/weekly Crew/event views. No raid/home scores on these boards.
- Crew changes locked until active event ends; queued departures processed before the next event lock. Manual/external prizes, with verified manifests and delivery records.
- Initial mastery catalogue is Fast Flag, Lightly Armed and Steady Hands only. No old four-preset bonus schedule. Multi-stage home challenges are deferred.
- Exclude `references/**`, including derivatives copied elsewhere unless independently approved. Existing Unity assets are candidates, not a source of automatically approved production art.

## First task to give Cursor

> Read this handoff and its M0/M1 references. Inspect applicable AGENTS.md instructions, repository status and the existing Unity project before editing. Identify implemented, partial, missing and conflicting systems with file evidence; preserve unrelated work and existing assets. Work only on M0/M1: a deterministic movement/combat foundation and a small readable test scene. Start by proposing and recording the numeric/RNG contract, then implement one bounded step and run its meaningful checks. Use primitives where art is unapproved; do not import references/. Do not build commerce, live rewards or all launch content now. Report what changed, tests run, remaining failures and the next milestone gate. Do not deploy, purchase services or issue prizes.

An implementation task authorizes code inspection at that time; the planning audit has not assessed the current architecture or implementation. Reuse compatible code after inspecting it. Do not replace the project merely because this handoff describes logical assemblies.

## M0 — Prove the expensive assumptions first

References: technical §§2–4, audit A03/A04, main §4, combat §§1–3/9.

- Inventory Unity/editor/package versions and target support; choose a supported pinned version based on actual project constraints.
- Record camera/orientation/input test assumptions. Use landscape as a reversible proposal, not a settled owner requirement.
- Specify integer scales, rounding, tick durations, movement normalization, collision ordering, RNG streams and exponential/cap evaluation in a numeric contract.
- Build the same simulation fixture for Unity native, WebAssembly and a C# server runner. Compare serialized events/state, not screenshots or approximate float values.
- Profile one water/terrain scene plus pooled swarm/projectiles on representative native and browser devices. Start from technical design's provisional 30 FPS mobile/20 MB initial download/256 MB heap targets; report actual process/graphics memory too.
- Measure verifier cost per active simulation minute; record the workload. No paid hosting is needed to prove local replay.

Exit evidence: pinned toolchain, device/build results, matching fixtures, measured limits and an explicit plan for any failing platform. Do not begin reward-bearing accounts on an unproven client-trust fallback.

## M1 — Small combat foundation

References: combat §§1–4.1/11.2, technical §§3/8, main §13.2.

Implement directional steering, deterministic planar collision, Deck Cannon, one standard Shark, XP/level offers, a minimal HUD and event-driven presentation. Use one test boss entity to exercise boss-success-plus-death ordering before commissioning boss art.

Logical modules: Simulation; Content; Presentation; AccountClient; PlatformAdapters; EditorValidation. Keep shared simulation independent of Unity objects/physics and wall-clock rendering. Pool presentation objects; map effects from simulation events, never use animation callbacks to determine damage.

Implement pause reasons as a set: choice/manual/focus/suspend/connectivity/loading. Closing one reason cannot clear the others. Persist simulation time separately from wall time. M1 local fake account/reward adapters must be explicitly development-only and incapable of calling production grants.

Acceptance fixtures:

- Same input at 30/60/120 render FPS yields identical simulation outcomes.
- Heal and fatal hit on one tick follow combat §11.2; two fatal hits consume Last Rum only once.
- Valid boss/player simultaneous death secures victory then ends the run.
- Level/POI/portal interactions cannot rescue a player already dead that tick.
- Replaying a checkpoint does not duplicate XP, unlocks or receipts.
- Rarity/capped-offer fallback respects eligible pools; no frame-rate-dependent proc counts.

Exit: responsive movement and readable damage; automated numeric/order checks pass; no live account writes. Record a short playable capture and remaining feel issues.

## M2 — Saltwind vertical slice

References: main §§3–5/8.1, combat §§4–11, Captain §§1–4, backlog M2.

Four Weapons: Deck Cannon, Broadside Battery, Powder Kegs, Flying Cutlasses. Eight baseline Logbooks and eight baseline Items. Proposed explicit four specialized slice Items: Grapeshot Pouch, Double-Fuse Cord, Serrated Scabbard, Broadside Drum; resolve stable IDs against combat definitions at export. Development presets may equip them for testing, but public unlock quests remain unchanged.

Seven standard enemy families, Barnacle King and both overtime archetypes. Rook/Brine/Flint profiles. Finite 12-chest/four-POI conquest layout using Repair Wreck, Powder Cache, Signal Buoy and Treasure Chart. Minimap/Beacon, minute-3/4/7/8 pressure beats, four temporary powerups, ten-minute overtime, boss success, survival/death and first-home preview.

Implement wireframes before polished onboarding: start → movement/choice → chest → Beacon/boss → Territory Claimed while combat continues → death → results → home/Ledger. Do not insert extraction. Basic one-stage mastery is scheduled after reliable home/account progression, not needed to prove first conquest.

Exit: playtesters understand objective and death, want to retry at least some weak builds, and can distinguish run rewards from saved resources. Record actual observations; do not claim fun from passing deterministic tests.

## Subsequent milestones and dependencies

- **M3 accounts:** technical §§4–6/9; economy §§1–7/14/18. Authentication, one run lease, sequenced input, checkpoints, pending/secured receipts, holdings, ledger and restore tests. PostgreSQL/API/worker is the recommended local prototype stack, not permission to deploy. Keep external payment rails sandboxed.
- **M4 progression/raids:** main §§3–7; combat full launch catalogue; economy §§8–13. Three biomes and Voyage tiers, home expansion/permutations, remaining Captains if retained, three mastery presets, defense compatibility and anti-collusion policy. Test concurrent settlement, storage clipping and shield races. Every reward source needs explicit mode eligibility.
- **M5 social/events:** Crew §§2–6; economy §§15–16; audit A06. Freeze membership/period/content per run, best-score aggregation, queued leave boundary, late submission/review, manual prize manifest and skin inventory. Rehearse events with fake prizes before publishing.
- **M6 distribution/commerce:** technical §2 and economy v3.2. Account-bound catalogue, Captain's Chronicle, Sponsored Cache, channel ad matrix, sandbox receipts/refunds/restoration, moderation/consent and support. No blockchain, wallet, player transfer or trading link.
- **M7 release:** backlog M7, audit A09/A10. Device/load/long-run tests, restore drill, rollout/rollback, approved art/audio, partner/store acceptance and operational owners.

The [milestone backlog](Rogue_Pirates_Implementation_Backlog.md) remains the detailed checklist. Do not mark its items complete merely because stubs compile.

## Content export contract

Author ScriptableObjects or another editor-friendly representation; export versioned shared data plus a manifest hash. Minimum fields: stable ID, schema version, content version, release phase, allowed modes, localization keys, dependencies, presentation IDs and validation cases. Run snapshots pin the manifest; persistent migrations and live-run content upgrades are different operations.

Record types: Captain, Weapon, Logbook, Item, Enemy, Boss/Attack, Status, Powerup, POI, Section/Connector, Stage/Director, Quest/Contract, MasteryPreset, LedgerTrack, RewardTable, CosmeticDefinition, EventRules and ChannelCapabilities. Do not confuse cosmetic definition/owned instance, temporary Item stack/persistent holding, or run Gold/treasury.

Validation rejects missing IDs, zero-total weights, invalid caps, incompatible traits/slots, unreachable quest prerequisites, banned reward modes, malformed geometry and unrestricted proc cycles. Expand formulas to numeric fixtures rather than duplicating them in UI labels. Clarify any remaining value ambiguity in the owning spec before exporting it.

Required state snapshots include actor IDs/HP/status and source ancestry; Weapon cooldowns; RNG streams; finite object consumption; stage clock/overtime and scheduled beats; Captain counters; selected content/ranks; pause reasons; input sequence; account lease fencing; objective receipts; Crew/event/period snapshots. Cosmetic/visual randomness must not perturb combat RNG.

## Transaction/API implementation boundary

Derive balances, kill score, chest outcomes and ownership from authenticated server commands and verified simulation. Client-submitted totals are not authority.

Commands should express intent: create/sync/resume run; collect production; start upgrade; acquire qualified content; choose contract/reforge; equip cosmetic; quote/start raid; join/queue leave Crew; enroll event; operator grant. Each needs an authenticated account, idempotency key, expected version where applicable, and explicit success/pending/conflict/expired/unavailable result.

Do not invent endpoint contracts and database constraints independently. Record request/response and failure semantics beside schema migrations. Lock affected account rows in a consistent order; enforce one active lease, one theft receipt and one prize allocation grant with database constraints. External payment confirmation and inventory transfer require durable reconciliation, not a best-effort pair of calls.

## Definition of done for each development task

Update the smallest coherent behavior and its source specification if needed; preserve unrelated changes. Include meaningful tests for simulation/transactions and a playable check for presentation. Record supported runtime results, remaining failures, content version and asset status. An editor screenshot is not proof of mobile performance; file existence is not license or import validation.

Stop only the dependent work when a gate fails and continue independent milestone tasks. Ask the owner about product tradeoffs, not routine implementation choices. Leave production payments, prizes and deployments disabled until separately authorized and validated.
