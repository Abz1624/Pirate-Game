# Rogue Pirates: GDD-to-Launch Development Plan

## 1. Product baseline and success criteria

Build Rogue Pirates as a polished, stylized 3D ship-based bullet-heaven roguelite with persistent Territory progression, asynchronous raids, Crews, competitive events, cosmetic monetization, and light comic storytelling.

Assume:

- A 4–8 person core team with specialist contractors.
- Quality-gated development without a promised launch date.
- One coordinated public launch across Android, iOS, direct web, Steam, CrazyGames, and Poki after every named channel passes certification.
- Landscape presentation on mobile, browser, and desktop.
- Teen/adult 13+ audience. Cash or SOL prize eligibility requires separate age, region, and event-rule checks; gameplay participation remains broader.
- Stylized modular 3D art with strong polish, animation timing, responsive audio, layered VFX, hit feedback, and readable enemy silhouettes.
- “The Sea Keeps Receipts” becomes the narrative baseline.
- All `references/**` assets remain excluded.
- No player trading, wallets, blockchain inventory, premium power, paid revivals, paid Gold/Rum, or pay-to-win progression.
- Crews, events, cosmetics, raids, three biomes, and the server-backed economy are release requirements.

Internal success gates:

- At least 70% of new players complete first conquest during usability testing.
- At least 40% of players who die before the first boss immediately start another run.
- At least 80% of testers correctly explain the Beacon, boss objective, portal, post-boss survival, and death settlement.
- Target telemetry hypotheses for soft validation: D1 retention ≥35%, D7 ≥12%, D30 ≥5%, with acquisition source and platform segmented.
- Crash-free sessions ≥99.5%; transaction duplication and currency imbalance incidents equal zero.
- Stable 30 FPS on minimum supported devices and mobile browsers; 60 FPS on recommended hardware.
- P95 account API latency below 300 ms outside run-verification completion; ordinary verifier backlog below 60 seconds.
- Ads remain below 5% of Gold and Rum supply for the median engaged player.
- Paid ownership has no measurable leaderboard advantage after controlling for playtime and progression.
- Launch only after players repeatedly choose another run because of build possibilities, rather than solely because of daily rewards.

## 2. Final game specification

### Core player flow

1. Boot, update check, consent, accessibility defaults, and account/guest authentication.
2. First-time players watch an optional 20-second, three-panel introduction.
3. Select Captain, activity, biome, and Voyage tier.
4. Start at Level 0 with the Captain’s signature Weapon; first conquest always uses Deck Cannon.
5. Explore a freshly assembled stage containing finite chests and POIs.
6. Auto-attack enemies, collect XP and run Gold, level up, and choose randomized upgrades.
7. Discover and activate the Treasury Beacon.
8. Defeat the stage boss.
9. For an intermediate Voyage stage, choose when to use the opened portal while overtime escalates.
10. After the final boss, continue fighting until death.
11. On death, bank 100% of unspent run Gold, settle generated rewards, reveal permanent progress, update quests/events, and show build/death analysis.
12. Return to the persistent Territory to upgrade, configure defenses, manage Items/cosmetics, choose contracts, or begin another activity.

### Modes

- **First conquest:** reserved Saltwind home seed; Barnacle King secures the Territory; play continues until death.
- **Ordinary Voyages:** free entry; Tier 1/2/3 contain 1/2/3 stages; eligible for the global kill leaderboard.
- **Expansion:** reveal and conquer one outer Territory band; failure never removes owned territory.
- **Home mastery:** Fast Flag, Lightly Armed, and Steady Hands; one stage each; repeatable ordinary rewards and once-only mastery prizes.
- **Raids:** paid entry, rearranged defender sections, hidden Beacon, one boss, once-only theft, then survival until death.
- **Defense practice:** no permanent rewards or scores.
- **Crew and partner events:** reuse verified ordinary Voyage facts; do not require a separate combat mode.

### World generation

- Section cell: 48×48 metres.
- Territory sizes: Saltwind 3×3 cells, Gravewater 5×5, Devil’s Maw 7×7.
- Four structural families: open water, reef channel, wreck cove, ruined harbor.
- Create three geometric variants per family and biome-specific dressing.
- Preserve the owner’s canonical home arrangement.
- Raid generation permutes sections only inside their original Zone band; decorations and authored encounters stay attached.
- Rotate only by validated quarter-turns.
- Require connected spawn, Beacon, mandatory encounters, chests, and boss arena.
- Minimum navigable corridor: 6 metres.
- Spawn-safe radius: 8 metres for 5 seconds.
- Beacon route lengths: 60–180 m, 100–260 m, and 140–340 m by Zone.
- Try 32 deterministic arrangements, then use a validated fallback.
- Test at least 10,000 generated seeds per biome/tier in CI.

