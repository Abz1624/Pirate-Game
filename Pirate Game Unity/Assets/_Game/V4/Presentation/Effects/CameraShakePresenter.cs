using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Camera shake (M14; port of V3's CameraShakeService: after the camera follow at order 200, a random offset of the
    /// current intensity each frame while the timer runs, off when the settings disable screen shake). V3 kept the largest
    /// intensity ever asked for; that is kept too.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CameraShakePresenter : MonoBehaviour
    {
        float _timer;
        float _intensity;

        public void Shake(float intensity, float seconds)
        {
            _intensity = Mathf.Max(_intensity, intensity);
            _timer = Mathf.Max(_timer, seconds);
        }

        void LateUpdate()
        {
            if (_timer <= 0f) return;
            if (AppRoot.Instance?.Config != null && !AppRoot.Instance.Config.ScreenShakeEnabled) return;
            _timer -= Time.unscaledDeltaTime;
            var cam = Camera.main;
            if (cam == null) return;
            var offset = Random.insideUnitSphere * _intensity;
            offset.z = 0f;
            cam.transform.position += offset;
        }
    }
}
