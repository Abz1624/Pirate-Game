using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// One-shot particle effects through the V4 pool (M14; replaces V3's VfxPool / VfxAutoReturn behind JuiceVfxBroker):
    /// each prefab gets a prewarmed pool keyed "fx/&lt;name&gt;"; a played effect restarts its particles and goes back to the
    /// pool after its lifetime (V3's estimate: the longest particle duration + start lifetime, 1.5 s at least).
    /// </summary>
    public sealed class PooledEffects
    {
        struct Live
        {
            public GameObject Go;
            public float ReleaseAt;
        }

        readonly PoolService _pools;
        readonly List<Live> _live = new List<Live>(64);
        readonly Dictionary<GameObject, (string key, float lifetime)> _byPrefab = new Dictionary<GameObject, (string, float)>();

        public PooledEffects(PoolService pools) { _pools = pools; }

        /// <summary>Registers a prefab's pool (prewarmed; grows past the prewarm and counts it).</summary>
        public void Register(GameObject prefab, int prewarm)
        {
            if (prefab == null || _pools == null || _byPrefab.ContainsKey(prefab)) return;
            var key = "fx/" + prefab.name;
            if (!_pools.Has(key)) _pools.Register(new PoolDefinition(key, prefab, prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "effects"));
            _byPrefab[prefab] = (key, EstimateLifetime(prefab));
        }

        public void Prewarm() => _pools?.PrewarmAll();

        /// <summary>Plays a registered effect at the pose; lifetimeOverride > 0 replaces the estimate.</summary>
        public GameObject Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetimeOverride = 0f, Transform parent = null)
        {
            if (prefab == null || _pools == null || !_byPrefab.TryGetValue(prefab, out var entry)) return null;
            var go = _pools.Acquire(entry.key, position, rotation);
            if (go == null) return null;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.localScale = Vector3.one * scale;
            var particles = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }
            _live.Add(new Live { Go = go, ReleaseAt = Time.time + (lifetimeOverride > 0f ? lifetimeOverride : entry.lifetime) });
            return go;
        }

        /// <summary>Returns finished effects to the pool (call once per frame).</summary>
        public void Tick()
        {
            var now = Time.time;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].ReleaseAt > now && _live[i].Go != null) continue;
                if (_live[i].Go != null) _pools.Release(_live[i].Go);
                _live.RemoveAt(i);
            }
        }

        public void ReleaseAll()
        {
            foreach (var l in _live) if (l.Go != null) _pools.Release(l.Go);
            _live.Clear();
        }

        /// <summary>V3 VfxAutoReturn.EstimateLifetime.</summary>
        public static float EstimateLifetime(GameObject prefab, float fallback = 1.5f)
        {
            if (prefab == null) return fallback;
            var longest = fallback;
            foreach (var p in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = p.main;
                var duration = main.duration + main.startLifetime.constantMax;
                if (duration > longest) longest = duration;
            }
            return longest;
        }
    }
}