### Stage pacing and spawns

Each stage uses its own active simulation clock:

- 0:00–2:00: Sharks 65%, Cannonboats 20%, Swordfish 15%.
- 2:00–5:00: Sharks 40%, Cannonboats 20%, Swordfish 20%, Mantas 10%, Crabs 10%.
- 5:00–10:00: Sharks 30%, Cannonboats 20%, Swordfish 20%, Mantas 12%, Crabs 10%, Hammerheads 5%, Kraken Spawn 3%.
- Gravewater and Devil’s Maw move eight Shark weight points to Siren or Fire Eel after minute 2.
- Elite Hammerhead at 3:00 and elite Kraken Spawn at 8:00.
- Director-rate pulses of +25% for 20 seconds at 4:00 and 7:00.
- Five-second warning before scheduled elite arrival.
- Defer a scheduled elite by at most 20 active seconds while a boss is active or no valid slot exists; then skip it and record why.
- Natural elite chance: 0% before minute 3 and 2% afterward.
- Ordinary cap: 180; elite cap: 12; boss cap: 1.
- Ordinary spawn rate:  
  `min(14, (2 + 0.5 × min(stageMinute,10)) × (1 + 0.25 × (depth-1)))`
- Stop accumulating spawn debt while capped.

Pre-overtime scaling:

- Hull: `(1+0.65(biome-1)) × (1+0.8(depth-1)) × (1+0.2(tier-1)) × (1+0.12×minute)`
- Damage: `(1+0.25(biome-1)) × (1+0.25(depth-1)) × (1+0.08×minute)`

At 10:00:

- Stop ordinary director spawning.
- Reserve at least 120 ordinary slots for Drowned Pursuit enemies.
- Retire excess pre-overtime enemies deterministically, with a two-second visible retreat.
- Drowned Pursuer/Harrier weighting is 80/20 for two overtime minutes, then 60/40.
- New overtime enemy Hull is fixed at spawn using `2^(overtimeMinute/2)`.
- Their live damage uses `1.45^(overtimeMinute/2)`.
- Their live movement uses `min(1.5,1+0.05×overtimeMinute)`.
- Overtime spawn rate grows to a maximum of 2× the minute-10 rate.
- At four overtime minutes, contract safe space by 5% of total stage area each minute to a 40% floor.
- Every contraction must retain the player and a valid route to an opened portal.
- If contraction validation fails, keep the boundary and add 10% enemy damage at the next step.
- Do not impose an instant-kill timer.

During a boss encounter, use 40% of current director rate. Freeze boss maximum Hull at activation; overtime can continue increasing boss outgoing damage and movement.

### Deterministic tick and simultaneous events

Use a fixed 30 Hz simulation. Each tick executes:

1. Expire effects, apply committed choices, read legal inputs, then advance movement and timers.
2. Resolve completed healing pickups before damage.
3. Generate attack, contact, DOT, and hazard events from stable actor IDs.
4. Resolve damage by propagation generation, source ID, attack ID, target ID, then event ID.
5. Evaluate fatal-hit prevention immediately at a would-be fatal hit.
6. Process bounded triggered effects and enemy deaths.
7. Secure a valid boss death even if the player also died that tick.
8. If the player is dead, cancel pending interactions and choices and settle results.
9. If alive, resolve interactions, pickups, XP, and at most one modal.
10. Process portal transition only after pending choices resolve.

A boss/player double death secures the objective and ends the run. A portal or late healing event cannot rescue a player already marked dead.

### Pause, interruption, and clocks

Replace the current single `PauseReason` with an independent reason set:

- Manual menu
- Level-up choice
- POI choice
- App focus loss
- Mobile suspension
- Connectivity loss
- Account takeover
- Loading/verification barrier

Rules:

