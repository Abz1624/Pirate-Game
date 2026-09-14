# Appendix B — Economy and Transaction Detail Records

**Authority note:** This appendix preserves detailed receipt, raid, holding, interruption and transaction rules. `plan/01_DECISION_REGISTER.md` and `plan/06_ECONOMY_LIVEOPS.md` override all historical references to trading, partial death banking or paid acceleration. Those features are removed.

# Rogue Pirates Economy and Systems Specification

**Version:** 3.1  
**Parent:** [Master implementation specification](../00_MASTER_IMPLEMENTATION_SPEC.md)  
**Combat definitions:** [Combat value records](A_COMBAT_CONTENT_VALUES.md)  
**Status:** concrete prototype defaults, not production balance or individually approved numerical rules.

**Supersession:** [Economy, Ads, IAP and LiveOps](../06_ECONOMY_LIVEOPS.md) is authoritative for player trading removal, account-bound inventory, 100% run-Gold banking, production/capacity/upgrade numbers, ads, IAP and live operations. Conflicting text below is historical and must not be implemented.

## 1. Resource accounts and invariants

Gold and Rum are the only gameplay currencies. Both are nonnegative whole units. Item progress units and cosmetic assets are inventory entries, not spendable currencies. Run Gold is a separate balance of Gold for the active run, not a third currency.

Account balances distinguish spendable treasury, passive uncollected production, protected Defense Chest receipts, and unapplied receipts. The UI shows spendable and pending amounts separately.

No treasury Gold enters a run. Players cannot buy extra stage chests by transferring savings mid-run. Gold picked up in a run funds its chests; remaining Gold contributes to the death payout using the formula below.

No automatic Gold/Rum conversion, direct SOL redemption, player-to-player transfer, or asset buyback. Gold, Rum, Items and cosmetics are account-bound.

## 2. New account and first-hour defaults

Account creation grants Deck Cannon, the eight foundational Logbooks, the eight Common baseline Items, and two Weapon/two Logbook slots. It grants no initial spendable treasury or production before conquest. Baseline Items are permanent account-bound entries that guarantee continued play and minimum pool breadth.

Conquering Saltwind activates Ledger rank 1 for Gold production, Rum production, Gold capacity, Rum capacity, and defense progression at no resource cost. One-time first-claim reward: 500 Gold and 180 Rum. Production starts at the boss event, not the later death screen. A 24-hour beginner shield starts then; no raid target listing before first claim. Outgoing raids are available after one guided defense setup, with Quick Setup sufficient.

First claim also qualifies the third Weapon-slot purchase, priced at 150 Rum. The player can choose that purchase or save toward another unlock. No mandatory purchase blocks ordinary Voyages.

The first results screen teaches Ledger upgrades, collection, and the next Voyage. Store, ads, pass and Crew surfaces remain closed until their contextual unlocks; no wallet connection is used.

## 3. Captain's Ledger progression

There are no functional buildings placed on the combat map. The five progression tracks below are account capabilities. Each rank has a visible name, current effect, next effect, cost, and completion time.

### 3.1 Rank and Zone gates

Ranks 1–3 require Saltwind, 4–6 Gravewater, 7–9 Devil's Maw. Reaching a new Zone permits the next range but does not buy ranks automatically. Ranks are purchased sequentially within each track.

Expansion entry requires prior Zone claimed and Defense Director rank 2 for Gravewater or rank 4 for Devil's Maw. This makes one defense investment relevant without requiring every track maximized. No expansion entry fee; boss difficulty remains the skill/build gate.

One works queue handles production, capacity, and Defense Director upgrades. Resources are charged at start. The old rank remains active until completion. No second queue is needed for instant Item/slot/content purchases. Queue completion is determined by authoritative time even while offline.

An upgrade cannot be canceled in the prototype. Show cost and duration before confirmation. A network retry must return the same upgrade rather than charging twice. Paid acceleration is disabled; no purchase or advertisement shortens works timers.

### 3.2 Production

Trade Routes Gold/hour by rank 1–9: **10, 15, 22, 32, 45, 60, 80, 105, 135**.

Rum Supply Rum/hour by rank 1–9: **0.5, 0.75, 1.1, 1.6, 2.3, 3.2, 4.5, 6.2, 8**. Fractional accrual remains authoritative and only whole units transfer on collection.

Each producer has an uncollected buffer equal to eight hours at its active rate, independent of treasury capacity. Fractional accrual is stored precisely; collection transfers whole units and retains the fractional remainder. When the buffer fills, production pauses until space becomes available. No offline catch-up beyond the buffer.

