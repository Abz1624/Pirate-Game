# V5 M4b: new weapons, batch 2 (phase plan and results)

Plan: `V5_PLAN.md` "#### M4b". Start commit: `eaf71a9d` (M4). Built after the owner's "proceed till M8" (1 Oct), on the
M4 decisions (D31) and D32 in `plan/v5/DECISIONS-WHILE-AWAY.md`.

## Mechanics (each opt-in; `V5WeaponGoldenTests` proves today's weapons unchanged)

| Item | What | Where |
|---|---|---|
| B1 Pull | `IEnemyDamageSink.Pull`: drags an enemy up to a distance toward a point, never past it; the knockback rules (at most 3 m, elites half, bosses not moved) | `EnemySimulation.Tick.cs` (`Pull`), `EnemyWorldContracts`, `WeaponContracts` |
| B2 Whirlpool | `predictedArea.whirlpoolSeconds` > 0: the shell's impact opens a whirlpool (a timed ring) that drags enemies in and hurts them every `whirlpoolTickSeconds`, then implodes (`implodeDamageMultiplier`) | `WeaponCombat.Whirlpool`, `WeaponManager.TickWhirlpools`, `PredictedAreaBehavior` |
| B3 Beam | `orbit.beamSegments` > 0: each blade is a line of hit spheres from the ship to the orbit radius (a sweeping beam), hit once per target per interval | `OrbitBehavior.ComputeHits` |
| B4 Summons | `summon.perQuantity` (one per point of quantity, fanned to both sides), `retarget` (a summon whose target sinks takes the nearest enemy and lives on), `shotSpeed` (it fires shots from range instead of striking alongside: the turret); summons drawn with their weapon's own model, pooled and prewarmed | `SummonBehavior`, `WeaponVfxPresenter.SummonSpawned`, `PrewarmModels` |
| B5 Kill gold | `goldPerKill` and `goldPerKillRunCap`: a kill by the weapon drops extra gold, at most the cap a run (anti-farming rule) | `WeaponKillGold`, `KillFanout.WeaponKillGold`, `V4Game` |

## Weapons, evolutions, items (D32)

| Weapon (Rum) | Shape | Evolution (item) |
|---|---|---|
| Parrot Squadron (170) | Summon: 2 parrots (more with quantity) dive at the nearest enemies, retargeting, 6 s | Parrot Armada: +2 parrots, ×1.2 (Captain's Perch, +9% damage) |
| Monkey Gunners (180) | Summon: a floating barrel turret astern that fires at anything within 10 m for 8 s | Monkey Battery: a second turret, called down more often, ×1.2 (Banana Crate, +9% attack speed) |
| Maelstrom Charge (190) | Predicted area: a lobbed charge opens a 3 s whirlpool (drags 3 m/s, ticks every 0.5 s), then implodes | Abyssal Vortex: ×1.3 size and damage (Storm Conch, +12% size) |
| Lighthouse Lantern (200) | Orbit beam: one 9 m beam sweeping at 60°/s, each enemy hit once per 0.5 s | Beacon of the Deep: a second, longer beam (Fresnel Lens, +12% size) |
| Cursed Doubloons (220) | Chain strike: coins jump between 5 enemies (more with quantity); each kill +1 gold, at most 40 a run | Pirate King's Hoard: 2 more jumps, ×1.25 (Cursed Chest Key, +9% damage) |

Damage and growth by the M3 weapon model (`ratios.txt`). The model and the runtime difficulty director now share
`WeaponStatResolver.HitsPerFiring`: a summon's strikes over half its life, a whirlpool's whole ticks and implosion, and a
per-target orbit's sweep (an enemy is hit only as the blade or beam passes it: this also retuned M4's Anchor Flail). Upgrade
cards: damage and cooldown on all five, plus quantity for the parrots, size for the beam and the doubloons; no size on the
summons (they do not grow), no quantity on the turret, beam or coins (their second copy is the evolution). Summons are
capped (`summon.maxAlive`: 12 parrots, 3 turrets).
Placeholder art: a cannonball for the parrots and the beam, the cannon barrel for the turret, the lightning strike for the
coins; other weapons' icons. Parity preset: the five out of the offers, their items out of the chest pool.

The content was written by a one-off Editor tool, kept as `tools/V5WeaponsBatch2.cs.txt`.

## Review (1 Oct, no blockers, 7 majors, 8 minors) and what was done

| # | Finding | Done |
|---|---|---|
| 1 | The turret could lock onto an enemy out of its reach | A turret takes the nearest enemy within its range when its target is out of it; test |
| 2 | The runtime director under-read the new weapons | `HitsPerFiring` moved to Runtime, used by the director and the model; test |
| 3 | Size cards did nothing on the summons | Removed; re-tuned |
| 4 | The model overstated slow sweeps (Lantern ~10×, Flail ~4×) | Sweep duty in `HitsPerFiring`; both re-tuned |
| 5 | No cap on live summons | `summon.maxAlive`, pools prewarmed to it; test |
| 6 | Gate evidence missing | Full suites and the weapon check run once on the final data (below) |
| 7 | Whirlpool ticks depended on the frame rate | Whole ticks counted, a long frame deals every tick it covered; test at 30 and 60 fps |
| 8 | The beam's pool was prewarmed for the Flail only | One pool per model sized to the most any weapon draws (evolutions included) |
| 9 | The default skiff became the parrot placeholder | Summons with the new switches are skipped |
| 10 | A string per summon spawn | Pool keys cached per weapon |
| 11 | Two evolution texts promised effects that do not exist | Reworded |
| 12 | Cursed Chest Key's stat did nothing for the coins | +9% damage |
| 13 | Plan and cards disagreed | This section |
| 14 | Kill gold: a contact kill after a coin hit still pays; no wiring test; the simulator leaves it out | Recorded (the cap bounds it at 40 a run) |
| 15 | Parrots all dive at the same enemy | Recorded (feel, for the playtest) |

M4b results: EditMode 442/442, PlayMode 60/60 (`editmode.json`, `playmode.json`), on the final data. Weapon check (`../tools/weapon-check.ps1`, `playable/`): all five weapons and their evolutions fire and kill in 6 s each (evolutions apply: +4 parrots with the deck's quantity, a second turret, ×1.3 whirlpool, a second beam, +2 coin jumps), the re-tuned Flail too; 0 game errors.
