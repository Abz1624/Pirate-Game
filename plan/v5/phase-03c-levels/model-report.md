# V5 M3c: simulator report with the level-up rhythm

Config hash `2194661eb224` · director Adaptive · 200 runs per personality · seed 1 · 32.3 s.
Targets missed: **2** of 76.

## Personalities

| Personality | Share | End minute p10 / median / p90 | Sunk | Boss won | Boss sunk at | Level 5:00 / 10:00 | Kills/min | Gold | Rum | Evolved (minute) | Targets |
|---|---|---|---|---|---|---|---|---|---|---|---|
| First-Timer | 15% | 3.3 / **8.9** / 13 | 100% | 0% | – | 9 / 15 | 51 | 277 | 32 | 35% (6.4) | ✓ median end minute 8.87 (5.5–9); ✓ boss win rate 0 (0–0.15) |
| Thumb Sailor | 15% | 3.2 / **7.9** / 12 | 87% | 21% | 8.3 | 9 / 16 | 37 | 172 | 27 | 13% (6.8) | ✓ median end minute 7.94 (7–12); ✓ boss win rate 0.21 (0.1–0.6) |
| Casual Cruiser | 20% | 7.6 / **8.8** / 13.8 | 100% | 42% | 8.6 | 9 / 15 | 41 | 230 | 33 | 42% (5.6) | ✓ median end minute 8.81 (8–12); ✓ boss win rate 0.42 (0.3–0.7) |
| Steady Captain | 20% | 7.4 / **13.7** / 16.1 | 100% | 79% | 7.1 | 9 / 14 | 38 | 468 | 81 | 52% (7.1) | ✓ median end minute 13.65 (11–15); ✓ boss win rate 0.79 (0.6–0.85); ✓ level at 10:00 14 (10–16) |
| Glass Cannon | 7% | 6.3 / **13.2** / 15.2 | 100% | 84% | 5.7 | 9 / 15 | 45 | 500 | 68 | 49% (5.9) | ✗ median end minute 13.15 (8–13); ✓ boss win rate 0.84 (0.6–1) |
| Turtle | 6% | 6.7 / **8.9** / 13.8 | 100% | 36% | 8.7 | 8 / 14 | 33 | 176 | 29 | 33% (5.3) | ✓ median end minute 8.91 (8–13); ✓ boss win rate 0.36 (0.3–0.7) |
| Treasure Hound | 6% | 4.3 / **8.9** / 14.8 | 100% | 39% | 8.5 | 9 / 15 | 35 | 191 | 33 | 33% (6.6) | ✓ median end minute 8.85 (7.5–12); ✓ boss win rate 0.39 (0.2–0.6) |
| Build Planner | 5% | 6.7 / **14.4** / 16.9 | 100% | 80% | 6.8 | 8 / 13 | 32 | 432 | 73 | 64% (5.5) | ✓ median end minute 14.39 (13–19); ✓ boss win rate 0.8 (0.8–1) |
| Sea Legend | 4% | 15.7 / **18.7** / 22.6 | 100% | 100% | 6.3 | 8 / 13 | 29 | 480 | 83 | 72% (5.3) | ✓ median end minute 18.66 (17––); ✓ boss win rate 1 (0.95–1) |
| Speedrunner | 2% | 4.3 / **6.2** / 8.5 | 5% | 96% | 6.2 | 8 / – | 35 | 279 | 47 | 0% (–) | ✓ median end minute 6.22 (3–8); ✓ boss win rate 0.96 (0.5–1) |

Most players (weighted by share): median end minute ≈ **10.6**.

Items by 10:00 (runs that reached it) and evolutions: First-Timer 3 items, evolved 35% (first at 5.2–6.4 min, p25–median); Thumb Sailor 4 items, evolved 13% (first at 5.4–6.8 min, p25–median); Casual Cruiser 4 items, evolved 42% (first at 4.2–5.6 min, p25–median); Steady Captain 5 items, evolved 52% (first at 5.5–7.1 min, p25–median); Glass Cannon 6 items, evolved 49% (first at 4.3–5.9 min, p25–median); Turtle 4 items, evolved 33% (first at 3.6–5.3 min, p25–median); Treasure Hound 5 items, evolved 33% (first at 4.3–6.6 min, p25–median); Build Planner 5 items, evolved 64% (first at 4–5.5 min, p25–median); Sea Legend 5 items, evolved 72% (first at 3.6–5.3 min, p25–median); Speedrunner – items, evolved 0% (first at ––– min, p25–median).

## Captains (baseline personality)

| Captain | Runs | Median end minute | Offense at 10:00 | Boss won |
|---|---|---|---|---|
| brine | 34 | 14.6 | 254 | 79% |
| ember | 33 | 13.1 | 247 | 76% |
| flint | 33 | 14.1 | 259 | 85% |
| richie | 33 | 13.2 | 321 | 73% |
| rook | 34 | 13.9 | 228 | 85% |
| roxie | 33 | 12.1 | 233 | 73% |

## Baseline per minute (model)

Medians of the baseline personality's runs that reached the minute. Model TTK = the fodder's / first scheduled elite's HP at the minute ÷ (the build's single-target DPS × the calibrated hit efficiency); it depends directly on that efficiency (fitted on crowd kills), so the measured fight lengths under Pacing are the ones checked.

