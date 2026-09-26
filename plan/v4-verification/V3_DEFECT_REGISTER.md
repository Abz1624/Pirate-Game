# V3 defect and doc-discrepancy register

Default for every row: ported faithfully (plan D7) so seed traces stay comparable. "Fix" is a proposal decided at the named phase gate. Status column is updated as decisions are taken.


| Id | Finding (evidence) | Fix proposal | Gate | Status |
|---|---|---|---|---|
| R-01 | Doc says band-0 rate 0.5 is live; spawner uses interval 1/0.6 (`SpawnProfiles/Saltwind/SaltwindAmbientBand0.asset`) | Keep 0.6 or switch to 0.5 | M1 | **decided 26 Sep: 0.5** (applied in M1; V3-parity runs use `presets/v3-parity.json`) |
| R-02 | Reef Crab at 150 s (`Resources/DemoPacingSettings.asset`), doc table says 1:30 | Keep 150 or set 90 | M3 | pending |
| R-03 | Scheduled "elite" Hammerhead/Kraken are plain units (`isElite 0`); elite second charge / sequential traps never fire | Beats use elite variants | M3 | pending |
| R-04 | Swarm pulse is ×1.25 rate; the 8-shark `SaltwindSwarmPulse` profile never spawns | Optional burst | M3 | pending |
| R-05 | Overtime spawns shark/cannonboat stats, not Drowned archetypes (no definitions in DB) | Use drowned variants | M3 | pending |
| R-06 | Kraken area-denial budget never released (`SaltwindCombatEncounterBudget`), krakens stop marking after 2 casts | Release on end | M8 | pending |
| R-12 | Adaptive counters dead: `RunCombatSignals` never created/ticked | Keep inert or enable | M8 | pending |
| R-13 | 8 specialist aliases render as primitive cubes (no visual prefab copied, `CombatSpecialistEnemyCatalog` L32–62) | Variants use template prefab | M2 | pending |
| R-18 | Stormshot lane hits every enemy within 22 m (arc 360 after runtime tuning) | Narrow arc | M5 | pending |
| R-19 | Lightning Mast is an AoE strike, not a chain; Shock does nothing | Chain behavior | M5 | pending |
| R-20 | Harpoon has no tether; Tarred/Breach/Curse have no readers | Apply statuses | M5/M8 | pending |
| R-21 | Rank damage factor only on projectile/lane patterns; others ignore weapon level | Uniform scaling | M5 | pending |
| R-22 | Broadside ignores `quantityBonus`; knockback never read; item attack speed applied twice; cooldown overshoot dropped | Fix each | M5 | pending |
| R-27 | Mortar lands at 0.6 s while telegraph shows 1.1 s | Align | M5 | pending |
| R-31 | Evolutions unreachable once every weapon is maxed | Offer path fix | M9 | pending |
| R-34 | Lookout's Glass +2.0 crit per rank (one rank = 100 % crit) | 0.02 per rank | M9 | pending |
| R-35–38 | Four Logbook stat ids wrong/invalid (doc decision) — note GoldGain is ignored in Saltwind so Quartermaster's still does nothing | Applied (D6) | M9 | pending |
| R-40 | Five item effects computed but unread (explosion, status, controlled-target, extra lightning jump, spawn density); near-miss hook never called | Wire readers | M9 | pending |
| R-43 | Powder Rush applies Damage ×1.3 (named attack speed) and shares Battle Signal's slot; Full Sail shares Tailwind's | Separate slots / attack speed | M9 | pending |
| R-47 | Chest plan looks for `WeaponController` on the player object → equipped weapons always null (`POIManager` L449–465) | Pass loadout | M7 | pending |
| R-49 | Brine armor never applied; Ember/Roxie multipliers unused; Richie pickup-radius burst missing; gamepad South = boost + active | Implement / rebind | M9 | pending |
| R-55 | B key summons the boss in any build | Dev-only (planned: M0 still ensures `V3BossSummonHotkey` exactly as V3) | M7 | planned |
| R-57 | Storm damage per frame is throttled by 0.2 s i-frames (effective ~0.7 dps) | Tick-based damage | M3 | pending |
| R-59 | `RunGoldExpiresAtRunEnd` never read; death toast says "gold banked" when it isn't | Text fix (applied with Rum) | M12 | pending |
| R-61 | Doc says 52-Rum fixture; approved constant is 57 (`SaltwindStageSettlementLogic` L88) | Keep 57 | M12 | pending |
| R-63 | Two `V3RunResultsScreen` in the V3 scene; damage numbers/health bars not pooled; many per-frame `FindFirstObjectByType` | Fixed structurally in V4 | M6/M8 | pending |
| R-67 | Some V3 tests write real PlayerPrefs and mutate `Data/GameDatabase.asset` in memory | Note for regression runs; V4 tests isolated | M0 | pending |
| R-68 | `V3MinibossArena` is static and survives a scene reload; dying mid-arena blocks the next run's spawns; V3 has the same bug on menu → Play reloads, only its in-place Retry clears it | V4 resets it at run start (`V4LegacySpawnBridge.ResetSpawnDirectors`, test `SceneReload_ClearsStaleMinibossArena`) | M0 | applied M0 |
| R-69 | Simulation reads `Time.time` (enemy wobble, pattern pulse, i-frames, cutlass orbit); `EnemyWorld._nextId` never resets; spawn placement depends on the camera view | Fixed-tick V4 clock + unit ids per run (M8); goldens pinned per D5 | M0/M8 | pending |
| R-70 | A VFX method (`WeaponJuiceController.OnMortarImpact`) consumes the gameplay combat RNG stream | Parity draw in M5; drop after you approve | M5 | pending |
| R-71 | `SpawnDirectorDebugUI` (F9) rewrites the base spawn rate every frame and compounds the boss multiplier | Not created in V4 (planned: M0 still ensures `SpawnDirectorDebugUI` exactly as V3) | M3 | planned |
| R-72 | `EnemyWorld` has no `enemyRoot` in the Saltwind scene, so `EnemyViewPool` uses `/Systems` as its root and deactivates/reactivates the whole object around every new enemy view (`EnemyViewPool.cs` L46–59): every component on `/Systems` gets OnDisable/OnEnable mid-run (found in M0: it closed the Tuning Console) | V4 components that react to OnDisable live off `/Systems` (applied to the console); V4 enemy views get their own pool root in M8 | M0/M8 | applied M0 (console) |
| R-73 | The stage profile export (`StageBalanceProfile.ToExport`) snapshots weapon/boss stats from the loaded `GameDatabase`, which every run mutates in memory with `CombatWeaponCatalog`/`CombatSpecialistEnemyCatalog.ApplyToDatabase` after the profile context began. So the profile `ContentHash` shown on the results screen and stored with runs changes with each run in one Editor session (`954db823…` clean, then `da239315…`, `9da95d63…`) until the asset is reloaded | V4 applies catalogs to a deep clone from M2, so the asset is never mutated; the trace harness unloads the database before each run (applied M0). Also: a mid-run profile rebuild after a V4 config edit re-snapshots the mutated database, so the hash changes even for unrelated edits (M1 review) | M0/M2 | harness applied M0; fix M2 |
| R-67a | V3 tests change real Editor PlayerPrefs outside the save isolation: `V3IntegrationTests` deletes `v3_home_seed` and `v3_first_conquest_complete`; PlayMode boss kills lock `v3_home_seed` | Back up/restore prefs around V3 suite runs with `tools/prefs-guard.ps1` (applied from M1) | M0 | tool added M0 |
| R-74 | Simulation depends on rendering: pickups align to their models' renderer bounds (`PickupWorld.cs` L172–195), and skinned-mesh bounds only update when drawn. With cameras drawing nothing, a gold pickup lands differently and the run diverges (found while trying to speed up traces) Also makes runs depend on Editor visibility: a V4 seed-101 replay diverged by one gold at 8:30 because skinned bounds only update when some camera draws them | V4 pickups (M9) place from data, not renderer bounds. Until then trace runs render normally and pin visual state (skinned meshes update off-screen, animators always animate; header `visualPins`) | M9 | harness pin applied M0 |
| R-75 | A miniboss arena (Reef Crab at 2:30, elite beats) pauses ambient spawns until its focus enemy dies (`V3MinibossArena`). If the player never kills it, ambient spawning stays off for the rest of the run: every trace before M1 had no ambient spawns after 2:30 because the kiting autopilot never fights it | Harness: kill the focus enemy 20 s after the arena opens (applied in M1). Design: arena timeout or keep blocking, decide at M3 | M1/M3 | harness applied M1 |
| R-76 | From about 3:40 the ambient cap (80 max − 8 reserved = 72 alive) is full for the autopilot's build, so new spawns only replace kills and band-rate changes after band 1 do not change spawn counts in these runs (band 2 at 4/s gave the same counts as 1.8/s) | Note for tuning: rate edits matter while below the cap; the cap and kill speed dominate later. M11 threat-rate lever must account for it | M3/M11 | noted M1 |


---
