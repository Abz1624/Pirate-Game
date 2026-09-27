# M9 research: V3 items and Captains (read-only map)

Paths relative to `Pirate Game Unity/Assets/_Game/`. Collected at the M9 start (commit 33f8b95e).

## Items

### V3ItemCatalog (`Scripts/V3/V3ItemCatalog.cs`)
- `InteractionItemIds` L9–23 (12, the level-up item pool): gunners_gloves, cursed_compass, ghost_lantern, reinforced_keel, captains_bell, black_powder_keg, stormglass, golden_figurehead, leviathan_tooth, powder_fuse, sea_witchs_eye, admirals_ledger. Used by `LevelOfferGenerator` L401, L481; `LevelSystem` L465; F8 hotkeys.
- `PlaytestItemOrder` L25–46 (all 19): the 12 + lucky_doubloon, grapeshot_pouch, double_fuse_cord, serrated_scabbard, broadside_drum, dragons_breath_nozzle, copper_conductor. Used by the menu Collection view.
- `ChestPool` L48–69: Common lucky_doubloon, gunners_gloves · Uncommon ghost_lantern, reinforced_keel, captains_bell, grapeshot_pouch, double_fuse_cord, serrated_scabbard · Rare black_powder_keg, stormglass, golden_figurehead, leviathan_tooth, powder_fuse, sea_witchs_eye, admirals_ledger · Epic cursed_compass, broadside_drum, dragons_breath_nozzle, copper_conductor.
- Display names L71–93, descriptions L95–117 (several descriptions disagree with the code: figurehead "Luck" but gives damage; grapeshot "Deck Cannons damage" but gives global Damage).
- Chest roll: `V3ChestLoot` L51 → `SaltwindStageEconomyLogic.TryPlanChestPurchase` L53–80 (24 attempts) with `SaltwindStageProgressionLogic.IsRunItemOfferEligible` L113–123 (evolution items only when their base weapon is accessible); `V3ContentUnlocks.IsItemUnlocked` L56–66.

### V3RunItemInventory (`Scripts/V3/V3RunItemInventory.cs`)
- Singleton; `Ensure()` L20–26 creates its own GameObject. `TryAddItem` L39–50: stacks uncapped, `_items` keeps acquisition order, `ApplyItemEffect` per copy, telemetry `RecordItemAcquired`, 4 s toast, always returns true. No C# events (LevelSystem raises LoadoutChanged after items, L609–614).
- `ApplyItemEffect` L52–100 (StatModifier on RunManager.Stats): grapeshot Damage ×1.1 · double_fuse AttackSpeed ×1.08 · serrated CritChance +0.04 · broadside_drum Quantity +1 · dragons_breath Size ×1.08 · copper ProjectileSpeed ×1.1 · cursed_compass Luck +0.08 · golden_figurehead GoldGain ×1.05 (ignored in Saltwind: `ShouldApplyGoldGainStat` L118) · stormglass Quantity +0.5 · black_powder CritDamage ×1.1 · sea_witch Luck +0.04 · admirals_ledger Damage ×1.04 · lucky_doubloon **and default** Luck +0.03 (so gloves, lantern, keel, bell, leviathan, powder_fuse each give +0.03 Luck per stack).
- `GetStackCount` L102, `HasItem` L105, `ClearForNewRun` L108–112 (does not remove modifiers; RunManager.BeginRun makes new PlayerStats).

