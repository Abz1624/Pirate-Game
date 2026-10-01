# V5 M4: new weapons, batch 1, and the missing evolutions (phase plan)

Plan: `V5_PLAN.md` "#### M4". Groundwork read: `RESEARCH.md` (30 Sep) and a code map (1 Oct). Built while the owner
is away (decisions D28-D30 in `plan/v5/DECISIONS-WHILE-AWAY.md`, questions 17-26). Start commit: `d3184a3d`.

The V4 weapon code could not express the four new weapons: there was no enemy slow, pierce was hard-coded to one hit,
weapon knockback was never read, only the first Orbit weapon worked, Aura had no visual and needed a target, and
weapons could not see the boost. The plan review (1 Oct: no blockers, 12 majors) also showed that the weapons and
evolutions need decisions only the owner can take (prices, the Ramming Prow against "auto-attacks", the design's rank
milestones, the evolution level) and balance-model work. So M4 is split:

- **M4a (built while the owner was away, its own commit)**: the mechanics, each opt-in, today's weapons provably
  unchanged; and the flamethrower VFX fix (R-89).
- **M4 proper (after the owner's answers)**: the four weapons, the eight evolutions, their items, prices, cards and
  balance.

## M4a: mechanics

Every mechanic is opt-in through a new field whose default leaves today's weapons exactly as they were (D28). The
goldens test proves it: `V5WeaponGoldenTests` fires each of today's ten weapons and six evolutions for 10 s at a fixed
ring of enemies at levels 1 and 6, with the default data and the v3-parity preset, and compares every hit and status
with `weapon-goldens.txt`, written by the same test on the code before M4 (`d3184a3d`, captured by stashing the M4
changes). It runs twice: with the ship still and with the ship sailing through the ring (the flamethrower and the
evolutions need a heading and enemies ahead; in the moving pass all 16 fingerprints differ), 128 lines in all. Its fake
logs any slow or knockback, so one that today's data started to apply would change a fingerprint.

| Item | What | Where |
|---|---|---|
| A1 Slow | A real enemy slow: share of speed and seconds on the enemy unit, the strongest holds (a weaker one does nothing while it lasts, an equal one extends it, a stronger one replaces it); movement and the reported velocity (aim leads, hit sampling) use it; a committed charge dash and the boss's forced movement are not slowed (they keep their telegraphed reach). Caps from the combat design 2.3 in `WeaponRulesConfig`: regular enemies (minibosses included) at most 60% slowed; bosses take half, at most 20% | `EnemyUnit.cs`, `EnemySimulation.Tick.cs` (`ApplySlow`, `SpeedMultiplier`, movement), `EnemyWorldContracts.cs`, `WeaponRulesConfig.cs` |
| A2 Pierce | `projectile.pierce` (0-7, default 0 = stop at the first hit, as before): a shot hits 1 + pierce enemies, each once (unit ids, up to 8 remembered per shot, no allocation; a piercing homing shot stops homing after its first hit); `projectile.slowFraction/slowSeconds` slow each hit; the shot's width is its existing `sizeBase` (hit radius) | `PlayerProjectiles.cs`, `ProjectileBlock`, `WeaponCombat.TraitsOf` |
| A3 Knockback | A push of a set distance (at most 3 m, `maxKnockbackMeters`) over `knockbackSeconds` (0.15) away from a point, through island collision; bosses not moved; elites half (`eliteKnockbackShare`) (combat design 2.3). Orbit weapons use it through `orbit.knockbackMeters` (default 0), scaled by the Knockback upgrade (D28) | `EnemySimulation.Tick.cs` (`Knockback`), `OrbitBlock`, `WeaponBehaviors.cs` |
| A4 Orbits | Each Orbit weapon has its own blades and hit timer (only the first worked); opt-in `orbit.perTargetIntervalSeconds`: each enemy hit once per interval by that weapon, checked every frame (the cutlasses keep their shared tick); an Orbit weapon with `visual.orbitModel` draws that model at its blades (pooled), the others keep the cutlass blades | `WeaponBehaviors.cs` (`OrbitBehavior`), `WeaponManager.OrbitHitsOf`, `WeaponVfxPresenter.cs` |
| A5 Aura | Pulses without a target; `aura.slowFraction/slowSeconds`; Curse only when `curseSeconds` > 0; a pulse effect (`IWeaponEffects.AuraPulse`, placeholder: the area-impact ring) | `WeaponBehaviors.cs` (`AuraBehavior`), `WeaponContracts.cs`, `AuraBlock` |
| A6 Boost | `ShipBoostController.IsBoosting` and `IShipState.IsBoosting` for the Ramming Prow | `ShipBoostController.cs`, `WeaponContracts.cs`, `SharedPorts.cs` |
| A7 Flamethrower VFX | The flame reaches the damage cone's range (measured from the muzzle, as the damage is) and spreads to its arc, from the stream's first frame and following the slot's live range and arc (size, items, Hellfire): each particle system's start speed is scaled so its particles travel the range in their authored lifetime (keeps the flame's density; the first version shortened lifetimes, which thinned it) (R-89). Simulated: the farthest particle at 9.91 m for a 9.5 m cone, 13.33 m for 13 m | `FlameStreamPresenter.cs`, `WeaponSlot.StreamRange/StreamArcDegrees` |
| T | `V5WeaponGoldenTests`; `V5WeaponMechanicsTests` (plain shot stops at the first hit; a piercing shot hits each enemy once and slows; the projectile block carries pierce and slow; two Orbit weapons both have blades; per-target orbit hits and knockback × the upgrade; the aura pulses without a target, slows, curses only when set; boost off by default; the flame reaches the cone's range at two settings); `V4EnemyTickM8Tests` (slow caps and the strongest-holds rule; a 50% slow halves travel; knockback 2 m, elites half, bosses not moved) | `Tests/V4/EditMode/` |

M4a results: `V5WeaponGoldenTests` passes (today's weapons fire exactly as before, 128 fingerprints); EditMode and
PlayMode suites in `editmode.json` (423/423), `playmode.json` (60/60). Playable check (`playable-check.ps1`, `playable/`): the
flamethrower alone streams and its flame is shaped to the damage cone's 9.5 m and 52° from the first frame
(screenshots `flame-1..3.png`, `after-flame.png`), 0 game errors. Slow and knockback are measured in the EditMode tests
(a live sample through the CLI, seconds apart, against chasing and dashing enemies, was meaningless and was dropped).

Plan review (1 Oct, no blockers, 12 majors, 8 minors) and what was done:

| # | Finding | Done |
|---|---|---|
| 1 | Evolutions need more mechanics (pull, split, clusters, lane burst, second pulse, Ram wake) | Moved to M4 proper with their own tests |
| 2 | Orbit hits are a shared tick, not per target | Opt-in `perTargetIntervalSeconds`, keyed by unit id, fixed arrays |
| 3, 4 | The weapon model cannot judge new behaviours; role targets would move | M4 proper (freeze the ten weapons' targets) |
| 5 | Design damage is 2-3× V4's; milestones dropped silently | M4 proper: design sets shape, parity sets damage; question 20 |
| 6-9 | Prices, offer-pool dilution, eight items in the chest pool, parity of new content | M4 proper; questions 18, 22 |
| 10 | Goldens before any change, all weapons and evolutions, both presets | Done (`weapon-goldens.txt` from `d3184a3d`) |
| 11 | Design caps for slow and knockback | Done in `WeaponRulesConfig` (D29) |
| 12 | Slowed enemies must report slowed velocity | Done |
| 13 | The Ram is underspecified; boost against "auto-attacks" | Question 19 |
| 14 | The Bell would curse | Curse only when `curseSeconds` > 0 |
| 15 | D28 missing, plan text level 8 | D28-D30 logged; question 21 |
| 16 | Dead Knockback card | Scales the new knockback; question 17 |
| 17 | No allocation | Fixed arrays, inline hit memory |
| 18, 19 | Playable check for M4a | Done (the flame) |
| 20 | Default no-op interface methods hide dead mechanics | Fakes record them; a test drives the real sink |

Code review (1 Oct, 1 blocker, 5 majors, 11 minors), all fixed or recorded: the flame was 1.65 m short (the cone is
measured from the muzzle) (blocker); a 3 m knockback cap; the boss rules applied to minibosses (now bosses only, as the
design; question 24) and slows shortened boss charges (dashes and forced moves exempt); the goldens could not see slows
or the moving weapons (fake logs them, moving pass); the flame test could not fail (now simulates the particles); no test
used the real sink (one does); a removed orbit weapon dealt one more tick; pierce and per-target memory by slot (unit
ids); piercing homing shots turned back; orbit models not released on clear (released; prewarmed when a weapon first
uses them, in M4 proper); the forwarder dropped `AuraPulse`; lifetimes thinned the flame (speed scaled instead);
`knockbackSeconds` in config; `pierce` 0-7; tests for equal and stronger slows, minibosses and the cap; the test's
template leak; stale comments.

Not in M4a (step 2, with their own tests): pull toward the ship, split-on-hit shots, several clusters at once, a
burst at the end of a lane, a delayed second pulse, the Ram behaviour and its wake (review #1); the balance model's
per-behaviour terms (#3, #4); prices, offer-pool and chest-pool effects (#6-#9).

## M4 proper: open for the owner (questions 17-26)

Design numbers from `Rogue_Pirates_Combat_Content_v3_1.md` §4.7 set each weapon's shape and cadence; its damage comes
from the M3 parity band (V4 damage runs at about 0.2-0.45× the design's), the existing ten weapons' role targets frozen
(review #4, #5).

| Weapon | Behaviour | Design (rank 1) | Evolution (needs) |
|---|---|---|---|
| Anchor Flail | Orbit (own model), per-target 0.75 s, knockback 2 m | 1 anchor, radius 3 m, 100°/s, 35 contact damage | Kraken Chain (Rusted Shackle) |
| Chainshot | Projectile, width 1.5 m, 2 pierces, 25% slow 2 s | every 1.5 s, 20 damage, 16 m, 18 m/s, 1 s life | Bar Shot Barrage (Chain Locker) |
| Siren Bell | Aura, 20% slow 1.5 s | every 3 s, 26 damage within 5 m | Drowned Choir (Siren Scale) |
| Ramming Prow | new Ram behaviour while boosting | new design | Ironclad Ram (Iron Figurehead) |

Evolutions for Harpoon Gun, Long Nine, Mortar and Swivel Guns, each with a new item (Whale Oil Barrel, Powder Horn of
the Line, Siege Quadrant, Swivel Mount), and the unlock prices, wait for the owner's answers.
