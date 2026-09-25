using System.Reflection;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.Platform;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Arms a trace run when the Editor left a request in Temp/. Pins everything that would otherwise make two runs
    /// of the same seed differ: frame dt, the global UnityEngine.Random seed, screen shake, the save backend and the
    /// run seed (V3RunBootstrap's serialized fields in V3 scenes, RunRequest in V4 scenes).
    /// </summary>
    static class TraceBoot
    {
        internal static TraceRequest Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            var request = TraceRequest.TryConsume();
            if (request == null) return;
            Active = request;
            Time.captureDeltaTime = request.captureDeltaTime;
            Random.InitState(request.seed);
            Application.runInBackground = true;
            Debug.Log($"V4 trace armed: {request.label} seed={request.seed} dt={request.captureDeltaTime}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            if (Active == null) return;

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