- Simulation pauses at a tick boundary when any reason is active.
- Clearing one reason cannot resume while another remains.
- Stage time, attacks, cooldowns, status durations, interactions, spawn beats, and charge progress stop.
- Server event and Crew deadlines continue in wall-clock time.
- Boss entrance is active gameplay and does not pause.
- Manual pause and choice screens count toward one unioned 30-minute competitive pause allowance.
- Exceeding the allowance removes leaderboard/event eligibility but preserves ordinary rewards.
- Show warnings at 20, 25, and 29 paused minutes.
- Require explicit Continue after focus, suspension, connection, or takeover recovery.
- A continuous 24-hour interruption expires the run.
- Expiry preserves secured boss milestones and theft, but loses unsecured run Gold, unfinished chest rewards, and score eligibility.
- Save at every boss, portal, permanent receipt, safe pause boundary, and every 10 active seconds.

### Economy

Keep separate run Gold and treasury Gold.

Sources:

- Enemy Gold: 1–5 base by family, modified only by eligible run effects.
- Boss rewards: `120 × biome × depth` Gold and `30 × biome × depth` Rum.
- Non-boss Rum at death: `min(20×highestBiome, floor(rewardableKills/100))`.
- Home challenges receive 50% generated currency.
- Passive production and LiveOps use the v3.2 budgets.
- Raid theft transfers existing resources and creates no new supply.
- Valid death banks `floor(all unspent run Gold)`.

Run chest price:

`ceil(min(1,200, 25 × 1.16^paidChestCount))`

A Skeleton Key opening is free and does not increment the counter.

Long-term economy:

- Five nine-rank tracks: Gold production, Rum production, Gold capacity, Rum capacity, Defense Director.
- One shared works queue.
- Total functional-upgrade Gold requirement: 1,927,600.
- Minimum uninterrupted queue duration: approximately 70.5 days.
- Ranks 1–3 require Saltwind, 4–6 Gravewater, 7–9 Devil’s Maw.
- Upgrades retain their old effect until completion.
- Combat remains unlimited while construction runs.
- Gold sinks: run chests, five upgrade tracks, raid entry, resource cosmetics, and repeatable Gilding.
- Rum sinks: licenses, slots, RNG tools, initial Item ownership, contracts, reforging, and selected cosmetics.
- Never sell Gold, Rum, Item progress, combat power, revivals, score, protection, or timer acceleration.

Storage and raiding:

- Exposed amount: `floor(0.20×stored + 0.40×uncollectedProduction)`.
- Raid fees: 40/80/120 Gold for one/two/three-Zone targets.
- Half of a qualified fee becomes protected defender compensation; half is sunk.
- Actual theft is the minimum of the entry quote, current exposure, remaining rolling loss allowance, and attacker free capacity.
- Deduct uncollected exposed production first, then stored treasury.
- Rolling 24-hour loss protection: 40% of each capacity.
- Successful theft starts an eight-hour shield immediately.
- First conquest starts a 24-hour shield.
- Boss settlement is atomic and occurs once; post-boss farming cannot steal again.
- Do not reserve defender resources during attacker exploration.

Economy operations:

- Run the deterministic 180-day simulation on every balance change.
- Add a Monte Carlo model for run RNG, player skill, churn, session frequency, chest purchasing, and raid outcomes.
- Evaluate P10/P50/P90 balances, time-to-upgrade, overflow, contract completion, storage exposure, and Gilding adoption.
- Require four weeks of closed economy telemetry before locking launch values.
- Remotely configure reward values, costs, timers, and event budgets, while pinning active runs to their starting content version.
- Never change an active run’s arithmetic remotely.

### Items and buildcraft

- Launch with six Weapons, fourteen Logbooks, and twenty-eight Items.
- Run limits: Weapon rank 12, Logbook rank 40, player level 200.
- Initial capacity: two Weapons and two Logbooks.
- Permanent specialized Item ranks add at most 5% to one named magnitude.
- Dramatic power comes from in-run stacks and interactions.
- Preserve eight Common baseline Items permanently.
- Specialized progress thresholds: 1, 3, 7, 13, 21, 31, 43, 57, 73, 91, 111.
- Reforge three progress plus 20 Rum into one same-rarity progress.
- Freeze ownership, ranks, Captain, content version, and RNG states at run start.
- Limit a source event to 64 secondary events and three propagation generations.
- Limit global secondary events to 2,000/sec, projectiles to 600, area effects to 80, and pickup entities to 200.
- Merge pickups without deleting their value.
- Level offers show three distinct useful choices where possible.
- If fewer than three exist, offer healing/damage/movement fallbacks.
- If every result is capped, show Continue.
- Keep Refresh, Skip, and Banish limited to their earned per-run charges.

