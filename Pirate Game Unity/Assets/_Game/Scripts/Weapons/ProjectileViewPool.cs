using System.Collections.Generic;
using HighSeas.Data;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.Weapons
{
    public sealed class ProjectileViewPool
    {
        readonly Transform _root;
        readonly DemoJuiceSettings _settings;
        readonly Dictionary<int, Stack<Transform>> _pools = new();

        public ProjectileViewPool(Transform root, DemoJuiceSettings settings)
        {
            _root = root;
            _settings = settings;
        }

        public Transform Acquire(Vector3 position, Color color, float radius, GameObject prefabOverride = null, float scaleMult = 1f)
        {
            var prefab = prefabOverride != null ? prefabOverride : _settings?.projectilePrefab;
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var visualRadius = radius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f);
            var scale = Mathf.Max(0.2f, visualRadius * 2f * sizeMult * scaleMult);
            var isHarpoon = prefab != null && _settings?.harpoonProjectilePrefab != null &&
                            prefab == _settings.harpoonProjectilePrefab;
            var useCannonballTrail = VfxParticleUtil.UsesCannonballTrail(prefab, _settings);

            if (prefab != null)
            {
                var key = prefab.GetInstanceID();
                if (!_pools.TryGetValue(key, out var stack))
                {
                    stack = new Stack<Transform>(32);
                    _pools[key] = stack;
                }

                Transform view;
                if (stack.Count > 0)
                {
                    view = stack.Pop();
                    view.gameObject.SetActive(true);
                    if (useCannonballTrail)
                    {
                        VfxParticleUtil.ClearMotionTrails(view.gameObject);
                        VfxParticleUtil.SetTrailEmitting(view.gameObject, true);
                    }
                }
                else
                {
                    var instance = Object.Instantiate(prefab, _root);
                    instance.name = prefab.name;
                    view = instance.transform;
                    StripPhysics(instance);
                    if (isHarpoon)
                        VfxParticleUtil.ConfigureHarpoonProjectile(instance);
                }

                if (isHarpoon)
                    VfxParticleUtil.SetTrailEmitting(view.gameObject, false);

                view.SetPositionAndRotation(position, Quaternion.identity);
                view.localScale = Vector3.one * scale;

                if (useCannonballTrail)
                    VfxParticleUtil.BoostCannonballTrails(view.gameObject);

                if (isHarpoon)
                {
                    VfxParticleUtil.ClearMotionTrails(view.gameObject);
                    VfxParticleUtil.SetTrailEmitting(view.gameObject, true);
                }

                Tint(view, color);
                return view;
            }

            return CreatePrimitive(position, color, scale);
        }

        public void Release(Transform view)
        {
            if (view == null) return;

            VfxParticleUtil.SetTrailEmitting(view.gameObject, false);
            VfxParticleUtil.ClearMotionTrails(view.gameObject);

            var matched = false;
            if (_settings != null)
            {
                if (_settings.projectilePrefab != null && view.name == _settings.projectilePrefab.name)
                    matched = ReleaseToPool(view, _settings.projectilePrefab);
                if (!matched && _settings.smallProjectilePrefab != null && view.name == _settings.smallProjectilePrefab.name)
                    matched = ReleaseToPool(view, _settings.smallProjectilePrefab);
                if (!matched && _settings.harpoonProjectilePrefab != null && view.name == _settings.harpoonProjectilePrefab.name)
                    matched = ReleaseToPool(view, _settings.harpoonProjectilePrefab);
                if (!matched && _settings.weaponVfxBindings != null)
                {
                    for (var i = 0; i < _settings.weaponVfxBindings.Length; i++)
                    {
                        var binding = _settings.weaponVfxBindings[i];
                        if (binding?.projectilePrefab == null || view.name != binding.projectilePrefab.name)
                            continue;
                        matched = ReleaseToPool(view, binding.projectilePrefab);
                        if (matched) break;
                    }
                }
            }

            if (!matched)
                Object.Destroy(view.gameObject);
        }

        bool ReleaseToPool(Transform view, GameObject prefab)
        {
            view.gameObject.SetActive(false);
            view.SetParent(_root, false);
            var key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<Transform>(32);
                _pools[key] = stack;
            }

            stack.Push(view);
            return true;
        }

        Transform CreatePrimitive(Vector3 position, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.material.SetColor("_EmissionColor", color * 0.65f);
                renderer.material.EnableKeyword("_EMISSION");
            }

            return go.transform;
        }

        static void Tint(Transform view, Color color)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].material;
                if (material.HasProperty("_Color"))
                    material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.65f);
                }
            }
        }

        static void StripPhysics(GameObject root)
        {
            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
                Object.Destroy(rigidbodies[i]);

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);
        }
    }
}
