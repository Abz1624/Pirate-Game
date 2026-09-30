# V5 M2 balance model and simulator report (after the retune)

Config hash `6206228f14e4` · director Adaptive · 200 runs per personality · seed 1 · 45.5 s.
Targets missed: **4** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 2.9 / **7.9** / 13.1 | 100% | 0% | – | 23 / 38 | 75 | 376 | 26 | 0% (–) | ✓ median end minute 7.94 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.3 / **8.1** / 12 | 80% | 27% | 8 | 22 / 40 | 59 | 266 | 28 | 0% (–) | ✓ median end minute 8.1 (7–12); ✓ boss win rate 0.27 (0.1–0.6) |
| Casual Cruiser | 20% | 6.6 / **8.6** / 13.8 | 100% | 43% | 8.2 | 22 / 38 | 63 | 367 | 32 | 0% (–) | ✓ median end minute 8.56 (8–12); ✓ boss win rate 0.43 (0.3–0.7) |
| Steady Captain | 20% | 7.3 / **12.2** / 14.8 | 100% | 80% | 6.9 | 22 / 34 | 57 | 588 | 76 | 0% (–) | ✓ median end minute 12.21 (11–15); ✓ boss win rate 0.8 (0.6–0.85); ✓ level at 10:00 34 (30–46) |
| Glass Cannon | 7% | 6 / **13.7** / 14.8 | 100% | 88% | 5.2 | 24 / 39 | 82 | 881 | 78 | 0% (–) | ✗ median end minute 13.66 (8–13); ✓ boss win rate 0.88 (0.6–1) |
| Turtle | 6% | 6.6 / **9.1** / 14.1 | 100% | 45% | 8.4 | 21 / 36 | 52 | 317 | 29 | 2% (8.6) | ✓ median end minute 9.07 (8–13); ✓ boss win rate 0.45 (0.3–0.7) |
| Treasure Hound | 6% | 4.7 / **8.4** / 12.9 | 100% | 30% | 8.4 | 22 / 39 | 49 | 268 | 33 | 1% (9.6) | ✓ median end minute 8.39 (7.5–12); ✓ boss win rate 0.3 (0.2–0.6) |
| Build Planner | 5% | 6.5 / **13.1** / 16 | 100% | 79% | 6 | 21 / 34 | 50 | 557 | 73 | 13% (8.6) | ✓ median end minute 13.1 (13–19); ✗ boss win rate 0.79 (0.8–1) |
| Sea Legend | 4% | 14.2 / **16.6** / 20.4 | 100% | 99% | 5.6 | 22 / 33 | 47 | 659 | 85 | 19% (9.3) | ✗ median end minute 16.55 (17––); ✓ boss win rate 0.99 (0.95–1) |
| Speedrunner | 2% | 3.7 / **5.6** / 7.9 | 6% | 94% | 5.6 | 22 / – | 61 | 361 | 50 | 0% (–) | ✓ median end minute 5.65 (3–8); ✓ boss win rate 0.94 (0.5–1) |

