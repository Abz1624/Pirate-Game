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

## M4 proper: built (1 Oct, after "proceed till M8")

Decisions on the owner's behalf (D31, `DECISIONS-WHILE-AWAY.md`): prices on today's ladder (120/130/150/160 Rum), the
design's rank milestones dropped, the evolution level stays 5, the Ramming Prow strikes only while boosting, offer
weights unchanged. Placeholder art (a cannonball for the anchor, other weapons' icons, the Grapeshot Pouch icon for the
eight items) until the art guide's assets (M6).

| Item | What | Where |
|---|---|---|
| W1 Anchor Flail | Orbit, one anchor (model drawn at the blade, pooled and prewarmed), radius 3.5 m, hits each enemy once per 0.75 s and knocks it 2 m; the per-target interval follows the cooldown (Cooldown cards and attack speed shorten it); upgrades damage, cooldown, size, knockback (no quantity: the second anchor is Kraken Chain) | `Data/Weapons/anchor_flail.asset`, `OrbitBehavior.TickPerTarget`, `WeaponVfxPresenter.PrewarmModels` |
| W2 Chainshot | Projectile, 0.75 m wide, passes through 2 more enemies, slows 25% for 2 s; cannon muzzle; no Knockback card (projectiles do not knock back) | `chainshot.asset` |
| W3 Siren Bell | Aura, 5 m, every 3 s, slows 20% for 1.5 s, no curse; upgrades damage, cooldown, size (no Duration card: it would only lengthen the slow) | `siren_bell.asset` |
| W4 Ramming Prow | Aura that pulses only while the ship boosts (it waits, cooldown kept and no target search, otherwise: `WeaponBehavior.CanFire`), 2.5 m ahead of the bow, 3 m radius, every 0.3 s, shoves 2.5 m; new `aura.requiresBoost`, `forwardOffset`, `knockbackMeters` | `ramming_prow.asset`, `AuraBehavior` |
| E1 Empowered evolutions | One generic effect (`EvolutionEffect.Empowered`, `EmpowerBlock`): extra quantity (a burst weapon's burst, everyone else's volley), damage ×, cooldown ×, size ×, knockback ×, extra pierce, extra slow, applied on top of the level-ups at every loadout sync (not stacked), by the game and the simulator alike; one that changes nothing fails validation | `WeaponEvolutionConfig.cs`, `WeaponStatResolver.ApplyEvolution`, `LevelMult`, `WeaponCombat.TraitsOf(slot)`, `SimBuild.SyncSlots` |
| E2 Eight evolutions | Kraken Chain (Flail: +1 anchor, ×1.3 damage, ×1.25 size and knockback), Bar Shot Barrage (Chainshot: +2 shots, +2 pierce, ×1.2), Drowned Choir (Bell: ×1.4 size, ×0.75 cooldown, +10% slow, ×1.25), Ironclad Ram (Prow: ×1.6, ×1.35 size, ×1.3 knockback, ×0.8 cooldown), Whaler's Volley (Harpoon: +1 shot, +1 pierce, ×1.25), Ship of the Line (Long Nine: ×1.5, ×1.5 width, ×0.85 cooldown), Siege Bombard (Mortar: ×1.35, ×1.35 size, ×0.85 cooldown), Swivel Battery (Swivel Guns: +2 shots, ×0.8 cooldown, ×1.15) | `Data/Evolutions/` |
| E3 Eight items | Uncommon chest items, one per evolution, each within the M3 Uncommon band (7-11% by the build model): Rusted Shackle (+12% size), Chain Locker (+10% projectile speed), Siren Scale (+9% attack speed), Iron Figurehead, Whale Oil Barrel and Powder Horn of the Line (+9% damage), Siege Quadrant (+12% size), Swivel Mount (+9% attack speed). In the chest pool like V3's six, but (new switch `chestPoolOwnedEvolutionsOnly`, on; off in the parity preset) a chest leaves out the evolution items of weapons you have not unlocked, so they do not crowd out today's Uncommons | `Data/Build/Items/`, `ChestEconomy.Pool` |
| B1 Balance | Damage and growth set by the M3 weapon model against the weapons a regular player owns, held fixed (`WeaponModel.TuneOnly`; the model counts the Prow only while boosting, `WeaponRulesConfig.modelBoostUptime` 0.3). All four within ±20% of their role at levels 1, 4, 6, 8 and 12. The model does not see pierce, slows or knockback (review #3): those are the weapons' identity, to judge in the playtest | `tune.txt`, `ratios.txt` |
| B2 Simulator | A regular player owns the starters and every weapon priced at most `BalanceTargetsConfig.regularPlayerRum` (100): the dearer new weapons are checked by the model test; the chest pool follows the game's ownership rule for the player modelled (a fresh save: starters only); the simulator applies Empowered evolutions as the game does | `SimContent.cs`, `SimBuild.cs` |
| B3 Parity preset | The new weapons out of the offers, the new items out of the chest pool, the unlock lists cut back to V3's four | `plan/v4-verification/presets/v3-parity.json` |
| T | `V5WeaponMechanicsTests` (+8: content reachable; Empowered numbers applied once and removed; the Bell slows and its Choir reaches further; the Prow only while boosting and only ahead; the Flail ticked against an enemy, once per 0.75 s with a shove each, faster with a Cooldown card; Kraken Chain swings two anchors; Swivel Battery's burst and Whaler's Volley fire more shots; Siege Bombard's shell and Ship of the Line's beam are wider); `V5BuildM3Tests.TheNewWeapons_AreWithin20Percent_OfTheirRole`; guard tests extended (palette, item count, evolution table, cannon muzzles) | `Tests/V4/EditMode/` |

The content was written by a one-off Editor tool, kept as `tools/V5WeaponsBatch1.cs.txt` (V4 code may not name
content ids, so it is not left in `Assets/`). The model's tune and the final ratios: `m4/tune-and-ratios.txt` (levels 1, 4,
6, 8, 12: Flail 1.19/0.92/0.98/0.99/0.96, Chainshot 0.89/0.93/1.13/1.02/1.12, Bell 1.17/0.91/0.95/0.96/0.94, Prow
1.18/0.91/0.97/0.98/0.95).

