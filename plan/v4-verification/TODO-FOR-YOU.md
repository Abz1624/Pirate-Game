# Your TODOs (updated 29 Sep, M16 committed)

## Next

- Nothing waiting on you. The V4 plan (M0–M16) is complete and committed. Options from here: open a pull request to
  merge `v4-modular-architecture` into `main`, or pick the next gameplay work.

## Still open

- Nothing else. (Gold curve checked: 965 gold paid ~21 Rum of your 37 total — the rest came from minutes, landmarks,
  elites and the boss. You chose to keep showing the total only.)

## Done

- **M16 playtest follow-ups** (29 Sep): the edge wall hurts (checked, god mode blocks it); your save stays exactly as
  left between Play sessions (the Editor trace tool that rolled it back is fixed; no write can drop an unlock); a normal
  run plays as before with the last content-id tables moved onto config (trace set identical to M15).
- **M15 V3 retirement** (29 Sep): a normal run and the Unity menus checked; V3's code, scenes, tests and data are gone
  and the shared code lives in `V4/Foundation`. The trace set drifted from M14 by float-sized amounts (the parity
  preset's V3 barrel markers are gone); details in `phase-15-retirement/`.
- **M14 switch-over** (29 Sep, `ba0aead6`): menu locks, a normal run, level-up cards, no feel difference, dev tools.
  Round-1 fixes committed with it: the ship's foam wake, the Powder Cache on one press of E, Sparky at 140 Rum only.
- **M10–M13 playtest rounds 1–3** (29 Sep, `d676b442`, `c1a0ab20`, `122f4ac8`): weak and strong builds, Pressure, the
  stronger levers, Rum at death with the flattening gold curve, barks and the tutorial — all confirmed; Chippy only.
- **M10–M13 built** (28 Sep, run unattended at your request) and **M0–M9 passed** earlier.

## Not measured well (known limits)

- Time-to-death with a bot is noisy (the bot dies to bomb blasts and the Barnacle King in every build), so it does
  not prove that strong builds die sooner; Pressure and spawn rates do separate clearly. Your playtests are the test.
