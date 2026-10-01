# V5 M3d: simulator report with count-based pressure

Config hash `03fc5a227d66` · director Adaptive · 200 runs per personality · seed 1 · 64.4 s.
Targets missed: **2** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.1 / **9.5** / 13.2 | 100% | 0% | – | 10 / 16 | 56 | 295 | 34 | 32% (6.8) | ✗ median end minute 9.45 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.1 / **7.3** / 12 | 77% | 36% | 8.3 | 9 / 17 | 43 | 147 | 22 | 13% (7.3) | ✓ median end minute 7.29 (7–12); ✓ boss win rate 0.36 (0.1–0.6) |
| Casual Cruiser | 20% | 3.5 / **10.7** / 14.9 | 100% | 64% | 8.5 | 9 / 16 | 47 | 431 | 63 | 51% (6.4) | ✓ median end minute 10.66 (8–12); ✓ boss win rate 0.64 (0.3–0.7) |
| Steady Captain | 20% | 7.8 / **13.4** / 16.5 | 100% | 84% | 7.1 | 9 / 15 | 44 | 499 | 80 | 56% (6.6) | ✓ median end minute 13.44 (11–15); ✓ boss win rate 0.84 (0.6–0.85); ✓ level at 10:00 15 (10–16) |
| Glass Cannon | 7% | 4 / **12.8** / 16 | 100% | 79% | 6 | 9 / 15 | 49 | 522 | 67 | 50% (5.9) | ✓ median end minute 12.76 (8–13); ✓ boss win rate 0.79 (0.6–1) |
| Turtle | 6% | 3.6 / **9.4** / 14.9 | 100% | 50% | 8.6 | 8 / 15 | 38 | 282 | 41 | 40% (6.7) | ✓ median end minute 9.41 (8–13); ✓ boss win rate 0.5 (0.3–0.7) |
| Treasure Hound | 6% | 3.2 / **8.9** / 15 | 100% | 45% | 8.5 | 9 / 16 | 39 | 231 | 34 | 29% (6.3) | ✓ median end minute 8.88 (7.5–12); ✓ boss win rate 0.45 (0.2–0.6) |
| Build Planner | 5% | 7.4 / **14.2** / 18.3 | 100% | 82% | 6.7 | 9 / 14 | 36 | 457 | 71 | 66% (4.8) | ✓ median end minute 14.24 (13–19); ✓ boss win rate 0.82 (0.8–1) |
| Sea Legend | 4% | 15.7 / **20.4** / 25.8 | 97% | 99% | 6.4 | 8 / 14 | 30 | 515 | 82 | 78% (6.2) | ✓ median end minute 20.41 (17––); ✓ boss win rate 0.99 (0.95–1) |
| Speedrunner | 2% | 4.9 / **6.5** / 8.6 | 10% | 90% | 6.5 | 8 / – | 35 | 286 | 47 | 0% (–) | ✓ median end minute 6.49 (3–8); ✓ boss win rate 0.9 (0.5–1) |