### UI layout and interaction

Use a 1920×1080 reference canvas, Canvas Scaler “Scale With Screen Size,” match 0.5, and safe-area padding of the larger of 24 logical pixels or the device inset.

Combat HUD:

- **Top left:** Captain portrait, Hull bar, numeric Hull, armor/status icons. Maximum 30% screen width.
- **Top center:** stage timer, stage/depth label, and overtime indicator. Objective text directly below.
- **Top right:** pause button, then minimap below it. Minimap size is 18% of screen height, clamped to 140–220 logical pixels.
- **Below minimap:** total kills, run Gold, and leaderboard-eligibility state.
- **Left middle:** pinned quest/contract and transient pressure warnings; collapse after five seconds.
- **Bottom center:** equipped Weapons and Logbooks; Items collapse into a scrollable summary rather than covering combat.
- **Bottom left on touch:** 160 logical-pixel floating joystick, at least 24 pixels from the safe edge.
- **Bottom right on touch:** 88-pixel interaction button with radial hold progress. Place it above system gesture areas.
- **Desktop:** WASD/arrows plus controller left stick; E/gamepad South interacts; Escape/Start pauses.
- No attack button is required because Weapons auto-fire.
- Minimum interactive target: 48×48 logical pixels.

Modals:

- Level-up and POI choices use a darkened full-screen scrim.
- Present three horizontal cards on desktop/tablet and three compressed horizontal cards in mobile landscape; use a paged single-card fallback below the minimum readable width.
- Every card shows current value, resulting value, caps, rarity, and input binding.
- Never place an ad, store prompt, or event interruption over a run choice.
- Boss-secured feedback occupies the upper-middle area for no more than three seconds and explicitly says survival continues.
- Overtime warning uses shape, color, audio, and edge treatment.
- Telegraphs render above decorative VFX and remain visible in reduced-effects mode.
- Beacon, portal, elite, and hazard minimap icons use different shapes as well as colors.

Menus:

- Home navigation: Play, Territory, Ledger, Collection, Defense, Crew, Events, Ship & Cosmetics, Store, Settings.
- The first home visit spotlights Play, Ledger, and Collection; locked systems explain their unlock requirement.
- Every service-backed screen has loading, empty, offline, pending, conflict, error, retry, and success states.
- Use controller, keyboard, and touch navigation parity.
- Settings include master/music/SFX volume, scalable HUD, reduced flashes, reduced shake, reduced particles, color-independent warnings, graphics preset, language, controls, account management, support, and reporting.

### Narrative and retention

Use “The Sea Keeps Receipts” through:

- Three skippable opening panels.
- Mags as Ledger/contract guide.
- Crumb as a low-dialogue visual comedy character.
- One introduction and aftermath for each Zone.
- Optional Item, boss, contract, and results flavor.
- Approximately 30 reusable barks with cooldowns.
- No dialogue tree, fully voiced campaign, or story-gated ordinary play.
- Reuse Captains, bosses, ships, and home decorations in 15–30 second marketing shorts.
- Never depict unavailable mechanics as gameplay.

Retention layers:

- Captain’s Journey onboarding milestones.
- One selected repeatable Item contract.
- Three daily Charts.
- Weekly Admiralty Orders.
- One weekly 48-hour overlay.
- Global weekly best-single-run kill board.
- Daily/weekly Crew standings and partner events.
- Three home mastery presets.
- Twenty-eight-day Captain’s Chronicle.
- Personal overtime milestones at 60/120/240 seconds.
- Every run may advance several systems, but no objective requires a random Item the player cannot guarantee.

### Monetization and LiveOps

- Optional Sponsored Cache after finalized results: 75 Gold or 5 Rum.
- Maximum two claims per account per UTC day; the second requires another completed run.
- One missed Daily Chart stamp recovery ad per seven days.
- No native/direct forced interstitials.
- Steam has no ads.
- Portal interstitials only after results or between runs, maximum one per two runs and three/day.
- Launch products: $1.99 Welcome, $4.99 Ship Style, $9.99 Captain’s Set, $14.99 Territory Theme, $19.99 Collaboration Set, and $7.99 non-renewing Chronicle.
- All purchases are known-value, account-bound, restorable, refundable, and cosmetic.
- Chronicle premium rewards contain no Gold, Rum, power, Item progress, ad claims, or acceleration.
- Daily total ≤60 Gold +3 Rum.
- Weekly personal total ≤250 Gold +12 Rum.
- Weekend Crew total ≤200 Gold +8 Rum per player.
- Weekly overlay total ≤150 Gold +6 Rum.
- Competitive leaderboard prizes use cosmetics, titles, badges, frames, or trophies.
- External prizes remain manually administered and require published age, territory, funding, tie, delivery, dispute, and eligibility rules.

