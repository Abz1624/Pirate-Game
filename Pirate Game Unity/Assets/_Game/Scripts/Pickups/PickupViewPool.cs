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

        public Transform Acquire(PickupType type, Vector3 position)
        {
            var prefab = type == PickupType.Gold
                ? _settings?.goldPickupPrefab
                : _settings?.infamyPickupPrefab;
            var scale = type == PickupType.Gold
                ? _settings?.goldPickupScale ?? 0.38f
                : _settings?.infamyPickupScale ?? 0.42f;
            var color = type == PickupType.Gold
                ? _settings?.goldColor ?? Color.yellow
                : _settings?.infamyColor ?? Color.cyan;

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

        public void Release(Transform view, PickupType type)
        {
            if (view == null) return;

            var prefab = type == PickupType.Gold
                ? _settings?.goldPickupPrefab
                : _settings?.infamyPickupPrefab;

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
