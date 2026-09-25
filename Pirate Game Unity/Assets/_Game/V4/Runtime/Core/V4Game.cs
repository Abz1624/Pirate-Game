using System;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.World;
using RoguePirates.Simulation;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
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

        /// <summary>Raised once per run after it starts; DevTools hooks the Tuning Console here (event, not state).</summary>
        public static event Action<V4Game> RunStarted;

        RunClock _clock;
        CombatEvents _events;
        RunContext _context;
        ModuleRegistry _modules;
        LegacyRunShell _shell;
        LegacyEventFeed _feed;
        bool _running;
        bool _ended;
        bool _objectiveSecured;

        public RunContext Context => _context;
        public ModuleRegistry Modules => _modules;
        public CombatEvents Events => _events;
        public LegacyRunShell Shell => _shell;
        public bool IsRunning => _running;

        void Awake()
        {
            _clock = new RunClock();
            _events = new CombatEvents();
            _context = new RunContext(_clock, _events);
            _modules = new ModuleRegistry();
            // M0: no V4 modules yet. Each phase registers its module here and removes the legacy piece it replaces.
            _shell = new LegacyRunShell(this, database, stageBuilder, player, islandLayout, usePlaceholderPoiVisuals);
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
        }

        void OnApplicationPause(bool paused) => _shell?.OnApplicationPause(paused);

        public static void RestartRun()
        {
            LegacyRunShell.NotifyRetryStarting();
            RunRequest.MarkRetry();
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
        }

        public static void RestartRunWithSeed(int seed)
        {
            RunRequest.RequestSeed(seed);
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
        }

        public static void GoToMenu() => SceneManager.LoadScene(V4SceneNames.MainMenu);
    }
}
