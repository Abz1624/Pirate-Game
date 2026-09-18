using HighSeas.Data;
using UnityEngine;

namespace HighSeas.VFX
{
    public static class VfxParticleUtil
    {
        public static void SetRenderersEnabled(GameObject root, bool enabled)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].enabled = enabled;
        }

        public static void RestartParticles(GameObject root)
        {
            if (root == null) return;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.startDelay = 0f;
                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }

        public static void ApplyTint(GameObject root, Color tint, float saturation = 0.72f, float alphaMultiplier = 1f)
        {
            if (root == null) return;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                var baseColor = main.startColor.color;
                var blended = Color.Lerp(baseColor, tint, saturation);
                blended.a = baseColor.a * alphaMultiplier;
                main.startColor = blended;
            }
        }

        public static void ClearMotionTrails(GameObject root)
        {
            if (root == null) return;
            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
                trails[i].emitting = false;
            }

            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var trailsModule = particles[i].trails;
                if (trailsModule.enabled)
                    particles[i].Clear(true);
            }
        }

        public static void ConfigureHarpoonProjectile(GameObject root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                if (string.Equals(t.name, "Trail2", System.StringComparison.OrdinalIgnoreCase))
                    t.gameObject.SetActive(false);
            }
        }

        public static void SetTrailEmitting(GameObject root, bool emitting)
        {
            if (root == null) return;
            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var i = 0; i < trails.Length; i++)
            {
                if (!trails[i].gameObject.activeInHierarchy) continue;
                trails[i].emitting = emitting;
                if (!emitting)
                    trails[i].Clear();
            }
        }

        public static bool UsesCannonballTrail(GameObject prefab, DemoJuiceSettings settings)
        {
            if (prefab == null || settings == null) return false;
            if (settings.harpoonProjectilePrefab != null && prefab == settings.harpoonProjectilePrefab)
                return false;
            if (prefab == settings.projectilePrefab || prefab == settings.smallProjectilePrefab)
                return true;
            if (prefab.name.IndexOf("Cannonball", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (settings.weaponVfxBindings == null) return false;
            for (var i = 0; i < settings.weaponVfxBindings.Length; i++)
            {
                var binding = settings.weaponVfxBindings[i];
                if (binding?.projectilePrefab == prefab)
                    return binding.weaponId != "harpoon_gun";
            }

            return false;
        }

        public static void BoostCannonballTrails(GameObject root)
        {
            if (root == null) return;
            PrepareProjectileVisual(root, true);
            SetTrailEmitting(root, true);
            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var i = 0; i < trails.Length; i++)
            {
                trails[i].time = Mathf.Max(trails[i].time, 0.38f);
                trails[i].widthMultiplier = Mathf.Max(trails[i].widthMultiplier, 0.9f);
            }
        }

        public static void PrepareProjectileVisual(GameObject root, bool allowTrails)
        {
            if (root == null) return;
            ClearMotionTrails(root);
            if (!allowTrails)
            {
                foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                    trail.gameObject.SetActive(false);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root.transform) continue;
                    if (t.name.Contains("Trail", System.StringComparison.OrdinalIgnoreCase))
                        t.gameObject.SetActive(false);
                }

                return;
            }

            TrailRenderer primaryTrail = null;
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (primaryTrail == null)
                {
                    primaryTrail = trail;
                    trail.gameObject.SetActive(true);
                    trail.emitting = true;
                }
                else
                {
                    trail.gameObject.SetActive(false);
                }
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                if (!t.name.Contains("Trail", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (primaryTrail != null && t == primaryTrail.transform) continue;
                t.gameObject.SetActive(false);
            }
        }
    }
}
