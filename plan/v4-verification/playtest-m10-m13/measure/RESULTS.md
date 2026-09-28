# Playtest round 1 — time-to-death measurement

Human-like autopilot (`HumanPilot`, DevTools, pattern `human-v1`) in the Windows trace player, Rook, seeds 101/202/303,
no invulnerability, runs end at death. The pilot explores, chases landmarks and chests, summons the boss, kites packs,
dodges shots, blast circles, charges and the boss's telegraphs, and answers level-up cards like a player
(`BestCard`). Every hit of 2+ damage is logged ("hurt" lines) with what was near the ship.

Build profiles:
- **Average** — takes the best card (new weapons first, then upgrades, items, Logbooks); buys chests.
- **Strong** — as Average, plus the six always-unlocked weapons at 1:00.
- **Weak** — keeps its one weapon (upgrades, items, Logbooks only); skips chests.

## Result (final code: overkill-capped power index, offense gain 0.8)

| Build | Mode | Death (median) | Runs (min) | Kills/min | Mean Pressure | Peak |
|---|---|---|---|---|---|---|
| Average | Observe (V3 difficulty) | 8.0 | 8.0, 5.6, 10.3 | 34 | — | — |
| Average | Adaptive | 6.3 | 6.3, 7.5, 5.4 | 45 | 1.12 | 1.33 |
| Strong | Observe | 7.3 | 6.6, 8.3, 7.3 | 36 | — | — |
| Strong | Adaptive | 7.4 | 7.4, 7.7, 6.0 | 49 | 1.25 | 1.35 |
| Weak | Observe | 4.8 | 8.8, 4.8, 2.6 | 22 | — | — |
| Weak | Adaptive | 4.1 | 8.3, 3.9, 4.1 | 21 | 1.01 | 1.04 |

(`observe3/` = director observing; `adaptive2/` = shipping default; `calibrate/` = the capped-index calibration runs.)

What it shows:
- **Pressure now separates builds.** Strong builds reach the top of the band (1.35 → threat ×1.75, elites 12 %,
  toughness ×1.25) by minute 3–4 and stay there; average builds climb to ~1.3 once they upgrade and evolve; weak builds
  sit on the floor (1.0 after 30 s, as asked). Strong/average adaptive runs face ~35 % more kills per minute.
- **Time-to-death is noisy** with three seeds and a bot: the pilot dies to the same threats in every build (bomb
  blasts, the Barnacle King, Reef Crab rams), so its survival is limited by its own dodging more than by its build.
  Treat the death times as a sanity check, not as balance numbers; your playtest is the real test.

## Why "weak vs strong felt the same" (the bug you reported)

1. The power index summed raw DPS, so one heavily upgraded weapon (plus crit items) scored like a full six-gun
   broadside — a one-weapon run could read as Pressure 1.35 (your "survived 1.35 with one weapon"). **Fix:** each hit
   counts at most 1.5 × a typical enemy's current HP (`overkillCap`, `referenceEnemyHp` 16 × the enemy HP curve):
   damage past a kill is wasted in a horde, so breadth counts. A one-weapon run now reads 0.2–0.4 of on-track.
2. Offense barely moved Pressure (gain 0.25: a build 30 % ahead added +0.075). **Fix:** gain 0.8.
3. Expected offense grew with the enemy HP curve, which grows far slower than a real build. **Fix:** an on-track
   curve measured from average runs (×4 by 1:00, ×8 by 3:00, ×10 by 6:00, ×24 by 10:00).

## Pilot notes (how it was made to play like a person)

- It used to lap the map edge: `IslandObstacles.ApplyAvoidanceSteering` returns the current velocity when no island
  is ahead, so the pilot now uses it only when it actually steered.
- Hit logs showed it dying to 20–47-damage blasts it sat in, Reef Crab rams while it pushed toward a goal, and boss
  attacks. It now drops its goal when in danger, leaves blast circles straight ahead when centred, sidesteps charges,
  gives big enemies (elites, 4× average HP) a wider berth, and reads the boss's marked circle and charge.
