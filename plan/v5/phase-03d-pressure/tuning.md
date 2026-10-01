# V5 M3d: tuning log (simulator, 200 runs per personality, seed 1)

Each round applied `CountPressure.Apply` with the listed tuning to a runtime copy of the data (no assets changed) and ran `SimBatch.Run(c, 200, 1)`. Rounds 1-5 in order; the chosen values are `CountPressure.Tuning.Default` (`model-report.md` is the final data).

# Round 1

## floor 1 rate 1 horde 1 boss 2600 eliteH 7.5 eliteK 16 crab 4.5: missed 14 of 76, weighted end 17.2
  first_timer: end 15.2 boss 0% evolved 42% L10 16
  thumb_sailor: end 5.5 boss 37% evolved 19% L10 17
  casual_cruiser: end 18.0 boss 75% evolved 64% L10 16
  steady_captain: end 23.7 boss 90% evolved 70% L10 15
  glass_cannon: end 20.4 boss 74% evolved 60% L10 16
  turtle: end 13.4 boss 57% evolved 50% L10 15
  treasure_hound: end 16.2 boss 59% evolved 48% L10 16
  build_planner: end 24.5 boss 83% evolved 73% L10 15
  sea_legend: end 30.0 boss 100% evolved 82% L10 14
  speedrunner: end 6.3 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.21/39/0.01  1:0.27/55/0.06  2:0.37/44/0.27  3:0.32/42/0.22  4:0.48/65/0.13  5:0.51/66/0.14  6:0.57/25/0.24  7:0.80/83/0.21  8:0.44/68/0.08  9:0.67/106/0.14  10:0.38/65/0.08  11:0.14/53/0.00  12:0.20/70/0.00
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 25 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 1.1 (target ––1) MISSED
  pacing scheduled elite time to kill (s): 6.13 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 67 (target 45–90) met
  MISS First-Timer: median end minute: 15.21 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 5.48 (target 7–12) MISSED
  MISS Casual Cruiser: median end minute: 17.96 (target 8–12) MISSED
  MISS Casual Cruiser: boss win rate: 0.75 (target 0.3–0.7) MISSED
  MISS Steady Captain: median end minute: 23.73 (target 11–15) MISSED
  MISS Steady Captain: boss win rate: 0.9 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 20.38 (target 8–13) MISSED
  MISS Turtle: median end minute: 13.41 (target 8–13) MISSED
  MISS Treasure Hound: median end minute: 16.15 (target 7.5–12) MISSED
  MISS Build Planner: median end minute: 24.53 (target 13–19) MISSED
  MISS minute 6: kills: 25 (target 30–140) MISSED
  MISS minute 11: alive share: 0.14 (target 0.25–1) MISSED
  MISS minute 12: alive share: 0.2 (target 0.25–1) MISSED
  MISS fodder time to kill (s, first hit to sunk): 1.1 (target ––1) MISSED
DONE

# Round 2

## late 1.6/1.4 bands 0.5/0.6 OT 2.5 ramp 2-3 | floor 1 rate 1 horde 1 boss 2600 eliteH 7.5 eliteK 16 crab 4.5: missed 8 of 76, weighted end 12.1
  first_timer: end 10.7 boss 0% evolved 36% L10 16
  thumb_sailor: end 5.6 boss 38% evolved 14% L10 17
  casual_cruiser: end 12.8 boss 67% evolved 56% L10 16
  steady_captain: end 15.2 boss 86% evolved 66% L10 15
  glass_cannon: end 14.1 boss 75% evolved 55% L10 16
  turtle: end 10.1 boss 56% evolved 44% L10 15
  treasure_hound: end 10.5 boss 56% evolved 42% L10 16
  build_planner: end 15.5 boss 83% evolved 68% L10 15
  sea_legend: end 22.8 boss 100% evolved 83% L10 14
  speedrunner: end 6.3 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.21/39/0.01  1:0.27/55/0.06  2:0.37/44/0.27  3:0.32/42/0.22  4:0.48/65/0.13  5:0.51/66/0.14  6:0.58/24/0.28  7:0.79/75/0.23  8:0.42/61/0.13  9:0.69/95/0.21  10:0.60/53/0.25  11:0.53/44/0.14  12:0.77/39/0.25
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 25 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.73 (target ––1) met
  pacing scheduled elite time to kill (s): 8.75 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 63 (target 45–90) met
  MISS First-Timer: median end minute: 10.65 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 5.59 (target 7–12) MISSED
  MISS Casual Cruiser: median end minute: 12.84 (target 8–12) MISSED
  MISS Steady Captain: median end minute: 15.17 (target 11–15) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 14.09 (target 8–13) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 8.75 (target 4–8) MISSED
## late 2/1.6 bands 0.55/0.65 OT 3 ramp 2-3.5 | floor 1 rate 1 horde 1 boss 2600 eliteH 7.5 eliteK 16 crab 4.5: missed 5 of 76, weighted end 11.2
  first_timer: end 9.7 boss 0% evolved 35% L10 16
  thumb_sailor: end 5.6 boss 38% evolved 12% L10 17
  casual_cruiser: end 11.5 boss 63% evolved 53% L10 16
  steady_captain: end 14.0 boss 85% evolved 62% L10 15
  glass_cannon: end 13.0 boss 74% evolved 53% L10 16
  turtle: end 10.1 boss 55% evolved 43% L10 15
  treasure_hound: end 9.1 boss 51% evolved 40% L10 16
  build_planner: end 14.8 boss 81% evolved 63% L10 14
  sea_legend: end 20.6 boss 100% evolved 77% L10 13
  speedrunner: end 6.4 boss 89% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.21/39/0.01  1:0.27/55/0.06  2:0.37/44/0.27  3:0.32/42/0.22  4:0.48/65/0.13  5:0.51/66/0.14  6:0.59/24/0.26  7:0.81/72/0.26  8:0.42/53/0.14  9:0.72/87/0.22  10:0.76/49/0.29  11:0.72/38/0.22  12:0.83/35/0.35
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 25 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.59 (target ––1) met
  pacing scheduled elite time to kill (s): 9.88 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 66.13 (target 45–90) met
  MISS First-Timer: median end minute: 9.69 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 5.62 (target 7–12) MISSED
  MISS Glass Cannon: median end minute: 13.02 (target 8–13) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 9.88 (target 4–8) MISSED