Upgrade Gold costs to ranks 2–9: **400, 1,200, 3,500, 8,000, 18,000, 40,000, 90,000, 180,000** per production track. Times: **5, 30, 120, 360, 1,080, 2,880, 5,760, 10,080 minutes**. Both tracks spend Gold; Rum remains mainly a collection/combat resource.

On rank completion, accrue the old rate through completion time, then new rate afterward. The buffer's capacity increases; it is not automatically filled. Manual collection sends only what fits into treasury and leaves the remainder in the producer buffer.

### 3.3 Capacity

Gold capacity by rank 1–9: **2,000, 4,000, 8,000, 16,000, 32,000, 75,000, 150,000, 300,000, 500,000**.

Rum capacity by rank 1–9: **600, 900, 1,400, 2,200, 3,400, 5,200, 8,000, 12,000, 18,000**.

Each capacity track costs Gold to reach ranks 2–9: **500, 1,500, 4,000, 9,000, 20,000, 45,000, 100,000, 200,000**. Times use the common works schedule above. Every cost fits within the previous Gold-capacity rank.

Capacity does not itself increase raid loss. Actual stored resources do. Uncollected production is included in exposed wealth under the raid formula.

### 3.4 Defense progression

Defense Director rank costs to ranks 2–9: **600, 1,800, 5,000, 12,000, 26,000, 60,000, 130,000, 250,000 Gold**. Times use the common works schedule above.

A rank adds options/budget rather than a hidden universal enemy damage multiplier. Encounter power is calculated through content and explicit threat costs. See section 11 for slots, budgets, and card unlocks.

### 3.5 Immediate purchases

Slots: third Weapon 150 Rum after Saltwind; fourth Weapon 500 Rum and third Logbook 350 Rum after Gravewater; fourth Logbook 900 Rum after Devil's Maw. Each purchase is once per account and immediate.

Weapon licenses: Broadside 120 Rum, Kegs 140, Cutlasses 160, Flamethrower 260, Lightning 300, after their combat-document quests. Logbook and initial specialized Item prices are in their content definitions. Completed quests alone do not automatically spend resources.

RNG tools: complete 10 distinct quests to buy Refresh for 100 Rum (two uses/run); 20 to buy Skip for 150 Rum (two uses/run); 30 to buy Banish for 250 Rum (one use/run). Uses reset at new run only, never at stage transition. They are account-bound and have no paid top-up during runs.

## 4. Run payouts and permanent reward chests

**14 September owner decision:** retain 100% of unspent run Gold at valid death. The v3.2 specification provides the corresponding chest, production, capacity, upgrade and repeatable-sink revisions.

### 4.1 Separate secured and generated rewards

Boss milestones, theft, and one-time unlocks are recorded immediately. Generated run Gold remaining after chest spending is banked at death using the rule below. A player cannot spend pending run earnings through the Ledger before death, but boss-secured account rewards can be recorded as pending receipts.

At run start snapshot the account's Treasury capacity ranks and reward modifiers. This avoids buying an upgrade mid-run to change earned reward calculation. Receipts still apply against actual free capacity at settlement; overflow is explicitly shown.

### 4.2 Resource payout

At death, bank **100% of unspent run Gold**, rounded down. Meaningful failures use the same conversion; chest purchases remain the in-run Gold sink. No additional death haircut applies.

Generated boss rewards: `Gold = 120 × objective_biome × stage_depth`; `Rum = 30 × objective_biome × stage_depth`. Apply Plunderer if equipped at boss death. Grant once per objective boss per run. These are game-generated rewards, distinct from raid theft. Conquest one-time claim rewards are additional.

Failure/nonboss Rum at death: `min(20 × highest_biome_reached, floor(verified_rewardable_kills/100))`. Summons and nonrewardable kills do not count. Boss reward already earned is never subtracted by death.

Home challenge recurring generated Gold/Rum is 50% of these formulas, rounded down; first-completion bonuses below are separate. Defense practice gives zero. Raid generated rewards use normal formulas; theft is additional.

These rates intentionally make active play valuable alongside modest early passive income. They require measured kill rates and session results before live release.

Worked example, before storage clipping and with no reward modifiers: a Saltwind Tier-1 Voyage ends with 1,200 rewardable kills, 500 unspent run Gold, and its boss defeated. The account receives 500 banked Gold plus 120 boss Gold, and 12 kill-based Rum plus 30 boss Rum: 620 Gold and 42 Rum. Its chest grade is Silver if the boss point plus the 300-kill point are earned. A nonboss result with the same kills/balance yields 500 Gold and 12 Rum.

