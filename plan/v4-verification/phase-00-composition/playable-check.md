# M0 playable check

## Automated (`tools/playable-check-m0.ps1`, final run 2026-09-26)

Driven through `unity command` in the open Editor from `V4Boot`. Prefs are snapshotted and restored, the Captain is pinned to Rook, the Game view is fixed at 1080p, and the ship is invulnerable until the forced death.

| # | Step | Result | Screenshot |
|---|---|---|---|
| 1 | `V4Boot` loads `V4MainMenu` (V4 menu controller present) | OK | `playable-check/01-menu.png` |
| 2 | Press **Set Sail** (real button `onClick`) → `V4SaltwindRun` reaches Playing, driven by `V4Game`, console attached | OK | `02-run-start.png` |
| 3 | Tuning Console opens and closes | OK | `03-console.png` |
| 4 | Boss summon through `POIManager` → boss agent spawns | OK | `04-boss.png` |
| 5 | Lethal hit on boss → defeated, objective secured, run continues | OK | `05-territory-claimed.png` |
| 6 | Player death → run ends, settlement applied once (25 Marks from boss victory) | OK | `06-results.png` |
| 7 | **Retry voyage** on the results screen (rebound button) → scene reload, new random seed, new RunId, Playing | OK | `07-after-retry.png` |
| 8 | Console restart on seed 101 → Playing with seed 101 | OK | `08-seed-101.png` |

Console errors during the final run: **0**. Step log: `playable-check/steps.log`.

In the final run a level-up choice happened to be open during shots 4–5. The step results are read from game state, not from the image.

## Manual playtest (you)

Status: **Passed** (2026-09-26).

What to try:
1. Open `Assets/_Game/Scenes/V4/V4Boot.unity` and press Play.
2. Pick a Captain and Set Sail. The run should feel exactly like V3: same map for the same seed, same enemies and timings, same HUD and level-ups.
3. Play to the Beacon and boss if you can, or summon the boss with B. Kill it, keep playing, then die.
4. On the results screen, press **Retry voyage** (new run, still V4) and **Main menu** (should go to the V4 menu, not the V3 one).
5. Press **F1** or the ⚙ button: the console should stay open while enemies spawn. Try Restart on seed, Time scale, God mode, Jump to 240, and Force death.
6. Optional: play `Assets/_Game/Scenes/v3/V3Boot.unity` too. V3 is unchanged.

Notes from your playtest:

- The game was working; survived to about 8–9 minutes.
- Feedback: summon the boss at 240 s, not 120 s. The autopilot died early because it got stuck on islands and couldn't dodge. Applied: new `player-v1` pilot (see CHECKLIST deviation 3), and all traces re-recorded at 240 s.
- Save data wiped by the V3 tests (First Conquest, home seed): leave as is.
