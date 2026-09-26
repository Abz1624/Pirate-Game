# M5 — Weapon configs and Weapon Manager

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `f8edb04e` (M4 passed) |
| Status | **Passed** (playtest 27 Sep; flamethrower visuals to be updated later, R-89) |

## What M5 delivers

**Every weapon is config.** `WeaponConfig` (all 10) now carries a V4 `behavior` and one block per firing logic with the constants V3 kept in `WeaponController`, `ProjectileWorld` and the juice settings: projectile/burst (Swivel 2 shots, ×0.55 damage and size, 2.4 s life, close-defense targeting), lane (Long Nine beam half-width 0.32), fan (Broadside 3 pellets that ignore Quantity, 42 speed floor, 1.2 s life, side salvo), orbit (2 + qty blades, radius 5, hit 2.75, every 0.12 s), stream (2.75 s, recharge max(0.55, 0.75 × cooldown), tick 0.08), trail, mine (arm 0.2, trigger 3, blast 3.2, ×0.72, 12-hit proc budget), area strike (3.5 + 0.5 × qty), aura, predicted area (0.6 s shell), summon, and the projectile model. `levelDamageFactor` is on only where V3 applied the level factor (R-21). **Evolutions** are six `WeaponEvolutionConfig` assets (Stormshot lane + burst, King's Broadside follow-up, Devil's Wake, Bladed Tide, Hellfire Nozzle, Tempest Mast) with their numbers; **weapon rules** hold the fire-all orbit volley and Roxie's salvo.

**The Weapon Manager** (`Runtime/Weapons`): one slot per equipped weapon, ten behaviors (one per firing logic), one stat resolver, a targeting service, a damage calculator and player projectiles — all ports of the V3 code with the same random draws, float expressions and iteration order, and no weapon-name checks. Contracts (`IEnemyQuery`, `IEnemyDamageSink`, `IShipState`, `IWeaponModifiers`, `IWeaponCombatHooks`, `IWeaponEffects`, `IProjectileViews`, `IWeaponLoadout`) keep it free of V3 types; `Runtime/Legacy/LegacyWeaponAdapters.cs` implements them over V3.

**The loadout record (register D3).** The V3 `WeaponController` is switched off and unbound from the ship: the legacy level system still equips and upgrades weapons on it until M9, V4 reads it every tick and writes its firing state back (the HUD, flame visuals and aim overlay keep working). Its own fire paths return at once.

**Frame order.** The weapon tick runs in a scene slot that took `WeaponController`'s file ID (658786988; the record moved to 658787988 and is off, `V4SceneSetup.ApplyM5`); player projectiles tick where V3 ticked `ProjectileWorld`; orbit blade positions are computed at order 50 from `Time.time`, where V3's juice controller computed them, and used the next frame.

**Captain actives and Captain's Bell** go through `LegacyActiveProbe`: an activation (the active's cooldown rising) fires V4's fire-all, keg arming or salvo; the Bell's 12th kill fires all weapons on the next weapon tick. Actives stay off by default until M9 (V3 setting).

**Presentation.** Projectile views go through the M4 view pool (`ProjectileViewPresenter`, pools derived per projectile model and prewarmed). Muzzle, beam, explosions, lightning, flames, mortar telegraphs, kegs and skiffs still draw through the legacy juice controller behind `LegacyWeaponEffects` (M6 moves them to V4 presenters).

**Tuning Console → Weapons**: each weapon's behavior, level, cooldown or stream, evolution; Fire now, +1 rank, Evolve, Remove; Add or Solo any weapon. Numbers are edited live in the Config section (`weapon.*`, `evolution.*`, `weapon_rules`).

## Intended differences

