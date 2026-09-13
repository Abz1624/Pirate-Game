# Rogue Pirates — Readiness audit

13 September 2026. Documentation and asset-file audit; no gameplay source review, Unity import/build, balance test, deployment or platform approval.

## Conclusion

There is enough design to start M0/M1 and a bounded Saltwind prototype. There is not yet enough validated evidence to call the whole release implementation-ready. The missing work is mostly specification consistency, cross-platform feasibility, content authoring, asset approval and operations—not more Weapons, currencies or retention mechanics.

Use the [Cursor handoff](Rogue_Pirates_Cursor_Implementation.md) to start in small milestones. Use the [asset checklist](Rogue_Pirates_Asset_Checklist.md) for production; unchecked means not accepted, even when a candidate file exists.

## Coverage and authority

Reviewed the current main GDD, Combat Content, Economy Systems, Captains and Mastery, Crews/Events/Skins, Technical Design, Implementation Backlog, Narrative/Retention/Production Proposal and Megabonk Design Review. The preserved v3.0 and original v2.1 Word document are history, not competing current specifications. This audit does not claim to revalidate historical source citations or inspect existing game implementation.

Owner instructions in this audit:

- Exclude **all `references/` assets**. Do not use the collection to claim art coverage or import it through a different location. A Unity copy still needs independent provenance approval.
- Use **Fast Flag, Lightly Armed and Steady Hands** as the initial home-challenge catalogue, replacing the older four presets and their rewards.
- Judge score deadlines by server receipt of the complete run, then verify timely submissions before finalizing standings. Verification queue delay must not disqualify a timely submission.

Latest explicit owner decisions take precedence. Main GDD controls product commitments; combat controls combat arithmetic; economy controls resource/transaction arithmetic; Crew extension controls social/events; Captain extension controls Captain/passive and selected mastery definitions. Technical proposals guide implementation without silently changing gameplay. Historical review sections are not open tasks merely because their original wording remains quoted.

## Findings and mitigations

### A01 — Two home-challenge catalogues and reward schedules — resolved

Main §3.4 and economy §13 previously specified four families with per-biome/tier Gold/Rum/Item rewards. Captain §4 specified three one-stage presets with small one-time rewards. Implementing both would add unintended scope and rewards.

Owner selected the newer three. Main, economy and Captain documents now agree: Tier 1, one stage, first completion once per account/preset; 30 Rum plus the named title/pennant. Replays retain ordinary home rewards. Multi-stage home challenges are deferred; multi-stage Voyages remain required. Verify the once-only grant even after changing biome or reconnecting.

### A02 — Existing asset files do not establish usable coverage — gate before production art integration

The eligible inventory covers only `Pirate Game Unity/Assets`. It contains 1,218 selected asset-related files, including package/demo content and duplicates. No production asset is certified by this audit. No standalone audio files were found among the scanned WAV/OGG/MP3/AIF/AIFF/FLAC formats. Game UI cannot be inferred from the six UXML files: those paths belong to packages/demos.

Mitigation: approve one starter-ship kit, one enemy, one terrain section, one boss danger marker and one water treatment at actual camera scale. Record source/rights, import dependencies, rig compatibility and mobile budgets before expanding the art set. Replace any excluded-reference derivatives; file relocation is not approval. See checklist for existing candidates and missing deliverables.

### A03 — Presentation baseline needs orientation/device validation — M0/M1

Main §10 already defines a top-down follow camera, approximately 40 by 30 meters of visible gameplay, WASD/arrows and a virtual joystick. The remaining details are projection, aspect-ratio/visibility policy, mobile orientation, touch interaction layout and reference devices. These affect whether a six-meter corridor and enemy telegraph are readable; the existing baseline should be refined, not discarded.

Recommended prototype assumption: landscape, the existing top-down follow camera and keyboard/touch movement, with an explicit interaction button. Test projection/zoom and letterboxing or viewport treatment using one real scene before approving an art bible; do not give wider displays an accidental unbounded sight advantage. Do not promise portrait, controller support or specific minimum devices without evidence. First-run wireframes remain deferred until M2, as requested; a functional test HUD is sufficient for M1.

Close with a short recorded camera/input decision and builds on selected iOS/Android native and mobile browser devices, plus desktop browser. Owner/art lead approves feel; engineer records measured memory/frame/download limits. Device procurement and access to iOS signing/build resources need an assigned owner.

### A04 — Shared arithmetic is an architecture proposal, not a finished replay contract — M0

Combat specifies 30 Hz, meters, fractional damage, percentage formulas and exponential overtime. Technical design recommends fixed-point arithmetic but does not define its precision, rounding modes, probability sampling, angle representation or overflow handling. Independent client/server implementations could disagree despite reading the same GDD.

Mitigation: write a simulation numeric contract before expanding combat: integer scales, duration-to-tick conversion, fixed RNG algorithm/streams, stable iteration order, normalization/trigonometry, exponent evaluation, collision tie-breaking, caps and saturating overflow. Export shared data and use the same library, not separately translated formulas. Test native, WebAssembly and server parity with identical fixtures. Exact numeric representation is an engineering task; the owner need not choose bit widths.

Close only when representative movement, collision, status, proc and overtime fixtures match exactly. If replay feasibility or cost fails, revisit architecture before public rewards; do not trust client score submissions as a shortcut.

### A05 — Content still needs explicit eligibility and compatibility fields — M1 through M4

Examples: quests describe kills/clears but lack one complete per-quest eligible-mode catalogue; home contracts are allowed while self-tests must never earn progress. Economy §11.4 offers Summon Adds to a boss catalogue that includes an Infernal Kraken with no summon attack. A generic trait button could sell a no-op or invent a new boss behavior. The three home presets also need explicit biome/boss/footprint records; naming Tier 1 alone does not select a boss on an expanded home.

