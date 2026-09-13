# Rogue Pirates Game Design Document

**Studio:** X2C  
**Version:** 3.1  
**Date:** 13 September 2026  
**Engine:** Unity, 3D assets  
**Platforms:** Android, iOS, desktop/mobile browsers; Steam, CrazyGames, Poki and other eligible F2P channels. Browser/mobile launch together; individual channel approval remains required.  
**Status:** implementation design baseline, not a claim of implemented or playtested behavior

Rogue Pirates is a 3D ship-based bullet-heaven roguelite. Players conquer a permanent Territory, develop its economy through a Captain's Ledger, author defenses, raid rearranged versions of other players' Territories, and chase extraordinary builds through multi-stage Voyages. Boss victories secure objectives. Runs continue until death.

## How to use this specification

This edition replaces v3.0 as the current design. The unchanged v3.0 file remains a historical record. The following Markdown documents form the current specification and its explicitly marked proposals:

- **This document:** product rules, world structure, activities, scope, interfaces, delivery order, and acceptance criteria.
- [Combat and content specification](Rogue_Pirates_Combat_Content_v3_1.md): common combat math, all launch Weapons, Logbooks, Items, enemies, bosses, spawning, and progression curves; expansion Weapon definitions are included separately from launch scope.
- [Economy and systems specification](Rogue_Pirates_Economy_Systems_v3_1.md): resources, Ledger upgrades, ownership/merging, contracts, raids, protection, trading, rewards, consumables, events, and persistent state.
- [Crews, events and ship skins](Rogue_Pirates_Crews_Events_Skins.md): newer owner-approved social/event requirements, event-duration membership locks, manual rewards and cosmetic inventory; detailed defaults remain labeled proposals.
- [Narrative, retention and production proposal](Rogue_Pirates_Narrative_Retention_Production_Proposal.md): narrative/revenue recommendations, asset checklist and implementation-readiness review. Unapproved recommendations do not override settled mechanics.
- [Unity technical design](Rogue_Pirates_Technical_Design.md): runtime, proposed backend/database/hosting, account saves, verified simulation, structured content and channel-specific integration.
- [Implementation milestone backlog](Rogue_Pirates_Implementation_Backlog.md): ordered development tasks and acceptance gates; wireframes explicitly remain a deferred TODO.
- [Megabonk reference review](Rogue_Pirates_Megabonk_Design_Review.md): researched polish decisions, explicit source conflicts, preserved constraints and validation priorities.
- [Captains and mastery](Rogue_Pirates_Captains_and_Mastery.md): original character passives, unlocks, skin independence and repeatable home challenge presets; new values are prototype proposals.

**Decision status:** owner-approved directions are identified as commitments below. All new numbers, algorithms, inventories, timings, and procedural rules in the linked specifications are **proposed prototype defaults**. They support staged prototyping, but the readiness audit identifies remaining engineering details and release decisions. They have not been approved individually or established through playtesting and are not production balance promises.

13 September audit outputs: [readiness audit](Rogue_Pirates_Readiness_Audit.md), [Cursor implementation handoff](Rogue_Pirates_Cursor_Implementation.md), and [asset production checklist](Rogue_Pirates_Asset_Checklist.md). The owner excluded all assets under `references/` from this plan. Their presence is not permission to import or ship them.

If descriptions appear to overlap, this document controls product commitments, the combat document controls combat/content arithmetic, and the economy document controls transactions and resource arithmetic. The newer Crew extension controls Crew/event/skin rules, including its approved scope additions. A content definition does not expand launch scope by itself. No game source code or existing engine architecture was inspected.

## 1. Product commitments

