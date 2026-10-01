using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Diagnostics
{
    /// <summary>
    /// V5 M3e (owner notes d, f, i, j): what a frame costs while sampling: frame time, main-thread time, managed memory
    /// allocated per frame (GC), batches, draw calls, SetPass calls and triangles, from the profiler's counters (Editor and
    /// development builds). The render counters read 0 where the platform does not report them. DevTools only; nothing
    /// is recorded until <see cref="Begin"/>.
    /// </summary>
    public sealed class RunPerfSampler : MonoBehaviour
    {
        ProfilerRecorder _gc, _main, _batches, _draws, _setPass, _tris;
        readonly List<float> _frameMs = new List<float>(4096), _mainMs = new List<float>(4096);
        readonly List<long> _gcBytes = new List<long>(4096), _batch = new List<long>(4096), _draw = new List<long>(4096), _pass = new List<long>(4096), _tri = new List<long>(4096);
        bool _sampling;

        public int Frames => _frameMs.Count;
        public bool Sampling => _sampling;

        public void Begin()
        {
            Stop();
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _frameMs.Clear(); _mainMs.Clear(); _gcBytes.Clear(); _batch.Clear(); _draw.Clear(); _pass.Clear(); _tri.Clear();
            _sampling = true;
        }

        public void End() => _sampling = false;

        void Update()
        {
            if (!_sampling) return;
            _frameMs.Add(Time.unscaledDeltaTime * 1000f);
            _mainMs.Add(_main.Valid ? _main.LastValue * 1e-6f : 0f);
            _gcBytes.Add(_gc.Valid ? _gc.LastValue : 0);
            _batch.Add(_batches.Valid ? _batches.LastValue : 0);
            _draw.Add(_draws.Valid ? _draws.LastValue : 0);
            _pass.Add(_setPass.Valid ? _setPass.LastValue : 0);
            _tri.Add(_tris.Valid ? _tris.LastValue : 0);
        }

        static float Percentile(List<float> v, float p)
        {
            if (v.Count == 0) return 0f;
            var s = v.OrderBy(x => x).ToList();
            return s[Mathf.Clamp(Mathf.CeilToInt(p * s.Count) - 1, 0, s.Count - 1)];
        }

        static double Mean(List<long> v) => v.Count == 0 ? 0 : v.Average(x => (double)x);

        /// <summary>One line: frames, frame ms (mean / p95 / worst), main thread ms, GC bytes per frame (mean and the share of
        /// frames that allocate), batches, draw calls, SetPass calls, triangles (means).</summary>
        public string Summary()
        {
            if (_frameMs.Count == 0) return "no frames";
            var allocating = _gcBytes.Count(b => b > 0) / (float)_gcBytes.Count;
            return $"frames {_frameMs.Count} | frame {_frameMs.Average():0.0} ms (median {Percentile(_frameMs, 0.5f):0.0}, p95 {Percentile(_frameMs, 0.95f):0.0}, worst {_frameMs.Max():0.0})" +
                   $" | main {_mainMs.Average():0.00} ms | GC {Mean(_gcBytes):0} B/frame ({allocating:P0} of frames)" +
                   $" | batches {Mean(_batch):0} | draws {Mean(_draw):0} | setpass {Mean(_pass):0} | tris {Mean(_tri) / 1000.0:0}k";
        }

        void Stop()
        {
            _gc.Dispose(); _main.Dispose(); _batches.Dispose(); _draws.Dispose(); _setPass.Dispose(); _tris.Dispose();
        }

        void OnDestroy() => Stop();
    }
}
