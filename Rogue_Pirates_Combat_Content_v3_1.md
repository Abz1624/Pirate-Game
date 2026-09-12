# Rogue Pirates Combat and Content Specification

**Version:** 3.1  
**Parent:** [Game Design Document](Rogue_Pirates_GDD_v3_1.md)  
**Status:** concrete, unvalidated prototype defaults. All values below are proposed starting data, not measured balance.

## 1. Units and player baseline

Distances are meters; time is active simulation seconds. Use 30 simulation ticks per second. Percentages are additive within their named stat bucket unless stated otherwise. Round damage to 0.01 internally; displayed damage rounds to integers. Death occurs at Hull <= 0 after fatal-hit prevention.

Player: 100 maximum Hull, full starting Hull, 6 m/s movement, 0.8-meter collision radius, 3-meter pickup radius, 5% critical chance, 1.5 critical multiplier, zero armor, zero passive regeneration, and one rank-1 starting Weapon. Two Weapon and two Logbook slots are initially available; permanent capacity follows the parent GDD.

Movement uses direct directional steering without simulated sailing inertia. Acceleration is 30 m/s², braking 40 m/s², turning up to 720 degrees/s. Facing tracks the last nonzero movement direction; retain facing at rest. Diagonal movement is normalized. Obstacles slide the ship along their surface rather than stopping both axes.

No universal dodge, ram attack, or active combat skill is needed for launch. Hammerhead Helm explicitly adds conditional ramming. Weapons auto-fire while an eligible target is present; Kegs may deploy without a target. Weapon cooldown advances during active play, including movement.

## 2. Shared damage and status rules

### 2.1 Damage calculation

`raw = source_base × source_level_factor × (1 + additive_damage_bonus) × conditional_multiplier × critical_multiplier`

Each conditional Item states its bucket. Multiply distinct named conditional buckets; sum contributions within a bucket. Generic damage bonus is capped at +1,000% for numerical safety. Crit chance caps at 75%; critical multiplier defaults to 1.5 and caps at 3. Armor reduces damage by `100 / (100 + armor)`; player armor caps at 150 and enemy armor at 100. Minimum nonzero damage is 0.1.

Avoid double-scaling secondary effects: when an effect uses a percentage of resolved parent-hit damage, do not apply generic damage or crit again. Sources using “base damage” instead calculate their own noncritical hit through the formula once.

Enemy elemental resistance is zero by default. Bosses take 50% of normal slow strength, at most 20% movement slow in total, and are immune to knockback, charm, and forced displacement. DOT damage has no blanket boss reduction; individual effects may specify one.

### 2.2 Hit categories

Direct Weapon hits may crit and trigger eligible on-hit Items. DOT ticks, triggered Item damage, reflected/ram damage, environment damage, and boss adds' scripted self-destruction do not trigger generic on-hit effects or crit unless explicitly stated. Every damage event records source, target, parent event, and generation.

An enemy dying from a DOT or Item effect credits its originating player once. Noncombat hazards and enemies killing each other grant no player kill unless an explicit player effect caused the damage. Each enemy ID grants kill credit at most once.

Contact hits share a 0.5-second player immunity interval with other ordinary enemy hits. Persistent hazards hit at most every 0.5 seconds, respecting that interval. Dread Tide bypasses ordinary immunity and is integrated separately. Dodging by movement is the primary defense; invulnerability cannot be chained from visual effects.

### 2.3 Status definitions

- **Burn:** one application deals 20% of the applying direct hit's pre-armor, noncritical resolved damage over 3 seconds, ticking every 0.5 seconds. Up to five independent applications per player/enemy; a sixth replaces the oldest. Armor is applied at each tick. No on-hit triggers from ticks.
- **Bleed:** Cutlass Item application deals 12% of the parent's noncritical resolved pre-armor damage over 4 seconds. Up to five independent applications; same replacement rule as burn.
- **Shock:** a marker lasting 2 seconds. It provides +5% lightning damage taken, nonstacking, refreshed on reapplication. It does not stun. The hit applying shock does not benefit from that new marker.
- **Slow:** use the strongest active slow rather than summing. Player speed cannot drop below 70% of normal; ordinary enemies below 40%; bosses follow the 20% cap.
- **Knockback:** displacement with collision sliding, max 3 meters per hit for regular enemies; elites receive half; bosses zero. Knockback does not cause extra collision damage.
- **Near miss:** an enemy projectile or charge passes within 1.2 meters of the player collision surface without hitting; eligible once per attack ID. Manta Sail handles its own cooldown.
- **Heavy hit:** at least 10% of current maximum Hull lost from one enemy damage event after mitigation. Environmental/Dread damage is excluded from streak bonuses but remains lethal.

On taking a maximum-Hull increase during a run, add the same increase to current Hull. On reducing maximum Hull, clamp current Hull; do not grant healing. Percentage healing uses final maximum Hull.

## 3. Run leveling and offer generation

XP needed for level `L` to `L+1` is `5 + 2L`, where the run starts at L=0. Collect XP as pickups; auto-collect within pickup radius at 20 m/s, and credit on arrival. Default XP per enemy is specified in its record. Bosses grant 30 XP; directors' scripted summons grant no XP, Gold, or score.

Prototype player level limit is 200. Excess XP still appears in the run summary but gives no further levels. This is a technical cap, not a short-session objective. A new stage preserves player level and XP toward the next level.

At each level choose three distinct offer IDs where possible. Category weights: equipped Weapon upgrade 45%, new Weapon 20%, equipped Logbook upgrade 25%, new Logbook 10%. Remove unavailable categories and renormalize. Within a category choose uniformly; choose a quality for upgrades, not new entries. Two offers for the same equipped content are not presented together.

