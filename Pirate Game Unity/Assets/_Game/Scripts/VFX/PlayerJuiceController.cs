using HighSeas.Data;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class PlayerJuiceController : MonoBehaviour
    {
        [SerializeField] DemoJuiceSettings settings;

        Transform _wake;

        void Start()
        {
            if (settings == null)
                settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");

            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null || settings?.seaFoamTrailPrefab == null) return;

            var instance = Instantiate(settings.seaFoamTrailPrefab, player.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * settings.seaFoamTrailScale;
            _wake = instance.transform;

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        void Update()
        {
            if (_wake == null) return;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;

            var moving = player.Velocity.sqrMagnitude > 0.5f;
            if (_wake.gameObject.activeSelf != moving)
                _wake.gameObject.SetActive(moving);
        }
    }
}
