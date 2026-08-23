using System.Collections.Generic;
using HighSeas.Data;
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

        public Transform Acquire(Vector3 position, Color color, float radius)
        {
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var visualRadius = radius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f);
            var scale = Mathf.Max(0.2f, visualRadius * 2f * sizeMult);

            if (_settings?.projectilePrefab != null)
            {
                var key = _settings.projectilePrefab.GetInstanceID();
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
                }
                else
                {
                    var instance = Object.Instantiate(_settings.projectilePrefab, _root);
                    instance.name = _settings.projectilePrefab.name;
                    view = instance.transform;
                    StripPhysics(instance);
                }

                view.SetPositionAndRotation(position, Quaternion.identity);
                view.localScale = Vector3.one * scale;
                Tint(view, color);
                return view;
            }

            return CreatePrimitive(position, color, scale);
        }

        public void Release(Transform view)
        {
            if (view == null) return;

            if (_settings?.projectilePrefab != null && view.name == _settings.projectilePrefab.name)
            {
                view.gameObject.SetActive(false);
                view.SetParent(_root, false);
                var key = _settings.projectilePrefab.GetInstanceID();
                if (!_pools.TryGetValue(key, out var stack))
                {
                    stack = new Stack<Transform>(32);
                    _pools[key] = stack;
                }

                stack.Push(view);
                return;
            }

            Object.Destroy(view.gameObject);
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
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);
        }
    }
}