## 3. Production architecture and interfaces

### Client and simulation

Retain Unity 6000.3.11f1 initially and pin it after M0 platform builds pass. Use URP, the Input System, uGUI for the existing runtime, and Addressables or an equivalent versioned content delivery layer after download tests.

Split assemblies into:

- Simulation
- Content
- Presentation
- AccountClient
- PlatformAdapters
- EditorValidation
- Tests

Move all authoritative arithmetic into pure C# without `MonoBehaviour`, `Time.deltaTime`, Unity physics, animation events, or `UnityEngine.Random`. Cosmetic bobbing and debris may use non-authoritative randomness. Compile the same simulation source for Unity and a .NET 10 verifier worker.

### Backend

Use:

- ASP.NET Core modular monolith.
- Separately scalable replay-verification worker.
- PostgreSQL as the only authoritative database.
- Railway for initial API, worker, and PostgreSQL deployment.
- Cloudflare CDN and private/public R2 buckets for versioned assets and replay payloads.
- HTTPS request/response first; server push only for justified event/standing updates.
- Transactional outbox jobs; no Kafka or Redis initially.

Identity:

- Guest account with explicit recovery warning.
- Apple/Google identity on mobile.
- Steam authentication ticket on Steam.
- Portal identity adapters on CrazyGames/Poki.
- Email magic-link identity for direct web.
- Account linking requires control of both identities and a conflict screen.
- Never merge two established economies automatically.

### Public contracts

Define and version:

- `ContentManifest`
- `CaptainDefinition`
- `WeaponDefinition`
- `LogbookDefinition`
- `ItemDefinition`
- `EnemyDefinition`
- `BossDefinition`
- `POIDefinition`
- `StageProfile`
- `EconomyDefinition`
- `EventDefinition`
- `CosmeticDefinition`
- `SimulationCommand`
- `SimulationEvent`
- `RunSnapshot`
- `RunCheckpoint`
- `RewardReceipt`
- `RaidQuote`
- `RaidSettlement`
- `PurchaseReceipt`
- `AdRewardReceipt`
- `LiveOpsProgress`
- `VerifiedScoreReference`

Every mutation carries an authenticated actor, idempotency key, expected version where necessary, and a stable source ID.

Minimum API groups:

- Authentication, identity linking, sessions, and recovery.
- Profile, Territory, Ledger, production claims, upgrades, holdings, contracts, cosmetics, and loadouts.
- Run creation, sequenced input upload, checkpoint acknowledgement, resume, takeover, boss settlement, death settlement, and verification status.
- Raid candidates, quote, start, settlement, history, shield, and defense publication.
- Crew membership, locks, queued leave, roster, events, enrollment, standings, and reward history.
- Catalogue, purchases, restore, refunds, ads, Chronicle, daily/weekly progress, and Gilding.
- Restricted operator content publication, event scheduling, moderation, score review, reward allocation, reconciliation, and audit export.

Run verification flow:

1. Server creates run ID, seed, manifest version, frozen progression, affiliations, and fencing lease.
2. Client uploads ordered input/choice batches every two active seconds and at every boss, portal, pause, and death boundary.
3. Worker advances from the last verified checkpoint.
4. Hash disagreement marks the result pending investigation; it never accepts client balances or kills.
5. Boss settlement generates an idempotent immediate receipt.
6. Death verification generates final rewards and a leaderboard submission.
7. Store ordinary replays for 30 days and prize/disputed replays for 90 days, subject to the final privacy policy.

### Specification cleanup before coding further

Resolve contradictions in the current documents and code vocabulary:

- Remove obsolete `list/cancel/buy holding`, `list cosmetic`, “list a spare,” and paid-acceleration commands.
- Correct duplicate GDD numbering.
- Replace stale statements that no source code was inspected.
- Mark the older `IMPLEMENTATION_STATUS.md` as historical or replace it with the Integration Checklist dashboard.
- Standardize gameplay terms: XP, Gold, Rum, Notoriety, and Bounties. Remove legacy Infamy/Doubloon names from player-facing and authoritative data.
- Replace legacy POI enum aliases with stable content IDs.
- Make Economy v3.2 authoritative wherever older files conflict.
- Keep one machine-readable manifest of every approved prototype value.