| Min | Runs | Alive (share of cap) | Kills | Level | Damage taken (per 100 hull) | Gold | DPS (1 target) | Fodder HP / TTK | Elite HP / TTK | Pressure | Checks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 200 | 0.09 | 30 | 4 | 0.00 | 16 | 42 | 5 / 0.50 s | 61 / 6.6 s | 1.10 | ✓ alive share (0.03–0.3); ✓ kills (8–45); ✓ level (3–5); ✓ damage taken (share of 100 hull) (––0.35) |
| 1 | 200 | 0.21 | 54 | 6 | 0.00 | 29 | 54 | 5 / 0.42 s | 65 / 5.5 s | 1.03 | ✓ alive share (0.1–0.45); ✓ kills (18–90); ✓ level (4–6); ✓ damage taken (share of 100 hull) (––0.45) |
| 2 | 200 | 0.23 | 39 | 6 | 0.20 | 21 | 58 | 6 / 0.43 s | 72 / 5.7 s | 1.01 | ✓ alive share (0.15–0.55); ✓ kills (25–90); ✓ level (5–7); ✓ damage taken (share of 100 hull) (––0.6) |
| 3 | 199 | 0.16 | 32 | 7 | 0.10 | 44 | 69 | 6 / 0.42 s | 83 / 5.5 s | 1.00 | ✓ alive share (0.12–0.55); ✓ kills (25–100); ✓ level (6–8); ✓ damage taken (share of 100 hull) (––0.6) |
| 4 | 199 | 0.51 | 56 | 9 | 0.14 | 29 | 83 | 7 / 0.40 s | 97 / 5.3 s | 1.00 | ✓ alive share (0.2–0.65); ✓ kills (35–120); ✓ level (7–10); ✓ damage taken (share of 100 hull) (––0.7) |
| 5 | 197 | 0.66 | 58 | 10 | 0.27 | 31 | 100 | 9 / 0.39 s | 114 / 5.2 s | 1.00 | ✓ alive share (0.18–0.7); ✓ kills (35–130); ✓ level (7–11); ✓ damage taken (share of 100 hull) (––0.8) |
| 6 | 188 | 0.70 | 23 | 10 | 0.36 | 13 | 110 | 10 / 0.43 s | 136 / 5.6 s | 1.00 | ✓ alive share (0.25–0.75); ✗ kills (30–140); ✓ level (8–12); ✓ damage taken (share of 100 hull) (––0.9) |
| 7 | 155 | 0.85 | 59 | 13 | 0.33 | 151 | 163 | 12 / 0.34 s | 162 / 4.5 s | 1.00 | ✓ alive share (0.3–0.85); ✓ kills (50–160); ✓ level (9–13); ✓ damage taken (share of 100 hull) (––1) |
| 8 | 145 | 0.66 | 49 | 14 | 0.22 | 54 | 193 | 15 / 0.35 s | 193 / 4.5 s | 1.00 | ✓ alive share (0.22–0.8); ✓ kills (45–160); ✓ level (10–14); ✓ damage taken (share of 100 hull) (––1) |
| 9 | 143 | 0.85 | 58 | 14 | 0.32 | 31 | 206 | 18 / 0.39 s | 231 / 5.1 s | 1.00 | ✓ alive share (0.35–0.9); ✓ kills (55–180); ✓ level (10–16); ✓ damage taken (share of 100 hull) (––1.1) |
| 10 | 140 | 0.88 | 42 | 15 | 0.40 | 21 | 213 | 21 / 0.45 s | 275 / 5.9 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–200); ✓ level (11–17) |
| 11 | 128 | 0.89 | 34 | 15 | 0.35 | 18 | 232 | 25 / 0.49 s | 328 / 6.4 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–220); ✓ level (12–18) |
| 12 | 116 | 0.90 | 32 | 16 | 0.37 | 16 | 260 | 30 / 0.52 s | 390 / 6.8 s | 1.00 | ✓ alive share (0.25–1); ✓ kills (30–240); ✓ level (13–19) |

## Pacing (baseline)

- ✓ seconds between level-ups in minute 1: **15** (target 12–18)
- ✓ seconds between level-ups over 0:00–10:00: **42.86** (target 37–55)
- ✓ miniboss time to kill (s): **24.25** (target 15–30)
- ✓ fodder time to kill (s, first hit to sunk): **0.78** (target ––1)
- ✓ scheduled elite time to kill (s): **6** (target 4–8)
- ✓ Barnacle King fight (s, summon to sunk): **63.75** (target 45–90)

## Weapons in the baseline runs

| Weapon | Runs carrying it | Median end minute |
|---|---|---|
| deck_cannons | 79 | 14.3 |
| swivel_guns | 97 | 14.1 |
| harpoon_gun | 75 | 14 |
| powder_kegs | 102 | 14 |
| broadside_battery | 73 | 13.8 |
| mortar | 68 | 13.8 |
| long_nine | 78 | 13.4 |
| flamethrower | 89 | 13.4 |
| flying_cutlasses | 73 | 13.4 |
| lightning_mast | 92 | 12.7 |