## late 1.6/1.4 bands 0.5/0.6 OT 2.5 ramp 2-3 | floor 1.1 rate 1 horde 1 boss 2600 eliteH 7.5 eliteK 16 crab 4.5: missed 7 of 76, weighted end 11.3
  first_timer: end 10.1 boss 0% evolved 36% L10 17
  thumb_sailor: end 3.8 boss 37% evolved 22% L10 17
  casual_cruiser: end 11.9 boss 61% evolved 48% L10 16
  steady_captain: end 14.9 boss 86% evolved 63% L10 15
  glass_cannon: end 13.9 boss 74% evolved 51% L10 16
  turtle: end 9.9 boss 57% evolved 42% L10 15
  treasure_hound: end 9.2 boss 48% evolved 37% L10 16
  build_planner: end 15.3 boss 80% evolved 64% L10 14
  sea_legend: end 22.5 boss 100% evolved 73% L10 14
  speedrunner: end 6.3 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.23/39/0.06  1:0.30/54/0.08  2:0.40/46/0.29  3:0.34/43/0.19  4:0.52/67/0.14  5:0.53/66/0.16  6:0.60/25/0.28  7:0.81/69/0.24  8:0.47/56/0.08  9:0.73/91/0.19  10:0.70/53/0.22  11:0.66/44/0.20  12:0.77/40/0.26
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 23.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.63 (target ––1) met
  pacing scheduled elite time to kill (s): 8.06 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 65.75 (target 45–90) met
  MISS First-Timer: median end minute: 10.1 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 3.79 (target 7–12) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.9 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.8 (target 0.8–1) MISSED
  MISS minute 6: kills: 25 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 8.06 (target 4–8) MISSED
## late 2/1.6 bands 0.55/0.65 OT 3 ramp 2-3.5 | floor 1.1 rate 1 horde 1 boss 2600 eliteH 7.5 eliteK 16 crab 4.5: missed 7 of 76, weighted end 10.7
  first_timer: end 9.3 boss 0% evolved 35% L10 17
  thumb_sailor: end 3.8 boss 33% evolved 15% L10 17
  casual_cruiser: end 10.7 boss 61% evolved 50% L10 16
  steady_captain: end 14.3 boss 87% evolved 63% L10 15
  glass_cannon: end 13.2 boss 74% evolved 51% L10 16
  turtle: end 9.6 boss 54% evolved 40% L10 15
  treasure_hound: end 8.8 boss 46% evolved 35% L10 16
  build_planner: end 14.7 boss 80% evolved 61% L10 14
  sea_legend: end 20.4 boss 100% evolved 76% L10 14
  speedrunner: end 6.3 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.23/39/0.06  1:0.30/54/0.08  2:0.40/46/0.29  3:0.34/43/0.19  4:0.52/67/0.14  5:0.53/66/0.16  6:0.59/25/0.28  7:0.82/69/0.24  8:0.47/53/0.15  9:0.72/88/0.26  10:0.76/48/0.32  11:0.81/36/0.28  12:0.89/34/0.44
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 23.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.56 (target ––1) met
  pacing scheduled elite time to kill (s): 9.5 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 66.75 (target 45–90) met
  MISS First-Timer: median end minute: 9.29 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 3.79 (target 7–12) MISSED
  MISS Steady Captain: boss win rate: 0.87 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.25 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.8 (target 0.8–1) MISSED
  MISS minute 6: kills: 25 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 9.5 (target 4–8) MISSED
DONE

# Round 3

## early 0.8 mid 1 | floor 1 rate 1 horde 1 boss 2600 eliteH 6 eliteK 13 crab 4.5: missed 7 of 76, weighted end 11.3
  first_timer: end 10.5 boss 0% evolved 39% L10 16
  thumb_sailor: end 3.9 boss 34% evolved 12% L10 16
  casual_cruiser: end 12.4 boss 70% evolved 56% L10 16
  steady_captain: end 14.5 boss 87% evolved 61% L10 15
  glass_cannon: end 13.4 boss 78% evolved 53% L10 16
  turtle: end 9.5 boss 53% evolved 41% L10 15
  treasure_hound: end 9.1 boss 50% evolved 40% L10 16
  build_planner: end 14.5 boss 80% evolved 62% L10 14
  sea_legend: end 20.9 boss 100% evolved 73% L10 14
  speedrunner: end 6.3 boss 93% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.17/37/0.00  1:0.23/51/0.00  2:0.31/44/0.22  3:0.27/43/0.18  4:0.43/62/0.13  5:0.52/62/0.15  6:0.63/24/0.26  7:0.83/69/0.23  8:0.47/59/0.13  9:0.74/85/0.25  10:0.75/46/0.30  11:0.82/39/0.23  12:0.89/34/0.34
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.5 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.59 (target ––1) met
  pacing scheduled elite time to kill (s): 8 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 66.5 (target 45–90) met
  MISS First-Timer: median end minute: 10.47 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 3.95 (target 7–12) MISSED
  MISS Casual Cruiser: median end minute: 12.35 (target 8–12) MISSED
  MISS Steady Captain: boss win rate: 0.87 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.41 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.8 (target 0.8–1) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED
## early 0.7 mid 1 | floor 1 rate 1 horde 1 boss 2600 eliteH 6 eliteK 13 crab 4.5: missed 5 of 76, weighted end 11.5
  first_timer: end 10.6 boss 0% evolved 40% L10 16
  thumb_sailor: end 7.7 boss 36% evolved 16% L10 17
  casual_cruiser: end 11.2 boss 62% evolved 50% L10 16
  steady_captain: end 13.9 boss 87% evolved 56% L10 15
  glass_cannon: end 13.3 boss 85% evolved 58% L10 15
  turtle: end 9.7 boss 55% evolved 38% L10 14
  treasure_hound: end 8.9 boss 44% evolved 38% L10 16
  build_planner: end 15.0 boss 81% evolved 66% L10 14
  sea_legend: end 20.7 boss 99% evolved 76% L10 14
  speedrunner: end 6.4 boss 94% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.24/42/0.19  4:0.43/62/0.08  5:0.48/65/0.14  6:0.57/23/0.26  7:0.83/63/0.31  8:0.48/54/0.14  9:0.75/79/0.18  10:0.84/43/0.30  11:0.89/34/0.34  12:0.90/31/0.37
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 8.75 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 66 (target 45–90) met
  MISS First-Timer: median end minute: 10.61 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.87 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.33 (target 8–13) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 8.75 (target 4–8) MISSED
## early 0.8 mid 1.15 | floor 1 rate 1 horde 1 boss 2600 eliteH 6 eliteK 13 crab 4.5: missed 6 of 76, weighted end 11.0
  first_timer: end 10.5 boss 0% evolved 38% L10 16
  thumb_sailor: end 3.9 boss 36% evolved 19% L10 17
  casual_cruiser: end 11.7 boss 65% evolved 58% L10 16
  steady_captain: end 14.1 boss 86% evolved 56% L10 15
  glass_cannon: end 13.4 boss 79% evolved 52% L10 16
  turtle: end 9.1 boss 49% evolved 40% L10 15
  treasure_hound: end 9.1 boss 51% evolved 35% L10 16
  build_planner: end 14.4 boss 77% evolved 66% L10 14
  sea_legend: end 20.9 boss 100% evolved 76% L10 14
  speedrunner: end 6.4 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.17/37/0.00  1:0.23/51/0.00  2:0.31/44/0.22  3:0.27/44/0.19  4:0.45/63/0.08  5:0.52/62/0.16  6:0.64/23/0.28  7:0.85/61/0.28  8:0.52/54/0.14  9:0.74/86/0.26  10:0.77/47/0.27  11:0.83/36/0.30  12:0.89/34/0.34
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.5 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.56 (target ––1) met
  pacing scheduled elite time to kill (s): 7.63 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 70.25 (target 45–90) met
  MISS First-Timer: median end minute: 10.46 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 3.95 (target 7–12) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.44 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.77 (target 0.8–1) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED
## early 0.7 mid 1.15 | floor 1 rate 1 horde 1 boss 2600 eliteH 6 eliteK 13 crab 4.5: missed 5 of 76, weighted end 11.3
  first_timer: end 9.7 boss 0% evolved 34% L10 16
  thumb_sailor: end 7.7 boss 36% evolved 20% L10 17
  casual_cruiser: end 10.9 boss 65% evolved 51% L10 15
  steady_captain: end 13.8 boss 86% evolved 59% L10 15
  glass_cannon: end 13.4 boss 81% evolved 48% L10 16
  turtle: end 9.5 boss 51% evolved 41% L10 14
  treasure_hound: end 8.8 boss 45% evolved 32% L10 16
  build_planner: end 14.5 boss 81% evolved 64% L10 15
  sea_legend: end 20.5 boss 99% evolved 72% L10 14
  speedrunner: end 6.4 boss 95% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/41/0.20  4:0.45/62/0.12  5:0.54/62/0.15  6:0.65/22/0.32  7:0.85/56/0.31  8:0.57/51/0.16  9:0.79/76/0.22  10:0.85/42/0.28  11:0.88/33/0.30  12:0.89/32/0.34
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.58 (target ––1) met
  pacing scheduled elite time to kill (s): 8.38 (target 4–8) MISSED
  pacing Barnacle King fight (s, summon to sunk): 70.88 (target 45–90) met
  MISS First-Timer: median end minute: 9.71 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.4 (target 8–13) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS scheduled elite time to kill (s): 8.38 (target 4–8) MISSED
DONE

# Round 4

## late crowd 1 | floor 1 rate 1 horde 1 boss 2900 eliteH 5.5 eliteK 12 crab 4.5: missed 6 of 76, weighted end 11.3
  first_timer: end 10.3 boss 0% evolved 37% L10 16
  thumb_sailor: end 7.5 boss 33% evolved 17% L10 17
  casual_cruiser: end 10.7 boss 61% evolved 48% L10 15
  steady_captain: end 13.9 boss 84% evolved 63% L10 15
  glass_cannon: end 13.1 boss 76% evolved 48% L10 16
  turtle: end 9.6 boss 51% evolved 43% L10 14
  treasure_hound: end 8.8 boss 40% evolved 32% L10 16
  build_planner: end 14.7 boss 79% evolved 62% L10 15
  sea_legend: end 21.1 boss 99% evolved 77% L10 14
  speedrunner: end 6.5 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/41/0.20  4:0.46/61/0.12  5:0.56/61/0.16  6:0.68/22/0.29  7:0.86/49/0.36  8:0.56/52/0.21  9:0.79/74/0.26  10:0.85/42/0.29  11:0.89/32/0.31  12:0.89/34/0.30
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 7.81 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 77.5 (target 45–90) met
  MISS First-Timer: median end minute: 10.26 (target 5.5–9) MISSED
  MISS Glass Cannon: median end minute: 13.07 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.79 (target 0.8–1) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS minute 7: alive share: 0.86 (target 0.3–0.85) MISSED
  MISS minute 7: kills: 49 (target 50–160) MISSED
