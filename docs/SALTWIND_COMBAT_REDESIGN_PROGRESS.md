# Saltwind combat redesign — implementation progress

**Combat balance version:** `3` (`SaltwindCombatFeature.CombatBalanceVersion`, `StageBalanceProfile.balanceVersion`)  
**Plan authority:** [SALTWIND_COMBAT_REDESIGN_PLAN.md](./SALTWIND_COMBAT_REDESIGN_PLAN.md)  
**Last updated:** 2026-09-24

## Stage checklist

| Stage | Status | Evidence |
|-------|--------|----------|
| 1 — Baseline & resolver repair | **Implemented** (partial verification) | `SpawnProfileResolver.Resolve(profile, spawnedEnemy, …)`, `EnemyCombatProfileRegistry`, manual `Spawn()` applies same params; `SaltwindCombatRedesignGateTests` |
| 2 — Weapon reliability & auto-orientation | **Implemented** (partial verification) | `WeaponFireOutcome` / cooldown only on emission; swivel nearby bias; deck/broadside/long-nine catalog targeting; broadside projectile damage; harpoon slow on hit |
| 3 — Ordinary & offensive specialists | **Implemented** (needs playtest) | `SaltwindCombatBehaviorSystem` archetypes for roster ids |
| 4 — Support, elites, bosses, overtime | **Partial** | Hazard budget hooks, ambient pool expansion, kraken/bomb ketch stubs; Barnacle King phase rewrite **not complete**; support heal/shield/net/boarding **simplified** |
| 5 — Integrated balance & polish | **Not complete** | Art briefs drafted below; seeded playtest matrix **not run**; playtest UI panel **not extended** |

## Implemented (code)

- `EnemyCombatProfile` + `EnemyCombatProfileRegistry` — per-enemy combat identity from **spawned** definition.
- `SaltwindCombatFeature` gate (Saltwind run + `Test_WeaponEnemy`).
- `SaltwindCombatBehaviorSystem` — movement/attack cycles for scoped enemies (telegraph/execute/recover FSM).
- `SaltwindCombatEncounterBudget`, `SaltwindHazardTelegraph`, contact suppression during telegraphs.
- `SaltwindReefCrabCombat` — sidestep/slam loop (replaces legacy bubble loop when redesign active).
- Weapon targeting/cooldown fixes in `WeaponTargeting`, `CombatWeaponCatalog`, `WeaponController`, `ProjectileWorld`.
- Ambient composition pools updated per plan §4 (`SaltwindAmbientCompositionLogic`).
- Run reset hooks in `V3RunBootstrap` and `WeaponEnemyPlaytestBootstrap`.

## Automatically verified

| Check | Result |
|-------|--------|
| `SaltwindCombatRedesignGateTests` | **4/4 passed** — `editmode-redesign-results.xml` (2026-09-24 batch) |
| Full EditMode suite | **180/183 passed** on pre-change batch (`editmode-results.xml`); 3 failures are **SaltwindBalanceLab** matrix tests (pre-existing numerical gates) |
| PlayMode / Saltwind scene | **Not executed** (no live Editor during implementation; batch PlayMode not run) |
| Seeded playtest 101/202/303 | **Not executed** |
| Performance 80-enemy cap | **Not executed** |

## Visually verified

- None in-editor during this pass.

## Awaiting player feedback

- Enemy readability, fairness, and “distinct & enjoyable” bar from plan §5.

## Known gaps / failures

1. **Barnacle King** — `BarnacleKingBossAttackLogic` still uses legacy enrage/selection; full phase table (65%/30%) and attack sequence not ported.
2. **Support specialists** — repair tender, shield frigate, boarding barge, net cutter, gunner cog formation: logic is **placeholder-level** (not full ally targeting, shield arc, or formation stagger).
3. **Elite variants** — second hammerhead charge and elite kraken sequential traps are **partially** wired.
4. **Playtest harness** — on-screen panel, spawn comparison telemetry, and boss combat context UI **not added**.
5. **Balance lab** — matrix evidence tests may fail after `balanceVersion` bump until lab export recalibrated.
6. **Documentation** — `WEAPONS_ENEMIES_ROSTER.md` not fully rewritten to match new targeting (follow-up).

## Art briefs (production; non-blocking)

See [SALTWIND_COMBAT_ART_BRIEFS.md](./SALTWIND_COMBAT_ART_BRIEFS.md).

## Commands

```powershell
unity test "Pirate Game Unity" --mode EditMode --filter "SaltwindCombatRedesignGateTests" --output editmode-redesign-results.xml
unity test "Pirate Game Unity" --mode PlayMode --filter "SaltwindScene" --output playmode-saltwind-results.xml
```