1. The core loop remains movement, auto-attacks, XP, randomized offers, exploration, and boss encounters.
2. Some builds fail early; others become unusually powerful. Do not guarantee the tools to complete a desired build during every run.
3. Each stage has a ten-minute soft timer, followed by increasingly dangerous overtime. This is not a ten-minute session cap.
4. Boss defeat records success but never opens a Return Home or extraction choice. Death ends play and presents results.
5. Boss-unlocked portals allow advancement within multi-stage Voyages and home challenges. Players can stay for remaining treasure at increasing risk.
6. Chests and POIs receive finite stage-start allocations. They do not replenish. Enemy XP and Gold drops continue in overtime.
7. A minimap marks discovered objectives and opened portals.
8. The home Territory has a persistent concentric-square layout. Saltwind is central; Gravewater and Devil's Maw surround it.
9. Raids rearrange that Territory's recognizable terrain sections with their attached decorations and authored encounters. Each attempt remains fixed after generation.
10. Economic progression lives in the Captain's Ledger. There is no mandatory building placement system. One Treasury Beacon represents the raid treasury and summons the boss.
11. Gold and Rum remain the only gameplay currencies. Fiat and SOL are payment rails; trading is optional, with no guaranteed resale price, buyback, or fixed Gold-to-SOL redemption.
12. A persistent Item type has one combined holding. Its earned upgrade progress transfers with a whole-holding sale and adds to a buyer's existing progress. Ownership enables future random drops; it does not grant an equipped starting Item.
13. Permanent Item upgrades provide a small total improvement. Run combinations create the dramatic power spikes.
14. Baseline Items maintain playability. Random rewards and repeatable targeted contracts allow reacquisition without a marketplace purchase.
15. Gold/Rum cosmetics can be displayed and traded for SOL. They do not affect combat or navigation.
16. There is one weekly best-single-run Voyage kill leaderboard across all biomes, tiers, and account progression. Biome and depth advantages are accepted. Raids and home challenges are excluded.
17. Raid theft settles once at boss defeat, followed immediately by applicable defender protection. The attacker can farm generated rewards afterward until death.

## 2. Audience and experience

The game serves players who enjoy replaying short initial attempts and extending successful builds into longer runs. Deeper tiers can last thirty minutes or longer. Conquest and wealth protection give progression emotional weight; the leaderboard shows what stronger accounts and lucky builds can achieve.

The intended reaction to a weak run is “That combination did not work; I want another attempt.” Preserve counterplay, readable telegraphs, useful early choices, and progress after meaningful failure. Do not make every loss disappear through guaranteed exchanges or unlimited rerolls.

The player fantasy is ship-based throughout the initial release. On-foot ports, agents, and co-op do not block the core game.

The Megabonk polish pass adds a selected Captain profile that supplies a signature starting Weapon and a run-level passive, represented through the existing ship and a portrait. Six profiles are defined; prototype Rook, Brine and Flint first. Captains have no permanent stat ranks and never derive power from collaboration skins. See the Captain specification for unlocks and baseline overrides.

## 3. Activities

### 3.1 Fresh-map Voyages

Free entry. Select an unlocked biome and Voyage tier. Receive fresh procedural geography and randomized build opportunities. Earn resources, permanent reward progress, contracts, unlocks, and an eligible leaderboard score.

Prototype tier sequence per biome: Tier 1 has one stage, Tier 2 has two, Tier 3 has three. Defeating the final required boss unlocks the next tier even though the run continues. The biome becomes available after conquering its Territory Zone. Saltwind Tier 1 is available immediately after first conquest.

### 3.2 First conquest

Reserve a home seed for the account and start a one-stage Level-0 Saltwind run. Guarantee Deck Cannon for first-conquest attempts until the first claim. Teach combat, chests, minimap, Beacon interaction, and boss telegraphs through short contextual prompts.

After first claim, Captain selection controls the starting Weapon as specified in the Captain document. All subsequent offers and Item pools remain randomized without Captain-specific weighting.

Barnacle King defeat secures Saltwind, unlocks the Ledger and subsequent progression, and shows TERRITORY CLAIMED without stopping play. Continue until death. Retry a failed initial attempt on the same reserved home geometry, with new reward/encounter seeds; avoid forcing regeneration of a home identity on each failure.

### 3.3 Permanent expansion

A dedicated Level-0 run temporarily reveals the next outer band. Defeat that band's objective boss to claim it immediately. Existing permanent territory is never lost on failure. Runs are not sequential multi-stage Voyages: one new band's boss is the objective, followed by continued survival until death.

Prototype access: claim the prior Zone, meet the Ledger rank requirement in the economy specification, and activate its Expansion command. No all-upgrades-maxed requirement. No entry resource fee. An unclaimed next-band seed is generated once and reused on retries.

### 3.4 Home challenges

Use the persistent home arrangement. Owner decision during the 13 September audit: the initial catalogue is the three presets in [Captains and mastery](Rogue_Pirates_Captains_and_Mastery.md): Fast Flag, Lightly Armed and Steady Hands. They replace the older four challenge families and their reward schedule; do not implement both.