## late crowd 1.1 | floor 1 rate 1 horde 1 boss 2900 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 11.2
  first_timer: end 10.0 boss 0% evolved 37% L10 16
  thumb_sailor: end 7.5 boss 33% evolved 16% L10 17
  casual_cruiser: end 10.7 boss 61% evolved 49% L10 16
  steady_captain: end 13.8 boss 84% evolved 63% L10 15
  glass_cannon: end 13.0 boss 76% evolved 47% L10 16
  turtle: end 9.6 boss 51% evolved 43% L10 14
  treasure_hound: end 8.8 boss 40% evolved 32% L10 16
  build_planner: end 14.6 boss 79% evolved 62% L10 15
  sea_legend: end 20.3 boss 99% evolved 76% L10 14
  speedrunner: end 6.5 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/41/0.20  4:0.46/61/0.12  5:0.56/61/0.16  6:0.68/22/0.29  7:0.86/49/0.36  8:0.56/52/0.22  9:0.78/75/0.24  10:0.84/44/0.31  11:0.88/33/0.32  12:0.90/33/0.43
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.53 (target ––1) met
  pacing scheduled elite time to kill (s): 7.81 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 77.5 (target 45–90) met
  MISS First-Timer: median end minute: 10 (target 5.5–9) MISSED
  MISS Build Planner: boss win rate: 0.79 (target 0.8–1) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS minute 7: alive share: 0.86 (target 0.3–0.85) MISSED
  MISS minute 7: kills: 49 (target 50–160) MISSED
## late crowd 1.1 | floor 1 rate 1 horde 1 boss 3200 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 11.0
  first_timer: end 10.0 boss 0% evolved 37% L10 16
  thumb_sailor: end 7.5 boss 30% evolved 18% L10 17
  casual_cruiser: end 9.6 boss 55% evolved 48% L10 16
  steady_captain: end 13.8 boss 80% evolved 61% L10 15
  glass_cannon: end 12.9 boss 73% evolved 50% L10 16
  turtle: end 9.4 boss 48% evolved 42% L10 14
  treasure_hound: end 8.8 boss 37% evolved 33% L10 16
  build_planner: end 14.3 boss 77% evolved 62% L10 15
  sea_legend: end 20.5 boss 99% evolved 74% L10 13
  speedrunner: end 6.6 boss 90% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/41/0.20  4:0.46/61/0.12  5:0.56/61/0.16  6:0.68/22/0.32  7:0.86/42/0.36  8:0.57/52/0.21  9:0.79/75/0.24  10:0.86/42/0.32  11:0.88/34/0.29  12:0.89/35/0.31
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 7.38 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 82.75 (target 45–90) met
  MISS First-Timer: median end minute: 10 (target 5.5–9) MISSED
  MISS Build Planner: boss win rate: 0.77 (target 0.8–1) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS minute 7: alive share: 0.86 (target 0.3–0.85) MISSED
  MISS minute 7: kills: 42 (target 50–160) MISSED
DONE

# Round 5

## mid 1.25 lateDmg 1.6 | floor 1 rate 1 horde 1 boss 2800 eliteH 5.5 eliteK 12 crab 4.5: missed 4 of 76, weighted end 11.4
  first_timer: end 10.0 boss 0% evolved 36% L10 16
  thumb_sailor: end 7.8 boss 37% evolved 17% L10 17
  casual_cruiser: end 11.1 boss 65% evolved 54% L10 15
  steady_captain: end 14.0 boss 82% evolved 59% L10 15
  glass_cannon: end 13.2 boss 85% evolved 60% L10 16
  turtle: end 9.8 boss 53% evolved 42% L10 14
  treasure_hound: end 8.8 boss 44% evolved 31% L10 16
  build_planner: end 14.5 boss 79% evolved 62% L10 15
  sea_legend: end 20.3 boss 99% evolved 78% L10 14
  speedrunner: end 6.4 boss 90% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/40/0.20  4:0.47/64/0.12  5:0.54/65/0.21  6:0.65/23/0.32  7:0.85/59/0.31  8:0.54/56/0.14  9:0.77/83/0.24  10:0.80/48/0.34  11:0.86/35/0.25  12:0.89/34/0.37
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.59 (target ––1) met
  pacing scheduled elite time to kill (s): 7.38 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 73 (target 45–90) met
  MISS First-Timer: median end minute: 10 (target 5.5–9) MISSED
  MISS Glass Cannon: median end minute: 13.22 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.79 (target 0.8–1) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED
## mid 1.15 lateDmg 2 | floor 1 rate 1 horde 1 boss 2800 eliteH 5.5 eliteK 12 crab 4.5: missed 6 of 76, weighted end 11.0
  first_timer: end 9.8 boss 0% evolved 37% L10 16
  thumb_sailor: end 7.5 boss 32% evolved 18% L10 17
  casual_cruiser: end 10.4 boss 61% evolved 48% L10 16
  steady_captain: end 13.4 boss 84% evolved 57% L10 15
  glass_cannon: end 13.2 boss 78% evolved 53% L10 16
  turtle: end 9.4 boss 52% evolved 40% L10 14
  treasure_hound: end 8.8 boss 41% evolved 31% L10 16
  build_planner: end 14.1 boss 79% evolved 60% L10 15
  sea_legend: end 20.5 boss 99% evolved 77% L10 14
  speedrunner: end 6.4 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/41/0.20  4:0.46/61/0.12  5:0.56/61/0.16  6:0.68/23/0.29  7:0.86/49/0.33  8:0.57/52/0.19  9:0.79/76/0.28  10:0.84/45/0.38  11:0.86/35/0.35  12:0.89/36/0.37
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.52 (target ––1) met
  pacing scheduled elite time to kill (s): 7.75 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 75.5 (target 45–90) met
  MISS First-Timer: median end minute: 9.81 (target 5.5–9) MISSED
  MISS Glass Cannon: median end minute: 13.19 (target 8–13) MISSED
  MISS Build Planner: boss win rate: 0.79 (target 0.8–1) MISSED
  MISS minute 6: kills: 22.5 (target 30–140) MISSED
  MISS minute 7: alive share: 0.86 (target 0.3–0.85) MISSED
  MISS minute 7: kills: 49 (target 50–160) MISSED
