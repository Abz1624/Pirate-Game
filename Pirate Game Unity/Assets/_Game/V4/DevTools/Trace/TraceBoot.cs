using System;
using System.IO;
using System.Reflection;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Arms a trace run. In the Editor the request comes from Temp/ (written by V4TraceSession); in a development
    /// player it comes from the command line (<c>-v4trace &lt;request.json&gt;</c>), and the player starts in the empty
    /// launcher scene, which loads the requested run scene. Pins everything that would otherwise make two runs of the
    /// same seed differ: frame dt, the global UnityEngine.Random seed, screen shake, the save backend, the PlayerPrefs a
    /// run reads, and the run seed (V3RunBootstrap's serialized fields in V3 scenes, RunRequest in V4 scenes).
    /// </summary>
    static class TraceBoot
    {
        internal static TraceRequest Active;

        /// <summary>
        /// PlayerPrefs a run reads, pinned to one fixed state so traces don't depend on whoever played last, and so
        /// parallel player instances (which share one PlayerPrefs store) never see different values: every write a
        /// run makes to these keys (First Conquest on victory, runs completed) rewrites the pinned value or a value
        /// only read at run start.
        /// </summary>
        static readonly (string key, int value)[] PinnedInts =
        {
            ("v3_selected_captain", 0),               // Rook
            ("combat_captain_actives_enabled", 0),    // V3 default: actives off
            ("combat_aim_visuals_enabled", 0),
            ("highseas_v3_developer_preset", 0),
            ("v3_first_conquest_complete", 1),        // read by level-up unlock gates mid-run, written on victory
            ("v3_saltwind_runs_completed", 2),        // tutorial already seen
        };
        static readonly string[] DeletedKeys = { "v3_home_seed", "highseas_run_snapshot_v1" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            var request = Application.isEditor ? TraceRequest.TryConsume() : TraceRequest.TryReadFromCommandLine();
            if (request == null) return;
            Active = request;
            Time.captureDeltaTime = request.captureDeltaTime;
            Random.InitState(request.seed);
            Application.runInBackground = true;
            foreach (var (key, value) in PinnedInts) PlayerPrefs.SetInt(key, value);
            foreach (var key in DeletedKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(request.presetPath) && File.Exists(request.presetPath))
                RunRequest.RequestPreset(File.ReadAllText(request.presetPath)); // V4Game applies it before anything reads config
            Debug.Log($"V4 trace armed: {request.label} seed={request.seed} dt={request.captureDeltaTime}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            if (Active == null) return;
            if (SceneManager.GetActiveScene().name == TraceRequest.LauncherSceneName && !string.IsNullOrEmpty(Active.scene))
            {
                // Player: the build starts in the empty launcher; arm when the run scene has loaded.
                SceneManager.sceneLoaded += OnRunSceneLoaded;
                SceneManager.LoadScene(Path.GetFileNameWithoutExtension(Active.scene));
                return;
            }
            Arm();
        }

        static void OnRunSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnRunSceneLoaded;
            Arm();
        }

        static void Arm()
        {
            AppRoot.EnsureInitialized();
            if (AppRoot.Instance != null)
            {
                AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend()));
                AppRoot.Instance.Config.ScreenShakeEnabled = false;
            }

            // V3 scenes: force the inspector seed before V3RunBootstrap.Start runs.
            var v3 = Object.FindFirstObjectByType<V3RunBootstrap>();
            if (v3 != null)
            {
                SetPrivateField(v3, "forceInspectorSeed", true);
                SetPrivateField(v3, "runSeed", Active.seed);
            }

            // V4 scenes read the seed from RunRequest.
            RunRequest.RequestSeed(Active.seed);

            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            if (ship != null)
                ship.ConfigureBotControl(Active.pilot == ScriptedPilot.PatternId ? new ScriptedPilot() : (IShipSteerInput)new PlayerPilot(ship));

            var go = new GameObject("V4TraceRecorder");
            go.AddComponent<RunTraceRecorder>().Begin(Active);
        }

        static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Debug.LogError($"V4 trace: field {target.GetType().Name}.{name} not found; seed not pinned.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
