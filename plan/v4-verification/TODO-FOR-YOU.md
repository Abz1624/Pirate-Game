# Your TODOs (updated 29 Sep, M15 committed, M16 started)

## M16 — started (29 Sep)

Your M15 playtest feedback becomes M16. Nothing to try yet; I will ask for a playtest when it is built.
- **Save safety:** your Hank unlock was lost between two playtests (you re-bought him). M15 did not change the save code
  and I could not find the cause in what was left to inspect, so M16 makes the tools and tests unable to overwrite your
  save and adds a check that reports (with the code responsible) any save write that would drop something you own.
- **Edge wall:** your decision — steady damage while outside the red edge wall at any time, with a warning toast
  (the v3-parity preset keeps V3's open edge).
- **Content ids into config:** the 17 Foundation files that still key V3's data tables by content id move onto the V4
  configs, with no gameplay change (checked by tests and one trace set).

## Still open

- Nothing else. (Gold curve checked: 965 gold paid ~21 Rum of your 37 total — the rest came from minutes, landmarks,
  elites and the boss. You chose to keep showing the total only.)

## Done

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
