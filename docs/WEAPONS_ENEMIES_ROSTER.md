# Weapons & enemies roster (HighSeas / V3 Saltwind)

Reference for **authored content**, **runtime behavior**, and **where each id is used**.  
Sources: `Assets/_Game/Data/`, `GameDatabase.asset`, `CombatWeaponCatalog`, `CombatSpecialistEnemyCatalog`, `SaltwindAmbientCompositionLogic`, `EncounterSpawnSchedule`, `V3WeaponPlaytestCatalog`, `V3SaltwindEnemyPlaytestCatalog`.

**Last aligned with repo:** solo Saltwind + `Test_WeaponEnemy` playtest flow.

---

## V3SaltwindRun scene (solo Saltwind voyage)

**Scene:** `Assets/_Game/Scenes/v3/V3SaltwindRun.unity`  
**Bootstrap:** `V3RunBootstrap` on `Systems`, with **`StageBalanceProfileHost`** → `SaltwindSoloStageBalanceProfile` (`profileId: saltwind_solo`).  
**Database:** `GameDatabase` + `CombatWeaponCatalog` + `CombatSpecialistEnemyCatalog` on each run boot (same as doc sections below).  
**Spawns:** `EncounterSpawner` uses `EncounterSpawnSchedule` linked from the balance profile (ambient bands, elites, boss, overtime)—**not** manual spawn like `Test_WeaponEnemy`.

### Weapons used in this scene

| Role | Weapon ids | Notes |
|------|------------|--------|
| **Run start (equipped)** | `swivel_guns` | `V3RunBootstrap.ResolveStartingWeapon` forces **`swivel_guns`** while the Saltwind stage balance profile is active (normal solo; not developer preset). |
| **Loadout cap** | Up to **6** slots | `SaltwindSoloStageBalanceProfile.weaponSlotCap` / `CombatExperimentProfile.weaponSlotCap`. |
| **Level-up offer pool (starter)** | `deck_cannons`, `broadside_battery`, `powder_kegs`, `flying_cutlasses`, `long_nine`, `swivel_guns` | `starterWeaponIds` on the profile; new weapons enter via level-up offers (`SaltwindStageProgressionLogic.IsWeaponInOfferPool`). |
| **Marks meta unlocks (offers when owned)** | `lightning_mast` (60 marks), `flamethrower` (80), `mortar` (100), `harpoon_gun` (100) | `weaponUnlocks` on the profile; eligible when save has effective ownership (`V3ContentUnlocks.IsWeaponUnlocked`). **Not** part of the core 6-weapon pool. |
| **Runtime defs (no `.asset`)** | `long_nine`, `swivel_guns` | Required for two of the six starters; injected by `CombatWeaponCatalog`. |
| **Marks meta unlocks (separate from the 6)** | `lightning_mast`, `flamethrower`, `mortar`, `harpoon_gun` | `weaponUnlocks` on the profile; only enter offers when owned via Marks (`V3ContentUnlocks`). Not in `starterWeaponIds`. |
| **Not used in Saltwind** | `grapeshot`, `boarding_party` | Disabled globally in `WeaponFeatureFlags`. |

**Active combat roster:** **6 weapons** — the `starterWeaponIds` list above (same as `V3WeaponPlaytestCatalog.All`). You can equip up to **6 at once** (`weaponSlotCap`), not 10.

**Developer preset only** (`V3PlaytestFlags.DeveloperPresetActive`): `V3RunPlaytestHotkeys` cycles the same **6** with `[` / `]` (or `,` / `.`). Off in normal Saltwind verification (`SaltwindDebugUnlockAll = false`).

### Enemies used in this scene

All spawning is driven by **`EncounterSpawnSchedule`** + **`SaltwindAmbientCompositionLogic`** (see timeline table below). No enemy is exclusive to another scene—this is the **full** Saltwind roster in play.

