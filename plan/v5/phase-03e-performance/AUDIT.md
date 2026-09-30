# M3d performance audit (1 Oct 2026, read-only survey of V4)

Target: WebGL mobile and Android. Paths are under `Pirate Game Unity/Assets/_Game/V4/` unless noted.

## Pooling (owner note d)

Everything spawned per gameplay event is already pooled (`Presentation/Pooling/PoolService.cs`): enemy views, player and
enemy projectile views, pickups, weapon VFX, hit/kill effects, damage numbers, health bars, kegs, skiffs. Simulation
entities are fixed struct arrays. Gaps:

- Pools grow without a cap (`Grow`): a spike past prewarm is a runtime `Instantiate` (`PoolService.cs:280`).
- `VfxProfilePlayer` registers profile effects and trails with prewarm 0: first uses instantiate mid-run.
- `FlameStreamPresenter` instantiates the whole flame prefab on first use (mid-run hitch).
- `HazardRingPresenter` grows timed rings with `new GameObject` beyond 8.
- `MinimapMarkerGraphics.Apply` destroys and recreates glyph Images on every kind change.
- First acquire of each pooled instance strips components (`DestroyImmediate`, `GetComponentsInChildren`) in
  `EnemyViewPresenter`, `ProjectileViewPresenter`, `HostileShotPresenter`, `PickupPresenter`, `WeaponVfxPresenter`:
  move the strip into prewarm.
- `WorldHealthBar` destroys itself when its target is null (a pooled bar is lost).

## Garbage and CPU in per-frame paths (note d)

1. `Foundation/Enemies/EnemyShipWakeJuice.cs:21-106`: per enemy per frame `GetComponentsInChildren` arrays and `name`
   strings: the largest GC source. Cache per view (as `ShipWakePresenter` does).
2. `Runtime/Enemies/SpatialHash.cs:20,27`: `Clear()` drops the cell lists, so every insert allocates a `List<int>`;
   rebuilt every tick and on every `FindNearest`. Keep and clear the lists.
3. `Config/Core/ConfigSetRuntime.cs:75-76`: `Single<T>`/`All<T>` use LINQ over ~109 configs; called per frame from
   `WeaponManager`, `WeaponModule`, `SpawnServices` (Timeline/Difficulty/Experiment), `FlowDirector`, `AmbientSpawner`,
   `EnemyWorldModule`. Cache by type.
4. `Runtime/Composition/EnemyWorldModule.cs:178`: two closures per frame.
5. `Runtime/Spawning/TimelineTriggers.cs:28`: `Kind.ToString()` per combat event per tick.
6. HUD: `LoadoutHudPresenter` runs and builds strings every frame while hidden; `AuthoredHudPresenter` timer string and
   layout every frame; `MinimapPresenter` `GetComponentInParent` and RectTransform moves every frame;
   `UiCanvasConfigurator.ApplyScale` every frame.
7. `Presentation/Hud/MinimapPresenter.cs:384-498`: the fog mask is rebuilt (up to ~0.5M HashSet lookups + texture
   upload) on every newly explored cell.
8. Per-effect `GetComponentsInChildren` in `PooledEffects`, projectile/hostile tint (`renderer.material`,
   `HasProperty(string)`), `VfxParticleUtil`, `VfxProfilePlayer` (`BroadcastMessage`), `PickupPresenter.AlignBarrel`,
   `EnemyViewAnimation`.
9. `GetComponent` per enemy per frame (`EnemySimulation.Tick.cs:57,132,173`, `EnemyCombatBehaviors.cs:179`,
   `EnemySpecialistCombat.cs:93`, `EnemyViewPresenter.cs:122`), `EnemyBurnFlash` material instance per frame;
   `DemoMode` properties call `Resources.Load` on every access (`AutoBossSeconds` every frame).
10. Telemetry dictionaries per event; `ScriptedPhases` pulse key strings.

Algorithmic hot spots (WebGL is single-threaded):
- `EnemySimulation.Tick.cs:214-249` `ResolveAgentOverlaps`: O(capacity²) × 2 passes over all 260 slots per frame; use
  the spatial hash.
- `PlayerProjectiles.cs:120-139`: every projectile against all 260 enemy slots plus a `Linecast` and a capsule check.
- `WeaponTargetingService.cs:236-245`: densest-cluster search O(cap²).
- Island queries: 6-10 physics queries per enemy per frame (avoidance casts, capsule resolve, `Depenetrate` twice).
- Hit feedback plays a 5-system spark per hit including burn ticks every frame (`CombatFeedbackPresenter.OnHit`);
  cannon hits play a 6-system explosion per hit.

## Batching (notes f, i)

- SRP Batcher on (mobile and PC URP assets); dynamic batching off; PlayerSettings static batching off on WebGL, on for
  Android. GPU instancing mostly moot (SRP Batcher wins; creatures are skinned).
- Batching breakers: per-instance `renderer.material` copies with `EnableKeyword` (projectile and hostile tint, burn
  flash, combat feedback, pickups, POI markers); `MaterialPropertyBlock` in `VfxProfilePlayer` tint.
- Islands: 45-50 spawned at runtime, each 1 renderer × 6 SimpleLit materials, shadows on, no LOD, not static:
  `StaticBatchingUtility.Combine(islandRoot)` after the stage is built. ~110 authored props in the run scene are destroyed
  at run start (load cost only).
- Enemy health bars: a world-space Canvas per enemy (up to 80+): one batch each, rebuilt on fill change, billboarded
  every frame. Damage numbers: legacy `TextMesh` (not SRP-Batcher compatible). Damage highlight: a non-URP shader
  sphere with shadows, one per hit.

## Culling (note j)

- No baked occlusion data; camera occlusion flag on but unused. Top-down angled camera (offset 0,25,-20, pitch 56),
  open sea, runtime-generated islands: occlusion culling cannot help (no static occluders to bake; GPU occlusion needs
  compute, absent on WebGL).
- Camera far clip is 1000 m while the view spans ~32 m around the ship: cut the far clip, add per-layer cull distances.

## Other mobile quick wins

- Two realtime directional lights in the run scene (the second at 0.39 becomes a per-pixel additional light): remove.
- Point lights (intensity 0) inside the keg and mortar explosion prefabs: remove.
- Mobile URP asset requires the opaque texture; the active water only needs depth: turn it off.
- Bloom high-quality filtering on mobile: off / skip iterations.
- Shadows cast by pickups (up to ~200 pooled barrels), kegs, cutlasses, muzzle gun, damage sphere: turn off.
- Particles: the player ship prefab has 48 particle systems; Lightning Mast 15; Explosion_Large 17; many max 1000.
- Muzzle prefab is a full gun with 14 renderers and 14 colliders.
- Textures: 1145 of 1171 at 2048 with no mobile overrides; WebGL uses DXT (decompressed on phones).
- Prewarm: ~300 inactive enemy instances + 80 health-bar canvases + ~200 barrels at load.
- Physics: full collision matrix; `PerfProbe` logs every 5 s on mobile; `SfxService.PlayPlaceholder` logs every boost.