### V3RunItemEffects (`Scripts/V3/V3RunItemEffects.cs`)
- Constants L23–27: bell 12 kills, heading 28°, gloves 2.5 s, lantern 2 s, keel speed 4.2.
- `Update` L65–77 (skipped paused/ended): gloves/lantern timers, `TrackHeadingAndMovement`, `RefreshDynamicMultipliers`.
- Gunner's Gloves L88–102: heading Atan2(fwd.x, fwd.z); |ΔAngle| ≥ 28° → 2.5 s, reference updates only then. `GetAttackSpeedMultiplier` L171–175 = 1.12 while active.
- Powder Fuse L104–114: distance since last hit > 18 → charged. `RefreshDynamicMultipliers` L117–134: keg Explosion ×1.12, sea witch Status ×1.15, leviathan ControlledTarget ×1.2, stormglass ExtraLightningJumps 1, fuse charged Explosion ×1.35.
- `NotifyPlayerTookDamage` L136–140; `NotifyNearMissDodge` L142–146 (lantern crit +0.08 via `GetCritChanceBonus` L177–181) — **no caller**; `NotifyKill` L148–156 (bell, 12th kill → WeaponController.CaptainFireAllWeapons, inert in V4); `NotifyExplosionResolved` L158–161 no caller.
- `GetIncomingDamageMultiplier` L163–169: keel 0.88 when XZ velocity ≥ 4.2. `GetSpawnDensityMultiplier` L183–188 compass 1.18 (unread).
- `GetGlobalDamageMultiplier` L190–207: ledger ×(1 + min(0.18, families × 0.03)) (families = distinct tags[0] or id, `WeaponController` L1703–1719); figurehead ×(1 + min(0.15, gold / 2000)).
- Triggered effects are per item (HasItem), not per stack.
- R-40 unread: ExplosionDamageMultiplier, StatusDamageMultiplier, ControlledTargetDamageMultiplier, ExtraLightningJumps, GetSpawnDensityMultiplier, NotifyExplosionResolved; near-miss never called (item and Captain).
- R-34: `Data/Logbooks/lookouts_glass.asset` CritChance perLevelAdditive 2 (fallback `GameDatabaseLoader` L92); `PlayerStats.CritChance` = min(1, …) (L67). Design source says +2 percentage points.

### Readers
- Keel/`NotifyPlayerTookDamage`: `DamageReceiver.TakeDamage` L69–84 (Brace ×0.4 L74–75, Keel L77–78, then i-frame, HP, notify L81); V4 steady storm `LegacySpawnAdapters.DamageOverTime` L159–179.
- Global damage + crit bonus: V3 `DamageSystem` L39–54; V4 `LegacyWeaponModifiers` (LegacyWeaponAdapters L72–73) → `DamageCalculator` L35–50.
- Attack speed: V3 `WeaponController` L328; V4 `LegacyWeaponModifiers.ItemAttackSpeedMultiplier` L74 → `WeaponManager` L117 (R-22 once).
- NotifyKill: V3 `EnemyWorld` L1582; V4 `LegacyEnemyKillTargets` (LegacyEnemyWorldAdapters L124). V4 fires the Bell through `LegacyActiveProbe` L242–251 (own 12-kill counter on TriggerBus.Kill; FireAllOnce next weapon tick L207–211).
- HasItem/Items/GetStackCount: `WeaponEvolutionCatalog` L85 (evolution gate), `RunSnapshot` L123–128, V3 HUDs, `LegacyLoadoutView` (LegacyViews L143, L163–166), `LegacyActiveProbe` L244. V4 `WeaponEvolutionConfig.requiredItemId` L70 waits for M9.

## Captains

### Profiles
- `V3CaptainId`: Rook 0, Brine 1, Flint 2, Richie 3, Ember 4, Roxie 5; PlayerPref `v3_selected_captain` (`V3CaptainSelection` L19–25, default Rook).
- `V3CaptainProfile.Get` L27–48 (weapon, hull, speed): Brine swivel_guns 110 5.7 · Flint powder_kegs 90 6.3 · Richie swivel_guns 95 6.1 · Ember flamethrower 92 5.9 · Roxie swivel_guns 88 6.5 · Rook swivel_guns 100 6.2. `GameConstants` L22–28 DefaultMoveSpeed 6.2, DefaultHull 100.
- `V3CaptainCatalog`: MenuOrder L9–17 (Rook, Brine, Richie, Flint, Ember, Roxie); labels L19–24 (Brine "Hank", Flint "Sparky"); titles L26–34; passive summary L36–44; menu description L46–60; theme colors L62–70 (033285, 4a5e27, bf1d2c, 75225e, ffac5c, 3485e4); active summary L79–87; `IsLocked` L89–105 (bypass flag; Rook never; Brine needs owned "brine"; Flint needs owned "flint" or First Conquest; Richie/Ember/Roxie never).
- Portraits: `V3PlayerInfoHudArtSettings` `GetCaptainIcon` L39–55 (asset in Resources); menu cards `V3CharacterCardArtSettings`. V4 HUD portrait via `ICaptainView` (`LegacyCaptainView`, LegacyViews L96–102; AuthoredHudPresenter L259–273).
- Unlock costs: stage profile `captainUnlocks` brine 100, flint 140 (marksCost = Rum); `weaponUnlocks` lightning_mast 60, flamethrower 80, mortar 100, harpoon_gun 100. No prerequisite fields.