### 4.3 Reward chest eligibility and grade

Each eligible run grants at most one post-run reward chest; it is not a stage chest. Requirements: at least 120 active seconds and 100 rewardable enemy kills, or any objective boss kill. Manual interruption alone never grants a chest.

Grade score = one point for each stage boss defeated (max three), one for reaching 300 rewardable kills, and one for clearing two finite bargain/defense encounters. Score 0–1 gives Bronze, 2–3 Silver, 4–5 Gold. A raid can qualify through real encounters even if its boss survives.

Every chest grants permanent holding progress from one qualified specialized Item: Bronze 1 unit, Silver 2, Gold 3. Roll Item rarity with Bronze weights Uncommon 80/Rare 18/Epic 1.8/Legendary 0.2; Silver 65/28/6/1; Gold 45/35/17/3. Pick uniformly among quest-qualified Items of the rolled rarity, downgrading to a qualified lower rarity if necessary.

If no specialized Item is quest-qualified, grant 20 Rum instead. This replacement is capped by storage and displayed before collection. The permanent reward stream is independent of currently owned holdings and run Luck.

Each chest additionally rolls one bonus: no extra 90%, random basic cosmetic 7%, home-challenge consumable 3%. The ordinary progress grant always occurs. Rare cosmetic variants can later replace some cosmetic outcomes after demand/content validation; they are not needed for the prototype.

Open the chest on results with no key, payment, or wait. Store its immutable reward receipt if presentation is skipped or interrupted; reopen the reveal without rerolling. Run rewards cannot be rerolled with currency.

### 4.4 Overflow

Ordinary generated Gold/Rum receipts fill available capacity and report the remainder as unbanked. They cannot become an unlimited protected mailbox. Item/cosmetic receipts have separate inventory rules and do not disappear at full Gold/Rum capacity.

Boss-secured theft is different: its amount is limited to the attacker's free receiving capacity at the actual boss settlement, and only that accepted amount is deducted from the defender. Full attacker storage does not destroy someone else's resources.

One-time first-claim and expansion rewards may exceed capacity only up to the account's initial capacity in that resource and remain as explicit protected milestone overflow. The player must spend below normal capacity before collecting more. This exception cannot be generated repeatedly by ordinary runs or traded.

## 5. Quests and targeted reacquisition

The [Captain/mastery extension](../04_CONTENT_CATALOGUE.md) adds account-bound Captain licenses granted after explicit gameplay qualification and one-time home-mastery rewards. Captain passive growth resets each run; no new premium currency, permanent Captain rank or repeatable resource multiplier is introduced. Mastery does not replace targeted Item reacquisition.

One-time content quests are listed in the combat document. Record progress from verified events; conditions specify cumulative versus single-run. A quest completion is permanent account history.

Initial specialized Item purchase is allowed once after qualification and adds one ownership progress unit at rank 0. Subsequent progress comes from post-run chests and contracts; the initial Rum purchase remains once per Item/account.

The Contract page offers one chosen active reacquisition contract. Select any qualified specialized Item. Acceptance costs 40/80/160/240 Rum for Uncommon/Rare/Epic/Legendary. Switching forfeits that contract's unclaimed progress after a clear confirmation; it does not affect ownership or refund its commission. No daily reset or paid refresh is required.

Contract completion requires **two qualifying boss clears plus 300 rewardable kills** while active. At least one clear must be in a biome at or above the Item's access band: Uncommon Saltwind, Rare Gravewater, Epic/Legendary Devil's Maw. An Item qualified earlier remains eligible for random chest rediscovery; targeted high-rarity acquisition is intentionally harder.

Reward: three progress units for the selected Item. The same contract can be selected again immediately. It never requires owning the missing Item or using it. The UI shows all conditions and the exact reward.

Gameplay quests can encourage specific build styles without being the sole reacquisition path. Contract criteria are countable objectives, not promised clock-time completion. Track actual completion time and adjust if targeted recovery becomes excessive.

## 6. Persistent holdings and merge arithmetic

### 6.1 Progress and rank

For specialized Items, total owned progress `P` is a nonnegative integer. P=0 means not owned. Rank thresholds for ranks 0–10 are **1, 3, 7, 13, 21, 31, 43, 57, 73, 91, 111**. Rank is the highest met threshold, capped at 10. Its combat improvement is defined in the combat document: at most +5% to one primary magnitude.

