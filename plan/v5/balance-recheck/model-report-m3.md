# V5 balance re-check: simulator report on the V5 M3 values (same code)

Config hash `80c6fbd3a512` · director Adaptive · 200 runs per personality · seed 1 · 59.9 s.
Targets missed: **2** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.1 / **8.4** / 14 | 100% | 0% | – | 24 / 38 | 74 | 391 | 31 | 31% (6.8) | ✓ median end minute 8.43 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.3 / **8.2** / 12 | 74% | 31% | 8 | 23 / 40 | 58 | 290 | 30 | 5% (7.3) | ✓ median end minute 8.25 (7–12); ✓ boss win rate 0.31 (0.1–0.6) |
| Casual Cruiser | 20% | 6.7 / **8.5** / 14 | 100% | 44% | 8.2 | 22 / 38 | 63 | 380 | 34 | 41% (6.6) | ✓ median end minute 8.53 (8–12); ✓ boss win rate 0.44 (0.3–0.7) |
| Steady Captain | 20% | 6.9 / **12.2** / 15 | 100% | 76% | 6.9 | 23 / 35 | 57 | 609 | 80 | 41% (7.2) | ✓ median end minute 12.23 (11–15); ✓ boss win rate 0.76 (0.6–0.85); ✓ level at 10:00 35 (30–46) |
| Glass Cannon | 7% | 7.4 / **13.7** / 15.1 | 100% | 94% | 5.3 | 24 / 39 | 80 | 881 | 71 | 48% (6.4) | ✗ median end minute 13.73 (8–13); ✓ boss win rate 0.94 (0.6–1) |
| Turtle | 6% | 6.8 / **8.9** / 14.4 | 100% | 43% | 8.3 | 21 / 37 | 53 | 310 | 30 | 37% (5.8) | ✓ median end minute 8.91 (8–13); ✓ boss win rate 0.43 (0.3–0.7) |
| Treasure Hound | 6% | 5 / **8.4** / 13.8 | 100% | 32% | 8.3 | 23 / 41 | 51 | 282 | 34 | 27% (7.1) | ✓ median end minute 8.38 (7.5–12); ✓ boss win rate 0.32 (0.2–0.6) |
| Build Planner | 5% | 7.3 / **14.1** / 15.7 | 100% | 90% | 5.9 | 22 / 36 | 57 | 710 | 76 | 69% (5.6) | ✓ median end minute 14.08 (13–19); ✓ boss win rate 0.9 (0.8–1) |
| Sea Legend | 4% | 14.5 / **16.5** / 19.8 | 100% | 100% | 5.4 | 22 / 36 | 52 | 758 | 85 | 79% (5.3) | ✗ median end minute 16.55 (17––); ✓ boss win rate 1 (0.95–1) |
| Speedrunner | 2% | 3.8 / **5.5** / 7.9 | 5% | 96% | 5.4 | 23 / – | 64 | 365 | 49 | 0% (–) | ✓ median end minute 5.51 (3–8); ✓ boss win rate 0.96 (0.5–1) |

Most players (weighted by share): median end minute ≈ **10.1**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 5 items, evolved 31% (first at 5.1–6.8 min, p25–median); Thumb Sailor 6 items, evolved 5% (first at 6.3–7.3 min, p25–median); Casual Cruiser 5 items, evolved 41% (first at 5.3–6.6 min, p25–median); Steady Captain 5 items, evolved 41% (first at 5.6–7.2 min, p25–median); Glass Cannon 7 items, evolved 48% (first at 5.2–6.4 min, p25–median); Turtle 5 items, evolved 37% (first at 4.7–5.8 min, p25–median); Treasure Hound 7 items, evolved 27% (first at 5.2–7.1 min, p25–median); Build Planner 6 items, evolved 69% (first at 3.9–5.6 min, p25–median); Sea Legend 6 items, evolved 79% (first at 3.8–5.3 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 14 | 703 | 85% |
| ember | 33 | 12.1 | 716 | 79% |
| flint | 33 | 11.8 | 683 | 67% |
| richie | 33 | 12.6 | 969 | 79% |
| rook | 34 | 13 | 1062 | 76% |
| roxie | 33 | 10.2 | 734 | 67% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.09 | 39 | 7 | 0.00 | 21 | 56 | 6 / 0.46 s | 73 / 6.0 s | 1.13 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–9); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.19 | 80 | 13 | 0.00 | 44 | 100 | 6 / 0.28 s | 82 / 3.7 s | 1.15 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (6–13); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 200 | 0.17 | 58 | 16 | 0.18 | 36 | 128 | 7 / 0.26 s | 98 / 3.5 s | 1.26 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (9–17); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 198 | 0.13 | 66 | 19 | 0.00 | 63 | 174 | 9 / 0.24 s | 123 / 3.2 s | 1.16 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (12–20); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 198 | 0.47 | 83 | 23 | 0.12 | 53 | 226 | 12 / 0.24 s | 156 / 3.1 s | 1.18 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (14–23); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 194 | 0.62 | 81 | 25 | 0.30 | 51 | 293 | 15 / 0.24 s | 200 / 3.1 s | 1.19 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (16–26); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 177 | 0.61 | 33 | 28 | 0.35 | 169 | 341 | 20 / 0.26 s | 257 / 3.4 s | 1.10 | ✓ alive share (0.25–0.75); ✓ kills (30–140); ✓ level (18–29); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 144 | 0.78 | 79 | 31 | 0.28 | 86 | 487 | 25 / 0.23 s | 328 / 3.1 s | 1.28 | ✓ alive share (0.3–0.85); ✓ kills (50–160); ✓ level (21–32); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 137 | 0.60 | 64 | 33 | 0.24 | 67 | 593 | 32 / 0.24 s | 417 / 3.2 s | 1.32 | ✓ alive share (0.22–0.8); ✓ kills (45–160); ✓ level (23–35); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 135 | 0.82 | 73 | 35 | 0.49 | 43 | 658 | 40 / 0.28 s | 527 / 3.6 s | 1.26 | ✓ alive share (0.35–0.9); ✓ kills (55–180); ✓ level (25–38); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 125 | 0.78 | 52 | 36 | 0.46 | 29 | 682 | 50 / 0.34 s | 661 / 4.4 s | 1.25 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (28–42) |
| 11 | 106 | 0.56 | 51 | 38 | 0.25 | 25 | 847 | 63 / 0.34 s | 823 / 4.4 s | 1.32 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (30–45) |
| 12 | 88 | 0.68 | 42 | 40 | 0.37 | 21 | 965 | 77 / 0.36 s | 1017 / 4.8 s | 1.35 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (32–48) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **8.57** (target 4–12)
- ✓ seconds between level-ups over 0:00–10:00: **17.14** (target 13–20)
- ✓ miniboss time to kill (s): **17.75** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.57** (target ––1)
- ✓ scheduled elite time to kill (s): **5.38** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **50** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| deck_cannons | 98 | 13.1 |
| broadside_battery | 104 | 13 |
| harpoon_gun | 78 | 13 |
| swivel_guns | 98 | 12.9 |
| mortar | 68 | 12.7 |
| flamethrower | 91 | 12.4 |
| flying_cutlasses | 77 | 12 |
| long_nine | 88 | 11.9 |
| powder_kegs | 99 | 11.7 |
| lightning_mast | 88 | 11.7 |
