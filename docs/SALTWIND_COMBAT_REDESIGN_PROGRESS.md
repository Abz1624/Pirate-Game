# Saltwind combat redesign — implementation progress

**Combat balance version:** `3` (`SaltwindCombatFeature.CombatBalanceVersion`, `StageBalanceProfile.balanceVersion`)  
**Plan authority:** [SALTWIND_COMBAT_REDESIGN_PLAN.md](./SALTWIND_COMBAT_REDESIGN_PLAN.md)  
**Last updated:** 2026-09-24 (playtest pass 2)

## Stage checklist

| Stage | Status | Evidence |
|-------|--------|----------|
| 1 — Baseline & resolver repair | **Implemented** | `SpawnProfileResolver`, `EnemyCombatProfileRegistry`, gate tests |
| 2 — Weapon reliability & auto-orientation | **Implemented** | `long_nine` ForwardArc 40°; `broadside_battery` PortStarboard only |
| 3 — Ordinary & offensive specialists | **Implemented** (playtest-driven fixes) | Behavior + specialist ticks, bomb boat VFX, manta repeat fire |
| 4 — Support, elites, bosses, overtime | **Mostly implemented** | Barnacle King phase table + Shell Slam; support specialists functional; elite hammer/kraken flags |
| 5 — Integrated balance & polish | **Partial** | `WEAPONS_ENEMIES_ROSTER.md` updated; seeded matrix / perf not run |

## Playtest harness (`Test_WeaponEnemy`)

- **`P`** — spawn selected cycle enemy  
- **`Ctrl+P`** — `reef_crab_miniboss`  
- **`Shift+P`** — `barnacle_king` (boss attack director ticks in playtest bootstrap)  
- **`[` / `]`** — weapons · **`;` / `'`** — enemies · **`\`** — clear

## Automatically verified

| Check | Result |
|-------|--------|
| `SaltwindCombatRedesignGateTests` | Re-run after this pass (see commands below) |
| PlayMode / in-editor spot check | **Recommended** after pulling changes |

## Known gaps

1. **Shield frigate** — intercept shot only; full frontal shield arc for allies not simulated.  
2. **Formation AI** — gunner cog uses spawn-id stagger, not leader/follower slots.  
3. **Balance lab** — matrix tests may still fail on `balanceVersion` 3 until recalibrated.  
4. **Playtest UI** — extended telemetry panel still not built.

## Commands

```powershell
unity test "Pirate Game Unity" --mode EditMode --filter "SaltwindCombatRedesignGateTests" --output editmode-redesign-results.xml
```