Upgrade quality probabilities: Common 70%, Uncommon 22%, Rare 6.5%, Epic 1.3%, Legendary 0.2%. A quality multiplier of 1, 1.2, 1.5, 1.8, or 2.2 applies to that level-up's scalar increment. It never multiplies discrete projectile-count unlocks. Each new Weapon/Logbook starts at rank 1 regardless of card rarity.

When fewer than three eligible offers remain, fill missing slots with distinct fallback cards: heal 10% maximum Hull; +2% additive damage for the run; +1% movement speed for the run. Repeated fallback stats respect global caps. Banish cannot remove baseline fallback availability.

RNG streams are separate for level offers, stage layout, chests, enemies, and permanent rewards. Taking an extra combat shot must not manipulate permanent chest rolls. Save RNG state with run snapshots.

Refresh/Skip/Banish unlocks and charges are specified in the systems document. Skip grants no upgrade and does not refund XP. Banish removes the chosen content from future offers during this run, not from ownership; it cannot remove currently equipped content or permanent starter access.

## 4. Weapons

All Weapons max at run rank 12. Their scalar increments use the quality multiplier on acquisition of that upgrade and accumulate. “+x% per rank” means an additive increment based on rank-1 base, not repeated compounding. Discrete milestones trigger on ranks 4, 8, and 12 once regardless of rarity.

Attacks do not pass through solid terrain unless specified. Targets are chosen within range by nearest distance, then lowest enemy spawn ID for ties. Hit once per projectile/enemy unless explicitly stated. Global attack-speed bonuses shorten firing intervals by dividing by `1 + bonus`; minimum interval is 0.12 seconds. Projectile count caps at 24 per emission, chain targets at 16, and area radius at 14 meters.

### 4.1 Deck Cannon

**Role:** dependable ranged damage, good against isolated threats. Base 24 damage; fires every 1.2 seconds at the nearest enemy within 22 meters. Projectile speed 24 m/s, lifetime 1.2 seconds, hit radius 0.3 meters. One projectile, zero pierce, 0.6-meter knockback.

Each rank upgrade adds +12% base damage and +3% projectile speed. Rank 4 adds one pierce; rank 8 adds a second projectile fired 0.1 seconds later; rank 12 adds one pierce. Secondary projectile retargets if the original target died.

Unlocked by default. Grapeshot supports crowd control while Shark Tooth/Admiral improve priority-target killing. Test: a pierced enemy is hit once, a fragment cannot immediately re-hit its source target, and geometry blocks ordinary shots.

### 4.2 Broadside Battery

**Role:** positioning-dependent crossfire. Every 1.8 seconds, fire two projectiles from each side of the ship along perpendicular facing directions, separated by 12 degrees. Each deals 14 damage, travels at 18 m/s for 0.9 seconds, pierces one enemy, and knocks back 0.4 meters. Fires if any enemy is within 18 meters, without auto-rotating the hull toward them.

Each rank adds +10% base damage and +3% projectile speed. Milestones each add one projectile to each side. Maximum rank has five per side before other modifiers. Broadside Drum counts distinct enemies hit by one volley.

Quest: defeat 150 Cannonboats cumulatively, then purchase with Rum. Test: both sides use the same heading snapshot and one enemy cannot trigger the Drum repeatedly from overlapping projectiles.

### 4.3 Powder Kegs

**Role:** route planning and area denial. Deploy one stationary keg 1.5 meters behind the ship every 2.4 seconds. Fuse 1.8 seconds; explosion 42 damage in 3.5-meter radius, with one-meter knockback. Kegs are not physical navigation blockers. Maximum active owner kegs is 12; if full, skip deployment without consuming any stored charge.

Each rank adds +12% base damage and +3% base radius. Rank 4 adds a second keg separated sideways by one meter; rank 8 shortens fuse to 1.4 seconds; rank 12 adds a third keg. Added kegs share emission cooldown but have independent IDs.

Quest: open 20 stage chests cumulatively. Double-Fuse and Queen Anne's Fuse have explicit propagation limits in the Item section. Test: every keg detonates once; overlapping trigger chains do not detonate a spent keg again.

### 4.4 Flying Cutlasses

**Role:** close-range persistent coverage. Two blades orbit at radius 2.5 meters at 180 degrees/s. Contact deals 12 damage per blade with a per-target/per-blade hit interval of 0.4 seconds. No crit reroll occurs before the next eligible hit. Knockback 0.2 meters. Blades continue around obstacles visually but cause no damage through solid terrain.

Each rank adds +9% base damage and +3% angular speed. Each milestone adds one blade. Duration stats do not add blades; attack speed reduces hit interval down to 0.15 seconds, not orbital speed.

Quest: defeat 200 Swordfish cumulatively. Test: idle contact cannot produce frame-rate-dependent damage and bleed respects its stack limit.

### 4.5 Flamethrower

**Role:** directional sustained pressure. Face the last movement direction. Emit a 55-degree cone six meters long for 1.8 seconds, followed by 1.2 seconds of recovery. Tick every 0.2 seconds for 5 base damage per enemy, apply burn, and stop at line-of-sight obstacles. Each enemy has a direct proc coefficient of 0.2 per tick.

Each rank adds +10% tick damage and +3% cone length. Milestones add 10 degrees of width, then 0.4 seconds of active duration, then 0.4 seconds active duration. Attack speed shortens the whole cycle proportionally, retaining tick rate; duration increases active time but not recovery.

