# Rogue Pirates — Unity Technical Design

13 September 2026 • planning specification, no implementation or deployment performed

## 1. Decisions and status

Owner-confirmed: Unity with 3D assets; Android and iOS native applications; desktop and mobile browsers; Steam, CrazyGames, Poki and other eligible free-to-play channels as distribution targets; account-based persistence in our database. Browser and mobile launch together. First-run wireframes are explicitly deferred to a backlog task.

Owner decision on 14 September 2026 removes player trading, blockchain inventory, wallet connection and the external trading hub from every build. Channel profiles still control ads, IAP providers, login and entitlement restoration.

Recommended, not yet owner-selected: Unity 6 supported LTS patch pinned after compatibility testing, URP with a restrained mobile rendering configuration, a shared pure-C# simulation library, ASP.NET Core API and verification worker in portable containers, PostgreSQL as the authoritative database, Railway for initial application compute, and Cloudflare R2/CDN for assets and private replay storage. Hosting plans are selected only when deploying; this document purchases nothing.

## 2. Distribution gates

One gameplay project does not imply identical account, purchase or advertising capabilities on every channel. Maintain explicit build profiles and server-side channel capabilities.

- **Direct browser:** Unity Web build, HTTPS, touch and desktop input, correctly configured compression/MIME/CORS, browser lifecycle handling, progressive content loading. Treat mobile Safari as an early test target, not a final port. Unity 6 documents mobile browser support, but that does not establish that our scenes meet memory/performance budgets. [Unity browser compatibility](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-browsercompatibility.html)
- **Android/iOS:** native builds, safe areas, app suspension/resumption, platform account and purchase adapters, restoration, consent and release signing. iOS builds require the appropriate Apple toolchain/signing environment. Exact minimum OS/device versions remain performance-test outputs.
- **Steam:** proposed first native desktop target is Windows; macOS/Linux/Steam Deck certification are not silently included. Omit ads, blockchain, wallets and external trading links. Use Steam-compatible paid cosmetic entitlements only after integration review. [Steam onboarding](https://partner.steamgames.com/doc/gettingstarted/onboarding)
- **CrazyGames:** use its Unity SDK and verified platform identity mapped to our internal account. Permit guest play and follow the platform's login/linking flow instead of imposing our email sign-in screen. Its documentation allows a game's own backend with the User module. [Account requirements](https://docs.crazygames.com/requirements/account-integration/)
- **Poki:** use its official Unity integration, loading/gameplay/ad lifecycle callbacks and approval process. Account integration, external resource access, purchases and prizes require channel-specific confirmation. The SDK's existence is not game acceptance. [Poki Unity SDK](https://developers.poki.com/guide/sdk-unity)
- **Other portals:** onboard individually through the same adapter interfaces; no blanket compatibility promise.

The v3.2 economy specification records the current ad and IAP design. All gameplay Items and cosmetics are ordinary account-bound database entitlements.

Model capability fields explicitly: advertisements, ad placement types, purchase provider, purchase catalogue, account login and entitlement restoration. The server enforces channel policy as well as the client.

## 3. Unity and simulation architecture

Use one project with logical assemblies: Simulation, Content, Presentation, AccountClient, PlatformAdapters, and EditorValidation. Avoid introducing a separate microservice or Unity scene for every system.

- **Simulation:** fixed 30 Hz, stable integer entity IDs, explicit RNG streams, deterministic event ordering, no dependency on Unity scene objects, animation callbacks, real-time frame delta or platform physics results.
- **Presentation:** 3D models, interpolation, animation, audio, HUD and VFX observe simulation events. Visual quality cannot change hits, enemies, pickups, detection or scoring.
- **Collision/navigation:** gameplay takes place on a planar water map with height used for presentation. Use deterministic circle/segment collision, a spatial grid and stable path tie-breaking for verified gameplay. Unity physics may support cosmetic debris only. Prototype arithmetic as integer/fixed-point positions and combat units; define rounding/overflow in the content schema.
- **Content:** ScriptableObjects are an authoring interface, exported into versioned data usable by both Unity and the server verifier. No balancing formulas exist only inside a prefab or Animator.
- **Platform adapters:** identity, lifecycle, input, asset hosting, commerce, ads, achievements and capability discovery. Missing capabilities produce supported UI states rather than broken purchase buttons.
- **Rendering:** pooled enemies/projectiles/pickups, shared materials, baked/static lighting where possible, limited transparency/shadows, low/high VFX tiers. Start with GameObjects and explicit pooling; adopt more complex entity frameworks only if profiling supports it.

Initial engineering targets, not published compatibility claims: stable 30 FPS on the selected mobile reference devices; 60 FPS where available; first playable download target <=20 MB compressed; initial WebAssembly heap target <=256 MB with measured headroom for browser/process/graphics memory. These limits may need revision after one real scene is profiled. A nominal heap size is not total browser RAM. First test the existing 180 ordinary/12 elite/one boss cap with dense projectile effects on mobile web.

## 4. Server authority and run verification

Recommend local responsive play with server-verified deterministic simulation rather than paying for a continuously connected Unity renderless instance per player. This is an engineering hypothesis to validate early, not an anti-cheat guarantee.

1. Server creates run ID, seed, content version, frozen Item ownership/ranks, cosmetics, event membership and a single-account run lease.
2. Client sends sequenced input batches, choices, interaction requests and lifecycle events. Proposed cadence: every two active seconds and at each boss/portal/death boundary. Do not send trusted kill counts or a trusted new balance.
3. A C# worker replays inputs from its last validated checkpoint using the same simulation library. It advances incrementally and commits checkpoints/receipts. Client state hashes detect divergence but are not proof by themselves.
4. A validated boss event triggers the existing once-only objective/theft transaction. Client shows a pending sync indicator until acknowledgement; it must not spend unverified secured loot. Later survival does not keep the defender reserved.
5. On death, finish verification, derive kills/rewards and publish an eligible score once. A delayed verifier gives a pending result, not a fabricated confirmed prize.

Server checks include content version, legal input/choice ranges, sequence continuity, active-time progression versus server-observed wall time, one run lease, RNG and receipt consistency. Replay alone cannot prove that inputs were human or prevent automation/tool-assisted play. Valuable events require input-pacing checks, anomaly review and a correction process in addition to replay validity.

Cross-platform replay parity is a P0 spike: run the same fixtures through native, WebAssembly and server builds. Do not use approximately matching floating-point trajectories as sufficient verification. If parity/verification cost fails, revisit live server-authoritative simulation before real reward-bearing play; do not quietly fall back to trusting clients.

No gameplay network traffic at every rendered frame. Retain input streams and checkpoints for disputes under a defined retention policy; suggested starting retention is 30 days for ordinary verified runs and 90 days for prize-winning/disputed runs, subject to storage/privacy review. Keep minimal final ledger records separately.

## 5. API, database and transaction model

Use a modular monolith plus a separately scalable worker. API handles authentication, profile/home/inventory, run creation/sync, Ledger commands, contracts, raids, Crew membership, events and administration. Use HTTPS requests first; add server push only when needed. An outbox/jobs table is sufficient initially; Redis/Kafka are not day-one requirements.

Recommend PostgreSQL because ownership, balances, membership locks, raid theft and reward grants span related records and need atomic constraints. Flexible JSONB content/snapshot payloads coexist with relational ownership records. MongoDB can implement transactions too; it is not disqualified, but one giant player document would make cross-player transfers and event queries awkward. Avoid maintaining both databases initially.

Core schema groups:

- Accounts, external identities, sessions, guest-link intents and recovery records.
- Currency accounts and immutable ledger entries, with transaction/source IDs and nonnegative balance constraints.
- Territories, section instances, defenses, upgrade jobs and production timestamps.
- Item holdings with progress/version; cosmetic instances with owner/status; loadouts and reservations.
- Runs, run leases, input batch references, validated checkpoints and reward receipts.
- Raid quotes, theft settlements, protection/loss events and compensation receipts.
- Crews, membership history, event membership locks, queued leaves and leadership state.
- Events, prize periods, verified score references, standings versions, reward allocations and external delivery references.
- Ad reward receipts, purchase validation/restoration/refunds, LiveOps state, operator audit trail and outbox jobs.

Every mutation has an authenticated actor, idempotency key and expected version where state can race. Lock affected records in consistent order; unique constraints enforce one boss theft, one allocation grant, one ad claim index and one active run. A raid cannot partially update two players. Store payment money as integers in the provider's smallest units; never binary floating point.

API responses distinguish success, pending, conflict, expired, unavailable and validation failure. Retry the same logical request ID after uncertainty. A new retry ID is not a substitute for reconciliation. The database is private; game clients never receive database credentials or write directly to account records.

## 6. Accounts, saves and recovery

Internal account IDs remain stable across platforms. Map verified provider subject IDs, not mutable email/display names, to that identity. Direct builds can offer platform-appropriate sign-in; portals use their approved identity flow. Guest play gets a scoped guest identity and a clear recovery limitation. No wallet identity is used.

Account linking requires proof of control of both identities and an explicit conflict screen. Do not automatically add two existing economies together, which could duplicate first-claim rewards. Proposed default: select the destination profile and escalate conflicting established holdings to a reviewed migration; no silent overwrite. Cross-channel linking must respect platform rules.

The server is canonical for progression and inventory. Local cache accelerates loading and can hold unacknowledged input batches, but cannot restore spent assets. Save verified checkpoints at milestones and every ten active seconds; flush after safe pause boundaries when the environment permits. Mobile termination/browser unload is not guaranteed to deliver a final network request, so never rely on it for the only save.

First reward-bearing version requires a connection at run start and milestone settlement. On lost connectivity, freeze at the last locally completed tick and attempt reconnect; recovery resumes from the server's acknowledged checkpoint plus a validated buffered input suffix. Unsupported or missing suffix rolls back only unacknowledged simulation, never applies a receipt twice. Offline free practice can be a later capability; it does not grant persistent progress, ad rewards or competition scores.

Acquire a durable account-level run lease with a fencing version. Another device can view state; taking over a suspended run revokes the old device's write lease before resume. Never merge two divergent active runs. Access tokens are short-lived, refresh sessions revocable, native secrets stored in platform secure storage; browser strategy must be tested for portal iframe/cookie restrictions.

## 7. Low-cost hosting recommendation

**14 September workload follow-up:** the [financial review](Rogue_Pirates_Economy_and_Revenue_Review.md) now derives CPU, requests, checkpoint writes, replay storage/operations, licenses and operating budgets from active players and simulation time. Use its [adjustable model](Rogue_Pirates_Financial_Model.html) for scenario budgeting. The small resource-footprint example below is not a traffic forecast. The latest Poki deal guide also makes non-exclusive recurring revenue an explicit commercial gate: [Poki deal types](https://developers.poki.com/guide/revenue-deal-types); do not assume its preferred web-exclusive revenue share can coexist with the planned other web channels.

Pricing checked 13 September 2026. These are service rates and modeled examples, not measured Rogue Pirates bills.

**Recommended initial setup:** Railway API + verification worker + PostgreSQL in one suitable region, with Cloudflare asset delivery/R2. Standard containers, SQL migrations and S3-compatible storage interfaces make migration possible. Configure backups and prove restoration; a deployed database template is not a substitute for an operations plan.

Railway lists Hobby at $5 minimum usage with $5 included, and Pro at $20 minimum with $20 included. The plan minimum is not added twice to resource usage. Its posted rates are $0.00000386/GB-second RAM, $0.00000772/vCPU-second CPU, $0.00000006/GB-second volume, and $0.05/GB service egress. [Railway pricing](https://railway.com/pricing)

Illustrative 30-day footprint across API, worker and database: 1.5 GB average RAM (~$15.01), 0.1 average vCPU (~$2.00), 5 GB volume (~$0.78), 10 GB service egress ($0.50) gives approximately $18.29 resource usage. That is about $18.29 on Hobby or at least $20 on Pro, excluding other services, taxes and extra backups. This is an assumption to test, not a prediction that 0.1 vCPU can verify the game's traffic. Extra 1 GB average RAM adds about $10/month; extra one average vCPU adds about $20/month at these rates.

Cloudflare R2 Standard includes 10 GB-month storage, 1 million Class A and 10 million Class B operations monthly; above allowances, storage is $0.015/GB-month with operation charges. Internet egress is free. Use it for content/replays, not balances. Private run data must never share a public asset policy. [R2 pricing](https://developers.cloudflare.com/r2/pricing/)

**Potential lowest-idle-cost alternative:** Workers plus Neon PostgreSQL and R2, with a separate verification service. Workers has a free tier and a paid $5/month minimum. Neon documents a free PostgreSQL allowance and usage-based paid compute. This can be cheaper for a mostly idle account API, but it changes our API runtime and still leaves C# combat verification to host. I would not add that split merely to save a few dollars before measuring workload. [Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/), [Neon free/paid overview](https://neon.com/faqs/postgres-services-free-to-production)

**MongoDB option:** Atlas Flex documents $8–$30 per 30 days; this is database cost, not backend/verifier hosting. It can be a reasonable choice for an experienced MongoDB team, but does not establish a cheaper total system or eliminate transaction design. [Atlas Flex costs](https://www.mongodb.com/docs/atlas/billing/atlas-flex-costs/)

No provider can be declared universally cheapest without active users, sessions, average run length, replay CPU and download traffic. Estimate verifier cost as runs × active seconds per run × measured CPU-seconds per simulated second. For example, 1,000 players × three 15-minute runs × 30 days is 81 million simulated seconds/month; multiply that by the benchmark, not by a guessed concurrency number. Track write frequency and replay retention too.

Scale by adding API replicas and independent worker capacity first, then tune database indexes/connection pools. Keep one authoritative write region initially; only add replicas/regions after latency/availability measurements justify complexity. Alert on spend and queue lag before granting prize scores late. Do not use an emergency spend cutoff that silently corrupts transactions.

## 8. Structured content definitions

We already have most design values. What remains is expressing them consistently and filling the few policy fields each content record needs. This is data-authoring work, not a request for the owner to hand-write JSON.

- **All records:** stable ID, schema/content version, display/localization keys, enabled modes/release phase, tags, dependencies, visual/audio references and validation cases.
- **Captain:** signature Weapon/license prerequisites, neutral-stat overrides, run-level formula/cap, passive counters, reset/expiry rules, portrait and leaderboard metadata. Captain identity is not a ship skin asset.
- **Powerup:** eligible drop sources, fixed probability/weights, per-stage spawn cap, pickup radius, ground lifetime, refresh/stack rules and effect expiry. Keep this separate from permanent reward loot tables.
- **Weapon:** targeting rule, base damage, interval, range, projectile/area behavior, hit/crit/proc rules, rank increments, milestone unlocks, caps and quest.
- **Item:** rarity, eligibility, run-stack formula/cap, permanent-rank axis, trigger conditions, cooldown, proc generation and reward source. Keep run stacks distinct from persistent progress.
- **Logbook:** stat bucket, increment/quality behavior, maximum rank, cap and unlock.
- **Enemy/boss:** stats, collision, movement state, attack sequence/timings, hit shapes, spawn profile, score/drop class, resistances and phase logic. Add the new overtime family explicitly.
- **POI:** count/weights, placement sockets, discovery, activation/charge, cost, finite uses, reward, failure/interrupt rules, allowed modes and consumed state.
- **Zone/stage:** section/connector sets, object allocation, spawn director, overtime profile, Beacon/portal validation and safe fallback.
- **Economy:** rank gates/costs/timers, storage/overflow, receipts, quest predicates, contracts and loot tables.
- **Crew/event:** eligibility and time zone, membership-lock boundaries, queued-leave ordering, scoring, ties, reward manifest and delivery states.
- **Cosmetic:** definition/instance distinction, rig/slot compatibility, art variants, provenance, transfer state and channel capabilities.

Export readable JSON plus a content manifest hash. Validate missing references, negative/invalid quantities, zero-total weights, unreachable unlocks, duplicate IDs, over-budget layouts, unbounded procs and incompatible cosmetic slots. Pin each run to a content version. Keep old versions available for resumption/verification; migrate persistent data separately from live run rules.

The Megabonk polish pass requires proc ancestry/source coefficients, whitelisted receivers, capped perk counters, scheduled pressure-beat IDs, and separate powerup RNG/budget state in the simulation/checkpoint schema. Snapshot stat-conversion inputs at the defined attack boundary, so changed Hull or spent Gold cannot retroactively change an emitted attack. Report source-level damage and capped-effect telemetry without uploading an event per VFX particle.

Example conceptual POI entry: `poi.signal_buoy`, schema 1, charge interaction, radius 4 m, charge 8 active seconds, uses 1, reward `shrine_stat_choice`, allowed modes from a named profile, and explicit depleted prefab/minimap icon. The full numerical behavior lives in the combat spec, not in duplicated UI text.

## 9. Operations and validation

Environment separation: development, staging and production with independent secrets/economies. CI produces pinned Unity build profiles and server containers, validates content, tests replay parity and transaction invariants, and archives version manifests. iOS signing/build runners and Unity licensing/build infrastructure are separate costs, not included in hosting estimates.

Monitor frame time/memory/download size, crashes, API latency/errors, verification lag/divergence, duplicate request rates, ledger imbalance, stale leases, daily/weekly event closure, failed manual grants and recovery attempts. Keep operator permissions narrower than database-admin access. Record manual prize exports and delivery references without exposing payout addresses publicly.

Backups: proposed daily snapshots plus pre-migration backups, encrypted off-host copy, monthly restore exercise; establish required recovery-point/time objectives before public paid operations. Use expand/migrate/contract schema changes so rollback does not require guessing old account state. Reward-distribution tools must support reconciliation and operator review.

Success criteria before public event prizes: a valid run produces the same events on every supported runtime, forged inputs cannot create arbitrary rewards, retries never double-grant, interruption resumes correctly, and mobile-browser performance remains acceptable during the strongest tested swarm build.
