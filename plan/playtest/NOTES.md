# Playtest branch (2 Oct 2026)

Branch `playtest`, from `v5-live-game` 45621fe7. Quick changes for a playtest, modelled on Survivor.io chapter 1
(your sheet `survivorio_chapter1_tracker.xlsx`, the playthrough video and the research notes). Not a V5 phase: no long
test suites; one independent review and a short scripted play.

## What changed

| # | Ask | What it does now | Where |
|---|---|---|---|
| a | Health bars only on bosses | Bars on the boss and the minibosses (Reef Crabs); none on ordinary enemies or elites | `EnemyViewPresenter.AttachHealthBar` |
| b | Whirlpool for the boss POI | The Treasury Beacon marker is the water whirlpool (`Prefabs/VFX/whirlpool.prefab`, particles, works on WebGL) | `DemoJuiceSettings.bossBeaconPrefab` |
| c | Survivor.io spawn pattern | 15-minute stage, 10 bands, beats below; enemy types follow Survivor.io's order (melee, ranged, fast, armored, heavy ranged, armored flood) | `SaltwindTimeline.asset`, spawners `band_5..9` |
| d | Fewer health drops | One small heal (25 hull) about every 90-150 s (Survivor.io), wait halved while the hull is under 35%; the per-kill 12% chance is gone; the Repair powerup no longer drops | `Pickups_Saltwind`, `Powerups_Saltwind` |
| e | Same base DPS for every weapon | Every weapon deals 8.96 damage per second to one target at level 1 (the median), counting projectiles, pellets, bursts, ticks, strikes and cycle time | `Data/Weapons/*.asset` |
| f | Kraken +50%, cannon crab | The Kraken elite is 1.5× its size; the Reef Crab keeps 10-14 m away and lobs one big, slow (8 m/s), dodgeable cannonball every 2.5 s after a 0.6 s aim; it still slams if you sail into it | `saltwind_elite_kraken`, `EnemyReefCrabCombat` |
| g | Boss at 14:00 | The boss rises from its whirlpool by the ship at 14:00; the beacon cannot call it earlier (it says how long is left); beacon direction from 12:00, exact from 13:00 | `DemoJuiceSettings.autoBossSeconds`, `StageContentModule` |
| h | Dynamic difficulty by 30 s band | Every 30 s: killed 85% or more of that band's spawns: full rate and 6% of ambient spawns become elites next band; 30% or less: no elites and 0.6× spawns (and floor) next band; between: the rate rises linearly. Replaces the Pressure-driven rate and elite levers | `DifficultyDirector`, `SaltwindDirector` |
| i | Survivor.io level curve | XP table fitted in the simulator to level = 5 × minutes^0.79 (Survivor.io run: level 39 at 13:20): about 5 at 1:00, 18 at 5:00, 31 at 10:00, 40 at 14:00 | `Progression_Saltwind.xpToNextTable` |

## The 15 minutes

| Time | Survivor.io | Rogue Pirates now |
|---|---|---|
| 0:00 | basic melee | Swordfish, Dinghies |
| 1:00 | ranged | + Cannonboats |
| 1:45-1:50 | fast melee ("dogs") | ring of 12 Swordfish; Hammerheads, Needle Sloops |
| 2:30 | | Reef Crab (cannon crab) |
| 3:00 | armored elite | Elite Hammerhead |
| 4:00 | horde from all sides | ring of 24 Dinghies; Skiffs, Sharks, Ram Raiders, Bomb Boats |
| 5:00 | boss | second Reef Crab (1.3× HP; enemies keep coming) |
| 5:40 | armored enemies take over | Skiffs, Rammer Rafts, Gunner Cogs, Bomb Ketches |
| 6:30 | swarm | swarm pulse + a line of 20 Swordfish |
| 8:00 | horde + miniboss | pack of 20 Skiffs, Elite Hammerhead (enemies keep coming); Barges, Mantas, Net Cutters |
| 9:00 | purple ranged | heavy Gunner Cog share |
| 10:00 | boss | the big Kraken (elite, 1.5× size) |
| 11:00-11:40 | ring, diagonal sweeps, miniboss | ring of 22 Sharks, line of 24 Swordfish, third Reef Crab (1.8× HP; enemies keep coming), swarm pulse |
| 12:00 | armored flood | pack of 24 Skiffs; Barges, Kraken Spawn, Shield Frigates |
| 13:00 | ring | ring of 26 Hammerheads |
| 14:00 | (15:00 final boss) | Barnacle King from the whirlpool |
| 15:00 | | overtime (Drowned Pursuit); storm at 17:00 |