Quest: claim Saltwind and complete one Powder Cache bargain encounter. Test: cone rotation follows ship steering, burns do not recursively trigger explosion Items indefinitely, and Flame ticks cannot independently trigger every attack proc at full rate.

### 4.6 Lightning Mast

**Role:** distributed damage and proc synergy. Every 1.7 seconds, strike the nearest enemy within 18 meters for 32 damage. Jump to two additional distinct enemies within six meters of the previous target. Each jump deals 80% of the previous hit's base. Chain targets cannot repeat. Apply shock after each hit.

Each rank adds +11% base damage and +2% chain reach. Rank 4 adds one jump; rank 8 adds one jump; rank 12 changes falloff to 85%. One crit roll per chain event is applied to all its hits. Each hit has proc coefficient 0.5.

Quest: defeat 100 Mantas. Test: no target is struck twice in one chain; Storm in a Bottle counts a chain event once, not once per enemy.

### 4.7 Expansion Weapons

These are fully specified candidates, disabled in the initial six-Weapon pool. They use the same 12-rank and offer-quality rules.

- **Harpoon Launcher:** every 2 seconds, launch one 75-damage projectile at nearest target within 26 meters; 32 m/s, one-second life, five pierces, 1.5-meter knockback. +12% damage/rank; milestones add one pierce, +4-meter target range, and a second harpoon. Quest: kill 100 Hammerheads. Rum license 800.
- **Mortar Deck:** every 2.8 seconds target the densest enemy cluster within 24 meters, choosing the cluster containing the lowest spawn ID on ties. A 0.9-second visible landing marker precedes 60 damage in a four-meter radius. +12% damage and +2% radius/rank; milestones add +1-meter radius, one additional shell at a second cluster, then -0.2-second landing delay. Quest: clear 20 Cannonboat encounters. License 800 Rum.
- **Anchor Flail:** one orbiting anchor at radius three meters, 100 degrees/s, 35 contact damage, 0.75-second target interval, 2-meter knockback. +11% damage/rank; milestones add +0.5-meter radius, another anchor, then +25 degrees/s. Quest: survive 60 cumulative seconds with three enemies within four meters. License 700 Rum.
- **Ghost Cannons:** every 1.6 seconds fire one 28-damage spectral shot at nearest enemy within 22 meters; 20 m/s, 1.4-second life, three pierces, ignores terrain but not Dread boundary. +12% damage/rank; milestones add pierce, second projectile, then pierce. Quest: conquer Gravewater. License 1,000 Rum.
- **Chainshot:** every 1.5 seconds fire a 20-damage, 1.5-meter-wide projectile at nearest enemy within 16 meters; 18 m/s, one-second life, two pierces. Applies 25% slow for two seconds. +10% damage/rank; milestones add pierce, +0.5-meter width, then slow duration +1 second. Quest: kill 300 charging enemies. License 650 Rum.
- **Siren Bell:** every 3 seconds pulse 26 damage within five meters, apply 20% slow for 1.5 seconds. +11% damage and +2% radius/rank; milestones add +0.5 seconds slow, +1-meter radius, and a second pulse after 0.35 seconds. No charm in prototype. Quest: kill 60 Sirens. License 900 Rum.

## 5. Logbooks

Logbooks max at run rank 40. Rank 1 grants one increment; each further rank grants another increment multiplied by offer quality. Caps below are total stat caps, including contributions from other sources. They remain useful with any eligible Weapon; all descriptions show the increment and current total.

### 5.1 Foundational eight

- **Gunner:** +4% additive damage per increment. Default.
- **Powdermaster:** +3% attack speed. Total attack-speed cap +200%. Default.
- **Navigator:** +2% movement speed. Total positive movement cap +80% before temporary bursts. Default.
- **Carpenter:** +4% of base maximum Hull (four Hull) per increment; heals by the increase. Default.
- **Lookout:** +1 percentage point crit chance per increment. Default.
- **Quartermaster:** +4% run Gold pickup value per increment, capped at +150% from all Gold modifiers. Affects generated run Gold only, never theft, passive income, contracts, or market proceeds. Default.
- **Scavenger:** +5% base pickup radius per increment, maximum radius 12 meters. Default.
- **Cartographer:** +3% XP pickup value per increment, maximum +150% XP bonus. Default.

All eight are eligible from the first-conquest pool; the initial equipped capacity stays two. The broader default pool is a prototype choice resolving the earlier four-versus-eight starter ambiguity.

### 5.2 Unlockable six

- **Ironclad:** +2 armor per increment. Quest: survive 300 cumulative mitigated enemy damage; price 180 Rum. Damage qualification counts prevented damage at current armor, not self-inflicted boundary damage.
- **Fortune:** +2 Luck points per increment; Luck caps at 100. Quest: open 30 stage chests; price 220 Rum.
- **Tidecaller:** +3% effect duration per increment, capped +100%. Affects burn/bleed total duration at unchanged tick damage (therefore more total DOT), slows, ink, and active flame windows. Does not extend invulnerability, frenzy, projectile lifetime, or stage timer. Quest: apply 300 burn stacks; price 260 Rum.
- **Admiral:** +3% damage to elites and bosses per increment, capped at +100%; its own conditional bucket. Quest: kill any objective boss within 90 seconds of activation; price 300 Rum.
- **Plunderer:** +3% generated boss Gold/Rum reward per increment, capped at +60%. Does not modify theft or permanent progress drops. Quest: secure three raid boss victories; price 300 Rum.
- **Chaos:** on each increment choose uniformly among +4% damage, +3% attack speed, +2% movement, +4 base Hull, +1 crit point, +2 armor. No extra quality roll after selection. Quest: secure any boss using three different Weapons; price 280 Rum.

## 6. Luck and chest rarity

