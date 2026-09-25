using System;
using System.Reflection;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.World;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.V4
{
    /// <summary>
    /// V4 composition root. Builds the run context and module list and connects them. Systems that have no V4 module
    /// yet run through <see cref="LegacyRunShell"/> (same order and slot as V3RunBootstrap, so M0 runs match V3).
    /// It must not grow gameplay logic of its own: new behavior goes into modules.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class V4Game : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] PlayerShipController player;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] bool usePlaceholderPoiVisuals = true;

        public static event Action<V4Game> RunStarted;

        RunClock _clock;
        CombatEvents _events;
        RunContext _context;
        ModuleRegistry _modules;
        LegacyRunShell _shell;
        LegacyEventFeed _feed;
        bool _running;
        bool _resultsRebound;

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
            var seed = request.seed != 0
                ? request.seed
                : request.retry ? UnityEngine.Random.Range(1, int.MaxValue) : V3HomeSeed.ResolveRunSeed();

            _modules.InitializeAll(_context);
            if (!_shell.Start(seed)) return;

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
            var simulating = run != null && !run.IsPaused && !run.IsRunEnded;
            if (simulating)
            {
                var steps = _clock.Advance(Time.deltaTime);
                for (var i = 0; i < steps; i++) _modules.TickAll(_clock.NextTick());
            }

            if (!_resultsRebound && run != null && run.IsRunEnded) RebindResultsButtons();
        }

        void LateUpdate()
        {
            if (!_running) return;
            _feed.AttachLevels();
            _events.EndTick();
        }

        void OnDestroy()
        {
            if (_running)
            {
                var run = RunManager.Instance;
                _modules.EndRunAll(new RunEnd(run != null ? run.ElapsedSeconds : 0f, run != null && run.ObjectiveSecured));
            }

            _feed?.Detach();
            _shell?.Unwire();
            _shell?.OnDestroy();
        }

        void OnApplicationPause(bool paused) => _shell?.OnApplicationPause(paused);

        // ---- Results screen: keep Retry/Home inside V4 --------------------------------------------------

        const string RestartButtonName = "PLACEHOLDER_v3_ui_run_results_restart";
        const string MenuButtonName = "PLACEHOLDER_v3_ui_run_results_menu";
        static readonly FieldInfo ContinueField = typeof(V3RunResultsScreen).GetField("continueButton", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo MenuField = typeof(V3RunResultsScreen).GetField("menuButton", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// The legacy results screens build placeholder buttons in Awake and add listeners in Start that restart
        /// through V3RunBootstrap (absent here) or load the V3 menu. By the time the run has ended every Start has
        /// run, so replace those listeners with V4 restart and V4 menu.
        /// </summary>
        void RebindResultsButtons()
        {
            _resultsRebound = true;
            foreach (var screen in FindObjectsByType<V3RunResultsScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Rebind(ContinueField?.GetValue(screen) as Button ?? FindButton(screen.transform, RestartButtonName), RestartRun);
                Rebind(MenuField?.GetValue(screen) as Button ?? FindButton(screen.transform, MenuButtonName), GoToMenu);
            }
        }

        static Button FindButton(Transform root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        static void Rebind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static void RestartRun()
        {
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