All three initial presets use one stage at Tier 1. Their first-completion grants occur once per account/preset, not once per biome/tier. Recurring home payouts still follow the economy specification. Losing a challenge condition does not end play; boss success and eventual death remain separate outcomes.

Multi-stage home challenges with temporary outer square bands remain a later extension, not part of these three presets. If added, temporary terrain must never change permanent ownership, production, storage, decoration sockets or future expansion seeds. Multi-stage Voyages remain in core scope.

### 3.5 Raids

Pay Gold, receive an immutable generated arrangement of a defender's Territory, start at Level 0, explore, activate the hidden Treasury Beacon, defeat the boss, and secure permitted theft. Raids have one objective boss corresponding to the defender's highest Zone, not a chain of Voyage stages. More Zones increase traversal and defensive variety.

A meaningful failed raid can earn non-stolen rewards. A successful raid continues to death; no additional treasury withdrawals happen during overtime.

### 3.6 Defense practice

Test a sample generated arrangement from a saved defense configuration. Show route, time-to-boss, damage taken, and encounter completion. No account resources, permanent Item progress, contract progress, or competitive score are earned. This prevents reward farming from deliberately weak self-authored defenses.

## 4. Territory geography

### 4.1 Geometry units

Prototype game units are meters. A terrain section occupies a 48 by 48 meter cell. Home Zone 1 is a 3 by 3 cell square (144-meter side). Zone 2 extends it to 5 by 5 (240-meter side). Zone 3 extends it to 7 by 7 (336-meter side).

Each new Zone is the added square band, not a replacement for inner geography. The ocean and rectangular cell boundaries are not rendered as a grid. Coastlines, reefs, wrecks, and obstacle interiors have irregular shapes.

The launch section library has four reusable structural families: open water, reef channels, wreck cove, and ruined harbor. Produce three geometric variants per family; biome art kits recolor and dress these shapes. A section has navigation connectors, encounter anchors, POI anchors, cosmetic sockets, safe spawn candidates, and optional boss-arena candidates.

### 4.2 Stable home

Generate the canonical home arrangement on first account creation, saving section identities and orientation. Expansion saves new bands without moving previous ones. The owner chooses decorations for named section sockets; no free-coordinate placement, building construction grid, or owner-scripted collision is required.

The Ledger is accessible from a home overview and results screen. Management does not require sailing to separate structures. The home map remains visible and personally recognizable.

### 4.3 Rearranged raid instance

Copy the owner's section inventory, unlocked bands, decoration attachments, and defense configuration into the raid snapshot. Permute section instances only within their original Zone band. Rotate sections in quarter-turn increments only where connector validation permits. Decorations and encounter assignments remain attached to their section instance and rotate with it.

Choose a fresh valid attacker spawn and Treasury Beacon after arranging sections. Use an attack-specific layout seed. A repeated attempt receives a new arrangement; replaying or resuming the same attempt uses its existing seed.

The arrangement changes, but the player's thematic regions, rare decorations, authored enemy choices, and permanent upgrades remain visible. Do not generate unrelated geography merely bearing their name.

### 4.4 Generation validation

Require one connected traversable component containing spawn, Beacon, and all mandatory encounter anchors. Ship-clearance corridors must be at least six meters wide; the player's collision radius is 0.8 meters. Every biome kit includes multiple section variants with an 18-meter-clear-radius boss candidate.

Beacon placement: highest unlocked Zone in raids; current objective Zone in conquest/expansion. Candidate route distance from spawn must be 60–180 meters in Zone 1, 100–260 in Zone 2, and 140–340 in Zone 3. Choose from candidates meeting the whole route and arena checks, not just Euclidean distance.

Reserve an eight-meter spawn-safe radius; no encounter or damaging hazard activates inside it for the first five seconds. No decorations create collision, conceal the Beacon, or block connectors. All stage rewards must be reachable, including a chest within 35 meters of spawn and two distinct routes toward the wider map when the template permits.

Try up to 32 deterministic layouts. If none pass, select a prevalidated arrangement for the same band inventory and validate again. Never charge an entry fee for an invalid generation. Invalid output blocks the attempt with a recoverable error, not a malformed combat map.

### 4.5 Treasury Beacon

One compact sealed wreck-vault is the raid objective. Its presentation combines treasure and a ritual signal in one silhouette. Gold and Rum capacity exist in the Ledger, not as separate destructible storage buildings.