Run Item chest rarity base weights: Common 65, Uncommon 25, Rare 8, Epic 1.8, Legendary 0.2. At stage depth `s` (1–3), multiply Rare/Epic/Legendary weights by `1 + 0.15(s-1)`. Multiply each non-Common weight further by `1 + Luck/100`; normalize the complete set to 100%.

Luck never affects permanent reward chests, contracts, theft, or marketplace rewards. It affects level-up upgrade quality using the same non-Common weighting rule without the stage multiplier. Effects tagged “better chest rarity” modify weights, not guaranteed tier outcomes.

If a rolled rarity has no eligible owned content, downgrade to the nearest lower rarity with an eligible Item. Pick uniformly within it. Ownership rank and held progress do not affect selection probability. Current run stacks do not remove an Item from the pool unless its definition specifies a one-copy cap.

## 7. Item stacking and permanent ranks

Each content type has a temporary run stack count `n`. Persistent rank `r` is 0–10 for tradeable holdings, with multiplier `q = 1 + 0.005r`; the maximum improvement is +5% to the named primary magnitude. Baseline nontradeable Items use q=1. Multiply only effects explicitly named “rank magnitude.” Do not improve multiple axes, discrete extra jumps, proc counts, or trigger frequency through permanent rank.

For chance Items, stack chance is `1 - (1-p)^n`, with p from the definition, then apply q and the stated cap. For linear scalar Items use `base × n × q`. Copies beyond a stated effect cap remain owned during the run but add no power; the UI says the effect is capped. Caps apply after all relevant modifications.

No run Item stack persists into a later run. Permanent holdings are rewarded through the separate economy system.

### 7.1 Common Items and baseline pool

The eight Common Items below are the protected starter pool. They cannot be sold, permanently ranked, or banished from ownership. Their temporary copies still stack in runs.

- **Lucky Doubloon:** +5 Luck per copy, global Luck cap 100. No extra effect. Test exact normalized rarity probabilities at 0/50/100 Luck.
- **Spare Planks:** +8 base Hull per copy; heal by eight on pickup. Test removal/clamp on snapshot restoration and no repeated pickup receipt.
- **Gunner's Glove:** +10% projectile speed per copy, cap +200%. Does not affect hitscan/chain timing or flame ticks. Test travel range remains governed by lifetime, with global arena constraints.
- **Powder Monkey:** +5% attack speed per copy within global cap. Test minimum cooldown for every Weapon.
- **Shark Tooth Necklace:** +8% damage per copy against targets above 90% Hull, cap +80%; evaluate target Hull before hit. Separate healthy-target bucket. Test the hit crossing 90% benefits, later hits do not.
- **Manta Sail:** +3% movement per copy, plus a nonstacking +20% speed burst for one second after a near miss; four-second cooldown shared across copies. Test one attack cannot repeatedly refresh the burst.
- **Reef Carapace:** +4 armor per copy, global cap 150. Test fractional damage and minimum damage.
- **Crow's Nest:** +10% pickup radius per copy; discovery distance for Beacon/POIs rises by 2 meters per copy, maximum +12 meters. It does not reveal the full map. Test discovery persists after leaving range.

### 7.2 Uncommon Items

Each entry lists quest qualification, immediate initial ownership price in Rum, effects, and rank magnitude.

- **Skeleton Key:** open 25 paid chests; 160 Rum. Each chest opening has chance `1-0.94^n`, multiplied by q, cap 35%, to be free. Decide before charging; a free opening does not increment the chest purchase counter. Rank magnitude: chance. Test no roll on insufficient funds until the chest is actually interactable; repeated canceled interactions cannot reroll.
- **Grapeshot Pouch:** reach Deck Cannon rank 6; 180 Rum. Cannon hit emits two fragments, each 20% × n × q of parent damage, cap 80%. Fragments travel five meters with zero pierce and no Item procs, cannot hit the parent target. Rank magnitude: fragment damage. Test fragments cannot spawn fragments.
- **Double-Fuse Cord:** kill ten enemies within three seconds using Kegs; 180 Rum. Keg blasts trigger undetonated owner Kegs within `2 + 0.3(n-1)` meters, max four. Triggered Kegs deal +5% × n × q base damage, cap +30%. Rank magnitude: damage bonus, not chain reach. Test maximum once-per-keg activation and normal natural fuse cancellation.
- **Serrated Scabbard:** kill 200 enemies with Cutlasses; 180 Rum. Cutlass hits apply bleed; each copy adds 3 percentage points to the base 12% DOT coefficient, cap 30%; q multiplies that coefficient. Rank magnitude: bleed damage. Test DOT ticks cannot apply bleed.
- **Dragon's Breath Nozzle:** apply 500 burns; 200 Rum. Burn duration increases 10% per copy, cap +50%, before Tidecaller. Burn damage per tick gains 2% × n × q, cap +10%. Rank magnitude: tick bonus only. Test five-stack replacement and expiry.
- **Copper Conductor:** hit three targets with one lightning chain 30 times; 200 Rum. Add one lightning jump, nonstacking; each copy adds 3 percentage points × q to jump retention, capped at 95% retention. Rank magnitude: falloff improvement only. Test no repeated target and no retention above 95%.
- **Broadside Drum:** hit four enemies with one volley 20 times; 180 Rum. A qualifying volley subtracts `0.10 × n × q` seconds from remaining Broadside cooldown, cap 0.4 seconds; trigger once per volley. Cannot reduce next emission below 0.12 seconds since prior emission. Rank magnitude: subtraction. Test no recursive immediate volley.
- **Hammerhead Helm:** kill 50 Hammerheads; 200 Rum. Reduce received knockback by 20% per copy, cap 80%. Moving above 5 m/s into an enemy already below full Hull deals `8 × n × q` damage, with a one-second cooldown per target. Rank magnitude: ram damage. Ram does not cancel incoming contact damage. Test no stationary ram and no on-hit procs.