| Source | Enemy ids |
|--------|-----------|
| **Ambient ordinary pools** (bands 0→4) | `swordfish`, `deckhand_dinghy`, `cutthroat_skiff`, `hammerhead`, `shark`, `cannonboat`, `manta_ray` |
| **Ambient specialist pools** (bands 1→4) | `needle_sloop`, `ram_raider`, `bomb_boat`, `rammer_raft`, `bomb_ketch`, `boarding_barge`, `net_cutter`, `repair_tender`, `powder_barge`, `shield_frigate`, `treasure_raider`, `kraken_spawn` (aliases register at boot from the 14 base defs) |
| **Ambient band anchors** (schedule profile enemy; composition may pick others) | `shark` (0–2 min), `cannonboat` (2–4), `manta_ray` (4–6), `hammerhead` (6–8), `manta_ray` (8–10) |
| **Scheduled beats** | `reef_crab_miniboss` (~90s, `V3PressureBeatDirector`); elite `hammerhead` (~3:00); elite `kraken_spawn` (~8:00); swarm **8×** `shark` (~4:00 & ~7:00) |
| **POI / objective** | **3×** `cutthroat_skiff` (powder cache ambush); **`barnacle_king`** (boss summon / beacon) |
| **Overtime (after 10:00)** | `drowned_pursuer`, `drowned_harrier` (profile ids; shark / cannonboat spawn templates) |

**14 authored definitions** in `GameDatabase.enemies` are the visual/stat base for spawns; **8 specialist ids** exist only after `CombatSpecialistEnemyCatalog.ApplyToDatabase` at run start.

### vs `Test_WeaponEnemy`

| | **V3SaltwindRun** | **Test_WeaponEnemy** |
|--|-------------------|----------------------|
| Spawns | Automatic schedule + composition | **`P`** cycle enemy · **`Ctrl+P`** `reef_crab_miniboss` · **`Shift+P`** `barnacle_king` |
| Weapons | Start **`swivel_guns`**; grow via level-ups within **6** starters (6 slots) | **`[` / `]`** cycles the same **6** (`V3WeaponPlaytestCatalog`) |
| Enemies | Full timeline above | **`;` / `'`** cycles 20 ids (Saltwind first-seen order) |

---

## How data is loaded

| Layer | Path / code | Role |
|--------|-------------|------|
| **GameDatabase (author)** | `Assets/_Game/Data/GameDatabase.asset` | 6 weapons, 14 enemy definitions |
| **GameDatabase (runtime)** | `Assets/_Game/Resources/GameDatabase.asset` | Loaded in play; keep in sync with Data |
| **CombatWeaponCatalog** | `ApplyToDatabase` on run boot | Retunes four authored gunnery weapons; adds **`long_nine`** + **`swivel_guns`** (two starters without assets). Also registers **`harpoon_gun`** + **`mortar`** in the DB for Marks unlock content—not in the 6-weapon offer pool. |
| **CombatSpecialistEnemyCatalog** | `ApplyToDatabase` on run boot | Adds 8 specialist **alias** enemies; retunes `manta_ray`, `reef_crab_miniboss` |
| **GameDatabaseLoader fallback** | If DB missing | 6 weapons + 10 enemy defs including `drowned_pursuer` / `drowned_harrier` (no `.asset`) |

---

## Weapons (solo Saltwind — 6 combat pool)

Cycle order in **`Test_WeaponEnemy`** and dev hotkeys (`[` / `]`): **`V3WeaponPlaytestCatalog.All`** (matches `SaltwindSoloStageBalanceProfile.starterWeaponIds`).  
Default equipped weapon in **`Test_WeaponEnemy`** and **V3SaltwindRun**: **`swivel_guns`**.

| # | id | Asset? | Pattern / targeting (effective) | Role |
|---|-----|--------|----------------------------------|------|
| 1 | `deck_cannons` | Yes | AutoProjectile · ForwardArc 120° · 22m | Forward gunnery |
| 2 | `broadside_battery` | Yes | FanVolley · **PortStarboard only** (no bow/stern) · 14m | Side volleys |
| 3 | `powder_kegs` | Yes | MineDrop · AftDeploy · 12m | Rear mines |
| 4 | `flying_cutlasses` | Yes | Orbit · AreaAroundShip 360° · 6m | Orbit melee |
| 5 | `long_nine` | **Runtime** | AutoProjectile · **ForwardArc 40° only** (highest-HP in cone) · 24m | Precision sniper — no rear/side shots |
| 6 | `swivel_guns` | **Runtime** | AutoProjectile · Nearest 360° · 16m | Default starter / omni poke |

