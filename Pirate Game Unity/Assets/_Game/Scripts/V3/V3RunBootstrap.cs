using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Debugging;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3.MultiShip;
using HighSeas.Audio;
using HighSeas.VFX;
using HighSeas.Weapons;
using HighSeas.Save;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// V3 run bootstrap: post-boss survival, 100% gold banking at death, tropical island layout.
    /// Ship controller and camera values remain scene-authored (copied from Run.unity).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class V3RunBootstrap : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] int runSeed = GameConstants.DemoRunSeed;
        [SerializeField] bool forceInspectorSeed;

        public int EditorRunSeed => runSeed;
        [SerializeField] bool usePlaceholderPoiVisuals = true;

        StageLayout _layout;
        static V3RunBootstrap _active;
        bool _runSystemsWired;

        void Awake()
        {
            _active = this;
            EnvironmentalHazardsDisabled.Set(true);
            DisableEnvironmentalHazards();
            AppRoot.EnsureInitialized();
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
            var multiConfig = FindFirstObjectByType<V3MultiShipSceneConfig>();
            if (multiConfig != null)
                V3RunSession.Configure(multiConfig.SessionKind);
            EnsureTouchJoystickActive();
            EnsureGameplayBootstrapComponents();
        }

        static void EnsureGameplayBootstrapComponents()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<PlayerShipVisualBootstrap>() == null)
                systems.AddComponent<PlayerShipVisualBootstrap>();
        }

        static void EnsureLevelSystem()
        {
            if (LevelSystem.Instance != null) return;

            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<LevelSystem>() == null)
                systems.AddComponent<LevelSystem>();
        }

        static void EnsureTouchJoystickActive()
        {
            var joystick = GameObject.Find("JoystickBG");
            if (joystick != null && !joystick.activeSelf)
                joystick.SetActive(true);
        }

        void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
                if (StageBalanceProfileContext.IsActive)
                    AppRoot.EndActiveSaltwindRunContext();
            }
        }

        public static bool TryRequestRestart()
        {
            if (_active == null || V3RunSession.IsPvP) return false;
            _active.RestartRun();
            return true;
        }

        void Start()
        {
            if (database == null)
            {
                Debug.LogError("V3RunBootstrap: GameDatabase is missing.");
                return;
            }

            if (RunManager.Instance == null)
                gameObject.AddComponent<RunManager>();

            if (stageBuilder == null)
                stageBuilder = FindFirstObjectByType<StageBuilder>();

            ConfigureStageBuilder();
            V3RunContext.IsActive = true;
            WireRunSystemsOnce();
            BootstrapRun(ResolveStartingSeed(), firstRun: true);
        }

        int ResolveStartingSeed() =>
            forceInspectorSeed && runSeed != 0 ? runSeed : V3HomeSeed.ResolveRunSeed();

        void WireRunSystemsOnce()
        {
            if (_runSystemsWired) return;
            _runSystemsWired = true;

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null)
            {
                player.Damage.Died += OnPlayerDied;
                if (TriggerBus.Instance != null)
                    player.Damage.Damaged += OnPlayerDamaged;
            }
        }

        void RestartRun()
        {
            SaltwindRunResetLogic.NotifyRetryStarting();
            RunSnapshotService.Clear();
            BootstrapRun(Random.Range(1, int.MaxValue), firstRun: false);
        }

        void BootstrapRun(int seed, bool firstRun)
        {
            ConfigureStageBuilder();
            LevelSystem.Instance?.CancelPendingLevelUps();
            LevelUpModal.Instance?.Hide();
            RunManager.Instance?.SetPause(PauseReason.None);

            TeardownTransientRunState();

            StageBalanceProfileContext.EndRun();
            if (!TryBeginStageBalanceProfile(out var stageBalanceError))
            {
                Debug.LogError(stageBalanceError);
                return;
            }

            var experimentProfile = CombatExperimentContext.Active;
            CombatWeaponCatalog.ApplyToDatabase(database, experimentProfile);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(database, experimentProfile);
            WeaponDamageLedger.Reset();
            WeaponEvolutionRuntime.Reset();
            _ = new RunCombatTelemetry(experimentProfile);
            RunManager.Instance.BeginRun(seed);
            SaltwindCombatEncounterBudget.ResetForRun();
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            RunMarksTracker.BeginRun(RunManager.Instance.RunId);
            SaltwindRunSettlement.ClearLastResults();
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                var ex = StageBalanceProfileContext.Export;
                RunCombatTelemetry.Instance?.RecordStageProfileStart(
                    ex.ProfileId,
                    ex.BalanceVersion.ToString(),
                    ex.ContentHash,
                    RunManager.Instance.RunId);
            }
            V3FloorPowerupWorld.Ensure();
            V3FloorPowerupWorld.Instance?.ResetForRun();
            V3RunItemInventory.Instance?.ClearForNewRun();
            V3RunItemEffects.Instance?.ResetForNewRun();

            _layout = stageBuilder != null ? stageBuilder.Build(seed, 0) : null;
            V3StormEdgeVisual.DisableWorldRings();
            PlayBounds.ResetStormSafeRegion();
            DisableEnvironmentalHazards();
            V3SaltwindStormRingVisual.Disable();
            V3PlayBoundsWallVisual.Ensure();
            V3RunContext.Layout = _layout;
            V3RunContext.CaptainId = V3CaptainSelection.Selected;

            var multiShipConfig = FindFirstObjectByType<V3MultiShipSceneConfig>();
            var isPvPScene = multiShipConfig != null && multiShipConfig.SessionKind == V3RunSessionKind.PvP;

            var poi = FindFirstObjectByType<POIManager>();
            if (poi != null)
            {
                if (isPvPScene)
                    poi.enabled = false;
                else
                {
                    poi.enabled = true;
                    poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: usePlaceholderPoiVisuals, useV3ChestPricing: true);
                    poi.Initialize(_layout, database);
                    if (_layout != null)
                        V3ExplosiveBarrelWorld.Ensure(_layout);
                }
            }

            FindFirstObjectByType<EnemyWorld>()?.ResetAllAgentsForPromo();
            FindFirstObjectByType<PickupWorld>()?.ClearAllActive();
            FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            FindFirstObjectByType<HostileProjectileWorld>()?.ClearAllActive();

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null && _layout != null)
            {
                var layoutDef = ResolveIslandLayout();
                PlacePlayerAtSafeSpawn(player, layoutDef, seed);
                player.SetMovementEnabled(true);
            }

            var captainId = V3CaptainSelection.Selected;
            var captainProfile = V3CaptainProfile.Get(captainId);
            EnsureCaptainController(captainId, player);
            EnsureCaptainActive(captainId, player);
            V3CaptainActiveController.Instance?.ResetForNewRun();

            var startingWeapon = ResolveStartingWeapon(captainProfile);
            LevelSystem.Instance?.ApplyStageBalanceProgression();

            var weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                ?? FindFirstObjectByType<WeaponController>();
            if (player != null)
                weapons?.BindOwner(player);
            weapons?.ApplyCombatExperiment(experimentProfile);
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export?.Progression != null)
                weapons?.EnsureWeaponSlotCapacity(StageBalanceProfileContext.Export.Progression.WeaponSlotCap);
            weapons?.Initialize(database, startingWeapon);

            EnsureLevelSystem();
            LevelSystem.Instance?.ConfigureV3();
            LevelSystem.Instance?.ApplyCombatExperiment(experimentProfile);
            LevelSystem.Instance?.PrepareForNewRun(database, startingWeapon);
            LevelSystem.Instance?.SyncWeaponControllerFromOwnedLoadout(weapons);
            V3RunItemInventory.Ensure();
            V3RunItemEffects.Ensure();

            if (firstRun)
            {
                V3RunPlaytestHotkeys.Ensure();
                SpawnDirectorDebugUI.Ensure();
                if (V3PlaytestFlags.EnableRunPlaytestHotkeys)
                    WeaponCardAnimationDemo.Ensure();
                V3BossSummonHotkey.Ensure();
            }

            EnemyIdentityDebugLabels.Ensure();
            WeaponTargetingVisualizer.Ensure();

            Canvas runCanvas;
            if (firstRun)
                runCanvas = V3RunUiBootstrap.EnsureAll(_layout);
            else
            {
                V3DiscoverySystem.Ensure(_layout);
                runCanvas = FindFirstObjectByType<RunHud>()?.GetComponentInParent<Canvas>();
                if (runCanvas != null)
                    V3MinimapHud.Ensure(runCanvas.transform, _layout);
            }

            runCanvas?.GetComponent<RunHud>()?.ForceRefresh();

            if (player != null)
            {
                if (firstRun)
                {
                    V3StormEdgeDamage.Ensure(player);
                    EnsureHostileProjectiles();
                }

                if (V3CaptainController.Instance == null)
                    player.InitializeFromRun(RunManager.Instance.Stats.MaxHull);

                EnsurePlayerHealthBar(player);
            }

            if (firstRun)
                EnsureCombatTelemetryDriver();

            EnsureCombatVfx();
            EnsurePlayfeelSystems(player, seed);

            if (!isPvPScene)
            {
                ResetSaltwindDirectors(enemyWorld, player);
                if (firstRun)
                {
                    EnsurePressureBeats(enemyWorld, player);
                    EnsureOvertimeDirector(enemyWorld, player);
                    V3RouteEventDirector.Ensure();
                }
            }

            RunManager.Instance.EnterPlaying();
            if (!isPvPScene)
            {
                if (firstRun && V3PlaytestFlags.EnableRunPlaytestHotkeys)
                {
                    V3ToastHud.Instance?.Show(
                        "Playtest: F8 hotkeys · G weapon card scatter · H card scroll",
                        5f);
                }

                var seedLabel = firstRun && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
                V3ToastHud.Instance?.Show(
                    firstRun
                        ? $"Saltwind voyage ({seedLabel} seed {seed}).\nFind salvage chests as you bank gold, secure the Beacon."
                        : $"New voyage (seed {seed}).\nIslands, chests, and spawn rerolled.",
                    firstRun ? 6f : 5f);
            }

            if (multiShipConfig != null && multiShipConfig.SessionKind != V3RunSessionKind.SinglePlayer)
                V3MultiShipRunSetup.ApplyAfterRunStart(
                    multiShipConfig.SessionKind,
                    player,
                    database,
                    startingWeapon,
                    multiShipConfig.ShipSpacing);

            var seedLabelLog = firstRun && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            Debug.Log($"V3RunBootstrap: run seed={seed} ({seedLabelLog}), POI rng={seed + 404}.");
        }

        void ConfigureEncounterSpawner(CombatExperimentProfile profile)
        {
            var spawner = FindFirstObjectByType<EncounterSpawner>();
            spawner?.ConfigureV3Pacing(true);
            spawner?.ApplyCombatExperiment(profile);
        }

        static void TeardownTransientRunState()
        {
            RunEncounterTracker.ResetForNewRun();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
            FindFirstObjectByType<PickupWorld>()?.ClearAllActive();
            FindFirstObjectByType<ProjectileWorld>()?.ClearAllActive();
            FindFirstObjectByType<HostileProjectileWorld>()?.ClearAllActive();
        }

        void ResetSaltwindDirectors(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            var spawner = FindFirstObjectByType<EncounterSpawner>();
            spawner?.ResetForNewRun();
            ConfigureEncounterSpawner(CombatExperimentContext.Active);
            FindFirstObjectByType<V3PressureBeatDirector>()?.ResetForRun();
            FindFirstObjectByType<V3RouteEventDirector>()?.ResetForRun();
            var overtime = V3OvertimeDirector.Ensure(enemyWorld, spawner, ship);
            overtime.ResetForRun();
            V3SaltwindStormDirector.Ensure().ResetForRun();
            BarnacleKingBossAttackDirector.Ensure(enemyWorld, ship).ResetForRun();
        }

        static void EnsureHostileProjectiles()
        {
            if (FindFirstObjectByType<HostileProjectileWorld>() != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            systems.AddComponent<HostileProjectileWorld>();
        }

        void EnsureCaptainController(V3CaptainId captainId, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            var controller = systems.GetComponent<V3CaptainController>();
            if (controller == null)
                controller = systems.AddComponent<V3CaptainController>();
            controller.Initialize(captainId, ship);
        }

        void EnsureCaptainActive(V3CaptainId captainId, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            var active = systems.GetComponent<V3CaptainActiveController>();
            if (active == null)
                active = systems.AddComponent<V3CaptainActiveController>();
            active.Initialize(captainId);
            HellfirePatchWorld.Ensure();
        }

        void EnsurePressureBeats(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            var spawner = systems.GetComponent<EncounterSpawner>();
            if (spawner == null)
                spawner = FindFirstObjectByType<EncounterSpawner>();

            var beats = systems.GetComponent<V3PressureBeatDirector>();
            if (beats == null)
                beats = systems.AddComponent<V3PressureBeatDirector>();
            beats.Initialize(enemyWorld, spawner, ship != null ? ship.transform : null);
        }

        void EnsureOvertimeDirector(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            var spawner = FindFirstObjectByType<EncounterSpawner>();
            var overtime = V3OvertimeDirector.Ensure(enemyWorld, spawner, ship);
            overtime.ResetForRun();
            V3SaltwindStormDirector.Ensure().ResetForRun();
            BarnacleKingBossAttackDirector.Ensure(enemyWorld, ship).ResetForRun();
        }

        static string ResolveStartingWeapon(V3CaptainProfile profile)
        {
            if (StageBalanceProfileContext.IsActive && !V3PlaytestFlags.SaltwindDebugUnlockAll)
                return "swivel_guns";

            if (!V3PlaytestFlags.UnlockAllContent && !PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey))
                return "swivel_guns";
            var starter = profile.StartingWeaponId;
            return string.IsNullOrEmpty(starter) ? "swivel_guns" : starter;
        }

        void PlacePlayerAtSafeSpawn(PlayerShipController ship, IslandLayoutDefinition layoutDef, int seed)
        {
            const float hullRadius = 1.15f;
            Physics.SyncTransforms();
            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(_layout, layoutDef, seed, hullRadius);
            if (IslandObstacles.IsAvailable)
            {
                var height = IslandObstacles.GetCollisionHeight(hullRadius);
                for (var i = 0; i < 8; i++)
                {
                    if (!IslandObstacles.IsPositionBlocked(spawn, hullRadius))
                        break;
                    spawn = IslandObstacles.Depenetrate(spawn, hullRadius, height);
                }
            }

            ship.SetWorldPosition(spawn);
        }

        void ConfigureStageBuilder()
        {
            if (stageBuilder == null) return;

            var layout = ResolveIslandLayout();
            stageBuilder.ConfigureV3Layout(layout);

            var islandSettings = ProceduralIslandSettings.Default.Clamped();
            stageBuilder.SetProceduralIslandSettings(islandSettings);
            stageBuilder.SetIslandDensityMultiplier(1f);
        }

        IslandLayoutDefinition ResolveIslandLayout()
        {
            if (islandLayout != null && islandLayout.HasIslands)
                return islandLayout;

            var fromResources = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            if (fromResources != null && fromResources.HasIslands)
                return fromResources;

            return IslandLayoutDefinition.LoadAssignedOrDefault(null);
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            var dt = Time.deltaTime;
            var playerPos = player != null ? player.Position : Vector3.zero;
            FindFirstObjectByType<BarnacleKingBossAttackDirector>()?.Tick(dt);
            FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            FindFirstObjectByType<HostileProjectileWorld>()?.Tick(dt, player?.Damage);
            FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            V3FloorPowerupWorld.Instance?.Tick(dt, playerPos);
            FindFirstObjectByType<StatusController>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            ResolveSameTickBossDeath();
        }

        void OnPlayerDamaged(float amount, bool _)
        {
            TriggerBus.Instance?.RaiseDamageTaken(amount);
            RunCombatTelemetry.Instance?.RecordDamageTaken(amount);
        }

        void OnPlayerDied()
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            if (player != null)
                player.SetMovementEnabled(false);
            if (V3RunSession.IsPvP)
            {
                RunManager.Instance.SetState(RunState.PlayerDead);
                RunManager.Instance.FinalizeRunOnDeath();
                V3ToastHud.Instance?.Show("You were sunk — the rival wins.", 4f);
                return;
            }

            FindFirstObjectByType<POIManager>()?.TrySecureIfBossDead();
            ApplyConquestUnlockIfSecured();
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.FinalizeRunOnDeath();
            V3OnboardingDirector.RecordRunCompleted();
            RunCombatTelemetry.Instance?.RecordPlayerDeath(RunManager.Instance.ObjectiveSecured);
            V3ToastHud.Instance?.Show("Run ended — gold banked.", 4f);
            if (StageBalanceProfileContext.IsActive && SaltwindRunSettlement.LastSettlementApplied)
            {
                var marks = SaltwindRunSettlement.LastMarksAwarded;
                V3ToastHud.Instance?.Show($"Marks settled: +{marks}", 4f);
            }
        }

        void ResolveSameTickBossDeath()
        {
            if (RunManager.Instance == null || player == null || player.Damage.IsAlive) return;
            if (!RunManager.Instance.IsRunEnded) return;
            FindFirstObjectByType<POIManager>()?.TrySecureIfBossDead();
            ApplyConquestUnlockIfSecured();
        }

        static void ApplyConquestUnlockIfSecured()
        {
            if (RunManager.Instance != null && RunManager.Instance.ObjectiveSecured)
                PlayerPrefs.SetInt(V3ContentUnlocks.FirstConquestKey, 1);
        }

        void EnsurePlayerHealthBar(PlayerShipController ship)
        {
            if (ship == null) return;
            if (ship.GetComponent<PlayerHealthBarBinder>() == null)
                ship.gameObject.AddComponent<PlayerHealthBarBinder>();
        }

        void EnsureCombatVfx()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            if (systems.GetComponent<JuiceVfxBroker>() == null)
                systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null)
                systems.AddComponent<CombatVfxController>();
            var damageNumbers = systems.GetComponent<FloatingDamageNumbers>();
            if (damageNumbers == null)
                damageNumbers = systems.AddComponent<FloatingDamageNumbers>();
            damageNumbers.Rebind();
            if (systems.GetComponent<WeaponJuiceController>() == null)
                systems.AddComponent<WeaponJuiceController>();
            if (systems.GetComponent<PlayerJuiceController>() == null)
                systems.AddComponent<PlayerJuiceController>();

            var vfxRoot = systems.transform.Find("VFXRoot");
            if (vfxRoot == null)
            {
                var go = new GameObject("VFXRoot");
                go.transform.SetParent(systems.transform, false);
                vfxRoot = go.transform;
            }

            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null)
                broker.Configure(settings, vfxRoot);

            systems.GetComponent<WeaponJuiceController>()?.Configure(settings, vfxRoot);
            if (systems.GetComponent<CameraShakeService>() == null)
                systems.AddComponent<CameraShakeService>();
            if (systems.GetComponent<SfxService>() == null)
                systems.AddComponent<SfxService>();
            if (systems.GetComponent<V3SpawnWarningHud>() == null)
                systems.AddComponent<V3SpawnWarningHud>();
        }

        static void EnsurePlayfeelSystems(PlayerShipController ship, int seed)
        {
            V3OnboardingDirector.Ensure()?.ResetForRun();
            DisableEnvironmentalHazards();

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

        static void DisableEnvironmentalHazards()
        {
            EnvironmentalHazardsDisabled.Set(true);

            foreach (var juice in Object.FindObjectsByType<PlayerJuiceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                juice.DisableSeaFoamTrail();

            foreach (var wind in Object.FindObjectsByType<WindGustDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(wind);

            foreach (var visual in Object.FindObjectsByType<EnvironmentHazardVisual>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(visual.gameObject);

            foreach (var current in Object.FindObjectsByType<OceanCurrentZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(current.gameObject);

            foreach (var pool in Object.FindObjectsByType<WhirlpoolHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pool.gameObject);

            var root = GameObject.Find("EnvironmentHazards");
            if (root != null)
                Object.Destroy(root);
        }

        static void EnsureCombatTelemetryDriver()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<CombatTelemetryDriver>() == null)
                systems.AddComponent<CombatTelemetryDriver>();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || RunManager.Instance == null || RunManager.Instance.IsRunEnded) return;
            if (LevelSystem.Instance == null) return;
            var snapshot = RunSnapshotService.CaptureFromRun(
                RunManager.Instance,
                LevelSystem.Instance,
                CombatExperimentContext.Active,
                FindFirstObjectByType<POIManager>());
            RunSnapshotService.Save(snapshot);
        }

        static bool TryBeginStageBalanceProfile(out string errorMessage)
        {
            errorMessage = null;
            if (V3RunSession.IsLocalMultiplayer)
                return true;

            if (!StageBalanceProfileResolver.TryResolveForCurrentScene(out var profile, out errorMessage))
                return false;

            StageBalanceProfileContext.BeginRun(profile);
            return true;
        }
    }
}