### 7.3 Rare Items

- **Smuggler's Ledger:** spend 5,000 run Gold cumulatively; 260 Rum. Stage chest prices increase 20%, nonstacking. Non-Common rarity weights multiply by `1 + 0.10 × n × q`, capped 1.5, before normalization. Rank magnitude: weight increase. Test a higher price does not affect boss or permanent chests.
- **Black Spot:** kill 100 elites; 280 Rum. Once per stage, first pickup registers one optional elite-bargain objective using an existing unconsumed stage-start bargain POI; it does not spawn a new POI. While held, naturally director-spawned elites drop +10% × n × q run Gold/XP, cap +50%. Rank magnitude: reward bonus. It does not multiply permanent rewards. Test no new infinitely repeatable elites/POIs from extra copies.
- **Dead Man's Compass:** discover a Beacon before minute four; 260 Rum. Reveal the Beacon immediately; increase the director's spawn rate by 10%, nonstacking, within entity caps. Give +3% × n × q XP gain, cap +15%. Rank magnitude: XP bonus. Test early reveal doesn't activate the boss or create a new Beacon.
- **Kraken Ink Sac:** kill 40 Kraken Spawn; 280 Rum. Elite deaths create a three-meter ink pool lasting three seconds, slowing enemies 20%; copy n adds 0.5 seconds duration, max six seconds total before duration modifiers. Damage is `4 × n × q` per second, cap 24 before q, ticking twice/sec. Rank magnitude: damage. Max four active pools/player; oldest replaced. Test no chained elite-death reward duplication.
- **Cursed Coin:** end any run carrying 500 unspent run Gold; 260 Rum. +10% × n × q generated run Gold gain, cap +50%, and -10% base maximum Hull once regardless of copies. Rank magnitude: Gold bonus only. Test no effect on theft, no instant heal when restoring max Hull, and global Gold cap applies.

### 7.4 Epic Items

- **Storm in a Bottle:** perform 300 Lightning Mast chain events; 400 Rum. Every 20 chain events triggers five lightning strikes over one second, each at a distinct nearby enemy for `18 × (1+0.25(n-1)) × q` base damage, stack multiplier capped at 2. Ten-second trigger cooldown. Rank magnitude: strike damage. No crit/on-hit triggers; these strikes do not charge the bottle.
- **Devil's Match:** secure Gravewater's boss after applying burn during the fight; 420 Rum. Burning enemy deaths create a 2.5-meter blast for `12 × n × q`, cap 60 before q. Rank magnitude: blast damage. Blasts do not apply burn; a blast-killed burning enemy can create one more blast but only up to generation 2 from the original kill. Test breadth cap and no indefinite propagation.
- **Queen Anne's Fuse:** detonate five Kegs in one chain; 420 Rum. Natural Keg explosions create one secondary blast at the nearest other enemy within four meters after 0.2 seconds, dealing 25% × q parent damage. Copies add generations: min(n,3); each generation retains 50% of previous damage. Rank magnitude: first secondary coefficient. These blasts do not trigger Cord, Match, or another independent Fuse chain. Test generation and event caps.
- **Barnacle Crown:** defeat Barnacle King after spending 20 seconds with at least four enemies within five meters; 400 Rum. While surrounded by four enemies, gain +2 armor and +2% damage per second up to `10 + 2(n-1)` stacks, cap 20. Decay two stacks/sec when not surrounded. q applies to damage per stack only. Test acquisition/loss and global armor cap.
- **Jolly Roger:** kill 150 enemies without a heavy hit; 400 Rum. Every ten kills adds one streak stack granting `1% × q` damage, maximum `20 + 5(n-1)` stacks capped 40. A heavy hit removes half, rounded up. Rank magnitude: per-stack damage. Test DOT kills credit once and scripted summons do not farm streak.

### 7.5 Legendary Items

- **Leviathan Heart:** conquer Devil's Maw; 650 Rum. Every 12 seconds target the densest cluster within 20 meters; after 0.6-second telegraph, deal `120 × (1+0.2(n-1)) × q` damage in five-meter radius, multiplier capped 2. Rank magnitude: damage. No Item procs; normal enemies knocked back 1.5 meters. Test single target fallback and obstacle-safe target selection.
- **Captain's Last Rum:** defeat any objective boss while below 20% Hull; 650 Rum. One active copy maximum. Once per run, replace a fatal enemy or Dread hit with restoration to 25% Hull, two seconds damage immunity, and +30% × q damage for eight seconds. Rank magnitude: frenzy bonus only. A second pickup after consumption does not recharge it. Before consumption, duplicate pickup gives a 10% Hull heal instead. Test one-use flag survives stage transition and reconnect.

### 7.6 Cross-effect guardrails

At most 64 new secondary combat events may be emitted by one source event and at most three propagation generations. If saturated, retain closest targets then lowest IDs; drop excess secondary events, not authoritative primary hits. Global event budget is 2,000 secondary events/sec; identical-time overflow follows the same stable priority.

Cap active projectiles at 600 total, area effects at 80, and XP pickup entities at 200. Merge nearby identical-value XP/Gold pickups into conserved bundles; do not delete value. Render culling does not alter hit resolution.

## 8. Enemy baseline and movement

