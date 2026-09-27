using System.Collections.Generic;
using System.Reflection;
using HighSeas.Combat;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Draws V3's pending ground hazards (Kraken trap circles, mines, nets from <see cref="SaltwindHazardTelegraph"/>),
    /// which V3 never shows: a trap was invisible until it exploded on the ship. Each hazard gets an outline ring and
    /// an inner ring that grows to the outline as the fuse runs down. Visual only: it reads the hazard slots in
    /// LateUpdate, owns no colliders and draws no random numbers, so gameplay and traces are unchanged.
    /// </summary>
    [DeletedInPhase(14, "V4 hazard presenter")]
    public sealed class LegacyHazardTelegraphView : MonoBehaviour
    {
        const int Segments = 40;
        const float Height = 0.15f;
        static readonly Color TrapColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        static readonly Color MineColor = new Color(1f, 0.7f, 0.1f, 0.95f);
        static readonly Color NetColor = new Color(0.35f, 0.8f, 1f, 0.9f);

        /// <summary>The run's hazards (M8: the V4 enemy world's; set by the run before the first frame).</summary>
        public static RoguePirates.V4.Enemies.IHazardView Source;

        /// <summary>
        /// The run's Hellfire patches (M8 playtest): a placeholder ground marker per burning patch, like the trap rings.
        /// The outline is the burn radius; the inner ring shrinks as the patch burns out.
        /// </summary>
        public static RoguePirates.V4.Enemies.HellfirePatches Patches;

        static readonly Color PatchColor = new Color(1f, 0.3f, 0.05f, 0.95f);
        LineRenderer[] _patchOutline, _patchFill;

        LineRenderer[] _outline, _fill;
        float[] _fuseTotal;
        Vector3[] _lastCenter;
        bool[] _wasActive;
        Material _material;

        public static void Ensure()
        {
            if (FindFirstObjectByType<LegacyHazardTelegraphView>() != null) return;
            new GameObject("V4HazardTelegraphs").AddComponent<LegacyHazardTelegraphView>();
        }

        static readonly Color TimedColor = new Color(1f, 0.45f, 0.1f, 0.95f);

        struct TimedRing
        {
            public Vector3 Center;
            public float Radius, Total, Left;
        }

        readonly List<TimedRing> _timed = new List<TimedRing>();
        readonly List<(LineRenderer outline, LineRenderer fill)> _timedViews = new List<(LineRenderer, LineRenderer)>();

        /// <summary>
        /// A landing ring whose inner ring fills over exactly <paramref name="seconds"/> of run time (V4 mortar shells, R-27
        /// fix: V3's reticle showed 1.1 s for a 0.6 s shell).
        /// </summary>
        public static void ShowTimedRing(Vector3 center, float radius, float seconds)
        {
            Ensure();
            var view = FindFirstObjectByType<LegacyHazardTelegraphView>();
            if (view != null) view._timed.Add(new TimedRing { Center = center, Radius = radius, Total = Mathf.Max(0.01f, seconds), Left = seconds });
        }

        void TickTimedRings()
        {
            var paused = HighSeas.Core.RunManager.Instance != null && HighSeas.Core.RunManager.Instance.IsPaused;
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var ring = _timed[i];
                if (!paused) ring.Left -= Time.deltaTime;
                if (ring.Left <= 0f) { _timed.RemoveAt(i); continue; }
                _timed[i] = ring;
            }
            while (_timedViews.Count < _timed.Count)
                _timedViews.Add((CreateRing("TimedOutline" + _timedViews.Count), CreateRing("TimedFill" + _timedViews.Count)));
            for (var i = 0; i < _timedViews.Count; i++)
            {
                var (outline, fill) = _timedViews[i];
                var visible = i < _timed.Count;
                ShowPair(outline, fill, visible);
                if (!visible) continue;
                var ring = _timed[i];
                var progress = 1f - Mathf.Clamp01(ring.Left / ring.Total);
                SetCircle(outline, ring.Center, ring.Radius, TimedColor, 0.3f);
                SetCircle(fill, ring.Center, Mathf.Max(0.05f, ring.Radius * progress), new Color(TimedColor.r, TimedColor.g, TimedColor.b, 0.55f + 0.4f * progress), 0.45f);
            }
        }

        void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            TickTimedRings();
            DrawPatches();
            var source = Source;
            if (source == null) return;
            var capacity = source.Capacity;
            if (_outline == null || _outline.Length != capacity) Allocate(capacity);
            for (var i = 0; i < capacity; i++)
            {
                if (!source.TryGet(i, out var hazard))
                {
                    _wasActive[i] = false;
                    Show(i, false);
                    continue;
                }

                var kind = hazard.kind.ToString();
                var center = hazard.center;
                var radius = hazard.radius;
                var timer = hazard.timer;
                if (!_wasActive[i] || center != _lastCenter[i]) _fuseTotal[i] = Mathf.Max(0.01f, timer); // a new hazard in this slot
                _wasActive[i] = true;
                _lastCenter[i] = center;

                var color = kind == "Mine" ? MineColor : kind == "NetSlow" ? NetColor : TrapColor;
                // Mines: fill while arming, then a full ring while armed. Traps: fill as the fuse runs down. Nets: full.
                var armed = kind == "Mine" && hazard.armed;
                var progress = kind == "NetSlow" || armed ? 1f : 1f - Mathf.Clamp01(timer / _fuseTotal[i]);
                Show(i, true);
                SetCircle(_outline[i], center, radius, color, 0.3f);
                SetCircle(_fill[i], center, Mathf.Max(0.05f, radius * progress), new Color(color.r, color.g, color.b, 0.55f + 0.4f * progress), 0.45f);
            }
        }

        void DrawPatches()
        {
            var patches = Patches;
            if (patches == null)
            {
                if (_patchOutline != null)
                    for (var i = 0; i < _patchOutline.Length; i++) ShowPair(_patchOutline[i], _patchFill[i], false);
                return;
            }
            if (_patchOutline == null || _patchOutline.Length != patches.Capacity)
            {
                _patchOutline = new LineRenderer[patches.Capacity];
                _patchFill = new LineRenderer[patches.Capacity];
                for (var i = 0; i < patches.Capacity; i++)
                {
                    _patchOutline[i] = CreateRing("PatchOutline" + i);
                    _patchFill[i] = CreateRing("PatchFill" + i);
                }
            }
            for (var i = 0; i < _patchOutline.Length; i++)
            {
                var live = patches.TryGet(i, out var center, out var radius, out var lifeLeft);
                ShowPair(_patchOutline[i], _patchFill[i], live);
                if (!live) continue;
                SetCircle(_patchOutline[i], center, radius, PatchColor, 0.3f);
                SetCircle(_patchFill[i], center, Mathf.Max(0.05f, radius * lifeLeft), new Color(PatchColor.r, PatchColor.g, PatchColor.b, 0.45f + 0.5f * lifeLeft), 0.45f);
            }
        }

        static void ShowPair(LineRenderer outline, LineRenderer fill, bool visible)
        {
            if (outline.gameObject.activeSelf != visible) outline.gameObject.SetActive(visible);
            if (fill.gameObject.activeSelf != visible) fill.gameObject.SetActive(visible);
        }

        void Allocate(int count)
        {
            _outline = new LineRenderer[count];
            _fill = new LineRenderer[count];
            _fuseTotal = new float[count];
            _lastCenter = new Vector3[count];
            _wasActive = new bool[count];
            for (var i = 0; i < count; i++)
            {
                _outline[i] = CreateRing("HazardOutline" + i);
                _fill[i] = CreateRing("HazardFill" + i);
            }
        }

        LineRenderer CreateRing(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = _material;
            child.SetActive(false);
            return line;
        }

        void Show(int i, bool visible) => ShowPair(_outline[i], _fill[i], visible);

        static void SetCircle(LineRenderer line, Vector3 center, float radius, Color color, float width)
        {
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (var s = 0; s < Segments; s++)
            {
                var a = s * Mathf.PI * 2f / Segments;
                line.SetPosition(s, new Vector3(center.x + Mathf.Cos(a) * radius, Height, center.z + Mathf.Sin(a) * radius));
            }
        }
    }
}