Store total progress, never reconstruct it from displayed rank. A P=8 holding and P=6 holding combine to P=14, which derives rank 3. Rank labels do not add arithmetically.

### 6.2 Application and costs

Earned chest/contract progress is queued as a grant and immediately joins the same account-bound holding without a merge timer or resource fee. Recalculate rank once the grant applies.

For an optional explicit **reforge**, consume three progress units from one owned specialized Item to add one unit to another owned or quest-qualified specialized Item of the same rarity, costing 20 Rum. The source can reach zero and lose future eligibility. A holding frozen in an active-run snapshot cannot be modified until the run ends. Baseline Items are excluded. Show the exact source loss and destination gain before confirmation.

This reforge is a permanent collection operation between runs, not a guaranteed in-run exchange. It destroys two progress units intentionally and does not create an additional material currency. Reforge is a proposed default implementing the discussed higher-quantity use of unwanted Items; ordinary merging/aggregation itself remains lossless.

### 6.3 Maximum rank and surplus

P may exceed 111; combat rank remains 10. Preserve and display all surplus progress. No hidden overflow deletion. Reforge can use excess units while retaining the desired rank.

Player-facing display: “Rank 3 / 10”, “14 total progress”, and “7 to next rank” are separate labels. Do not use an unexplained 3/10 counter for several meanings.

### 6.4 Ownership and run eligibility

Eligible run Items = protected baseline plus all quest-qualified owned specialized holdings, with one entry per Item type. No extra drop weight from progress, price, or rank. Reforging unwanted progress creates specialization while eight baseline Items maintain breadth; owned types remain eligible.

At run creation freeze eligibility and rank for the run. Reforges and new grants affect the next run snapshot; they never rewrite an active run. Passive accrual and cosmetic viewing continue.

## 7. Account-bound cosmetic economy

All cosmetics are account-bound. Resource prices and paid product bands are defined in v3.2. Resource purchases are immediate, with no construction timer. Display slots remain two per section plus one main flag socket. One owned cosmetic entitlement may be equipped in every compatible cosmetic socket unless the asset definition forbids it for visual reasons.

Cosmetics have no collision, cover, stealth effect, navigation change, defense effect, or stat bonus. Cosmetic variants share attachment rules. Gilding provides the repeatable Gold sink defined in v3.2 and changes only appearance/prestige display.

Paid entitlements use idempotent provider receipts, restoration and explicit refund/revocation state. They cannot be converted into Gold/Rum, transferred, resold, or used as Crew membership proof. Earned and collaboration cosmetics record their source for collection history without implying monetary value.

## 8. Raid quotes and once-only settlement

### 8.1 Entry fee and quote

Raid entry costs 40 Gold for a one-Zone target, 80 for two, 120 for three. Candidate list contains three eligible targets and can refresh free every 30 seconds. Exclude self, current Crew members, active shields, and the same target attacked within the last 24 hours. No direct reward-bearing friend challenge.

Each card shows current estimated exposed Gold/Rum, difficulty, Zone count, and fee. The estimate is explicitly not a guaranteed reserved payout: a defender can legitimately collect/spend before the boss dies. Capture a maximum payable quote at entry; future income cannot increase this raid's theft above that quote.

No treasury resources are reserved during exploration. This avoids freezing the owner's savings while an attacker farms. Raid snapshots freeze combat content, not the defender's ability to use the Ledger.

### 8.2 Exposed resources

At each authoritative calculation, combine spendable stored amount S and uncollected production U for that resource. Protected Defense Chest and one-time milestone overflow are excluded. Base exposed amount is `floor(0.20S + 0.40U)`.

This is a prototype exposure formula, not an owner-approved final percentage. The boss can take the whole exposed amount, subject to its entry quote, remaining daily loss allowance, and attacker capacity. It is not 20% of an already-20%-limited amount. No separate fixed per-raid cap is used in this baseline.

### 8.3 Boss settlement

Use one atomic defender transaction:

1. Verify the run's objective boss success and that no theft receipt exists for this run.
2. Check current shield and daily-loss eligibility. A shield created after this run began also protects against its late settlement.
3. Recalculate exposure from current balances.
4. Compute each actual theft as the minimum of entry quote, current exposed amount, remaining daily loss allowance, and attacker's free treasury space.
5. Deduct the actual theft from uncollected production first up to its exposed share, then stored treasury; do not remove amounts outside the formula's eligible shares.
6. Credit exactly that actual amount to attacker treasury using the same receipt and increment the defender loss counter.
7. If either resource was stolen, start the post-loss shield immediately. Mark raid victory and settlement even if accepted theft was zero.