Numbers below are at biome Saltwind, stage 1, minute zero. Enemies use separation steering and pathfinding through valid water. They do not enter unplayable sections. Offscreen spawns use a 24–36-meter annulus around the player and approved anchors; no spawn within eight meters or directly inside the visible camera region.

Enemy record: Hull; speed m/s; contact/attack damage; XP; run Gold. Gold is the base pickup amount before player bonuses. Each base enemy kill counts one leaderboard kill irrespective of type; bosses count one too. Scripted boss/encounter reinforcements marked summon count zero for score and drops, unless they are part of the finite original authored encounter roster.

### 8.1 Standard families

- **Shark:** 18 Hull; speed 4.4; contact 6; XP 1; Gold 1. Direct chase with gentle separation. No special attack. It teaches movement and fuels early builds. Test minimum spawn safety and no contact stacking inside immunity.
- **Cannonboat:** 36 Hull; speed 2.4; contact 5; cannon shot 9; XP 2; Gold 2. Seek 12–16-meter range, 0.8-second aiming line, fire one 10 m/s projectile, then two-second recovery. No steering after firing. If trapped, path sideways rather than fire through walls. Test projectile lifetime three seconds and clear telegraph.
- **Swordfish:** 25 Hull; cruise 3.5; contact 5; charge 12; XP 2; Gold 2. Approach to ten meters, telegraph a line for 0.7 seconds, lock direction, dash at 12 m/s for 0.75 seconds, recover 1.5 seconds. One charge hit per attack ID. Test collisions terminate dash and near-miss eligibility.
- **Hammerhead:** 100 Hull; cruise 2.8; contact 8; charge 18; XP 4; Gold 4. Acquire within 14 meters; 1.1-second windup, 10 m/s dash for one second, recover two seconds, displace player 1.5 meters. Attack cooldown six seconds. Test visible heavy windup and terrain collision.
- **Manta:** 45 Hull; speed 3.2; contact 6; sweep projectile 10; XP 2; Gold 2. Move laterally at ten meters; 0.9-second fan telegraph, emit three 8 m/s projectiles separated by 18 degrees, four-second cooldown. Test no fan aimed directly behind its orientation and all lanes visible.
- **Reef Crab:** 90 Hull; speed 1.8; armor 30; contact 10; slam 14; XP 3; Gold 3. Within three meters, 1.0-second circle telegraph then three-meter slam, cooldown four seconds. Armor falls to zero for 1.5 seconds after slam. Test weak window and no permanent armor oscillation exploit.
- **Kraken Spawn:** 120 Hull; speed 1.5; contact 8; tentacle 16; XP 5; Gold 5. Every six seconds mark two 1.5-meter circles near the player's position, wait one second, strike; circles remain dangerous 0.5 seconds. No hard grab in the initial prototype. Test circles cannot seal all movement exits.

### 8.2 Gravewater variants

Apply Gravewater base multiplier from section 9, then one behavior change, not an unrelated new control set.

- Drowned Shark: every five seconds becomes translucent and ignores enemy separation for 0.6 seconds; remains damageable and collides with terrain/player normally.
- Bone Swordfish: second dash after 0.8-second fresh telegraph at 75% first-dash damage; no invisible instant turn.
- Ghost Cannonboat: projectile ignores terrain but gains a distinct spectral appearance; damage unchanged, speed reduced to 8 m/s.
- Wraith Manta: sweep leaves two-meter wake patches lasting two seconds with 15% slow, max three patches per Manta.
- Drowned Crab: armor rises to 60 while preparing slam; weakness window remains zero armor. No full invulnerability.
- Spectral Hammerhead: telegraph length increases to 1.3 seconds; dash goes through ordinary enemies but not terrain.
- Gravewater Kraken Spawn: three tentacle circles rather than two, each with 1.2-second warning.
- **Siren:** 80 base Hull; speed 2.2; contact 6; pulse 12; XP 4; Gold 4. Every six seconds, 1.2-second expanding-ring telegraph, then apply 15% player slow for two seconds within eight meters. Buff nearest three enemies' movement +10% while alive, nonstacking across Sirens. It does not forcibly move the player. Apply normal biome/time scaling.

### 8.3 Devil's Maw variants

- Hellfin Shark: leaves a one-meter burning wake for one second after each three seconds of pursuit; 4 base damage per 0.5 seconds, one wake damage stream per player.
- Ember Swordfish: dash endpoint explodes after 0.6-second warning, radius two meters, base 10 damage.
- Infernal Cannonboat: replaces shot with a visible target circle and 1.2-second delayed three-meter mortar blast, base 14 damage, four-second cooldown.
- Ash Manta: fan projectiles create a two-second stationary flame patch on terrain impact, 5 base damage per half second, maximum three patches.
- Magma Crab: after its slam, leave a two-meter lava patch for two seconds; base 6 damage per half second. Weakness window unchanged.
- Abyssal Hammerhead: dash completion emits a four-meter ring after 0.7-second warning, base 12 damage.
- Infernal Kraken Spawn: three circles and a second delayed strike in one previously marked circle; a separate one-second warning precedes the repeat.
- **Fire Eel:** 65 base Hull; speed 4.0; contact 8; line attack 14; XP 4; Gold 4. Every five seconds mark a six-meter line, wait one second, dash along it and leave a two-second hazard. Line path is fixed at telegraph start. Apply biome/time scaling.

## 9. Spawn director and difficulty

Stage depth d=1,2,3. Biome b=1 Saltwind, 2 Gravewater, 3 Devil's Maw. Active stage minute m is continuous elapsed seconds/60. Tier t=1,2,3 governs encounter profile and total stage count.