### V3CaptainController
- `Initialize` L25–36 (LevelChanged subscription), `ApplyBaseStats` L48–61 (SetBase Hull/MoveSpeed, `player.InitializeFromRun(BaseHull)`, Brine armor).
- Brine armor L63–79 `SetArmor(min(25, 0.5 × level))` — never read by `TakeDamage` (R-49).
- `GetAdditiveDamageBonus` L82–95: Rook/Flint 0.005/level cap 0.25; Richie min(0.2, gold/4500) (L97–101); Ember 0.004 cap 0.18; Roxie 0.003 cap 0.15; Brine 0.
- `GetBurnDamageMultiplier` (Ember 1.12) L103–104, `GetLightningDamageMultiplier` (Roxie 1.1) L106–107: no readers (R-49).
- Flint Running Fuse `Update` L109–128: XZ speed ≥ 4 for 2 s → `GetWeaponDamageMultiplier` L131–137 = 1.15 for powder_kegs.
- Richie pickup-radius burst missing (`_richieTimer` never read).
- Readers: `DamageSystem` L31–38; V4 `LegacyWeaponModifiers.CaptainAdditiveDamage/CaptainWeaponMultiplier` L54–70 → `DamageCalculator` L35–36; `Instance == null` checks V3RunBootstrap L293, LegacyRunShell L211.

### V3CaptainActiveController
- Properties L25–33: brace IncomingDamageMultiplier 0.4; keg blast radius ×1.2; Ember flame range ×1.2 and burn ×1.3.
- `Update` L61–77: needs `CombatExperimentContext.CaptainActivesEnabled`; input `WasActivatePressed` L82–88 = Space/Q pressed or gamepad South (conflicts with `ShipBoostController` L37–39, R-49).
- `TryActivate` L90–129: Brine brace 2 s + ApplySlow(0.75, 2); Flint `CaptainArmPowderKegs` + keg radius 4 s; Richie 3 s + damage buff 5 s (+0.1 additive, L131–132) + ApplyMoveEffect(1.35, 3); Ember flame boost 5 s; Roxie ApplyMoveEffect(1.4, 2.5) + `CaptainFireLightningSalvo`; Rook `CaptainFireAllWeapons`. Cooldown, telemetry "captain_active_used", toast.
- Cooldowns `GetDefinition` L140–178: Brace! 20, Light the Fuse 18, Cut and Run 16, Stoke the Flames 18, Ride the Gale 15, All Guns! 18.
- Roxie near-miss refund L134–138: no caller.
- V3 fire paths `WeaponController` L458–515 (salvo searches missing ids, R-88); V4 `WeaponManager` FireAllOnce L229–247, ArmAllKegs L249, FireLightningSalvo L255–273.
- Toggle: `CombatExperimentProfile` L45 PlayerPref `combat_captain_actives_enabled`, default off. V4: `ExperimentConfig.captainActivesEnabled` [AppliesFrom(9)] (unread); TraceBoot L35 pins 0; V4TraceSession L26; V4RunPlayModeTests L19.
- Active HUD: `V3RunCaptainActiveHud` (L78 click, L84–88); V4 hides it when the authored HUD exists (LegacyAdapters L416, L426). No V4 active/cooldown presenter.

### Selection, locks, starting weapon
- `V3MainMenuController` L56–71; `V4MainMenuController` L52–62 (same rules, "until M9"); `V3MainMenuCharacterSelect` lock overlay L374–380, L467–481.
- `V3ContentUnlocks` L18–48: powder_kegs unlocked by First Conquest or Flint; lightning_mast/flamethrower need First Conquest (non-profile path). Key `v3_first_conquest_complete` L11. V4 sets it in `LegacyRewardsAdapter.ApplyConquestUnlockIfSecured` (LegacyAdapters L294–298).
- `V3RunBootstrap.ResolveStartingWeapon` L431–440 (stage profile active → swivel_guns; no First Conquest key → swivel_guns; else profile weapon). V4 copy `LegacyBuildAdapter.ResolveStartingWeapon` (LegacyAdapters L219–227).

