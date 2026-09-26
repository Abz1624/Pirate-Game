# M2 playable check

## Automated (`tools/playable-check-m2.ps1`, after the review fixes)

| # | Step | Result | Screenshot |
|---|---|---|---|
| 1 | Open `V4ModuleLab`, wait 6 s | OK: lab mode, 0 enemies; encounter spawner, POIs and hold-to-interact are off | |
| 2 | Spawn all 21 non-boss enemies in a ring | OK: 21 alive; the 8 specialists use their template models (R-13) | `playable-check/01-all-enemies.png` |
| 3 | Module Lab → elite_hammerhead row → Spawn | OK: one elite hammerhead (Rich affix, base hammerhead profile) | |
| 4 | Open the console's Module Lab section, select needle_sloop | OK: the list shows archetypes, "(variant)" and "(elite, base profile)" rows | `02-lab-inspector.png` |
| 5 | Live edit: `variant.ram_raider` baseHP 22 → 60 | OK: the runtime definition changes; new spawns use it | |
| 6 | Retry | OK: `V4ModuleLab` reloads in lab mode | |

Console: 6 errors, all Pipeline "main thread operation timed out" while Play Mode started (tooling, no game errors). The trace comparison is in `trace-comparison.txt`.

## Manual playtest (you): later, together with M1 and M3

Status: **Pending playtest**.

**Run (`Assets/_Game/Scenes/V4/V4Boot.unity`)**
1. Play a run with no edits. It should feel like M0/V3, except the first 90 seconds are a little quieter: band 0 is now 0.5 enemies/s (R-01; V3 was 0.6).
2. Press F1 → **Config** (or search a field name). Early in a run, set **band_0 ratePerSecond** from 0.5 to 1.0; the early game gets busier right away. After about 3:40 the enemy cap is usually full, so later band edits change little (R-76).
3. Change a profile value, for example storm `outsideDamagePerSecond` or `bossAmbientRateMultiplier`, and check it takes effect.
4. Save a preset, then **Restart with it**: same seed, your values from the start.
5. Enemy stats: in **Config**, find `enemy.shark` and raise `stats.moveSpeed` (or `stats.baseHP`). Newly spawned sharks use the new value; sharks already alive keep theirs. (In runs, some combat values come from the spawner's override instead of the archetype, as in V3; the lab below shows archetype combat edits directly.)

**Module Lab (`Assets/_Game/Scenes/V4/V4ModuleLab.unity`)**
6. Nothing spawns by itself and the ship cannot die. Chests, the Beacon and the B key are off.
7. F1 → **Module Lab**: click a name to see its resolved values and which layer set each (Archetype, Variant, Default); press **Spawn** to place it ahead of the ship (the slider sets the distance); **Clear all** removes everything.
8. Change `enemy.hammerhead` → `combat.chargeWindUp` in Config, spawn a new hammerhead and watch its charge.
9. Kills still give XP, so the level-up screen appears in the lab too. Pick a card or skip.
10. Retry (F1 → Run) restarts the lab.

**R-13 (decided):** the 8 specialists now use their template enemy's model as a stand-in (their own models are future content, see the register). Some ship models show magenta patches from empty material slots in the V3 prefabs (R-80).

Notes from your playtest:

-