## mid 1.25 lateDmg 2 | floor 1 rate 1 horde 1 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 4 of 76, weighted end 11.2
  first_timer: end 9.7 boss 0% evolved 36% L10 16
  thumb_sailor: end 7.8 boss 36% evolved 20% L10 17
  casual_cruiser: end 11.0 boss 64% evolved 53% L10 16
  steady_captain: end 13.7 boss 81% evolved 56% L10 15
  glass_cannon: end 13.1 boss 86% evolved 63% L10 16
  turtle: end 9.5 boss 54% evolved 39% L10 15
  treasure_hound: end 8.8 boss 42% evolved 33% L10 16
  build_planner: end 14.4 boss 80% evolved 65% L10 15
  sea_legend: end 20.2 boss 99% evolved 72% L10 14
  speedrunner: end 6.4 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.16/35/0.00  1:0.21/51/0.00  2:0.28/42/0.22  3:0.25/40/0.20  4:0.47/64/0.12  5:0.54/65/0.21  6:0.65/24/0.32  7:0.85/65/0.28  8:0.54/57/0.16  9:0.76/85/0.23  10:0.79/43/0.30  11:0.85/35/0.26  12:0.89/34/0.30
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.54 (target ––1) met
  pacing scheduled elite time to kill (s): 7.25 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 69.38 (target 45–90) met
  MISS First-Timer: median end minute: 9.69 (target 5.5–9) MISSED
  MISS Glass Cannon: median end minute: 13.12 (target 8–13) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED
  MISS minute 7: alive share: 0.85 (target 0.3–0.85) MISSED
DONE

# After the independent review (rounds 6-10)

The review found the curve did not follow the sketch (per-stretch scales turned the valley into a climb) and that targets had been fitted to measurements. The keyframes were re-authored as final values (one scale for the whole curve), the per-minute targets derived from the design (`CountPressure.TargetEdits`), the hordes made ordinary ships, and the tuning re-run. Round 6: the new curve; round 7: the floor peak moved to 3:45 (the horde's lull and the elite's release window had erased it); round 8: gentler rate or floor variants (rate ×0.9 saved the Thumb Sailor but took all hull loss out of 0:00-2:00, so it was not taken); round 9: lower spawn rates in the valley only (4:10-6:35); round 10: the Barnacle King at 2500 and 2350 HP (2500 taken). Targets in rounds 6-10 are M3c's (the new ones are checked in `model-report.md`).

## Round 6

## floor 1 rate 1 horde 1 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.1
  first_timer: end 9.9 boss 0% evolved 33% L10 16
  thumb_sailor: end 6.8 boss 33% evolved 17% L10 17
  casual_cruiser: end 10.9 boss 59% evolved 49% L10 16
  steady_captain: end 13.5 boss 82% evolved 57% L10 15
  glass_cannon: end 12.7 boss 79% evolved 59% L10 16
  turtle: end 9.4 boss 51% evolved 40% L10 15
  treasure_hound: end 9.2 boss 52% evolved 36% L10 16
  build_planner: end 14.2 boss 82% evolved 71% L10 14
  sea_legend: end 20.7 boss 99% evolved 78% L10 14
  speedrunner: end 6.5 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/41/0.22  4:0.50/65/0.13  5:0.50/65/0.14  6:0.61/23/0.28  7:0.83/62/0.29  8:0.47/56/0.14  9:0.74/82/0.22  10:0.75/47/0.30  11:0.76/37/0.26  12:0.85/35/0.32
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.56 (target ––1) met
  pacing scheduled elite time to kill (s): 7.38 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 67 (target 45–90) met
  MISS First-Timer: median end minute: 9.87 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 6.84 (target 7–12) MISSED
  MISS minute 6: kills: 22.5 (target 30–140) MISSED

## Round 7

## floor 1 rate 1 horde 1 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 10.6
  first_timer: end 9.6 boss 0% evolved 31% L10 16
  thumb_sailor: end 5.9 boss 34% evolved 16% L10 17
  casual_cruiser: end 9.6 boss 58% evolved 53% L10 16
  steady_captain: end 13.7 boss 82% evolved 57% L10 15
  glass_cannon: end 12.8 boss 77% evolved 48% L10 15
  turtle: end 9.4 boss 52% evolved 44% L10 15
  treasure_hound: end 9.0 boss 45% evolved 34% L10 16
  build_planner: end 14.3 boss 81% evolved 66% L10 15
  sea_legend: end 20.2 boss 99% evolved 80% L10 14
  speedrunner: end 6.5 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.55/66/0.17  5:0.57/63/0.16  6:0.64/23/0.28  7:0.84/55/0.30  8:0.49/55/0.12  9:0.75/80/0.24  10:0.83/45/0.35  11:0.88/34/0.32  12:0.89/34/0.35
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 7.5 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 73 (target 45–90) met
  MISS First-Timer: median end minute: 9.59 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 5.88 (target 7–12) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED

## Round 8

## floor 1 rate 0.9 horde 1 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.5
  first_timer: end 10.5 boss 0% evolved 44% L10 16
  thumb_sailor: end 7.8 boss 33% evolved 18% L10 17
  casual_cruiser: end 11.6 boss 65% evolved 55% L10 16
  steady_captain: end 14.0 boss 87% evolved 62% L10 15
  glass_cannon: end 12.2 boss 73% evolved 50% L10 16
  turtle: end 9.4 boss 52% evolved 40% L10 15
  treasure_hound: end 9.1 boss 49% evolved 33% L10 17
  build_planner: end 14.1 boss 83% evolved 59% L10 14
  sea_legend: end 20.4 boss 98% evolved 80% L10 14
  speedrunner: end 6.2 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.19/39/0.00  1:0.25/53/0.01  2:0.32/42/0.26  3:0.30/41/0.19  4:0.51/67/0.14  5:0.48/64/0.14  6:0.51/23/0.27  7:0.79/61/0.24  8:0.46/56/0.08  9:0.72/87/0.21  10:0.69/49/0.32  11:0.68/37/0.22  12:0.87/34/0.30
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 25.5 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 7.25 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 67.75 (target 45–90) met
  MISS First-Timer: median end minute: 10.5 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.87 (target 0.6–0.85) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED
