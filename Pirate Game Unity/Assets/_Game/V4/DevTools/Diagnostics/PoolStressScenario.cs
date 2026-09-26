using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Diagnostics
{
    /// <summary>
    /// Module Lab pool stress (M4). Churn: every frame acquires a batch and releases the oldest, keeping each Module Lab
    /// pool at its prewarm count, so nothing may be created after prewarm. Overflow: pushes each pool past its cap so
    /// its policy acts (RecycleOldest recycles, Grow creates and counts it, Reject refuses). Frame-driven, no randomness.
    /// </summary>
    public sealed class PoolStressScenario
    {
        public const float ChurnSeconds = 5f, OverflowSeconds = 2f;
        public const string LabSource = "catalog (Module Lab)";
        const int BatchPerFrame = 8;

        readonly PoolService _pools;
        readonly List<(string key, int prewarm, int cap)> _keys;
        readonly Dictionary<string, Queue<GameObject>> _held = new Dictionary<string, Queue<GameObject>>();
        readonly Vector3 _origin;
        readonly int _startCreates;
        readonly Dictionary<string, int> _size;
        float _elapsed;
        int _frame;

        public int Acquires { get; private set; }
        public int ChurnCreatesAfterPrewarm { get; private set; } = -1;
        public int OverflowCreatesAfterPrewarm { get; private set; }
        public int Recycled { get; private set; }
        public int Rejected { get; private set; }
        public int PoolCount => _keys.Count;
        public bool Done { get; private set; }
        public bool InOverflow => _elapsed >= ChurnSeconds;
        public string Summary { get; private set; } = "not run";

        public PoolStressScenario(PoolService pools, Vector3 origin)
        {
            _pools = pools;
            _origin = origin;
            _keys = pools.Stats().Where(s => s.Source == LabSource).Select(s => (s.Key, Mathf.Max(1, s.Prewarmed), s.Cap)).ToList();
            foreach (var k in _keys) _held[k.key] = new Queue<GameObject>();
            _pools.ResetPeaks(_held.Keys);
            _startCreates = _pools.TotalCreatedAfterPrewarm;
            // A pool that grew in an earlier run starts larger; overflow targets are relative to its current size.
            _size = pools.Stats().Where(s => _held.ContainsKey(s.Key)).ToDictionary(s => s.Key, s => s.Total);
            if (_keys.Count == 0) { Done = true; Summary = "no Module Lab pools in the catalog"; }
        }

        /// <summary>Advances one frame. Returns true when finished (everything released, results in the properties).</summary>
        public bool Tick(float dt)
        {
            if (Done) return true;
            var overflow = InOverflow;
            foreach (var (key, prewarm, cap) in _keys)
            {
                var held = _held[key];
                var target = overflow ? System.Math.Max(cap > 0 ? cap : prewarm, _size[key]) + 16 : prewarm;
                for (var i = 0; i < BatchPerFrame; i++)
                {
                    // Keep the pool at its target: while churning, release the oldest before acquiring.
                    if (!overflow && held.Count >= target) _pools.Release(held.Dequeue());
                    if (overflow && held.Count >= target) break;
                    var angle = (_frame * BatchPerFrame + i) * 0.37f;
                    var go = _pools.Acquire(key, _origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (4f + i % 6), Quaternion.identity);
                    if (go == null) continue;
                    Acquires++;
                    // RecycleOldest hands back the instance acquired first, which is the head of this queue.
                    if (held.Count > 0 && held.Peek() == go) held.Dequeue();
                    held.Enqueue(go);
                }
            }
            _frame++;
            _elapsed += dt;
            if (!overflow && InOverflow) ChurnCreatesAfterPrewarm = _pools.TotalCreatedAfterPrewarm - _startCreates;
            if (_elapsed < ChurnSeconds + OverflowSeconds) return false;

            foreach (var q in _held.Values) while (q.Count > 0) _pools.Release(q.Dequeue());
            var stats = _pools.Stats().Where(s => _held.ContainsKey(s.Key)).ToList();
            Recycled = stats.Sum(s => s.Recycled);
            Rejected = stats.Sum(s => s.Rejected);
            OverflowCreatesAfterPrewarm = _pools.TotalCreatedAfterPrewarm - _startCreates - ChurnCreatesAfterPrewarm;
            Done = true;
            Summary = $"churn {ChurnSeconds:0} s: {ChurnCreatesAfterPrewarm} created after prewarm ({Acquires} acquires in total); " +
                      $"overflow: {Recycled} recycled, {OverflowCreatesAfterPrewarm} grown, {Rejected} rejected; peaks " +
                      string.Join(", ", stats.Select(s => $"{s.Key} {s.Peak}/{(s.Cap > 0 ? s.Cap.ToString() : "no cap")}"));
            return true;
        }
    }
}
