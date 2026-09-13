using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class EnemyViewPool
    {
        readonly Transform _root;
        readonly Dictionary<int, Stack<Transform>> _pools = new();

        public EnemyViewPool(Transform root)
        {
            _root = root;
        }

        public Transform Acquire(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            if (prefab == null)
                return null;

            var key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<Transform>(16);
                _pools[key] = stack;
            }

            Transform view;
            if (stack.Count > 0)
            {
                view = stack.Pop();
                StripNavMeshComponents(view.gameObject);
                view.gameObject.SetActive(true);
            }
            else
            {
                var instance = Object.Instantiate(prefab, _root);
                instance.name = prefab.name;
                instance.SetActive(false);
                StripPhysics(instance);
                StripNavMeshComponents(instance);
                view = instance.transform;
            }

            view.SetPositionAndRotation(position, rotation);
            view.localScale = Vector3.one * scale;
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(Transform view, GameObject prefab)
        {
            if (view == null)
                return;

            StripNavMeshComponents(view.gameObject);
            view.gameObject.SetActive(false);
            view.SetParent(_root, false);

            if (prefab == null)
            {
                Object.Destroy(view.gameObject);
                return;
            }

            var key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<Transform>(16);
                _pools[key] = stack;
            }

            stack.Push(view);
        }

        static void StripPhysics(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(colliders[i]);
                else
#endif
                    Object.Destroy(colliders[i]);
            }

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(rigidbodies[i]);
                else
#endif
                    Object.Destroy(rigidbodies[i]);
            }
        }

        static void StripNavMeshComponents(GameObject root)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;

                var typeName = component.GetType().Name;
                if (typeName is not ("NavMeshAgent" or "NavMeshObstacle"))
                    continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(component);
                else
#endif
                    Object.Destroy(component);
            }
        }
    }
}
