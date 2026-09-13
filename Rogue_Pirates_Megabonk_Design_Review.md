# Rogue Pirates — Megabonk Reference Review and Polish Pass

13 September 2026 • researched design revision, not measured game balance

The owner requested a broad review of Megabonk.org and adoption/adaptation of useful mechanics, values and logic. This pass reviewed its mechanics hub and focused timing, powerup, damage, proc, scaling, status and shrine guides; the character, Weapon, Tome and Item catalogues; representative individual entries; maps, progression, challenges and unlocks. This is broad systems coverage, not a claim to have audited every individual Item page or verified the game's source code.

Megabonk.org identifies itself as an independent fan resource. Its reports are references, not official specifications. New Rogue Pirates values below and in linked specs remain prototype defaults. Existing owner decisions prevail, particularly death-only endings, finite exploration objects, small permanent Item-rank advantages, ownership-driven Item pools, the shared global leaderboard and real-time Crew-event membership locks.

## 1. Source conflicts that must not become our bugs

- The focused timer guide describes overtime at ten minutes, whereas the progression guide describes Final Swarm as beginning after boss defeat. Retain our explicit rule: ten-minute overtime, boss kill independently securing victory and opening the next-stage portal when applicable. [Timer](https://megabonk.org/guides/mechanics/timer/), [progression](https://megabonk.org/guides/progression/)
- The Tome catalogue shows Precision at +7% in its main list and +2% in a lower card. Do not treat either as verified current data merely because the page has a recent date. Our Lookout uses an explicitly chosen +2 percentage points as a conservative starting value. [Tomes](https://megabonk.org/database/tomes/)
- Za Warudo's page describes one use per run while its patch section discusses a 25-copy cap. Keep Captain's Last Rum's one-use flag; do not infer 25 revives or a full heal. [Za Warudo](https://megabonk.org/database/items/za-warudo)
- The XP page mixes an additive table with loose claims of multiplicative growth. Our XP modifiers remain a single additive bucket; extra XP can accelerate later level acquisition without multiplying every copy against the previous one. [XP Tome](https://megabonk.org/database/tomes/xp-tome)

No code, sprites, models, icons, descriptions or character names are imported from the reference game. Numeric relationships and mechanical patterns are translated into our original pirate content.

## 2. What changed in Rogue Pirates

### A. A stage has recognizable pressure beats

Adopt the reference timing nodes of 3:00 and 8:00 miniboss pressure, and 4:00 and 7:00 swarm pulses. Our implementation uses existing elite behaviors and capped director changes, avoiding four new bespoke encounters per map. These beats make route timing matter between ordinary leveling and the ten-minute overtime transition. The exact HP, rewards and pulse intensity are our tuning data. [Timer reference](https://megabonk.org/guides/mechanics/timer/)

The normal objective boss remains player-activated. Early boss defeat does not start overtime early. Scheduled events neither replenish exploration loot nor steal more defender resources. Stage departure cancels unplayed beats rather than banking them for later stages.

### B. Captains provide playstyle identity

The reference character layer combines starting equipment and a passive that can grow during a run. Adopt that role using six original Captain profiles, with two starters and four progression unlocks; prototype three before funding the full roster. They are ship commanders represented through portraits and effects, not on-foot characters requiring new navigation/animation systems. [Character reference](https://megabonk.org/guides/characters/)

See [Captains and mastery](Rogue_Pirates_Captains_and_Mastery.md). Character identity is distinct from collaboration ship skins. A premium-looking ship must never secretly select a stronger Captain. Permanent Captain stat ranks are not introduced; their modest level scaling resets each run.

### C. Early Logbook choices matter more

The original prototype offered small increments despite limited slots and a ten-minute first stage. Use +7.5% attack speed and +7% XP as reference-derived starting increments for Powdermaster and Cartographer. These are additive, retain our existing caps, and are not promises of the reference game's final balance. [Cooldown Tome](https://megabonk.org/database/tomes/cooldown-tome), [XP Tome](https://megabonk.org/database/tomes/xp-tome)

Other revised common increments are our adaptation: Gunner +8% damage, Carpenter +10 Hull, Navigator +3% speed, Lookout +2 crit points, Quartermaster +8% run Gold, Scavenger +10% pickup radius. Fortune becomes +5 Luck and Tidecaller +5% duration. The XP requirement formula and enemy base curves initially remain fixed so playtests can isolate the impact. Record level/upgrade counts at minutes 3, 7 and 10 rather than guessing a target from another game's videos.

### D. Items connect builds instead of only strengthening matching Weapons

The existing set leaned toward closed pairs: lightning Item needs lightning Weapon, keg Item needs kegs. Keep those recognizable identities, but let selected effects connect them. Copper Conductor can now produce a weak lightning arc from other Weapon hits; Devil's Match can ignite through eligible on-hit events; Storm in a Bottle can charge from those chain events. Grapeshot fragments and bottle strikes receive narrowly defined follow-up trigger permissions.

The reference proc guide motivates explicit source coefficients rather than letting every rapid hit trigger everything at full rate. Our source/receiver whitelist and strict limits are original implementation rules, not a copied claim that all Megabonk Items chain. [Proc reference](https://megabonk.org/guides/mechanics/proc-coefficient/)

This preserves all 28 Item identities and their ownership/rank rules. No giant catalogue is required. Successful runs can discover bridges; a Captain does not guarantee those Items will drop. No Item-pool bias toward the selected Captain is added.

### E. More than one stat can matter to a build

Keep explicit additive buckets with separate conditional multipliers. Add a bounded Hull-to-pulse conversion to Leviathan Heart and an unspent-run-Gold passive for a Captain. This makes defensive or economic investment potentially useful offensively, while chest spending remains a real choice. The reference Beefy Ring illustrates stat conversion; our pulse coefficient and cap are different. [Beefy Ring](https://megabonk.org/database/items/beefy-ring)

Do not copy a separate multiplier for every new Item. That makes balancing opaque and can overwhelm the intended narrow permanent-power gap. The reference damage guide is useful for the grouped-bonus concept; its item-specific bracket map is not our architecture. [Damage brackets](https://megabonk.org/guides/mechanics/damage-brackets/)

### F. Temporary pickups create short tactical opportunities

Add a small temporary-powerup layer separate from Items, XP, permanent holdings and post-run chests: a repair supply, offensive signal, speed gust and collection bell. They create a reason to route into danger without adding a new persistent resource or inventory tab. Their drop rate, stage cap and durations are our proposed data. [Powerup categories](https://megabonk.org/guides/mechanics/powerups/)

No protection/invulnerability powerup is included initially. Last Rum already supplies one rare fatal-hit prevention mechanic, and another refreshable immunity source would make overtime harder to reason about.

### G. Mastery changes decisions using existing content

Add three home challenge presets: a boss timing objective, reduced Weapon slots and uninterrupted shrine charging. Use verified event predicates and a clear earned/failed state. A failed objective does not end combat; death still ends the run. Reward first completions with titles/trophies and a small fixed resource grant, avoiding a new permanent income multiplier. [Challenge reference](https://megabonk.org/guides/challenges/)

Challenges remain outside global and community Voyage kill boards. This matches our existing separation. Challenge constraints never become a mandatory condition for routine Item reacquisition.

## 3. What we deliberately retain or defer

- **Finite chests/POIs:** no passive periodically creates exploration chests. The reference Hoarder-style character idea would conflict with this owner commitment.
- **No guaranteed in-run build repair:** Microwave-style selected duplication and broad pool toggles remain deferred. Our existing permanent reforging, sale/reacquisition and minimum starter pool serve the collection model. [Microwave reference](https://megabonk.org/guides/mechanics/microwave/)
- **Run gold is not treasury:** do not copy unlimited movement-to-currency generation into a game where earned currency buys tradeable assets. Movement-based combat identity can exist without a repeatable money fountain. [Movement-gold reference](https://megabonk.org/database/items/golden-sneakers)
- **No universal evasion, shield gate, poison, freeze or stun layer yet:** burn/bleed/shock/slow already support the selected Weapons. Copying every defensive/status system would multiply interaction work. Add one only when a concrete content need justifies it. [Status reference](https://megabonk.org/guides/mechanics/status-effects/)
- **No overcrit yet:** maintain the current critical chance cap. Test revised offensive increments and Item links before adding another damage tier.
- **Three Zones and one-to-three-stage tiers remain:** the reference shows how repeated stage depth supplies content reuse; it does not require us to copy every biome or increase stage count. [Maps](https://megabonk.org/guides/maps/)
- **Our unlock economy remains ours:** retain Ledger gates and repeatable reacquisition instead of copying the reference quest-count/slot-purchase sequence. The useful lesson is a visible unlock route and explicit purchase step, not a universal numerical threshold. [Unlocks](https://megabonk.org/guides/unlocks/)
- **No reference monetization or leaderboard economy is assumed:** Megabonk's single-player item acquisition is not validation of SOL trading, asynchronous theft or sponsor prizes.

## 4. Six test builds, not guaranteed recipes

- **Artillery:** Deck Cannon, Grapeshot, Lookout, Shark Tooth; ranged burst becomes crowd coverage through fragments. Weakness: surrounding enemies and obstructed firing lanes.
- **Stormfire:** Copper Conductor, Devil's Match and eventually Storm in a Bottle, with a multi-hit Weapon. Weakness: missing bridge pieces and lower per-hit proc coefficients.
- **Powder trail:** Kegs, Double-Fuse and Queen Anne's Fuse; route enemies through an existing chain. Weakness: fast pursuers and badly positioned explosions.
- **Armored brawler:** Cutlasses, Carpenter/Ironclad, Barnacle Crown, potentially Leviathan Heart. Weakness: ranged pressure and the need to remain close without absorbing unlimited hits.
- **Treasure wager:** Smuggler Captain, Quartermaster, Cursed Coin and Smuggler's Ledger. Weakness: spending Gold reduces stored-Gold combat value, while hoarding sacrifices new Items.
- **Mobile explorer:** Navigator, Manta Sail, Crow's Nest and terrain-aware kiting. Weakness: movement alone cannot clear threats or stop the overtime fleet.

Show discovered interactions in the post-run summary, not a pre-run promise of a particular legendary. Track damage by Weapon/Item, proc counts, healing, shrine rewards, largest hit, boss time, stage departure time and source of death. Use that evidence to help players form their own next-run hypothesis.

## 5. Validation and scope

First comparison: old vs revised Logbook increments using the same seeds/inputs where feasible, followed by human playtests because inputs change with better choices. Compare weak, median and high-synergy builds, new and mature collections, and all Captains. A deterministic replay regression is not a fun/balance test.

Check that a run still has room for adaptation, minute-3 pressure does not routinely kill unprepared beginners, the new proc bridges do not exceed secondary-event budgets, and Gold-generation gains do not make cosmetics or Ledger costs trivial. Retest raid difficulty with changed attacker output; keep raid loss rules unchanged until evidence calls for adjustment.

Review saturation: number of capped/no-benefit drops, share of level offers useful, time to first meaningful combination, frame time during Stormfire, and minutes spent after boss victory. Favor changes that alter routing or build decisions over simply adding more enemies/Items.

Implementation scope: starter Captains plus one unlock, pressure beats, revised increments and temporary pickups enter the prototype sequence. Remaining Captain portraits/unlocks follow the full content milestone. Existing Weapons/Items are amended in place in the combat spec; their old values must not be implemented alongside this revision. All exact new data remains unvalidated prototype tuning.
