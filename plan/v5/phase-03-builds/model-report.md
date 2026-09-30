# V5 M3 build balance: simulator report (after the retune)

Config hash `88db06c71684` · director Adaptive · 200 runs per personality · seed 1 · 49.6 s.
Targets missed: **2** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.1 / **8.5** / 14 | 100% | 0% | – | 24 / 38 | 74 | 392 | 32 | 30% (6.8) | ✓ median end minute 8.47 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.3 / **8.2** / 12 | 75% | 31% | 8.1 | 23 / 40 | 61 | 291 | 30 | 6% (7.7) | ✓ median end minute 8.21 (7–12); ✓ boss win rate 0.31 (0.1–0.6) |
| Casual Cruiser | 20% | 6.9 / **8.5** / 13.9 | 100% | 45% | 8.2 | 22 / 37 | 63 | 379 | 34 | 36% (6.6) | ✓ median end minute 8.49 (8–12); ✓ boss win rate 0.45 (0.3–0.7) |
| Steady Captain | 20% | 7 / **12.8** / 15 | 100% | 82% | 6.9 | 23 / 34 | 57 | 617 | 80 | 42% (7.3) | ✓ median end minute 12.78 (11–15); ✓ boss win rate 0.82 (0.6–0.85); ✓ level at 10:00 34 (30–46) |
| Glass Cannon | 7% | 7.4 / **13.8** / 15.1 | 100% | 94% | 5.3 | 24 / 39 | 81 | 881 | 71 | 47% (6.5) | ✗ median end minute 13.76 (8–13); ✓ boss win rate 0.94 (0.6–1) |
| Turtle | 6% | 7.1 / **8.8** / 14.4 | 100% | 40% | 8.3 | 21 / 37 | 51 | 277 | 30 | 35% (5.7) | ✓ median end minute 8.78 (8–13); ✓ boss win rate 0.4 (0.3–0.7) |
| Treasure Hound | 6% | 4.9 / **8.6** / 13.8 | 100% | 34% | 8.3 | 22 / 39 | 49 | 276 | 34 | 25% (6.7) | ✓ median end minute 8.57 (7.5–12); ✓ boss win rate 0.34 (0.2–0.6) |
| Build Planner | 5% | 7.4 / **14.1** / 15.7 | 100% | 89% | 5.9 | 22 / 35 | 57 | 686 | 75 | 68% (5.7) | ✓ median end minute 14.09 (13–19); ✓ boss win rate 0.89 (0.8–1) |
| Sea Legend | 4% | 14.5 / **16.5** / 19.5 | 100% | 100% | 5.4 | 22 / 36 | 53 | 748 | 85 | 76% (5.5) | ✗ median end minute 16.45 (17––); ✓ boss win rate 1 (0.95–1) |
| Speedrunner | 2% | 3.8 / **5.5** / 7.9 | 5% | 96% | 5.4 | 23 / – | 64 | 365 | 49 | 0% (–) | ✓ median end minute 5.51 (3–8); ✓ boss win rate 0.96 (0.5–1) |

