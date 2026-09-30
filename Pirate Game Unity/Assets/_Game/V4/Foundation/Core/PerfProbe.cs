using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// Demo (30 Sep 2026): on phones, logs the frame rate every 5 s ("[Perf]" in logcat: average fps, worst frame,
    /// frames over 25 ms) so performance can be read from a device without a development build.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        float _elapsed, _worst;
        int _frames, _slow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isMobilePlatform || DemoMode.Off) return;
            var go = new GameObject("PerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbe>();
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            if (dt > _worst) _worst = dt;
            if (dt > 0.025f) _slow++;
            if (_elapsed < 5f) return;
            Debug.Log($"[Perf] fps={_frames / _elapsed:0.0} worst={_worst * 1000f:0}ms slow={_slow}/{_frames} target={Application.targetFrameRate} res={Screen.width}x{Screen.height}");
            _elapsed = _worst = 0f;
            _frames = _slow = 0;
        }
    }
}
