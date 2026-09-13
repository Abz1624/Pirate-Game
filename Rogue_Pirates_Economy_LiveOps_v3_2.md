# Rogue Pirates — Economy, Ads, IAP and Live Operations v3.2

**Decision date:** 14 September 2026  
**Status:** owner decisions plus simulation-derived prototype values. Values remain remotely configurable and must be retuned from playtest telemetry.

This document supersedes trading, banking, passive-production, economy-monetization, and live-operations rules in v3.1 documents where they conflict.

## 1. Owner decisions and boundaries

1. Player-to-player Item and cosmetic trading is removed. There is no marketplace, listing, sale, buyer/seller transfer, wallet requirement, onchain inventory, marketplace fee, or trading-hub link.
2. Items, progress, ship skins, Territory decorations, titles, and collaboration rewards are account-bound. Duplicate Item progress merges into the owner's existing holding. Selling an owned gameplay Item is removed.
3. At valid run death, bank **100% of unspent run Gold** after chest purchases. Death never applies another percentage penalty. Already-secured boss rewards and raid theft remain separate receipts.
4. Gold and Rum cannot be purchased directly at launch. Paid products do not grant combat stats, leaderboard advantages, revivals, paid random chests, or Item progress.
5. Ads never interrupt active combat, continue a dead run, alter the submitted score, or create a reward that can be traded.

Manual SOL/cash prizes funded and fulfilled by a collaboration partner can still be described in a specific Crew event. They are external prizes, not player trading or an in-game currency.

## 2. Currency roles

**Run Gold** is earned during the active run, buys temporary stage chests, carries through Voyage portals, and becomes treasury Gold at death. Treasury Gold never enters a run.

**Treasury Gold** develops the Territory through Ledger upgrades, capacities, and Defense Director; pays raid entry; and funds visible vanity projects. It is the high-volume currency.

**Rum** expands collection choice: Weapon and Logbook licenses, slots, RNG tools, initial specialized Item ownership, targeted contracts, reforging, and select cosmetics. It is lower-volume and should feel deliberate. It is not energy and never gates an ordinary Voyage.

Raid theft transfers existing Gold/Rum between accounts and is neither a global source nor sink. The sunk half of a raid entry fee is a Gold sink; defender compensation transfers the other half.

## 3. Run economy

Enemy base Gold remains the combat table's 1–5 units before pickup modifiers. Keep the current `Quartermaster`, Captain Vale, Cursed Coin, elite, and Smuggler's Oath modifiers and their caps.

Stage chest price becomes:

`ceil(min(1,200, 25 × 1.16^k))` run Gold

where `k` is paid chest openings across the whole run. A free Skeleton Key opening does not increment `k`. Smuggler's Ledger applies its existing 20% increase after the base cap. Twelve purchases cost about 780 Gold before Smuggler's Ledger. This makes opening every chest a real build investment when all leftovers are banked.

At death:

`banked_run_gold = floor(unspent_run_gold)`

No boss requirement applies. An expired/invalid run still receives no unsecured payout under interruption rules. Generated boss Gold/Rum, kill Rum, and raid theft retain their existing formulas and once-only settlement.

Example: 500 remaining run Gold plus a Saltwind boss grant pays 620 treasury Gold: 500 retained +120 boss Gold. The matching Rum example remains 42 when it contains 12 kill Rum and 30 boss Rum.

## 4. Revised passive production, capacity, upgrades and timers

Passive production is deliberately smaller than regular play. A producer buffers eight hours; one, two, or three well-spaced collections capture at most 8, 16, or 24 production hours daily.

| Ledger rank | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Gold/hour | 10 | 15 | 22 | 32 | 45 | 60 | 80 | 105 | 135 |
| Rum/hour | 0.5 | 0.75 | 1.1 | 1.6 | 2.3 | 3.2 | 4.5 | 6.2 | 8 |
| Gold capacity | 2,000 | 4,000 | 8,000 | 16,000 | 32,000 | 75,000 | 150,000 | 300,000 | 500,000 |
| Rum capacity | 600 | 900 | 1,400 | 2,200 | 3,400 | 5,200 | 8,000 | 12,000 | 18,000 |

Ranks 4–6 still require Gravewater and 7–9 require Devil's Maw. One shared works queue remains.

