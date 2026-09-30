# V5 balance re-check: simulator report on the demo baseline

Config hash `5783d80dcfdc` · director Adaptive · 200 runs per personality · seed 1 · 39.2 s.
Targets missed: **24** of 70.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.1 / **7.4** / 9.4 | 100% | 0% | – | 14 / 24 | 50 | 220 | 25 | 6% (6.7) | ✓ median end minute 7.42 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.1 / **6.4** / 8.7 | 97% | 9% | 8.3 | 14 / 26 | 37 | 139 | 22 | 1% (7.1) | ✗ median end minute 6.36 (7–12); ✗ boss win rate 0.09 (0.1–0.6) |
| Casual Cruiser | 20% | 3.4 / **8** / 9.2 | 100% | 12% | 8.5 | 14 / 24 | 40 | 194 | 30 | 3% (6.6) | ✓ median end minute 8.04 (8–12); ✗ boss win rate 0.12 (0.3–0.7) |
| Steady Captain | 20% | 6.4 / **7.9** / 13.5 | 100% | 49% | 7.2 | 14 / 22 | 38 | 303 | 34 | 6% (8.1) | ✗ median end minute 7.89 (11–15); ✗ boss win rate 0.49 (0.6–0.85); ✗ level at 10:00 22 (30–46) |
| Glass Cannon | 7% | 5 / **9.5** / 13.8 | 100% | 67% | 5.6 | 14 / 24 | 44 | 397 | 58 | 3% (10.2) | ✓ median end minute 9.52 (8–13); ✓ boss win rate 0.67 (0.6–1) |
| Turtle | 6% | 3.5 / **8.1** / 9.6 | 100% | 11% | 8.7 | 12 / 22 | 32 | 137 | 24 | 6% (6.5) | ✓ median end minute 8.07 (8–13); ✗ boss win rate 0.11 (0.3–0.7) |
| Treasure Hound | 6% | 3.3 / **8.1** / 9 | 100% | 8% | 8.7 | 13 / 25 | 34 | 155 | 29 | 1% (7.8) | ✓ median end minute 8.12 (7.5–12); ✗ boss win rate 0.08 (0.2–0.6) |
| Build Planner | 5% | 5.5 / **8.8** / 14.8 | 100% | 54% | 6.5 | 13 / 22 | 32 | 299 | 51 | 20% (6.9) | ✗ median end minute 8.81 (13–19); ✗ boss win rate 0.54 (0.8–1) |
| Sea Legend | 4% | 12.5 / **16** / 19.4 | 100% | 93% | 6.3 | 13 / 20 | 26 | 409 | 79 | 30% (7.5) | ✗ median end minute 16.03 (17––); ✗ boss win rate 0.93 (0.95–1) |
| Speedrunner | 2% | 4.2 / **6** / 8.3 | 21% | 80% | 5.7 | 13 / – | 34 | 263 | 45 | 0% (–) | ✓ median end minute 6.05 (3–8); ✓ boss win rate 0.8 (0.5–1) |

Most players (weighted by share): median end minute ≈ **8.1**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 4 items, evolved 6% (first at 5.5–6.7 min, p25–median); Thumb Sailor 5 items, evolved 1% (first at 6.7–7.1 min, p25–median); Casual Cruiser 4 items, evolved 3% (first at 5.8–6.6 min, p25–median); Steady Captain 4 items, evolved 6% (first at 7.2–8.1 min, p25–median); Glass Cannon 6 items, evolved 3% (first at 8.8–10.2 min, p25–median); Turtle 4 items, evolved 6% (first at 5.5–6.5 min, p25–median); Treasure Hound 5 items, evolved 1% (first at 6.8–7.8 min, p25–median); Build Planner 5 items, evolved 20% (first at 5.9–6.9 min, p25–median); Sea Legend 4 items, evolved 30% (first at 6–7.5 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 8.1 | 322 | 53% |
| ember | 33 | 8.2 | 404 | 61% |
| flint | 33 | 8 | 320 | 39% |
| richie | 33 | 9.7 | 465 | 64% |
| rook | 34 | 7.6 | 536 | 38% |
| roxie | 33 | 7.5 | 665 | 39% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.10 | 24 | 4 | 0.00 | 12 | 40 | 6 / 0.64 s | 73 / 8.3 s | 1.00 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–9); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.19 | 54 | 7 | 0.00 | 27 | 58 | 6 / 0.49 s | 82 / 6.4 s | 1.00 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (6–13); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 200 | 0.20 | 42 | 9 | 0.20 | 22 | 72 | 7 / 0.47 s | 98 / 6.2 s | 1.00 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (9–17); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 192 | 0.15 | 36 | 12 | 0.10 | 46 | 92 | 9 / 0.46 s | 123 / 6.1 s | 1.00 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (12–20); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 191 | 0.50 | 56 | 14 | 0.16 | 29 | 114 | 12 / 0.47 s | 156 / 6.2 s | 1.00 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (14–23); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 185 | 0.68 | 58 | 16 | 0.36 | 30 | 143 | 15 / 0.49 s | 200 / 6.4 s | 1.00 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (16–26); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 158 | 0.71 | 21 | 17 | 0.45 | 12 | 166 | 20 / 0.53 s | 257 / 7.0 s | 1.00 | ✓ alive share (0.25–0.75); ✗ kills (30–140); ✗ level (18–29); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 92 | 0.86 | 50 | 20 | 0.39 | 166 | 254 | 25 / 0.45 s | 328 / 5.9 s | 1.00 | ✗ alive share (0.3–0.85); ✗ kills (50–160); ✗ level (21–32); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 83 | 0.68 | 44 | 21 | 0.37 | 52 | 292 | 32 / 0.49 s | 417 / 6.5 s | 1.01 | ✓ alive share (0.22–0.8); ✗ kills (45–160); ✗ level (23–35); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 76 | 0.85 | 54 | 22 | 0.49 | 29 | 317 | 40 / 0.58 s | 527 / 7.6 s | 1.00 | ✓ alive share (0.35–0.9); ✗ kills (55–180); ✗ level (25–38); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 62 | 0.89 | 35 | 24 | 0.66 | 19 | 366 | 50 / 0.62 s | 661 / 8.2 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–200); ✗ level (28–42) |
| 11 | 47 | 0.89 | 29 | 24 | 0.59 | 16 | 395 | 63 / 0.72 s | 823 / 9.5 s | 1.00 | – |
| 12 | 26 | 0.90 | 29 | 26 | 0.52 | 12 | 479 | 77 / 0.74 s | 1017 / 9.7 s | 1.00 | – |

## Pacing (baseline)

- ✗ seconds between level-ups in minute 1: **15** (target 4–12)
- ✗ seconds between level-ups over 0:00–10:00: **27.27** (target 13–20)
- ✓ miniboss time to kill (s): **23.75** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.79** (target ––1)
- ✓ scheduled elite time to kill (s): **6.5** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **72.5** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| mortar | 78 | 8 |
| broadside_battery | 95 | 8 |
| flamethrower | 90 | 8 |
| flying_cutlasses | 68 | 7.9 |
| swivel_guns | 104 | 7.9 |
| long_nine | 64 | 7.9 |
| powder_kegs | 90 | 7.9 |
| harpoon_gun | 71 | 7.9 |
| deck_cannons | 86 | 7.9 |
| lightning_mast | 100 | 7.7 |
