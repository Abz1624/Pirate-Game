using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class VfxPool
    {
        readonly Transform _root;
        readonly Dictionary<int, Stack<GameObject>> _pools = new();

        public VfxPool(Transform root) => _root = root;

        public void Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetimeOverride = 0f)
        {
            if (prefab == null) return;

            var instance = Acquire(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;

            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }

            var lifetime = lifetimeOverride > 0f
                ? lifetimeOverride
                : VfxAutoReturn.EstimateLifetime(prefab);

            var autoReturn = instance.GetComponent<VfxAutoReturn>();
            if (autoReturn == null)
                autoReturn = instance.AddComponent<VfxAutoReturn>();

            autoReturn.Play(lifetime, go => Release(prefab, go));
        }

        public GameObject AcquirePublic(GameObject prefab) => Acquire(prefab);

        public void ReleasePublic(GameObject prefab, GameObject instance) => Release(prefab, instance);

        GameObject Acquire(GameObject prefab)
        {
            var key = prefab.GetInstanceID();
            if (_pools.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                var existing = stack.Pop();
                existing.SetActive(true);
                return existing;
            }

            var instance = Object.Instantiate(prefab, _root);
            instance.name = prefab.name;
            return instance;
        }

        void Release(GameObject prefab, GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            instance.transform.SetParent(_root, false);

            var key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>(16);
                _pools[key] = stack;
            }

            stack.Push(instance);
        }
    }
}
