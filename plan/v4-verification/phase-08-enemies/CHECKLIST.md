# M8 — Enemy World

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `c26cdaf7` (M7, awaiting playtest) |
| Status | Implemented and reviewed; **awaiting your playtest** |

Design record: [DESIGN.md](DESIGN.md) (reader audit, update order, determinism hazards, build order).

## Progress

| Step | What | State |
|---|---|---|
| 0 | Read-only enemy views and dev actions (`IEnemyWorldView`, `IEnemyDevControl`, `IHostileProjectileView`, `IHazardView`) over the legacy world; the trace recorder, the trace pilot and the Tuning Console (header, Module Lab, boss health) read enemies only through them (V3 scenes use the same legacy views) | Done. PlayMode test: the views equal the legacy arrays slot by slot every frame (live cannonboat shots included); the dev kill goes through the kill path |
| 1 | `EnemySimulation`: slots (lowest-free), unit ids (per run, from 1, only on success), `Spawn` / `SpawnFromProfile` / `SpawnPressureBeat`, despawning, combat state per run; V3's enemy-id rules as archetype/variant data (`world` block, migrator `ApplyM8World`) | Done. EditMode lockstep: every definition and every Saltwind spawn profile, reuse after despawns, a full world (failed spawns use no ids), cull/despawn-all — identical to V3 field by field, with and without the Saltwind combat feature |
| 2–4 | The frame: behaviours per combat archetype, specialists, Reef Crab, pattern movement, special behaviours, movement/islands/separation/overlaps, views, ranged path, contact damage, hazards, statuses, hostile shots, hit path, kill fan-out | Ported (mechanical port of V3's code, statics → per-run instances, enemy ids → rules). EditMode lockstep: a crowd of every enemy + every spawn profile for 1,500 frames with hits and burns, and 12 archetypes standing still then chased for 1,200 frames — agents, combat states, hostile shots, hazards, damage to the ship and kills identical to V3 every frame. PlayMode lockstep in the Module Lab (real islands, physics and V3's models, R-81 respected by spawning each world in its own frame): 19 enemy types incl. every specialist and the Reef Crab around the islands for 900 frames with hits, burns and kills — identical every frame |
| 5 | Special behaviours (no V3 data uses them) | Ported with step 2 (summon goes to a service; adaptive signals stay null as in V3, R-12) |
| 5b | Boss path; contracts over the V4 world; silent-loss ports | Boss lockstep (spawn-profile movement, the director's hold and forced charge, escorts, hits) identical to V3 for 900 frames. `EnemyWorldContracts` implements the spawn sink, weapon query/damage, boss control, views, dev actions and the presenters' enemy view over the V4 world (same semantics as the legacy adapters). `HellfirePatches` (lockstep: same rolls on the combat stream, same burns and kills as V3's HellfirePatchWorld) and `ExplosiveBarrels` (enemy damage from barrel blasts) ported; not bound yet |
| 6 | Switch-over | Done; reviewed. End-of-phase trace set (run once): seeds 101/202/303 with the v3-parity preset equal the V3 goldens exactly (437/409/523 lines, `trace-comparison.txt`). `LegacyEnemyWiring` builds the V4 world in `V4Game.Awake` and registers its contracts (spawn sink, enemy views, dev actions, hostile shot and hazard views); the shell runs `ResetAndDefine`, the enemy frame (`TickFrame`, V3RunBootstrap.Update's order: boss director, enemies, player projectiles, hostile shots, V3 pickups and floor powerups, statuses), the R-06 release, a hellfire slot where V3 ensured HellfirePatchWorld and a telemetry slot where CombatTelemetryDriver was added; weapons, the boss director, barrels (V3's markers kept, its update moved to a slot on the same object), the presenters' enemy view and the hazard rings use the V4 world. `EnemyWorld` and `StatusController` removed from V4SaltwindRun and V4ModuleLab (`V4SceneSetup.ApplyM8`; their scene values now on `V4Game.enemyWorld`). Replaced adapters deleted; the rest re-tagged M14 with reasons; `V4Phase.Current = 8`. Tests: EditMode 214/214, PlayMode 37/37 (switch-over test: V3 pieces gone, slots present, hazard rings on V4, a hellfire patch burns a V4 enemy; parity-preset barrels on the V4 world; Module Lab lockstep with islands against a V3 world the test builds). Playable check (`playable-check/`): the full route (spawns and kills, chest, Repair Wreck, Signal Buoy, Powder Cache ambush of 3, Beacon, boss kill, results) on the V4 world, 0 game errors |

## Deviations from the task list (recorded so far)

| Plan | M8 | Why |
|---|---|---|
| Frame-order adapters (`LegacyUpdateSlot`, `LegacyOrder50Slot`, `LegacySpawnSlots`, stage frame slots) deleted in M8 | Re-tag to M14 (at switch-over) | They keep V4 ticks at V3's positions relative to the run clock (`RunManager.Update`), which stays until M14 |
| Enemy views through PoolService | V3's `EnemyViewPool` under /Systems (`LegacyEnemyViewSink`, M14) | Creating a view toggles /Systems and new ship models keep island colliders until the end of the frame (R-81); the simulation sees both |
| Damage numbers / hit feedback as V4 presenters | V3's calls at V3's points (`LegacyEnemyFeedback`, M14) | Damage numbers draw from Unity's global random state, which feeds pickup offsets |
| Combat parameters from config blocks without the registry | Spawn parameters still from V3's `SpawnProfileResolver` over the registry that `LegacyCombatProfileOverride` fills from config (`LegacyEnemySpawnParams`, M14) | Keeps M8's size down; the resolver's own id rules stay in legacy code |
| V4 `EncounterTracker` | The kill path still completes encounters in the static RunEncounterTracker (`LegacyEnemyKillTargets`) | The stage content and its adapters read it; moving both together is safer |
| Port V3's combat-feature-off branches | Not ported: `ReefCrabMinibossBehavior` and the shark rush only ran with the Saltwind combat feature off | V4 runs always have the stage profile active |
| Statuses keyed by unit id (intended difference) | Slot-keyed as V3 for now; the switch comes after exact parity | A burn applied after a killing hit is inherited by the next spawn in V3; changing it breaks trace parity |
| `PlayerDamageService` (I8.6) | Enemy damage to the ship still goes through `LegacyPlayerDamageSink` / `DamageReceiver.TakeDamage` (re-tagged M14) | The captain and item damage multipliers it applies are still V3 components until M9 |
| Pooled health bars, damage-number presenter (I8.7) | V3's health bars and `FloatingDamageNumbers` at V3's points (`LegacyEnemyViewSink`, `LegacyEnemyFeedback`, M14) | Damage numbers draw from Unity's global random state (pickup offsets); moving them changes the run |
| Stop `LegacyEventFeed`'s Hit/Kill republishing | Kept | The V4 world still raises TriggerBus Hit/Kill; nothing publishes Hit/Killed natively yet, so stopping it would drop both events |
| Telemetry driver | `CreateTelemetrySlot` samples the V4 world's enemies and the V4 projectile count (V3's driver counted `ProjectileWorld`, 0 since M5); it looks the ship up until found (V3 cached it in `Start`) | Telemetry only (not in traces); the V4 count means what V3's did |
| — | Shared V3 code still read: `EnemyCombatProfileRegistry` (config-filled), `PlayBounds`, `IslandObstacles`, `GameConstants`, `GameMath`, `SpatialHash`, physics; `Time.time`/`Time.deltaTime` where V3 read them | No per-run state of the old world; same frame, same values |

## Legacy reader audit (switch-over)

Readers of V3's `EnemyWorld`, `StatusController`, `HostileProjectileWorld`, `HellfirePatchWorld`, `CombatTelemetryDriver` and the static hazard/budget/combat-state stores reachable in V4 runs:

| Reader | Classification |
|---|---|
| `LegacyEnemyWorldPort`, `LegacyEnemySpawnSink`, `LegacyWeaponEnemies`, `LegacyBossControl`, `LegacyHostileProjectiles`, `LegacyEnemyView`, `LegacyAreaDenialRelease` | Replaced by the V4 world and deleted |
| Trace recorder, trace pilot, Tuning Console (header, Module Lab, boss health) | Switched to the enemy views/dev actions (step 0); V3 scenes keep the legacy views for the golden recordings |
| `HellfirePatchWorld` | Replaced: `HellfirePatches` in a slot where V3 ensured it; the weapon hook calls the V4 patches |
| `V3ExplosiveBarrelWorld` (parity preset only) | Its update moved to a V4 slot on the same object (markers kept) |
| `CombatTelemetryDriver` | Replaced by the telemetry slot |
| `LegacyHazardTelegraphView` | Reads the V4 hazards |
| `EncounterSpawner` (switched off) | Still receives the despawn count and summon queue, as from V3's EnemyWorld (no effect; D4) |
| `WeaponController` record, `ProjectileWorld` | Disabled / never filled: their enemy-world reads are unreachable |
| Static `SaltwindHazardTelegraph`, `SaltwindCombatEncounterBudget`, `SaltwindCombatAgentStateStore` | Only reset by `LegacyRewardsAdapter`; the V4 world keeps its own per run |
| `V3RunPlaytestHotkeys` (dev hotkey "despawn oldest enemy") | Null-safe: now does nothing (dev only; the console's Module Lab "Clear all" covers it) |
| `EnemyIdentityDebugLabels` | No longer created (debug overlay; a DevTools port later) |
| `V3MultiShipRunSetup` despawn | Only with a multi-ship scene config, which no V4 scene has |
| `MinimapController`, `V3MinimapHud`, `WeaponTargetingVisualizer`, `CollisionDebugOverlay`, `SpawnDirectorDebugUI`, `BotShipBrain`, V3 boss/spawn directors | Not in the V4 scenes, never created |

## Independent review of the switch-over

A read-only reviewer compared the switch-over with the deleted port, V3RunBootstrap.Update and V3's EnemyWorld. No blockers; the frame order, reset, despawn listener order, object parents and creation moments, slot positions and scene values were confirmed. Findings and actions:

| Finding | Action |
|---|---|
| Reader audit missing; two dev-tool losses unrecorded | Audit above; the dev hotkey and debug labels recorded |
| Deviations unrecorded (PlayerDamageService, I8.7 presenters, the Hit/Kill feed kept) | Rows added; DESIGN corrected |
| The island scene values were only covered by the trace | EditMode test: both scenes carry V3's values on `V4Game.enemyWorld` and no V3 enemy components |
| Tests lost what they claimed (views test now maps the V4 world; hellfire via reflection; R-06 and telemetry untested in a run) | Views test renamed; flame-kill hook test (patches only for the Hellfire evolution); R-06 in a run and off under the parity preset; telemetry samples the V4 world's enemies |
| Telemetry sampler differs from V3's driver | Recorded (deviations) |
| Nits: dead V3 hellfire fallback, stale hazard-view source, per-hit config scan, LegacyAdapters.cs line endings, the TMP font asset | Fallback removed; source cleared when the run's scene goes; highlight lookup cached (rebuilt on config edits); file encoding restored; the font asset is kept out of the commit |
| Suggested: a scan for V3 enemy-world types in V4 code | Added (only the V3-scene golden views and the scene step may name them) |

## Independent review of steps 0–5

A read-only reviewer diffed every V4 file against its V3 original: faithful (no behavioural change in arithmetic, conditions, early returns or call order; every enemy-id rule read where V3 compared the id, including ally vs self). Findings and actions:

| Finding | Action |
|---|---|
| The step-0 legacy views were tagged for M8 but DevTools keep them for V3-scene golden recordings; the pilot/recorder could fall back to them in a V4 run | Re-tagged to M15; the fallback only applies when there is no V4 run |
| The R-06 fix (area-denial release) lived on V3's static budget and would silently stop at switch-over | Ported onto `EncounterBudget.TickAreaDenialRelease`; test: identical to the legacy release over 600 frames (hold 0 and 2 s) |
| Hit feedback and the kill fan-out were not tested (feedback null in the lockstep) | Order test: highlight (none for Powder Kegs) → shake + sound → hit event → damage number; lethal hit → fan-out while the slot is still active → despawn |
| The hammerhead latch writes V3's shared registry; tests left it changed | Tests restore it; the override adapter stays (re-tagged M14) so it resets per run |
| The boss path was excluded | Boss lockstep added (hold, forced charge, escorts) |
| Switch-over list incomplete | Completed in DESIGN (despawn listener order, highlight flag, status event, summon, hazard view kind, boss/weapon/view adapters, debug labels, /Systems root, unit ids) |
| Minor/nits (direct time reads, registry reads, docs disagreeing on status keying, summon counting, rule test coverage, tooltip) | Recorded as deviations / fixed; rule test now covers every rule and asserts all other enemies have none |

## Register additions (to write at the gate)

- R-99: V3's hazard telegraphs cache the player statically, so from the second run in a process Kraken traps, mines and nets never hurt (V4 per-run; fixed by the port).
- R-100: the first elite hammerhead to tick gives every hammerhead the elite second charge (V3 writes the shared profile); V4 keeps it per run (the registry is re-applied at run start).

## Your playtest

1. **A normal run from `V4Boot`.** Enemies should look, move, attack and die exactly as before: sharks, swordfish dashes, hammerhead charges, cannonboat shots, bomb boats, Kraken traps (the ground rings), the specialists, the Reef Crab at 2:30, the elite beats and the Barnacle King (with its bigger charge from the M7 follow-up). Hits flash, damage numbers show, kills drop XP and gold.
2. **Flamethrower with the Hellfire Nozzle evolution** (F1 → Weapons → Solo / Evolve in the Module Lab): burning patches should damage enemies standing in them.
3. **Module Lab** (`V4ModuleLab`): F1 → Module Lab → spawn a few enemies, then **Clear all**.
4. **F1 → Landmarks, Beacon & Boss → Summon boss**, then set its health (e.g. 25 %) and fight it.
5. Decide: R-100 (only elite hammerheads charge twice; V3 lets the first elite give every hammerhead the second charge).

## Playtest round 1 (27 Sep) and fixes

| Your note | Finding | Fix (targeted tests only; no traces) |
|---|---|---|
| R-100: only elite hammerheads charge twice | Decision | `enemy.hammerhead.world`: `eliteLatchesSecondChargeForAll` off, new `eliteChargesTwice` on (the elite charges twice itself; the shared profile is never latched). The v3-parity preset restores V3's latch; the V3 lockstep tests apply V3's rules. EditMode `R100_OnlyTheEliteHammerheadChargesTwice_AndTheSharedProfileStaysUnchanged` |
| The burning patch VFX looks broken; use placeholder markers like the Kraken rings | V3's burning-trail prefab, played through the disabled `WeaponJuiceController`, renders with a broken pink material (R-104) | Each Hellfire patch draws a ground ring (burn radius 2.1 m) with an inner ring that shrinks as the patch burns out (`LegacyHazardTelegraphView.Patches`); the broken effect is not played. Visual only. PlayMode `HellfirePatch_ShowsAGroundMarker_UntilItBurnsOut` |
| Module Lab: Clear all works | — | — |
| F1 "Summon boss" says "Summon refused, see toast" | In the Module Lab, stage content was switched off by design (V3 switched `POIManager` off in its test scene) and the refusal showed no toast. In a run the summon and the Beacon prompt work (checked live) | The lab keeps chests, landmarks, the Beacon and their prompts on; the encounter queue ticks in the lab so the boss and the Powder Cache ambush spawn; the B key works in the lab; every refused summon shows a reason. Ambient spawns stay off. A lab boss victory is not saved (no home-seed lock, no boss-victory Marks); Clear all during a lab boss fight forgets the summon so the boss can be summoned again. PlayMode `ModuleLab_BossSummonAndKill_IsNotSaved`, `ModuleLab_ClearAllDuringTheBossFight_LetsYouSummonAgain`, `ModuleLab_PowderCache_AmbushSpawnsThroughTheEncounterQueue`, `ModuleLab_SpawnsOnlyOnRequest` (updated). This deliberately reverses the M2 review's lab rule (no POIs in the lab) and M7's "never in the Module Lab" for the summon |
| No "hold E" popup on the Treasury Beacon | Same cause (Module Lab) | Same fix |
| No chest popups in V4ModuleLab | Same cause | Same fix |
| Magnetic Tide: XP, health and gold don't increase | V3 defect R-103 (confirmed live: 28 XP and 16 gold barrels flew in and vanished; XP and gold unchanged; health barrels not pulled) | `LegacyMagneticTide` (tag M9): when a Magnetic Tide is collected, every XP, gold and health barrel opens where it lies and its crystals, coins and health fly to the ship and pay (`stage.saltwind.magneticTideOpensBarrels`; off in the v3-parity preset). PlayMode `R103_MagneticTide_OpensEveryBarrel_AndPays_NotUnderTheParityPreset` (both presets), `R103_AnExpiredMagneticTide_OpensNothing`; EditMode: the default config has it on |

### Independent review of the playtest fixes

No blockers; the v3-parity preset restores V3 exactly for R-100 and R-103, and the lab and encounter-queue changes are reachable only in the lab (not used by traces). Findings and actions:

| Finding | Action |
|---|---|
| Major: a boss killed in the lab would lock the home seed (PlayerPrefs) and register the boss-victory Marks, changing later real runs | Lab mode skips both; test kills a lab boss and checks the home seed and Marks |
| The stated Hellfire cause was wrong: V3's WeaponJuiceController is still added (disabled) and did play the pink prefab | Comments, R-104 and this table corrected |
| Clear all during a lab boss fight left the run stuck in the boss fight with the summon used | Clear all forgets a despawned boss; test |
| The lab summon test proved nothing about the queue tick and could be flaky | Summon retried; new Powder Cache ambush test only the queue tick can pass |
| No negative test for an expired tide; no EditMode check of the default switch | Both added |
| Nits: per-frame config scan, a redundant flag reset, duplicate ring helper, stale InteractionModel doc | Switch cached (refreshed on config edits); cleaned up |

Tests after the fixes and the review: EditMode 215/215, PlayMode 43/43.

## Playtest round 2 (27 Sep) and fixes

| Your note | Finding | Fix (targeted tests only; no traces) |
|---|---|---|
| Module Lab chest, Beacon, summon and Powder Cache work, but the Beacon's hold goes to 100%, back to 0% and up again before the boss appears | After the hold, the Beacon's 0.7 s summon telegraph runs while the prompt keeps offering the hold (V3 behaviour); a held E runs a second hold that does nothing | While the telegraph runs the prompt shows "summoning..." (R-101 switch). EditMode `Beacon_WhileTheSummonTelegraphs_ThePromptOffersNoSecondHold` |
| The Powder Cache doesn't seem to spawn enemies; it should spawn 4-5 elites which, all killed, drop a rare treasure chest, like Megabonk | V3's ambush: 3 plain cutthroat skiffs out of view on approach (R-105). Megabonk's Challenge Shrine: accept the challenge, marked elites spawn, killing all of them drops a free chest | `powderCacheEliteChallenge`: hold E to spring; 4 elites (2 armored skiffs, 2 rich cannonboats) in view; the cache hides (no prompt) while they fight; then it becomes a free Rare Treasure Chest (Rare or better item + 20 gold). V4-only spawners get their own runtime spawn profile. EditMode `PowderCacheChallenge_HoldSpringsFourElites_NotByProximity`, `..._AllElitesSunk_TheCacheBecomesAFreeRareChest`, `..._AFailedSpawn_Regroups`; PlayMode `ModuleLab_PowderCacheChallenge_FourElites_ThenAFreeRareChest`; V3's ambush kept under the parity preset (`PowderCache_V3Ambush_UnderTheParityPreset_SpawnsThroughTheV4Executor`) |
| Magnetic Tide works | — | — |
| Hellfire patches only show on enemies; also show them on the water | V3 drops a patch only where the flame kills (R-106) | While the flame streams, a patch every 0.6 s on the water ahead of the ship. EditMode `HellfireNozzle_LeavesPatchesOnTheWaterWhileStreaming_NotUnderTheParityPreset` |
| Elite hammerhead charges twice, normal ones once | — | — |

### Independent review of round 2

No blockers; the v3-parity preset turns every round-2 behaviour off, and the Beacon change has no simulation effect. Findings and actions:

| Finding | Action |
|---|---|
| Major: a partly spawned elite group timed out, left its elites outside the encounter and queued a new group, possibly again and again | Challenge elites have no spawn deadline: they keep trying until all 4 are placed |
| Major: Clear all, or a group that could not be queued, left the cache hidden for good | Clear all brings open challenge caches back; a group with nothing queued fails cleanly and the cache stays usable; tests |
| The cannonboat elites took the skiff-tuned encounter overrides; the gold/XP multipliers are read by nothing | Overrides zeroed (the enemies' own values); multipliers left at 1 |
| Validator gaps (hold seconds of 0 would interact on arrival, rarity range, stream-patch values); an unmatched V3 spawner would silently get its own profile | Validated; a warning unless the spawner's profile id is marked V4-only (`v4_`) |
| PlayMode challenge test could flake (a lab weapon sinking an elite early; a stacked item) | Counts the encounter's spawns and the item stacks |
| Hellfire stream patches: interval drift; balance (about 4 live patches while streaming) | Drift fixed; balance recorded under R-106 |
| Nits: the chest used the landmark range; the hidden-cache prompt depended on the R-101 switch | Chest range 6 m; the challenge switch also releases the hidden cache |

Tests after round 2 and its review: EditMode 222/222, PlayMode 43/43. Checked visually in the Module Lab (prompt, 4 elites, the chest and its prompt).

## Playtest round 3 (27 Sep)

| Your note | Fix |
|---|---|
| Beacon: "summoning.." works | — |
| Couldn't find the Powder Cache; add an F1 button to spawn or locate it | F1 → Landmarks, Beacon & Boss → **Go to**: Powder Cache, Beacon, Chest, Repair Wreck, Signal Buoy, Treasure Chart. The ship moves next to the nearest unused one, inside its prompt range (dev console only). PlayMode `ConsoleGoTo_PowderCache_PutsTheShipInItsPromptRange_InTheRunAndTheLab` |
| Hellfire rings on the water work | — |