## 4. Step-by-step implementation checklist

### Gate 0 — Reconcile and measure the prototype

- [ ] Freeze a clean baseline branch and preserve unrelated current Unity changes.
- [ ] Convert the document hierarchy into one authoritative rules index.
- [ ] Complete the specification cleanup listed above.
- [ ] Inventory every existing scene, prefab, ScriptableObject, service, test, and placeholder.
- [ ] Classify each as retain, refactor, replace, or remove.
- [ ] Pin Unity, URP, Input System, test framework, and build packages.
- [ ] Define the numeric precision, integer/fixed-point, rounding, and overflow contract.
- [ ] Define named RNG streams and remove authoritative `UnityEngine.Random` calls.
- [ ] Build dense 180-enemy, 12-elite, 600-projectile, and 80-area-effect stress fixtures.
- [ ] Test native Windows, Android, iOS, desktop WebGL, Android Chrome, and mobile Safari.
- [ ] Select minimum devices only from measured results.
- [ ] Benchmark verifier CPU seconds per simulated second.
- [ ] Record memory, thermals, download size, battery, frame time, and browser tab survival.
- [ ] Create CI jobs for Unity EditMode, PlayMode, deterministic replay, server, content validation, and builds.
- [ ] Exit only when one golden replay matches Unity native, WebAssembly, and server exactly.

### Gate 1 — Deterministic combat foundation

- [ ] Replace `RunManager`’s single pause value with the pause-reason set.
- [ ] Implement the exact ten-step tick ordering.
- [ ] Move movement, collision, damage, statuses, RNG, spawning, pickups, and scores into pure simulation code.
- [ ] Use deterministic circle/segment collision and stable spatial-grid traversal.
- [ ] Implement input buffering for keyboard, controller, and touch.
- [ ] Implement target selection with stable distance/ID tie-breaking.
- [ ] Complete damage attribution, proc ancestry, event budgets, and once-only kill credit.
- [ ] Implement useful-offer filtering, multi-level XP queues, and one-modal ordering.
- [ ] Add checkpoint serialization and hash comparison.
- [ ] Add placeholder presentation adapters that consume simulation events.
- [ ] Exit when 30/60/120 FPS produce identical outcomes and nested pause reasons cannot resume incorrectly.

### Gate 2 — Polished Saltwind vertical slice

- [ ] Complete Deck Cannon, Broadside, Powder Kegs, and Flying Cutlasses.
- [ ] Complete eight baseline Logbooks and 12 slice Items.
- [ ] Complete all seven base enemy families.
- [ ] Complete Drowned Pursuer and Harrier.
- [ ] Complete Barnacle King with three phases and vulnerability windows.
- [ ] Implement 12 finite chests and four finite POIs.
- [ ] Complete Repair Wreck, Powder Cache, Signal Buoy, and Treasure Chart.
- [ ] Enforce six temporary powerups per stage.
- [ ] Implement Beacon discovery at 24 m, sector hint at 7:00, exact marker at 9:00.
- [ ] Implement the 3/4/7/8-minute pressure beats.
- [ ] Implement overtime replacement and storm contraction.
- [ ] Implement boss success followed by survival to death.
- [ ] Implement Rook, Brine, and Flint.
- [ ] Produce the first approved ship, Saltwind kit, enemy readability set, minimal VFX/audio, HUD, minimap, choices, death, and results.
- [ ] Complete first-run wireframes and usability tests.
- [ ] Exit when players understand the loop, weak runs prompt retries, and strong runs remain readable at cap.

### Gate 3 — Accounts, persistence, and economy integrity

- [ ] Create development, staging, and production environments with separate secrets and economies.
- [ ] Implement account identities, guests, linking, recovery, sessions, and one active-run lease.
- [ ] Build PostgreSQL migrations and immutable currency ledgers.
- [ ] Implement run batching, checkpoint/resume, takeover, expiry, and replay verification.
- [ ] Implement RewardReceipt idempotency and reconciliation.
- [ ] Implement first conquest, saved home seed, Ledger, production, capacities, queues, holdings, contracts, and cosmetics.
- [ ] Implement 100% death banking and immutable result chest reveal.
- [ ] Implement economy telemetry and the Monte Carlo simulator.
- [ ] Conduct backup restoration and duplicate-request tests.
- [ ] Exit when cross-device reconnect cannot duplicate or lose accepted rewards and forged balances are rejected.