The Beacon has no attackable treasury HP. It is an interactable: remain within three meters and hold the interaction for 0.75 seconds while movement pauses. Damage interrupts the interaction. Successful activation starts the boss encounter once. There is no currency cost.

Before discovery, the minimap does not reveal its exact position. At minute seven, show a broad directional sector. At minute nine, show the exact marker. Discovery within 24 meters marks it permanently for that run. Beacon arena and environmental obstacles remain fixed after activation.

### 4.6 Dread Tide

The permanent ocean boundary remains distinct from overtime. Prototype outside-boundary damage: 0.75-second grace; then 6% maximum Hull per second until two seconds outside, 12% until four seconds, then 25%. Damage bypasses armor, cannot crit, and cannot be used to trigger offensive Item procs. Returning inside for one second resets exposure.

A qualifying expansion moves the home boundary immediately in saved state. Temporary challenge space affects only that instance. Overtime may contract accessible safe space around navigable routes, but never randomly strand an opened portal outside it.

## 5. Stage transitions and run completion

Every stage begins its soft timer at zero. Time advances only during active simulation. Level-up choices, loading, and a bounded interruption do not advance combat time. The combat specification defines escalation, stage growth, and upper performance limits.

An intermediate boss unlocks a portal at its Beacon after the death sequence. Approach within three meters and interact for 0.75 seconds; damage can interrupt. Carry level, Weapons, Logbooks, Item stacks, current Hull fraction, run Gold, and spent RNG-tool counters into the next stage. New stage chests use the continuous run chest-purchase count. No automatic full heal or free reroll reset.

Final boss death secures the activity goal; it opens no escape portal. Enemy waves continue. The eventual death opens results exactly once. A fatal-damage-prevention Item may delay death once; it is not an alternative completion method.

On results, distinguish objective secured, stage reached, total kills, secured theft, generated rewards, overflow, permanent rewards, and contract progress. Do not label a post-victory death as a failed conquest or failed raid.

Application close is not a voluntary extraction. Resume policy and expiry settlement are defined in the systems specification. A settings menu can leave the application; it cannot award an extraction bonus.

## 6. Captain's Ledger

The home has five functional pages: Treasury, Trade Routes, Rum Supply, Defense Director, and Collection. Cosmetics and Marketplace are additional accessible pages. The former Shipyard/Armory/Archive/War Room names can appear as tab flavor, but do not require placed buildings.

- Treasury: balances, capacities, exposed loot, protection, and capacity upgrades.
- Trade Routes: Gold production, collection, and its upgrades.
- Rum Supply: Rum production, collection, and its upgrades.
- Defense Director: section-bound encounters, patterns, boss traits, budget, and upgrades.
- Collection: Weapons, Logbooks, Item holdings, progress merging, RNG tools, and contracts.

Economic upgrades can have timers and retain their previous function while upgrading. One works queue handles production/capacity/defense improvements; one collection queue is not required because Item merges and unlocked content purchases are immediate. The systems specification supplies prices and times.

No extra generic ship damage/armor research ships in the prototype. Permanent slots and small Item upgrades already supply account combat progression. Cosmetic purchase and equipping are immediate.

## 7. Defense authoring and random arrangements

Players assign encounters to eligible section instances, not coordinates or a fixed Beacon route. Choose an Enemy Card and compatible Pattern. The authored intention follows its section when a raid rearranges the map.

Quick Setup chooses a legal arrangement within budget; Custom Setup offers changes. A preview can display the stable home version and sample raid variants, clearly labeled as examples rather than guaranteed layouts.

Not all encounters are mandatory. Raiders can route around pressure, sacrificing finite rewards and XP. The Beacon remains accessible without clearing a checklist. Automatic encounter activation, encounter budgets, anti-overlap limits, and concrete card definitions are in the systems and combat specifications.

The defender has no control over the attack-specific random seed, attacker spawn, exact Beacon position, or currency payout. Cosmetic layout cannot change encounter collision or difficulty.

## 8. Initial content scope

### 8.1 First playable slice

Saltwind, one-stage runs, initial conquest, Deck Cannon/Broadside/Powder Kegs/Flying Cutlasses, eight foundational Logbooks, eight starter Items plus four specialized Items, the seven standard enemy families, and Barnacle King. Include the post-boss death loop, fixed POIs, minimap, and saved success immediately.

