# Rogue Pirates — Economy, revenue and operating-cost review

14 September 2026. Historical analysis preceding the owner's v3.2 decisions. [Economy, Ads, IAP and LiveOps v3.2](Rogue_Pirates_Economy_LiveOps_v3_2.md) supersedes its trading, banking, passive-production, ad and monetization recommendations. The revised financial model removes marketplace income and uses the new ad placements.

Owner inputs: audience size, geography, platform mix, business jurisdiction and operating budget are currently undecided. Consequently, all engagement, conversion, spending, ad yield, staffing and workload assumptions below are explicit examples. Vendor rates have sources; a source does not validate our player or performance assumptions.

Open the [interactive financial model](Rogue_Pirates_Financial_Model.html) to change assumptions. Its [saved scenario results](Rogue_Pirates_Financial_Results.json) contain the base/downside/upside calculations and source list. The [Cursor canvas](C:/Users/shara/.cursor/projects/c-Kshitij-Work-X2C-web3-GitHub-Pirate-Game/canvases/rogue-pirates-economy.canvas.tsx) provides the same calculation engine in a side panel. The HTML works offline; no external data requests or payments occur.

## 1. Why only 25% of run Gold is banked?

The 25% conversion in [economy §4.2](Rogue_Pirates_Economy_Systems_v3_1.md) was introduced as an unvalidated prototype tuning assumption. It was not an owner-approved requirement or a proven Megabonk/Clash rule. It affects only leftover run Gold after chest purchases; secured boss grants and actual raid theft are separate.

Its rationale was to limit generated treasury income while retaining an incentive to spend run Gold on chests. However, death is the required ending even after victory. Presenting the normal ending as losing 75% of a visible balance can feel arbitrary. There is no evidence yet that exactly 25% is necessary.

Example: 500 unspent run Gold and one Saltwind depth-1 boss currently yield 125 converted Gold +120 boss Gold =245. At 50% conversion the same inputs yield 370; at 100%, 620. Rum is unaffected. Raising conversion to 100% quadruples the leftover-Gold component, not the entire payout.

**Recommendation: test keeping 100% of unspent run Gold on normal death, whether or not the boss was defeated.** Preserve boss bonuses and raid theft. Tune the whole flow using observed kills, chest spending and upgrade affordability. Do not apply an additional failure haircut merely because a player died, and do not silently multiply all upgrade prices by four.

The counter-risk is chest skipping: each 100 Gold spent on a chest would cost 100 future treasury Gold instead of 25. Measure whether better builds earn enough additional rewards to make spending worthwhile. Compare 25%, 50% and 100% with identical starting profiles, then human playtests; a static payout comparison cannot predict changed behavior.

Alternative if hoarding dominates: calculate the permanent Voyage payout from meaningful combat/objective progress independently of leftover run Gold, keeping run Gold principally for chests. That is a larger explanation/UI change, not a third currency requirement. Do not adopt both payout systems accidentally. No rate change is applied to the GDD until the owner selects the direction.

## 2. What Rum does now

Gold supports the Territory economy: production/storage/defense upgrades, raid entry and cosmetics. Run Gold additionally buys temporary chests, and cannot be topped up from treasury.

Rum supports collection and build variety: Weapon/Logbook licenses, permanent slot unlocks, initial specialized Item ownership, permanent RNG-tool unlocks and reforging. It also buys cosmetics. It is not run energy, a timer currency or a required payment to start ordinary Voyages.

Current Rum sources:

- Passive supply at 8–180/hour by Ledger rank, capped at eight hours uncollected.
- Generated boss reward `30 × biome × stage depth`, with the specified Plunderer modifier.
- Kill-based end payout `min(20 × highest biome, floor(rewardable kills/100))`, including runs that also killed a boss.
- First claim grants 180; each of the three mastery first clears grants 30.
- Permanent reward chest grants 20 Rum instead of Item progress when no specialized Item is qualified; consumable-cap overflow can also give 20.
- Raid theft transfers exposed defender Rum; operator event rewards may grant it under published rules.

