using HighSeas.Core;
using UnityEngine;

namespace HighSeas.VFX
{
    [DefaultExecutionOrder(200)]
    public sealed class CameraShakeService : MonoBehaviour
    {
        public static CameraShakeService Instance { get; private set; }

        float _shakeTimer;
        float _shakeIntensity;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (_shakeTimer <= 0f) return;
            if (AppRoot.Instance?.Config != null && !AppRoot.Instance.Config.ScreenShakeEnabled) return;

            _shakeTimer -= Time.unscaledDeltaTime;
            var cam = Camera.main;
            if (cam == null) return;
            var offset = Random.insideUnitSphere * _shakeIntensity;
            offset.z = 0f;
            cam.transform.position += offset;
        }

        public static void Shake(float intensity, float durationSeconds)
        {
            if (Instance == null) return;
            Instance._shakeIntensity = Mathf.Max(Instance._shakeIntensity, intensity);
            Instance._shakeTimer = Mathf.Max(Instance._shakeTimer, durationSeconds);
        }
    }
}