## floor 0.9 rate 1 horde 0.8 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 10.7
  first_timer: end 10.3 boss 0% evolved 45% L10 16
  thumb_sailor: end 4.9 boss 35% evolved 15% L10 17
  casual_cruiser: end 10.1 boss 58% evolved 52% L10 16
  steady_captain: end 13.8 boss 80% evolved 53% L10 15
  glass_cannon: end 12.6 boss 76% evolved 53% L10 16
  turtle: end 9.3 boss 50% evolved 44% L10 15
  treasure_hound: end 9.1 boss 48% evolved 36% L10 16
  build_planner: end 13.9 boss 80% evolved 67% L10 14
  sea_legend: end 20.6 boss 100% evolved 74% L10 13
  speedrunner: end 6.4 boss 86% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.17/35/0.00  1:0.23/50/0.03  2:0.32/43/0.22  3:0.27/38/0.22  4:0.50/63/0.14  5:0.54/61/0.16  6:0.70/23/0.28  7:0.85/53/0.32  8:0.58/52/0.15  9:0.78/77/0.24  10:0.82/47/0.35  11:0.88/34/0.30  12:0.88/36/0.30
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 26 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.62 (target ––1) met
  pacing scheduled elite time to kill (s): 7.13 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 76.13 (target 45–90) met
  MISS First-Timer: median end minute: 10.29 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 4.92 (target 7–12) MISSED
  MISS Build Planner: boss win rate: 0.8 (target 0.8–1) MISSED
  MISS minute 6: kills: 22.5 (target 30–140) MISSED
  MISS minute 7: alive share: 0.85 (target 0.3–0.85) MISSED
## floor 0.9 rate 0.9 horde 0.8 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.3
  first_timer: end 10.5 boss 0% evolved 36% L10 16
  thumb_sailor: end 8.2 boss 39% evolved 17% L10 17
  casual_cruiser: end 10.3 boss 61% evolved 54% L10 16
  steady_captain: end 13.9 boss 92% evolved 65% L10 15
  glass_cannon: end 12.8 boss 82% evolved 52% L10 15
  turtle: end 9.7 boss 51% evolved 42% L10 15
  treasure_hound: end 9.1 boss 47% evolved 39% L10 17
  build_planner: end 14.1 boss 82% evolved 68% L10 14
  sea_legend: end 20.8 boss 100% evolved 76% L10 13
  speedrunner: end 6.4 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.17/37/0.00  1:0.23/54/0.00  2:0.29/43/0.22  3:0.27/44/0.14  4:0.46/67/0.14  5:0.46/66/0.14  6:0.50/22/0.24  7:0.81/57/0.30  8:0.52/51/0.09  9:0.75/75/0.24  10:0.82/42/0.38  11:0.89/35/0.31  12:0.89/34/0.37
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 25.25 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.61 (target ––1) met
  pacing scheduled elite time to kill (s): 7.88 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 69.25 (target 45–90) met
  MISS First-Timer: median end minute: 10.45 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.92 (target 0.6–0.85) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED

## Round 9

## floor 1 rate 1 horde 1 boss 2700 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.0
  first_timer: end 9.3 boss 0% evolved 34% L10 16
  thumb_sailor: end 7.0 boss 33% evolved 14% L10 17
  casual_cruiser: end 10.6 boss 62% evolved 49% L10 16
  steady_captain: end 13.5 boss 83% evolved 57% L10 15
  glass_cannon: end 12.8 boss 75% evolved 55% L10 16
  turtle: end 9.8 boss 56% evolved 43% L10 15
  treasure_hound: end 9.1 boss 49% evolved 33% L10 17
  build_planner: end 14.2 boss 78% evolved 63% L10 14
  sea_legend: end 20.9 boss 100% evolved 79% L10 14
  speedrunner: end 6.5 boss 90% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/41/0.22  4:0.54/65/0.15  5:0.52/64/0.16  6:0.56/23/0.28  7:0.79/69/0.24  8:0.45/55/0.13  9:0.74/82/0.27  10:0.78/45/0.31  11:0.83/37/0.21  12:0.86/34/0.34
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.56 (target ––1) met
  pacing scheduled elite time to kill (s): 7.63 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 67.25 (target 45–90) met
  MISS First-Timer: median end minute: 9.26 (target 5.5–9) MISSED
  MISS Build Planner: boss win rate: 0.78 (target 0.8–1) MISSED
  MISS minute 6: kills: 23 (target 30–140) MISSED

## Round 10

## floor 1 rate 1 horde 1 boss 2500 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.2
  first_timer: end 9.3 boss 0% evolved 34% L10 16
  thumb_sailor: end 7.0 boss 33% evolved 13% L10 18
  casual_cruiser: end 11.1 boss 64% evolved 48% L10 15
  steady_captain: end 14.1 boss 85% evolved 60% L10 15
  glass_cannon: end 13.0 boss 77% evolved 58% L10 16
  turtle: end 10.4 boss 59% evolved 45% L10 15
  treasure_hound: end 9.0 boss 50% evolved 34% L10 16
  build_planner: end 14.1 boss 80% evolved 67% L10 15
  sea_legend: end 20.8 boss 100% evolved 76% L10 14
  speedrunner: end 6.4 boss 93% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/41/0.22  4:0.54/65/0.15  5:0.52/64/0.16  6:0.56/24/0.28  7:0.79/74/0.21  8:0.46/55/0.14  9:0.76/85/0.23  10:0.76/49/0.30  11:0.80/38/0.23  12:0.87/36/0.29
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.58 (target ––1) met
  pacing scheduled elite time to kill (s): 7.5 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 64.25 (target 45–90) met
  MISS First-Timer: median end minute: 9.26 (target 5.5–9) MISSED
  MISS Glass Cannon: median end minute: 13.04 (target 8–13) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED
## floor 1 rate 1 horde 1 boss 2350 eliteH 5.5 eliteK 12 crab 4.5: missed 3 of 76, weighted end 11.2
  first_timer: end 9.3 boss 0% evolved 34% L10 16
  thumb_sailor: end 7.0 boss 33% evolved 14% L10 17
  casual_cruiser: end 11.0 boss 66% evolved 52% L10 16
  steady_captain: end 14.0 boss 86% evolved 63% L10 15
  glass_cannon: end 12.7 boss 79% evolved 58% L10 16
  turtle: end 11.1 boss 60% evolved 42% L10 15
  treasure_hound: end 9.7 boss 52% evolved 34% L10 16
  build_planner: end 14.4 boss 83% evolved 67% L10 14
  sea_legend: end 20.2 boss 100% evolved 76% L10 14
  speedrunner: end 6.3 boss 94% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/41/0.22  4:0.54/65/0.15  5:0.52/64/0.16  6:0.55/24/0.28  7:0.79/75/0.21  8:0.43/54/0.13  9:0.74/84/0.26  10:0.75/46/0.33  11:0.84/35/0.27  12:0.89/34/0.40
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.58 (target ––1) met
  pacing scheduled elite time to kill (s): 8 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 62 (target 45–90) met
  MISS First-Timer: median end minute: 9.26 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS minute 6: kills: 24 (target 30–140) MISSED

# After the second review pass (rounds 11-14)

