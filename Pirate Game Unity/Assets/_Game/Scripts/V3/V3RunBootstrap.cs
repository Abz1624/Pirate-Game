using HighSeas.Combat;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Debugging;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.VFX;
using HighSeas.Weapons;
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
        [SerializeField] bool usePlaceholderPoiVisuals = true;

        StageLayout _layout;

        void Awake()
        {
            AppRoot.EnsureInitialized();
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
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
            var experimentProfile = CombatExperimentContext.Active;
            CombatWeaponCatalog.ApplyToDatabase(database, experimentProfile);
            WeaponDamageLedger.Reset();
            WeaponEvolutionRuntime.Reset();
            _ = new RunCombatTelemetry(experimentProfile);
            var seed = forceInspectorSeed && runSeed != 0
                ? runSeed
                : V3HomeSeed.ResolveRunSeed();
            RunManager.Instance.BeginRun(seed);
            V3FloorPowerupWorld.Ensure();
            V3FloorPowerupWorld.Instance?.ResetForRun();
            _layout = stageBuilder != null ? stageBuilder.Build(seed, 0) : null;
            V3StormEdgeVisual.Ensure();
            V3RunContext.Layout = _layout;
            V3RunContext.CaptainId = V3CaptainSelection.Selected;

            var poi = FindFirstObjectByType<POIManager>();
            if (poi != null)
            {
                poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: usePlaceholderPoiVisuals, useV3ChestPricing: true);
                poi.Initialize(_layout, database);
            }

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.SetDefinitions(database.enemies);

            if (player == null)
                player = FindFirstObjectByType<PlayerShipController>();

            if (player != null && _layout != null)
            {
                var layoutDef = ResolveIslandLayout();
                PlacePlayerAtSafeSpawn(player, layoutDef, seed);
            }

            var captainId = V3CaptainSelection.Selected;
            var captainProfile = V3CaptainProfile.Get(captainId);
            EnsureCaptainController(captainId, player);
            EnsureCaptainActive(captainId, player);

            var startingWeapon = ResolveStartingWeapon(captainProfile);
            var weapons = FindFirstObjectByType<WeaponController>();
            weapons?.ApplyCombatExperiment(experimentProfile);
            weapons?.Initialize(database, startingWeapon);

            EnsureLevelSystem();
            LevelSystem.Instance?.ConfigureV3();
            LevelSystem.Instance?.ApplyCombatExperiment(experimentProfile);
            LevelSystem.Instance?.Initialize(database, startingWeapon);
            V3RunItemInventory.Ensure();
            V3RunItemEffects.Ensure();
            V3RunPlaytestHotkeys.Ensure();
            var runCanvas = V3RunUiBootstrap.EnsureAll(_layout);
            runCanvas.GetComponent<RunHud>()?.ForceRefresh();

            if (player != null)
            {
                V3StormEdgeDamage.Ensure(player);
                EnsureHostileProjectiles();
            }

            if (V3CaptainController.Instance == null)
                player?.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
            EnsurePlayerHealthBar(player);

            if (player != null)
            {
                player.Damage.Died += OnPlayerDied;
                if (TriggerBus.Instance != null)
                    player.Damage.Damaged += OnPlayerDamaged;
            }

            EnsureCombatVfx();
            CollisionDebugOverlay.Ensure();
            EnsureCombatTelemetryDriver();
            WeaponTargetingVisualizer.Ensure();
            ConfigureSpawnDirector(experimentProfile);
            EnsurePressureBeats(enemyWorld, player);
            EnsureOvertimeDirector(enemyWorld, player);
            RunManager.Instance.EnterPlaying();
            if (player != null)
                V3RunOpeningXpScatter.ScatterNearPlayer(player.Position, seed);
            LevelSystem.Instance?.TryShowOpeningWeaponDraft();
            if (V3PlaytestFlags.EnableRunPlaytestHotkeys)
            {
                V3ToastHud.Instance?.Show(
                    "Playtest: press F8 for hotkeys — [ ] cycle solo weapons",
                    5f);
            }
            var seedLabel = V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            V3ToastHud.Instance?.Show(
                $"Saltwind voyage ({seedLabel} seed {seed}).\nExplore 12 chests + 4 shrines, secure the Beacon.",
                6f);
            Debug.Log($"V3RunBootstrap: run seed={seed} ({seedLabel}), POI rng={seed + 404}.");
        }

        void ConfigureSpawnDirector(CombatExperimentProfile profile)
        {
            var director = FindFirstObjectByType<SpawnDirector>();
            director?.ConfigureV3Pacing(true);
            director?.ApplyCombatExperiment(profile);
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

            var director = systems.GetComponent<SpawnDirector>();
            if (director == null)
                director = FindFirstObjectByType<SpawnDirector>();

            var beats = systems.GetComponent<V3PressureBeatDirector>();
            if (beats == null)
                beats = systems.AddComponent<V3PressureBeatDirector>();
            beats.Initialize(enemyWorld, director, ship != null ? ship.transform : null);
        }

        void EnsureOvertimeDirector(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            var director = FindFirstObjectByType<SpawnDirector>();
            V3OvertimeDirector.Ensure(enemyWorld, director, ship);
        }

        static string ResolveStartingWeapon(V3CaptainProfile profile)
        {
            if (!V3PlaytestFlags.UnlockAllContent && !PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey))
                return "deck_cannons";
            return profile.StartingWeaponId;
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
            FindFirstObjectByType<EnemyWorld>()?.Tick(dt, playerPos, player?.Damage);
            FindFirstObjectByType<ProjectileWorld>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            FindFirstObjectByType<HostileProjectileWorld>()?.Tick(dt, player?.Damage);
            FindFirstObjectByType<PickupWorld>()?.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius);
            V3FloorPowerupWorld.Instance?.Tick(dt, playerPos);
            FindFirstObjectByType<StatusController>()?.Tick(dt, FindFirstObjectByType<EnemyWorld>());
            ResolveSameTickBossDeath();
        }

        void OnPlayerDamaged(float amount, bool _) => TriggerBus.Instance?.RaiseDamageTaken(amount);

        void OnPlayerDied()
        {
            LevelSystem.Instance?.CancelPendingLevelUps();
            if (player != null)
                player.SetMovementEnabled(false);
            FindFirstObjectByType<POIManager>()?.TrySecureIfBossDead();
            ApplyConquestUnlockIfSecured();
            RunManager.Instance.SetState(RunState.PlayerDead);
            RunManager.Instance.FinalizeRunOnDeath();
            V3ToastHud.Instance?.Show("Run ended — gold banked at port.", 4f);
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

            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null)
            {
                var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
                broker.Configure(settings, vfxRoot);
            }
        }

        static void EnsureCombatTelemetryDriver()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<CombatTelemetryDriver>() == null)
                systems.AddComponent<CombatTelemetryDriver>();
        }
    }
}