Current Rum uses: slots total 1,900; five nonstarter Weapons total 980; six unlockable Logbooks total 1,540; RNG tools total 500; all 20 initial specialized Item purchases total 6,160. Together these are **11,080 Rum of finite purchases** if each initial Item entitlement is bought rather than obtained another way. Quest and Zone gates still apply. Reforge costs 20 Rum plus three source Item progress units; it is not an unlimited Rum-only sink. Cosmetics are the main remaining repeatable Rum purchase.

Recommended identity: **Gold develops and protects home; Rum expands the collection and playstyle options.** Keep both earnable through play. Do not introduce Rum entry fees to manufacture scarcity while promising uninterrupted runs.

## 3. Economy blind spots

### Passive production can overwhelm the active loop

At rank 1, one eight-hour collection gives 320 Gold/64 Rum; three well-timed collections in a day give 960/192. At rank 9 these become 7,200/1,440 and 21,600/4,320. Full-day collection requires three visits and sufficient storage/spending; it is not one login's yield.

The current sample successful Saltwind run yields 245 Gold/42 Rum. Two such runs give 490/84. This is an illustrative early-run comparison, not a claim about expected late-game payout. Nevertheless, rank-9 Rum production can nominally cover the entire finite 11,080-Rum purchase list in about 2.6 days, before unlock gates and assuming it was not already spent. Even once-daily collection can mint 1,440 Rum/day after finite unlock demand falls away.

All five Ledger tracks together cost 158,550 Gold to buy ranks 2–9. Those are finite sinks too. At full progression, recurring production continues while upgrades stop. More storage delays overflow; it does not solve a lasting income/spending imbalance.

Mitigation: flatten late passive growth; set it as a supplemental source relative to measured same-tier active rewards. Test one-, two- and three-collection schedules. Add repeatable cosmetic demand only where players actually want the art; do not create endless stat ranks or forced fees merely to consume resources.

### The same cosmetic sold for earned currency and cash can undermine IAP demand

If a cosmetic can be repeatedly bought cheaply with abundant Rum and resold, its market supply can compete directly with its paid storefront version. Tradability does not ensure premium pricing, recurring demand or fee revenue.

Recommendation: keep resource-funded everyday styles and define a distinct paid artistic catalogue—special sails, trim, emblems, coordinated themes—using shared rigs. Both may remain tradeable where supported, as requested. This separates acquisition sources without introducing paid combat advantages or silently binding earned cosmetics. It is a proposed catalogue decision requiring owner approval, not a reason to restrict all trading.

Do not promise scarcity for an unlimited SKU. A buyer must see whether a pack is repeatedly available, earnable, transferable and compatible with their channel. A winner's achievement remains with the winner even if its cosmetic is sold.

### Cosmetics can shelter resources from raids

Spending exposed Gold/Rum on nonlootable tradeable assets moves value out of the treasury. This supports the owner's desire to encourage spending, but may reduce attractive raid targets and weaken the economic case for defenses. Storage upgrades alone do not fix this.

Test exposed wealth, savings time for the next upgrade, actual raid yields and cosmetic purchases together. Preserve the accepted principle that larger empty storage does not increase losses. Do not add cosmetic theft or a guaranteed resource buyback as an unrequested workaround.

### Failure rewards before first conquest need a capacity rule

The current account has no activated treasury/capacity until first claim. Failed first-conquest attempts can therefore lose generated currency to capacity rules, despite the desired rewarding-failure loop. Qualified permanent Item progress may still be useful; that does not settle currency behavior.

Recommended proposal: a small explicitly capped pre-claim purse, transferred once when the home is claimed, or initial storage without passive production. Choose one, show it clearly and test repeated failed attempts. Do not use an unlimited protected mailbox.

### Fast failures and boss farming can dominate reward-per-minute

The post-run chest eligibility threshold is short relative to a multi-stage run. Repeatable contracts, training raid rules, boss grants and the one-chest-per-run structure may favor repeated short attempts over taking portals. Grade improvements must justify greater time/risk without making failures worthless.

