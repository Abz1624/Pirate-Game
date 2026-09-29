using UnityEngine;

namespace RoguePirates.V4.Foundation.PlayerSystems
{
    [DefaultExecutionOrder(150)]
    public sealed class BoostCameraZoom : MonoBehaviour
    {
        [SerializeField] float baseFov = 50f;
        [SerializeField] float boostFov = 58f;

        Camera _camera;
        ShipBoostController _boost;

        void Awake()
        {
            _camera = Camera.main;
            _boost = GetComponent<ShipBoostController>();
        }

        void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _boost == null) return;
            var t = _boost.BoostNormalized;
            _camera.fieldOfView = Mathf.Lerp(baseFov, boostFov, t);
        }
    }
}
