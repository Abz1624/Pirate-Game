using System.Collections.Generic;
using System.Text.RegularExpressions;
using RoguePirates.V4.Presentation;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Diagnostics
{
    /// <summary>
    /// Counts GameObjects created during a run outside the V4 view pool (M4): while enabled, it scans the scene every
    /// frame and counts each new object tree once (its topmost new object), grouped by name with "(Clone)" and
    /// numbering stripped. Objects the pool created are left out. The count while playing is the legacy creation
    /// baseline that M14 has to bring to zero. Off by default: it scans the whole scene every frame. Objects created
    /// and destroyed within one frame are not seen, and a child added to a new pooled object in its first frame is
    /// counted with the pool (left out).
    /// </summary>
    public sealed class SceneObjectCreationProbe : MonoBehaviour
    {
        const float RateWindowSeconds = 10f;
        static readonly Regex CloneSuffix = new Regex(@"(\s*\(Clone\))+|\s*\(\d+\)$|\d+$", RegexOptions.Compiled);

        readonly HashSet<int> _known = new HashSet<int>();
        readonly HashSet<int> _seenNow = new HashSet<int>();
        readonly HashSet<int> _newNow = new HashSet<int>();
        readonly Dictionary<string, int> _byName = new Dictionary<string, int>();
        readonly Queue<(float time, int count)> _window = new Queue<(float, int)>();
        bool _baselined;
        int _windowSum;
        V4PresentationRoot _presentation;

        public bool Tracking { get; private set; }
        /// <summary>Object trees created outside the pool since tracking started or was reset.</summary>
        public int Total { get; private set; }
        public float TrackedSeconds { get; private set; }
        /// <summary>Creations per second over the last 10 s.</summary>
        public float RatePerSecond => _windowSum / Mathf.Clamp(TrackedSeconds, 1f, RateWindowSeconds);
        public IReadOnlyDictionary<string, int> ByName => _byName;

        public void SetTracking(bool on)
        {
            if (on == Tracking) return;
            Tracking = on;
            _baselined = false; // the first scan after switching on only records what exists
        }

        public void ResetCounts()
        {
            Total = 0;
            TrackedSeconds = 0f;
            _byName.Clear();
            _window.Clear();
            _windowSum = 0;
        }

        void LateUpdate()
        {
            if (!Tracking) return;
            if (_presentation == null) _presentation = FindFirstObjectByType<V4PresentationRoot>();
            var pools = _presentation != null ? _presentation.Pools : null;

            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _seenNow.Clear();
            _newNow.Clear();
            foreach (var t in all)
            {
                var id = t.gameObject.GetInstanceID();
                _seenNow.Add(id);
                if (_baselined && !_known.Contains(id)) _newNow.Add(id);
            }

            var created = 0;
            if (_baselined)
            {
                foreach (var t in all)
                {
                    var id = t.gameObject.GetInstanceID();
                    if (!_newNow.Contains(id)) continue;
                    if (pools != null && pools.CreatedByPool(id)) continue;
                    var parent = t.parent;
                    if (parent != null && _newNow.Contains(parent.gameObject.GetInstanceID())) continue; // counted with its root
                    var name = CloneSuffix.Replace(t.gameObject.name, string.Empty).Trim();
                    if (name.Length == 0) name = "(unnamed)";
                    _byName.TryGetValue(name, out var n);
                    _byName[name] = n + 1;
                    created++;
                }
                Total += created;
                TrackedSeconds += Time.unscaledDeltaTime;
            }

            var now = Time.unscaledTime;
            _window.Enqueue((now, created));
            _windowSum += created;
            while (_window.Count > 0 && _window.Peek().time < now - RateWindowSeconds) _windowSum -= _window.Dequeue().count;

            // Remember what exists now (drops destroyed objects, so the set stays the size of the scene).
            _known.Clear();
            _known.UnionWith(_seenNow);
            _baselined = true;
        }

        /// <summary>The names with the most creations, for the console and the playable check.</summary>
        public List<KeyValuePair<string, int>> Top(int count)
        {
            var list = new List<KeyValuePair<string, int>>(_byName);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }
    }
}