## Adopted decisions (quoted sources)
- `docs/GAMEPLAY_FLOW_AND_MODULES.md` §21: L751 First Conquest weapon Swivel Guns; L752 starting weapons (Rook Swivel Guns, Hank Deck Cannons, Richie Broadside Battery, Sparky Powder Kegs, Ember Flamethrower, Roxie Lightning Mast); L755 names/ids; L758 starter six vs Rum unlocks; L765 19-item pool; L768 actives on by default.
- §8: L365–374 Captain table; L376 Ember/Roxie require their weapon owned; L378 one config asset per Captain; L383–386 mismatches; L384 the Saltwind Swivel forcing applies only to Rook and First Conquest.
- `docs/V4_IMPLEMENTATION_PLAN.md` Phase 6 L313–333: L316 items as modifiers/effects; L321 starting weapons, First Conquest forces Swivel; L322 actives default on; L323 "Richie, Ember and Roxie require their weapon unlock; Hank 100 and Sparky 140 Rum"; L331 no item/Captain name comparisons under V4/.
- Richie ambiguity: gameplay doc locks only Ember/Roxie; plan also names Richie, but Broadside Battery is a starter weapon (always unlocked).

## Callers by class
- V3-only: V3RunBootstrap L186–187, 234–238, 257–258, 293, 384–405; EnemyWorld L1582; POIManager L503, 533, 601; V3RunPlaytestHotkeys L194; V3 HUDs; V3RunCaptainActiveHud; WeaponController (record in V4); DamageSystem L31–41 (V4 hellfire still rolls through it).
- Shared code V4 still runs: DamageReceiver.TakeDamage L74–81; LevelSystem.ApplyOffer L609–614; WeaponEvolutionCatalog L85; RunSnapshot L123, 136; ShipBoostController L38.
- V4 adapters: LegacyBuildAdapter (LegacyAdapters L184–257); LegacyRunShell L191–195, 202, 211; LegacyWeaponModifiers L40–78; LegacyActiveProbe L183–258 (reflection on captainId; Brine/Richie/Ember nothing, Flint ArmAllKegs, Roxie salvo, else FireAllOnce); LegacySpawnAdapters L164–165, 170; LegacyEnemyKillTargets L124; LegacyStageEconomy (LegacyStageAdapters L72–130); LegacyCaptainView / LegacyLoadoutView (LegacyViews L96–167).
- V4 consumers via interfaces: StageContentModule (IStageEconomy), DamageCalculator, WeaponManager L117, WeaponBehaviors L589/655/776, IWeaponModifiers (WeaponContracts L60–67), LoadoutHudPresenter L332–345, AuthoredHudPresenter, V4MainMenuController, RunTraceRecorder L169, V4IconCatalogBuilder.
- Tests: V4EnemyPlayModeTests L301–307; V4RunPlayModeTests L19, 300, 496; V4HudM6Tests L32, 169.

## V4 run-start order (LegacyRunShell)
1. L165–167 ledgers, telemetry, `RunManager.BeginRun(seed)` (new PlayerStats).
2. L169 `ResetPowerupsAndItems` (null-safe on the first run).
3. L171–189 layout, world, `SetRunContext` (CaptainId), POIs, enemies, player.
4. L191–193 `EnsureCaptain` (controller Initialize, active controller Initialize + hellfire slot, ResetForNewRun).
5. L195 starting weapon; L196 stage progression.
6. L198–201 weapons record, PrepareLevelSystem, SyncLevelSystemLoadout, `Weapons.Bind()` (tick slot polls LegacyActiveProbe).
7. L202 `EnsureItemSystems` (inventory then effects; same place as V3 L257–258).
8. L204–205 dev tools, run UI.
9. L211–212 hull fallback if no Captain controller.
10. L235 `EnterPlaying`.
