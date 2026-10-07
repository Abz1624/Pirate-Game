using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4.DevTools.Scenarios
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): the Scenario Lab's entry point. <c>yield return Scenario.Load("crab_at_10m")</c> (PlayMode
    /// tests) or the F1 console's Scenarios section: sets the run's switches (seed, Captain, playtest switches, a 1v1 over
    /// the in-process relay), loads the real run scene (V4SaltwindRun: what ships, not a copy), waits for the run to play,
    /// then a <see cref="ScenarioRunner"/> in that scene sets up the rest (clock, build, enemies, points of interest, boss).
    /// The scenario ends with its scene; the switches it changed go back then.
    /// </summary>
    public static class Scenario
    {
        /// <summary>Where the scenario assets live (the Editor finds them by name here).</summary>
        public const string Folder = "Assets/_Game/V4/DevTools/Scenarios";
        /// <summary>Development builds: scenarios in a Resources/Scenarios folder are listed too (none ship by default).</summary>
        public const string ResourcesFolder = "Scenarios";
        const float LoadTimeout = 30f;

        /// <summary>The runner of the scenario now loaded (null when none, or after its scene went).</summary>
        public static ScenarioRunner Current { get; internal set; }

        /// <summary>The last load's problem (unknown name, the run did not start), or null.</summary>
        public static string LastError { get; private set; }

        public static bool Loading { get; private set; }

        /// <summary>Every scenario asset: the Scenarios folder in the Editor, Resources/Scenarios in a build.</summary>
        public static IReadOnlyList<ScenarioConfig> All()
        {
            var list = new List<ScenarioConfig>();
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ScenarioConfig), new[] { Folder }))
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScenarioConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
#endif
            foreach (var asset in Resources.LoadAll<ScenarioConfig>(ResourcesFolder))
                if (asset != null && !list.Contains(asset)) list.Add(asset);
            return list.OrderBy(s => s.name).ToList();
        }

        /// <summary>The scenario asset with this name (file name), or null.</summary>
        public static ScenarioConfig Find(string name) => All().FirstOrDefault(s => s.name == name);

        /// <summary>A scenario from its JSON (the console's paste box; <see cref="ToJson"/> writes it).</summary>
        public static ScenarioConfig FromJson(string json, string name = "pasted")
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioConfig>();
            scenario.name = name;
            JsonUtility.FromJsonOverwrite(json, scenario);
            return scenario;
        }

        public static string ToJson(ScenarioConfig scenario) => JsonUtility.ToJson(scenario, true);

        /// <summary>Loads the named scenario asset (fails the load when there is none).</summary>
        public static IEnumerator Load(string name)
        {
            var scenario = Find(name);
            if (scenario == null)
            {
                LastError = $"No scenario named {name} in {Folder}.";
                throw new System.ArgumentException(LastError);
            }
            yield return Load(scenario);
        }

        /// <summary>
        /// Loads a scenario: the run scene with its switches, then the runner's setup. Throws when the run does not reach
        /// Playing; the runner's setup problems (unknown ids) are in <see cref="ScenarioRunner.Errors"/>.
        /// </summary>
        public static IEnumerator Load(ScenarioConfig scenario)
        {
            if (scenario == null) throw new System.ArgumentNullException(nameof(scenario));
            LastError = null;
            Loading = true;
            try
            {
                AppRoot.EnsureInitialized();
                var restore = new ScenarioRunner.Restore { DemoOff = DemoMode.Off, CaptainId = Progression.CaptainChoice.LegacyId };
                DemoMode.Off = scenario.playtest == ScenarioConfig.PlaytestSwitches.Off;
                Random.InitState(scenario.seed);
                FakePvpRelay relay = null;
                if (scenario.pvp != null && scenario.pvp.enabled)
                {
                    relay = new FakePvpRelay(scenario.seed, scenario.pvp);
                    PvpSession.Instance.UseRelay(relay.Lobby, relay.Match, relay);
                    RunRequest.RequestPvp(scenario.seed);
                    Composition.MenuServices.BeginSaltwindExperiment();
                }
                else
                {
                    RunRequest.ClearPvp();
                    RunRequest.RequestSeed(scenario.seed);
                    if (scenario.captain != null)
                    {
                        Progression.CaptainChoice.LegacyId = scenario.captain.legacyId;
                        RunRequest.AllowLockedCaptainOnce();
                    }
                }

                yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
                V4Game game = null;
                var deadline = Time.realtimeSinceStartup + LoadTimeout;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (game == null) game = Object.FindFirstObjectByType<V4Game>();
                    if (game != null && game.IsRunning && RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) break;
                    yield return null;
                }
                if (game == null || !game.IsRunning || RunManager.Instance == null || RunManager.Instance.State != RunState.Playing)
                {
                    restore.Apply();
                    if (relay != null && PvpSession.Exists) PvpSession.Instance.Forget();
                    LastError = $"Scenario {scenario.name}: the run did not reach Playing in {LoadTimeout:0} s.";
                    throw new System.InvalidOperationException(LastError);
                }

                var runner = new GameObject("V5ScenarioRunner").AddComponent<ScenarioRunner>();
                Current = runner;
                runner.Begin(scenario, game, relay, restore);
                yield return null; // one frame for the views (enemy views, health bars, markers) to bind
                if (runner.Errors.Count > 0) Debug.LogWarning($"Scenario {scenario.name}: " + string.Join("; ", runner.Errors));
                else Debug.Log($"[V5 Scenario] {scenario.name} loaded (seed {scenario.seed}, clock {RunManager.Instance.ElapsedSeconds:0} s)");
            }
            finally { Loading = false; }
        }

        /// <summary>The console's load: runs <see cref="Load(ScenarioConfig)"/> on an object that outlives the scene change.</summary>
        public static void LoadFromConsole(ScenarioConfig scenario)
        {
            if (scenario == null || Loading) return;
            var host = new GameObject("V5ScenarioLoader").AddComponent<LoaderHost>();
            Object.DontDestroyOnLoad(host.gameObject);
            host.Run(scenario);
        }

        sealed class LoaderHost : MonoBehaviour
        {
            public void Run(ScenarioConfig scenario) => StartCoroutine(Go(scenario));

            IEnumerator Go(ScenarioConfig scenario)
            {
                var load = Load(scenario);
                while (true)
                {
                    object current;
                    try
                    {
                        if (!load.MoveNext()) break;
                        current = load.Current;
                    }
                    catch (System.Exception e)
                    {
                        LastError = e.Message;
                        Debug.LogException(e);
                        break;
                    }
                    yield return current;
                }
                Destroy(gameObject);
            }
        }
    }
}
