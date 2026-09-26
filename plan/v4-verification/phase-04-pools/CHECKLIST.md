# M4 — Pool Service, Pool Catalog, Slot Pools

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `bd4d0f51` (M1–M3 passed) |
| Status | Implemented, reviewed and verified. Awaiting your playtest |

## What M4 delivers

**Slot pools for the simulation** (`Runtime/Core/SlotPool.cs`): a fixed-capacity struct array with a free-list stack and a dense active list. Allocate and free are O(1), iteration touches active slots only, nothing allocates after construction, and a per-slot generation tells a reused slot from the original. `SlotPoolCapacities` gives V3's sizes per quality tier (enemies 160/260/420, player projectiles 280/500/900, hostile shots 96, pickups 180). Their users arrive with the ports: player projectiles in M5, enemies and hostile shots in M8, pickups in M9.

**The view pool** (`Presentation/Pooling/PoolService.cs`): keyed pools with prewarm, cap and an overflow policy per key: **Grow** (creates anyway and counts it as "created after prewarm"), **RecycleOldest** (takes back the instance acquired first), **Reject** (refuses). Counters per pool: prewarmed, total, in use, available, peak, created after prewarm, recycled, rejected, lost. Acquire and release allocate nothing (each instance keeps its list node and its `IPoolable` hooks). Prewarm instantiates under an inactive staging object, so no Awake, OnEnable or play-on-awake effect runs until first use. An instance destroyed from outside is dropped and counted as lost.

**The pool catalog** (`Config/Schemas/PoolCatalogConfig.cs`, data `V4/Data/Pools/SaltwindPools.asset`, in the config set): explicit entries, plus one pool per enemy model the wave timeline can spawn (spawner anchors, overtime alternates, pool entries, focus enemies and variants), keyed `enemy/<model>`. Enemies sharing a model share its pool. Per-archetype `visual.poolPrewarm` overrides the default (12, or 1 for boss and miniboss models).

**The presentation root** (`Presentation/V4PresentationRoot.cs`): one per run, installed on `V4Game.RunStarted` on its own object (like the console), builds the pool from the catalog.

**Tuning Console → Pools**: every pool with source (hover), prewarm, cap, policy, use, peak and creates after prewarm; the header shows pools in use (and creates after prewarm, if any); the creation probe toggle and its top sources; in the Module Lab, a **Pool stress** button.

**Creation probe** (`DevTools/Diagnostics/SceneObjectCreationProbe.cs`): while on, counts object trees created outside the pool each frame, grouped by name. Off by default (it scans the scene every frame). Limits: objects created and destroyed within one frame are not seen.

**Module Lab stress** (`DevTools/Diagnostics/PoolStressScenario.cs`): 5 s of churn keeping each lab pool at its prewarm count, then 2 s past each cap so the policies act.

## Deviations from the task list (recorded)

| Plan | M4 | Why |
|---|---|---|
| Catalog derives weapon projectile, muzzle and impact keys from the offer pool | Enemy view keys only; weapon keys come in M5 | Weapons have no visual prefabs in V4 until the M5 weapon presenter |
| Prewarm the derived entries | Listed but not prewarmed (`prewarmEnemyViews` off, "applies from M8") | Nothing renders through the pool before M5/M8; prewarming would only cost memory and scene objects |
| Doc: "moves in the four view pools", ten-minute run with 0 creates | Infrastructure only | As planned in §3: pools are adopted by M5–M9, the zero-create proof is M14. The probe records today's legacy baseline |

## Legacy reader audit

M4 removes or replaces no legacy component. New run objects: `V4Presentation` (with `Pools` and an inactive `PoolStaging`), no pooled instances. The console and probe are not installed during trace runs.

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 96 / 96. New: slot pool allocate/refuse, free/reuse/generation, active iteration, capacities per tier; view pool prewarm, Grow, RecycleOldest (with release hooks), Reject, release and foreign objects; catalog derivation (one pool per model, not prewarmed, boss counts), validator | `editmode-results.json` |
| V4 PlayMode | 17 / 17. New: Module Lab stress (0 creates while churning, recycle at the cap, grow by 16 past the cap); a run has its pool and creates nothing; the probe counts objects outside the pool and leaves pooled ones out | `playmode-results.json` |
| Traces (end of phase, short set) | V4 with the v3-parity preset == V3 goldens on seeds 101/202/303 (MATCH ×3); replay MATCH. Default runs: new baseline; identical to the M1–M3 playtest default until overtime (the Drowned stats and ramp) | `trace-comparison.txt`, `player/` |
| Playable check | Module Lab stress from the Pools section: 11,592 acquires, 0 created after prewarm while churning, 1,880 recycled, 16 grown, 0 rejected. Run: 12 derived pools, 0 prewarmed, 0 pooled instances. Console: 0 errors | `playable-check/` |
| Legacy creation baseline (M14 target 0) | From 4:10 for 63 s: 19,685 objects, 312/s; 19,325 are minimap `Circle` images (R-86). After killing the Reef Crab miniboss: 76/s, still mostly spawn-ping markers. The rest: damage numbers, health bars, explosion VFX, enemy models, pickups | `playable-check/steps.log` |
| Config set hash | `89322b25…` (53 configs; changed by the catalog and `poolPrewarm`) | `config-hash.txt` |

## Independent review

No blockers. Fixed: destroyed in-use instances (leaked, then threw on recycle); per-acquire allocations; prewarm running Awake/OnEnable; catalog edits looking live (now "restart" / "applies from M8"); duplicate derived keys; stress numbers on a second run and resetting other pools' peaks; `AppRoot` use outside Legacy (now `QualityProfile.Current`); a failing console task throwing every frame; the probe's rate in its first 10 s; a probe test added; missing evidence (this checklist, trace comparison, config hash, doc status). The TMP font side effect is reverted before the commit. Found while verifying: the Pools section's long note widened the console and clipped the section titles (fixed, screenshot `03`).

## Your playtest

1. Play a normal run: nothing should look or feel different from M1–M3.
2. F1 → Pools: 12 `enemy/...` pools, all 0 (they get used in M8). Tick "Count objects created outside the pool" for a minute: you'll see the legacy creation rate and its top sources (the minimap `Circle` markers, R-86).
3. Module Lab scene → F1 → Pools → **Pool stress**: after 7 s the summary should read 0 created after prewarm, with recycled and 16 grown.
