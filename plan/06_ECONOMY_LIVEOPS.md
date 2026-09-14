# 06 — Economy, Ads, IAP and LiveOps

## Currency tables

Ranks 1–9 Gold/hour: `10,15,22,32,45,60,80,105,135`; Rum/hour: `.5,.75,1.1,1.6,2.3,3.2,4.5,6.2,8`. Producer buffer is 8 hours. Gold capacity: `2k,4k,8k,16k,32k,75k,150k,300k,500k`; Rum: `600,900,1400,2200,3400,5200,8000,12000,18000`.

Producer upgrade Gold: `400,1200,3500,8000,18000,40000,90000,180000`; capacity: `500,1500,4000,9000,20000,45000,100000,200000`; defense: `600,1800,5000,12000,26000,60000,130000,250000`. Times: `5m,30m,2h,6h,18h,2d,4d,7d`. One queue; charge at start; no cancellation or paid acceleration.

Run Gold chest price is `ceil(min(1200,25×1.16^k))`. Death banks all remaining run Gold. Boss reward is `120×biome×depth` Gold and `30×biome×depth` Rum. Kill Rum is `min(20×highestBiome,floor(validKills/100))`.

Rum buys slots, licenses, RNG tools, initial specialized ownership, contracts, reforging and cosmetics. Contracts cost 40/80/160/240 by rarity and require two qualifying bosses +300 kills for three progress. Reforge costs three same-rarity source progress +20 Rum for one destination progress. Holding thresholds are `1,3,7,13,21,31,43,57,73,91,111`.

Cosmetic bands: 500G/100R sail; 1k/200 flag; 5k/1k ornament; 15k/3k ship; 50k/8k prestige. Gilding unlocks at Gold capacity rank 6 and costs `ceil(5000×1.35^g/100)×100`; five visual bands, then numeral/prestige only.

## Ads and purchases

Sponsored Cache after finalized results: choose 75 Gold or 5 Rum; two/account/UTC day, second after another run. Failed ad consumes nothing. One missed Daily Chart stamp may be restored per seven days. Native/direct uses no forced interstitials; Steam none; portals only between runs/results, one per two runs and three/day.

Products: $1.99 Welcome, $4.99 Ship Style, $9.99 Captain Set, $14.99 Territory Theme, $19.99 Collaboration Set, $7.99 28-day Chronicle. Localize prices. Validate/restore/refund server-side. No product grants functional currency or power.

## LiveOps budgets

- Daily Charts total ≤60G+3R.
- Weekly Orders ≤250G+12R.
- Crew weekend ≤200G+8R/player.
- Weekly 48h overlay ≤150G+6R.
- Chronicle: 30×10-Mark steps. Daily Marks 3 first run +2 first boss +2 two Charts; four weekly missions ×26. Free ≤600G+30R+3 consumables+1 cosmetic; premium cosmetics only.
- Competitive rewards are cosmetics/status. External prizes use event-specific manual operations.

## Economy control

Every ledger entry has account, source/sink type, source ID, currency, amount, balance-before/after, content version and idempotency key. Never silently delete above-cap progress. Ordinary currency overflow is shown and discarded; one-time milestone overflow is protected within its stated limit. Run deterministic and Monte Carlo models for P10/P50/P90 cohorts before every live balance version. Launch requires four weeks of closed telemetry and guardrails: ads <5% supply; <10% of progressing accounts lose >10% weekly generation to overflow; median next affordable upgrade 1–3 play days within a Zone.

