using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Ports;
using UnityEngine;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// The run start sequence (M14.14; was LegacyRunShell, V3RunBootstrap's Awake/Start/BootstrapRun/Update/death in
    /// V3's order): the database and stage profile, the run clock, the world and the ship's spawn, stage content, the
    /// enemy world, the Captain, level-ups, weapons and items, the frame steps, then Playing. Shared V3 code is reached
    /// through the keep-list types and the tag-15 ports. V4 restarts by reloading the scene, so every run is a fresh
    /// run; V3's multi-ship (PvP) scene setup is not ported (V4 is solo, plan D9).
    /// </summary>
    public sealed class RunBootstrapSequence
    {
        readonly MonoBehaviour _host;
        readonly bool _usePlaceholderPoiVisuals;
        readonly RunDatabaseLoader _database = new RunDatabaseLoader();
        readonly FramePipeline _frame;
        readonly RoguePirates.V4.Spawning.SpawnController _spawn;
        readonly RoguePirates.V4.Spawning.SpawnRateModifiers _encounters;
        readonly WorldPort _world;
        /// <summary>V5 M9c S1: the run's session clock (the ship, its damage receiver and boost, and the run manager read it).</summary>
        readonly SessionClock _clock;
        readonly StageProfilePort _stageProfile;
        readonly V4RuntimeDatabase _runtimeDatabase;
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

        /// <summary>M14: the stage profile the shared V3 code reads (built from the V4 configs).</summary>
        public StageProfilePort StageProfile => _stageProfile;
        /// <summary>M14: the V4 spawner profiles (bound to the run database here, where V3 retargeted its clones).</summary>
        public RoguePirates.V4.Spawning.SpawnerProfiles Spawners { get; set; }
        /// <summary>M5: the V4 weapon module; bound at run start after the level system synced the loadout.</summary>
        public WeaponModule Weapons { get; set; }
        /// <summary>M7: chests, landmarks, Beacon, discovery, interaction and the boss director.</summary>
        public StageModule Stage { get; set; }
        /// <summary>M14: the V4 run lifecycle and the shared run port.</summary>
        public RunLifecycle Lifecycle { get; set; }
        public RunPort RunPort { get; set; }
        /// <summary>M14: the run's toast sink.</summary>
        public RoguePirates.V4.Views.IToastSink Toasts { get; set; }
        /// <summary>M8: the V4 enemy world (replaces EnemyWorld, StatusController and HostileProjectileWorld).</summary>
        public EnemyWorldModule Enemies { get; set; }
        /// <summary>M9: level-ups, items, the Captain, powerups and pickups (replace LevelSystem, the weapon record, PickupWorld and the V3 build components).</summary>
        public BuildModule Build { get; set; }
        /// <summary>M12: Rum, settled at death.</summary>
        public RoguePirates.V4.Rewards.RewardModule Rewards { get; set; }
        /// <summary>Playtest (3 Oct, owner): places the ship at run start instead of the stage's safe spawn (the 1v1 seat).</summary>
        public System.Action<PlayerShipController> PlaceShip { get; set; }

        public RunBootstrapSequence(MonoBehaviour host, GameDatabase database, StageBuilder stageBuilder,
            PlayerShipController player, IslandLayoutDefinition islandLayout, bool usePlaceholderPoiVisuals,
            ConfigSetRuntime config, FramePipeline frame, RoguePirates.V4.Spawning.SpawnController spawn,
            RoguePirates.V4.Spawning.SpawnRateModifiers encounters, SessionClock clock = null)
        {
            _clock = clock ?? new SessionClock();
            _frame = frame;
            _spawn = spawn;
            _encounters = encounters;
            // V5 baseline: the ship's speed factor (top speed and acceleration) for this run.
            var runConfig = config != null ? config.Single<RunConfig>() : null;
            if (player != null)
            {
                player.Clock = _clock;
                player.RunSpeedMultiplier = runConfig != null && runConfig.shipSpeedMultiplier > 0f ? runConfig.shipSpeedMultiplier : 1f;
                player.IslandImpact = runConfig == null ? default : new PlayerShipController.IslandImpactSettings
                {
                    MinSpeed = runConfig.islandImpactMinSpeed, Restitution = runConfig.islandBounceRestitution, PushSpeed = runConfig.islandBouncePushSpeed,
                    Damage = runConfig.islandImpactDamage, Cooldown = runConfig.islandImpactCooldown,
                    DamageCooldown = runConfig.islandDamageCooldown, BounceSeconds = runConfig.islandBounceSeconds,
                };
                player.SyncRunSpeedMultiplier();
            }
            _stageProfile = new StageProfilePort(config);
            _runtimeDatabase = new V4RuntimeDatabase(config);
            _host = host;
            _player = player;
            _usePlaceholderPoiVisuals = usePlaceholderPoiVisuals;
            _serializedDatabase = database;
            _world = new WorldPort(stageBuilder, islandLayout);
        }

        readonly GameDatabase _serializedDatabase;

        /// <summary>V3RunBootstrap.Awake, same order.</summary>
        public void Awake()
        {
            WorldPort.PrepareScene(); // hazards off, the touch joystick, the ship visual bootstrap
            AppRoot.EnsureInitialized();
            _database.Load(_serializedDatabase);
        }

        /// <summary>Seed for the next run (explicit request, else random on Retry, else the home seed).</summary>
        public static int ResolveSeed(int requestedSeed, bool retry) => WorldPort.ResolveSeed(requestedSeed, retry);

        /// <summary>V3RunBootstrap.RestartRun side effects, called before the retry reload.</summary>
        public static void NotifyRetryStarting() => WorldPort.NotifyRetryStarting();

        /// <summary>V3RunBootstrap.Start. Returns false when the run could not start.</summary>
        public bool Start(int seed, bool isRetry)
        {
            if (_database.Database == null)
            {
                Debug.LogError("V4Game: GameDatabase is missing.");
                return false;
            }

            if (RunManager.Instance == null) _host.gameObject.AddComponent<RunManager>();
            RunManager.Instance.Clock = _clock;
            _world.ConfigureStageBuilder();
            WorldPort.MarkRunContextActive();
            WireRunSystemsOnce();
            return BootstrapRun(seed, isRetry);
        }

        void WireRunSystemsOnce()
        {
            if (_wired) return;
            _wired = true;
            if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
            if (_player == null) return;
            _player.Clock = _clock;
            _player.Damage.Died += OnPlayerDied;
            _player.Damage.Damaged += OnPlayerDamaged;
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

            Enemies?.ClearTransientWorlds();

            if (!_stageProfile.TryBegin(out var profileError))
            {
                Debug.LogError(profileError);
                return false;
            }

            // M2: enemies come from V4 archetype/variant configs, in the exact legacy array layout.
            _runDatabase = _runtimeDatabase.Build(_database.Database);
            if (_runtimeDatabase.Unconfigured.Count > 0)
                Debug.LogWarning("V4Game: enemies without V4 config (kept from V3): " + string.Join(", ", _runtimeDatabase.Unconfigured));
            Spawners?.BindEnemies(id => _runDatabase.GetEnemy(id)); // M14: the V4 spawner profiles
            RunManager.Instance.BeginRun(seed);
            if (Lifecycle != null)
            {
                // (M14.9: player damage feedback is the presenters', from the PlayerDamaged event.)
                Lifecycle.StageProfile ??= _stageProfile.Identity;
                Lifecycle.CancelPendingLevelUps ??= () => Build?.CancelPendingLevelUps(); // a goal run's end (as a sinking's)
                Lifecycle.BeginRunTracking();
            }
            Build?.ResetForRun(); // V3 reset its floor powerups and items here

            _layout = _world.Build(seed);
            WorldPort.ResetWorldVisuals();

            // M7: V4 stage content in POIManager's place.
            Stage?.InitializePoi(_layout);

            Enemies?.ResetAndDefine(_runDatabase);

            if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
            if (_player != null) _player.Clock = _clock;
            if (_player != null && _layout != null)
            {
                if (PlaceShip != null) PlaceShip(_player); // Playtest (3 Oct, owner): the 1v1 seat's spawn
                else _world.PlacePlayerAtSafeSpawn(_player, _layout, seed);
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

            // (M14.14: V3's playtest hotkeys are DevTools'; the run canvas setup is the presentation root's.)
            Stage?.EnsureDiscovery(_layout);
            Stage?.EnsureInteraction();

            if (_player != null)
            {
                _frame.Register(FrameStep.StormEdge, _spawn.TickStormEdge); // storm damage at the storm edge (the V4 storm phase applies it)
                // (V3 added HostileProjectileWorld to /Systems here; M8: the V4 enemy world owns the hostile shots.)
                if (captain == null)
                    _player.InitializeFromRun(RunManager.Instance.Stats.MaxHull);
                // (M14.9b: the ship health bar is a V4 presenter; V3's PlayerHealthBarBinder is not added.)
            }

            Enemies?.CreateTelemetrySlot(() => Weapons != null ? Weapons.Manager.Projectiles.ActiveCount : 0); // CombatTelemetryDriver's place
            // (M14.9b: V3's JuiceVfxBroker, WeaponJuiceController and PlayerJuiceController are not added; every effect is
            // a V4 presenter.)
            WorldPort.EnsurePlayfeel(_player, seed); // day-night, boost, boost camera zoom

            // V3 created its spawn directors here (V3RunBootstrap.ResetSaltwindDirectors and the first-run path).
            // M3: the V4 spawn controller replaces them; M14.14: its ticks are frame steps (were legacy update slots).
            _frame.Register(FrameStep.Overtime, _spawn.TickOvertime);
            _frame.Register(FrameStep.Storm, _spawn.TickStorm);
            Stage?.ResetBoss();
            _frame.Register(FrameStep.Beats, _spawn.TickBeats);
            Stage?.ResetBoss();
            _frame.Register(FrameStep.Ambient, _spawn.TickAmbient);
            Stage?.BindManagerSlot();

            RunManager.Instance.EnterPlaying();
            if (PlaceShip == null) Lifecycle?.ShowStartToast(!isRetry, seed, WorldPort.HomeSeedLocked); // (a 1v1 has no voyage toast)

            var seedLabel = !isRetry && WorldPort.HomeSeedLocked ? "locked" : "random";
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


        /// <summary>V3RunBootstrap.Update.</summary>
        static readonly Unity.Profiling.ProfilerMarker EnemiesMarker = new Unity.Profiling.ProfilerMarker("V4.Enemies.Frame");
        object _projectilesOwner, _bossOwner;
        System.Action<float> _tickProjectiles, _tickBoss;

        public void Tick(float dt)
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            // V5 M3e: the two callbacks are made once per owner (two delegates a frame before).
            if (_projectilesOwner != Weapons) { _projectilesOwner = Weapons; _tickProjectiles = Weapons != null ? Weapons.TickProjectiles : (System.Action<float>)null; }
            if (_bossOwner != Stage) { _bossOwner = Stage; _tickBoss = Stage != null ? Stage.TickBoss : (System.Action<float>)null; }
            using (EnemiesMarker.Auto()) Enemies?.TickFrame(dt, _player, _tickProjectiles, _tickBoss);
            Enemies?.TickAreaDenialRelease(RunManager.Instance.ElapsedSeconds);
            ResolveSameTickBossDeath();
        }

        void ResolveSameTickBossDeath()
        {
            if (RunManager.Instance == null || _player == null || _player.Damage.IsAlive) return;
            if (!RunManager.Instance.IsRunEnded) return;
            Stage?.TrySecureIfBossDead();
            Lifecycle?.ApplyConquestUnlockIfSecured();
        }

        void OnPlayerDamaged(float amount, bool _) => Lifecycle?.RecordDamage(amount);

        void OnPlayerDied() => Lifecycle?.OnPlayerDied(() => Stage?.TrySecureIfBossDead(), () => Build?.CancelPendingLevelUps());

        public void OnDestroy()
        {
            _stageProfile.EndIfActive();
            _stageProfile.Dispose();
            _runtimeDatabase.Dispose();
        }

        /// <summary>
        /// V3RunBootstrap.OnApplicationPause captured a suspend snapshot from LevelSystem and POIManager that nothing ever
        /// loaded; both left the V4 scenes, so V4 captures nothing (plan M9 9.8).
        /// </summary>
        public void OnApplicationPause(bool paused) { }
    }
}
