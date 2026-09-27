using System;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.World;
using RoguePirates.Simulation;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4
{
    /// <summary>
    /// V4 composition root. Builds the run context and module list and connects them. Systems that have no V4 module
    /// yet run through <see cref="LegacyRunShell"/> (same order and slot as V3RunBootstrap, so M0 runs match V3).
    /// It must not grow gameplay logic of its own: new behavior goes into modules; legacy calls go into Legacy/.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class V4Game : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] bool usePlaceholderPoiVisuals = true;
        [Header("Config (M1)")]
        [Tooltip("The config set this run copies (SaltwindDefault).")]
        [SerializeField] ConfigSet configSet;
        [Tooltip("V3 stage profile the per-run profile starts from; fields V4 does not own yet pass through.")]
        [SerializeField] StageBalanceProfile legacyBaseProfile;
        [Tooltip("Module Lab scene: invulnerable ship, no ambient spawns or beats; enemies are spawned from the console.")]
        [SerializeField] bool moduleLab;
        [Tooltip("M8: the enemy world's scene values (were serialized on V3's EnemyWorld component in the V4 scenes).")]
        [SerializeField] Enemies.EnemyWorldSettings enemyWorld = new Enemies.EnemyWorldSettings();

        /// <summary>Raised once per run after it starts; DevTools hooks the Tuning Console here (event, not state).</summary>
        public static event Action<V4Game> RunStarted;

        ConfigSetRuntime _config;
        RunClock _clock;
        CombatEvents _events;
        RunContext _context;
        ModuleRegistry _modules;
        LegacyRunShell _shell;
        LegacyEventFeed _feed;
        SpawnController _spawn;
        LegacyWeaponWiring _weapons;
        LegacyResultsTelemetry _resultsTelemetry;
        LegacyStageWiring _stage;
        LegacyEnemyWiring _enemies;
        LegacyBuildWiring _build;
        bool _running;
        bool _ended;
        bool _objectiveSecured;

        public RunContext Context => _context;
        public ConfigSetRuntime Config => _config;
        public ModuleRegistry Modules => _modules;
        public CombatEvents Events => _events;
        public LegacyRunShell Shell => _shell;
        /// <summary>M9: the build and progression modules (console sections, trace pilot).</summary>
        public LegacyBuildWiring Build => _build;
        public bool IsRunning => _running;
        public bool IsModuleLab => moduleLab;

        void Awake()
        {
            _config = configSet != null ? new ConfigSetRuntime(configSet) : null;
            ApplyRequestedPreset();
            var run = _config?.Single<RunConfig>();
            _clock = run != null ? new RunClock(run.tickRate, run.maxStepsPerFrame) : new RunClock();
            _events = new CombatEvents();
            _context = new RunContext(_clock, _events, _config);
            if (_config != null) _config.ConfigChanged += OnConfigChanged;
            _modules = new ModuleRegistry();
            // Each phase registers its module here and removes the legacy piece it replaces.
            // M3: the spawn controller (replaces the legacy ambient spawner and the V3 spawn directors).
            _spawn = new SpawnController { LabMode = moduleLab };
            _modules.Add(_spawn);
            _context.Register<ISpawnController>(_spawn);
            // Contracts the V4 modules use, implemented over legacy systems until those systems are ported.
            var runState = new LegacyRunState();
            var encounters = new LegacyEncounterExecutor();
            // M8: the V4 enemy world (replaces EnemyWorld, StatusController and HostileProjectileWorld).
            _enemies = new LegacyEnemyWiring(_config, enemyWorld, () => _shell != null ? _shell.Player : null);
            _context.Register<IEnemySpawnSink>(_enemies.Contracts);
            _context.Register<Enemies.IEnemyWorldView>(_enemies.Contracts);
            _context.Register<Enemies.IEnemyDevControl>(_enemies.Contracts);
            _context.Register<Enemies.IHostileProjectileView>(_enemies.Services.Shots);
            _context.Register<Enemies.IHazardView>(_enemies.Services.Hazards);
            _context.Register<ISpawnPlacement>(new LegacySpawnPlacement());
            _context.Register<IRunRandom>(runState);
            _context.Register<IRunStatus>(runState);
            _context.Register<ISpawnFeedback>(new LegacySpawnFeedback());
            _context.Register<IStormArena>(new LegacyStormArena());
            var damageSink = new LegacyPlayerDamageSink();
            _context.Register<IPlayerDamageSink>(damageSink);
            _context.Register<ILegacySpawnRateSource>(encounters);
            var slots = new LegacySpawnSlots(_spawn.TickAmbient, _spawn.TickOvertime, _spawn.TickStorm, _spawn.TickBeats, _spawn.TickStormEdge);
            _shell = new LegacyRunShell(this, database, stageBuilder, player, islandLayout, usePlaceholderPoiVisuals, _config, legacyBaseProfile, slots, encounters);
            _context.Register<ISpawnerProfiles>(_shell.LegacyConfig);
            _shell.Enemies = _enemies;
            // M9: level-ups, Logbooks, items, the Captain, floor powerups and pickups (replace LevelSystem, the weapon
            // record, PickupWorld, the V3 Captain/item/powerup components and the switched-off EncounterSpawner).
            _build = new LegacyBuildWiring(_config, () => _shell != null ? _shell.Player : null, () => _shell?.Database);
            _shell.Build = _build;
            _enemies.Build = _build;
            _enemies.KillTargets.Build = _build;
            _enemies.Services.PlayerIncomingDamage = _build.IncomingDamageMultiplier;
            _enemies.Target.IncomingDamage = _build.IncomingDamageMultiplier;
            damageSink.IncomingDamage = _build.IncomingDamageMultiplier;
            damageSink.TookSteadyDamage = _build.Items.NotifyPlayerTookDamage;
            encounters.DeadCalm = () => _build.Powerups.SpawnRateMultiplier;
            runState.Level = () => _build.Progression.Level;
            _context.Register<Progression.IItemInventory>(_build.Items);
            // M5: the Weapon Manager (replaces WeaponController's firing and ProjectileWorld). M9: it reads the V4 loadout.
            _weapons = new LegacyWeaponWiring(_shell, _context, _config, runState, _events, _clock, _enemies, _build);
            _shell.Weapons = _weapons;
            _build.Weapons = _weapons.Manager;
            _enemies.HellfireEnvironment.Calculator = () => _weapons.Manager.Combat.Calculator;
            _enemies.HellfireEnvironment.HasHellfire = _build.HasHellfire;
            _context.Register<RoguePirates.V4.Weapons.IWeaponManager>(_weapons.Manager);
            // M6: the read-only views presentation reads (over the legacy hubs until their modules exist).
            // M7: stage content (chests, landmarks, Beacon, discovery, interaction, boss director) replaces POIManager.
            _stage = new LegacyStageWiring(_context, _config, runState, damageSink, _spawn, () => _shell.Player, () => _weapons.Manager, usePlaceholderPoiVisuals, _enemies);
            _shell.Stage = _stage;
            _stage.Economy.AddItem = _build.Items.TryAddItem;
            var views = LegacyViewFactory.Create(player, _weapons.Manager, RestartRun, GoToMenu, _enemies.Contracts, _build,
                () => _config?.Single<ExperimentConfig>()?.weaponSlotCap ?? 6);
            views.Stage = _stage.View;
            _context.Register<Views.IRunViews>(views);
            _resultsTelemetry = new LegacyResultsTelemetry();
            _feed = new LegacyEventFeed(_events, _clock);
            damageSink.SteadyDamageReported += _feed.OnSteadyDamage;
            _shell.Awake();
        }

        void Start()
        {
            var request = RunRequest.Consume();
            var seed = LegacyRunShell.ResolveSeed(request.seed, request.retry);

            _modules.InitializeAll(_context);
            if (!_shell.Start(seed, request.retry)) return;

            _feed.Attach(_shell.Player);
            _feed.AttachLevels(_build.Progression);
            _build.AttachShip(_shell.Player);
            _resultsTelemetry.Attach();
            if (moduleLab) _shell.EnterModuleLab();
            _modules.BeginRunAll(new RunStart(seed, request.retry));
            _running = true;
            RunStarted?.Invoke(this);
        }

        void Update()
        {
            if (!_running) return;
            // Legacy systems tick exactly like V3RunBootstrap.Update (variable dt) until their modules replace them.
            _shell.Tick(Time.deltaTime);

            var run = RunManager.Instance;
            SyncPause(run);
            if (run != null && !run.IsRunEnded)
            {
                var steps = _clock.Advance(Time.deltaTime);
                for (var i = 0; i < steps; i++)
                {
                    _modules.TickAll(_clock.NextTick());
                    _events.EndTick(); // one batch per simulation tick
                }
            }

            if (run == null) return;
            _objectiveSecured = run.ObjectiveSecured;
            if (!_ended && run.IsRunEnded) EndRun();
        }

        void LateUpdate()
        {
            if (!_running) return;
            _shell.LateTick();

            // Legacy producers publish outside V4 ticks (their own Update, or while paused); deliver those with the
            // current tick stamp so readers see them this frame instead of waiting for the next tick.
            if (_events.Pending.Count > 0) _events.EndTick();
        }

        /// <summary>The V4 clock mirrors the legacy pause (one source of truth until V4 owns pausing).</summary>
        void SyncPause(RunManager run)
        {
            _clock.Pause.Remove(SimPauseReason.Manual | SimPauseReason.Choice);
            if (run == null || !run.IsPaused) return;
            _clock.Pause.Add(run.PauseReason == PauseReason.UserPause ? SimPauseReason.Manual : SimPauseReason.Choice);
        }

        void EndRun()
        {
            _ended = true;
            _modules.EndRunAll(new RunEnd(_clock.SimTime, _objectiveSecured));
        }

        void OnDestroy()
        {
            // Fallback for runs left before they ended (restart, menu); values captured while RunManager existed.
            if (_running && !_ended)
            {
                _ended = true;
                _modules.EndRunAll(new RunEnd(_clock.SimTime, _objectiveSecured));
            }

            _feed?.Detach();
            _resultsTelemetry?.Dispose();
            _stage?.Dispose();
            _enemies?.Dispose();
            _weapons?.Dispose();
            _build?.Dispose();
            _shell?.Unwire();
            _shell?.OnDestroy();
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            _config?.Dispose();
        }

        /// <summary>A preset requested for this run (console "restart with preset") applies before anything reads config.</summary>
        void ApplyRequestedPreset()
        {
            var json = RunRequest.ConsumePreset();
            if (_config == null || string.IsNullOrEmpty(json)) return;
            var errors = new System.Collections.Generic.List<string>();
            if (!PresetCodec.Apply(_config, PresetCodec.FromJson(json), 0f, errors))
                Debug.LogWarning("V4Game: preset applied with errors:\n" + string.Join("\n", errors));
        }

        /// <summary>Every live config edit is also a ConfigChanged fact on the event stream (telemetry, console log).</summary>
        void OnConfigChanged(ConfigChange change)
        {
            _enemies?.OnConfigChanged();
            _events.Publish(new CombatEvent(CombatEventKind.ConfigChanged, _clock.TickIndex, _clock.SimTime, 0, 0, 0f, 0f, 0f, change.configId + "." + change.path));
        }

        void OnApplicationPause(bool paused) => _shell?.OnApplicationPause(paused);

        public static void RestartRun()
        {
            LegacyRunShell.NotifyRetryStarting();
            RunRequest.MarkRetry();
            SceneManager.LoadScene(CurrentRunScene());
        }

        public static void RestartRunWithSeed(int seed)
        {
            RunRequest.RequestSeed(seed);
            SceneManager.LoadScene(CurrentRunScene());
        }

        /// <summary>Restarts reload the run scene they came from (the Module Lab stays the lab).</summary>
        static string CurrentRunScene() =>
            SceneManager.GetActiveScene().name == V4SceneNames.ModuleLab ? V4SceneNames.ModuleLab : V4SceneNames.SaltwindRun;

        public static void GoToMenu() => SceneManager.LoadScene(V4SceneNames.MainMenu);
    }
}