### Gate 4 — Full progression, content, Territory, and raids

- [ ] Complete Flamethrower and Lightning Mast.
- [ ] Complete all 14 Logbooks and 28 Items.
- [ ] Complete Gravewater, Devil’s Maw, Siren, Fire Eel, Drowned Admiral, and Infernal Kraken.
- [ ] Decide from testing whether Vale, Cinder, and Tempest all add distinct playstyles; retain only those that do.
- [ ] Implement Tier 1/2/3 Voyages and build carryover across portals.
- [ ] Implement persistent 3×3, 5×5, and 7×7 home expansion.
- [ ] Implement all eight POIs and three mastery challenges.
- [ ] Implement Defense Director cards, patterns, budgets, traits, preview, and no-reward practice.
- [ ] Implement raid target selection, former-Crew exclusion, quotes, fees, permutations, theft, shields, loss limits, refunds, and compensation.
- [ ] Run thousands of concurrent-raid transaction tests.
- [ ] Run four weeks of closed economy and progression testing.
- [ ] Exit when all three stages remain finite, portals create meaningful timing choices, raids are understandable, and resource loss does not prevent progress.

### Gate 5 — Crews, competition, cosmetics, and narrative

- [ ] Implement Crew creation, joining, applications, invitations, roles, moderation, and configurable capacity.
- [ ] Implement event-duration membership locks and atomic “Leave when event ends.”
- [ ] Process event close, queued departure, then next event lock in that order.
- [ ] Implement global, Crew, and partner standings from verified runs.
- [ ] Implement best-run replacement, shared-rank ties, provisional standings, 24-hour submission cutoff, and later verification.
- [ ] Build the operator event and reward-allocation console.
- [ ] Implement account-bound cosmetic instances and next-run loadout freezing.
- [ ] Produce one complete partner cosmetic pack and rehearse one event without real prizes.
- [ ] Implement the opening story, Zone beats, Mags, Crumb, barks, and narrative marketing templates.
- [ ] Exit after an end-to-end daily and weekly event proves roster changes, retries, ties, and grants cannot alter or duplicate results.

### Gate 6 — Ads, IAP, Chronicle, and channel adapters

- [ ] Integrate sandbox Apple, Google, Steam, direct-web, CrazyGames, and Poki adapters.
- [ ] Implement capability-driven store and ad UI.
- [ ] Implement server-side purchase validation, restoration, refund, and revocation.
- [ ] Implement the six launch product bands.
- [ ] Implement Sponsored Cache claims and cross-device limits.
- [ ] Implement portal interstitial frequency limits and lifecycle callbacks.
- [ ] Implement Daily Charts, Admiralty Orders, weekly overlay, Crew weekend, and Chronicle.
- [ ] Validate consent, privacy, age treatment, ratings, taxation, refunds, and external-prize rules for launch markets.
- [ ] Verify paid cosmetics and ads cannot affect verified simulation or score.
- [ ] Exit only when every purchase/ad recovery path works in sandbox and each platform feature matrix is approved.

### Gate 7 — Production polish and coordinated launch

- [ ] Replace every gameplay placeholder with accepted, licensed production assets.
- [ ] Finish three biome kits, ship cosmetics, bosses, icons, portraits, VFX, audio, UI, and accessibility variants.
- [ ] Finish English copy and localization architecture; select launch languages from market testing.
- [ ] Run full device, browser, thermal, memory, controller, touch, safe-area, and accessibility QA.
- [ ] Run API, database, verifier, event-finalization, and raid load tests at 2× expected launch peak.
- [ ] Conduct security review, economy exploit testing, purchase fraud testing, moderation exercises, and disaster recovery.
- [ ] Create support, incident, refund, entitlement, moderation, event-dispute, and prize-delivery runbooks.
- [ ] Prepare app icons, screenshots, trailers, capsules, age ratings, privacy disclosures, and store copy.
- [ ] Submit every named platform early enough to resolve certification findings before the coordinated date.
- [ ] Run a release-candidate season in staging with accelerated time.
- [ ] Freeze content, economy, and server schema versions for launch.
- [ ] Prepare feature flags, kill switches, rollback, spend alerts, and on-call ownership.
- [ ] Launch only when every gate is passed; no document or placeholder counts as playable evidence.

