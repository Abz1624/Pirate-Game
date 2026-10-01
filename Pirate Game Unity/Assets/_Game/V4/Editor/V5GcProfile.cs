using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Profiling;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M3e (owner note d): where the run's per-frame garbage and main-thread time go. <see cref="Start"/> records
    /// profiler frames in the open Editor's play mode; <see cref="Report"/> walks the recorded main-thread hierarchies
    /// and lists the markers that allocate (self bytes, without their children) and take time, averaged per frame.
    /// Called from the M3e perf check through the Unity CLI; Editor only.
    /// </summary>
    public static class V5GcProfile
    {
        public static void Start()
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            Profiler.enabled = true;
            ProfilerDriver.enabled = true;
        }

        public static string Report(int top = 30)
        {
            ProfilerDriver.enabled = false;
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first) return "no frames recorded";
            var bytes = new Dictionary<string, double>();
            var ms = new Dictionary<string, double>();
            var frames = 0;
            for (var f = first; f <= last; f++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnGcMemory, false);
                if (view == null || !view.valid) continue;
                frames++;
                Walk(view, view.GetRootItemID(), "", bytes, ms);
            }
            if (frames == 0) return "no valid frames";
            var sb = new StringBuilder();
            sb.AppendLine($"frames {frames}; total GC {bytes.Values.Sum() / frames:0} B/frame");
            sb.AppendLine("self GC per frame (bytes), by marker path:");
            foreach (var kv in bytes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(top))
                sb.AppendLine($"  {kv.Value / frames,8:0}  {kv.Key}");
            sb.AppendLine("self time per frame (ms), by marker path:");
            foreach (var kv in ms.OrderByDescending(kv => kv.Value).Take(top))
                sb.AppendLine($"  {kv.Value / frames,8:0.000}  {kv.Key}");
            return sb.ToString();
        }

        static void Walk(HierarchyFrameDataView view, int id, string path, Dictionary<string, double> bytes, Dictionary<string, double> ms)
        {
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (var child in children)
            {
                var name = view.GetItemName(child);
                var p = path.Length == 0 ? name : (path.Count(c => c == '>') >= 3 ? path.Substring(path.IndexOf('>') + 1) : path) + ">" + name;
                var total = view.GetItemColumnDataAsDouble(child, HierarchyFrameDataView.columnGcMemory);
                var self = total;
                var selfMs = view.GetItemColumnDataAsDouble(child, HierarchyFrameDataView.columnSelfTime);
                var grand = new List<int>();
                view.GetItemChildren(child, grand);
                foreach (var g in grand) self -= view.GetItemColumnDataAsDouble(g, HierarchyFrameDataView.columnGcMemory);
                if (self > 0) bytes[p] = (bytes.TryGetValue(p, out var b) ? b : 0) + self;
                ms[p] = (ms.TryGetValue(p, out var m) ? m : 0) + selfMs;
                Walk(view, child, p, bytes, ms);
            }
        }
    }
}