| Change | Why |
|---|---|
| No cooldown is spent without an emission: a fan that finds nothing to fire at, or a volley blocked by a full projectile pool, keeps its cooldown and retries (V3 spent it; with the pool full V4 no longer rolls damage or plays effects on the retry) | Doc rule (gameplay doc, M5 plan) |
| Every weapon hit counts toward the results' damage share (V3's ledger only had lanes, auras, strikes and skiffs); burst and follow-up pellets credit their weapon. Kill credit (last-hit weapon) is unchanged | Doc rule; ledger only, nothing in the simulation changes |

## Deviations from the task list (recorded)

| Plan | M5 | Why |
|---|---|---|
| Port WeaponJuiceController visuals into a V4 weapon presenter and remove it (and ProjectileWorld) from the scene | The juice controller keeps drawing muzzle, beam, explosions, lightning, flames, mortar telegraphs, kegs, skiffs, orbit blades and the flame stream behind `LegacyWeaponEffects`; its **gameplay** moved to V4 (blade hit spheres, beam width in config, mortar impact draws in the simulation). `ProjectileWorld` stays in the scene, unticked and empty | Visual port belongs with M6 (presentation). Consequence: the drawn blades use V3's constants, so editing `orbit.*` moves V4's hit spheres away from them until M6 |
| Player projectiles on a `SlotPool` | Plain array, lowest free slot first, index order | Iteration order decides kill order, which feeds the loot random stream the traces compare |
| — | The aim overlay's mortar pulse is gone (it reads the record's empty strike list); ship-versus-ship projectile hits (PvP only) are not ported | Legacy overlay retired in M6; V4 is the solo game |
| `WeaponStatParityTests` ranks 1/4/8/12, `PlayerProjectileParityTests` against ProjectileWorld | Covered by exact trace parity (every hit, kill and spawn of three seeded runs) plus a 2,000-case damage-roll test against V3 `DamageSystem`; per-behavior unit tests with fakes | The traces compare the real V3 code end to end |

## V3 quirks kept (register)

- **R-87** Swivel Guns' "burst" gap is overwritten by the full cooldown on the normal path (only fire-all keeps it).
- **R-88** Roxie's salvo searches for weapon ids that do not exist, so it always strikes the fallback, for 1 damage.
- R-18, R-21, R-22, R-70 as listed in the register (Stormshot hits everything in range, level factor, Broadside ignores Quantity, mortar effect draws).

## Legacy reader audit

| Replaced | Readers in V4 scenes | Classification |
|---|---|---|
| WeaponController firing (now a switched-off record) | LevelSystem (equips, upgrades, reads levels), HUDs (loadout icons/levels), juice controller (blade count/size, flame streaming), aim overlay (slots), POIManager chest plan, V3RunItemEffects (Admiral's Ledger families; Captain's Bell fire), V3CaptainActiveController (actives), V3MultiShipRunSetup | Record kept: found by type, loadout and mirrored firing state stay correct. Captain/Bell fire paths inert (null owner) and reproduced by the probe; multi-ship setup re-switched off |
| ProjectileWorld (no longer ticked or filled) | CombatDebugOverlays, playtest tools | Show 0 projectiles |
| WeaponJuiceController gameplay (cutlass hits, beam width, mortar draws) | WeaponController (inert) | Moved to V4 |

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 115 / 115. New: every weapon's behavior and constants, evolution configs, damage calculator equal to V3 DamageSystem on 2,000 rolls (and the streams stay in step), targeted weapons keep their cooldown without a target, Swivel's cooldown (R-87), keg blast proc budget of 12, mortar impact draws (exact count), flame stream start and recharge, orbit blades and hit interval, fire-all, salvo (R-88), projectile slots and hits, loadout sync and mirror, each evolution's effect | `editmode-results.json` |
| V4 PlayMode | 20 / 20. New: weapons fire from V4 and the record's fire path is inert (ProjectileWorld empty); level-up writes to the record reach V4 slots; all 10 weapons fire in a live run | `playmode-results.json` |
| Traces (end of phase, final code) | V4 with the v3-parity preset == V3 goldens on seeds 101/202/303 (MATCH ×3) and replay MATCH, with V4 doing all the firing; default runs == the M4 default runs (MATCH ×3: the V4 weapons change nothing in a run) | `trace-comparison.txt`, `player/` |
| Playable check (Module Lab) | All 10 weapons fire (3 s each: deck 9, swivel 16, long nine 5, harpoon 8, broadside 8, cutlasses 11, flamethrower 3 streams, kegs 5, lightning 7, mortar 7); live baseCooldown 1 → 0.25 took Deck Cannons from 10 to 35 shots per 3 s; forcing Stormshot gave lanes hitting up to 12; projectile pools 0 created after prewarm; console 0 errors | `playable-check/` |
| Config set hash | `342a6153…` (60 configs) | `config-hash.txt` |