If another raid settled first or the owner spent resources, actual loot can be below the displayed entry estimate. The attacker still gets generated boss rewards and progression. When a newly created defender shield blocks the entire theft, refund this attacker's entry fee from the game's refund ledger, not from defender resources; grant no extra make-good treasure. Every run can receive at most one such refund. Record this as “Treasury protected before arrival.”

This live-quote rule is a new proposed resolution of concurrent raids, not a previously agreed guaranteed-loot promise. It must be tested for perceived fairness before production. A future escrow model would be a different design and must not be mixed into this baseline.

### 8.4 Entry-fee allocation

Half the fee is a permanent Gold sink. Half is credited to the defender's protected Defense Chest after a meaningful attempt reaches 120 active seconds and 100 rewardable kills, or kills the boss. Early abandonment does not award defender income; its pending half becomes sink.

Defender compensation is capped at 200/400/600 Gold per rolling 24 hours by Zone count. Excess is sink. Shield-race fee refunds are game-funded; compensation receipts are not rolled back if already awarded. Track this generated refund cost explicitly.

Defense Chest can hold up to three times the daily cap. Overflow compensation is sink. Collection transfers only what fits in treasury; once collected it becomes ordinary exposed Gold. Defense Chest has no Rum, yield, or SOL redemption.

## 9. Shields cumulative loss and reputation

Post-loss shield lasts eight hours. Outgoing raids do not shorten it. First conquest gives 24-hour protection. No shield is granted simply because an attack was attempted or because a boss died without any accepted theft. Existing shield blocks ordinary theft settlement, as defined above.

Cumulative loss allowance per rolling 24 hours equals **40% of Gold capacity and 40% of Rum capacity**, using the highest capacity rank reached during that window. Track loss events with timestamps; subtract still-in-window events from the allowance. This is protection against repeated draining, not a per-raid cap or a promise of one-session recovery.

Changing storage rank does not create a new window or erase loss history. The allowance rises only with genuinely purchased increased capacity. Calculate each resource independently.

Reputation prototype: first Zone claims +100/+250/+500 Bounty; each completed eligible raid +10; a meaningful failed incoming raid before boss success +5 to the defender, maximum 25/day. No Bounty for receiving an attack, shield-race zero-loot victory, self-test, or repeated-target abuse. Profile wealth shows current resource band, not a permanent score from briefly moving balances.

No algorithm can prove a defense “credible” merely from an account flag. Prototype credibility requires target/attacker matchmaking band overlap, 120 seconds/100 kills minimum, no same Crew, and no same-target reward within 24 hours. Review performance and collusion signals before attaching valuable prizes.

## 10. Raid matchmaking

Initial candidate filter: target Zone count within one of attacker Zone count; never show a three-Zone target to a newly claimed one-Zone player. Let the player opt into a one-Zone-higher target after completing three raids.

Power estimate = `10 × purchased_Weapon_slots + 6 × purchased_Logbook_slots + 2 × Defense_rank + mean_owned_specialized_Item_rank`. It is a coarse matchmaking signal, not a damage formula. Candidate score considers power difference, target published Threat, raid win history, and exposed treasury. Prefer estimates within 25%, widening to 50% after ten seconds if needed; do not introduce infinite waits for perfect matches.

New accounts' first three raids use opted-in curated valid defenses with real rules and clearly labeled training status; they grant generated rewards but no player treasury theft or reputation. Afterward normal raids unlock. These maps are not falsely presented as live players.

The leaderboard remains unsegmented; these raid protections do not alter that decision. Refine power estimation when actual telemetry shows what predicts outcomes, especially Item synergy and consumables.

## 11. Defense Director configuration

### 11.1 Slots and budget

Each owned Zone has three Encounter Slots attached to three eligible section instances. The player selects which eligible sections receive those slots; no more than one slot per section. Quick Setup chooses the distribution.

Per-Zone base Threat budgets: Saltwind 10, Gravewater 12, Devil's Maw 14. Add `floor((Defense_rank-1)/2)` to each owned Zone. Zone expansion grants its slots/base budget; Defense rank increases the small shared addition. Active highest-Zone boss trait costs are charged to that Zone only.

Unused budget is allowed. Publishing over budget is rejected without altering the prior published version. One saved draft and one published version exist initially; additional presets are convenience, not power.

### 11.2 Enemy Cards