### 8.2 Core release target

Six original Weapons, fourteen Logbooks, twenty-eight defined Items, seven enemy families with Gravewater/Devil's Maw variants, Siren and Fire Eel, two Drowned Pursuit overtime archetypes, eight pirate POI types, three boss families, all three Territory Zones, one-to-three-stage Voyages, three initial one-stage home mastery presets, Ledger, Defense Director, raids, one weekly Voyage board, holding progression and reacquisition, cosmetics, Crews/community events and ship inventory.

First-party payment and public marketplace launch requires transaction and platform validation, but their ownership rules are part of this specification. Implement local/sandbox versions before live settlement. Six expansion Weapons are fully defined for future use; they are not required to prove the core loop.

### 8.3 Optional later features

Crews, daily/weekly community events, collaboration ship variants and cosmetic inventory are now planned release requirements, specified in the [Crews, events and skins extension](Rogue_Pirates_Crews_Events_Skins.md). They follow verified scoring and ownership foundations; the first combat slice stays unchanged. Partner self-service publishing, agents, co-op, on-foot exploration, elaborate replay heatmaps, and new biomes remain later features.

## 9. Competition and player identity

Crew membership is locked while the current Crew's event is active. At the published event end, players may immediately join another Crew; score verification and manual reward delivery do not prolong the lock. Historical scores and awards stay attributed to the original event/Crew.

The owner has added daily and weekly Crew/community rankings and partner events that reward top affiliated players. These supplement the global board below. See the [event extension](Rogue_Pirates_Crews_Events_Skins.md) for proposed roster locks, top-member Crew aggregation, timing, ties and reward settlement. The previous single-board decision continues to mean one shared global Voyage board across progression levels; community/event views are now explicitly supported.

One weekly board ranks each account's best eligible Voyage run by total verified kills, including all stages and post-boss survival. No biome/tier or account-power brackets. Show biome, tier, stages, loadout, duration, and Item progression beside the score.

Prototype tie policy: equal kills share rank. Display tied entries by score timestamp and run ID without awarding a higher competitive rank for that display order. Week begins Monday 00:00 UTC; an attempt belongs to its start week. Its complete submission through death must reach the server by 24 hours after period end; verification may finish later and standings remain provisional until timely submissions are resolved. Separate interruption-expiry rules still apply. No cash/SOL prize is assumed for the baseline global board; partner-event prizes are defined separately.

Raids, home challenges, and self-tests never submit to the Voyage board. A later raid competition requires its own rules. Profile reputation rewards actual achievements and credible defenses; merely receiving attacks gives no points.

Baseline consumables in the systems specification are allowed only in unranked home challenges for the first prototype. This is a proposed restriction, not an owner-approved final competitive policy. Ordinary Voyages remain leaderboard-eligible without a separate casual queue.

## 10. Presentation and accessibility

Add Crew/event interfaces and a Ship & Cosmetics inventory with collaboration variants, loadout preview, discrete cosmetic copies and ownership/trading states. Cosmetics remain stat-free. Production assets, visibility constraints and lifecycle rules are defined in the [skin inventory extension](Rogue_Pirates_Crews_Events_Skins.md).

Top-down perspective with a follow camera. Prototype visible gameplay footprint is approximately 40 by 30 meters at the default aspect ratio; other aspect ratios preserve equivalent visibility without changing simulation rules. WASD/arrows on desktop and a virtual joystick on Android control movement; auto-attacks operate continuously. Touch and keyboard have equivalent interact/upgrade functions. No manual aim is required. Menus must expose textual effect descriptions and next-upgrade changes.

Minimap shows explored water, player heading, discovered chests/POIs, discovered Beacon, and opened portal. Do not reveal a raid's full route at spawn. Use distinct shapes as well as colors for Beacon, portal, hazard, and elite signals.

Enemy attack windups remain visible above decorative effects. Provide reduced flashes, reduced screen shake, scalable HUD, and an option to reduce decorative particles during combat. These options change presentation only, not simulation, spawns, damage, or score.

The home overview is stable. Cosmetics attach to section sockets and follow those sections during raids. No decoration may replace an essential telegraph or imitate a gameplay pickup.

## 11. Logical data and interfaces

These are engine-neutral contracts, not a prescribed framework or wire protocol. Use stable IDs and versioned data. Resolve authoritative state before awarding resources or marketplace assets.

