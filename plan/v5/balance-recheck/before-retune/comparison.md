# V5 balance re-check: pre-demo values → demo baseline

Same code, 200 runs per personality, seed 1. Targets missed: **2 → 24** of 70.

| Personality | Median end minute | Boss won | Level 5:00 | Level 10:00 | Kills/min | Gold | Evolved | Targets now |
|---|---|---|---|---|---|---|---|---|
| First-Timer | 8.5 → **7.4** | 0% → **0%** | 24 → 14 | 38 → 24 | 74 → 50 | 392 → 220 | 30% → 6% | all met |
| Thumb Sailor | 8.2 → **6.4** | 31% → **9%** | 23 → 14 | 40 → 26 | 61 → 37 | 291 → 139 | 6% → 1% | ✗ median end minute 6.36 (7–12); boss win rate 0.09 (0.1–0.6) |
| Casual Cruiser | 8.5 → **8** | 45% → **12%** | 22 → 14 | 37 → 24 | 63 → 40 | 379 → 194 | 36% → 3% | ✗ boss win rate 0.12 (0.3–0.7) |
| Steady Captain | 12.8 → **7.9** | 82% → **49%** | 23 → 14 | 34 → 22 | 57 → 38 | 617 → 303 | 42% → 6% | ✗ median end minute 7.89 (11–15); boss win rate 0.49 (0.6–0.85); level at 10:00 22 (30–46) |
| Glass Cannon | 13.8 → **9.5** | 94% → **67%** | 24 → 14 | 39 → 24 | 81 → 44 | 881 → 397 | 47% → 3% | all met |
| Turtle | 8.8 → **8.1** | 40% → **11%** | 21 → 12 | 37 → 22 | 51 → 32 | 277 → 137 | 35% → 6% | ✗ boss win rate 0.11 (0.3–0.7) |
| Treasure Hound | 8.6 → **8.1** | 34% → **8%** | 22 → 13 | 39 → 25 | 49 → 34 | 276 → 155 | 25% → 1% | ✗ boss win rate 0.08 (0.2–0.6) |
| Build Planner | 14.1 → **8.8** | 89% → **54%** | 22 → 13 | 35 → 22 | 57 → 32 | 686 → 299 | 68% → 20% | ✗ median end minute 8.81 (13–19); boss win rate 0.54 (0.8–1) |
| Sea Legend | 16.5 → **16** | 100% → **93%** | 22 → 13 | 36 → 20 | 53 → 26 | 748 → 409 | 76% → 30% | ✗ median end minute 16.03 (17––); boss win rate 0.93 (0.95–1) |
| Speedrunner | 5.5 → **6** | 96% → **80%** | 23 → 13 | – → – | 64 → 34 | 365 → 263 | 0% → 0% | all met |

Most players (weighted by share): median end minute 10.2 → **8.1**.

## Pacing and per-minute targets that changed

- minute 6: kills: 33.5 → 21 (target 30–140) met → **MISSED**
- minute 6: level: 27 → 17 (target 18–29) met → **MISSED**
- minute 7: alive share: 0.75 → 0.86 (target 0.3–0.85) met → **MISSED**
- minute 7: kills: 78 → 49.5 (target 50–160) met → **MISSED**
- minute 7: level: 31 → 20 (target 21–32) met → **MISSED**
- minute 8: kills: 64 → 44 (target 45–160) met → **MISSED**
- minute 8: level: 33 → 21 (target 23–35) met → **MISSED**
- minute 9: kills: 70 → 53.5 (target 55–180) met → **MISSED**
- minute 9: level: 34 → 22 (target 25–38) met → **MISSED**
- minute 10: level: 36 → 24 (target 28–42) met → **MISSED**
- seconds between level-ups in minute 1: 8.57 → 15 (target 4–12) met → **MISSED**
- seconds between level-ups over 0:00–10:00: 17.65 → 27.27 (target 13–20) met → **MISSED**

## Weapons (power ratio to the role at each parity level)

| Weapon | L1 | L4 | L8 | L12 |
|---|---|---|---|---|
| deck_cannons | 1.00 → 1.00 | 0.96 → 0.96 | 1.01 → 1.01 | 1.03 → 1.03 |
| broadside_battery | 0.95 → 0.95 | 1.04 → 1.04 | 1.02 → 1.02 | 1.00 → 1.00 |
| powder_kegs | 1.05 → 1.05 | 0.96 → 0.96 | 0.97 → 0.97 | 1.16 → 1.16 |
| lightning_mast | 0.87 → 0.87 | 1.10 → 1.10 | 1.16 → 1.16 | 0.91 → 0.91 |
| flying_cutlasses | 0.95 → 0.95 | 1.05 → 1.05 | 1.06 → 1.06 | 0.92 → 0.92 |
| flamethrower | 1.11 → 1.11 | 0.91 → 0.91 | 0.92 → 0.92 | 1.08 → 1.08 |
| swivel_guns | 0.88 → 0.88 | 1.13 → 1.13 | 0.99 → 0.99 | 1.02 → 1.02 |
| long_nine | 1.01 → 1.01 | 1.04 → 1.04 | 0.99 → 0.99 | 0.97 → 0.97 |
| harpoon_gun | 1.12 → 1.12 | 0.89 → 0.89 | 1.01 → 1.01 | 0.99 → 0.99 |
| mortar | 1.10 → 1.10 | 0.94 → 0.94 | 0.89 → 0.89 | 0.95 → 0.95 |

## Which change moves the runs (each group put back alone on the demo baseline)

| Put back | Targets missed | Most players: median end minute | Steady Captain: end / boss won / level 10:00 | Casual Cruiser: boss won |
|---|---|---|---|---|
| nothing (the demo baseline) | 24 of 70 | 8.1 | 7.9 / 49% / 22 | 12% |
| XP pace (xpPickupMultiplier 0.5 → 1) | 2 of 76 | 10.2 | 12.7 / 78% / 35 | 43% |
| ship speed (shipSpeedMultiplier 1.625 → 1) | 23 of 73 | 7.7 | 8 / 50% / 23 | 12% |
| weapon ranges and shot lifetimes | 21 of 70 | 8.4 | 7.9 / 51% / 23 | 17% |
| enemies (charge timings, ranged from 1:00, band 0 specialists, kraken spawn in view) | 18 of 70 | 8.2 | 7.9 / 49% / 23 | 16% |