M4 results: EditMode 432/432 after the First-Timer tolerance note (431/432 before it: the recorded question-9 miss at 10.03 min, see DECISIONS), PlayMode 60/60 (`m4/editmode.json`, `m4/playmode.json`). Weapon check (`tools/weapon-check.ps1`, `m4/playable/`, 9 weapons and evolutions, 6 s each): every weapon fires and kills, evolutions apply (Kraken Chain +1 anchor, Bar Shot +2 shots, Choir and Ironclad ×1.4/×1.35 size, Swivel Battery +2 burst shots), 0 game errors; the plain Prow scored no kill in its 6 s (the scripted boost), its evolved form did.

Not in M4 (recorded): the flame-stream prewarm (D27) is dropped, the flame going to the owner's VFX artist (owner, 1 Oct);
the Prow's aim overlay is drawn around the ship, not at the bow (cosmetic, with the art pass); placeholder icons (Grapeshot
Pouch's for the eight items, other weapons' for the four) until the art guide's icons (M6).

Code review (1 Oct, 2 blockers, 6 majors, 6 minors), all fixed or recorded:

| # | Finding | Done |
|---|---|---|
| 1 | No green full run on the final data; `ratios.txt` cited but missing | Full suites run once on the final data (`m4/editmode.json`, `m4/playmode.json`); ratios written |
| 2 | Swivel Battery's extra shots did nothing (bursts ignore quantity) | Extra quantity lengthens a burst weapon's burst (`ExtraBurstShots`); test |
| 3 | Rusted Shackle's knockback stat is read by nothing | +12% size |
| 4 | The simulator ignored Empowered evolutions | `WeaponStatResolver.ApplyEvolution`, shared by the manager and `SimBuild` |
| 5 | Eight more Uncommons crowd the chest for everyone | Evolution items of locked weapons stay out of chests (game and simulator) |
| 6 | Dead cards: Flail Cooldown, Bell Duration, Chainshot Knockback | The per-target interval follows the cooldown; Bell without Duration; Chainshot without Knockback; re-tuned |
| 7 | Weak tests | Flail ticked; a play test per Empowered evolution kind |
| 8 | Module Lab check, flame prewarm, orbit-model prewarm | Weapon check with screenshots (`tools/weapon-check.ps1`, `m4/playable/`); flame prewarm dropped (VFX artist); orbit models prewarmed at bind |
| 9 | The waiting Prow searched for a target every frame | `CanFire` before the search |
| 10 | D31 and the plan disagreed | D31 corrected |
| 11 | The simulator's 100 Rum was a constant | `BalanceTargetsConfig.regularPlayerRum` |
| 12 | Prow overlay; uptime assumption | Overlay recorded above; the 0.3 uptime (boosting whenever ready) in its tooltip |
| 13 | Placeholder icons | Recorded above (M6) |
| 14 | A neutral Empowered evolution passes validation | Validation error |
