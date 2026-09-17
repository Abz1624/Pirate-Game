using HighSeas.Data;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.POI
{
    /// <summary>Golden point light + optional loop VFX on salvage chest POIs.</summary>
    public sealed class SalvageChestTreasureGlow : MonoBehaviour
    {
        static readonly Color GoldLight = new(1f, 0.82f, 0.32f);
        static readonly Color GoldVfxTint = new(1f, 0.88f, 0.28f);

        Light _pointLight;
        float _pulsePhase;

        public static void Attach(GameObject chestRoot, DemoJuiceSettings settings)
        {
            if (chestRoot == null || settings == null)
                return;
            if (chestRoot.GetComponentInChildren<SalvageChestTreasureGlow>(true) != null)
                return;

            var glow = chestRoot.AddComponent<SalvageChestTreasureGlow>();
            glow.Build(settings);
        }

        void Build(DemoJuiceSettings settings)
        {
            var anchor = new GameObject("TreasureGlow");
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = ResolveGlowLocalPosition();

            _pointLight = anchor.AddComponent<Light>();
            _pointLight.type = LightType.Point;
            _pointLight.color = GoldLight;
            _pointLight.intensity = 1.15f;
            _pointLight.range = 2.8f;
            _pointLight.shadows = LightShadows.None;

            var glowPrefab = ResolveGlowPrefab(settings);
            if (glowPrefab != null)
            {
                var vfx = Object.Instantiate(glowPrefab, anchor.transform, false);
                vfx.name = "GoldenShineVfx";
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;
                var scale = settings.salvageChestGlowScale;
                vfx.transform.localScale = Vector3.one * scale;
                VfxParticleUtil.ApplyTint(vfx, GoldVfxTint, 0.85f, 1f);
                VfxParticleUtil.RestartParticles(vfx);
            }
        }

        static GameObject ResolveGlowPrefab(DemoJuiceSettings settings) =>
            settings != null ? settings.salvageChestGlowPrefab : null;

        Vector3 ResolveGlowLocalPosition()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Vector3(0f, 0.35f, 0f);

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var top = new Vector3(bounds.center.x, bounds.max.y + 0.08f, bounds.center.z);
            return transform.InverseTransformPoint(top);
        }

        void Update()
        {
            if (_pointLight == null)
                return;

            _pulsePhase += Time.deltaTime * 2.2f;
            _pointLight.intensity = 0.95f + Mathf.Sin(_pulsePhase) * 0.3f;
        }
    }
}