## Weapon base damage (level 1, single target, 8.96 DPS each)

deck_cannons 9.41, swivel_guns 6.35, long_nine 21.50, harpoon_gun 10.75, chainshot 13.44, broadside_battery 7.47,
flying_cutlasses 10.24, anchor_flail 32.25, lighthouse_lantern 53.75, flamethrower 3.14 (ticks every 0.16 s), powder_kegs
27.37, lightning_mast 16.13, cursed_doubloons 17.92, ramming_prow 8.96, siren_bell 26.88, mortar 16.13, maelstrom_charge
12.11, monkey_gunners 4.13, parrot_squadron 1.63.

Assumptions (tell me if you want them different):
- Ramming Prow: counted at 30% of the time boosting (the balance model's figure).
- Summons (Monkey Gunners, Parrots): every strike over their life counted; if they spend time travelling, they come out a little lower.
- Flamethrower: its burn is not scaled by base damage, so it ticks half as often (half the burn stacks) and its direct damage is half the target.
- Multi-target value (pierce, chains, areas, fans across a crowd) is not counted, as you described; upgrades still grow each weapon differently.

## What to check when you play

1. Do ordinary enemies look right without bars; is the boss/crab bar enough?
2. The whirlpool beacon: size and look on the water.
3. Pace: does the 0:00-5:00 ramp feel like Survivor.io; are the 4:00, 8:00, 11:00, 12:00 and 13:00 beats readable?
4. Health: with heals about every 2 minutes, is the run too punishing or about right?
5. Weapons: do any feel clearly stronger or weaker now?
6. The Kraken's size; the crab's cannonballs (readable, dodgeable, too easy or too hard?).
7. Boss at 14:00: is the wait too long; is the beacon message clear?
8. Dynamic difficulty: kill everything and see if elites appear; play passively and see if it eases. The Tuning Console
   (F1 in the Editor) shows "Kill band" under Difficulty.
9. Levels: about 5 by 1:00, 18 by 5:00, 31 by 10:00. Too many level-up screens?

## Review (2 Oct, independent V5 reviewer + a search for 10-minute assumptions)

No blockers. Fixed:
- Minibosses at 5:00, 8:00 and 11:30 no longer stop the waves (an arena held back the 8:00 and 11:40-12:00 hordes, which
  were then skipped); the 2:30 crab, 3:00 Hammerhead and 10:00 Kraken still clear the sea for a fight. Crabs 2 and 3
  have less HP (1.3× and 1.8× the first, not 1.6× and 2.4×): the crab keeps its distance now, hard for short-range builds.
- Rum: the 15-minute stage would pay qualifying minutes for 15 minutes (+50%); capped at 10 minutes, as before.
- The day and night cycle followed a fixed 10:00 (the sun stopped from 10:00 on); it follows the stage length now.
- The console's and the dev key's boss summon ignore the 14:00 gate; the kill band resets on a clock jump; the crab's
  first shot waits a moment after it comes in range.

Left as they are (your call):
- The flamethrower: its direct damage is half the target (4.5/s) and its burn makes up the rest (about 4-5/s with the
  slower ticks; estimated, not measured).
- A heal that comes due while every pickup slot is full is lost (rare).
- The simulator does not model the kill band or the timed heals, so the level curve is fitted on the old rules: check
  levels at 1:00, 5:00, 10:00 and 14:00 when you play.
- V5 tests that assert the old rules now fail (Pressure levers on, bars on every enemy, the perf test waiting for bars);
  not fixed on this branch.

Questions for you:
1. The beacon: the boss now always rises by the ship at 14:00, so the beacon (shown from 12:00) cannot do anything.
   Keep it as a "boss comes here" marker, let it call the boss early from 13:00, or remove it?
2. Elites (the 3:00 Hammerhead, the 10:00 Kraken) have no health bar now. Should they?
3. Rum for minutes: capped at 10 (as before), or pay for all 15?
