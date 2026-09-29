using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Player projectile views through the V4 view pool (M5; V3's ProjectileViewPool). The model comes from the firing
    /// weapon's config (projectileVisual) or the default projectile model; scale, tint and trails follow V3. Views are
    /// presentation only: the simulation hands out an int handle and never touches the objects.
    /// </summary>
    public sealed class ProjectileViewPresenter : IProjectileViews
    {
        public const string KeyPrefix = "projectile/";

        readonly PoolService _pools;
        readonly ConfigSetRuntime _config;
        readonly DemoJuiceSettings _settings;
        readonly Dictionary<int, GameObject> _live = new Dictionary<int, GameObject>();
        readonly HashSet<int> _prepared = new HashSet<int>();
        readonly Dictionary<string, WeaponConfig> _weapons = new Dictionary<string, WeaponConfig>();
        int _next;

        public ProjectileViewPresenter(PoolService pools, ConfigSetRuntime config, DemoJuiceSettings settings)
        {
            _pools = pools;
            _config = config;
            _settings = settings;
        }

        public int LiveCount => _live.Count;

        /// <summary>The pool key for a model (the catalog derives these from the weapons, so they are prewarmed).</summary>
        public static string KeyFor(GameObject prefab) => prefab != null ? KeyPrefix + prefab.name : null;

        public int Acquire(string weaponId, Vector3 position, Color color, float radius)
        {
            var visual = VisualOf(weaponId);
            var prefab = visual != null && visual.prefab != null ? visual.prefab : _settings != null ? _settings.projectilePrefab : null;
            var scaleMult = visual != null ? visual.scaleMultiplier : 1f;
            var visualRadius = radius * (visual != null ? visual.radiusMultiplier : 1f);
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var scale = Mathf.Max(0.2f, visualRadius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f) * 2f * sizeMult * scaleMult);

            var key = KeyFor(prefab);
            if (key == null) return -1;
            if (!_pools.Has(key)) _pools.Register(new PoolDefinition(key, prefab, 0, 0, ConfigEnums.PoolOverflowPolicy.Grow, "projectile (on demand)"));
            var go = _pools.Acquire(key, position, Quaternion.identity);
            if (go == null) return -1;

            var isHarpoon = _settings != null && _settings.harpoonProjectilePrefab != null && prefab == _settings.harpoonProjectilePrefab;
            var cannonballTrail = VfxParticleUtil.UsesCannonballTrail(prefab, _settings);
            if (_prepared.Add(go.GetInstanceID()))
            {
                StripPhysics(go);
                if (isHarpoon) VfxParticleUtil.ConfigureHarpoonProjectile(go);
            }
            else if (cannonballTrail)
            {
                VfxParticleUtil.ClearMotionTrails(go);
                VfxParticleUtil.SetTrailEmitting(go, true);
            }

            if (isHarpoon) VfxParticleUtil.SetTrailEmitting(go, false);
            go.transform.localScale = Vector3.one * scale;
            if (cannonballTrail) VfxParticleUtil.BoostCannonballTrails(go);
            if (isHarpoon)
            {
                VfxParticleUtil.ClearMotionTrails(go);
                VfxParticleUtil.SetTrailEmitting(go, true);
            }
            Tint(go.transform, color);

            var handle = _next++;
            _live[handle] = go;
            return handle;
        }

        public void Move(int handle, Vector3 position, Vector3 velocity)
        {
            if (!_live.TryGetValue(handle, out var go) || go == null) return;
            go.transform.position = position;
            if (velocity.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        public void Release(int handle)
        {
            if (!_live.TryGetValue(handle, out var go)) return;
            _live.Remove(handle);
            if (go == null) return;
            VfxParticleUtil.SetTrailEmitting(go, false);
            VfxParticleUtil.ClearMotionTrails(go);
            _pools.Release(go);
        }

        ProjectileVisual VisualOf(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || _config == null) return null;
            if (!_weapons.TryGetValue(weaponId, out var config))
            {
                foreach (var w in _config.All<WeaponConfig>())
                    if (w.weaponId == weaponId) { config = w; break; }
                _weapons[weaponId] = config;
            }
            return config != null ? config.projectileVisual : null;
        }

        static void Tint(Transform view, Color color)
        {
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.material;
                if (material.HasProperty("_Color")) material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.65f);
                }
            }
        }

        static void StripPhysics(GameObject root)
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(body);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
        }
    }
}