## Independent review

No blockers. Fixed: a captain active pressed on the frame its cooldown expired was missed (major; now any rise of the cooldown counts); Captain's Bell fired inside the kill event (now on the next weapon tick, V3's subscriber order); console showed legacy-read fields (tags, max level, upgrade axes, pattern, name) as live (now "applies from M6/M9") and the blade-visual caveat; retries with a full projectile pool rolled damage and played effects every frame; multi-ship setup re-armed the record (switched off again); console loadout changes left kegs, skiffs and follow-ups alive (now cleared, as V3's SetWeaponLoadout did; teardown clears without effects); per-call config lookups (cached); a static mutable default; the mortar draw test now checks the exact count; added a test per evolution. Recorded: kill credit is still last-hit (V3); aim-overlay mortar pulse and PvP projectile hits (deviations table).

## Your playtest

1. **Module Lab** (`V4ModuleLab`): F1 → Weapons → **Solo** each weapon; spawn enemies (Module Lab section) and watch it fire. Try **Fire**, **+1**, and **Evolve** on Deck Cannons (Stormshot lanes) and Flying Cutlasses (Bladed Tide pulse).
2. F1 → Config → `weapon.deck_cannons` → `baseCooldown` 0.3: it fires much faster at once.
3. **A full run** from `V4Boot`: level up to several weapons; everything should look and feel exactly like before (it matched V3 hit for hit in the traces).

## Playtest feedback and approved fixes (27 Sep)

**Flamethrower visuals missing** (damage worked): V3's juice controller set the existing `Flamethrower_Stream_VFX` flame particles to 1 particle a second (authored 60), so the stream was nearly invisible (R-89). V4 now shows that same effect at its authored emission while the flamethrower streams (`FlameStreamPresenter`); verified 24 live particles mid-stream, off during recharge (`flame-probe/flame-v4-streaming.png`).

**Fixes approved (R-18 to R-22, R-27, R-70, R-87, R-88)**, each a config value on by default and turned off by the v3-parity preset:
- R-18 Stormshot lane 40° arc · R-19 chain lightning · R-21 level factor on every weapon · R-22 Broadside takes Quantity, item attack speed once, cooldown overshoot carried · R-27 mortar ring matches its 0.6 s shell · R-70 no effect draws · R-87 Swivel burst keeps its gap · R-88 salvo uses your lightning weapon (level factor 1).
- **Deferred to M8** (they need the V4 enemy world): Shock, Tarred, Breach and Curse readers, the harpoon tether (R-19/R-20), weapon knockback (R-22).
- **Balance note:** together these make weapons noticeably stronger (every weapon scales with level, Swivel fires two shots per cooldown, attack speed and cooldowns tighter). All are in F1 → Config.

Verification (targeted, no traces): EditMode 122/122 (new: a test per fix, and the preset restoring V3 for each), weapon PlayMode tests 3/3, Module Lab screenshots of the flame stream and chain lightning.

## Re-check (27 Sep)

Passed: flame stream, chain lightning, mortar ring and Swivel's two shots all work. **Note for later:** the flamethrower's visuals need a proper update (R-89: the stream is the existing effect at its authored rate, but it runs past the 9.5 m damage cone and is not matched to the stream timing).