Most players (weighted by share): median end minute ≈ **10**.

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.09 | 40 | 7 | 0.00 | 21 | 57 | 6 / 0.45 s | 73 / 5.9 s | 1.14 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–9); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.19 | 80 | 13 | 0.00 | 45 | 93 | 6 / 0.31 s | 82 / 4.0 s | 1.10 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (6–13); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 200 | 0.16 | 56 | 16 | 0.14 | 34 | 127 | 7 / 0.27 s | 98 / 3.5 s | 1.18 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (9–17); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 200 | 0.12 | 60 | 19 | 0.00 | 62 | 162 | 9 / 0.26 s | 123 / 3.4 s | 1.07 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (12–20); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 200 | 0.46 | 79 | 22 | 0.13 | 46 | 210 | 12 / 0.26 s | 156 / 3.4 s | 1.13 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (14–23); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 197 | 0.61 | 80 | 25 | 0.25 | 48 | 254 | 15 / 0.27 s | 200 / 3.6 s | 1.12 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (16–26); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 186 | 0.61 | 29 | 26 | 0.37 | 23 | 276 | 20 / 0.32 s | 257 / 4.2 s | 1.03 | ✓ alive share (0.25–0.75); ✗ kills (30–140); ✓ level (18–29); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 153 | 0.83 | 74 | 30 | 0.38 | 95 | 415 | 25 / 0.27 s | 328 / 3.6 s | 1.12 | ✓ alive share (0.3–0.85); ✓ kills (50–160); ✓ level (21–32); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 143 | 0.63 | 60 | 32 | 0.27 | 65 | 483 | 32 / 0.30 s | 417 / 3.9 s | 1.19 | ✓ alive share (0.22–0.8); ✓ kills (45–160); ✓ level (23–35); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 134 | 0.83 | 67 | 34 | 0.43 | 38 | 521 | 40 / 0.35 s | 527 / 4.6 s | 1.07 | ✓ alive share (0.35–0.9); ✓ kills (55–180); ✓ level (25–38); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 123 | 0.82 | 50 | 36 | 0.48 | 27 | 618 | 50 / 0.37 s | 661 / 4.9 s | 1.17 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (28–42) |
| 11 | 105 | 0.72 | 47 | 37 | 0.40 | 22 | 721 | 63 / 0.40 s | 823 / 5.2 s | 1.15 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (30–45) |
| 12 | 83 | 0.76 | 39 | 39 | 0.38 | 19 | 791 | 77 / 0.45 s | 1017 / 5.8 s | 1.09 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (32–48) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **8.57** (target 4–12)
- ✓ seconds between level-ups over 0:00–10:00: **17.65** (target 13–20)
- ✓ miniboss time to kill (s): **18** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.08** (target ––1)
- ✓ scheduled elite time to kill (s): **5.63** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **55.5** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| lightning_mast | 89 | 13.2 |
| swivel_guns | 95 | 12.9 |
| flamethrower | 97 | 12.5 |
| deck_cannons | 93 | 12.4 |
| long_nine | 91 | 12.4 |
| broadside_battery | 106 | 12.4 |
| mortar | 72 | 12.3 |
| harpoon_gun | 76 | 12.2 |
| flying_cutlasses | 82 | 11.8 |
| powder_kegs | 97 | 11.1 |

## Weapon pairs (the first two weapons, baseline personality, 6 runs each)

| Pair | Median end minute | Boss won |
|---|---|---|
| flamethrower + harpoon_gun | 15 | 100% |
| lightning_mast + swivel_guns | 14.2 | 83% |
| flamethrower + mortar | 14.2 | 100% |
| lightning_mast + mortar | 14.2 | 100% |
| flying_cutlasses + long_nine | 14.1 | 100% |
| deck_cannons + swivel_guns | 14 | 100% |
| broadside_battery + harpoon_gun | 14 | 100% |
| powder_kegs + flying_cutlasses | 14 | 100% |
| flying_cutlasses + mortar | 13.9 | 100% |
| lightning_mast + flying_cutlasses | 13.9 | 100% |
| broadside_battery + flying_cutlasses | 13.8 | 100% |
| deck_cannons + lightning_mast | 13.8 | 100% |
| broadside_battery + swivel_guns | 13.7 | 100% |
| swivel_guns + long_nine | 13.6 | 67% |
| swivel_guns + harpoon_gun | 13.6 | 100% |
| lightning_mast + long_nine | 13.6 | 100% |
| lightning_mast + harpoon_gun | 13.6 | 100% |
| deck_cannons + harpoon_gun | 13.5 | 100% |
| long_nine + mortar | 13.4 | 100% |
| flying_cutlasses + swivel_guns | 13.4 | 67% |
| harpoon_gun + mortar | 13.4 | 100% |
| long_nine + harpoon_gun | 13.4 | 100% |
| powder_kegs + harpoon_gun | 13.3 | 83% |
| flamethrower + long_nine | 13.3 | 83% |
| deck_cannons + broadside_battery | 13.2 | 100% |
| powder_kegs + lightning_mast | 13.2 | 100% |
| broadside_battery + long_nine | 13.2 | 100% |
| flying_cutlasses + flamethrower | 13.1 | 100% |
| broadside_battery + powder_kegs | 13.1 | 100% |
| deck_cannons + powder_kegs | 13 | 83% |
| powder_kegs + swivel_guns | 12.9 | 100% |
| broadside_battery + lightning_mast | 12.8 | 100% |
| broadside_battery + mortar | 12.7 | 83% |
| lightning_mast + flamethrower | 12.6 | 100% |
| deck_cannons + mortar | 12.5 | 83% |
| flamethrower + swivel_guns | 12.4 | 83% |
| swivel_guns + mortar | 12.4 | 67% |
| broadside_battery + flamethrower | 12.2 | 83% |
| deck_cannons + flying_cutlasses | 12.1 | 67% |
| flying_cutlasses + harpoon_gun | 12 | 67% |
| powder_kegs + long_nine | 11.8 | 100% |
| deck_cannons + long_nine | 11.8 | 100% |
| powder_kegs + flamethrower | 10.8 | 83% |
| deck_cannons + flamethrower | 9.3 | 50% |
| powder_kegs + mortar | 9.2 | 50% |
