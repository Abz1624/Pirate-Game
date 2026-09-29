using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Diagnostics
{
    /// <summary>
    /// Frame times while sampling (M14.15 perf check): unscaled frame durations, reported as average, 95th percentile and
    /// worst, with the frame count. DevTools only; nothing runs until <see cref="Begin"/>.
    /// </summary>
    public sealed class FrameTimeSampler : MonoBehaviour
    {
        readonly List<float> _ms = new List<float>(8192);
        bool _sampling;

        public int Frames => _ms.Count;

        public void Begin()
        {
            _ms.Clear();
            _sampling = true;
        }

        public void End() => _sampling = false;

        void Update()
        {
            if (_sampling) _ms.Add(Time.unscaledDeltaTime * 1000f);
        }

        public string Summary()
        {
            if (_ms.Count == 0) return "no frames";
            var sorted = new List<float>(_ms);
            sorted.Sort();
            var sum = 0f;
            foreach (var v in sorted) sum += v;
            var p95 = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.95f) - 1, 0, sorted.Count - 1)];
            return $"frames={sorted.Count} avg={sum / sorted.Count:0.00}ms p95={p95:0.00}ms max={sorted[sorted.Count - 1]:0.00}ms";
        }
    }
}
