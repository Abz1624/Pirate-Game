using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Debugging;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using HighSeas.Audio;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    // Each adapter wraps one legacy area that V4 still depends on. The attribute names the phase that deletes it;
    // DeletedInPhaseTests fails if an adapter outlives that phase. Method bodies are ported from V3RunBootstrap
    // with the call order unchanged, so M0 runs are identical to V3 runs.

    /// <summary>Stage balance profile context (V3 asset in M0; runtime profile built from V4 config from M1).</summary>
    [DeletedInPhase(14, "Profile context readers are gone after switch-over")]
    public sealed class LegacyProfileAdapter
    {
        /// <summary>
        /// Begins the stage profile context with the per-run profile built from V4 config (M1; replaces the scene's
        /// StageBalanceProfileHost). Validated with the same rules the V3 resolver applies.
        /// </summary>
        public bool TryBegin(StageBalanceProfile runtimeProfile, out string error)
        {
            error = null;
            StageBalanceProfileContext.EndRun();
            if (V3RunSession.IsLocalMultiplayer) return true;
            if (runtimeProfile == null) { error = "V4Game: no stage profile (config set or legacy base profile missing)."; return false; }
            var validation = StageBalanceProfileValidator.Validate(runtimeProfile);
            if (!validation.IsValid) { error = validation.ErrorMessage; return false; }
            StageBalanceProfileContext.BeginRun(runtimeProfile);
            return true;
        }

        /// <summary>
        /// Rebuilds the profile snapshot after a live config edit. Readers of Authoring see the edited runtime profile
        /// at once; the snapshot is rebuilt and the chest purchase count replayed so chest prices do not reset.
        /// </summary>
        public void Rebuild(StageBalanceProfile runtimeProfile)
        {
            if (!StageBalanceProfileContext.IsActive) return;
            var purchases = StageBalanceProfileContext.Runtime?.SuccessfulChestPurchases ?? 0;
            StageBalanceProfileContext.BeginRun(runtimeProfile);
            for (var i = 0; i < purchases; i++) StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
        }

        public void EndIfActive()
        {
            if (StageBalanceProfileContext.IsActive)
                AppRoot.EndActiveSaltwindRunContext();
        }
    }

    /// <summary>Game database load and the runtime catalogs. M0 applies them exactly as V3 does.</summary>
    [DeletedInPhase(9, "Enemies (M2), weapons (M5) and logbooks (M9) come from V4 configs")]
    public sealed class LegacyDatabaseAdapter
    {
        public GameDatabase Database { get; private set; }

        public void Load(GameDatabase serialized)
        {
            Database = serialized;
            if (Database == null || !GameDatabaseLoader.IsValid(Database))
                Database = GameDatabaseLoader.Load();
        }

        public void ApplyCatalogs(CombatExperimentProfile experiment)
        {
            CombatWeaponCatalog.ApplyToDatabase(Database, experiment);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(Database, experiment);
        }
    }

    /// <summary>World generation, player spawn placement, bounds and V3 run context.</summary>
    [DeletedInPhase(14, "Becomes WorldModule over the shared StageBuilder")]
    public sealed class LegacyWorldAdapter
    {
        readonly StageBuilder _stageBuilder;
        readonly IslandLayoutDefinition _islandLayout;

        public LegacyWorldAdapter(StageBuilder stageBuilder, IslandLayoutDefinition islandLayout)
        {
            _stageBuilder = stageBuilder != null ? stageBuilder : Object.FindFirstObjectByType<StageBuilder>();
            _islandLayout = islandLayout;
        }

        public StageBuilder StageBuilder => _stageBuilder;

        public static void DisableEnvironmentalHazards()
        {
            EnvironmentalHazardsDisabled.Set(true);
            foreach (var juice in Object.FindObjectsByType<PlayerJuiceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                juice.DisableSeaFoamTrail();
            foreach (var wind in Object.FindObjectsByType<WindGustDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(wind);
            foreach (var visual in Object.FindObjectsByType<EnvironmentHazardVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(visual.gameObject);
            foreach (var current in Object.FindObjectsByType<OceanCurrentZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(current.gameObject);
            foreach (var pool in Object.FindObjectsByType<WhirlpoolHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pool.gameObject);
            var root = GameObject.Find("EnvironmentHazards");
            if (root != null) Object.Destroy(root);
        }

        public static void EnsureTouchJoystickActive()
        {
            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null && !joystick.activeSelf) joystick.SetActive(true);
        }

        public static void EnsureGameplayBootstrapComponents()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        public IslandLayoutDefinition ResolveIslandLayout()
        {
            if (_islandLayout != null && _islandLayout.HasIslands) return _islandLayout;
            var fromResources = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            if (fromResources != null && fromResources.HasIslands) return fromResources;
            return IslandLayoutDefinition.LoadAssignedOrDefault(null);
        }

        public void ConfigureStageBuilder()
        {
            if (_stageBuilder == null) return;
            _stageBuilder.ConfigureV3Layout(ResolveIslandLayout());
            _stageBuilder.SetProceduralIslandSettings(ProceduralIslandSettings.Default.Clamped());
            _stageBuilder.SetIslandDensityMultiplier(1f);
        }

        public StageLayout Build(int seed) => _stageBuilder != null ? _stageBuilder.Build(seed, 0) : null;

        public static void ResetWorldVisuals()
        {
            V3StormEdgeVisual.DisableWorldRings();
            PlayBounds.ResetStormSafeRegion();
            DisableEnvironmentalHazards();
            V3SaltwindStormRingVisual.Disable();
            V3PlayBoundsWallVisual.Ensure();
        }

        public static void SetRunContext(StageLayout layout)
        {
            V3RunContext.Layout = layout;
            V3RunContext.CaptainId = V3CaptainSelection.Selected;
        }

        public void PlacePlayerAtSafeSpawn(PlayerShipController ship, StageLayout layout, int seed)
        {
            const float hullRadius = 1.15f;
            Physics.SyncTransforms();
            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(layout, ResolveIslandLayout(), seed, hullRadius);
            if (IslandObstacles.IsAvailable)
            {
                var height = IslandObstacles.GetCollisionHeight(hullRadius);
                for (var i = 0; i < 8; i++)
                {
                    if (!IslandObstacles.IsPositionBlocked(spawn, hullRadius)) break;
                    spawn = IslandObstacles.Depenetrate(spawn, hullRadius, height);
                }
            }

            ship.SetWorldPosition(spawn);
        }
    }

    /// <summary>Legacy enemy, projectile, pickup and status worlds: reset, definitions and the per-frame tick.</summary>
    [DeletedInPhase(8, "V4 Enemy World")]
    public sealed class LegacyEnemyWorldPort
    {
        public static void ClearTransientWorlds()
        {
            Object.FindFirstObjectByType<PickupWorld>()?.ClearAllActive();
            Object.FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            Object.FindFirstObjectByType<HostileProjectileWorld>()?.ClearAllActive();
        }

        public static EnemyWorld ResetAndDefine(GameDatabase database)
        {
            Object.FindFirstObjectByType<EnemyWorld>()?.ResetAllAgentsForPromo();
            ClearTransientWorlds();
            var enemyWorld = Object.FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);
            return enemyWorld;
        }

        public static void EnsureHostileProjectiles()
        {
            if (Object.FindFirstObjectByType<HostileProjectileWorld>() != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            systems.AddComponent<HostileProjectileWorld>();
        }

        /// <summary>Same calls, same order as V3RunBootstrap.Update.</summary>
        public static void Tick(float dt, PlayerShipController player)
        {
            var playerPos = player != null ? player.Position : Vector3.zero;
            Object.FindFirstObjectByType<BarnacleKingBossAttackDirector>()?.Tick(dt);
            Object.FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            Object.FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, Object.FindFirstObjectByType<EnemyWorld>());
            Object.FindFirstObjectByType<HostileProjectileWorld>()?.Tick(dt, player?.Damage);
            Object.FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            V3FloorPowerupWorld.Instance?.Tick(dt, playerPos);
            Object.FindFirstObjectByType<StatusController>()?.Tick(dt, Object.FindFirstObjectByType<EnemyWorld>());
        }
    }

    /// <summary>Captains, items, powerups, hellfire patches and LevelSystem.</summary>
    [DeletedInPhase(9, "Build & Progression modules")]
    public sealed class LegacyBuildAdapter
    {
        public static void ResetPowerupsAndItems()
        {
            V3FloorPowerupWorld.Ensure();
            V3FloorPowerupWorld.Instance?.ResetForRun();
            V3RunItemInventory.Instance?.ClearForNewRun();
            V3RunItemEffects.Instance?.ResetForNewRun();
        }

        public static void EnsureCaptain(V3CaptainId captainId, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var controller = systems.GetComponent<V3CaptainController>();
                if (controller == null) controller = systems.AddComponent<V3CaptainController>();
                controller.Initialize(captainId, ship);
            }

            systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var active = systems.GetComponent<V3CaptainActiveController>();
                if (active == null) active = systems.AddComponent<V3CaptainActiveController>();
                active.Initialize(captainId);
                HellfirePatchWorld.Ensure();
            }

            V3CaptainActiveController.Instance?.ResetForNewRun();
        }

        public static string ResolveStartingWeapon(V3CaptainProfile profile)
        {
            if (StageBalanceProfileContext.IsActive && !V3PlaytestFlags.SaltwindDebugUnlockAll)
                return "swivel_guns";
            if (!V3PlaytestFlags.UnlockAllContent && !PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey))
                return "swivel_guns";
            var starter = profile.StartingWeaponId;
            return string.IsNullOrEmpty(starter) ? "swivel_guns" : starter;
        }

        public static void ApplyStageBalanceProgression() => LevelSystem.Instance?.ApplyStageBalanceProgression();

        /// <summary>Ensure and configure LevelSystem. The weapon-record sync that follows is done by V4LegacyBridge.</summary>
        public static void PrepareLevelSystem(GameDatabase database, CombatExperimentProfile experiment, string startingWeapon)
        {
            if (LevelSystem.Instance == null)
            {
                var systems = GameObject.Find("Systems");
                if (systems != null && systems.GetComponent<LevelSystem>() == null)
                    systems.AddComponent<LevelSystem>();
            }

            LevelSystem.Instance?.ConfigureV3();
            LevelSystem.Instance?.ApplyCombatExperiment(experiment);
            LevelSystem.Instance?.PrepareForNewRun(database, startingWeapon);
        }

        public static void EnsureItemSystems()
        {
            V3RunItemInventory.Ensure();
            V3RunItemEffects.Ensure();
        }

        public static void CancelPendingLevelUps()
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            LevelUpModal.Instance?.Hide();
        }
    }

    /// <summary>POIManager, barrels, boss attack director and storm edge damage.</summary>
    [DeletedInPhase(7, "StageContent and BossDirector")]
    public sealed class LegacyStageAdapter
    {
        public static void InitializePoi(StageLayout layout, GameDatabase database, bool usePlaceholderVisuals, bool isPvPScene)
        {
            var poi = Object.FindFirstObjectByType<POIManager>();
            if (poi == null) return;
            if (isPvPScene) { poi.enabled = false; return; }
            poi.enabled = true;
            poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: usePlaceholderVisuals, useV3ChestPricing: true);
            poi.Initialize(layout, database);
            if (layout != null) V3ExplosiveBarrelWorld.Ensure(layout);
        }

        public static void ResetBossDirector(EnemyWorld enemyWorld, PlayerShipController ship) =>
            BarnacleKingBossAttackDirector.Ensure(enemyWorld, ship).ResetForRun();

        /// <summary>
        /// Module Lab: POIs, hold-to-interact and the B-key summon are off, so chests, the powder-cache ambush and the
        /// Beacon boss cannot spawn anything (a boss summon runs the spawner's trigger queue even while it is stopped).
        /// </summary>
        public static void EnterModuleLab()
        {
            foreach (var poi in Object.FindObjectsByType<POIManager>(FindObjectsSortMode.None)) poi.enabled = false;
            foreach (var interaction in Object.FindObjectsByType<V3InteractionController>(FindObjectsSortMode.None)) interaction.enabled = false;
            foreach (var hotkey in Object.FindObjectsByType<V3BossSummonHotkey>(FindObjectsSortMode.None)) hotkey.enabled = false;
        }

        public static void TrySecureIfBossDead() => Object.FindFirstObjectByType<POIManager>()?.TrySecureIfBossDead();
    }

    /// <summary>Marks tracking, settlement, telemetry, damage ledger and the death finalize path.</summary>
    [DeletedInPhase(12, "RewardModule")]
    public sealed class LegacyRewardsAdapter
    {
        public static void TeardownTransientRunState()
        {
            RunEncounterTracker.ResetForNewRun();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
            LegacyEnemyWorldPort.ClearTransientWorlds();
        }

        public static void ResetLedgers()
        {
            WeaponDamageLedger.Reset();
            WeaponEvolutionRuntime.Reset();
        }

        public static void CreateTelemetry(CombatExperimentProfile experiment) => _ = new RunCombatTelemetry(experiment);

        public static void BeginRunTracking()
        {
            SaltwindCombatEncounterBudget.ResetForRun();
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            RunMarksTracker.BeginRun(RunManager.Instance.RunId);
            SaltwindRunSettlement.ClearLastResults();
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                var ex = StageBalanceProfileContext.Export;
                RunCombatTelemetry.Instance?.RecordStageProfileStart(ex.ProfileId, ex.BalanceVersion.ToString(), ex.ContentHash, RunManager.Instance.RunId);
            }
        }

        public static void EnsureTelemetryDriver()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<CombatTelemetryDriver>() == null)
                systems.AddComponent<CombatTelemetryDriver>();
        }

        public static void ApplyConquestUnlockIfSecured()
        {
            if (RunManager.Instance != null && RunManager.Instance.ObjectiveSecured)
                PlayerPrefs.SetInt(V3ContentUnlocks.FirstConquestKey, 1);
        }

        public static void RecordDamage(float amount)
        {
            HighSeas.Meta.TriggerBus.Instance?.RaiseDamageTaken(amount);
            RunCombatTelemetry.Instance?.RecordDamageTaken(amount);
        }

        /// <summary>V3RunBootstrap.OnPlayerDied (solo branch; the PvP branch is kept for parity).</summary>
        public static void OnPlayerDied(PlayerShipController player)
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            if (player != null) player.SetMovementEnabled(false);
            if (V3RunSession.IsPvP)
            {
                RunManager.Instance.SetState(RunState.PlayerDead);
                RunManager.Instance.FinalizeRunOnDeath();
                V3ToastHud.Instance?.Show("You were sunk — the rival wins.", 4f);
                return;
            }

            LegacyStageAdapter.TrySecureIfBossDead();
            ApplyConquestUnlockIfSecured();
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.FinalizeRunOnDeath();
            V3OnboardingDirector.RecordRunCompleted();
            RunCombatTelemetry.Instance?.RecordPlayerDeath(RunManager.Instance.ObjectiveSecured);
            V3ToastHud.Instance?.Show("Run ended — gold banked.", 4f);
            if (StageBalanceProfileContext.IsActive && SaltwindRunSettlement.LastSettlementApplied)
                V3ToastHud.Instance?.Show($"Marks settled: +{SaltwindRunSettlement.LastMarksAwarded}", 4f);
        }
    }

    /// <summary>V3 UI bootstrap, VFX, playfeel, debug tools and toasts.</summary>
    [DeletedInPhase(14, "All presentation is V4 after switch-over")]
    public sealed class LegacyPresentationAdapter
    {
        public static void EnsureDevAndDebugTools(bool firstRun)
        {
            if (firstRun)
            {
                V3RunPlaytestHotkeys.Ensure();
                // SpawnDirectorDebugUI (F9) is not created from M3: it rewrote the legacy spawner's base rate every
                // frame (register R-71), and the legacy spawner no longer runs ambient spawns.
                if (V3PlaytestFlags.EnableRunPlaytestHotkeys) WeaponCardAnimationDemo.Ensure();
                V3BossSummonHotkey.Ensure();
            }

            EnemyIdentityDebugLabels.Ensure();
            WeaponTargetingVisualizer.Ensure();
        }

        public static Canvas EnsureRunUi(StageLayout layout, bool firstRun)
        {
            Canvas runCanvas;
            if (firstRun)
                runCanvas = V3RunUiBootstrap.EnsureAll(layout);
            else
            {
                V3DiscoverySystem.Ensure(layout);
                runCanvas = Object.FindFirstObjectByType<RunHud>()?.GetComponentInParent<Canvas>();
                if (runCanvas != null) V3MinimapHud.Ensure(runCanvas.transform, layout);
            }

            runCanvas?.GetComponent<RunHud>()?.ForceRefresh();
            return runCanvas;
        }

        public static void EnsurePlayerHealthBar(PlayerShipController ship)
        {
            if (ship == null) return;
            if (ship.GetComponent<PlayerHealthBarBinder>() == null)
                ship.gameObject.AddComponent<PlayerHealthBarBinder>();
        }

        public static void EnsureCombatVfx()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<JuiceVfxBroker>() == null) systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null) systems.AddComponent<CombatVfxController>();
            var damageNumbers = systems.GetComponent<FloatingDamageNumbers>();
            if (damageNumbers == null) damageNumbers = systems.AddComponent<FloatingDamageNumbers>();
            damageNumbers.Rebind();
            if (systems.GetComponent<WeaponJuiceController>() == null) systems.AddComponent<WeaponJuiceController>();
            if (systems.GetComponent<PlayerJuiceController>() == null) systems.AddComponent<PlayerJuiceController>();

            var vfxRoot = systems.transform.Find("VFXRoot");
            if (vfxRoot == null)
            {
                var go = new GameObject("VFXRoot");
                go.transform.SetParent(systems.transform, false);
                vfxRoot = go.transform;
            }

            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null) broker.Configure(settings, vfxRoot);
            systems.GetComponent<WeaponJuiceController>()?.Configure(settings, vfxRoot);
            if (systems.GetComponent<CameraShakeService>() == null) systems.AddComponent<CameraShakeService>();
            if (systems.GetComponent<SfxService>() == null) systems.AddComponent<SfxService>();
            if (systems.GetComponent<V3SpawnWarningHud>() == null) systems.AddComponent<V3SpawnWarningHud>();
        }

        public static void EnsurePlayfeelSystems(PlayerShipController ship, int seed)
        {
            V3OnboardingDirector.Ensure()?.ResetForRun();
            LegacyWorldAdapter.DisableEnvironmentalHazards();

            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var dayNight = systems.GetComponent<DayNightDirector>();
                if (dayNight == null) dayNight = systems.AddComponent<DayNightDirector>();
                dayNight.BeginRun(seed);
            }

            if (ship == null) return;
            var boost = ship.GetComponent<HighSeas.PlayerSystems.ShipBoostController>();
            if (boost == null) boost = ship.gameObject.AddComponent<HighSeas.PlayerSystems.ShipBoostController>();
            boost.Bind(ship);
            if (ship.GetComponent<HighSeas.PlayerSystems.BoostCameraZoom>() == null)
                ship.gameObject.AddComponent<HighSeas.PlayerSystems.BoostCameraZoom>();
        }

        public static void ShowStartToasts(bool firstRun, int seed)
        {
            if (firstRun && V3PlaytestFlags.EnableRunPlaytestHotkeys)
                V3ToastHud.Instance?.Show("Playtest: F8 hotkeys · G weapon card scatter · H card scroll", 5f);

            var seedLabel = firstRun && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            V3ToastHud.Instance?.Show(
                firstRun
                    ? $"Saltwind voyage ({seedLabel} seed {seed}).\nFind salvage chests as you bank gold, secure the Beacon."
                    : $"New voyage (seed {seed}).\nIslands, chests, and spawn rerolled.",
                firstRun ? 6f : 5f);
        }
    }
}
