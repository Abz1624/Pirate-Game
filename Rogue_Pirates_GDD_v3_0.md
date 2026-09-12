# Rogue Pirates Game Design Document

**Studio:** X2C  
**Version:** 3.0  
**Date:** 13 September 2026  
**Target platforms:** WebGL and Android  
**Genre:** 3D ship-based bullet-heaven action roguelite with persistent territory progression and asynchronous PvP raids

Rogue Pirates turns the map a player conquers into their permanent Territory. Players return for fresh-map Voyages, longer multi-stage runs, home challenges, economic growth, defense authoring, and raids against other players. Random builds create both disappointing attempts and spectacular survival runs. Boss victories secure objectives; death ends the run.

This document consolidates the original v2.1 GDD and the subsequent design discussion. It replaces conflicting v2.1 rules. It is a design specification, not a statement of implemented features. Agreed direction is written as a rule; illustrative numbers, proposed defaults, and unresolved decisions are identified explicitly. No repository implementation was reviewed.

## Contents

1. [Vision and design pillars](#1-vision-and-design-pillars)
2. [Terminology](#2-terminology)
3. [Activities and the return loop](#3-activities-and-the-return-loop)
4. [Run lifecycle and overtime](#4-run-lifecycle-and-overtime)
5. [First conquest and onboarding](#5-first-conquest-and-onboarding)
6. [Territory geography and permanent expansion](#6-territory-geography-and-permanent-expansion)
7. [Multi-stage Voyages](#7-multi-stage-voyages)
8. [Home challenges](#8-home-challenges)
9. [Combat builds and randomized offers](#9-combat-builds-and-randomized-offers)
10. [Persistent Item ownership and merging](#10-persistent-item-ownership-and-merging)
11. [Unlocks and reacquisition](#11-unlocks-and-reacquisition)
12. [Chests POIs and rewards](#12-chests-pois-and-rewards)
13. [Asynchronous raids](#13-asynchronous-raids)
14. [Defense Director](#14-defense-director)
15. [Gold Rum and resource exposure](#15-gold-rum-and-resource-exposure)
16. [Buildings upgrades and storage](#16-buildings-upgrades-and-storage)
17. [Cosmetics ownership and trading](#17-cosmetics-ownership-and-trading)
18. [Payments and monetization](#18-payments-and-monetization)
19. [Leaderboards reputation and matchmaking](#19-leaderboards-reputation-and-matchmaking)
20. [Weapons Logbooks and Items](#20-weapons-logbooks-and-items)
21. [Enemies and bosses](#21-enemies-and-bosses)
22. [Replayability and production strategy](#22-replayability-and-production-strategy)
23. [Crews events and partnerships](#23-crews-events-and-partnerships)
24. [Later systems](#24-later-systems)
25. [Interface and readability](#25-interface-and-readability)
26. [Integrity persistence and ownership boundaries](#26-integrity-persistence-and-ownership-boundaries)
27. [Analytics and validation](#27-analytics-and-validation)
28. [Development sequence](#28-development-sequence)
29. [Open decisions](#29-open-decisions)
30. [Changes from v2.1](#30-changes-from-v21)
31. [References](#31-references)

## 1. Vision and design pillars

**Pitch:** Conquer a pirate world, grow it into your Territory, shape the encounters other pirates must survive, and chase the next extraordinary build across Voyages and raids.

### 1.1 The run is the game

Movement, auto-attacks, XP collection, randomized upgrades, exploration, and boss fights are the shared foundation. Construction and trading provide reasons to return to combat. They do not replace it.

### 1.2 Conquest creates ownership

The first successful conquest secures the actual procedural map as the player's home. Expansion visibly moves the boundary outward and preserves existing geography and investment.

### 1.3 Uncertainty drives another attempt

The desired response to an unsuccessful build is: **“That combination did not work; I want another attempt.”** Some builds should be ordinary; others should feel unusually powerful and lucky. Progression influences possibilities without guaranteeing a completed build.

Do not add reliable ways to repair every poor build. Choices should remain meaningful, but a perfect outcome is not owed on every run.

### 1.4 Victory and run completion are different events

Boss defeat secures the corresponding conquest, expansion, raid, or challenge objective. It does not end combat. Players continue farming or advance through an available portal. Death ends the run and opens results. A death after success does not revoke success.

### 1.5 Ten minutes is a soft stage timer

The standard phase lasts ten minutes before escalating swarm pressure. It is not a hard session limit. Strong builds can continue; multi-stage runs can last substantially longer. A one-stage Voyage remains the shorter option.

### 1.6 Progression and defense have consequences

Stored wealth attracts raiders. Investing in defense preserves resources. Investing in production, storage, Items, or cosmetics competes for resources. Losses can delay future purchases but do not erase completed conquest or upgrades.

### 1.7 High replay value with restrained content production

Prioritize interactions among a small content set, procedural routes, stage progression, enemy patterns, optional bargains, mastery objectives, and player-authored defenses. Extra content should create new decisions rather than merely increase catalog size.

### 1.8 A playable game with optional trading

Gold and Rum are the only gameplay currencies. Fiat and SOL are payment rails. Players may earn tradeable assets through gameplay and sell them to other players, but the game promises neither financial yield nor resale value. All core gameplay must work without a wallet.

## 2. Terminology

- **Territory:** the player's permanently conquered procedural home map.
- **Zone:** a permanent Territory region and biome band. Saltwind is central; Gravewater and Devil's Maw surround it.
- **Stage:** one area of a continuous run, with its own boss, finite exploration rewards, and soft timer.
- **Voyage tier:** a run configuration governing stage count and difficulty. It is distinct from permanent Territory Zone count.
- **Boss Beacon:** a discoverable interactable used to summon the current objective boss.
- **Expansion Beacon:** a conquered Beacon associated with the next permanent expansion activity.
- **Portal:** an exit to the next stage within the same run, unlocked by the current stage boss.
- **Dread Tide:** the dangerous boundary outside the currently playable area.
- **Overtime or swarm phase:** escalating danger after the soft timer expires.
- **Weapon:** an auto-firing attack occupying a limited run slot.
- **Logbook:** a passive stat-progression choice occupying a limited run slot.
- **Run Item:** a stackable modifier collected and used during a run; it disappears when the run ends.
- **Persistent Item holding:** the owned, tradeable collection entry that makes an Item eligible for future drops and stores its accumulated permanent upgrade progress.
- **Item progress or blueprints:** earned inputs accumulated into a persistent holding. They are not extra copies equipped at run start.
- **Consumable:** a separate persistent, single-use resource or advantage for a later run. Its exact effects and mode eligibility remain open.
- **Defense Director:** the casual encounter-authoring system for a Territory.
- **Threat Budget:** per-Zone limit on authored defensive content, presented as readable Skull/Threat points.
- **Defense Chest:** protected defender compensation from part of the Gold raid fee; distinct from attacker reward chests.
- **Crew:** a persistent social group with a name, tag, roster, and identity.
- **Event Tag:** temporary campaign allegiance separate from Crew membership.

## 3. Activities and the return loop

### 3.1 Ordinary Voyages

Repeatable PvE on fresh procedural maps. Players experiment with builds, pursue unlocks and reacquisition, earn Gold/Rum and post-run rewards, unlock deeper Voyage tiers, and compete for weekly single-run kills.

Voyages remain a full progression path alongside raids. Proposed entry default: no Gold entry fee, ensuring a player with an empty treasury can still play and recover.

### 3.2 Home challenges

Repeatable activities anchored to the player's persistent geography. They create reasons to revisit the Territory through boss mastery, routes, encounter variations, and multi-stage challenge extensions. Temporary outer bands can provide additional stages without granting land.

### 3.3 Conquest and expansion

Special runs secure the central Territory and its next permanent Zone. Initial implementation retains their objective structure rather than requiring the Voyage sequence of several stage bosses.

### 3.4 Raids

Players pay a Gold fee to attempt another player's authored Territory. They start at Level 0, build during the run, and steal the permitted treasury amount only by defeating the objective boss. Meaningful unsuccessful attempts can still generate non-stolen rewards.

### 3.5 Territory management

Players collect production, choose investments, author defenses, merge Item progress, acquire content, decorate, and trade. Construction and research can continue while they play any available combat activity.

### 3.6 Example return session

Review defense results, collect production, start an upgrade, choose a desired unlock or Item contract, then enter a Voyage or raid. A player with full resource storage can pursue mastery, Item rewards, build experimentation, cosmetics, or personal bests. Waiting for an upgrade must not be the sole available activity.

## 4. Run lifecycle and overtime

### 4.1 Core sequence

**Start at Level 0 → kill and collect → select randomized upgrades → explore → find and defeat the boss → continue farming or take the portal → die → results.**

The boss may be activated before or after the ten-minute point. Boss defeat does not restart the current stage timer or remove overtime pressure.

### 4.2 Soft timer

Each standard stage has a ten-minute normal phase. After that, danger increases until continued survival becomes untenable. Enemy drops, XP, Gold, and kills can continue during overtime.

The exact escalation curve is tuning data. The desired accelerating pressure may combine enemy strength, speed, composition, attack pressure, elites, and reduced safe space. Avoid health-only escalation and unbounded entity counts that damage performance or readability.

### 4.3 Staying versus advancing

Killing an intermediate stage boss opens a marked portal. Players may delay entering to collect remaining chests, Items, POIs, XP, and kills. Eventually the danger of staying outweighs the benefit. The portal remains a known destination on the minimap.

At the next stage, the ten-minute stage timer restarts against a higher baseline difficulty suitable for the carried build. The player does not reset to Level 0. At the final stage, no further progression portal is required; combat continues after the final boss until death.

### 4.4 Death and secured success

No designed Return Home or extraction option ends a successful run. Players may intentionally allow enemies to kill them. Results distinguish death before an objective from death after that objective was completed.

Boss-secured ownership, expansion, raid theft, and qualifying completion credit are recorded when earned. The final results screen presents them after death. Accumulated run rewards use the applicable survival/failure rules, which require tuning separately from secured milestones.

Application closure, disconnects, suspension, and interrupted-run recovery need explicit policies before launch; they must not silently become an exploit or revoke already-recorded boss success.

## 5. First conquest and onboarding

1. Reserve a procedural seed and drop the new player at a valid random spawn inside the central Saltwind square.
2. Begin at Level 0 with two Weapon slots and two Logbook slots.
3. Proposed tutorial default retained from v2.1: guarantee Deck Cannon on the first run. Subsequent starts draw from the eligible Armory Set.
4. Restrict offers to beginner-approved content. Introduce movement, auto-attacks, XP, three-choice upgrades, chests, and the minimap.
5. Reveal Beacon guidance increasingly as the target boss time approaches.
6. Defeat Barnacle King to secure the Territory and display **TERRITORY CLAIMED** during play.
7. Continue the run until death. The results acknowledge the conquest, then Territory management becomes available.

Conquest unlocks construction, production/storage, the path toward a third Weapon slot purchase, and raids after an introductory protection/tutorial state. The conquered Beacon becomes relevant to permanent expansion.

The seed is preserved on successful conquest. Failure-before-first-conquest seed reuse and tutorial retry behavior remain open. Initial Item defaults must support enough variety before the player can earn specialized holdings.

## 6. Territory geography and permanent expansion

### 6.1 Concentric square structure

- **Zone 1 — Saltwind Reach:** central square; tropical waters, reefs, beaches, wrecks, and pirate enemies.
- **Zone 2 — Gravewater:** a surrounding square band; cursed fog, drowned ruins, ghost ships, and spectral variants.
- **Zone 3 — Devil's Maw:** the next surrounding square band; volcanic waters, burning wrecks, red storms, and infernal variants.

An expansion adds area around the existing perimeter. It does not replace the inner map or erase buildings. The square is the broad progression boundary; coastlines, obstacles, and local navigation may remain irregular and natural.

The initial standard permanent footprint ends at three Zones. More Voyage stages or temporary home-challenge bands do not automatically add permanent Territory tiers.

### 6.2 Expansion run

An eligible owner starts a fresh Level-0 expansion attempt. The next outer band becomes playable for the attempt. The player builds power, locates the new Zone Beacon, and defeats its boss. Boss defeat permanently claims the band and secures associated progression gates. Combat continues until death.

Failure before the required boss does not claim the band. Existing owned geography and completed upgrades remain intact. Geometry generation timing, provisional-band persistence across retries, and expansion entry requirements remain open.

### 6.3 Dread Tide

The boundary visually distinguishes playable space through storms, water color, fog, waves, warnings, sound, and a directional cue back to safety. It advances outward when land is claimed.

Original boundary-damage tuning hypothesis, not a committed balance rule:

- First 0.75 seconds outside: warning grace.
- 0.75–2 seconds: 6% maximum Hull per second.
- 2–4 seconds: 12% maximum Hull per second.
- Beyond 4 seconds: 25% maximum Hull per second.

Boundary enforcement prevents bypassing the intended map. It is distinct from overtime escalation, even if their presentation shares storm elements. Temporary home-challenge boundaries apply only within that challenge.

## 7. Multi-stage Voyages

### 7.1 Tier progression

Initial proposed structure per biome:

- Tier 1: one stage.
- Tier 2: two stages connected by a boss-unlocked portal.
- Tier 3: three stages with escalating difficulty and a final boss variation.

The final required boss clear unlocks the next tier where available, even though death occurs later. Precise unlock conditions and biome access gates require confirmation. Voyage tiers are independent of how many Zones a player owns.

### 7.2 Carryover

Carry run level, equipped Weapons, Logbooks, collected Item stacks, and unspent run Gold between stages. Generate fresh geometry, encounters, chests, and POIs using the selected biome's content.

Health restoration, chest-price carryover, level-up offer caps, and other transition adjustments are open tuning decisions. Do not assume a full heal or economy reset merely because the stage timer restarts.

### 7.3 Finite exploration

A fixed allocation of chests and POIs spawns at stage initialization. These do not replenish during that stage. New stages receive fresh allocations. Enemy drops continue in overtime. Finite exploration rewards and rising danger motivate portal use without prohibiting additional farming.

Boss drops and earned encounter rewards must be distinguished from replenishing map chests; neither should respawn a consumed POI indefinitely.

### 7.4 Session length

The ten-minute structure is per stage and is a soft limit. Tier 3 plus overtime can exceed thirty minutes. Communicate stage count before entry. Preserve one-stage runs as a shorter option without imposing a hard end on exceptional builds.

## 8. Home challenges

Home challenges reuse the persistent map, with different routes, encounter arrangements, mastery goals, and boss variations. Multi-stage extensions can reveal temporary outer square bands.

- Temporary bands provide challenge space, not permanent building slots or production.
- Ending a challenge removes access to its temporary extension; dedicated expansion remains the route to ownership.
- Temporary geography must not mutate the owner's published raid layout.
- Boss and challenge success can be recorded before death.
- Fixed starting chests/POIs and stage escalation apply to standard staged challenges.

Examples include faster boss kills, limited Weapon slots, no purchased chests, specific build milestones, and harder combinations of existing enemies. Challenge-specific rules and rewards should be explicit before entry.

**Test My Territory** is a separate defense-testing function. Reward restrictions must prevent publishing an intentionally weak defense and repeatedly farming it. Home challenges and self-tests do not enter the Voyage kill leaderboard.

## 9. Combat builds and randomized offers

### 9.1 Three-layer build

- Weapons determine attacks.
- Logbooks provide passive scaling.
- Items add rules, procs, conditional effects, economy choices, and synergies.

Every new run begins at Level 0. Stage transitions within that run retain the build. Account progression provides options and modest benefits, not a finished build.

### 9.2 Level-up offers

Each level-up briefly pauses gameplay and presents three randomized offers: new Weapon, equipped Weapon upgrade, new Logbook, or equipped Logbook upgrade. Suppress new Weapons/Logbooks when their slots are full unless a future replacement rule is explicitly added.

Offers may roll Common, Uncommon, Rare, Epic, or Legendary quality. Quality affects upgrade strength or axes; probabilities and stacking rules remain balance data.

Refresh, Skip, and Banish can be earned through account progression in limited amounts. Their purpose is some agency, not deterministic builds. Consumable versions must not silently create unlimited rerolls.

### 9.3 Slot progression retained from v2.1

- New account: two Weapon and two Logbook slots.
- Claim Saltwind: third Weapon slot becomes purchasable with Rum.
- Claim Gravewater: fourth Weapon and third Logbook slots become purchasable with Rum.
- Claim Devil's Maw: fourth Logbook slot becomes purchasable with Rum.

Slot purchases are permanent capacity upgrades; they do not fill the slots automatically.

### 9.4 Eligible pools

Armory and Logbook Sets influence offer eligibility with minimum breadth. The starting Weapon is randomly selected from the active eligible pool outside the proposed first-run tutorial exception.

Run Item eligibility depends on baseline starter content and currently owned, quest-qualified persistent Item holdings. Preserve an unremovable starter baseline and minimum breadth so selling specialized holdings cannot reduce every chest to a guaranteed synergy. The exact enforcement rule needs definition and must allow continued play after selling holdings.

Accumulating more progress for an Item creates no extra weighted entries in the drop pool. Permanent Item power improves modestly, but its appearance remains uncertain.

### 9.5 No reliable build rescue at launch

The previously discussed guaranteed Smuggler's Exchange is deferred. First prove that ordinary offers, exploration, and drops create enjoyable imperfect builds and exceptional combinations. Any future exchange should preserve scarcity, uncertainty, and a meaningful sacrifice; it must not become an expected repair step in every run.

## 10. Persistent Item ownership and merging

### 10.1 One holding per Item type

Each player has one combined persistent holding for an owned Item type. Earned duplicate progress and purchased holdings add to that holding. The system does not model separately equipped permanent copies of the same Item.

The holding contains ownership and accumulated upgrade progress. A displayed level is derived from progress using the upgrade curve. Permanent upgrade rank is distinct from an Item's drop rarity and the number of temporary copies stacked during a run.

### 10.2 Combining progress

Add underlying earned progress, not displayed levels. Example with arbitrary units: a 30-point holding combined with a purchased 20-point holding becomes one 50-point holding. Recalculate its displayed level. If the buyer owned none, the purchased holding transfers intact.

This preserves value under nonlinear level costs. Consolidation reduces holding count but does not, by itself, destroy accumulated upgrade value.

### 10.3 Selling

The player sells the entire accumulated holding for that Item type, including its upgrade progress. A completed sale removes that Item from the seller's future eligible pool unless it belongs to the protected baseline. The buyer adds the transferred progress to their existing holding, or receives it unchanged if they had none.

Quest completion remains account history and is not sold or reset. The buyer's gameplay usability must still respect their own applicable quest/progression gates. Sealed versus purchase-blocked handling is unresolved.

Do not carry forward the earlier rule that selling only spare blueprints leaves permanent license access. This version intentionally transfers ownership and access together. Partial sales are not part of the agreed model.

### 10.4 Upgrading future drops

Permanent progress gives small improvements to the Item when it randomly appears in future runs. It never grants the Item at run start. The total advantage across all ranks must remain modest, not just each individual increment.

Use this progression to replace part of generic combat research, avoiding excessive stacked account power. Powerful run synergies and accumulated temporary stacks remain the source of exceptional runs.

Rum can fund upgrade/merge actions. Exact copy requirements, whether progress is applied automatically or through a paid upgrade action, compatible off-type inputs, maximum rank, and overflow handling remain open. The UI must distinguish “level 3 of 10” from “3 of 10 progress toward next level.”

### 10.5 Supply and demand

Repeatable acquisition creates new tradeable supply. Additive merging preserves that progress rather than removing it. Once players reach their desired upgrades, demand may fall; ordinary Items can become inexpensive and accessible to newcomers. This is acceptable.

Do not introduce expiry, endlessly increasing power caps, or unnecessary grind simply to defend resale prices. No Item has a promised SOL value.

## 11. Unlocks and reacquisition

### 11.1 Initial unlock path

Retain the core **quest → purchase with Rum → eligibility** structure. Objectives teach mechanics and encourage exploration and different builds. For tradeable Items, completed quests establish qualification; actual ownership establishes ongoing eligibility.

Weapons and Logbooks retain their initial unlock structure. Extending the combined tradable-holding model to them is not yet agreed.

### 11.2 Baseline playability

Starter Items keep the game playable when specialized holdings have been sold. This prevents a player from selling their way out of basic combat, but is not sufficient for reacquiring a particular desired Item.

### 11.3 Random rediscovery

Earned post-run chests can award permanent Item progress from the player's quest-qualified reward collection, including previously sold Items. This permanent reward pool is separate from the current run's owned-Item drop pool.

### 11.4 Targeted reacquisition

Repeatable Item contracts provide a known route to desired progress through gameplay. A contract cannot require the exact missing Item as its sole way to complete. Existing Weapons or broadly available mechanics can support the objective.

Trading supplies speed and choice. Gameplay provides an independent reacquisition path. The original quest need not be repeated after every sale.

### 11.5 Quest families

- Enemy mastery: kills, charge avoidance, elite clears.
- Weapon mastery: multi-kills, burn uptime, chains, and explosion interactions.
- Boss mastery: speed, Hull thresholds, or limited slots.
- Exploration: discover POIs, find the Beacon, or open a chest threshold.
- Raid challenges: credible equal-tier victories and authored encounter clears.
- Greed challenges: carry unspent Gold, spend on chests, or clear without chests.

Avoid circular unlocks and raw grind requirements that add duration without teaching or changing play.

## 12. Chests POIs and rewards

### 12.1 In-run economy

Enemies and exploration provide run Gold. Spend it on the finite chests generated at stage start to gain temporary Items. Keeping Gold can improve later resource earnings, while spending improves current survival.

Proposed accounting default: the run has a separate balance of the same Gold currency, with no treasury top-ups. This avoids turning persistent savings into unrestricted run purchasing power without introducing a third currency. Confirm banking and failure retention before implementation.

Chest prices can increase with purchases or context; higher-risk content can improve reward quality. Luck influences rarity without guaranteeing high-quality drops. Stage-transition pricing remains open.

### 12.2 Three reward layers

1. **Run drops:** XP, Gold, and temporary Items support current play.
2. **Earned post-run rewards:** resource payouts, quest progress, permanent Item progress, cosmetics, and eligible consumables can reward meaningful attempts, including failures.
3. **Boss-secured rewards:** conquest, expansion, raid theft, tier progression, and qualifying challenge success remain earned after death.

Run Item stacks never automatically become equivalent permanent holdings on death. Permanent rewards use their own rules and pool.

### 12.3 Earned reward chests

Post-run chests should provide ordinary useful outcomes with occasional exciting rare results that can make even a failed attempt memorable. Do not make only the jackpot worthwhile.

Proposed controls: earn eligibility through real encounter progress; improve rewards with deeper achievement; add value for boss success; prevent entering-and-dying loops or indefinitely spawned summons from becoming the best reward source. Chest eligibility, payout rates, duplicate handling, and opening flow remain tuning decisions. No paid key or opening charge is specified.

### 12.4 Consumables

Consumables are an accepted post-run reward category in addition to resources, cosmetics, and Item progress. They are distinct from permanent holdings and temporary run Items. Examples previously suggested include a treasure-encounter map or an extra chest choice; these are candidates, not committed content.

Raid and leaderboard eligibility, usage limits, stockpiling, trading, and direct-sale rules remain unresolved. Do not infer unrestricted competitive use from the decision to include consumables.

## 13. Asynchronous raids

### 13.1 Target selection

Show owner identity/flag, Territory tier, approximate difficulty, lootable Gold/Rum, Gold entry cost, protection status, and relevant reputation. Decorations should be visible on arrival and during exploration.

No SOL fee is required for ordinary raids. The defender need not be online.

### 13.2 Start and snapshot

The attacker starts at Level 0 using eligible content and allowed account progression. The raid captures the relevant map, authored encounters, boss configuration, progression, loot entitlement, and version. Spawn at a valid randomized entry node.

Initial raids retain the highest-owned-Zone boss objective from v2.1. They do not automatically inherit Voyage-style sequential stage bosses. Attackers can traverse the owned Territory; highest-Zone boss defeat secures the raid. Travel time across larger Territories requires testing.

### 13.3 Success and continued farming

At the objective boss kill:

1. Verify the success and allowed loot entitlement.
2. Deduct the permitted stolen amount from the defender exactly once.
3. Record that amount as secured attacker loot.
4. Start applicable defender protection without waiting for attacker death.
5. Allow continued survival and farming until death.

Additional farming creates game rewards, not repeated withdrawals from the defender. Later attacker death does not undo the theft. Results present the secured theft alongside other earned rewards.

Illustration only: 10,000 lootable Gold with an allowed 2,500 theft results in one 2,500 transfer at boss defeat, even if the attacker survives for twelve more minutes. These numbers do not commit the final loot formula.

### 13.4 Unsuccessful attempts

Death before the objective boss means no treasury theft. Meaningful combat can still earn progression, retained resources according to the banking policy, and eligible post-run chests. Failure rewards are supplied by the game; they do not subtract from defender savings.

The v2.1 manual-retreat success flow is not retained. Disconnect/abandonment policy remains an integrity decision.

### 13.5 Entry cost and defender compensation

Gold Raid Cost discourages spam and makes target selection meaningful. The v2.1 hypothesis is 50% removed as a sink and 50% paid into a protected Defense Chest. This split, fee formula, and earning cap remain unconfirmed tuning values.

Compensation must not support profitable collusion. Defense's main economic value is preventing theft; compensation is supplementary.

### 13.6 Protection

Use meaningful post-loss protection, cumulative-loss safeguards, repeat-target restrictions, and concurrency rules. Wealth can attract attacks, but the owner must be able to save toward upgrades. There is no committed “recover every loss in one successful session” rule and no final hard per-raid cap.

Snapshot reservation and simultaneous-attack settlement must prevent several attacks draining the same promised balance. Shield duration and whether shields block new entry versus existing attempts remain open.

## 14. Defense Director

### 14.1 Pick Pattern Publish

Generated valid Encounter Slots let players select an Enemy Card, one compatible Pattern, and optional boss/hazard choices within the Zone's Threat Budget. The game chooses valid spawn coordinates, spacing, and pacing. Casual players do not script timings or place hundreds of objects.

Quick Setup supplies a valid recommendation. Custom Setup allows a few high-impact changes. Target a comprehensible first setup in under a minute.

### 14.2 Initial cards and patterns

Reuse Cannonboats, Sharks, Swordfish, Hammerheads, Mantas, Kraken Spawn, and Reef Crabs. Patterns include Patrol, Ambush, Pincer, Reinforcements, and Swarm. Show only compatible patterns for each card. Later Zones add themed variants and hazards.

Boss Traits can alter adds, armor windows, or arena pressure. Progression should broaden defensive choices and combinations while keeping numerical difficulty legible.

### 14.3 Threat Budget

Enemy Cards, hazards, and boss traits consume budget. Territory upgrades and expansion can unlock more capacity and slots.

Illustrative Saltwind setup from v2.1: 10 Threat; Shark Ambush 2, Cannonboat Pincer 3, Crab Reinforcements 2, boss adds trait 3. Costs and slot counts require balance validation.

### 14.4 Fairness and incentives

Validate navigation, Beacon access, spawn distance, boundaries, and Threat totals. Avoid unavoidable attacks spawning directly on the raider. A valid geometry or budget is not proof that a combination is fair or enjoyable; test overlapping slows, charges, and area denial.

Provide difficulty estimates, defense history, and Test My Territory. Owner-clear requirements can be considered later rather than required at launch. Reward credible defense performance, not simply receiving attacks.

Late possibilities include featured Territories, difficult defenses, route summaries, heatmaps, and creator recognition. Do not make precision editing necessary for effective defense.

## 15. Gold Rum and resource exposure

### 15.1 Gold

Sources: runs, resource production, quests, boss rewards, raids, and eligible Defense Chest income.

Uses: construction, storage, production, Territory utility, War Room/defense progression, raid entry, run chests, and cosmetics. Exact immediate defense-unlock versus timed-upgrade rules need definition.

### 15.2 Rum

Sources: production, bosses, quests, raids, selected run rewards, and milestones.

Uses: quest-qualified content purchases, slot upgrades, Item progress upgrades/merging, remaining Shipyard progression, cosmetics, and later agent costs.

Rum is pirate-themed; Supplies remains a naming fallback if audience requirements warrant it. The exact passive-production versus active-play contribution is open.

### 15.3 Exposure follows stored wealth

Upgrading an empty storage does not itself increase losses. Holding more resources in the larger storage can increase loot exposure and target attractiveness. Wealth status and credible defenses can support prestige.

Proportional exposure is a candidate model, with shields and cumulative-loss protection. The original 20–30% of lootable resources, centered on 25%, remains a historical tuning hypothesis rather than a final formula. Define what is lootable before applying any percentage; total savings and exposed loot are different quantities.

Do not automatically award reputation per incoming raid. Arranged attacks must not become a prestige faucet.

### 15.4 Desired economic behavior

Saving, spending, defending, and expanding should all matter. Active players can continue progressing despite losses. Stronger defenses should measurably preserve resources. Rich Territories should remain worthwhile targets. Neither perfectly protected hoarding nor repeated unrecoverable draining is the goal.

Trading is optional. The game emits no proprietary token, routine SOL payout, staking yield, or guaranteed financial return. Gold/Rum are not directly redeemable at a fixed SOL rate.

## 16. Buildings upgrades and storage

### 16.1 Structures

- Trade Dock: Gold production.
- Distillery: Rum production.
- Treasure Vault and Rum Cellar: respective storage.
- Shipyard: bounded ship/account progression and remaining research.
- Armory: Weapon access and purchases.
- Captain's Archive: Logbooks, Items, and collection management.
- War Room: Defense Director, progression, and presets.
- Lookout Post: raid history and later route/replay tools.
- Market Dock: trading access.
- Flag Mast, Trophy Wreck, docks, and decorative props: identity and display.

Structures can appear as outposts, platforms, ships, wreck settlements, and docks across the Territory. The design does not require a separate island village.

### 16.2 Progression within Zones

Conquest unlocks ranges of available upgrades; it does not grant them automatically. Players choose combat, defense, production, storage, or appearance within the current range.

Illustration only: Saltwind permits levels 1–3, Gravewater 4–6, Devil's Maw 7–9. Actual counts are open. Players should not need every prior upgrade maximized before attempting the next conquest. More aggressive players may expand early; others may invest first.

### 16.3 Timed versus immediate spending

Timed investments include construction, meaningful resource/storage upgrades, defense infrastructure, and remaining research. Immediate purchases include cosmetics and, as a proposed default, already-qualified content licenses and slot purchases.

Do not add another wait to every quest → save → purchase reward. Small Item upgrades replace part of generic research instead of simply layering more power systems.

### 16.4 Timers and continuous play

Retain meaningful timers from the first economy tests. Original hypotheses: minutes for introductory upgrades, roughly 10–60 minutes in early progression, then hours where justified. Multi-day waits are not an initial requirement.

Timers run offline. Existing functionality remains usable at the prior level while an upgrade runs. Upgrades do not block all rewarding runs. Separate construction and research queues are a proposed way to maintain investment choices; counts and concurrency remain open.

### 16.5 Full storage

Storage expansion increases saving room and access to larger purchases but cannot solve overflow indefinitely. Each storage upgrade must be affordable using preceding capacity.

Provide useful immediate purchases, particularly cosmetics, while queues are busy. Show storage/overflow clearly before entry. Missing potential earnings due to capacity and losing existing savings to raids are separate pressures; balance them together.

No automatic overflow conversion or guaranteed resource buyback is specified. Players can still pursue Item progress, quests, personal bests, and mastery at full storage.

## 17. Cosmetics ownership and trading

Gold/Rum cosmetics are immediate, optional spending outlets and may be traded for SOL. Do not impose an account-bound-only cosmetic policy. Players can display their spending or transfer the acquired asset to another player.

Examples: sails, flags, ship skins, decorative wrecks, dock themes, trophy displays, portraits, emotes, and upgraded visual variants. Reuse art through meaningful variants where possible. Cosmetics grant no defensive advantage, block no routes, and obscure no essential telegraphs.

Raiders see decorations on arrival and during exploration. A visit/share presentation is desirable for social value; its feature scope remains open.

### 17.1 Market behavior

First-party cosmetic purchases can remove Gold/Rum from circulation. Resales transfer the cosmetic and SOL between players. Supply, player tastes, and rarity affect whether buyers exist; low prices or unsold ordinary assets are acceptable outcomes.

No game-funded guaranteed buyback or fixed Gold-to-SOL conversion. Resource-funded cosmetics can move wealth outside raid-exposed storage; monitor this interaction without banning the desired trading behavior.

### 17.2 Trading categories

Core intended categories include persistent Item holdings with their progress, eligible cosmetics, and applicable Item progress assets. Future candidates include Weapon variants, special chests, and agents. Their eligibility is not automatically approved by the Item-holding model.

Marketplace fees retain the original 2.5–5% hypothesis, pending product decisions. Trading should function without a proprietary token. Wallet connection occurs at point of need.

## 18. Payments and monetization

Players can create an account, conquer, progress, raid, and unlock content without understanding wallets. Fiat and SOL are optional payment routes for supported first-party purchases; SOL is the intended marketplace settlement asset.

Potential revenue includes cosmetics, appropriate content, timer acceleration, marketplace fees, and partner campaigns. Speed-ups do not bypass conquest, boss, quest, or slot gates. Players can complete the same eligible timed upgrade by waiting.

Recognize that faster upgrades and purchased upgraded Items can create present-day competitive advantages even if maximum power is identical. Matchmaking, bounded Item power, and analytics must reflect actual progression. Do not describe Level 0 or a common power ceiling as proof of equal starting strength.

Paid consumable use and paid random reward products are not specified by the accepted earned-chest design. Platform checkout and marketplace requirements need separate validation before release.

## 19. Leaderboards reputation and matchmaking

### 19.1 Weekly Voyage kill leaderboard

One shared weekly leaderboard ranks each player's best single-run enemy kill count. Count kills across all stages and continued survival after bosses, until death.

- No cumulative weekly kill score.
- No separate biome or tier leaderboards.
- New and experienced accounts share the board.
- Longer and deeper runs may have greater scoring opportunity; this is accepted.
- Some biomes being better scoring choices is acceptable and does not require splitting the board.
- Player-authored raids, customizable home challenges, and self-tests are excluded from the Voyage kill competition.

Show the run's biome, tier, stages, build, and progression context alongside scores. This helps players understand what is possible. Do not normalize away progression merely to create equal loadouts; the shared progression competition is intentional.

Consumables, ties, weekly boundaries, midweek balance changes, interrupted runs, and leaderboard rewards remain open. Integrity validation is required before a score is accepted.

### 19.2 Raid and defense status

Profiles may show raid victories, defense results, wealth status, and achievements without separate competitive boards at launch. Dedicated raid competition is deferred pending credible scoring and abuse controls.

Bounty remains reputation, not financial yield. Favor accomplishment and credible defensive success over raw incoming attack count. Exact Bounty formula and relationship to event scoring remain open.

### 19.3 Raid matchmaking

Consider Territory Zone count, defense progression, Threat used, attacker slot progression, Item power, raid rating, recent performance, and loot band. Avoid newly claimed Territories facing fully optimized veterans as routine matches. Players can deliberately choose harder targets for attractive loot.

Shared Voyage leaderboard rules do not imply unrestricted raid matchmaking. Consumable accounting remains unresolved.

## 20. Weapons Logbooks and Items

The following preserves the original content foundation. Effects and unlock objectives are design candidates, not final numerical balance. Initial shipping subsets remain smaller than the complete catalog.

### 20.1 Initial Weapons

- **Deck Cannon:** reliable nearby-target cannonballs; damage, projectile speed, quantity, pierce, reload. Default starter.
- **Broadside Battery:** volleys perpendicular to ship heading; volley size, quantity, reload, spread, knockback. Unlock concept: Cannonboat mastery, originally 150 kills.
- **Powder Kegs:** explosive drops/throws; radius, quantity, fuse, damage, chain reactions. Unlock concept: chest/explosion objective, originally 20 chests or environmental barrels.
- **Flying Cutlasses:** orbiting or returning blades; blade count, radius, speed, critical chance, pierce. Unlock concept: Swordfish mastery, originally 200 kills.
- **Flamethrower:** close cone with burn; width, range, stacks, duration, tick rate. Unlock concept: Barnacle King plus fire-themed challenge.
- **Lightning Mast:** chaining strikes; chains, damage, range, cooldown, shock. Unlock concept: Manta/storm challenge, originally 100 Manta kills.

### 20.2 Expansion Weapons

- Harpoon Launcher: heavy linear single-target/pierce, tied to Shark/Hammerhead mastery.
- Mortar Deck: long-range area damage, tied to ranged encounter clears.
- Anchor Flail: heavy orbit and knockback, tied to close-range survival.
- Ghost Cannons: spectral piercing behavior, tied to Gravewater conquest.
- Chainshot: slowing/control against fast enemies.
- Siren Bell: periodic radial damage/control, tied to Siren encounters.

Prioritize missing tactical roles and compatibility with existing synergies before expanding the roster.

### 20.3 Foundational Logbooks

- Gunner's Log: damage.
- Powdermaster's Log: attack speed/cooldown.
- Navigator's Log: movement speed.
- Carpenter's Log: maximum Hull.
- Lookout's Log: critical chance.
- Quartermaster's Log: Gold gain.
- Scavenger's Log: pickup range.
- Cartographer's Log: XP gain.

The original starter proposal explicitly includes Gunner, Navigator, Carpenter, and Quartermaster. Exact default access for the remaining foundational Logbooks is open.

Unlockable candidates: Ironclad (armor; survival challenge), Fortune (luck; chest challenge), Tidecaller (duration; lingering-effect mastery), Admiral (elite/boss damage; speed clear), Plunderer (resource payout; raids/objectives), and Chaos (randomized stat increases; mixed challenge).

### 20.4 Item catalog

Initial development target remains a focused 12–16 Items with approximately 6–8 beginner defaults; choose the subset to support coherent builds. The full original candidate set follows.

**Common**

- Lucky Doubloon: Luck/chest quality; chest-opening unlock concept.
- Spare Planks: maximum Hull; cumulative healing/repair objective.
- Gunner's Glove: projectile speed; cannon mastery.
- Powder Monkey: attack speed; firing milestone.
- Shark Tooth Necklace: bonus against healthy targets; Shark mastery.
- Manta Sail: movement and a near-miss speed burst; Manta mastery.
- Reef Carapace: armor/contact mitigation; Crab mastery.
- Crow's Nest: pickup range and Beacon discovery; POI exploration.

**Uncommon**

- Skeleton Key: chance of free chest opening; paid-chest objective.
- Grapeshot Pouch: Deck Cannon impact fragments; cannon-level milestone.
- Double-Fuse Cord: explosions trigger nearby kegs; explosive multi-kills.
- Serrated Scabbard: Cutlass bleed; Cutlass mastery.
- Dragon's Breath Nozzle: improved burn duration/stacks; burn milestone.
- Copper Conductor: improved lightning chains; shock/chain milestone.
- Broadside Drum: multi-target hits shorten cooldown; Broadside mastery.
- Hammerhead Helm: knockback resistance and conditional ramming; Hammerhead mastery.

**Rare**

- Smuggler's Ledger: costlier chests with improved rarity; chest-spending objective.
- Black Spot: more elites with better rewards; elite mastery.
- Dead Man's Compass: earlier Beacon/POI reveal with added pressure; exploration speed.
- Kraken Ink Sac: elite kills create slowing ink; Kraken Spawn mastery.
- Cursed Coin: more Gold with a defensive drawback; carried-Gold objective.

**Epic**

- Storm in a Bottle: repeated lightning procs trigger a storm; chain milestone.
- Devil's Match: burning enemies explode; fire challenge.
- Queen Anne's Fuse: diminishing recursive explosion chains; chain-reaction mastery.
- Barnacle Crown: surrounded-state defensive/offensive stacks; boss-pressure challenge.
- Jolly Roger: kill streak increases damage, heavy hits remove part of the bonus; aggression mastery.

**Legendary**

- Leviathan Heart: periodic tentacle strike near enemy groups; final-conquest/rare-reward concept.
- Captain's Last Rum: prevents fatal damage once and triggers frenzy; critical-Hull boss challenge.

Items may use linear stacking, diminishing probabilities, thresholds, and conditional synergies. Avoid accidental infinite loops and unreadable proc chains. A second-life effect postpones final death; it does not change the death-ended run model.

## 21. Enemies and bosses

### 21.1 Saltwind foundation

- Cannonboat: ranged pressure with telegraphed fire.
- Shark: direct chaser.
- Swordfish: windup and fast line charge.
- Hammerhead: heavy charge/displacement.
- Manta Ray: sweeping lanes and movement denial.
- Kraken Spawn: tentacles, slowing, grabs, or area denial.
- Reef Crab: armored tank and single-target pressure.
- Barnacle King: large, readable armored boss with adds and arena pressure.

### 21.2 Gravewater

Reuse silhouettes with a meaningful behavior variation: spectral-phase Sharks, double-dash Swordfish, Ghost Cannonboats, slowing-wake Mantas, intermittently protected Crabs, and more Kraken Spawn. Siren is a candidate new control/support silhouette.

**Drowned Admiral:** cursed flagship with broadsides, fog phases, ghost adds, cursed wakes, and vulnerability windows. Its conquest secures Gravewater and associated progression.

### 21.3 Devil's Maw

Candidates: burning-wake Sharks, explosive Swordfish, mortar/fire Cannonboats, lava-pool Crabs, flame-trail Mantas, shockwave Hammerheads, and enhanced tentacle encounters. Fire Eel is a candidate new silhouette.

**Infernal Kraken:** tentacle attack zones, burning water, arena contraction, grab/slam telegraphs, and frenzy. Its conquest secures the final standard Territory band and related unlocks.

### 21.4 Stage boss variations

Voyage tiers need increasingly challenging bosses, but not necessarily a new model for every stage. Reuse existing bosses with readable traits, phase combinations, and encounter context. Exact stage-boss assignments are open. Preserve learnable enemy language across themes.

## 22. Replayability and production strategy

### 22.1 Priorities

1. Interactions among existing Weapons and Items: burn, explosions, lightning, close-range play, mobility, and greed.
2. Optional run bargains: activate a finite extra elite encounter for a better reward, using existing content.
3. Mastery goals that encourage different builds and routes.
4. A small collection of meaningful encounter modifiers shared across appropriate activities.
5. Multi-stage progression that lets a build develop without restarting.
6. Defense Director combinations as player-authored variety.
7. Later ship specializations with genuine tradeoffs, such as durable close-range versus fast fragile play.

### 22.2 Constraints

Combinations require testing even when they reuse art. Favor a few behavior-changing modifiers over many minor percentage changes. Do not rely on procedural geometry alone to create replayability.

Do not flatten all runs into similar outcomes through guaranteed exchanges, unrestricted pool pruning, or unlimited consumables. Account Item upgrades remain small in total. The exceptional build should feel discovered during play.

No extra biomes are required merely to support every new stage or challenge. Finite initial chests/POIs, changed routes, enemy combinations, and progression timing supply much of the variation.

## 23. Crews events and partnerships

Crews provide persistent names, 2–5 character tags, flags/emblems, rosters, profiles, and social identity. They have no mandatory combat-stat bonus in the initial design.

Event Tags let players represent a temporary partner/community without leaving their Crew. Selection should lock for the campaign or have a meaningful cooldown. Actions can contribute to individual, Crew, and campaign goals using explicit scoring rules.

Later events may use verified boss kills, raid results, treasure, mastery, or survival objectives. This does not change the one shared weekly Voyage kill leaderboard into multiple default biome/tier boards.

Partnership candidates include themed flags, existing-gameplay campaigns, featured Territories, approved community tags, and verifiable leaderboard exports for external allowlists or partner rewards. Reuse core systems rather than building a separate mode for every partnership.

Partner-provided prizes are not a routine SOL emission promise. Crew sizes, event aggregation, anti-farm caps, rewards, and production scope remain open.

## 24. Later systems

AI agents can eventually scout, accompany players, defend, or complete bounded resource missions. Training/deployment uses game resources, primarily Rum. Tradeable agent ownership is a later possibility; no promised yield.

Co-op boss missions, agent reputation, richer social systems, and creator discovery are later extensions. On-foot ports/islands are explicitly outside the core design; current progression must not depend on them.

Davy Jones's Locker could return as protected non-yield storage if testing justifies it. It is not an agreed required feature and is never a staking system.

## 25. Interface and readability

- Minimap marks an opened portal clearly. Beacon discovery and the extent of other markers require UI tuning.
- Distinguish stage timer, overtime state, total run time, and kill count.
- Show boss success immediately without ending play.
- Results distinguish secured success from the eventual death that ended survival.
- Show finite chest/POI opportunities without implying replenishment.
- Territory view distinguishes permanently owned square bands from temporary challenge space.
- Defense setup prioritizes a few readable decisions and Quick Setup.
- Item screens distinguish quest qualification, ownership, accumulated progress, derived upgrade rank, and temporary run stacks.
- Marketplace preview explains that selling transfers the entire holding and removes future pool access.
- Decorations remain visible without compromising enemy telegraphs or navigation.

## 26. Integrity persistence and ownership boundaries

Reward-relevant results are server-authoritative. Validate run seed, spawn, XP, rolls, kills, boss events, time, stage transitions, finite encounter claims, resource calculations, and Territory snapshot version. A client cannot simply claim a boss kill or leaderboard score.

The ownership system must preserve progress through trades and merging, apply quest usability gates, and prevent duplicated holdings or rewards. Selling during an active run, changing pools mid-run, marketplace escrow, overflow, and disconnect recovery require explicit rules.

Core run simulation, Gold/Rum, XP, RNG, production, defense configuration, and raid validation remain ordinary authoritative game state. Optional onchain ownership and settlement apply only where useful for tradeable assets. Technical implementation is a later phase.

Specific exploit scenarios include collusive defense fees, trivial self-tests, endless summon farming, repeated failure chests, multiple simultaneous treasury claims, score tampering, and reward reacquisition loops. Countermeasures should address real incentives without removing the agreed gameplay.

## 27. Analytics and validation

### 27.1 Core enjoyment

Measure repeat attempts, runs per session, early-death rates, build diversity, perceived agency, spectacular-build frequency, boss success, first-conquest conversion, and willingness to try another combination. Test whether bad runs feel disappointing but understandable rather than predetermined.

### 27.2 Stages and overtime

Measure stage duration distributions, time after ten minutes, boss timing, portal timing, remaining exploration rewards at departure, deaths before reaching an opened portal, deeper-tier conversion, and post-final-boss survival. Validate performance under the strongest expected builds.

### 27.3 Territory and defense

Measure expansion timing, temporary-home-challenge use, setup completion, pattern diversity, attack routes, defense win rates, unfair combinations, resource retention by defensive investment, and post-raid churn.

### 27.4 Economy

Track Gold/Rum sources and sinks separately, passive versus active income, raid-fee effects, storage saturation, missed overflow, upgrade affordability, timer abandonment, cosmetic spend, and time to reacquire desired Items.

Do not impose a one-session recovery target. Test proportional loss exposure with shields and cumulative limits; add or adjust a hard per-raid cap only if evidence supports it.

### 27.5 Trading and progression

Track holding acquisition, progress transferred, merging costs, seller pool changes, reacquisition time, listing completion, supply surplus, fee revenue, and payer/nonpayer power differences. Confirm that total Item-rank advantage remains small across complete builds.

### 27.6 Leaderboards and community

Measure best-score improvement, new-player participation, stage/biome distribution, dominant strategies, score integrity, and repeated attempts. Unequal scoring potential across biomes/tiers is accepted; monitor it without assuming segmentation is the solution.

### 27.7 Acceptance scenarios

- Boss success persists when the player subsequently dies.
- No boss victory ends the run or offers a Return Home success exit.
- Intermediate portals unlock, remain visible, and preserve the build on transition.
- Chests/POIs do not replenish within a stage; new stages receive fresh allocations.
- Temporary home bands grant no permanent Territory or production.
- Raid theft happens once at boss death; continued farming does not drain the defender.
- Meaningful failed raids can receive non-stolen rewards.
- A sold holding loses eligibility and transfers all its progress; purchases combine underlying points without duplication.
- Previously sold Items remain earnable through independent permanent reward pools and contracts.
- Starter content preserves playability without guaranteeing specialized builds.
- The weekly board uses one best single-run Voyage score across all biomes/tiers and counts post-boss kills.
- Queue waits and empty treasuries do not eliminate all rewarding gameplay.

## 28. Development sequence

This is a proposed validation order, not a production estimate or an implementation instruction.

1. **Prove the run and conquest:** Saltwind, initial Weapons/Logbooks/Items, fixed exploration rewards, minimap, Barnacle King, ten-minute escalation, continued post-boss play, death-ended results, and secured Territory ownership.
2. **Prove replayability:** fresh Voyages, initial tier/stage transitions, build carryover, finite stage rewards, and representative mastery goals. Test the difference between satisfying uncertainty and helpless failure.
3. **Test authored encounters early:** a small Defense Director prototype with existing enemies and self-tests, before committing to broad content production.
4. **Prove ownership and economy:** concentric permanent expansion, temporary home challenges, production/storage, intermediate upgrades, meaningful timers, immediate cosmetics, and Item progression/reacquisition. Test eligible timer payments when the progression loop is reviewable.
5. **Prove resource raids:** matchmaking, snapshot integrity, fees, partial non-stolen rewards, once-only treasury transfer, protection, and post-boss survival.
6. **Validate public competition:** verified single-run scores, one weekly Voyage board, score context, and settled consumable rules.
7. **Expand content and trading:** Gravewater/Devil's Maw content depth, persistent holding transfers and merging, cosmetic trading, wallet-at-need flow, and marketplace economics. Item ownership must be designed for this before trading launches.
8. **Extend selectively:** Crews/events, partners, co-op, agents, and richer creator tools as justified.

Do not delay the distinctive defense or stage experience until after every economic or payment feature is complete. Conversely, public reward transfers and leaderboards require integrity validation first.

## 29. Open decisions

The following are intentionally not presented as settled rules.

### 29.1 Run pacing

- Exact overtime curve, enemy caps, and expected survival distribution.
- Health, chest prices, resource retention, and other adjustments between stages.
- Boss assignments, Voyage-tier unlock conditions, and biome access.
- Behavior on disconnect, app closure, pause, backgrounding, or abandonment.
- Interaction between claim/expansion completion and simultaneous incoming raids.

### 29.2 Territory and defense

- Band dimensions, navigation density, expansion entry gates, and geometry generation timing.
- How temporary challenge bands interact with unowned future permanent bands.
- First-run seed retries and long-term dissatisfaction with a home seed.
- Final Encounter Slots, Threat curve, card/pattern costs, and difficulty estimates.
- Whether all authored encounters must be engaged or can be bypassed.
- Reward rules for home challenges and self-tests.

### 29.3 Resources and raid protection

- Run Gold treasury separation, banking on death, and retained earnings from failed runs.
- Exact lootable fraction, proportional-loss formula, possible cap, and cumulative-loss ceiling.
- Shield duration, triggering threshold, protection during attacks, and concurrent raid entitlement.
- Gold entry fee, Defense Chest split/cap, and repeat-target restrictions.
- Gold/Rum production rates, storage curves, queue counts, timer lengths, and overflow behavior.

### 29.4 Items and rewards

- Starter Item set, minimum pool breadth, and how it is enforced after sales.
- Maximum permanent rank and its small total effect budget.
- Progress unit definition, merge prices, optional compatible-item inputs, and whether upgrades require an explicit action.
- Maximum-rank purchase overflow; never silently destroy paid progress.
- Whether sold/purchased holdings affect active runs or only subsequent runs.
- Sealed ownership versus blocking purchases before gameplay qualification.
- Permanent chest reward rates, failure eligibility, contracts, duplicate/overflow rewards, and boss bonuses.
- Whether Weapons/Logbooks eventually use a comparable tradeable ownership model.

### 29.5 Competition payments and social systems

- Consumable effects, inventory limits, mode eligibility, trading, and leaderboard eligibility.
- Weekly reset time, ties, cross-week runs, scoring exclusions, rewards, and balance-version policy.
- Bounty calculation and eventual raid competition.
- Cosmetic acquisition costs, variants, market eligibility details, and transaction fee.
- Fiat/SOL checkout and platform requirements; no paid loot-box model is implied.
- Crew sizes, event allegiance locks, campaign scoring, and partnership rewards.

## 30. Changes from v2.1

- Added full repeatable PvE through fresh-map Voyages alongside raids.
- Added home challenges and temporary outer challenge bands.
- Established concentric square permanent Territory growth.
- Replaced boss-ended runs with boss-secured objectives and death-ended survival.
- Adopted ten-minute soft stage timers and escalating overtime, including post-boss farming.
- Added multi-stage Voyages/home challenges with build carryover and boss-opened portals.
- Added minimap portal guidance and fixed, non-replenishing stage-start chests/POIs.
- Added one combined weekly best-single-run Voyage kill leaderboard without biome/tier segmentation.
- Accepted account progression and differing biome/tier scoring opportunities on that board.
- Clarified non-stolen rewards for meaningful raid failures and immediate once-only theft settlement at boss success.
- Kept proportional resource exposure under evaluation without committing to a hard cap or fixed recovery-session target.
- Added immediate Gold/Rum cosmetics that can trade for SOL, without buyback or fixed conversion guarantees.
- Replaced permanent Item licenses plus spare copies with one tradeable accumulated holding per Item type.
- Selling transfers ownership and upgrade progress; buying adds underlying progress to any existing holding.
- Added small-total permanent Item upgrades, independent reacquisition, and repeatable targeted contracts.
- Deferred reliable in-run exchanges to preserve imperfect builds and lucky runs.
- Retained baseline Item playability and minimum pool breadth without increasing drop weight for extra progress.
- Recognized that additive merging preserves progress supply and that declining resale prices are acceptable.
- Preserved limited-production priorities and reduced duplicated combat-power systems.

The removed v1 token emissions, Proof of Plunder epochs, token buybacks/burns, staking/APY, TVL goals, and time-played financial rewards remain removed.

## 31. References

The original source is `Rogue_Pirates_GDD_v2_1_REPAIRED.docx`, dated 12 September 2026, supplied by the project owner. This version incorporates the design decisions made in the subsequent discussion. References below inform design; Rogue Pirates does not automatically inherit every rule or balance value from another game.

- [Megabonk official Steam page](https://store.steampowered.com/app/3405340/Megabonk/) — random upgrades, procedural survival, Items, and replayability.
- [Megabonk Items community reference](https://megabonk.wiki/wiki/Items) — unlock qualification and future-run eligibility.
- [Megabonk Forest community reference](https://megabonk.wiki/wiki/Forest) — stage bosses and portals.
- [Megabonk Microwave community reference](https://megabonk.wiki/wiki/Microwave) — uncertain Item conversion; an inspiration, not an adopted required feature.
- [Megabonk Final Swarm update reference](https://megabonk.wiki/wiki/V1.0.12) — overtime and excessive-run-length concerns. Rogue Pirates' exact curve is not copied or verified against current source code.
- [Supercell loot guide](https://support.supercell.com/clash-of-clans/en/articles/about-multiplayer-and-trophies-2.html) — partial lootability and progression-aware resource rewards.
- [Supercell shield guide](https://support.supercell.com/clash-of-clans/en/articles/multiplayer-attacking-defending-2.html) — the consulted guide specifies an eight-hour shield after at least 30% of available loot is stolen and allows attacking without reducing it. These are reference values, not adopted Rogue Pirates constants.
- [Clash October 2025 update](https://supercell.com/en/games/clashofclans/blog/release-notes/get-ready-for-ranked-update/) — reward/protection changes, 10% Loot Cart recovery in that update, and Shadow Base revenge. No current full storage-percentage/cap table was established for this GDD.
- [Clash continuous-play direction](https://supercell.com/en/games/clashofclans/blog/news/battle-and-ranked-modes-are-coming-to-clash-/) — continuous combat alongside long-term progression.
- [Risk of Rain 2 Scrapper](https://riskofrain2.fandom.com/wiki/Scrapper) and [3D Printer](https://riskofrain2.fandom.com/wiki/3D_Printers) community references — run-local Item conversion, distinct from permanent ownership.
- [Survivor.io equipment community reference](https://survivorio.fandom.com/wiki/Equipment) — permanent equipment merging as a conceptual reference; exact merge trees are not adopted.
- [Hades progression update](https://www.supergiantgames.com/blog/hades-superstar-update-patch-notes/) — reusable challenges and progression context.
- [Brotato official Steam page](https://store.steampowered.com/app/1942280/Brotato/) — build variation through traits and shared content.

Review evolving external rules again when making release-specific decisions. The authoritative rules for Rogue Pirates are the agreed sections of this document, with unresolved details listed explicitly above.
