using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.POI
{
    /// <summary>Submerges salvage chests slightly and bobs them on the water like loot barrels.</summary>
    public sealed class SalvageChestWaterFloat : MonoBehaviour
    {
        const float BobAmplitude = 0.12f;
        const float BobFrequency = 3.5f;

        [Tooltip("Extra sink as a fraction of chest height (0.05 = 5% lower in the water).")]
        [Range(0f, 0.25f)]
        public float extraSubmergeFraction = 0.05f;

        DemoJuiceSettings _settings;
        Vector3 _baseWorldPosition;
        float _bobPhase;
        bool _aligned;

        public static void Attach(GameObject chestRoot, DemoJuiceSettings settings)
        {
            if (chestRoot == null)
                return;

            if (!chestRoot.TryGetComponent<SalvageChestWaterFloat>(out var floating))
                floating = chestRoot.AddComponent<SalvageChestWaterFloat>();

            floating.Initialize(settings);
        }

        void Initialize(DemoJuiceSettings settings)
        {
            _settings = settings;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
            AlignToWaterSurface();
        }

        void AlignToWaterSurface()
        {
            var waterY = _settings != null ? _settings.pickupWaterSurfaceY : 0f;

            if (!TryGetVisualBounds(out var bounds))
            {
                _baseWorldPosition = transform.position;
                _baseWorldPosition.y = waterY + 0.05f;
                transform.position = _baseWorldPosition;
                _aligned = true;
                return;
            }

            var height = bounds.size.y;
            var submerged = (_settings != null ? _settings.barrelSubmergedFraction : 0.15f) + extraSubmergeFraction;
            var targetMinY = waterY - submerged * height;
            transform.position += Vector3.up * (targetMinY - bounds.min.y);
            _baseWorldPosition = transform.position;
            _aligned = true;
        }

        void Update()
        {
            if (!_aligned)
                return;

            var bob = Mathf.Sin(Time.time * BobFrequency + _bobPhase) * BobAmplitude;
            transform.position = _baseWorldPosition + Vector3.up * bob;
        }

        bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var rendererBounds = renderers[i].bounds;
                if (rendererBounds.size.sqrMagnitude < 0.0001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(rendererBounds);
            }

            return hasBounds;
        }
    }
}
