using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Enemy cannonball models (M14.9b; port of V3's ProjectileViewPool as the hostile shots used it: the default
    /// projectile model, V3's scale formula, the shot tint and the cannonball trail) through the V4 view pool, key
    /// "hostile/&lt;model&gt;", prewarmed at bind. The impact splash is the weapon presenter's water splash (V3 shared the
    /// broker's splash and its merge window).
    /// </summary>
    public sealed class HostileShotPresenter : IHostileShotViews
    {
        public const string KeyPrefix = "hostile/";
        const int Prewarm = 24;

        readonly PoolService _pools;
        readonly DemoJuiceSettings _settings;
        readonly Action<Vector3> _splash;
        readonly GameObject _prefab;
        readonly string _key;
        readonly bool _cannonballTrail;
        readonly HashSet<int> _prepared = new HashSet<int>();
        GameObject _primitive;

        public int LiveCount { get; private set; }

        public HostileShotPresenter(PoolService pools, DemoJuiceSettings settings, Action<Vector3> splash)
        {
            _pools = pools;
            _settings = settings;
            _splash = splash;
            _prefab = settings != null ? settings.projectilePrefab : null;
            if (_prefab == null)
            {
                _prefab = _primitive = PrimitiveTemplate();
                if (_pools != null) _primitive.transform.SetParent(_pools.Staging, false);
            }
            _cannonballTrail = _primitive == null && VfxParticleUtil.UsesCannonballTrail(_prefab, _settings);
            _key = KeyPrefix + _prefab.name;
            if (_pools != null && !_pools.Has(_key))
            {
                _pools.Register(new PoolDefinition(_key, _prefab, Prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "hostile shots"));
                _pools.Prewarm(_key);
            }
        }

        /// <summary>V3 ProjectileViewPool.Acquire (no override prefab, scale ×1).</summary>
        public Transform Acquire(Vector3 position, Color color, float visualRadius)
        {
            var go = _pools?.Acquire(_key, position, Quaternion.identity);
            if (go == null) return null;
            if (_prepared.Add(go.GetInstanceID())) StripPhysics(go);
            else if (_cannonballTrail)
            {
                VfxParticleUtil.ClearMotionTrails(go);
                VfxParticleUtil.SetTrailEmitting(go, true);
            }
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var radius = visualRadius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f);
            go.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius * 2f * sizeMult);
            if (_cannonballTrail) VfxParticleUtil.BoostCannonballTrails(go);
            Tint(go.transform, color, 0.65f);
            LiveCount++;
            return go.transform;
        }

        public void Release(Transform view)
        {
            if (view == null) return;
            VfxParticleUtil.SetTrailEmitting(view.gameObject, false);
            VfxParticleUtil.ClearMotionTrails(view.gameObject);
            _pools?.Release(view.gameObject);
            LiveCount = Mathf.Max(0, LiveCount - 1);
        }

        public void Splash(Vector3 position) => _splash?.Invoke(position);

        public void Dispose()
        {
            if (_primitive != null) Remove(_primitive);
        }

        /// <summary>V3 CreatePrimitive (no projectile model): a collider-less sphere, as a pool template.</summary>
        static GameObject PrimitiveTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HostileShot";
            go.SetActive(false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Remove(collider);
            return go;
        }

        static void Tint(Transform view, Color color, float emission)
        {
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.material;
                if (material.HasProperty("_Color")) material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * emission);
                }
            }
        }

        static void StripPhysics(GameObject root)
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) Remove(body);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Remove(collider);
        }

        /// <summary>Components go at once (templates are stripped before the pool clones them; edit-mode safe).</summary>
        static void Remove(Object o)
        {
            if (Application.isPlaying && o is GameObject) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