Most players (weighted by share): median end minute ≈ **10.2**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 5 items, evolved 30% (first at 4.9–6.8 min, p25–median); Thumb Sailor 6 items, evolved 6% (first at 7–7.7 min, p25–median); Casual Cruiser 5 items, evolved 36% (first at 5.4–6.6 min, p25–median); Steady Captain 5 items, evolved 42% (first at 5.9–7.3 min, p25–median); Glass Cannon 7 items, evolved 47% (first at 5.3–6.5 min, p25–median); Turtle 5 items, evolved 35% (first at 4.6–5.7 min, p25–median); Treasure Hound 6 items, evolved 25% (first at 5.2–6.7 min, p25–median); Build Planner 6 items, evolved 68% (first at 4–5.7 min, p25–median); Sea Legend 6 items, evolved 76% (first at 3.7–5.5 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 13.9 | 655 | 91% |
| ember | 33 | 12.8 | 672 | 82% |
| flint | 33 | 12.4 | 683 | 79% |
| richie | 33 | 13.2 | 978 | 85% |
| rook | 34 | 13.5 | 1181 | 82% |
| roxie | 33 | 10.5 | 734 | 73% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.09 | 39 | 7 | 0.00 | 21 | 56 | 6 / 0.45 s | 73 / 6.0 s | 1.13 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–9); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.19 | 80 | 13 | 0.00 | 45 | 100 | 6 / 0.28 s | 82 / 3.7 s | 1.16 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (6–13); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 199 | 0.16 | 58 | 16 | 0.14 | 37 | 130 | 7 / 0.26 s | 98 / 3.4 s | 1.27 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (9–17); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 197 | 0.12 | 65 | 19 | 0.00 | 63 | 174 | 9 / 0.24 s | 123 / 3.2 s | 1.17 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (12–20); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 197 | 0.46 | 84 | 23 | 0.11 | 53 | 227 | 12 / 0.24 s | 156 / 3.1 s | 1.22 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (14–23); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 195 | 0.61 | 81 | 25 | 0.31 | 50 | 293 | 15 / 0.24 s | 200 / 3.1 s | 1.24 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (16–26); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 180 | 0.59 | 34 | 27 | 0.30 | 170 | 340 | 20 / 0.26 s | 257 / 3.4 s | 1.13 | ✓ alive share (0.25–0.75); ✓ kills (30–140); ✓ level (18–29); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 153 | 0.75 | 78 | 31 | 0.27 | 94 | 467 | 25 / 0.24 s | 328 / 3.2 s | 1.27 | ✓ alive share (0.3–0.85); ✓ kills (50–160); ✓ level (21–32); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 147 | 0.62 | 64 | 33 | 0.27 | 67 | 580 | 32 / 0.25 s | 417 / 3.3 s | 1.31 | ✓ alive share (0.22–0.8); ✓ kills (45–160); ✓ level (23–35); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 143 | 0.82 | 70 | 34 | 0.48 | 41 | 621 | 40 / 0.29 s | 527 / 3.9 s | 1.23 | ✓ alive share (0.35–0.9); ✓ kills (55–180); ✓ level (25–38); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 133 | 0.80 | 51 | 36 | 0.48 | 27 | 682 | 50 / 0.34 s | 661 / 4.4 s | 1.25 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (28–42) |
| 11 | 117 | 0.68 | 50 | 37 | 0.33 | 24 | 827 | 63 / 0.34 s | 823 / 4.5 s | 1.32 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (30–45) |
| 12 | 95 | 0.67 | 42 | 40 | 0.39 | 21 | 981 | 77 / 0.36 s | 1017 / 4.7 s | 1.34 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (32–48) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **8.57** (target 4–12)
- ✓ seconds between level-ups over 0:00–10:00: **17.65** (target 13–20)
- ✓ miniboss time to kill (s): **17.75** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.11** (target ––1)
- ✓ scheduled elite time to kill (s): **5.5** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **50.13** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| deck_cannons | 100 | 13.3 |
| harpoon_gun | 79 | 13.1 |
| broadside_battery | 103 | 13.1 |
| swivel_guns | 100 | 13.1 |
| lightning_mast | 87 | 13 |
| flamethrower | 96 | 12.6 |
| mortar | 70 | 12.5 |
| long_nine | 89 | 12.5 |
| powder_kegs | 102 | 12.4 |
| flying_cutlasses | 76 | 12 |

## Weapon pairs (the first two weapons, baseline personality, 6 runs each)

| Pair | Median end minute | Boss won |
|---|---|---|
| deck_cannons + broadside_battery | 15.1 | 83% |
| flamethrower + mortar | 14.6 | 100% |
| broadside_battery + harpoon_gun | 14.5 | 100% |
| deck_cannons + powder_kegs | 14.4 | 83% |
| powder_kegs + mortar | 14.3 | 100% |
| lightning_mast + long_nine | 14.3 | 100% |
| flying_cutlasses + swivel_guns | 14.2 | 67% |
| powder_kegs + flying_cutlasses | 14.1 | 100% |
| powder_kegs + harpoon_gun | 14.1 | 83% |
| broadside_battery + long_nine | 14 | 100% |
| flying_cutlasses + long_nine | 14 | 100% |
| flying_cutlasses + mortar | 13.9 | 100% |
| powder_kegs + swivel_guns | 13.9 | 100% |
| swivel_guns + harpoon_gun | 13.9 | 100% |
| broadside_battery + mortar | 13.8 | 83% |
| lightning_mast + swivel_guns | 13.8 | 100% |
| lightning_mast + harpoon_gun | 13.6 | 100% |
| broadside_battery + powder_kegs | 13.6 | 100% |
| broadside_battery + swivel_guns | 13.5 | 100% |
| swivel_guns + long_nine | 13.5 | 83% |
| lightning_mast + mortar | 13.5 | 83% |
| powder_kegs + lightning_mast | 13.4 | 100% |
| deck_cannons + lightning_mast | 13.4 | 100% |
| deck_cannons + mortar | 13.3 | 100% |
| deck_cannons + harpoon_gun | 13.3 | 83% |
| broadside_battery + flying_cutlasses | 13.3 | 100% |
| flamethrower + long_nine | 13.2 | 67% |
| lightning_mast + flying_cutlasses | 13.2 | 83% |
| deck_cannons + flamethrower | 13.2 | 83% |
| flamethrower + swivel_guns | 13.1 | 100% |
| swivel_guns + mortar | 13 | 100% |
| flying_cutlasses + harpoon_gun | 13 | 100% |
| broadside_battery + lightning_mast | 12.8 | 67% |
| deck_cannons + flying_cutlasses | 12.5 | 100% |
| lightning_mast + flamethrower | 12.4 | 83% |
| long_nine + harpoon_gun | 12.3 | 83% |
| long_nine + mortar | 11.8 | 83% |
| deck_cannons + swivel_guns | 11.8 | 83% |
| deck_cannons + long_nine | 11.6 | 83% |
| broadside_battery + flamethrower | 11.3 | 83% |
| harpoon_gun + mortar | 10.7 | 50% |
| powder_kegs + long_nine | 9.7 | 83% |
| powder_kegs + flamethrower | 9.6 | 50% |
| flamethrower + harpoon_gun | 9.1 | 50% |
| flying_cutlasses + flamethrower | 7.8 | 83% |
