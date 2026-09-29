# Player personalities for the balance simulator (defined 30 Sep 2026)

The owner asked for a variety of player personalities to be defined now, for the new Monte Carlo balance simulator
(written from scratch on V4 in M2; no V3 code). Each personality is a set of behaviour parameters the simulator uses to
play a run many times with different random draws. The same parameters become the AI captain profile in M16
(`AiCaptainProfile` uses these fields plus raid-only ones).

## Parameters

| # | Parameter | Type | Meaning |
|---|---|---|---|
| 1 | Dodge skill | 0–1 | Share of **avoidable damage** the player avoids. Avoidable = anything with a telegraph or a travel time: enemy shots, charges, ground hazards (Kraken traps, mines, nets, bombardment), boss attacks. Contact damage is parameter 2; storm and edge-wall damage count as avoidable only when the player chooses to stay out |
| 2 | Contact avoidance | 0–1 | Share of melee contact hits (rammers, sharks, boarding barges) avoided |
| 3 | Engage distance | metres | Preferred distance to the nearest enemy while fighting (short-range weapons pull it in by up to 40%) |
| 4 | Movement style | enum | `Kite` (circles around the crowd), `Loose` (wide, drifting circles), `Stop-go` (short moves, frequent stops), `Charge` (sails into the crowd), `Beeline` (straight lines to objectives) |
| 5 | Pickup greed | 0–1 | How far it detours for XP, gold and barrels: 0 = only what's on its path, 1 = everything within 30 m even through enemies |
| 6 | Chest policy | enum | `All` (buys every chest it can afford), `Some` (buys when gold ≥ 2× price), `Evolution` (buys to find its evolution item), `None` |
| 7 | Landmark use | set | Which landmarks it visits: `Wreck` (Repair Wreck), `Cache` (Powder Cache), `Buoy` (Signal Buoy), `Chart` (Treasure Chart) |
| 8 | Boss timing | trigger | When it summons the Barnacle King: `Minute(n)` (at n:00 if the Beacon is found), `OnHint` (at the 5:00 hint), `OnMarker` (at the 7:00 marker), `Never` |
| 9 | Card policy | enum | `Random`, `Shiny` (new weapons and highest rarity), `Balanced` (spreads levels, takes hull when below 50%), `Damage`, `Defense` (hull, armour, repair), `Focused` (maxes one weapon and hunts its evolution item), `Economy` (gold, XP and pickup-radius logbooks first) |
| 10 | Reroll use | 0–1 | Chance it spends a refresh when no card fits its policy |
| 11 | Active use | enum | `OnCooldown`, `Danger` (saved for hull below 40% or a boss attack), `Sometimes` (50% of the time it is ready), `Forgets` (10%) |
| 12 | Stop rule | enum | `Death` (plays until the ship sinks), `AfterBoss(n)` (quits n minutes after beating the boss), `Time(n)` (quits at n:00) |

## The personalities

Share = the assumed mix of the player base, used when the simulator reports "most players" (sums to 100%). All values
are config (`PlayerPersonalityConfig` in M2) and tunable without code.

| Personality | Share | Dodge | Contact | Engage (m) | Movement | Greed | Chests | Landmarks | Boss | Cards | Reroll | Active | Stop |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **First-Timer** | 15% | 0.35 | 0.30 | 6 | Stop-go | 0.2 | Some | Wreck | Never | Shiny | 0.0 | Forgets | Death |
| **Thumb Sailor** (mobile casual) | 15% | 0.45 | 0.40 | 8 | Stop-go | 0.3 | Some | Wreck, Chart | OnMarker | Random | 0.1 | Sometimes | Time(12) |
| **Casual Cruiser** | 20% | 0.55 | 0.50 | 9 | Loose | 0.4 | Some | Wreck, Buoy, Chart | OnMarker | Balanced | 0.2 | Sometimes | Death |
| **Steady Captain** (baseline) | 20% | 0.70 | 0.65 | 10 | Kite | 0.5 | Some | Wreck, Cache, Buoy, Chart | Minute(6) | Balanced | 0.4 | OnCooldown | Death |
| **Glass Cannon** | 7% | 0.65 | 0.45 | 5 | Charge | 0.4 | All | Cache | Minute(4) | Damage | 0.5 | OnCooldown | Death |
| **Turtle** | 6% | 0.60 | 0.70 | 14 | Loose | 0.3 | Some | Wreck, Buoy | OnMarker | Defense | 0.3 | Danger | Death |
| **Treasure Hound** | 6% | 0.55 | 0.50 | 9 | Loose | 0.9 | All | Wreck, Cache, Buoy, Chart | OnMarker | Economy | 0.3 | OnCooldown | Death |
| **Build Planner** | 5% | 0.75 | 0.70 | 10 | Kite | 0.5 | Evolution | Cache, Chart | Minute(5) | Focused | 0.9 | OnCooldown | Death |
| **Sea Legend** (expert) | 4% | 0.90 | 0.90 | 11 | Kite | 0.6 | Evolution | Wreck, Cache, Buoy, Chart | Minute(4) | Focused | 1.0 | Danger | Death |
| **Speedrunner** | 2% | 0.80 | 0.75 | 12 | Beeline | 0.1 | None | Chart | Minute(3) | Damage | 0.6 | OnCooldown | AfterBoss(0) |

Who they represent: brand-new players (First-Timer); phone players in short sessions (Thumb Sailor); relaxed regulars
(Casual Cruiser); the "average player" every M2 target is set against (Steady Captain); aggressive players racing for
power (Glass Cannon); careful survivors (Turtle); greedy looters who test gold and Rum farming (Treasure Hound); players
who plan evolutions (Build Planner); top players who test the late game and the leaderboard (Sea Legend); Island Race
players (Speedrunner).

## What the simulator reports per personality

- Time of death (median and spread), level at 5:00 and 10:00, kills per minute, gold and Rum per run.
- How often the boss is beaten, and when.
- Evolution rate and the minute it happens.
- Which weapons, logbooks and items appear in the best and worst runs (outliers flagged for M3).
- "Most players" views weighted by the shares above, and the baseline (Steady Captain) against the M2 targets.
- The strongest personality's 99th-percentile run, which sets the upper plausibility bounds for M9.

Calibration: the Steady Captain's simulated death time and level curve must match a scripted average run in the real
game within a tolerance set in M2; the owner's playtests are the final check.
