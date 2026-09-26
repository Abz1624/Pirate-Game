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
        bool _running;
        bool _ended;
        bool _objectiveSecured;

        public RunContext Context => _context;
        public ConfigSetRuntime Config => _config;
        public ModuleRegistry Modules => _modules;
        public CombatEvents Events => _events;
        public LegacyRunShell Shell => _shell;
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
            _context.Register<IEnemySpawnSink>(new LegacyEnemySpawnSink());
            _context.Register<ISpawnPlacement>(new LegacySpawnPlacement());
            _context.Register<IRunRandom>(runState);
            _context.Register<IRunStatus>(runState);
            _context.Register<ISpawnFeedback>(new LegacySpawnFeedback());
            _context.Register<IStormArena>(new LegacyStormArena());
            _context.Register<IPlayerDamageSink>(new LegacyPlayerDamageSink());
            _context.Register<ILegacySpawnRateSource>(encounters);
            var slots = new LegacySpawnSlots(_spawn.TickAmbient, _spawn.TickOvertime, _spawn.TickStorm, _spawn.TickBeats, _spawn.TickStormEdge);
            _shell = new LegacyRunShell(this, database, stageBuilder, player, islandLayout, usePlaceholderPoiVisuals, _config, legacyBaseProfile, slots, encounters);
            _context.Register<ISpawnerProfiles>(_shell.LegacyConfig);
            _feed = new LegacyEventFeed(_events, _clock);
            _shell.Awake();
        }

        void Start()
        {
            var request = RunRequest.Consume();
            var seed = LegacyRunShell.ResolveSeed(request.seed, request.retry);

            _modules.InitializeAll(_context);
            if (!_shell.Start(seed, request.retry)) return;

            _feed.Attach(_shell.Player);
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
            _feed.AttachLevels();
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
            LegacyResultsRebinder.Rebind(RestartRun, GoToMenu);
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
        void OnConfigChanged(ConfigChange change) =>
            _events.Publish(new CombatEvent(CombatEventKind.ConfigChanged, _clock.TickIndex, _clock.SimTime, 0, 0, 0f, 0f, 0f, change.configId + "." + change.path));

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
