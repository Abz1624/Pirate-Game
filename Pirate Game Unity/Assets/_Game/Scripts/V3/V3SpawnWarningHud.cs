using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Lightweight spawn direction warnings (toast fallback until full arrow UI).</summary>
    public sealed class V3SpawnWarningHud : MonoBehaviour
    {
        public static V3SpawnWarningHud Instance { get; private set; }

        float _lastWarnTime;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void WarnIncoming(string label)
        {
            if (Instance == null) return;
            if (Time.unscaledTime - Instance._lastWarnTime < 1.5f) return;
            Instance._lastWarnTime = Time.unscaledTime;
            V3ToastHud.Instance?.Show(label, 2.5f);
        }
    }
}