Mitigation: export stable IDs and a mode/quest eligibility matrix, rewardable enemy tags, quest dependency graph, trait-to-boss/attack compatibility and UI explanation for disabled choices. Recommended default: Summon Adds only where a documented summon attack exists; Wide Volley only for compatible projectile attacks with a validated escape lane. Do not manufacture a Kraken summon attack. Reject incompatible data in the editor/API. Pin exact first-slice specialized Items instead of implementing all 28 immediately.

There is a further boss exception to resolve: combat §10 says bosses pursue beyond 30 meters, while §10.3 calls the Kraken stationary. Recommended proposal: the Kraken remains stationary during its attack sequences but can perform a clearly telegraphed reposition when the player leaves the encounter range, without healing or resetting success. Confirm that behavior before implementing this boss; do not silently create an immobile boss that can be farmed from outside its reach or an unavoidable off-screen attack.

Close with validated content records, the Kraken leash exception and unlock-reachability fixtures. This is bounded design/engineering work, not a reason to add more content.

### A06 — Verification delay and score deadlines — policy resolved; implementation due M5

Economy §14 and Crew §4 previously required late scores to settle within 24 hours after period end. A player could finish on time but miss that deadline because the verifier was backed up. Server-controlled waits being excluded from pause allowance did not resolve this deadline issue.

Owner-approved policy: require the server to receive the complete sequenced run through death by the published cutoff; verify afterward and keep affected standings provisional. Never accept a client-written death timestamp as proof of timely submission. Define the review incident/escalation procedure rather than silently excluding queued runs or paying unverified scores. Crew switching still unlocks at competition end, not review completion. Updated economy and Crew rules carry this decision.

Also specify receipt ordering at a raid boss milestone: the authoritative theft/shield transaction commits when verified, using then-current balances, in transaction order. Client animation time is not a retroactive reservation. This follows the technical pending-state model and existing live-quote rule; expose pending/actual loot clearly and test concurrent raids and verifier latency.

### A07 — Former-Crew raid protection has no precise window — before reward-bearing raids

Economy excludes current Crew members and a repeated target within 24 hours. Crew rules additionally say leaving should not immediately enable reward farming, but provide no duration or exact check. Different implementations would produce different target eligibility.

Recommended tunable prototype: exclude pairs that shared a Crew within the previous seven days, checked at raid creation using membership history, alongside the existing current-Crew/repeat-target checks. Seven days is a proposal, not an owner decision or an anti-collusion guarantee. Measure how it affects small target pools; do not silently remove protection to fill three cards. Show fewer valid targets or a retry state. Freeze the policy version in the raid quote and specify behavior for later relationship changes before M4 acceptance.

### A08 — Revenue, entitlement and retention additions are still proposals — M6 gate

Audience age range and launch markets remain open. Economy v3.2 now defines the prototype paid catalogue, ads, account-bound entitlements and removal of wallet/trading systems; real provider contracts, consent/age treatment and regional storefront configuration remain release gates.

Recommended development scope: resource-funded cosmetics and versioned economy simulation first; then sandbox account-bound purchases, Sponsored Cache receipts and live-event configuration. Keep live ads and payments disabled until provider/channel/consent review. Economy v3.2 supplies prototype bounty and pass budgets for closed testing.

Close through concrete catalogue/channel decisions and sandbox recovery/refund tests. Obtain updated official provider guidance when integrating; this audit does not reissue legal or platform conclusions from earlier citations.

### A09 — Operating ownership and acceptance thresholds remain unassigned — before public accounts/prizes

Technical design lists backups, retention, monitoring and administration, but recovery-point/time objectives, verifier-lag thresholds, partner size/load assumptions and named responders remain unassigned. A flexible manual prize system still needs funding, delivery and dispute responsibility per event.

Mitigation: nominate gameplay/art/engineering/release/operations owners; define deployment spending limits, alert thresholds, restoration targets and support escalation. Benchmark concurrent active runs and verifier CPU rather than treating a modeled hosting price as a quote. Test an event end-to-end with simulated winners and retryable grants before announcing prizes. No automated payouts are required.

### A10 — Balance and replayability remain hypotheses — M2 and later

Ten-minute stage timing can produce much longer multi-stage runs by design. One leaderboard intentionally includes different biomes, tiers and account power. These are accepted tradeoffs, not gaps to undo. Stronger Logbooks, Captain passives and proc bridges could nevertheless make overtime too easy or speed resource generation dramatically.

Mitigation: measure weak, typical and high-synergy runs; record first meaningful combination, boss/portal times, death cause, overtime duration, upgrade affordability, storage overflow and reacquisition time. Ask players whether they understand their loss and want another attempt. Freeze a tested baseline before increasing content scope. More Items are not a substitute for a satisfying first loop.

## Gate summary

- **Start now:** isolated M0/M1 prototype with placeholders, versioned content and no live economy.
- **Before the Saltwind slice is accepted:** camera/input choice, representative device results, numeric contract, selected content records and basic readability/feel testing.
- **Before public accounts and raids:** authoritative verification, transaction/recovery tests, eligibility matrices and former-Crew policy.
- **Before partner prizes:** deadline policy, tested closure/review/delivery process and event-specific funding/eligibility.
- **Before monetization/release:** approved art provenance, audience/channel/catalogue decisions, support/restore targets and real device/store acceptance.

No implementation completion is inferred from file names, prefabs, existing scenes or the number of documents. The first Cursor task must establish what already exists before changing game code.