- `ContentDefinition`: content ID, version, unlock rule, behavior ID, base values, level/rank curve, tags, compatible effects, and visual/audio references.
- `Territory`: owner ID, canonical seed, owned Zone count, section instances, canonical transforms, decoration attachments, progression levels, and published defense version.
- `RaidLayout`: run ID, layout seed, section transforms, spawn, Beacon, POIs, encounter placements, validation version, and defender snapshot reference.
- `ItemHolding`: account, Item type, total earned progress units, derived rank, lock state, and version. One aggregate per account/type; unique transfer lots are temporary transaction records.
- `RunSnapshot`: run/account IDs, mode, content version, RNG streams, start time, frozen eligible pools/ranks, stage, player build, timers, chest counts, rewards, objective flags, and interruption deadline.
- `CaptainDefinition` and `MasteryDefinition`: signature Weapon, base overrides, passive triggers/scaling/caps, unlock predicates, mode constraints and one-time rewards. RunSnapshot includes Captain/version, passive counters, mastery status, pressure-beat state and temporary powerup budget/RNG.
- `RaidEntitlement`: defender/attacker, captured exposed-amount ceilings, fee split, quote timestamp, and settlement state. Prototype exploration does not reserve defender balances; the systems document defines live settlement.
- `RewardReceipt`: unique source event, account, Gold/Rum amounts, permanent grants, overflow, and applied status.
- `Trade`: listing/holding reservation, buyer/seller, quoted asset/progress, quote version, payment reference, settlement state, and applied receipt.

Required logical commands: start/resume run; choose upgrade; activate Beacon; enter portal; settle death; claim passive income; start/finish Ledger upgrade; publish defense; start/settle raid; accept/claim contract; merge holding progress; list/cancel/buy holding; buy/equip/list cosmetic; submit verified score.

Mutations use idempotent command/event IDs. Repeated network delivery must not duplicate resource grants, merges, boss milestones, or theft. Reads expose resulting versions so clients can recover from stale UI.

## 12. Technical boundaries

Use server-authoritative reward and ownership validation. Gold/Rum, XP, RNG, enemy state, defense configurations, upgrades, and raid outcomes are ordinary game state. Optional onchain ownership/settlement can represent tradeable assets; no proprietary currency or routine SOL emissions are introduced.

The first combat prototype may run locally for feel testing, but its results are not accepted into a public economy or leaderboard. Public raid scoring needs authoritative simulation or sufficiently complete verified replay; client-declared kills alone are insufficient.

Simulation timestep target is 30 Hz with rendering interpolated separately. Game time and spawn rates must not depend on frame rate. Device presentation may cull distant VFX, never score-affecting enemies. Hard entity and effect budgets are defined in the combat specification.

Unity with 3D assets is owner-selected. The [technical design](Rogue_Pirates_Technical_Design.md) proposes a shared C# simulation, containerized C# backend/verifier, PostgreSQL, Railway compute and Cloudflare assets, pending prototype benchmarks and hosting selection. Save state is account-based in the server database. No source code or existing implementation constraints were inspected.

On channels prohibiting blockchain, the owner selected a build without that functionality, with a trading-hub link only where permitted and no link otherwise. Platform acceptance and treatment of external trading-derived gameplay entitlements remain separate checks; see the technical design. Do not silently remove ownership or promise all-channel transaction parity.

## 13. Acceptance criteria

The latest polish pass also requires tests for Captain/skin independence, minute-3/4/7/8 pressure beats, useful level offers after caps, explicit proc bridges and temporary pickup limits. These are detailed in the combat, Captain and reference-review documents; performance and economy effects remain unvalidated.

### 13.1 World and layout

- The owner revisits the identical saved home arrangement after login.
- Two independent raids vary arrangement while retaining the same owned section set, Zone identity, attachments, and defense choices.
- Resuming an existing raid never regenerates its layout.
- Every accepted map has safe spawn, reachable finite rewards, reachable Beacon, and valid boss arena.
- Decoration changes cannot affect navigation or difficulty.
- Temporary challenge bands never alter permanent ownership or passive production.

### 13.2 Run behavior

- Boss success is saved before continued combat and survives later death/disconnect.
- No boss kill ends play or offers extraction.
- Overtime continues after bosses; intermediate portal progression carries the same build.
- No stage chest/POI respawns after use.
- All gameplay time, spawns, hits, and score remain frame-rate independent.

