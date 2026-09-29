using System;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Diagnostics;
using RoguePirates.V4.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Pools (M4): every view pool with its source, prewarm, cap, policy, use, peak and creates after prewarm; the
    /// legacy object-creation probe; and, in the Module Lab, the pool stress scenario.
    /// </summary>
    public sealed class PoolsSection : IConsoleSection
    {
        public string Title => "Pools";
        public string SearchText => "pools pool prewarm cap overflow recycle grow reject creation instantiate stress";

        const float TextWidth = 440f; // long lines wrap here, so the console's scroll area never widens past the window
        V4PresentationRoot _root;
        GUIStyle _wrap, _wrapBox;
        PoolStressScenario _stress;
        string _lastStress = "not run";

        public void Draw(TuningConsole console)
        {
            if (_root == null) _root = Object.FindFirstObjectByType<V4PresentationRoot>();
            var pools = _root != null ? _root.Pools : null;
            if (pools == null) { GUILayout.Label("No V4 view pool in this scene."); return; }

            if (_wrap == null)
            {
                _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _wrapBox = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            }
            var stats = pools.Stats().ToList();
            GUILayout.Label($"{stats.Count} pools · {stats.Sum(s => s.InUse)} in use · {stats.Sum(s => s.Prewarmed)} prewarmed · {pools.TotalCreatedAfterPrewarm} created after prewarm", _wrap, GUILayout.Width(TextWidth));
            GUILayout.Label("Nothing in the run renders through the pool yet: weapons move to it in M5, enemy views in M8. Derived enemy pools are listed but not prewarmed until then. Hover a key for its source.", _wrapBox, GUILayout.Width(TextWidth));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key", GUILayout.Width(150));
            GUILayout.Label("Warm", GUILayout.Width(52));
            GUILayout.Label("Cap", GUILayout.Width(40));
            GUILayout.Label("Policy", GUILayout.Width(80));
            GUILayout.Label("Use", GUILayout.Width(32));
            GUILayout.Label("Peak", GUILayout.Width(36));
            GUILayout.Label("Created+", GUILayout.Width(54));
            GUILayout.EndHorizontal();
            var filter = console.SearchText;
            foreach (var s in stats)
            {
                if (filter.Length > 0 && s.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                                      && (s.Source ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(s.Key, s.Source), GUILayout.Width(150));
                GUILayout.Label(s.Prewarmed.ToString(), GUILayout.Width(52));
                GUILayout.Label(s.Cap > 0 ? s.Cap.ToString() : "none", GUILayout.Width(40));
                var extra = s.Recycled > 0 ? $" ({s.Recycled})" : s.Rejected > 0 ? $" ({s.Rejected})" : string.Empty;
                GUILayout.Label(PolicyLabel(s.Overflow) + extra, GUILayout.Width(80));
                GUILayout.Label(s.InUse.ToString(), GUILayout.Width(32));
                GUILayout.Label(s.Peak.ToString(), GUILayout.Width(36));
                GUILayout.Label(s.CreatedAfterPrewarm.ToString(), GUILayout.Width(54));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            var probe = console.CreationProbe;
            if (probe != null)
            {
                GUILayout.BeginHorizontal();
                var on = GUILayout.Toggle(probe.Tracking, "Count objects created outside the pool (scans the scene each frame)", GUILayout.Width(TextWidth - 60));
                if (on != probe.Tracking) probe.SetTracking(on);
                if (GUILayout.Button("Reset", GUILayout.Width(52))) probe.ResetCounts();
                GUILayout.EndHorizontal();
                if (probe.Tracking || probe.Total > 0)
                {
                    GUILayout.Label($"Legacy creation: {probe.Total} in {probe.TrackedSeconds:0} s · {probe.RatePerSecond:0.0}/s now (M14 target: 0)", _wrap, GUILayout.Width(TextWidth));
                    foreach (var kv in probe.Top(8)) GUILayout.Label($"   {kv.Value,5}  {kv.Key}", _wrap, GUILayout.Width(TextWidth));
                }
            }

            if (console.Game == null || !console.Game.IsModuleLab) return;
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = _stress == null;
            if (GUILayout.Button($"Pool stress ({PoolStressScenario.ChurnSeconds + PoolStressScenario.OverflowSeconds:0} s)", GUILayout.Width(150)))
            {
                var ship = Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>();
                _stress = new PoolStressScenario(pools, ship != null ? ship.Position + Vector3.forward * 12f : Vector3.zero);
                console.RunEveryFrame(dt =>
                {
                    if (_stress == null || !_stress.Tick(dt)) return false;
                    _lastStress = _stress.Summary;
                    _stress = null;
                    return true;
                });
            }
            GUI.enabled = true;
            GUILayout.Label(_stress != null ? (_stress.InOverflow ? "overflow..." : "churn...") : _lastStress, _wrap, GUILayout.Width(TextWidth - 160));
            GUILayout.EndHorizontal();
        }

        static string PolicyLabel(ConfigEnums.PoolOverflowPolicy p) =>
            p == ConfigEnums.PoolOverflowPolicy.RecycleOldest ? "recycle" : p == ConfigEnums.PoolOverflowPolicy.Reject ? "reject" : "grow";
    }
}