Cards define finite roster members using the combat document's regular enemy stats and local biome variants. Threat already accounts for count; no invisible additional upgrade multiplier.

- Shark Pack: eight Sharks, cost 2; Patrol, Ambush, Swarm.
- Cannonboat Patrol: four Cannonboats, cost 3; Patrol, Pincer, Reinforcements.
- Swordfish Rush: five Swordfish, cost 3; Ambush, Pincer.
- Hammerhead Breaker: two Hammerheads, cost 4; Patrol, Reinforcements.
- Manta Sweep: four Mantas, cost 3; Patrol, Pincer.
- Crab Holdout: four Crabs, cost 2; Patrol, Reinforcements.
- Kraken Nest: two Kraken Spawn, cost 4; Ambush, Reinforcements.

Unlocks: Shark/Cannonboat/Crab on claim; Swordfish after 100 Swordfish kills; Manta after 60 Manta kills; Hammerhead after 30 kills; Kraken after 20 kills. No separate Card copies or third upgrade currency. Patterns Patrol/Ambush available initially; Pincer at Defense rank 2; Reinforcements rank 3; Swarm rank 4.

### 11.3 Patterns

- Patrol: roster active along a local 8–12-meter route, engages within 16 meters; no extra cost.
- Ambush: entering a 12-meter section trigger starts a one-second warning, then roster appears at approved nodes at least eight meters from the player; +1 Threat.
- Pincer: divide roster into two groups at opposite approved entries, one-second warning, both at least eight meters away; +1 Threat.
- Reinforcements: spawn half on encounter entry, the remainder after half of that group dies or eight seconds elapse; +1 Threat. Original finite roster size does not double.
- Swarm: replace roster with 1.5× its count, rounded up, each at 75% Hull and damage; +1 Threat. Only Shark Pack supports it initially.

All authored roster kills grant their ordinary finite XP/Gold/kill credit within the raid, but raid kills never enter the Voyage board. Encounter clears grant one grade point contribution toward reward-chest conditions, not another permanent chest.

### 11.4 Hazards and traits

One environmental hazard per Zone, unlocked after owning that Zone and Defense rank 4: either a slow-current area (15% slow within a marked four-meter pool) or periodic cannon crossfire (two warned shots every eight seconds). Cost 2 Threat. Fixed section anchors and clear paths remain required; cosmetics cannot conceal them.

One boss trait on the active boss: Summon Adds (one additional no-reward summon per summon attack, cost 3); Armored Recovery (+10 armor outside documented vulnerability windows, cost 2); Wide Volley (two extra projectiles while retaining an escape wedge, cost 3). Traits never shorten mandatory telegraphs or remove all safe lanes.

### 11.5 Overlap validation

At most two authored encounters can be active simultaneously. Triggering a third queues its activation until one clears or the player leaves that section for ten seconds. Do not replace consumed roster members. No more than one active slow hazard within 12 meters of the player. Reserve the boss arena clear of authored hazard anchors.

Generated layout validation checks reachability and encounter overlap. Difficulty estimate uses total Threat and observed clear rates, never advertises guaranteed beatability. Test published defense against several valid permutations.

## 12. Consumable prototype

Include only two earned consumables initially, usable in home challenges and never in Voyages, conquest/expansion, raids, or defense practice. This conservative mode restriction is a prototype choice awaiting owner review.

- **Survey Chart:** before the home run starts, reveal its first stage's fixed POI markers. Does not create POIs or reveal later stages. One use consumed only after successful run creation.
- **Salvage Voucher:** the first ordinary paid chest in that home run offers two independently rolled eligible Items; choose one. No extra chest or guaranteed rarity. Consumed when the chest resolves, not if the run dies before opening it.

At most one consumable equipped/run, inventory cap ten of each. Overflow chest bonus converts to 20 Rum subject to capacity. Consumables are account-bound and not sold directly. These add value to chests without assuming unrestricted stockpiled advantages in competitive modes.

## 13. Home challenge rewards

Owner-selected initial catalogue: Fast Flag, Lightly Armed and Steady Hands from [Captains and mastery](../04_CONTENT_CATALOGUE.md), each one stage at Tier 1. These replace the older standard/no-chests/two-Weapon/timed catalogue and its per-biome/tier Gold/Rum/progress bonuses.

First completion is once per account and preset: Fast Flag grants Quick on the Draw title +30 Rum; Lightly Armed grants Single Cannon Pennant +30 Rum; Steady Hands grants Steady Hands title +30 Rum. Rum follows normal capacity/overflow rules. Record completion at its verified qualifying boss milestone; subsequent death does not revoke it. There is no additional first-clear Gold or specialized progress grant. Biome changes do not reset a preset's claim flag.

