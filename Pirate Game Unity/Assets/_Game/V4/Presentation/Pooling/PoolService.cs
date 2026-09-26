using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Pooling
{
    /// <summary>A view pool definition: where it comes from (catalog entry or derived) is kept for the console.</summary>
    public readonly struct PoolDefinition
    {
        public readonly string Key;
        public readonly GameObject Prefab;
        public readonly int Prewarm, Cap;
        public readonly ConfigEnums.PoolOverflowPolicy Overflow;
        public readonly string Source;

        public PoolDefinition(string key, GameObject prefab, int prewarm, int cap, ConfigEnums.PoolOverflowPolicy overflow, string source)
        {
            Key = key;
            Prefab = prefab;
            Prewarm = Mathf.Max(0, prewarm);
            Cap = Mathf.Max(0, cap);
            Overflow = overflow;
            Source = source;
        }
    }

    /// <summary>Counters for one pool (console Pools section, telemetry).</summary>
    public struct PoolStats
    {
        public string Key, Source;
        /// <summary>Total = instances alive (free + in use); Prewarmed = created by prewarm.</summary>
        public int Cap, Prewarmed, Total, InUse, Available, Peak, CreatedAfterPrewarm, Recycled, Rejected, Lost;
        public ConfigEnums.PoolOverflowPolicy Overflow;
    }

    /// <summary>Optional hooks on pooled views (any component on the instance or its children).</summary>
    public interface IPoolable
    {
        void OnAcquired();
        void OnReleased();
    }

    /// <summary>Keyed view pools for everything V4 renders. Presentation only: simulation never holds views.</summary>
    public interface IPoolService
    {
        bool Has(string key);
        /// <summary>An instance, active, at the pose; null when the key is unknown or the pool refuses at its cap.</summary>
        GameObject Acquire(string key, Vector3 position, Quaternion rotation);
        /// <summary>Returns an instance; false (and a warning) when it did not come from this service.</summary>
        bool Release(GameObject instance);
        bool IsPooled(GameObject instance);
        IEnumerable<PoolStats> Stats();
        /// <summary>Instances created after prewarm, summed over all pools.</summary>
        int TotalCreatedAfterPrewarm { get; }
    }

    /// <summary>
    /// The V4 view pool (M4). Each key holds free instances (inactive, under the service root) and the ones in use in
    /// acquire order. At the cap: Grow creates anyway and counts it as a create after prewarm, RecycleOldest takes back
    /// the instance acquired first, Reject returns null. Instances are never destroyed by the service while it lives;
    /// an instance destroyed from outside (for example with a parent it was attached to) is dropped and counted as lost.
    /// Acquire and release allocate nothing: each instance keeps its list node and its <see cref="IPoolable"/> hooks.
    /// </summary>
    public sealed class PoolService : IPoolService, IDisposable
    {
        sealed class Pool
        {
            public PoolDefinition Definition;
            public readonly Stack<Instance> Free = new Stack<Instance>();
            public readonly LinkedList<Instance> InUse = new LinkedList<Instance>();
            public int Total, Prewarmed, Peak, CreatedAfterPrewarm, Recycled, Rejected, Lost;
        }

        sealed class Instance
        {
            public GameObject Go;
            public int Id;
            public Pool Pool;
            public LinkedListNode<Instance> Node;
            public IPoolable[] Hooks;
            public bool InUse;
        }

        readonly Transform _root;
        readonly Transform _staging; // inactive: prefabs are instantiated here so no Awake/OnEnable runs until first use
        readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>(StringComparer.Ordinal);
        readonly List<string> _order = new List<string>();
        readonly Dictionary<int, Instance> _owned = new Dictionary<int, Instance>();
        readonly HashSet<int> _createdIds = new HashSet<int>();

        public PoolService(Transform root)
        {
            _root = root;
            var staging = new GameObject("PoolStaging");
            staging.SetActive(false);
            staging.transform.SetParent(root, false);
            _staging = staging.transform;
        }

        public int TotalCreatedAfterPrewarm { get; private set; }

        /// <summary>True for a GameObject (or a child of one) this service created; the creation probe leaves them out.</summary>
        public bool CreatedByPool(int gameObjectInstanceId) => _createdIds.Contains(gameObjectInstanceId);

        public bool Has(string key) => key != null && _pools.ContainsKey(key);

        /// <summary>
        /// Adds a pool. A key registered twice keeps the first definition (and warns): catalogs must use unique keys.
        /// A pool registered after <see cref="PrewarmAll"/> is not prewarmed unless <see cref="Prewarm"/> is called.
        /// </summary>
        public void Register(PoolDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.Key) || definition.Prefab == null) return;
            if (_pools.ContainsKey(definition.Key)) { Debug.LogWarning($"PoolService: duplicate pool key '{definition.Key}' ignored"); return; }
            _pools[definition.Key] = new Pool { Definition = definition };
            _order.Add(definition.Key);
        }

        /// <summary>Creates each pool's prewarm instances (those not created yet). Later creates count as after prewarm.</summary>
        public void PrewarmAll()
        {
            foreach (var key in _order) Prewarm(key);
        }

        public void Prewarm(string key)
        {
            if (!_pools.TryGetValue(key, out var pool)) return;
            while (pool.Total < pool.Definition.Prewarm)
            {
                pool.Free.Push(Create(pool, false));
                pool.Prewarmed++;
            }
        }

        public GameObject Acquire(string key, Vector3 position, Quaternion rotation)
        {
            if (key == null || !_pools.TryGetValue(key, out var pool)) return null;
            Instance instance = null;
            while (pool.Free.Count > 0 && instance == null)
            {
                var candidate = pool.Free.Pop();
                if (candidate.Go != null) instance = candidate;
                else Drop(candidate);
            }
            if (instance == null)
            {
                var atCap = pool.Definition.Cap > 0 && pool.Total >= pool.Definition.Cap;
                if (!atCap || pool.Definition.Overflow == ConfigEnums.PoolOverflowPolicy.Grow)
                    instance = Create(pool, true);
                else if (pool.Definition.Overflow == ConfigEnums.PoolOverflowPolicy.RecycleOldest && (instance = OldestAlive(pool)) != null)
                {
                    ReleaseInternal(instance); // its views reset like any release
                    pool.Free.Pop(); // the instance just released
                    pool.Recycled++;
                }
                else
                {
                    pool.Rejected++;
                    return null;
                }
            }

            var go = instance.Go;
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            instance.InUse = true;
            pool.InUse.AddLast(instance.Node);
            if (pool.InUse.Count > pool.Peak) pool.Peak = pool.InUse.Count;
            foreach (var h in instance.Hooks) h.OnAcquired();
            return go;
        }

        /// <summary>The instance acquired first that still exists (destroyed ones are dropped on the way).</summary>
        Instance OldestAlive(Pool pool)
        {
            while (pool.InUse.First != null)
            {
                var first = pool.InUse.First.Value;
                if (first.Go != null) return first;
                Drop(first);
            }
            return null;
        }

        public bool Release(GameObject instance)
        {
            // A destroyed instance compares equal to null but keeps its id, so it can still be found and dropped.
            if (ReferenceEquals(instance, null)) return false;
            var id = instance.GetInstanceID();
            if (!_owned.TryGetValue(id, out var entry))
            {
                if (instance != null) Debug.LogWarning($"PoolService: {instance.name} was not acquired from a pool");
                return false;
            }
            if (instance == null) { Drop(entry); return false; }
            return entry.InUse && ReleaseInternal(entry);
        }

        bool ReleaseInternal(Instance instance)
        {
            if (!instance.InUse) return false;
            instance.Pool.InUse.Remove(instance.Node);
            instance.InUse = false;
            foreach (var h in instance.Hooks) if (h as Object != null) h.OnReleased();
            instance.Go.SetActive(false);
            instance.Go.transform.SetParent(_root, false);
            instance.Pool.Free.Push(instance);
            return true;
        }

        /// <summary>Forgets an instance destroyed from outside; its pool can create a replacement.</summary>
        void Drop(Instance instance)
        {
            if (instance.InUse) instance.Pool.InUse.Remove(instance.Node);
            instance.InUse = false;
            if (_owned.Remove(instance.Id))
            {
                instance.Pool.Total--;
                instance.Pool.Lost++;
            }
        }

        public bool IsPooled(GameObject instance) => !ReferenceEquals(instance, null) && _owned.ContainsKey(instance.GetInstanceID());

        public IEnumerable<PoolStats> Stats()
        {
            foreach (var key in _order)
            {
                var p = _pools[key];
                yield return new PoolStats
                {
                    Key = key,
                    Source = p.Definition.Source,
                    Cap = p.Definition.Cap,
                    Overflow = p.Definition.Overflow,
                    Prewarmed = p.Prewarmed,
                    Total = p.Total,
                    InUse = p.InUse.Count,
                    Available = p.Free.Count,
                    Peak = p.Peak,
                    CreatedAfterPrewarm = p.CreatedAfterPrewarm,
                    Recycled = p.Recycled,
                    Rejected = p.Rejected,
                    Lost = p.Lost,
                };
            }
        }

        /// <summary>Releases everything in use (for example between Module Lab scenarios). Counters are kept.</summary>
        public void ReleaseAll()
        {
            foreach (var key in _order)
            {
                var pool = _pools[key];
                while (pool.InUse.First != null)
                {
                    var first = pool.InUse.First.Value;
                    if (first.Go != null) ReleaseInternal(first); else Drop(first);
                }
            }
        }

        /// <summary>Clears peaks and the recycled/rejected counts (not the creates after prewarm), for the given keys or all.</summary>
        public void ResetPeaks(IEnumerable<string> keys = null)
        {
            foreach (var key in keys ?? _order)
                if (_pools.TryGetValue(key, out var p)) { p.Peak = p.InUse.Count; p.Recycled = 0; p.Rejected = 0; }
        }

        Instance Create(Pool pool, bool afterPrewarm)
        {
            // Under the inactive staging object the copy never becomes active here, so particles/audio set to play on
            // awake and component Awake/OnEnable wait until the first Acquire.
            var go = Object.Instantiate(pool.Definition.Prefab, _staging, false);
            go.name = pool.Definition.Key;
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            var instance = new Instance { Go = go, Id = go.GetInstanceID(), Pool = pool, Hooks = go.GetComponentsInChildren<IPoolable>(true) };
            instance.Node = new LinkedListNode<Instance>(instance);
            pool.Total++;
            _owned[instance.Id] = instance;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) _createdIds.Add(t.gameObject.GetInstanceID());
            if (afterPrewarm)
            {
                pool.CreatedAfterPrewarm++;
                TotalCreatedAfterPrewarm++;
            }
            return instance;
        }

        public void Dispose()
        {
            foreach (var instance in _owned.Values) Destroy(instance.Go);
            _owned.Clear();
            _pools.Clear();
            _order.Clear();
            _createdIds.Clear();
            if (_staging != null) Destroy(_staging.gameObject);
        }

        static void Destroy(GameObject g)
        {
            if (g == null) return;
            if (Application.isPlaying) Object.Destroy(g); else Object.DestroyImmediate(g);
        }
    }
}
