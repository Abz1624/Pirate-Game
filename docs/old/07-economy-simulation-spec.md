# 07 — Economy Simulation Spec

A specification for the model that validates [06 — Economy and Tokenomics](06-economy-and-tokenomics.md) before any of it is committed to code, contracts, or a whitepaper.

This document does not contain the simulation. It contains everything needed to build it.

## Why this exists

Every parameter in [06](06-economy-and-tokenomics.md) is a guess. Some of them are guesses whose consequences only appear eighteen months in, at which point they are expensive to change and impossible to change if they live in a contract.

The simulation exists to answer questions that cannot be answered by argument.

1. At what daily active user count does the burn target of 100–140% of emission actually hold?
2. What does a casual player earn per hour compared to a dedicated grinder, and is that ratio defensible?
3. Does the distribution concentrate? What share does the top 1% take, and does that worsen over time?
4. What happens to a top-decile grinder's yield when the playerbase grows tenfold?
5. How much daily sell pressure does the vesting schedule produce, and does the instant-claim burn meaningfully offset it?
6. How badly does a bot farm distort things, and at what infiltration rate does it become visible to players?
7. If the playerbase collapses, does the economy fail gracefully or catastrophically?

Question 4 is the one most likely to be uncomfortable. See [the tension worth naming](06-economy-and-tokenomics.md#the-tension-worth-naming).

## What to build

A deterministic, seeded, agent-based simulation stepping in **epochs of one hour** over a horizon of **five years** (43,830 epochs).

Suggested implementation: Python with `numpy` and `pandas`, results to Parquet, plots to `matplotlib`. Nothing here needs to be fast; clarity matters more than performance, and a full five-year run should complete in under a minute if cohorts are vectorised rather than simulated per-agent.

Every run must be reproducible from a seed and a parameter file. Parameters live in a single YAML or JSON file, never inline in code, because the entire point is sweeping them.

### Loop structure

```
for each epoch:
    1. Update population (growth curve, churn, cohort mix)
    2. Each cohort generates runs -> Contribution Score
    3. Apply staking multipliers
    4. Split emission across the three pools
    5. Distribute pool emission proportionally within each pool
    6. Accrue to claimable balances
    7. Process claims (vested unlocks + instant claims with burn)
    8. Process sinks (keys, entries, fusion, ranked rake, heraldry)
    9. Update staking positions (stake, unstake, cooldowns)
   10. Update price via the price model
   11. Record metrics
```

Steps 10 and 11 are where most of the value is. The price model is the weakest link and should be treated as a scenario input rather than an output — run each stress test across pessimistic, base, and optimistic price paths rather than pretending the model predicts price.

## Player cohorts

Simulating individual players is unnecessary. Five cohorts, each with a distribution rather than a point value, captures the behaviour that matters.

| Cohort | Share of DAU | Hours/day | Avg Tide | Ranked play | Stakes? | Claim behaviour |
| --- | --- | --- | --- | --- | --- | --- |
| **Tourist** | 40% | 0.3 | 1–2 | No | No | Instant, always |
| **Casual** | 35% | 1.0 | 2–4 | Occasional | Rarely | Instant, mostly |
| **Regular** | 18% | 2.5 | 4–6 | Yes | Sometimes | Mixed |
| **Grinder** | 6% | 8.0 | 6–9 | Yes | Usually | Vested |
| **Whale** | 1% | 3.0 | 6–8 | Yes | Heavily | Vested, restakes |

Add a sixth cohort, **Bot**, present only in the relevant stress test: 24 hours/day, Tide capped at `[botTideCap]`, no ranked, no stake, instant claim always.

Within each cohort, draw hours and Tide from a distribution (lognormal for hours, discrete for Tide) rather than using the mean, or the model will understate variance and make the Gini coefficient look better than reality.

Cohort shares should drift with game age. Tourists dominate early; the Grinder and Whale shares grow as the population matures. Model this as a simple linear drift over the first twelve months.

## Parameters

Everything below is an input to the sweep. Values are the proposals from [06](06-economy-and-tokenomics.md).

### Emission

| Parameter | Symbol | Proposed | Sweep range |
| --- | --- | --- | --- |
| Hard cap | `S_max` | 100,000,000 | 50M – 250M |
| Emission per epoch | `E` | 500 | 200 – 2,000 |
| Epoch length | — | 1 hour | 10 min – 24 h |
| Roll over unminted emission | — | false | fixed |

### Pool split

| Parameter | Proposed | Sweep range |
| --- | --- | --- |
| Ranked Bounty Waters | 40% | 25–55% |
| Deep Dungeon (Tide 6+) | 35% | 20–50% |
| Casual (Tide 1–5, co-op) | 25% | 15–40% |

Must sum to 1.0. The sweep should test whether the casual pool share is high enough to keep casual yield perceptible — see acceptance criteria.

### Contribution Score

| Parameter | Symbol | Proposed | Sweep range |
| --- | --- | --- | --- |
| Damage weight | `w_dmg` | 1.0 | 0.5–2.0 |
| Waves survived weight | `w_wave` | 8.0 | 4–20 |
| Boss/elite weight | `w_boss` | 25.0 | 10–60 |
| Objective seconds weight | `w_obj` | 0.5 | 0.1–2.0 |
| Placement weight | `w_plc` | 40.0 | 20–100 |
| Revive weight | `w_rev` | 15.0 | 0–40 |
| PvE depth exponent | `γ` | **1.6** | 1.0–2.4 |
| PvPvE bracket exponent | `γ_pvp` | 1.4 | 1.0–2.0 |
| Per-enemy damage cap | — | 1× enemy max HP | 1–3× |

`γ` is the single most consequential parameter in the model. It controls how much deep play is rewarded over shallow play, and therefore both the sybil argument and the casual-versus-grinder yield ratio. Sweep it densely.

### Staking

| Parameter | Proposed | Sweep range |
| --- | --- | --- |
| Multiplier form | `1 + min(1, sqrt(stake / stakeRef))` | also test linear and log |
| `stakeRef` | 5,000 `$PLUNDER` | 1,000 – 50,000 |
| Max multiplier | 2.0x | 1.5x – 3.0x |
| Play-gated | true | fixed — do not sweep |
| Unstake cooldown | 7 days | 0–30 days |

`Play-gated` is not a tuning parameter. Turning it off converts the token into a pure yield instrument and the model should not be used to argue for that.

### Sinks

| Sink | Proposed price | Participation assumption |
| --- | --- | --- |
| Voyage Key | 2 `$PLUNDER` | 1 per scoring run at Tide 6+ |
| Deep Tide entry (T8+) | 5 + 3 per tier above 8 | 20% of Grinder runs |
| Cosmetic fusion | 150 | 0.05 per active player per week |
| Shard crafting | 40 | 0.3 per active player per week |
| Ranked buy-in rake | 8% of pot | all ranked matches |
| Heraldry | 60 | 0.02 per active player per week |
| Extra reroll/banish | 3 | 0.4 per ranked match |
| Season pass | 400 | 8% of DAU per season |

**Sink prices must be modelled as dynamically repriced against realised emission**, not as fixed nominal values. Implement both and compare — the fixed-price variant is expected to decouple badly under the price-shock stress test, and demonstrating that is one of the more useful outputs of the whole exercise.

### Claim and vesting

| Parameter | Proposed | Sweep range |
| --- | --- | --- |
| Vesting period | 14 days linear | 0–90 days |
| Instant claim payout | 50% | 25–80% |
| Instant claim remainder | burned | fixed |
| Cliff | none | fixed — do not add |

### Population

| Parameter | Proposed | Notes |
| --- | --- | --- |
| Launch DAU | 5,000 | |
| Growth curve | logistic to 150,000 over 24 months | scenario input |
| Monthly churn | 12% of non-Grinder cohorts | |
| Sessions per active day | cohort-dependent | |

### Price model

Treated as a **scenario**, not an output. Three paths, each run against every stress test:

- **Pessimistic** — declines 60% over year one, flat thereafter
- **Base** — tracks the square root of DAU growth
- **Optimistic** — tracks DAU growth linearly

Do not build a reflexive price model where price feeds back into participation. It will produce confident-looking nonsense.

## Metrics to record

Per epoch, aggregated to daily and monthly:

**Supply**
- Circulating supply, claimable balance outstanding, cumulative burned
- Net supply change per day
- Burn as a percentage of emission (the headline number, target 100–140%)
- Projected years to `S_max`

**Distribution**
- Tokens earned per cohort, absolute and per hour played
- Ratio of Grinder yield per hour to Casual yield per hour
- Gini coefficient across earners
- Top 1% and top 10% share of epoch emission
- Fraction of participants earning a strictly positive reward — must be 1.0, always

**Behaviour and pressure**
- Staked supply as a percentage of circulating
- Daily unlocked tokens (proxy for sell pressure)
- Instant-claim uptake and the burn it generates
- Participation split across the three pools

**Per-pool**
- Emission, participant count, and mean yield within each pool

## Stress tests

Seven scenarios. Each runs against all three price paths.

### T1 — Viral spike

DAU multiplies 10x over 14 days, then decays 40% over the following 60.

*Watching for:* per-player yield collapse, whether the casual pool absorbs the influx, whether sinks scale with the population or lag, and whether the Gini coefficient spikes as new low-CS accounts flood in.

*Expected:* yield per player drops roughly tenfold at peak. This is correct behaviour, not a bug. The question is whether the *fiat* yield holds under the base price path.

### T2 — Collapse

DAU falls 80% over 30 days.

*Watching for:* burn dropping below emission and the token turning inflationary at exactly the worst moment; remaining players' yield spiking, which is the intended cushion; whether staked supply unwinds and floods the market.

*Expected:* net inflation returns as sinks shrink faster than emission. This is the model's most dangerous known weakness and the simulation must quantify it. Emission is time-based and therefore does not shrink with the playerbase.

### T3 — Bot farm

A Bot cohort accounting for 5%, 20%, and 50% of total Contribution Score, each run separately.

*Watching for:* the yield reduction inflicted on legitimate cohorts; whether bots can reach the Deep Dungeon pool given the Tide cap; the effect of raising the Voyage Key price as a countermeasure.

*Expected:* purely dilutive, never inflationary. Confirming this quantitatively is the point — it is the argument for spending proportionately on anti-bot work rather than treating it as existential.

### T4 — Whale stake concentration

Top 1% holds 40%, 60%, and 80% of all staked supply.

*Watching for:* whether the concave `sqrt` multiplier keeps the top 1% emission share within the acceptance threshold; comparison against linear and logarithmic multiplier forms.

*Expected:* the concave form should hold. If it does not, `stakeRef` needs raising or the cap needs lowering.

### T5 — Grinder yield floor

The named tension from [06](06-economy-and-tokenomics.md). Track a top-decile Grinder's yield per hour, in tokens and in fiat, across the full growth curve.

*Watching for:* the point at which token yield per hour falls below what that player earned at launch, and whether the base price path offsets it.

*Reporting:* this needs to go into a chart that goes in front of the community before the token launches. If the honest answer is "your token yield will fall as the game grows, and here is the curve", saying so early costs far less than being discovered later.

### T6 — Sink failure

Sink participation at 40% of the base assumption.

*Watching for:* how far the burn ratio falls below 100%, and how much sink prices would need to rise to compensate — and whether that price is one a player would actually pay.

*Expected:* the burn target is the least robust assumption in the whole design because it depends entirely on unvalidated behavioural guesses.

### T7 — Price shock

A 70% drawdown over 14 days.

*Watching for:* whether fixed-price sinks become trivially cheap and get drained, versus whether dynamically repriced sinks hold; whether staking unwinds; whether instant-claim uptake spikes and, ironically, increases the burn.

*Expected:* this is the test that should decide fixed versus dynamic sink pricing, and it should decide it decisively.

## Acceptance criteria

Go/no-go thresholds. Failing any of these means retuning, not shipping.

| # | Criterion | Threshold |
| --- | --- | --- |
| 1 | Every participant with `CS > 0` earns a positive reward | 100%, always, no exceptions |
| 2 | Casual yield per hour versus Grinder yield per hour | ≥ 1:8 |
| 3 | Top 1% share of epoch emission | ≤ 15% |
| 4 | Gini across earners | ≤ 0.65 |
| 5 | Burn as a share of emission at target DAU | ≥ 100% |
| 6 | Burn as a share of emission across T1 and T6 | ≥ 60% |
| 7 | Daily unlocked supply | ≤ 0.4% of circulating |
| 8 | Years to hard cap | 15–30 |
| 9 | Sybil advantage from splitting one account into ten | negative in every scenario |
| 10 | Grinder yield per hour at 10x DAU, base price path | ≥ 50% of launch fiat yield |

Criterion 2 encodes a judgement worth arguing about explicitly: a grinder playing eight hours should out-earn a casual playing one, but by less than the 8:1 their raw time would suggest, because the segmented pools protect the casual. If the model produces 1:30, the casual pool share is too small or `γ` is too high.

Criterion 9 is the formal check on the sybil argument in [06](06-economy-and-tokenomics.md#sybils-gain-nothing-and-lose-on-two-counts). It should be implemented as an explicit experiment — take one Grinder agent, split its hours and stake across ten accounts, and confirm total yield falls.

## Validating against reality

The simulation is worth little until it is checked against real behaviour. Per the sequencing recommendation in [06](06-economy-and-tokenomics.md#sequencing-ship-without-the-token), the game launches without a token but **runs the ledger anyway**.

From day one of soft launch:

- Compute and store Contribution Score for every run
- Run the full distribution against a shadow emission schedule
- Show players a "would have earned" figure in the results screen
- Record every event that a sink would have been priced against

That produces months of genuine behavioural data — real session lengths, real Tide distributions, real cohort mix, real ranked participation — to replace the guesses in the cohort table above. Re-run the whole suite against measured parameters before a single token exists.

This is the single highest-value thing on this list. Every other item is an argument; this one is evidence.

## Deliverables

1. `sim/model.py` — the epoch loop and cohort model
2. `sim/params/base.yaml` — the base parameter set, with every value above
3. `sim/params/stress/*.yaml` — one file per stress test
4. `sim/run.py` — CLI to execute a scenario or a sweep
5. `sim/report.py` — charts and an acceptance-criteria pass/fail table
6. `sim/README.md` — how to run it and how to read the output
7. A written findings document recommending final parameter values

## Open questions

1. **Is a one-hour epoch right?** Shorter epochs smooth yield and raise settlement cost; longer ones batch efficiently but make short sessions feel unrewarded.
2. **Should the price model be reflexive?** Currently no, deliberately. Revisit only if the non-reflexive results are ambiguous.
3. **Modelling cohort migration.** Players move between cohorts as they mature, and the current model only drifts aggregate shares. Migration probably matters for the yield-floor test.
4. **Secondary market.** Not modelled at all. Once cosmetics or shards become tradeable, the gold economy stops being isolated and this model becomes incomplete.