## 5. Asset and production workstreams

Staffing baseline:

- Product/game director and economy designer.
- Gameplay/simulation engineer.
- Unity presentation/platform engineer.
- Backend/online engineer.
- 3D/environment/technical artist.
- UI/UX and 2D artist.
- QA/release owner.
- Contract audio, writing, animation, localization, community, legal, and specialist art support.

Art production order:

- Art bible and gameplay readability tests.
- One production ship, Shark, Cannon, water section, chest, Beacon, and HUD.
- Saltwind vertical-slice set.
- Reusable base enemy rigs and attack-language library.
- Gravewater and Devil’s Maw material/prop variations.
- Bosses and remaining Weapons.
- Cosmetic modular system and partner pack.
- Narrative panels, marketing key art, and store media.

Required production inventory:

- 12 structural section templates.
- Three biome dressing kits.
- Base ship rig plus hull, sail, flag, emblem, wake, and ornament sockets.
- Seven base enemies, two extra biome enemies, two overtime enemies, and three bosses.
- Six launch Weapon presentation kits.
- Eight POIs, four temporary pickups, Beacon, portal, chest, XP, Gold, and Item drops.
- Forty-eight gameplay icons and six Captain portraits.
- Complete HUD/menu/state illustrations.
- Music states for home, combat, boss, overtime, and results.
- Reusable SFX families for Weapons, enemies, bosses, pickups, UI, economy, and events.
- Low/high VFX variants, reduced-flash/shake variants, and color-independent telegraphs.
- Storefront, social, event, partner, and short-video templates.
- Provenance, licensing, source files, import settings, performance budgets, and reviewer approval for every asset.

“Stylized modular” controls production cost; it does not reduce polish. Polish comes from timing, anticipation, hit reactions, camera impulse, layered sound, trails, water response, reward choreography, transitions, and consistent visual hierarchy.

## 6. Validation matrix

Automated tests:

- Golden replay parity across Unity native, WebAssembly, and server.
- Boss/player double death, fatal prevention, DOT boss death, late healing, and portal/death conflicts.
- Pause-reason overlap, allowance calculation, 24-hour expiry, reconnect, and device takeover.
- Proc ancestry, recursion limits, event caps, stable target order, and frame-rate independence.
- Ten-thousand-seed map reachability and boss-arena validation.
- Chest pricing, 100% banking, passive accrual, capacities, upgrades, contracts, reforging, Gilding, ads, and overflow.
- Raid quote races, concurrent settlements, shields, refunds, loss allowances, and attacker capacity.
- Purchase duplicate callbacks, restoration, refund, entitlement fallback, and ad duplicate claims.
- Crew lock boundaries, queued leave, leadership fallback, ties, late submissions, score invalidation, and reward retries.
- Content-schema references, weights, unlock reachability, caps, asset compatibility, and version resumption.

Manual/playable tests:

- First-session comprehension without developer explanation.
- Weak, normal, and exceptional build runs.
- Every Captain/Weapon pair and intended cross-Item interaction.
- Every enemy tell under maximum VFX density.
- Every boss phase with reduced effects and color-vision filters.
- Portal visibility during storm contraction.
- Mobile touch obstruction, hand comfort, safe areas, and browser rotation.
- Controller-only navigation from boot through purchase restoration.
- Interruption during every modal, boss, portal, reward, ad, and purchase state.
- Raid fairness across repeated permutations.
- Full-storage messaging and resource-spending motivation.
- Event enrollment, Crew switching, provisional standings, and prize history.

Operational gates:

- Daily backup and monthly restore exercise.
- Versioned migration and rollback rehearsal.
- Alerting for crashes, API errors, verifier lag, divergence, ledger imbalance, duplicate requests, stale leases, failed purchases, failed grants, and abnormal currency generation.
- Named owner for gameplay, economy, art, backend, release, community, support, privacy, and event rewards.
- Launch dashboard segmented by platform, device, acquisition source, payer status, progression, and Crew participation.

The current Unity project should be treated as an M2 prototype. Its existing scene chain, pooling, content scaffolding, interaction code, and presentation experiments are useful starting material, but no reward-bearing online system should build on them until deterministic parity, pause stacking, event ordering, authoritative RNG, and server verification pass Gate 1.
