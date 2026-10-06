using UnityEngine;

namespace RoguePirates.V4.Foundation.PlayerSystems
{
    /// <summary>
    /// Playtest (3 Oct, owner): the camera no longer zooms out on boost; the boost only speeds the ship up. The component
    /// stays (the run bootstrap still adds it) and holds the camera at its resting field of view, as it did between boosts.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class BoostCameraZoom : MonoBehaviour
    {
        [SerializeField] float baseFov = 50f;

        Camera _camera;

        void Awake() => _camera = Camera.main;

        void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            if (!Mathf.Approximately(_camera.fieldOfView, baseFov)) _camera.fieldOfView = baseFov;
        }
    }
}