### 13.3 Economy and ownership

- Each boss theft occurs once, and post-boss farming never deducts further defender resources.
- Protected and reserved resources cannot be spent or stolen twice.
- Whole-holding sale transfers conserved progress and removes seller eligibility for future runs.
- Both random and targeted gameplay reacquisition work after a sale.
- Max-rank progress never silently vanishes on a purchase.
- Ledger upgrades work without placing any functional building.

### 13.4 Content and competition

- All launch content has a concrete definition, stacking rule, unlock, and test case.
- Proc chains terminate, damage can be attributed, and boss resistance rules are consistent.
- One weekly Voyage board includes every supported biome/tier and rejects non-Voyage runs.
- Poor builds can fail and extraordinary builds can survive longer without exceeding performance budgets.

## 14. Delivery sequence

Use the [milestone backlog](Rogue_Pirates_Implementation_Backlog.md) and [Cursor handoff](Rogue_Pirates_Cursor_Implementation.md) for detailed dependencies and acceptance:

1. M0: platform/performance/replay feasibility and foundations.
2. M1: small deterministic combat sandbox.
3. M2: Saltwind slice, finite exploration, overtime and death after victory.
4. M3: accounts, authoritative persistence, Ledger/holdings and reward integrity.
5. M4: full run content and multi-stage Voyages, Territory expansion/permutations, three mastery presets and raids.
6. M5: verified global/community competition, required Crews/events, skin inventory and manual reward operations.
7. M6: validated channel integrations and sandbox commerce, with live features gated by approvals.
8. M7: release validation, production assets, operational readiness and accepted channel launches.

Tune systems through playtests rather than treating the initial values as proven. Keep numerical balance changes versioned; do not change a live run's rules mid-attempt.

## 15. Validation plan and unresolved production choices

See the [narrative, retention, revenue and production proposal](Rogue_Pirates_Narrative_Retention_Production_Proposal.md) for the 13 September discussion addendum, non-code asset checklist and staged readiness review. Its new recommendations remain pending owner review and do not supersede accepted rules in this GDD.

The companion specifications resolve prototype behavior for former open questions. Unity and platform targets are selected; the technical design proposes networking, account saves and hosting. Remaining production decisions include measured device floor and replay feasibility, vendor/plan selection before deployment, channel eligibility and entitlement behavior, wallet custody model, live fee pricing, naming moderation, support/recovery targets, and event-specific funding/delivery operations. Event rewards are owner-selected as flexible and manually/externally fulfilled.

Owner review is still needed for newly proposed defaults, especially consumable restrictions, retention percentages, raid fees/protection, account progression rates, and overtime reach. Implementation can prototype these values; a public launch should not imply they were individually agreed in conversation.

Playtest tracks: time-to-first-claim, weak-build retry intent, portal departure timing, endgame survival duration, defense fairness across permutations, upgrade affordability after losses, Item reacquisition time, storage saturation, and contribution of account power to raid outcomes. Test at 30/60 FPS and under latency/disconnect conditions.

## 16. Reference and history

The source lineage is the supplied `Rogue_Pirates_GDD_v2_1_REPAIRED.docx`, the design conversation, and the preserved [v3.0 GDD](Rogue_Pirates_GDD_v3_0.md). External references informed earlier choices; this edition's numerical rules are original prototype proposals, not copied claims about current games.

- [Megabonk](https://store.steampowered.com/app/3405340/Megabonk/) — uncertain builds and survival replayability.
- [Megabonk stage reference](https://megabonk.wiki/wiki/Forest) — boss-opened stage progression.
- [Supercell loot guide](https://support.supercell.com/clash-of-clans/en/articles/about-multiplayer-and-trophies-2.html) and [shield guide](https://support.supercell.com/clash-of-clans/en/articles/multiplayer-attacking-defending-2.html) — progression-aware exposure and protection, not adopted exact values.
- [Risk of Rain 2 Scrapper](https://riskofrain2.fandom.com/wiki/Scrapper) and [Survivor.io equipment](https://survivorio.fandom.com/wiki/Equipment) — conversion/merging references; reliable in-run exchange is deferred.

Changes in v3.1: replace functional building placement with the Ledger; combine treasury interaction and boss Beacon; retain a canonical home while rearranging section-bound terrain/decorations/encounters per raid; add concrete combat, content, economy, transaction, and test defaults through the companion specifications.
