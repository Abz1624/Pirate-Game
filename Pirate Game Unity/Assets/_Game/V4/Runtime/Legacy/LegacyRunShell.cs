using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.World;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Runs the V3 run sequence (V3RunBootstrap Awake/Start/BootstrapRun/Update/death) through the legacy adapters,
    /// with the call order unchanged. Every phase moves a piece of this into a V4 module; it is deleted at switch-over.
    /// V4 restarts by reloading the scene, so every run here is a "first run".
    /// </summary>
    [DeletedInPhase(14, "All run wiring is V4 modules after switch-over")]
    public sealed class LegacyRunShell
    {
        readonly MonoBehaviour _host;
        readonly bool _usePlaceholderPoiVisuals;
        readonly LegacyProfileAdapter _profile = new LegacyProfileAdapter();
        readonly LegacyDatabaseAdapter _database = new LegacyDatabaseAdapter();
        readonly V4LegacyBridge _bridge = new V4LegacyBridge();
        readonly V4LegacySpawnBridge _spawnBridge = new V4LegacySpawnBridge();
        readonly LegacyWorldAdapter _world;
        readonly LegacyConfigRuntime _legacyConfig;
        PlayerShipController _player;
        StageLayout _layout;
        bool _wired;

        public PlayerShipController Player => _player;
        public StageLayout Layout => _layout;
        public GameDatabase Database => _database.Database;
        public V4LegacyBridge Bridge => _bridge;
        public int Seed { get; private set; }

        public LegacyConfigRuntime LegacyConfig => _legacyConfig;

        public LegacyRunShell(MonoBehaviour host, GameDatabase database, StageBuilder stageBuilder,
            PlayerShipController player, IslandLayoutDefinition islandLayout, bool usePlaceholderPoiVisuals,
            ConfigSetRuntime config, StageBalanceProfile legacyBaseProfile)
        {
            _legacyConfig = new LegacyConfigRuntime(config, legacyBaseProfile);
            _host = host;
            _player = player;
            _usePlaceholderPoiVisuals = usePlaceholderPoiVisuals;
            _serializedDatabase = database;
            _world = new LegacyWorldAdapter(stageBuilder, islandLayout);
        }

        readonly GameDatabase _serializedDatabase;

        /// <summary>V3RunBootstrap.Awake, same order.</summary>
        public void Awake()
        {
            EnvironmentalHazardsDisabled.Set(true);
            LegacyWorldAdapter.DisableEnvironmentalHazards();
            AppRoot.EnsureInitialized();
            _database.Load(_serializedDatabase);
            // M1: config-driven copies of the stage profile and encounter schedule, installed before any spawner reset.
            if (_legacyConfig.Build(out var configError)) _spawnBridge.InstallSchedule(_legacyConfig.RuntimeSchedule);
            else Debug.LogError("V4Game: " + configError);
            _legacyConfig.ProfileChanged += () => _profile.Rebuild(_legacyConfig.RuntimeProfile);
            var multiConfig = Object.FindFirstObjectByType<V3MultiShipSceneConfig>();
            if (multiConfig != null) V3RunSession.Configure(multiConfig.SessionKind);
            LegacyWorldAdapter.EnsureTouchJoystickActive();
            LegacyWorldAdapter.EnsureGameplayBootstrapComponents();
        }

        /// <summary>
        /// Seed for the next run, as V3 resolves it: explicit request, else a random seed on Retry
        /// (V3RunBootstrap.RestartRun), else the locked home seed / random first-run seed (V3HomeSeed).
        /// </summary>
        public static int ResolveSeed(int requestedSeed, bool retry) =>
            requestedSeed != 0 ? requestedSeed : retry ? Random.Range(1, int.MaxValue) : V3HomeSeed.ResolveRunSeed();

        /// <summary>V3RunBootstrap.RestartRun side effects, called before the retry reload.</summary>
        public static void NotifyRetryStarting()
        {
            HighSeas.Balance.SaltwindRunResetLogic.NotifyRetryStarting();
            HighSeas.Save.RunSnapshotService.Clear();
        }

        /// <summary>V3RunBootstrap.Start. Returns false when the run could not start.</summary>
        public bool Start(int seed, bool isRetry)
        {
            if (_database.Database == null)
            {
                Debug.LogError("V4Game: GameDatabase is missing.");
                return false;
            }

            if (RunManager.Instance == null) _host.gameObject.AddComponent<RunManager>();
            _world.ConfigureStageBuilder();
            V3RunContext.IsActive = true;
            WireRunSystemsOnce();
            return BootstrapRun(seed, isRetry);
        }

        void WireRunSystemsOnce()
        {
            if (_wired) return;
            _wired = true;
            if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
            if (_player == null) return;
            _player.Damage.Died += OnPlayerDied;
            if (HighSeas.Meta.TriggerBus.Instance != null) _player.Damage.Damaged += OnPlayerDamaged;
        }

        public void Unwire()
        {
            if (!_wired || _player == null) return;
            _player.Damage.Died -= OnPlayerDied;
            _player.Damage.Damaged -= OnPlayerDamaged;
            _wired = false;
        }

        bool BootstrapRun(int seed, bool isRetry)
        {
            // Every V4 run is a fresh scene, so all "first run" creation paths apply; only the start toast and log
            // label follow V3's retry wording (V3 retries in place with firstRun: false).
            const bool firstRun = true;
            Seed = seed;
            _world.ConfigureStageBuilder();
            LegacyBuildAdapter.CancelPendingLevelUps();
            RunManager.Instance?.SetPause(PauseReason.None);

            LegacyRewardsAdapter.TeardownTransientRunState();

            if (!_profile.TryBegin(_legacyConfig.RuntimeProfile, out var profileError))
            {
                Debug.LogError(profileError);
                return false;
            }

            var experiment = CombatExperimentContext.Active;
            _database.ApplyCatalogs(experiment);
            LegacyRewardsAdapter.ResetLedgers();
            LegacyRewardsAdapter.CreateTelemetry(experiment);
            RunManager.Instance.BeginRun(seed);
            LegacyRewardsAdapter.BeginRunTracking();
            LegacyBuildAdapter.ResetPowerupsAndItems();

            _layout = _world.Build(seed);
            LegacyWorldAdapter.ResetWorldVisuals();
            LegacyWorldAdapter.SetRunContext(_layout);

            var multiShipConfig = Object.FindFirstObjectByType<V3MultiShipSceneConfig>();
            var isPvPScene = multiShipConfig != null && multiShipConfig.SessionKind == V3RunSessionKind.PvP;

            LegacyStageAdapter.InitializePoi(_layout, _database.Database, _usePlaceholderPoiVisuals, isPvPScene);

            var enemyWorld = LegacyEnemyWorldPort.ResetAndDefine(_database.Database);

            if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
            if (_player != null && _layout != null)
            {
                _world.PlacePlayerAtSafeSpawn(_player, _layout, seed);
                _player.SetMovementEnabled(true);
            }

            var captainId = V3CaptainSelection.Selected;
            var captainProfile = V3CaptainProfile.Get(captainId);
            LegacyBuildAdapter.EnsureCaptain(captainId, _player);

            var startingWeapon = LegacyBuildAdapter.ResolveStartingWeapon(captainProfile);
            LegacyBuildAdapter.ApplyStageBalanceProgression();

            _bridge.SetUpWeapons(_player, experiment, _database.Database, startingWeapon);
            LegacyBuildAdapter.PrepareLevelSystem(_database.Database, experiment, startingWeapon);
            _bridge.SyncLevelSystemLoadout();
            LegacyBuildAdapter.EnsureItemSystems();

            LegacyPresentationAdapter.EnsureDevAndDebugTools(firstRun);
            LegacyPresentationAdapter.EnsureRunUi(_layout, firstRun);

            if (_player != null)
            {
                LegacyStageAdapter.EnsureStormEdgeDamage(_player);
                LegacyEnemyWorldPort.EnsureHostileProjectiles();
                if (V3CaptainController.Instance == null)
                    _player.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
                LegacyPresentationAdapter.EnsurePlayerHealthBar(_player);
            }

            LegacyRewardsAdapter.EnsureTelemetryDriver();
            LegacyPresentationAdapter.EnsureCombatVfx();
            LegacyPresentationAdapter.EnsurePlayfeelSystems(_player, seed);

            if (!isPvPScene)
            {
                _spawnBridge.ResetSpawnDirectors(enemyWorld, _player);
                LegacyStageAdapter.ResetBossDirector(enemyWorld, _player);
                _spawnBridge.EnsureSpawnDirectorsFirstRun(enemyWorld, _player,
                    () => LegacyStageAdapter.ResetBossDirector(enemyWorld, _player));
            }

            RunManager.Instance.EnterPlaying();
            if (!isPvPScene) LegacyPresentationAdapter.ShowStartToasts(!isRetry, seed);

            if (multiShipConfig != null && multiShipConfig.SessionKind != V3RunSessionKind.SinglePlayer)
                V3MultiShipRunSetup.ApplyAfterRunStart(multiShipConfig.SessionKind, _player, _database.Database,
                    startingWeapon, multiShipConfig.ShipSpacing);

            var seedLabel = !isRetry && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            Debug.Log($"V4Game: run seed={seed} ({seedLabel}), POI rng={seed + 404}.");
            return true;
        }

        /// <summary>V3RunBootstrap.Update.</summary>
        public void Tick(float dt)
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            LegacyEnemyWorldPort.Tick(dt, _player);
            ResolveSameTickBossDeath();
        }

        void ResolveSameTickBossDeath()
        {
            if (RunManager.Instance == null || _player == null || _player.Damage.IsAlive) return;
            if (!RunManager.Instance.IsRunEnded) return;
            LegacyStageAdapter.TrySecureIfBossDead();
            LegacyRewardsAdapter.ApplyConquestUnlockIfSecured();
        }

        void OnPlayerDamaged(float amount, bool _) => LegacyRewardsAdapter.RecordDamage(amount);

        void OnPlayerDied() => LegacyRewardsAdapter.OnPlayerDied(_player);

        public void OnDestroy()
        {
            _profile.EndIfActive();
            _legacyConfig.Dispose();
        }

        /// <summary>V3RunBootstrap.OnApplicationPause: capture a suspend snapshot (never loaded by V3 either).</summary>
        public void OnApplicationPause(bool paused)
        {
            if (!paused || RunManager.Instance == null || RunManager.Instance.IsRunEnded) return;
            if (HighSeas.UI.LevelSystem.Instance == null) return;
            var snapshot = HighSeas.Save.RunSnapshotService.CaptureFromRun(
                RunManager.Instance, HighSeas.UI.LevelSystem.Instance, CombatExperimentContext.Active,
                Object.FindFirstObjectByType<HighSeas.POI.POIManager>());
            HighSeas.Save.RunSnapshotService.Save(snapshot);
        }
    }
}
