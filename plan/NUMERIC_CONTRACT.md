# Rogue Pirates — Numeric and Simulation Contract

Version: `2026-09-14` • Schema: `1` • Content profile: `baseline_v1`

Authority: [Rogue_Pirates_Cursor_Implementation.md](../Rogue_Pirates_Cursor_Implementation.md) §M0, [plan/00_MASTER_IMPLEMENTATION_SPEC.md](00_MASTER_IMPLEMENTATION_SPEC.md)

---

## Platform contract (M0-001)

| Item | Pinned value |
|---|---|
| Unity Editor | `6000.3.11f1` |
| Render pipeline | URP `17.3.0` |
| Input | Unity Input System `1.19.0` |
| Gameplay orientation | Landscape only |
| Authoritative sim rate | **30 Hz** fixed tick (`SimConstants.TickHz = 30`) |
| Render rates tested | 30, 60, 120 FPS — must not change sim outcomes |
| Coordinate space | XZ plane, Y up; top-down camera pitch `52°` |
| Playfield baseline | ~40×30 m readable combat zone within larger stage |
| Integer scale | `SimFixed.Scale = 1000` (millimeter-equivalent fixed point) |

---

## Time

- Simulation advances in fixed ticks: `tickDuration = 1/30` seconds.
- `SimClock.SimSeconds` accumulates only while no blocking pause reasons are active.
- Wall clock (`Unity Time`) drives presentation only; never authoritative for combat.
- Overtime begins after `StageDurationSeconds = 600` sim seconds (10 minutes).

---

## RNG streams

Deterministic 32-bit xorshift streams seeded from run seed. Stream indices:

| Stream | Purpose | Seed mix |
|---|---|---|
| 0 | Map/layout | `seed * 73856093` |
| 1 | Loot/rarity | `seed * 19349663` |
| 2 | Director/spawns | `seed * 83492791` |
| 3 | Combat procs | `seed * 50331653` |
| 4 | Powerups | `seed * 27182818` |

Rules:
- Cosmetic/visual randomness must not consume combat streams.
- Legendary rarity cap: ≤ 4% after luck weighting.
- All rolls use stream-local state; no `System.Random` or `UnityEngine.Random` in Simulation assembly.

---

## Numeric precision

- Positions, velocities, HP, damage stored as `SimFixed` (int64 scaled by 1000).
- Angles stored as millidegrees (int32).
- Rounding: damage final value uses **banker's rounding to nearest integer fixed unit**, then clamp min 1.
- XP/chest costs: round to nearest whole unit using `MidpointRounding.AwayFromZero`.
- Threat/damage formulas mirror `GameMath` in [`GameConstants.cs`](../Pirate%20Game%20Unity/Assets/_Game/Scripts/Core/GameConstants.cs) but use fixed-point implementations in `SimMath`.

---

## Combat tick ordering (M1)

Per tick, in order:

1. Apply scheduled pause/unpause transitions (pause set is a **set**, not single enum).
2. If paused: skip sim advance except connectivity heartbeat counters.
3. Process player input for tick.
4. Move actors; resolve planar collision (circle-vs-AABB islands, circle-circle actors).
5. Weapon cooldowns → fire → spawn projectiles.
6. Advance projectiles; resolve hits (source/receiver whitelist).
7. Apply status ticks and proc queue (bounded proc count per tick).
8. Resolve simultaneous lethal events (Last Rum rules).
9. Spawn director / enemy AI step.
10. Pickup collection.
11. Emit presentation events (damage, death, XP, objective).

---

## Pause reason set

Reasons: `Choice`, `Manual`, `Focus`, `Suspend`, `Connectivity`, `Loading`.

- Multiple reasons may be active simultaneously.
- Clearing one reason does not clear others.
- Simulation time stops when **any** blocking reason is active.

---

## Acceptance fixtures (golden hashes)

| Fixture | Seed | Ticks | Expected state hash |
|---|---|---|---|
| `m1_movement_only` | 12345 | 900 | recorded at first CI run |
| `m1_deck_cannon_shark` | 12345 | 1800 | recorded at first CI run |
| `m1_boss_player_same_tick` | 99999 | 3600 | objective secured before run end |
| `m1_last_rum_same_tick` | 77777 | 120 | single consumption |

Hashes computed from canonical JSON snapshot of actor HP, positions (fixed), RNG stream heads, objective flags.

---

## Content schema version

- Manifest version field on all exported JSON: `schemaVersion: 1`.
- Content version string: `baseline_v1`.
- Run snapshots pin manifest hash at run start.

See `Content/ContentExporter.cs` and `plan/balance_profiles.json`.
