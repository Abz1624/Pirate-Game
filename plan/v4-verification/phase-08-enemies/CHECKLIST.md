# M8 — Enemy World

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `c26cdaf7` (M7, awaiting playtest) |
| Status | In progress — V4 enemy world built and lockstep-tested against V3; not yet bound to the run (switch-over pending) |

Design record: [DESIGN.md](DESIGN.md) (reader audit, update order, determinism hazards, build order).

## Progress

| Step | What | State |
|---|---|---|
| 0 | Read-only enemy views and dev actions (`IEnemyWorldView`, `IEnemyDevControl`, `IHostileProjectileView`, `IHazardView`) over the legacy world; the trace recorder, the trace pilot and the Tuning Console (header, Module Lab, boss health) read enemies only through them (V3 scenes use the same legacy views) | Done. PlayMode test: the views equal the legacy arrays slot by slot every frame (live cannonboat shots included); the dev kill goes through the kill path |
| 1 | `EnemySimulation`: slots (lowest-free), unit ids (per run, from 1, only on success), `Spawn` / `SpawnFromProfile` / `SpawnPressureBeat`, despawning, combat state per run; V3's enemy-id rules as archetype/variant data (`world` block, migrator `ApplyM8World`) | Done. EditMode lockstep: every definition and every Saltwind spawn profile, reuse after despawns, a full world (failed spawns use no ids), cull/despawn-all — identical to V3 field by field, with and without the Saltwind combat feature |
| 2–4 | The frame: behaviours per combat archetype, specialists, Reef Crab, pattern movement, special behaviours, movement/islands/separation/overlaps, views, ranged path, contact damage, hazards, statuses, hostile shots, hit path, kill fan-out | Ported (mechanical port of V3's code, statics → per-run instances, enemy ids → rules). EditMode lockstep: a crowd of every enemy + every spawn profile for 1,500 frames with hits and burns, and 12 archetypes standing still then chased for 1,200 frames — agents, combat states, hostile shots, hazards, damage to the ship and kills identical to V3 every frame. PlayMode lockstep in the Module Lab (real islands, physics and V3's models, R-81 respected by spawning each world in its own frame): 19 enemy types incl. every specialist and the Reef Crab around the islands for 900 frames with hits, burns and kills — identical every frame |
| 5 | Special behaviours (no V3 data uses them) | Ported with step 2 (summon goes to a service; adaptive signals stay null as in V3, R-12) |
| 5b | Boss path; contracts over the V4 world; silent-loss ports | Boss lockstep (spawn-profile movement, the director's hold and forced charge, escorts, hits) identical to V3 for 900 frames. `EnemyWorldContracts` implements the spawn sink, weapon query/damage, boss control, views, dev actions and the presenters' enemy view over the V4 world (same semantics as the legacy adapters). `HellfirePatches` (lockstep: same rolls on the combat stream, same burns and kills as V3's HellfirePatchWorld) and `ExplosiveBarrels` (enemy damage from barrel blasts) ported; not bound yet |
| 6 | Switch-over: V4 world bound in runs, legacy EnemyWorld/StatusController out of the scenes, weapons/boss/spawner rebound, hellfire and barrel damage ported, telemetry sampler, `V4Phase.Current = 8`, PlayMode lockstep with islands, end-of-phase trace set | Pending |

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
| — | Shared V3 code still read: `EnemyCombatProfileRegistry` (config-filled), `PlayBounds`, `IslandObstacles`, `GameConstants`, `GameMath`, `SpatialHash`, physics; `Time.time`/`Time.deltaTime` where V3 read them | No per-run state of the old world; same frame, same values |

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