| Upgrade to rank | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Gold or Rum producer cost, Gold | 400 | 1,200 | 3,500 | 8,000 | 18,000 | 40,000 | 90,000 | 180,000 |
| Gold or Rum capacity cost, Gold | 500 | 1,500 | 4,000 | 9,000 | 20,000 | 45,000 | 100,000 | 200,000 |
| Defense Director cost, Gold | 600 | 1,800 | 5,000 | 12,000 | 26,000 | 60,000 | 130,000 | 250,000 |
| Works time | 5 min | 30 min | 2 h | 6 h | 18 h | 2 d | 4 d | 7 d |

The five tracks contain about 1.93m Gold in total and roughly 70 days of continuous queue time. A player can attempt expansion before maxing a band. Timers set long-term cadence; combat remains unlimited.

## 5. Repeatable sinks

The existing once-only Rum catalogue costs 11,080 Rum if every initial specialized Item is bought. It cannot sustain a mature economy alone.

Starting a repeatable targeted Item contract now costs Rum when accepted: Uncommon 40, Rare 80, Epic 160, Legendary 240. The reward and gameplay requirements remain three Item progress after two qualifying boss clears plus 300 kills. Abandoning a contract does not refund the commission. Random post-run Item progress remains free.

Reforge remains three source progress +20 Rum for one destination progress of the same rarity. No listing locks are needed. Reforging to zero removes ownership eligibility until gameplay restores a progress unit; the original quest remains complete.

Account-bound cosmetics use these launch bands:

- Sail recolor: 500 Gold or 100 Rum.
- Flag design: 1,000 Gold or 200 Rum.
- Territory ornament set: 5,000 Gold or 1,000 Rum.
- Ship appearance set: 15,000 Gold or 3,000 Rum.
- Seasonal prestige set: 50,000 Gold or 8,000 Rum.

Add **Gilding** as a content-light, repeatable vanity sink after Gold capacity rank 6. A player gilds any owned ship skin, decoration, or trophy. Level `g` costs `ceil(5,000 × 1.35^g / 100) × 100` Gold. Visual art has five intensity bands; later levels increase the displayed Roman-numeral level and account's lifetime Gold-spent record without combat effect. Gilding cannot obstruct navigation or improve raid defense.

This sink is optional. A player who declines it can remain near storage capacity and therefore display more exposed wealth to raiders. The game does not create a hidden tax or delete treasury below the published capacity.

## 6. Rewarded ads and interstitials

### 6.1 Sponsored Cache

After a completed run's results are finalized, offer one rewarded video for a choice of **75 Gold or 5 Rum**. A second daily offer unlocks only after completing another run. Hard cap: two claimed Sponsored Caches per account per UTC day across all devices and channels.

The receipt is server-issued after the ad provider confirms completion. A failed/closed ad consumes no claim. The player chooses the resource after completion; overflow rules are previewed before watching. The reward cannot modify the completed run, its chest, Item roll, kills, boss state, or leaderboard submission.

One additional rewarded placement may restore one missed Daily Chart stamp per account per seven-day period. It grants no direct resource and cannot complete a competitive score requirement.

Do not offer ads for revival, post-death farming, double run Gold, double boss theft, permanent Item progress, more stage chests, shorter works timers, or leaderboard multipliers.

### 6.2 Channel placement

- Android, iOS and direct web: optional rewarded ads only; no forced interstitials at launch.
- Steam: no ads.
- CrazyGames, Poki and similar portals: their required gameplay/ad lifecycle applies. Interstitials may appear only after results or between runs, no more than one per two completed runs and three per UTC day. Never show one during loading into a run, combat, boss settlement, level-up choice, or a Crew score reveal.
- If a portal contract gives no recurring revenue, its impressions remain zero-revenue in projections even though its SDK requires ads.

The base revenue scenario uses 0.35 rewarded impressions per DAU/day on native/direct builds and a blended 1.25 impressions per DAU/day on portals (0.35 rewarded +0.90 interstitial), 75% fill, $12 rewarded-style eCPM on native/direct, and $7.70 blended portal eCPM before portal share. These are editable assumptions, not market guarantees.

## 7. IAP catalogue

