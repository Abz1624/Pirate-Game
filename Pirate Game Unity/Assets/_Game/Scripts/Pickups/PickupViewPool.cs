using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Pickups
{
    public sealed class PickupViewPool
    {
        readonly Transform _root;
        readonly DemoJuiceSettings _settings;
        readonly Dictionary<int, Stack<Transform>> _pools = new();

        public PickupViewPool(Transform root, DemoJuiceSettings settings)
        {
            _root = root;
            _settings = settings;
        }

        public Transform Acquire(PickupType type, Vector3 position, int value = 1)
        {
            var prefab = GetPrefab(type, value);
            var scale = GetScale(type, value);
            var color = GetColor(type);

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
                }
                else
                {
                    var instance = Object.Instantiate(prefab, _root);
                    instance.name = prefab.name;
                    view = instance.transform;
                    StripPhysics(instance);
                }

                view.position = position;
                view.localScale = Vector3.one * scale;
                return view;
            }

            return CreatePrimitive(position, color, scale);
        }

        public void Release(Transform view, PickupType type, int value = 1)
        {
            if (view == null) return;

            var prefab = GetPrefab(type, value);
            if (prefab != null && view.name == prefab.name)
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
                return;
            }

            Object.Destroy(view.gameObject);
        }

        GameObject GetPrefab(PickupType type, int value)
        {
            if (_settings == null) return null;
            return type switch
            {
                PickupType.GoldBarrel => _settings.goldBarrelPickupPrefab != null
                    ? _settings.goldBarrelPickupPrefab
                    : _settings.goldPickupPrefab,
                PickupType.Gold or PickupType.FlyingGold => _settings.goldPickupPrefab,
                PickupType.Powerup => _settings.healthPickupPrefab,
                _ => _settings.infamyPickupPrefab
            };
        }

        float GetScale(PickupType type, int value)
        {
            if (_settings == null) return 0.4f;
            return type switch
            {
                PickupType.GoldBarrel => _settings.goldBarrelPickupScale,
                PickupType.Gold or PickupType.FlyingGold => _settings.goldPickupScale * 0.85f,
                PickupType.Powerup => _settings.healthPickupScale,
                _ => _settings.infamyPickupScale
            };
        }

        static Color GetColor(PickupType type) => type switch
        {
            PickupType.Gold or PickupType.GoldBarrel or PickupType.FlyingGold => new Color(1f, 0.85f, 0.15f),
            PickupType.Powerup => new Color(0.35f, 1f, 0.45f),
            _ => new Color(0.2f, 0.95f, 1f)
        };

        Transform CreatePrimitive(Vector3 position, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pickup";
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.material.SetColor("_EmissionColor", color * 0.8f);
                renderer.material.EnableKeyword("_EMISSION");
            }

            return go.transform;
        }

        static void StripPhysics(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);
        }
    }
}