Measure resource/progress per active minute by mode and stage. Evaluate intentional deaths after eligibility, early boss kills, first-conquest retries and home presets. Distinguish legitimate efficient play from zero-credit summons/duplicate-grant exploits. No forced extraction or guaranteed ten-minute run cap is needed.

### Single-board competition and account power remain an accepted tradeoff

Permanent slots, Item ranks and collection breadth already affect opportunity. Selling timer acceleration or combat currency would increase this effect. Recommend cosmetic-first monetization and defer paid acceleration until the effect on raids and competition is measured. Do not introduce paid rerolls, resurrection or guaranteed build ingredients.

### Inflation and fraud must be measured across the whole economy

Ordinary enemy/boss/passive rewards create resources; upgrade/cosmetic/chest/reforge costs consume them; treasury raids largely transfer resources rather than remove them. Initial grants, fee refunds, Defense Chest compensation, event grants and overflow also affect totals. Exposed loot percentages are not an economy-wide resource sink.

Record every source/sink/transfer separately. Replay verifies legal simulation, not whether inputs were human. Bots and multiple accounts can still farm tradable supply and consume server resources. Observe related-account trading, repeated raid pairs, fee bypass, refunds and suspicious acquisition patterns. Do not subsidize network costs on unlimited low-value transfers before measuring abuse.

## 4. Proposed revenue design

### Primary: known cosmetic purchases on compatible channels

Start with a small catalogue: a roughly $4.99 single style, $9.99 coordinated pack and $14.99 supporter/partner pack are **test price points**, not final SKU prices. Include a preview and exact contents; no paid random loot. Reuse hull geometry, animations, sail masks and palette slots to support the low-content goal. Do not assume everyone buys monthly: the model uses a small payer fraction and monthly spend per payer.

Native stores use their approved billing/entitlement flows; direct web uses an eligible provider; Steam has its own integration. Direct-web processing uses a US domestic-card fee reference only because incorporation is unknown. The cash/SOL checkout, marketplace custody and tradeability of paid entitlements remain product/provider decisions. Subscription/pass revenue is excluded until we can sustain the promised art cadence.

### Supplement: partner service fees

Charge a sponsor, where commercially agreed, for preparing a reusable skin variant, configuring the event, hosting verification and administering results. Keep this separate from prize funding. Example pricing test: $500 service fee against $150 fulfillment cost yields $350 before shared overhead, not $500 profit. Those are proposals; the baseline counts zero unsigned events.

Money a sponsor supplies solely to distribute to winners is a pass-through liability, not studio revenue. If the studio funds a prize, record an expense. Cash delivery can remain manual, as requested. A community partnership is not automatically a paying sponsorship.

### Supplement: marketplace fees, after viability is proven

At a 3% fee, $10,000 of completed captured trading volume produces $300 of gross fees—not $10,000 revenue. Payment/RPC/indexing, support, refunds and fraud costs remain. Trades that bypass our marketplace may generate no fee.

The model starts marketplace income at zero. Optional inputs use eligible audience, trader fraction, spend, captured share and settlement allowances. There is no assumed SOL appreciation, token emission, guaranteed liquidity or forced Item expiry. Maintain the ability to reacquire all gameplay Items through play.

### Channel-specific ads, not ads funding every player

Steam does not support advertising as the game's monetization model, so Steam ad income stays zero. [Steam advertising rules](https://partner.steamgames.com/doc/marketing/advertising?language=english)

For native/direct builds, the initial model assumes no ads. A later test could use infrequent non-rewarded ads after results, never during combat or between stages, but must measure damage to immediate retry/retention. Portal scenarios allow SDK-managed natural-break opportunities, modeled at one opportunity per two runs before fill, with no resource reward. This is a proposed experiment, not an approved placement schedule or guaranteed impression count.

AdMob's rewarded rules require non-transferable rewards and include indirect conversion to transferable assets. Since Gold/Rum can buy tradeable cosmetics, “watch for Gold/Rum/Item progress” is not a safe baseline under those rules. Do not silently change tradeability to accommodate ads. [AdMob rewarded rules](https://support.google.com/admob/answer/7313578?hl=en-GB)