### Authored weapon assets not in the 6 (`Data/Weapons/`)

These exist in **`GameDatabase`** and are unlocked via **Marks**, not `starterWeaponIds`:

| id | Display | Pattern | Notes |
|----|---------|---------|--------|
| `flamethrower` | Flamethrower | TrailZone · ForwardCone | Asset file `burning_oil.asset`; Marks 80 |
| `lightning_mast` | Lightning Mast | ChainLightning · Nearest | Marks 60 |

`CombatWeaponCatalog` also defines **`harpoon_gun`** and **`mortar`** (Marks 100 each) for owned-unlock offers only.

### Weapon usage summary

- **V3SaltwindRun:** Level-up **new weapon** offers pull from the **6 starters** only until Marks unlocks add the four extras.
- **V3 run playtest hotkeys** (developer preset): cycles the **6** above.
- **Test_WeaponEnemy:** `[` / `]` cycles the **6**; start weapon **`swivel_guns`**; **`O` / `V`** aim overlays; **`\`** clears enemies (scene only).

---

## Enemies

### Categories & behavior enum

- **Category:** `Ship` (ranged / formation) vs `Creature` (contact-first; intercept movement).
- **Behavior:** Drives `EnemyWorld.TickBehavior` unless a **custom id hook** overrides (see below).

| Behavior | Movement / combat gist |
|----------|-------------------------|
| `DirectChaser` | Straight chase + contact |
| `FlankingChaser` | Chase with lateral offset |
| `TelegraphedCharge` | Wind-up, burst charge, cooldown |
| `MaintainRange` | Keep `preferredRange`; strafe or hold; ships often shoot |
| `SuicideApproach` | Aggressive close rush |
| `TankChaser` | Slow heavy chase |
| `StrafeBurst` | Strafe + burst windows (`manta_ray` after catalog tune) |
| `FormationRanged` | Range + formation bias |
| `FastSwarm` | Faster swarm chase (creatures) |
| `DiveLunge` | Used with spawn profiles; swordfish profile path |

**Custom logic (ignore generic behavior for movement):**

- **`swordfish`** — `TickSwordfishBehavior`: cruise orbit → spring lunge in range → retreat → cooldown (always sets velocity; required for manual spawn).
- **`shark`** — `TickSharkBehavior`: intercept / lead targeting.
- **`reef_crab_miniboss`** — `ReefCrabMinibossBehavior` + telegraphed charge pattern.
- **`barnacle_king`** — Boss slot + `BarnacleKingBossAttackDirector` (HP phases **>65% / 35–65% / ≤30%** scripted rotation: spread, shell slam, charge, marked bombard).

**Saltwind combat v3 (`SaltwindCombatBehaviorSystem` + `SaltwindSpecialistCombat`)** when `SaltwindCombatFeature` is on:

| id | Player-facing role |
|----|---------------------|
| `swordfish` | Close to ~10m → short telegraph → **guided dash** (limited turn rate) → retreat |
| `deckhand_dinghy` | **Fodder rammer**: wobbly swarm approach, **hull contact damage**, 0.8s recoil, repeat (no guns) |
| `cutthroat_skiff` | Rear-quarter pass → crossing strike → reposition (not a straight chase) |
| `cannonboat` | Orbit at range, single shots while in band |
| `needle_sloop` | Long-range sniper; flees if you close inside ~8m |
| `ram_raider` / `rammer_raft` | Telegraph charge with **lead prediction** |
| `hammerhead` / elite | Charge; elite gets **second charge** before recover |
| `manta_ray` | Orbit + repeating spread volleys |
| `bomb_boat` | Telegraph → missile rush → explode on hull or timeout (VFX) |
| `bomb_ketch` | Mortar marks at **predicted** player position |
| `boarding_barge` | Mid-range telegraph → **slow + chip damage** on connect |
| `net_cutter` | Flank route → **net segment** ahead of movement |
| `powder_barge` | Chase while dropping **proximity mines** |
| `repair_tender` | Escorts wounded allies (heal pulse in range) |
| `gunner_cog` | Formation stagger → 3-shot burst at player |
| `shield_frigate` | Mid-range intercept shot (ally cover) |
| `kraken_spawn` / `elite_kraken_spawn` | Anchor + circle traps; elite fires **3 sequential** marks |

---

### Authored enemies (`Data/Enemies/` → `GameDatabase`)

Stats are **base** values; run applies `GameMath` scaling by time, tier, stage.

| id | Category | Behavior (asset / effective) | Base HP | Speed | Preferred range | Usage |
|----|----------|------------------------------|---------|-------|-----------------|--------|
| `swordfish` | Creature | FastSwarm → **custom spring/lunge** | 8 | 6.5 | 8 | Ambient band 0–1 ordinary; playtest cycle #1 |
| `deckhand_dinghy` | Ship | FastSwarm | 10 | 5.5 | 8 | Ambient band 0–1; playtest #2 |
| `shark` | Creature | DirectChaser + **shark intercept** | 16 | 4.2 | 8 | Ambient anchor band 0; bands 2+; swarm pulse (8×); playtest #3 |
| `cannonboat` | Ship | MaintainRange | 18 | 3.8 | **13** | Ranged ship; ambient anchor band 1+; cannonboat cap in Saltwind; playtest #4 |
| `cutthroat_skiff` | Ship | FlankingChaser | 16 | 4.5 | 8 | Band 1+ ordinary; **powder cache ambush** (3×); playtest #5 |
| `hammerhead` | Creature | FlankingChaser | 14 | 5.0 | 8 | Band 1+; ambient anchor band 3; **~3:00 elite beat**; playtest #8 |
| `manta_ray` | Creature | FlankingChaser → **StrafeBurst** (catalog) | 22 | 4.5 | 8 | Ambient anchor band 2+; band 3–4 ordinary; playtest #10 |
| `bomb_boat` | Ship | SuicideApproach | 10 | 6.0 | 8 | Specialist band 2+; playtest #11 |
| `rammer_raft` | Ship | TelegraphedCharge | 14 | 5.0 | 8 | Specialist band 2+; playtest #12 |
| `boarding_barge` | Ship | MaintainRange | 24 | 3.2 | 10 | Specialist band 3+; playtest #14 |
| `gunner_cog` | Ship | FormationRanged | 20 | 3.5 | 12 | Specialist templates; playtest #20 |
| `kraken_spawn` | Creature | TankChaser | 28 | 2.5 | 8 | Specialist band 3+; **~8:00 elite beat**; playtest #19 |
| `reef_crab_miniboss` | Creature | TelegraphedCharge (catalog) | 120 | 4.6 | 5 | **~1:30 miniboss** (`V3PressureBeatDirector`); optional playtest cycle |
| `barnacle_king` | Creature | TankChaser (boss) | 800 | 2.5 | 8 | **Boss summon** schedule + POI; Shift+P in playtest |

---

### Runtime specialist aliases (`CombatSpecialistEnemyCatalog`)

Not stored as assets; cloned at run boot from template prefab/stats. Tagged `specialist`. Picked from **SpecialistFamilies** by ambient band (20–40% of picks from band 1 onward).

| id | Template visual | Behavior | Threat | Base HP | Speed | Saltwind band (first seen) |
|----|-----------------|----------|--------|---------|-------|----------------------------|
| `needle_sloop` | cutthroat_skiff | FlankingChaser | 2 | 14 | 5.5 | 1 (2:00+) |
| `ram_raider` | rammer_raft | TelegraphedCharge | 3 | 22 | 7 | 1 |
| `bomb_ketch` | bomb_boat | MaintainRange | 5 | 28 | 3.2 | 2 (4:00+) |
| `net_cutter` | gunner_cog | FormationRanged | 4 | 18 | 4.5 | 3+ |
| `repair_tender` | boarding_barge | MaintainRange | 6 | 24 | 2.8 | 3+ |
| `powder_barge` | boarding_barge | SuicideApproach | 4 | 40 | 2.2 | 3+ |
| `shield_frigate` | gunner_cog | TankChaser | 7 | 45 | 3 | 3+ |
| `treasure_raider` | deckhand_dinghy | FastSwarm | 4 | 16 | 6.5 | 3+ |

Playtest **`;` / `'`** order matches first-seen Saltwind order: `V3SaltwindEnemyPlaytestCatalog.All` (20 entries by default; +miniboss +boss if enabled on director).