The second pass asked for the crowd itself (not only the floor) to show the sketch's 3:45 peak over the valley. `SimRunResult` now records the crowd in 15 s bins. Round 11: the valley's spawn rates lowered to 0.65-0.9 (crowd at 3:45-4:00: 44 against about 34 through 4:30-6:15; before: 46 against 38-40); rounds 12-14: a softer climb into 7:00 (6:35 and 7:00 keyframes 0.48 and 0.54, rates 0.8 and 1.0) and the Barnacle King at 2500, 2650 and 2800 HP (2800 taken: the Steady Captain's boss wins back to 0.84, the Thumb Sailor 7.3 min).

Crowd bins (baseline, 60 runs, median alive every 15 s from 0:00) after round 13:

```
0.00 8.1 n60
0.25 16.8 n60
0.50 20.7 n60
0.75 19.0 n60
1.00 14.0 n60
1.25 24.7 n60
1.50 22.3 n60
1.75 19.6 n60
2.00 25.4 n60
2.25 33.3 n60
2.50 31.1 n60
2.75 22.0 n60
3.00 20.0 n60
3.25 18.0 n60
3.50 25.3 n59
3.75 40.3 n59
4.00 43.7 n59
4.25 35.4 n59
4.50 35.0 n59
4.75 36.7 n59
5.00 34.3 n59
5.25 32.6 n59
5.50 34.3 n59
5.75 33.5 n59
6.00 30.2 n59
6.25 32.0 n59
6.50 38.8 n58
6.75 46.4 n58
7.00 59.6 n58
7.25 60.8 n58
7.50 64.9 n56
7.75 64.6 n55
8.00 52.5 n54
8.25 44.2 n54
8.50 35.2 n51
8.75 34.8 n51
9.00 45.8 n51
9.25 62.0 n51
9.50 68.7 n50
9.75 66.9 n50
```

## Round 11

## floor 1 rate 1 horde 1 boss 2500 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 11.0
  first_timer: end 9.6 boss 0% evolved 28% L10 16
  thumb_sailor: end 6.9 boss 38% evolved 13% L10 17
  casual_cruiser: end 10.2 boss 59% evolved 50% L10 16
  steady_captain: end 13.8 boss 86% evolved 60% L10 15
  glass_cannon: end 12.9 boss 82% evolved 55% L10 15
  turtle: end 9.6 boss 54% evolved 38% L10 15
  treasure_hound: end 9.1 boss 52% evolved 31% L10 16
  build_planner: end 14.8 boss 89% evolved 70% L10 14
  sea_legend: end 20.0 boss 100% evolved 74% L10 14
  speedrunner: end 6.3 boss 90% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.48/67/0.14  5:0.41/63/0.14  6:0.45/22/0.18  7:0.76/70/0.27  8:0.46/54/0.10  9:0.74/82/0.24  10:0.79/45/0.30  11:0.84/36/0.31  12:0.88/35/0.29
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.56 (target ––1) met
  pacing scheduled elite time to kill (s): 7.75 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 63.25 (target 45–90) met
  MISS First-Timer: median end minute: 9.64 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 6.89 (target 7–12) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS minute 8: alive share: 0.46 (target 0.47–0.8) MISSED

## Round 12

## floor 1 rate 1 horde 1 boss 2500 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 11.1
  first_timer: end 9.3 boss 0% evolved 31% L10 16
  thumb_sailor: end 7.0 boss 38% evolved 17% L10 17
  casual_cruiser: end 10.7 boss 62% evolved 48% L10 16
  steady_captain: end 13.8 boss 86% evolved 64% L10 15
  glass_cannon: end 12.9 boss 82% evolved 55% L10 15
  turtle: end 10.5 boss 59% evolved 40% L10 15
  treasure_hound: end 9.0 boss 48% evolved 32% L10 16
  build_planner: end 14.4 boss 89% evolved 63% L10 14
  sea_legend: end 20.3 boss 100% evolved 79% L10 14
  speedrunner: end 6.3 boss 92% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.48/67/0.14  5:0.41/63/0.14  6:0.45/22/0.21  7:0.74/75/0.22  8:0.45/55/0.09  9:0.74/80/0.26  10:0.79/46/0.33  11:0.86/37/0.30  12:0.89/33/0.33
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.52 (target ––1) met
  pacing scheduled elite time to kill (s): 7.81 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 62.88 (target 45–90) met
  MISS First-Timer: median end minute: 9.29 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 6.99 (target 7–12) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS minute 6: kills: 22 (target 30–140) MISSED
  MISS minute 8: alive share: 0.45 (target 0.47–0.8) MISSED
## floor 1 rate 1 horde 1 boss 2650 eliteH 5.5 eliteK 12 crab 4.5: missed 4 of 76, weighted end 11.1
  first_timer: end 9.3 boss 0% evolved 31% L10 16
  thumb_sailor: end 7.0 boss 37% evolved 13% L10 17
  casual_cruiser: end 10.5 boss 60% evolved 50% L10 16
  steady_captain: end 14.0 boss 84% evolved 60% L10 15
  glass_cannon: end 12.7 boss 80% evolved 56% L10 15
  turtle: end 10.7 boss 57% evolved 41% L10 15
  treasure_hound: end 8.9 boss 47% evolved 32% L10 16
  build_planner: end 14.2 boss 87% evolved 59% L10 14
  sea_legend: end 20.1 boss 100% evolved 78% L10 14
  speedrunner: end 6.4 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.48/67/0.14  5:0.41/63/0.14  6:0.45/21/0.21  7:0.75/71/0.24  8:0.45/55/0.13  9:0.73/84/0.24  10:0.79/45/0.33  11:0.87/37/0.32  12:0.90/31/0.36
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.53 (target ––1) met
  pacing scheduled elite time to kill (s): 7.38 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 66 (target 45–90) met
  MISS First-Timer: median end minute: 9.29 (target 5.5–9) MISSED
  MISS Thumb Sailor: median end minute: 6.99 (target 7–12) MISSED
  MISS minute 6: kills: 21 (target 30–140) MISSED
  MISS minute 8: alive share: 0.45 (target 0.47–0.8) MISSED

## Round 13

## floor 1 rate 1 horde 1 boss 2650 eliteH 5.5 eliteK 12 crab 4.5: missed 5 of 76, weighted end 11.1
  first_timer: end 9.5 boss 0% evolved 32% L10 16
  thumb_sailor: end 7.3 boss 36% evolved 16% L10 17
  casual_cruiser: end 10.5 boss 66% evolved 52% L10 16
  steady_captain: end 13.6 boss 86% evolved 59% L10 15
  glass_cannon: end 13.1 boss 81% evolved 52% L10 15
  turtle: end 9.7 boss 54% evolved 42% L10 15
  treasure_hound: end 8.9 boss 46% evolved 31% L10 16
  build_planner: end 14.6 boss 84% evolved 70% L10 14
  sea_legend: end 21.0 boss 99% evolved 73% L10 14
  speedrunner: end 6.4 boss 91% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.48/67/0.14  5:0.41/63/0.14  6:0.44/20/0.18  7:0.74/67/0.24  8:0.45/52/0.14  9:0.74/80/0.24  10:0.80/43/0.38  11:0.85/35/0.30  12:0.88/34/0.32
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.53 (target ––1) met
  pacing scheduled elite time to kill (s): 8 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 66.88 (target 45–90) met
  MISS First-Timer: median end minute: 9.45 (target 5.5–9) MISSED
  MISS Steady Captain: boss win rate: 0.86 (target 0.6–0.85) MISSED
  MISS Glass Cannon: median end minute: 13.12 (target 8–13) MISSED
  MISS minute 6: kills: 20 (target 30–140) MISSED
  MISS minute 8: alive share: 0.45 (target 0.47–0.8) MISSED

## Round 14

## floor 1 rate 1 horde 1 boss 2800 eliteH 5.5 eliteK 12 crab 4.5: missed 2 of 76, weighted end 11.0
  first_timer: end 9.5 boss 0% evolved 32% L10 16
  thumb_sailor: end 7.3 boss 36% evolved 13% L10 17
  casual_cruiser: end 10.7 boss 64% evolved 51% L10 16
  steady_captain: end 13.4 boss 84% evolved 56% L10 15
  glass_cannon: end 12.8 boss 79% evolved 50% L10 15
  turtle: end 9.4 boss 50% evolved 40% L10 15
  treasure_hound: end 8.9 boss 45% evolved 29% L10 16
  build_planner: end 14.2 boss 82% evolved 66% L10 14
  sea_legend: end 20.4 boss 99% evolved 78% L10 14
  speedrunner: end 6.5 boss 90% evolved 0% L10 NaN
  minutes (alive share / kills / damage taken): 0:0.20/37/0.05  1:0.26/52/0.07  2:0.36/44/0.27  3:0.31/40/0.22  4:0.48/67/0.14  5:0.41/63/0.14  6:0.44/20/0.18  7:0.76/57/0.24  8:0.47/52/0.14  9:0.75/77/0.23  10:0.83/45/0.33  11:0.86/35/0.30  12:0.89/34/0.29
  pacing seconds between level-ups in minute 1: 15 (target 12–18) met
  pacing seconds between level-ups over 0:00–10:00: 40 (target 37–55) met
  pacing miniboss time to kill (s): 24.38 (target 15–30) met
  pacing fodder time to kill (s, first hit to sunk): 0.55 (target ––1) met
  pacing scheduled elite time to kill (s): 7.69 (target 4–8) met
  pacing Barnacle King fight (s, summon to sunk): 68.38 (target 45–90) met
  MISS First-Timer: median end minute: 9.45 (target 5.5–9) MISSED
  MISS minute 6: kills: 20 (target 30–140) MISSED