Royal Match currently sells known coin/treasure packages and a monthly 30-step Royal Pass with free/premium tracks, social gifting, and a capped post-completion Bonus Bank. It also runs many simultaneous solo, race, team and collection events driven by completing normal levels. [Royal Match coins](https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/21-how-can-i-obtain-and-use-coins/?l=en), [Royal Pass](https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/112-royal-pass/), [events](https://dreamgames.helpshift.com/hc/en/3-royal-match/section/23-events-and-tournaments/), [US App Store products](https://apps.apple.com/us/app/royal-match/id1482155847)

Zen Match's current store description emphasizes a ten-minute daily habit, daily puzzles, room decoration and cosmetic collections; it discloses IAP including randomized items. A 2022 third-party deconstruction documented historical patterns including a weekly pass, daily calendar, streaks, piggy bank, coin/booster bundles, frequent ads and a remove-ads product. Those historical details may have changed and are used as design evidence, not copied as current facts. [Zen Match App Store](https://apps.apple.com/us/app/zen-match-relaxing-puzzle/id1560124228), [historical deconstruction](https://naavik.co/f2p-mobile/zen-match-hypercasual-casual/)

Rogue Pirates adopts four principles: one play session advances several visible goals; known-value offers are clearer than paid randomness; a pass rewards sustained engagement; and social purchases can create a small Crew celebration without transferring power. It does not copy paid continuation pressure, energy/lives, repeated exit prompts, or frequent forced interstitials.

Launch price architecture, localized through each platform:

- **Deckhand Welcome, $1.99:** one permanent flag, one profile portrait frame, one arrival wake color. Once per account.
- **Ship Style, $4.99:** coordinated sail, hull trim, wake and flag.
- **Captain's Set, $9.99:** premium ship appearance, captain portrait treatment, nameplate and Territory ornament.
- **Territory Theme, $14.99:** coordinated section dressing, dock treatment, ambience palette and ship accent. Geometry and visibility remain unchanged.
- **Collaboration Supporter Set, $19.99:** disclosed partner-themed cosmetics. Revenue-share terms belong in the partner contract.
- **Captain's Chronicle, $7.99 per 28-day season:** premium cosmetic reward track described below. It is a non-renewing season purchase unless a store later implements and discloses a subscription separately.

Purchases are account-bound, known before payment, restorable, and deduplicated across linked accounts where the platform permits. Do not sell Gold, Rum, chests, Item progress, permanent power, run revivals, leaderboard score, Crew placement, raid protection, or works acceleration at launch.

The catalogue deliberately has weaker short-term monetization pressure than Royal Match's purchasable extra moves/boosters. Revenue projections must therefore use Rogue Pirates conversion data rather than assuming Royal Match's performance.

## 8. Live-operations cadence

Every event reuses verified run facts. It does not need a new map or enemy family unless the content plan explicitly provides one.

### Daily

Three Daily Charts choose from kills, boss activation/defeat, chest opening, POI use, damage-style and survival objectives. All three together pay at most **60 Gold +3 Rum**. Never require a specific random Item the player cannot guarantee. A completed set awards one daily stamp for the seasonal display.

### Weekly

- Admiralty Orders: seven cumulative milestones using ordinary runs; full completion pays **250 Gold +12 Rum**.
- Global best single-run kill leaderboard: keep the current score. Position rewards are titles, badges, frames, or a trophy appearance—not power or large currency—so winners do not compound a competitive advantage.
- Weekend Crew Voyage: personal and Crew milestones pay at most **200 Gold +8 Rum** per player. Partner top-player prizes follow published event rules and manual verification.

### Rotating overlays

Run one 48-hour solo overlay each week, rotating among enemy-type collection, consecutive boss victories, POI exploration, chest choices, and controlled bargain challenges. Full completion pays at most **150 Gold +6 Rum** plus cosmetic/event progress. Several active overlays may read the same run, but never count one event currency as another currency.

### Captain's Chronicle

The 28-day track has 30 steps of ten Marks, 300 total. A player can earn up to seven daily Marks: three for the first completed run, two for the first boss secure, and two for completing two Daily Charts. Four weekly missions grant up to 26 Marks each. The full schedule supplies exactly 300 without paid tier skips; rotating events can supply make-up Marks only when the design team intentionally budgets them.

The free track totals at most **600 Gold, 30 Rum, three home-challenge consumables and one cosmetic**. The $7.99 premium track adds only known cosmetics: five small pieces, two ship variants, one Territory ornament, one frame, and one title. It grants no Gold/Rum, power, Item progress, score, extra daily ad claim, or works acceleration. After step 30, a capped bonus track awards profile flourishes, not currency.

When a player purchases the Chronicle, their Crew receives a purely visual 24-hour celebration in Crew chat/event UI. It grants no resources and cannot be claimed repeatedly through Crew switching.

### Collections, later phase

A 42-day themed chart collection can reuse cosmetics, lore pages and event badges. Packs come from live events and the free/pass tracks. Duplicates fill a visible duplicate-protection meter; the next missing entry is guaranteed when it fills. There is no player transfer. Limit the first collection to 24 entries, not hundreds, because each entry needs readable art and UI.

## 9. Economy simulation

The companion [simulation engine](planning/game-economy-simulation.mjs) runs 180 deterministic daily expected-value steps. It includes current enemy Gold, pickup rate, player Gold bonuses, chest spending, boss Gold/Rum, kill Rum, revised passive production, claims, storage, the shared works queue, Zone timing gates, upgrade spending, finite Rum purchases, targeted-contract commissions, reforging, cosmetics, Gilding, expected raid losses, live-operations budgets, and capped ad choices.

The profiles are stress cases retained from v3.1: Casual 2 runs/day, Active 6, Highly Engaged 15. They assume respectively 500/1,000/1,600 kills per run, 35%/60%/78% boss success, 6/9/12 paid chests, and increasing biome/depth reward factors. These are hypotheses, not measured player behavior.

| 180-day result | Casual | Active | Highly engaged |
|---|---:|---:|---:|
| Monthly Gold sources | 49.1k | 290.4k | 1.223m |
| Monthly Rum sources | 1.88k | 9.38k | 34.4k |
| Monthly banked run/boss Gold | 31.5k | 248.8k | 1.154m |
| Monthly ad Gold / Rum | 512 / 18 | 1,170 / 42 | 1,890 / 54 |
| Ad share of Gold / Rum sources | 1.04% / 0.98% | 0.40% / 0.45% | 0.15% / 0.16% |
| Gold / Rum overflow share | 0.49% / 0% | 2.70% / 0% | 2.00% / 5.84% |
| Works upgrades completed of 40 | 27 | 34 | 38 |

The ad reward is intentionally small: under 1.1% of simulated currency supply in every profile. This means ads are a convenience and revenue surface, not the main progression strategy.

The largest finding is that 100% banking makes high-frequency play generate vastly more Gold than functional progression can consume. The model contains an assumed 120k/month Active and 900k/month Highly Engaged Gilding/vanity spend. Without an attractive vanity sink of that scale, those profiles quickly cap storage. This is acceptable only if full storage and visible raid exposure feel like understandable consequences rather than punishment.

The simulation is deterministic expected value and does not yet model individual run variance, exact build-dependent kill curves, player response to chest prices, event completion rates, time-varying biome choice, churn, or payer behavior. Monte Carlo runs and telemetry must replace these inputs after the first playable slice.

## 10. Financial-model revision

Player trading, marketplace GMV, fees, RPC/indexer costs and settlement costs are removed from the financial model. At 100k illustrative MAU, the revised Base scenario estimates about **$9,549 IAP net +$1,296 ads = $10,846 studio receipts**, $1,683 recurring technology, the example $5,000 operating budget, and about **$4,163 monthly operating surplus** before income tax and development recovery. Approximate operating break-even becomes **57,200 MAU**.

Ad revenue is about 12% of Base receipts at 100k MAU. It is meaningful but does not replace IAP. Poki remains zero recurring revenue under the modeled non-exclusive arrangement; CrazyGames' 50% developer share remains an unverified placeholder. The adjustable [financial model](Rogue_Pirates_Financial_Model.html) must be refit with actual retention, impressions, fill, eCPM, conversion and platform contracts.

## 11. Telemetry and launch gates

Record source and sink IDs for every currency receipt. Required economy metrics: Gold/Rum earned, spent, overflowed, stolen and protected per account/day; chest purchases/run; resource balance percentiles; upgrade queue idle time; contract acceptance/completion; Gilding adoption; ad offer/availability/start/completion/failure; reward choice; event completion; IAP impression/purchase/refund; and payer/nonpayer retention.

Initial guardrails:

- Sponsored Cache supply stays below 5% of Gold and Rum sources for the median engaged cohort.
- Fewer than 10% of progressing accounts lose more than 10% of generated currency to capacity overflow in a seven-day window.
- Median time to the next affordable functional upgrade is 1–3 play days inside a Zone band, while the works timer establishes the longer overall cadence.
- No paid product produces a statistically meaningful leaderboard lift after controlling for playtime and account progression.
- Suppress or redesign any ad placement that reduces next-run starts or D1/D7 retention more than its net revenue justifies.

Before public monetization: approve the real cosmetic catalogue and asset cadence; integrate receipt validation/refunds/restoration; obtain consent and age treatment for ads; confirm each channel's ads/IAP rules; benchmark the backend; and run a closed economy test for at least four weeks.