---

### Overtime-only ids (`GameDatabaseLoader` fallback)

No `Data/Enemies/*.asset`. Spawn profiles override id while reusing shark/cannonboat anchors.

| id | Based on | Behavior (fallback) | Usage |
|----|----------|---------------------|--------|
| `drowned_pursuer` | shark profile | DirectChaser | Saltwind **overtime** after 10:00 (`saltwind_drowned_pursuer`) |
| `drowned_harrier` | cannonboat profile | MaintainRange + ranged | Overtime ranged (`saltwind_drowned_harrier`) |

Not in **Test_WeaponEnemy** enemy cycle.

---

## Saltwind solo spawn timeline (usage map)

| Time / event | Enemy ids involved |
|--------------|-------------------|
| **0:00–2:00** (band 0) | Ambient anchor `shark`; ordinary pool: `swordfish`, `deckhand_dinghy` |
| **2:00–4:00** (band 1) | Anchor `cannonboat`; + `cutthroat_skiff`, `hammerhead`; specialists `needle_sloop`, `ram_raider` |
| **4:00–6:00** (band 2) | Anchor `manta_ray`; ordinary + `shark`, `cannonboat`; specialists `bomb_boat`, `rammer_raft` |
| **6:00–8:00** (band 3) | Ordinary `manta_ray`, `cannonboat`, `hammerhead`; specialists `bomb_boat`, `boarding_barge`, `kraken_spawn` |
| **8:00–10:00** (band 4) | Same families; heavier specialist share |
| **~1:30** | `reef_crab_miniboss` (pressure beat) |
| **~3:00** | Elite `hammerhead` (`hammerheadBeat`) |
| **~4:00 / ~7:00** | Swarm pulse: 8× `shark` |
| **~8:00** | Elite `kraken_spawn` (`krakenBeat`) |
| **POI powder cache** | 3× `cutthroat_skiff` |
| **Boss** | `barnacle_king` |
| **10:00+ overtime** | `drowned_pursuer`, `drowned_harrier` (no new ordinary ambient) |