Repeat completions use ordinary post-run reward-chest eligibility and 50% generated currency rates. Contract progress is permitted for qualifying bosses/kills; self-tests give none. Challenge rules are engine-provided and cannot be edited by the owner to lower requirements.

## 14. Interrupted runs and active-session rules

One active reward-bearing run per account. Another device may view account state but cannot start a parallel run or mutate active Item holdings. Run combat and permanent reward seeds are authoritative and recorded at start.

Pause on level-up and Signal Buoy choice. Manual pause, focus loss/browser backgrounding, app suspension and lost connection also stop simulation at a safe tick boundary. No enemies, charge progress, cooldowns or ten-minute stage time advance while paused. Use independent pause reasons: closing the pause menu must not resume combat underneath an unresolved choice or backgrounded app. Resume only when all reasons clear, with an explicit continue action after lifecycle interruption.

Proposed competition rule: the existing 30-minute paused allowance now includes choice-screen time, manual pause, backgrounding, suspension and connectivity interruption, counted as a union of wall-time intervals (never double-count overlapping reasons). Trusted server-controlled loading/verification waits are excluded. Show allowance and warn before exhaustion; beyond it the attempt remains playable and reward-bearing but loses global and event score eligibility. This limit is a Rogue Pirates proposal, not a claimed Megabonk rule. A paused interval of 24 hours expires the unfinished run, including an abandoned choice screen; ordinary active simulation still has no hard survival timer. Never count client-supplied elapsed values as authoritative.

The practical distinction: choosing an upgrade stops the ship's fight clock, while the real-world community event still ends at its advertised time. A run starting before reset belongs to its start period. Owner-approved deadline policy: the server must receive the complete sequenced submission through death by the published cutoff, 24 hours after that period ends. Store authoritative receipt time; client death timestamps alone are insufficient. A timely complete submission may finish verification afterward without losing eligibility because of our queue delay. Incomplete uploads at the cutoff are not timely complete submissions. Missing the cutoff removes that period's score eligibility without ending live combat or erasing secured rewards. Pause remains a convenience, not a way to extend event deadlines. Exact Megabonk wall-time/leaderboard treatment was not established from primary documentation; this policy adapts the desired paused choice experience to our online events.

Save after every boss milestone, stage transition, permanent receipt, and every ten active seconds. Resume from last acknowledged state without replaying already-applied events. Client rewind must not duplicate chests or drops. Network architecture must support the intended state cadence.

If a run is not resumed within 24 hours, mark it abandoned/expired. Do not award a successful extraction, pending survival chest, unsecured run Gold, or kill score. Keep previously secured boss milestones, boss receipts, theft, and quest completions. Apply no additional defender loss. Release active-session locks.

This expiry is an operational failure policy, not a gameplay Return Home feature. The UI warns about loss of unfinished rewards when leaving. If the player resumes normally, they continue until death.

## 15. Weekly score and event state

At run start assign the current Monday-00:00-UTC week ID and frozen content version. On verified death, eligible Voyages submit total score once. Keep the best kill count per account/week. Ties share rank. Stage count, biome, tier, duration, build, and content version accompany the score without creating separate boards.

A started-before-reset run must be completely submitted through death by 24 hours after reset, subject to the separate interruption-expiry rules. Verification may finish later. Final weekly rewards wait until the submission window closes and all timely submissions that could affect standings have been resolved. Keep results provisional during backlog/review; do not grant an unverified score or reject a timely submission merely because verification is slow. For this prototype the weekly reward is an account-bound profile badge only, with participation and top-10 badges; no cash/SOL prize.

Deploy balance changes between weeks where possible. In an emergency invalidate affected exploit scores with a reason and preserve unaffected receipts. Do not silently compare a modified active run against the wrong ruleset.

No same-seed requirement is imposed; random favorable maps and builds remain part of the open competition. Difficulty/tier advantages are accepted. Leaderboard anti-cheat still verifies each event against that run's frozen rules.

## 16. Crews and community event extension

The owner has promoted Crews, daily/weekly community competitions, top-player rewards and collaboration ship inventory into planned release scope. The [Crews, events and skins specification](../09_SOCIAL_NARRATIVE.md) governs these additions, including roster snapshots, affiliations, settlement and cosmetic-instance ownership. Event rewards are separate from the baseline global weekly profile badges described above.