Poki documents SDK ads and no IAP. Its specific partnership guide describes preferred web-exclusive revenue sharing and a possible non-exclusive one-time license fee without ongoing share. That conflicts with assuming ordinary recurring Poki ad revenue alongside simultaneous direct-web/CrazyGames distribution. Baseline Poki recurring revenue is therefore zero pending a compatible agreement; an upfront license fee is not fabricated or repeated every month. [Poki monetization](https://developers.poki.com/guide/how-monetization-works), [deal types](https://developers.poki.com/guide/revenue-deal-types)

CrazyGames supports ads and optional IAP, but we have not verified our contractual share. The model's 50% developer ad share is an editable **assumption**; portal IAP stays zero until integrated and validated. Portal availability and marketing exposure are not guaranteed users. [CrazyGames FAQ](https://docs.crazygames.com/faq/)

## 5. What the financial model actually assumes

The model is a steady-state 30-day month evaluated at 1k, 10k, 50k, 100k, 500k and 1m **unique monthly active accounts**. It does not predict how quickly we acquire them. To avoid double-counting cross-platform players, each account is allocated once to a primary channel in this first model. Actual telemetry must later count unique accounts separately from platform visits and additional sessions.

Illustrative audience mix: direct web 10%, Android 30%, iOS 20%, Steam 10%, CrazyGames 15%, Poki 15%. Even the assumed nonmonetizing Poki users incur gameplay hosting costs. Change the mix if that channel is not accepted or cannot support the account/economy design.

Base purchase assumptions: direct 2% monthly payers spending $12 each; Android 1.5% spending $10; iOS 2% spending $12; Steam 2.5% spending $12. Portal IAP is zero. These are not industry benchmarks or observed Rogue Pirates performance.

DAU/MAU: 20% direct/native, 25% Steam, 10% portals. Runs per DAU per day: 1.5 direct/native/Steam and 1.2 portals. Mean run lengths: 18/22/12 minutes respectively. Sessions include failures and longer successes; they are not stage-timer claims.

Consumer IAP billings are reduced by a 3% refund assumption and converted from tax-inclusive to tax-exclusive using an illustrative 10% effective indirect-tax rate. Store share defaults to 30% on net-of-tax/refund store sales. Direct processing is charged on original billings, including a $0.30 per-order allowance at 1.5 orders/payer/month; this conservatively retains processing expense after refunds. Corporate income tax and withholding are not guessed.

Apple has a conditional enrolled 15% small-business rate. Google's current fee schedule distinguishes regions/programs/install cohorts; 15% is not a universal permanent rate. Change the effective fee input when actual eligibility/geography is known. Stripe's 2.9% +$0.30 reference is for US domestic cards and does not include a merchant-of-record service or universal international processing. [Apple program](https://developer.apple.com/app-store/small-business-program/), [Google fees](https://support.google.com/googleplay/android-developer/answer/112622), [Stripe pricing](https://stripe.com/pricing)

Base portal ad inputs: 0.6 opportunities/DAU/day, 75% combined fill/eligible-display fraction, $8 eCPM before portal split. These already reflect only a modest between-run opportunity count. Do not multiply impressions by fill twice, or take portal share again from an eCPM already reported net to the developer. Geography, consent, ad blockers and retention can change the result substantially.

Downside halves payer conversion, reduces payer spend to 75% and eCPM to 50%, and assumes slower verification (0.20 CPU seconds per simulated second). Upside doubles conversion, increases spend 25% and eCPM 75%, and assumes 0.02 CPU seconds per simulated second. Base uses 0.05. These are sensitivity cases, not probability bounds; a failed launch can earn less than Downside.

## 6. Server and recurring technology costs

Retain the proposed portable architecture: Unity client, shared C# simulation verifier, API/worker/PostgreSQL, object storage/CDN. Avoid operating both MongoDB and PostgreSQL without a concrete need. Start with one appropriate region; a global low-latency/high-availability design is additional scope. The earlier tiny Railway example was an idle resource illustration, not a multiplayer traffic quote.

The model derives runs and active simulation seconds from each platform's activity. CPU verification work = simulated seconds × CPU ratio ×1.10 duplicate-work allowance. The base 0.05 ratio means twenty simulated seconds per CPU second on one core, which is **unbenchmarked**. Additional API/DB CPU and idle budget are modeled separately. Peak concurrency is 4× average with 1.5× CPU headroom; peak worker memory is conservatively reserved all month.

Input requests every two active seconds and checkpoint writes every ten seconds make backend work substantial. At 100k MAU in the base mix, the model produces about 760,500 runs/month, 312 average active simulations, 1,249 peak, 414.7m API requests and 89.0m checkpoint/transaction writes. The approximately 105-vCPU peak envelope must be tested. Cheap CPU pricing does not prove that database IOPS, connection pools or queue latency can support it.

Live checkpoints overwrite current state; they are not all retained forever. Replay chunks are uploaded every 30 seconds with an assumed compressed 120 bytes per simulation second, retained 30 days. Account storage uses 10 stored accounts per MAU; ledger working storage uses 12 months of generated records. Two database-copy equivalents are included in object storage for a basic backup allowance. Winners/disputes, legal retention, PITR logs and high availability can require more; do not treat this as an approved retention policy.

Railway rates used: CPU $0.00000772/vCPU-second, RAM $0.00000386/GB-second, volume $0.00000006/GB-second, service egress $0.05/GB. The $20 Pro minimum includes usage; it is not added again on top of a larger bill. R2 includes storage/operation allowances; billable units round up. Its free internet egress does not erase Railway's charge to upload data to it. [Railway](https://railway.com/pricing), [R2](https://developers.cloudflare.com/r2/pricing/)

At 100k MAU base: Railway CPU about $357, RAM $329, disk $4, service egress $26; R2 storage/operations about $142. With 25% hosting contingency, hosting is about **$1,072/month**. At 10k MAU, about $142. At 1m MAU, about $10,516. These are workload-based budget estimates, not vendor quotes or tested capacity promises.

R2 writes dominate its small storage bill in this model. Do not persist every input network request as a separate object or log every particle/hit into SQL. Batch compact inputs, separate current checkpoint state from audit history, reuse private-network traffic and measure compression. Longer durable chunks require recovery tradeoff testing; do not reduce integrity to obtain a cheaper estimate.

Other modeled recurring technology: two budgeted Unity Pro seats ($385/month when annual cost is amortized), Apple enrollment ($8.25/month equivalent), domain $2, build/signing $60, monitoring $25, email $10, miscellaneous technology $20, plus $0.001/MAU of extra tools allowance. Except the cited licenses/enrollment, these are budget allowances, not quoted vendor plans. Base all-tech cost is **$1,683 at 100k MAU**, including hosting. Unity Personal may be available within its revenue/funding eligibility; the model deliberately budgets paid seats rather than assume it. Unity has no Runtime Fee. [Unity pricing](https://unity.com/products/pricing-updates), [Apple enrollment](https://developer.apple.com/programs/enroll/)

One-time/irregular costs remain separate: Google $25 enrollment; Steam $100 per app, potentially recoupable under its terms; iOS build hardware/access, test devices, initial assets, security/payment review, incorporation and development. Annual prepaid licenses require cash before their monthly expense is recognized. Local build hardware can replace a service allowance, but has purchase/depreciation/maintenance costs. [Google enrollment](https://support.google.com/googleplay/android-developer/answer/6112435), [Steam Direct fee](https://partner.steamgames.com/doc/gettingstarted/appfee)

Marketplace is off initially. When enabled, the model adds an editable RPC/indexer allowance, per-transaction and variable settlement costs. These are not SOL network-fee quotes; custody/provider/tax treatment remains undecided. Player network fees must not be silently counted as studio revenue or studio expense twice.

## 7. Profitability and thresholds

The base fixed operating example is $3,500 team +$1,000 art/content +$500 support/admin =**$5,000/month**, above technology. Marketing, studio-funded prizes and unsigned sponsor revenue are zero until changed. This does not imply that such a team can operate every audience size; the 500k/1m rows hold budget constant for comparison and must be rebudgeted for actual staffing, fraud, partners and support.

At 10k MAU: approximately $963 net studio receipts, $662 technology, and **$4,699 operating loss**. At 50k: $4,815 receipts, $1,109 technology and **$1,294 loss**. At 100k: $9,630 receipts, $1,683 technology and **$2,948 surplus** before income tax and development recovery.

Base break-even estimates: about **6,500 MAU for all recurring technology**, **65,400 for technology plus $5k operating budget**, **124,300 with a $10k operating budget**, and **301,300 with $25k**. Hosting alone crosses much earlier, around 600 MAU, but that is not a viable-studio threshold.

At 100k MAU, Downside loses about $5,328/month and Upside earns about $17,828. Downside remains negative at 1m MAU; its mathematical crossing beyond 2.7m is an extrapolation far beyond validated capacity, not a business plan. Raising MAU without improving weak unit economics may not solve profitability.

Marketplace-only revenue needs about $166,667/month captured GMV to earn $5,000 at a 3% fee **before** settlement/technology/support. That is why the studio should not rely on marketplace fees to fund early development.

Base contribution LTV is only roughly $0.21 under an illustrative geometric 60% active-month retention, before fixed operating costs. This is a simplified sensitivity, not a measured acquisition bid limit. Paying $1 to acquire a user would not be justified by that case. Actual cohort retention, repeat purchases, platform switching and net acquisition conversion must replace the simplification before paid growth.

The calculator can show steady-state payback of an illustrative $25k unrecovered development budget and burn against $25k cash already available after setup. These are editable examples. At 100k base MAU, recovery is roughly 8.5 profitable steady-state months, **after reaching that scale**, ignoring ramp-up and income tax. It is not eight months from starting development. Receivables, payout delays, withheld taxes, annual prepayments and prize liabilities can require additional working capital even when the monthly result is positive.

## 8. Recommended decisions and next measurements

1. Test 100% leftover-Gold banking against the existing 25% profile, with the same chest/upgrade baseline initially; observe behavior before changing prices. Keep the approved death-only ending.
2. Keep Rum's collection role, flatten passive production and validate finite-versus-repeatable spending across new/mature accounts. Decide the capped pre-claim wallet behavior.
3. Approve a small distinct paid cosmetic catalogue and its tradeability by channel. Keep real-money combat power, paid random chests and paid revival out of the first revenue test.
4. Treat portal monetization as its own unit-economics test; obtain contract terms, especially Poki's distribution/revenue compatibility. Consider a negotiated non-exclusive license only if it covers its expected support/hosting obligation.
5. Benchmark replay CPU, concurrency, memory, API/write rate and request batching on the Saltwind slice before selecting a production plan.
6. Collect D1/D7/D30 retention, DAU/MAU, runs/time per user, chest spending, earned/spent/overflow by currency, unlock timing, payer conversion, spend/repeats/refunds, filled ad opportunities and verification cost per completed run.
7. Refit this model after the first cohort; select an operating budget, jurisdiction and cash runway. Do not use platform audience totals as a forecast of our own game users.

The gameplay proposals above remain under discussion. Existing GDD numerical rules are flagged for review rather than silently replaced. More content is not the immediate missing piece: sustainable resource flows, a desirable small cosmetic catalogue and measured retention/verification costs are.

## 9. Completion and verification

The interrupted economy review is complete: source-backed platform constraints, editable audience scenarios, recurring workload costs, operating thresholds and recommended economy changes are saved. Automated checks passed for channel reconciliation, payment-fee sensitivity, optional marketplace income, replay-cost sensitivity, zero activity/revenue, break-even boundaries and invalid inputs. The browser model was visually inspected and its Base/Downside controls recalculated correctly at 100,000 MAU. The canvas passes TypeScript syntax transpilation and matches its workspace copy; compilation/rendering inside the canvas host was not independently verified.

Pending owner decisions are the Gold banking direction, Rum production/repeatable-spending balance, and paid-cosmetic catalogue. Pending evidence is player cohort data, server benchmarks, platform contracts and an actual operating budget/jurisdiction. These are not supplied by the illustrative model and remain required before treating it as a launch forecast.
