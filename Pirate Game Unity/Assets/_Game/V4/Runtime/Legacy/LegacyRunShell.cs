using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.World;
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
        readonly LegacyWorldAdapter _world;
        PlayerShipController _player;
        StageLayout _layout;
        bool _wired;

        public PlayerShipController Player => _player;
        public StageLayout Layout => _layout;
        public GameDatabase Database => _database.Database;
        public V4LegacyBridge Bridge => _bridge;
        public int Seed { get; private set; }

        public LegacyRunShell(MonoBehaviour host, GameDatabase database, StageBuilder stageBuilder,
            PlayerShipController player, IslandLayoutDefinition islandLayout, bool usePlaceholderPoiVisuals)
        {
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
            var multiConfig = Object.FindFirstObjectByType<V3MultiShipSceneConfig>();
            if (multiConfig != null) V3RunSession.Configure(multiConfig.SessionKind);
            LegacyWorldAdapter.EnsureTouchJoystickActive();
            LegacyWorldAdapter.EnsureGameplayBootstrapComponents();
        }

        /// <summary>V3RunBootstrap.Start. Returns false when the run could not start.</summary>
        public bool Start(int seed)
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
            return BootstrapRun(seed);
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

        bool BootstrapRun(int seed)
        {
            const bool firstRun = true;
            Seed = seed;
            _world.ConfigureStageBuilder();
            LegacyBuildAdapter.CancelPendingLevelUps();
            RunManager.Instance?.SetPause(PauseReason.None);

            LegacyRewardsAdapter.TeardownTransientRunState();

            if (!_profile.TryBegin(out var profileError))
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
                _bridge.ResetSpawnDirectors(enemyWorld, _player);
                LegacyStageAdapter.ResetBossDirector(enemyWorld, _player);
                _bridge.EnsureSpawnDirectorsFirstRun(enemyWorld, _player,
                    () => LegacyStageAdapter.ResetBossDirector(enemyWorld, _player));
            }

            RunManager.Instance.EnterPlaying();
            if (!isPvPScene) LegacyPresentationAdapter.ShowStartToasts(firstRun, seed);

            if (multiShipConfig != null && multiShipConfig.SessionKind != V3RunSessionKind.SinglePlayer)
                V3MultiShipRunSetup.ApplyAfterRunStart(multiShipConfig.SessionKind, _player, _database.Database,
                    startingWeapon, multiShipConfig.ShipSpacing);

            var seedLabel = V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
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

        public void OnDestroy() => _profile.EndIfActive();

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