Ambient composition: `SaltwindAmbientCompositionLogic` — ordinary/specialist split 100/0 → 60/40 by band; picks uniformly within eligible families subject to caps (`cannonboat`, `kraken_spawn`, charge windups).

Schedule asset: `Assets/_Game/Data/EncounterSpawnSchedule.asset` (sync to `Resources/` before play).

---

## Test_WeaponEnemy quick reference

| Input | Action |
|-------|--------|
| `[` / `]` | Cycle **6 Saltwind weapons** (`V3WeaponPlaytestCatalog`) |
| `;` / `'` | Cycle **20 enemies** (Saltwind order; excludes boss/miniboss by default) |
| `P` | Spawn selected enemy ahead of ship |
| `Shift+P` | Spawn `barnacle_king` |
| `\` | Despawn all enemies (this scene only) |
| — | Player **invulnerable** (this scene only) |

Scene boot: `WeaponEnemyPlaytestBootstrap` → same `GameDatabase` + combat catalogs as V3; **no** ambient `EncounterSpawner` (manual spawns only).

---

## Related docs & menus

- Spawn wiring: [ENCOUNTER_SPAWN_SETUP_GUIDE.md](./ENCOUNTER_SPAWN_SETUP_GUIDE.md)
- Editor: **HighSeas → Setup Enemy Visuals**, **HighSeas → Sync Enemies Folder To GameDatabase**, **HighSeas → Saltwind → Author Step 04 Spawn Schedule**
- Playtest scene: **HighSeas → Scenes → Open Weapon+Enemy Playtest**
