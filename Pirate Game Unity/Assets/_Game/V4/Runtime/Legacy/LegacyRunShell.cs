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
        readonly LegacySpawnSlots _spawnSlots;
        readonly LegacyEncounterExecutor _encounters;
        readonly LegacyWorldAdapter _world;
        readonly LegacyConfigRuntime _legacyConfig;
        readonly V4RuntimeDatabase _runtimeDatabase;
        readonly LegacyCombatProfileOverride _combatOverride;
        GameDatabase _runDatabase;
        PlayerShipController _player;
        StageLayout _layout;
        bool _wired;

        public PlayerShipController Player => _player;
        public StageLayout Layout => _layout;
        /// <summary>The database the run uses: the M2 runtime database once built, else the loaded one.</summary>
        public GameDatabase Database => _runDatabase != null ? _runDatabase : _database.Database;
        public V4RuntimeDatabase RuntimeDatabase => _runtimeDatabase;
        public int Seed { get; private set; }

        public LegacyConfigRuntime LegacyConfig => _legacyConfig;
        /// <summary>M5: the V4 weapon module; bound at run start after the level system synced the loadout.</summary>
        public LegacyWeaponWiring Weapons { get; set; }
        /// <summary>M7: chests, landmarks, Beacon, discovery, interaction and the boss director.</summary>
        public LegacyStageWiring Stage { get; set; }
        /// <summary>M8: the V4 enemy world (replaces EnemyWorld, StatusController and HostileProjectileWorld).</summary>
        public LegacyEnemyWiring Enemies { get; set; }
        /// <summary>M9: level-ups, items, the Captain, powerups and pickups (replace LevelSystem, the weapon record, PickupWorld and the V3 build components).</summary>
        public LegacyBuildWiring Build { get; set; }

        public LegacyRunShell(MonoBehaviour host, GameDatabase database, StageBuilder stageBuilder,
            PlayerShipController player, IslandLayoutDefinition islandLayout, bool usePlaceholderPoiVisuals,
            ConfigSetRuntime config, StageBalanceProfile legacyBaseProfile, LegacySpawnSlots spawnSlots,
            LegacyEncounterExecutor encounters)
        {
            _spawnSlots = spawnSlots;
            _encounters = encounters;
            _legacyConfig = new LegacyConfigRuntime(config, legacyBaseProfile);
            _runtimeDatabase = new V4RuntimeDatabase(config);
            _combatOverride = new LegacyCombatProfileOverride(config);
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
            // M1: config-driven copies of the stage profile and spawn profiles (the encounter-only schedule went with the
            // legacy EncounterSpawner in M9).
            if (!_legacyConfig.Build(out var configError)) Debug.LogError("V4Game: " + configError);
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
            Build?.CancelPendingLevelUps();
            RunManager.Instance?.SetPause(PauseReason.None);

            LegacyRewardsAdapter.TeardownTransientRunState(() => Enemies?.ClearTransientWorlds());

            if (!_profile.TryBegin(_legacyConfig.RuntimeProfile, out var profileError))
            {
                Debug.LogError(profileError);
                return false;
            }

            var experiment = CombatExperimentContext.Active;
            _database.ApplyCatalogs(experiment);
            // M2: enemies come from V4 archetype/variant configs, in the exact legacy array layout.
            _runDatabase = _runtimeDatabase.Build(_database.Database);
            if (_runtimeDatabase.Unconfigured.Count > 0)
                Debug.LogWarning("V4Game: enemies without V4 config (kept from V3): " + string.Join(", ", _runtimeDatabase.Unconfigured));
            _legacyConfig.RetargetEnemies(_runDatabase); // every spawn profile reads the per-run copies
            _combatOverride.ApplyAll();
            LegacyRewardsAdapter.ResetLedgers();
            LegacyRewardsAdapter.CreateTelemetry(experiment);
            RunManager.Instance.BeginRun(seed);
            LegacyRewardsAdapter.BeginRunTracking();
            Build?.ResetForRun(); // V3 reset its floor powerups and items here

            _layout = _world.Build(seed);
            LegacyWorldAdapter.ResetWorldVisuals();
            LegacyWorldAdapter.SetRunContext(_layout);

            var multiShipConfig = Object.FindFirstObjectByType<V3MultiShipSceneConfig>();
            var isPvPScene = multiShipConfig != null && multiShipConfig.SessionKind == V3RunSessionKind.PvP;

            // M7: V4 stage content in POIManager's place (markers and barrels created at the same moment as V3).
            Stage?.InitializePoi(_layout, isPvPScene);
            Stage?.AttachKillHook();

            Enemies?.ResetAndDefine(_runDatabase);

            if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
            if (_player != null && _layout != null)
            {
                _world.PlacePlayerAtSafeSpawn(_player, _layout, seed);
                _player.SetMovementEnabled(true);
            }

            // M9: the V4 Captain, level-ups and items where V3 set up its Captain controllers, weapon record, LevelSystem
            // and item components.
            var captain = Build?.SelectedCaptain();
            Build?.BeginCaptain(captain, () => Enemies?.CreateHellfireSlot());
            var startingWeapon = Build?.ResolveStartingWeapon(captain);
            Build?.BeginProgression(startingWeapon);
            Weapons?.Bind();
            Build?.BeginItems();

            LegacyPresentationAdapter.EnsureDevAndDebugTools(firstRun);
            LegacyPresentationAdapter.EnsureRunUi(_layout, Stage);

            if (_player != null)
            {
                _spawnSlots.AddStormEdgeSlot(_player); // V3StormEdgeDamage's place; the V4 storm phase applies the damage
                // (V3 added HostileProjectileWorld to /Systems here; M8: the V4 enemy world owns the hostile shots.)
                if (captain == null)
                    _player.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
                LegacyPresentationAdapter.EnsurePlayerHealthBar(_player);
            }

            Enemies?.CreateTelemetrySlot(() => Weapons != null ? Weapons.Manager.Projectiles.ActiveCount : 0); // CombatTelemetryDriver's place
            LegacyPresentationAdapter.EnsureCombatVfx();
            LegacyPresentationAdapter.EnsurePlayfeelSystems(_player, seed);

            if (!isPvPScene)
            {
                // V3 created its spawn directors here (V3RunBootstrap.ResetSaltwindDirectors and the first-run path).
                // M3: the V4 spawn controller replaces them; its ticks are created at the same points so they run at
                // the same place in the frame (LegacyUpdateSlot).
                LegacyEncounterExecutor.ResetForRun();
                _spawnSlots.CreateOvertimeSlot();
                _spawnSlots.AddStormSlot();
                Stage?.ResetBoss();
                _spawnSlots.AddBeatSlot();
                Stage?.ResetBoss();
                _spawnSlots.BindAmbient();
                Stage?.BindManagerSlot();
            }

            RunManager.Instance.EnterPlaying();
            if (!isPvPScene) LegacyPresentationAdapter.ShowStartToasts(!isRetry, seed);

            if (multiShipConfig != null && multiShipConfig.SessionKind != V3RunSessionKind.SinglePlayer)
            {
                V3MultiShipRunSetup.ApplyAfterRunStart(multiShipConfig.SessionKind, _player, _runDatabase,
                    startingWeapon, multiShipConfig.ShipSpacing);
            }

            var seedLabel = !isRetry && V3HomeSeed.HasLockedHomeSeed ? "locked" : "random";
            Debug.Log($"V4Game: run seed={seed} ({seedLabel}), POI rng={seed + 404}.");
            return true;
        }

        /// <summary>
        /// Module Lab mode (M2): invulnerable ship; ambient spawns and beats are off, so enemies appear
        /// only when requested from the console or by the stage content (M8 playtest: chests, landmarks and the Beacon work).
        /// </summary>
        public void EnterModuleLab()
        {
            Stage?.EnterModuleLab();
            if (_player != null) _player.Damage.SetPlaytestInvulnerable(true);
        }

        /// <summary>End of frame, after legacy UI callbacks ran (skip button, level-up resume).</summary>
        public void LateTick() => LegacyPresentationAdapter.HideLevelUpCardsLeftAfterSkip(Build != null ? Build.Progression.PendingLevelUps : 0);

        /// <summary>V3RunBootstrap.Update.</summary>
        public void Tick(float dt)
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            Enemies?.TickFrame(dt, _player, Weapons != null ? Weapons.TickProjectiles : (System.Action<float>)null,
                Stage != null ? Stage.TickBoss : (System.Action<float>)null);
            Enemies?.TickAreaDenialRelease(RunManager.Instance.ElapsedSeconds);
            ResolveSameTickBossDeath();
        }

        void ResolveSameTickBossDeath()
        {
            if (RunManager.Instance == null || _player == null || _player.Damage.IsAlive) return;
            if (!RunManager.Instance.IsRunEnded) return;
            Stage?.TrySecureIfBossDead();
            LegacyRewardsAdapter.ApplyConquestUnlockIfSecured();
        }

        void OnPlayerDamaged(float amount, bool _) => LegacyRewardsAdapter.RecordDamage(amount);

        void OnPlayerDied() => LegacyRewardsAdapter.OnPlayerDied(_player, () => Stage?.TrySecureIfBossDead(), () => Build?.CancelPendingLevelUps());

        public void OnDestroy()
        {
            _profile.EndIfActive();
            _legacyConfig.Dispose();
            _combatOverride.Dispose(); // restore V3's shared combat-profile registry
            _runtimeDatabase.Dispose();
        }

        /// <summary>
        /// V3RunBootstrap.OnApplicationPause captured a suspend snapshot from LevelSystem and POIManager that nothing ever
        /// loaded; both left the V4 scenes, so V4 captures nothing (plan M9 9.8).
        /// </summary>
        public void OnApplicationPause(bool paused) { }
    }
}