Core release needs profiles, flags, public run summaries, and moderation hooks for display names. Use a 3–20-character player name with service-side profanity/abuse review and report/block affordances before public social launch. Names never affect gameplay IDs.

Crews have no stat bonuses. Players cannot change Crews during their current Crew's active event. Membership unlocks at the published event end, without waiting for score review or manual rewards; no additional switching/contribution cooldown applies. Proposed defaults are one leader, up to three officers, a 2–5-character tag and ordinary capacity 30; partner Crew capacity is configurable. Membership history supports anti-collusion checks. Event snapshots preserve score attribution after unlock, as specified in the extension.

The owner selected Crew-based event representation instead of separate Event Tags. Players can join another sponsor's Crew after their current Crew's event ends. Proposed per-event Crew snapshots freeze on the first eligible run after enrollment; the membership lock already applies from event start or joining an active-event Crew. Personal best verified Voyage scores feed community views without modifying global scoring.

Daily/weekly partner events and community leaderboards are planned release features. Rewards are flexible, including skins, gameplay resources and SOL/cash, with manual/external fulfillment as requested. Verified winner manifests and delivery tracking are required; automatic external payouts are not. Partner self-service, agents, co-op and on-foot play remain deferred.

## 17. Payment and timer acceleration boundaries

Prototype implements a sandbox purchase interface with product ID, displayed price, currency, account-bound entitlement, and idempotent receipt. Real providers and regional prices are configured per approved channel.

Launch products are known account-bound cosmetics and the cosmetic Captain's Chronicle. Do not sell works acceleration, Gold/Rum, random reward chests, rerolls, resurrection, raid protection, Item progress or score.

Public payment release is blocked on provider integration, receipt validation, restoration, refund/revocation behavior, account recovery and platform validation. This does not block a playable offline or closed-server prototype.

## 18. Required transaction tests

- Collect production twice with the same command ID: one transfer only, fractional accrual retained.
- Finish an upgrade offline across a rate change: correct old/new accrual, no free refill.
- Spend while a raid exists: owner spending works; attacker quote remains a maximum and final settlement uses live balances.
- Two bosses resolve against one defender concurrently: one commits first; resulting shield protects against the other, and fee refund occurs at most once.
- Attacker full storage: actual theft is zero for that resource, defender loses zero, generated overflow is reported separately.
- Partially filled attacker storage: transfer only available capacity, preserving total resources across both accounts.
- Claim boss receipt then die/reconnect: no second grant or revoked victory.
- Grant P=8 then P=6 to one account: P=14, rank 3, and only one Item type enters its eligible pool.
- Grant above P=111: all progress preserved, rank remains 10, no probability increase.
- Reforge three units: source loses three, destination gains one, 20 Rum charged once, zero source removes eligibility.
- Restore/refund a paid cosmetic receipt: entitlement applies or revokes once without touching unrelated earned assets.
- Complete an ad twice with one provider callback ID: one Sponsored Cache claim only; cross-device daily cap remains two.
- Expire interrupted run: milestones retained, no new survival payout/score, session restrictions released.
- Weekly run spans reset: score belongs to start week and final badge processing waits for allowed completions.

## 19. Economy tuning experiments

Following the [Megabonk polish pass](../03_SIMULATION_COMBAT.md), also model larger Quartermaster/Cartographer increments, Captain Vale's Gold bonus, timed threat rewards, temporary pickup survival gains and the small one-time mastery grants. Improved survival can increase Gold/progress supply even when drop rates are unchanged. Do not assume the previous economy sample predicts the revised combat's payouts.

Run at least three modeled cohorts before live tuning: low-frequency player (two runs/day), active player (six), and high-frequency player (fifteen). These are stress inputs, not audience forecasts. Vary win rate, average kills, chest spending, raid losses, production claims, targeted contracts, vanity spending, ad completion and live-event completion.

Observe whether each can afford the next meaningful choice, whether storage saturates while queues are busy, and whether defense investment improves saved resources. Compare the live-quote raid rule against player expectations; if it feels unfair, redesign it explicitly rather than quietly guaranteeing unaffordable loot.

Check Item acquisition, rank power advantage, cosmetic/Gilding adoption, consumable overflow, capacity loss and targeted-contract time. Do not optimize solely for higher playtime, purchases, ads, or IAP at the expense of enjoyable runs.

All proposed formulas and prices should live in versioned data with telemetry labels. A numerical adjustment must not mutate already-secured run, ad, event or purchase receipts.
