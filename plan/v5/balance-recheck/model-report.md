# V5 balance re-check: simulator report on the current data

Config hash `84c5fd983298` · director Adaptive · 200 runs per personality · seed 1 · 64.3 s.
Targets missed: **2** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.1 / **7.8** / 14 | 100% | 0% | – | 15 / 26 | 55 | 261 | 26 | 27% (6.9) | ✓ median end minute 7.79 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.1 / **7.8** / 12 | 80% | 27% | 8.1 | 15 / 28 | 41 | 193 | 27 | 5% (7.7) | ✓ median end minute 7.85 (7–12); ✓ boss win rate 0.27 (0.1–0.6) |
| Casual Cruiser | 20% | 5.2 / **8.5** / 14.5 | 100% | 43% | 8.2 | 15 / 26 | 48 | 280 | 33 | 33% (6.9) | ✓ median end minute 8.5 (8–12); ✓ boss win rate 0.43 (0.3–0.7) |
| Steady Captain | 20% | 7.1 / **13.8** / 15.7 | 100% | 78% | 6.9 | 15 / 24 | 47 | 540 | 82 | 44% (7.2) | ✓ median end minute 13.75 (11–15); ✓ boss win rate 0.78 (0.6–0.85); ✓ level at 10:00 24 (21–35) |
| Glass Cannon | 7% | 4.8 / **13.2** / 15.9 | 100% | 81% | 5.6 | 15 / 26 | 54 | 552 | 68 | 38% (6.5) | ✗ median end minute 13.2 (8–13); ✓ boss win rate 0.81 (0.6–1) |
| Turtle | 6% | 3.5 / **8.8** / 14.9 | 100% | 41% | 8.4 | 13 / 24 | 38 | 205 | 30 | 29% (6.9) | ✓ median end minute 8.75 (8–13); ✓ boss win rate 0.41 (0.3–0.7) |
| Treasure Hound | 6% | 3.3 / **8.4** / 14 | 100% | 32% | 8.4 | 15 / 25 | 38 | 201 | 33 | 19% (6.5) | ✓ median end minute 8.44 (7.5–12); ✓ boss win rate 0.32 (0.2–0.6) |
| Build Planner | 5% | 6.7 / **14.5** / 16.8 | 100% | 83% | 6.3 | 14 / 23 | 41 | 517 | 74 | 52% (6.2) | ✓ median end minute 14.53 (13–19); ✓ boss win rate 0.83 (0.8–1) |
| Sea Legend | 4% | 15.9 / **18.4** / 21.9 | 100% | 99% | 5.9 | 13 / 23 | 36 | 564 | 83 | 64% (6.3) | ✓ median end minute 18.39 (17––); ✓ boss win rate 0.99 (0.95–1) |
| Speedrunner | 2% | 4.1 / **5.8** / 8.2 | 5% | 95% | 5.8 | 14 / – | 38 | 285 | 47 | 0% (–) | ✓ median end minute 5.84 (3–8); ✓ boss win rate 0.95 (0.5–1) |

Most players (weighted by share): median end minute ≈ **10.3**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 4 items, evolved 27% (first at 5.5–6.9 min, p25–median); Thumb Sailor 5 items, evolved 5% (first at 6.6–7.7 min, p25–median); Casual Cruiser 5 items, evolved 33% (first at 4.9–6.9 min, p25–median); Steady Captain 5 items, evolved 44% (first at 6.2–7.2 min, p25–median); Glass Cannon 6 items, evolved 38% (first at 5.3–6.5 min, p25–median); Turtle 4 items, evolved 29% (first at 5.2–6.9 min, p25–median); Treasure Hound 6 items, evolved 19% (first at 5.1–6.5 min, p25–median); Build Planner 5 items, evolved 52% (first at 4.8–6.2 min, p25–median); Sea Legend 5 items, evolved 64% (first at 4.9–6.3 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 14.6 | 392 | 91% |
| ember | 33 | 13.4 | 364 | 76% |
| flint | 33 | 8.5 | 450 | 64% |
| richie | 33 | 13.7 | 445 | 88% |
| rook | 34 | 14.2 | 393 | 79% |
| roxie | 33 | 13.1 | 458 | 67% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.10 | 25 | 4 | 0.00 | 13 | 40 | 5 / 0.62 s | 72 / 8.1 s | 1.04 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–9); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.21 | 55 | 7 | 0.00 | 28 | 59 | 6 / 0.45 s | 77 / 6.0 s | 1.07 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (6–13); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 200 | 0.24 | 43 | 10 | 0.23 | 25 | 72 | 6 / 0.41 s | 85 / 5.3 s | 1.13 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (9–17); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 199 | 0.16 | 39 | 12 | 0.12 | 50 | 95 | 7 / 0.36 s | 98 / 4.7 s | 1.06 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (12–20); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 199 | 0.53 | 66 | 15 | 0.16 | 37 | 117 | 9 / 0.34 s | 114 / 4.4 s | 1.02 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (14–23); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 195 | 0.67 | 71 | 17 | 0.29 | 39 | 144 | 10 / 0.32 s | 134 / 4.2 s | 1.01 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (16–26); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 187 | 0.68 | 26 | 18 | 0.41 | 17 | 159 | 12 / 0.35 s | 159 / 4.6 s | 1.01 | ✓ alive share (0.25–0.75); ✗ kills (30–140); ✓ level (15–26); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 152 | 0.83 | 76 | 21 | 0.37 | 85 | 251 | 14 / 0.26 s | 190 / 3.4 s | 1.05 | ✓ alive share (0.3–0.85); ✓ kills (50–160); ✓ level (17–29); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 146 | 0.60 | 66 | 23 | 0.28 | 64 | 296 | 17 / 0.27 s | 227 / 3.5 s | 1.08 | ✓ alive share (0.22–0.8); ✓ kills (45–160); ✓ level (18–31); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 145 | 0.80 | 75 | 24 | 0.37 | 39 | 341 | 21 / 0.28 s | 271 / 3.6 s | 1.04 | ✓ alive share (0.35–0.9); ✓ kills (55–180); ✓ level (20–33); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 133 | 0.76 | 59 | 26 | 0.42 | 31 | 380 | 25 / 0.30 s | 324 / 3.9 s | 1.07 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (21–35) |
| 11 | 123 | 0.49 | 51 | 27 | 0.20 | 24 | 439 | 29 / 0.30 s | 386 / 4.0 s | 1.04 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (22–37) |
| 12 | 117 | 0.70 | 43 | 28 | 0.31 | 22 | 458 | 35 / 0.35 s | 458 / 4.5 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (23–39) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **15** (target 10–20)
- ✓ seconds between level-ups over 0:00–10:00: **25** (target 20–30)
- ✓ miniboss time to kill (s): **23.75** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.83** (target ––1)
- ✓ scheduled elite time to kill (s): **5.5** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **54** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| swivel_guns | 96 | 14.3 |
| broadside_battery | 106 | 14.2 |
| deck_cannons | 92 | 13.9 |
| lightning_mast | 98 | 13.9 |
| mortar | 80 | 13.6 |
| long_nine | 63 | 13.6 |
| harpoon_gun | 74 | 13.5 |
| powder_kegs | 93 | 13.5 |
| flying_cutlasses | 72 | 13.5 |
| flamethrower | 93 | 13.4 |