Most players (weighted by share): median end minute ≈ **11**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 4 items, evolved 32% (first at 5.6–6.8 min, p25–median); Thumb Sailor 5 items, evolved 13% (first at 5.5–7.3 min, p25–median); Casual Cruiser 4 items, evolved 51% (first at 4.9–6.4 min, p25–median); Steady Captain 5 items, evolved 56% (first at 4.8–6.6 min, p25–median); Glass Cannon 6 items, evolved 50% (first at 3.8–5.9 min, p25–median); Turtle 4 items, evolved 40% (first at 4.5–6.7 min, p25–median); Treasure Hound 5 items, evolved 29% (first at 3.8–6.3 min, p25–median); Build Planner 5 items, evolved 66% (first at 3.6–4.8 min, p25–median); Sea Legend 5 items, evolved 78% (first at 3.4–6.2 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 13.9 | 132 | 82% |
| ember | 33 | 12.8 | 184 | 85% |
| flint | 33 | 13.3 | 123 | 85% |
| richie | 33 | 13.5 | 141 | 82% |
| rook | 34 | 13.8 | 191 | 85% |
| roxie | 33 | 13.8 | 151 | 85% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.20 | 37 | 4 | 0.05 | 18 | 43 | 5 / 0.56 s | 64 / 6.7 s | 1.08 | ✓ alive share (0.12–0.3); ✓ kills (8–45); ✓ level (3–5); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.26 | 52 | 6 | 0.07 | 29 | 54 | 5 / 0.45 s | 64 / 5.4 s | 1.03 | ✓ alive share (0.17–0.45); ✓ kills (18–90); ✓ level (4–6); ✓ damage taken (share of 100 hull) (0.05–0.45) |
| 2 | 200 | 0.36 | 44 | 7 | 0.27 | 24 | 60 | 5 / 0.41 s | 64 / 4.9 s | 1.01 | ✓ alive share (0.25–0.55); ✓ kills (25–90); ✓ level (5–7); ✓ damage taken (share of 100 hull) (0.05–0.6) |
| 3 | 193 | 0.31 | 40 | 8 | 0.22 | 49 | 70 | 5 / 0.34 s | 64 / 4.1 s | 1.00 | ✓ alive share (0.28–0.55); ✓ kills (25–100); ✓ level (6–8); ✓ damage taken (share of 100 hull) (0.05–0.6) |
| 4 | 193 | 0.48 | 67 | 9 | 0.14 | 35 | 89 | 5 / 0.27 s | 64 / 3.3 s | 1.00 | ✓ alive share (0.24–0.65); ✓ kills (35–120); ✓ level (7–10); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 193 | 0.41 | 63 | 11 | 0.14 | 34 | 110 | 5 / 0.22 s | 64 / 2.7 s | 1.00 | ✓ alive share (0.31–0.7); ✓ kills (35–130); ✓ level (7–11); ✓ damage taken (share of 100 hull) (––0.6) |
| 6 | 193 | 0.44 | 20 | 11 | 0.18 | 11 | 113 | 5 / 0.22 s | 64 / 2.6 s | 1.00 | ✓ alive share (0.35–0.75); ✗ kills (30–140); ✓ level (8–12); ✓ damage taken (share of 100 hull) (––0.6) |
| 7 | 174 | 0.76 | 57 | 13 | 0.24 | 154 | 147 | 5 / 0.16 s | 64 / 2.0 s | 1.00 | ✓ alive share (0.37–0.85); ✓ kills (50–160); ✓ level (9–13); ✓ damage taken (share of 100 hull) (––0.6) |
| 8 | 164 | 0.47 | 52 | 14 | 0.14 | 55 | 180 | 5 / 0.13 s | 64 / 1.6 s | 1.00 | ✓ alive share (0.47–0.8); ✓ kills (45–160); ✓ level (10–14); ✓ damage taken (share of 100 hull) (––0.6) |
| 9 | 163 | 0.75 | 77 | 15 | 0.23 | 40 | 196 | 5 / 0.12 s | 64 / 1.5 s | 1.00 | ✓ alive share (0.5–0.9); ✓ kills (55–180); ✓ level (10–16); ✓ damage taken (share of 100 hull) (––0.6) |
| 10 | 156 | 0.83 | 45 | 15 | 0.33 | 23 | 212 | 5 / 0.11 s | 64 / 1.4 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (11–17) |
| 11 | 137 | 0.86 | 35 | 16 | 0.30 | 17 | 254 | 5 / 0.10 s | 64 / 1.1 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (12–18) |
| 12 | 112 | 0.89 | 34 | 17 | 0.29 | 17 | 279 | 5 / 0.09 s | 64 / 1.0 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (13–19) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **15** (target 12–18)
- ✓ seconds between level-ups over 0:00–10:00: **40** (target 37–55)
- ✓ miniboss time to kill (s): **24.38** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.55** (target ––1)
- ✓ scheduled elite time to kill (s): **7.69** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **68.38** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| broadside_battery | 104 | 13.7 |
| mortar | 47 | 13.7 |
| harpoon_gun | 84 | 13.6 |
| lightning_mast | 92 | 13.6 |
| flamethrower | 81 | 13.5 |
| flying_cutlasses | 67 | 13.5 |
| deck_cannons | 106 | 13.4 |
| powder_kegs | 89 | 13.2 |
| swivel_guns | 101 | 13 |
| long_nine | 69 | 12.7 |

## Barnacle King fight at an early and a late summon (plan review P4)

Steady Captain, 30 unsinkable runs per summon time (seeds 700-729, the Captains in turn); target 45-90 s.

- summon at 4.5 min: beaten in 30 of 30, fight median 119 s (p10 63, p90 162)
- summon at 8.5 min: beaten in 30 of 30, fight median 64 s (p10 37, p90 99)
