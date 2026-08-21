# Project High Seas — Implementation Status

Last updated: 2026-08-21

## Completed

### Phase 0 — Cleanup & Skeleton
- Removed `onchain/`, onchain CI, `magicblock-test-storage/`, `target/`
- Retired old prototype scripts/scenes/prefabs
- Created `Assets/_Game/` structure with asmdefs, 5 scenes, GameDatabase asset
- Editor bootstrap (`HighSeas/Bootstrap Project`) and build scripts
- `IMPLEMENTATION_STATUS.md`, `NOTES.md`, batch tooling in `tools/`

### Phase A — Core Loop (Tasks 0–6)
- AppRoot, RunManager FSM, RunRng, PlayerStats, SaveService
- Kinematic ship controller, follow camera, keyboard + virtual joystick
- EnemyWorld (pooled agents, spatial hash, steering, contact damage)
- ProjectileWorld + WeaponController (Deck Cannons, Broadside Battery, Flying Cutlasses + 3 more weapons)
- PickupWorld (Infamy/Gold, merge-on-cap), LevelSystem, level-up modal, Run HUD
- SpawnDirector (threat budget, elites, miniboss timing)
- EditMode tests: 8/8 passing

### Phase B — Buildcraft (Tasks 7–8)
- Powder Kegs, Lightning Mast, Burning Oil weapon patterns
- 8 MVP Logbooks in GameDatabase
- StatusController (Burn/Tarred/Breach/Shock/Curse)
- TriggerBus, LootService, 12 MVP Relics

### Phase C — World & Economy (Tasks 9–10)
- StageGenerator (14×14 grid, reachability validation, POI placement)
- POIManager (Salvage Chest, Lighthouse, Trial Buoy, Boss Beacon)
- Chest price curve via SpawnDirector

### Phase D — Boss & Meta (Tasks 11–12)
- Barnacle King boss spawn via Boss Beacon
- Miniboss (Reef Crab) at 30%/72% stage time
- Dread Tide overtime via threat formula + HUD label
- Run end screen, Doubloon payout, QuestService scaffold, save migration

### Phase E — Platform & Polish (Tasks 13–14)
- QualityProfile caps (Low/Mid/High)
- Test_Performance scene + PerformanceHarness (F9 stress spawn)
- Tutorial overlay, accessibility settings hooks
- WebGL + Android build menu items (`HighSeas/Build/WebGL`, `HighSeas/Build/Android`)

## Verification
- Bootstrap: `tools/bootstrap-highseas.bat` or Unity menu **HighSeas → Bootstrap Project**
- Tests: `tools/run-highseas-tests.bat` (8 EditMode tests)
- Play: Open **Run** scene or Boot → MainMenu → Play
- WebGL: `tools/build-webgl.bat` or **HighSeas → Build/WebGL**

## Known Issues
- Player ship uses placeholder capsule; imported ship FBX can be wired in bootstrap later
- Unity batch tests require omitting `-quit` before `-runTests`
- WebGL build time varies; first build compiles all shaders

## Next Steps (post-MVP)
- Wire pirateship FBX visual to PlayerShip prefab
- Unlocks/Quests UI screens
- Audio/VFX placeholder polish pass
- Device profiling on Android hardware
