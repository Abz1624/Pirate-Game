using System;
using System.Collections.Generic;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Ground rings for pending hazards (M14.9b; was LegacyHazardTelegraphView, a V4 addition: V3 never showed a Kraken
    /// trap until it exploded on the ship). Each hazard gets an outline ring and an inner ring that grows to the outline as
    /// the fuse runs down; burning patches get a ring that shrinks as they burn out; timed rings (mortar landings, R-27
    /// fix) fill over their exact delay. Visual only: it reads the hazard and patch views in LateUpdate. Every ring is
    /// created at bind for the views' capacities (timed rings: a prewarmed set that grows if ever exceeded).
    /// </summary>
    public sealed class HazardRingPresenter : MonoBehaviour
    {
        const int PrewarmTimed = 8;
        const int Segments = 40;
        const float Height = 0.15f;
        static readonly Color TrapColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        static readonly Color MineColor = new Color(1f, 0.7f, 0.1f, 0.95f);
        static readonly Color NetColor = new Color(0.35f, 0.8f, 1f, 0.9f);
        // Playtest (3 Oct, owner, round 7: enemy attacks must be readable): the warning circle and lane of an attack the
        // enemy deals itself (charges, slams, bomb boats, aimed cannonballs), a strong red.
        static readonly Color WarningColor = new Color(1f, 0.12f, 0.08f, 0.95f);

        IHazardView _hazards;
        IBurningPatchView _patches;
        Func<bool> _paused;

        static readonly Color PatchColor = new Color(1f, 0.3f, 0.05f, 0.95f);
        LineRenderer[] _patchOutline, _patchFill;

        LineRenderer[] _outline, _fill;
        float[] _fuseTotal;
        Vector3[] _lastCenter;
        bool[] _wasActive;
        Material _material;

        /// <summary>Binds the run's hazards and burning patches and creates their rings.</summary>
        public void Bind(IHazardView hazards, IBurningPatchView patches, Func<bool> paused)
        {
            _hazards = hazards;
            _patches = patches;
            _paused = paused;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            if (_hazards != null && (_outline == null || _outline.Length != _hazards.Capacity)) Allocate(_hazards.Capacity);
            if (_patches != null) AllocatePatches(_patches.Capacity);
            while (_timedViews.Count < PrewarmTimed) AddTimedView();
            _timed.Clear();
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
        public void ShowTimedRing(Vector3 center, float radius, float seconds) =>
            _timed.Add(new TimedRing { Center = center, Radius = radius, Total = Mathf.Max(0.01f, seconds), Left = seconds });

        public int TimedRingCount => _timed.Count;

        void AddTimedView() =>
            _timedViews.Add((CreateRing("TimedOutline" + _timedViews.Count), CreateRing("TimedFill" + _timedViews.Count)));

        void TickTimedRings()
        {
            var paused = _paused != null && _paused();
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var ring = _timed[i];
                if (!paused) ring.Left -= Time.deltaTime;
                if (ring.Left <= 0f) { _timed.RemoveAt(i); continue; }
                _timed[i] = ring;
            }
            while (_timedViews.Count < _timed.Count) AddTimedView();
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

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            TickTimedRings();
            DrawPatches();
            var source = _hazards;
            if (source == null || _outline == null) return;
            var capacity = Mathf.Min(source.Capacity, _outline.Length);
            for (var i = 0; i < capacity; i++)
            {
                if (!source.TryGet(i, out var hazard))
                {
                    _wasActive[i] = false;
                    Show(i, false);
                    continue;
                }

                var kind = hazard.kind;
                var center = hazard.center;
                var radius = hazard.radius;
                var timer = hazard.timer;
                if (hazard.duration > 0f) _fuseTotal[i] = hazard.duration; // Playtest (3 Oct): the hazard's own length (moving warnings)
                else if (!_wasActive[i] || center != _lastCenter[i]) _fuseTotal[i] = Mathf.Max(0.01f, timer); // a new hazard in this slot
                _wasActive[i] = true;
                _lastCenter[i] = center;

                if (kind == HazardKind.WarningLine)
                {
                    Show(i, true);
                    SetLane(_outline[i], _fill[i], center, hazard.direction, hazard.length, radius, 1f - Mathf.Clamp01(timer / _fuseTotal[i]));
                    continue;
                }

                var color = kind == HazardKind.Mine ? MineColor : kind == HazardKind.NetSlow ? NetColor : kind == HazardKind.Warning ? WarningColor : TrapColor;
                // Mines: fill while arming, then a full ring while armed. Traps: fill as the fuse runs down. Nets: full.
                var armed = kind == HazardKind.Mine && hazard.armed;
                var progress = kind == HazardKind.NetSlow || armed ? 1f : 1f - Mathf.Clamp01(timer / _fuseTotal[i]);
                Show(i, true);
                SetCircle(_outline[i], center, radius, color, 0.3f);
                SetCircle(_fill[i], center, Mathf.Max(0.05f, radius * progress), new Color(color.r, color.g, color.b, 0.55f + 0.4f * progress), 0.45f);
            }
        }

        void DrawPatches()
        {
            var patches = _patches;
            if (patches == null || _patchOutline == null) return;
            for (var i = 0; i < _patchOutline.Length; i++)
            {
                var live = patches.TryGet(i, out var center, out var radius, out var lifeLeft);
                ShowPair(_patchOutline[i], _patchFill[i], live);
                if (!live) continue;
                SetCircle(_patchOutline[i], center, radius, PatchColor, 0.3f);
                SetCircle(_patchFill[i], center, Mathf.Max(0.05f, radius * lifeLeft), new Color(PatchColor.r, PatchColor.g, PatchColor.b, 0.45f + 0.5f * lifeLeft), 0.45f);
            }
        }

        void AllocatePatches(int count)
        {
            if (_patchOutline != null && _patchOutline.Length == count) return;
            _patchOutline = new LineRenderer[count];
            _patchFill = new LineRenderer[count];
            for (var i = 0; i < count; i++)
            {
                _patchOutline[i] = CreateRing("PatchOutline" + i);
                _patchFill[i] = CreateRing("PatchFill" + i);
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

        /// <summary>Playtest (3 Oct, owner, round 7): a lane: its outline as a rectangle and a translucent strip that runs
        /// from the attacker to the lane's end as the attack comes.</summary>
        static void SetLane(LineRenderer outline, LineRenderer fill, Vector3 start, Vector3 direction, float length, float halfWidth, float progress)
        {
            var side = Vector3.Cross(Vector3.up, direction) * halfWidth;
            var end = start + direction * length;
            start.y = end.y = Height;
            outline.loop = true;
            outline.positionCount = 4;
            outline.startColor = outline.endColor = WarningColor;
            outline.widthMultiplier = 0.3f;
            outline.SetPosition(0, start + side);
            outline.SetPosition(1, end + side);
            outline.SetPosition(2, end - side);
            outline.SetPosition(3, start - side);
            fill.loop = false;
            fill.positionCount = 2;
            var c = new Color(WarningColor.r, WarningColor.g, WarningColor.b, 0.3f + 0.35f * progress);
            fill.startColor = fill.endColor = c;
            fill.widthMultiplier = halfWidth * 2f * 0.85f;
            fill.SetPosition(0, start);
            fill.SetPosition(1, Vector3.Lerp(start, end, Mathf.Max(0.05f, progress)));
        }

        static void SetCircle(LineRenderer line, Vector3 center, float radius, Color color, float width)
        {
            if (line.positionCount != Segments) { line.positionCount = Segments; line.loop = true; } // after a lane
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