Before overtime, enemy Hull multiplier is `(1+0.65(b-1)) × (1+0.8(d-1)) × (1+0.2(t-1)) × (1+0.12m)`. Enemy damage multiplier uses `(1+0.25(b-1)) × (1+0.25(d-1)) × (1+0.08m)`. Speed multiplier grows only with overtime, with global speed cap described below. In raids/conquest/expansion use d=1,t=1 and the local enemy biome; the boss uses the highest objective Zone.

Spawn rate is `min(14, (2+0.5min(m,10)) × (1+0.25(d-1)))` enemies/sec before optional director modifiers. Entity cap: 180 ordinary enemies plus 12 elites and one boss. When full, do not accumulate an invisible spawn debt; resume normal pacing as enemies die. Fairness limits apply before spawning elites/hazards.

Saltwind roster weights by elapsed time:

- Minutes 0–2: Shark 65, Cannonboat 20, Swordfish 15.
- Minutes 2–5: Shark 40, Cannonboat 20, Swordfish 20, Manta 10, Crab 10.
- Minutes 5+: Shark 30, Cannonboat 20, Swordfish 20, Manta 12, Crab 10, Hammerhead 5, Kraken Spawn 3.

For Gravewater/Devil's Maw use themed replacements, taking eight weight points from Shark for Siren/Fire Eel once minute two begins. A natural elite is 3× Hull, 1.2× damage, +10 armor, and 3× ordinary XP/Gold. Elite probability is 0 before minute three, then 2%; max active 12. Their telegraphs remain at base duration.

### 9.1 Overtime

Let `o=max(0,m-10)`. Freeze normal minute scaling at m=10. Multiply Hull by `2^(o/2)`, damage by `1.45^(o/2)`, speed by `min(1.5,1+0.05o)`, and spawn rate by `min(2,1+0.15o)` subject to entity caps. Player debuffs do not reduce enemy speed below their applicable slow cap.

At overtime minute four, add a visible pursuing storm front that reduces optional safe space by 5% of stage area per subsequent minute, down to 40% remaining. Its safe component must include the player and a navigable corridor to any opened portal. If that validation fails, skip that contraction and increase next minute's enemy damage multiplier by an additional 10% instead.

On a new stage overtime resets, but d increases and the build carries. This is the escape opportunity the portal creates. Soft timer and danger are displayed separately from total run time. There is no arbitrary instant-kill timeout; escalating combat is intended to end play.

### 9.2 Boss activation pressure

When a boss is active, director rate falls to 40% of its current rate, preserving danger without overwhelming telegraphs. Overtime still advances. After death animation, restore full director rate. Boss-specific summons are visually identifiable and give zero XP/Gold/kills to prevent stalling as a score farm.

Freeze the boss's normal stage-time Hull scale at activation, but apply ongoing overtime multipliers to outgoing damage and movement. Boss maximum/current Hull are not repeatedly inflated during the fight. This avoids an unfinishable moving HP target while maintaining urgency.

## 10. Boss framework

Bosses spawn at the Treasury/Boss Beacon's validated arena after a two-second entrance telegraph. Players can move and attack during entrance, but the boss is invulnerable only for the entrance. No multi-minute immunity phases. Default arena radius is 18 meters, with navigable escape lanes. Leaving beyond 30 meters causes boss pursuit, not healing/resetting or automatic victory.

Base boss stats below apply before biome/depth/tier/time multipliers. Use boss-specific biome identity instead of applying the biome multiplier twice. For Saltwind boss b=1, Admiral b=2, Kraken b=3. Final required boss clear determines tier/conquest objective; intermediate versions secure a stage clear and open the portal.

Boss phases transition at 65% and 30% Hull. Finish the current telegraphed attack before changing phase; never interrupt a readable windup with unannounced damage. Choose attacks in the deterministic sequences below. Global recovery between attacks is one second unless a record specifies more.

### 10.1 Barnacle King

Base 1,500 Hull, 15 armor, movement 1.5 m/s, contact 12, collision radius three meters. Approach the player slowly; prioritize attacks within 14 meters.

Attacks:

1. **Shell Slam:** 1.2-second expanding circle, five-meter radius, base 22 damage and 1.5-meter displacement. Afterward armor becomes zero for two seconds.
2. **Barnacle Volley:** 0.9-second fan telegraph, fire five 9 m/s shells at 20-degree spacing, base 14 damage each, three-second lifetime.
3. **Crab Call:** 1.5-second visible summoning marks, create three no-reward Crabs at valid arena-edge nodes; at most six living summons. Two-second recovery.
4. **Rolling Charge:** phase 3 only, 1.5-second line telegraph, move at 8 m/s for 1.2 seconds, base 25 damage, stop on terrain, recover two seconds with zero armor.

Phase 1 sequence: Slam, Volley, Slam. Phase 2: Slam, Call, Volley. Phase 3: Charge, Volley, Call, Slam. No attack begins if its target geometry would leave no player escape lane; substitute Volley with a 90-degree open wedge.

Stage variations: depth 1 standard; depth 2 volley has seven shells with the same open escape wedge; depth 3 Call uses four summons and Charge has a second independently telegraphed pass. This is configuration reuse, not another boss model.

Counterplay: bait slam/charge, use vulnerability window, maneuver through fan gaps. Test: zero-armor windows, summons cap/no rewards, phase transition during low Hull, and once-only objective receipt.

### 10.2 Drowned Admiral

Base 1,800 Hull before b=2 scaling, armor 10, movement 2 m/s, contact 14, radius 3.5 meters. Rotates slowly to present broadside; does not instantly track during telegraphs.

Attacks:

1. **Ghost Broadside:** 1.1-second side markers, fire six shells from each side at 8 m/s, base 16 damage, three-second life. Central forward/aft lanes remain open.
2. **Cursed Wake:** mark a curved six-meter route for 1.2 seconds, move along it for two seconds, leave patches lasting three seconds; 15% slow and 6 damage/half-second.
3. **Fog Order:** phase 2+, reduce distant contrast for four seconds while retaining minimap, ship, nearest enemies, and all attack telegraphs. Summon two no-reward Ghost Cannonboats, max four.
4. **Crossfire:** phase 3, two separated 1.2-second fan warnings followed by diagonal volleys; each shell 18 damage. Alternate fans by 0.6 seconds, never fire both simultaneously into the same safe lane.

Sequences: phase 1 Broadside, Wake; phase 2 Broadside, Fog, Wake; phase 3 Crossfire, Wake, Broadside, Fog. After Wake, armor drops to zero for two seconds. Depth 2 adds two shells per broadside; depth 3 increases Wake width from two to three meters but not warning duration.

Counterplay: stay near forward/aft gaps, avoid following the wake, use the map in fog. Test: reduced-VFX settings preserve fog gameplay and all telegraphs; no projectile through-terrain behavior without spectral visual; summons never grant farming credit.

### 10.3 Infernal Kraken

Base 2,200 Hull before b=3 scaling, armor 20, stationary central body radius four meters, contact 16. Damageable body remains available; tentacles are attack emitters, not independently rewardable enemies.

Attacks:

1. **Tentacle Slam:** mark three circles of radius two meters around predicted player positions with 1.3-second warnings; strike for 24 damage. At least one four-meter escape lane remains clear.
2. **Burning Rings:** expand two visible rings from the body separated by one second, speed 4 m/s, base 18 damage. Each ring has a rotating 70-degree gap fixed when its telegraph begins.
3. **Drag Current:** phase 2+, a visible sector applies movement force toward the body at 1 m/s for three seconds after one-second warning; does not disable controls. No stun/grab lock.
4. **Infernal Sweep:** phase 3, rotate a warned tentacle beam through 120 degrees over two seconds for 8 damage/half-second; safe opposite sector stays open.

Sequences: phase 1 Slam, Rings; phase 2 Slam, Current, Rings; phase 3 Sweep, Slam, Rings, Current. Every sequence ends with a two-second armor-zero opening. Depth 2 adds one slam circle; depth 3 shortens recovery to 0.75 seconds, never windups. Arena pressure may use the overtime safe-space system, never an independent unvalidated wall closing off all routes.

Counterplay: cross ring gaps, move against or sideways to currents, punish vulnerability windows. Test: body and tentacle visuals never award duplicate boss kills; no unavoidable overlap across Rings/Sweep; arena contraction preserves valid space.

## 11. Finite stage objects

Each Voyage/conquest stage starts with 12 ordinary chests and four POIs. Home stages use ten chests/four POIs. Raids use 12 + four per extra owned Zone chests and four + two per extra Zone POIs across the full map. Allocate 40% of raid exploration objects to highest Zone, distributing remaining ones evenly over lower Zones. Counts do not replenish after boss defeat.

Four POI types are chosen once per stage with no duplicate type in that stage:

- **Repair Wreck:** one-use heal 25% maximum Hull, no Gold fee.
- **Powder Cache:** activate a marked finite fight of six ordinary enemies plus one elite; reward one random run Item and 20 run Gold on completion. All members have unique one-time reward IDs.
- **Cursed Shrine:** one-use choice to accept +15% incoming enemy damage for 90 seconds in exchange for +20 Luck for the same period. It does not create a permanent reward or guaranteed Item.
- **Treasure Chart:** reveals the nearest three unopened stage chests. If fewer exist, reveal the remainder. One use.

No merchant/exchange guarantees a missing Item. Opening a chest costs `ceil(20 × 1.12^k)` run Gold, where k is paid chest openings in the entire run. Price hard cap 1,000 run Gold per chest; this is an economic bound, not a global currency cap. Smuggler's Ledger applies its +20% after this base cap. Interaction duration 0.4 seconds within two meters; pickups and combat continue afterward. Items drop physically but expire only on stage exit/death, not an arbitrary short timer.

Each boss grants one guaranteed run Item chest with Rare/Epic/Legendary weights 85/14/1 and normal owned-pool fallback, plus generated account rewards in the systems document. It is a one-time boss reward, not a replenishing exploration chest.

## 12. Combat validation scenarios

- Verify expected base damage and cooldowns for each Weapon at ranks 1/4/8/12 and mixed-quality upgrades.
- Verify flame/cone/orbit hits under 30/60/120 rendering FPS with identical simulation input.
- Verify crit, armor, DOT, rank q, boss resistance, and conditional bonus ordering through numeric fixtures.
- Verify source attribution and hard limits for Cord, Match, Fuse, Grapeshot, and Storm combinations.
- Verify cooldown/stack caps, diminishing probability, fatal-hit prevention, and resuming mid-status.
- Verify no forced perfect offers, eligible pools cannot become empty, and fallback offers work at maximum ranks.
- Verify no-renewal stage objects and continuous chest pricing across three stages.
- Verify each enemy's windup, locked targeting, wall interaction, and one-hit-per-attack rule.
- Verify boss phase transitions do not cancel telegraphs and post-boss waves resume.
- Measure plausible weak, average, and strong builds at minute 10, overtime 4, and overtime 8. Do not infer actual survival duration from formulas alone.
- Stress the entity/event budgets with the strongest available proc combinations and preserve score/resource conservation.

The values here need feel testing. The specification establishes reproducible behavior so tuning changes can be measured rather than hidden in implementation assumptions.
