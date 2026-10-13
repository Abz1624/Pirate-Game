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
        const int PrewarmTimedCount = 8;
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
            PrewarmTimed(null, PrewarmTimedCount);
            ClearTimed();
        }

        static readonly Color TimedColor = new Color(1f, 0.45f, 0.1f, 0.95f);

        struct TimedRing
        {
            public Vector3 Center;
            public float Radius, Total, Left;
            public int View;
        }

        /// <summary>A timed ring's outline and fill lines, drawn from a weapon's landing ring prefab (null: code-built).</summary>
        sealed class TimedView
        {
            public LineRenderer Outline, Fill;
            public GameObject Prefab;
            public bool Busy;
        }

        readonly List<TimedRing> _timed = new List<TimedRing>();
        readonly List<TimedView> _timedViews = new List<TimedView>();

        /// <summary>
        /// A landing ring whose inner ring fills over exactly <paramref name="seconds"/> of run time (V4 mortar shells, R-27
        /// fix: V3's reticle showed 1.1 s for a 0.6 s shell). Hackathon (13 Oct): <paramref name="prefab"/> is the
        /// weapon's landing ring prefab (a LineRenderer, from the weapon prefab catalog; null: the code-built ring); its
        /// <see cref="RoguePirates.V4.Presentation.Weapons.WeaponVisualTuning"/> sets the width and colour.
        /// </summary>
        public void ShowTimedRing(Vector3 center, float radius, float seconds, GameObject prefab = null) =>
            _timed.Add(new TimedRing { Center = center, Radius = radius, Total = Mathf.Max(0.01f, seconds), Left = seconds, View = AcquireTimedView(prefab) });

        public int TimedRingCount => _timed.Count;

        /// <summary>Makes at least <paramref name="count"/> ring views of <paramref name="prefab"/> before the run (null: code-built).</summary>
        public void PrewarmTimed(GameObject prefab, int count)
        {
            var have = 0;
            foreach (var v in _timedViews) if (v.Prefab == prefab) have++;
            for (; have < count; have++) AddTimedView(prefab);
        }

        int AddTimedView(GameObject prefab)
        {
            var n = _timedViews.Count;
            var prefix = prefab != null ? prefab.name + "_" : "";
            _timedViews.Add(new TimedView { Outline = CreateRing(prefix + "TimedOutline" + n, prefab), Fill = CreateRing(prefix + "TimedFill" + n, prefab), Prefab = prefab });
            return n;
        }

        int AcquireTimedView(GameObject prefab)
        {
            for (var i = 0; i < _timedViews.Count; i++)
                if (!_timedViews[i].Busy && _timedViews[i].Prefab == prefab) { _timedViews[i].Busy = true; return i; }
            var added = AddTimedView(prefab); // past the prewarm (counted nowhere: as before, it only grows)
            _timedViews[added].Busy = true;
            return added;
        }

        void ClearTimed()
        {
            _timed.Clear();
            foreach (var v in _timedViews)
            {
                v.Busy = false;
                ShowPair(v.Outline, v.Fill, false);
            }
        }

        void TickTimedRings()
        {
            var paused = _paused != null && _paused();
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var ring = _timed[i];
                if (!paused) ring.Left -= Time.deltaTime;
                if (ring.Left <= 0f)
                {
                    var done = _timedViews[ring.View];
                    done.Busy = false;
                    ShowPair(done.Outline, done.Fill, false);
                    _timed.RemoveAt(i);
                    continue;
                }
                _timed[i] = ring;
            }
            for (var i = 0; i < _timed.Count; i++)
            {
                var ring = _timed[i];
                var view = _timedViews[ring.View];
                ShowPair(view.Outline, view.Fill, true);
                var progress = 1f - Mathf.Clamp01(ring.Left / ring.Total);
                var width = Weapons.WeaponVisualTuning.Width(view.Prefab);
                var color = Weapons.WeaponVisualTuning.Colour(view.Prefab, TimedColor);
                SetCircle(view.Outline, ring.Center, ring.Radius, color, 0.3f * width);
                SetCircle(view.Fill, ring.Center, Mathf.Max(0.05f, ring.Radius * progress), new Color(color.r, color.g, color.b, color.a / TimedColor.a * (0.55f + 0.4f * progress)), 0.45f * width);
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
            var width = Weapons.WeaponVisualTuning.Width(_patchPrefab);
            var color = Weapons.WeaponVisualTuning.Colour(_patchPrefab, PatchColor);
            for (var i = 0; i < _patchOutline.Length; i++)
            {
                var live = patches.TryGet(i, out var center, out var radius, out var lifeLeft);
                ShowPair(_patchOutline[i], _patchFill[i], live);
                if (!live) continue;
                SetCircle(_patchOutline[i], center, radius, color, 0.3f * width);
                SetCircle(_patchFill[i], center, Mathf.Max(0.05f, radius * lifeLeft), new Color(color.r, color.g, color.b, color.a / PatchColor.a * (0.45f + 0.5f * lifeLeft)), 0.45f * width);
            }
        }

        GameObject _patchPrefab;

        /// <summary>Hackathon (13 Oct): the burning patches' rings from the flame weapon's patch ring prefab (a
        /// LineRenderer, from the weapon prefab catalog), remade before the run when it changes.</summary>
        public void UsePatchRing(GameObject prefab)
        {
            if (prefab == _patchPrefab) return;
            _patchPrefab = prefab;
            if (_patchOutline == null) return;
            var count = _patchOutline.Length;
            for (var i = 0; i < count; i++)
            {
                if (_patchOutline[i] != null) Kill(_patchOutline[i].gameObject);
                if (_patchFill[i] != null) Kill(_patchFill[i].gameObject);
            }
            _patchOutline = null;
            AllocatePatches(count);
        }

        void AllocatePatches(int count)
        {
            if (_patchOutline != null && _patchOutline.Length == count) return;
            _patchOutline = new LineRenderer[count];
            _patchFill = new LineRenderer[count];
            for (var i = 0; i < count; i++)
            {
                _patchOutline[i] = CreateRing("PatchOutline" + i, _patchPrefab);
                _patchFill[i] = CreateRing("PatchFill" + i, _patchPrefab);
            }
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
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

        /// <summary>A ring line: an instance of <paramref name="prefab"/> (a LineRenderer at its root; Hackathon), else code-built.</summary>
        LineRenderer CreateRing(string name, GameObject prefab = null)
        {
            if (prefab != null && prefab.GetComponent<LineRenderer>() != null)
            {
                var go = Instantiate(prefab, transform, false);
                go.name = name;
                var own = go.GetComponent<LineRenderer>();
                own.useWorldSpace = true;
                own.loop = true;
                own.positionCount = Segments;
                go.SetActive(false);
                return own;
            }
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            ConfigureRingLine(line);
            line.material = _material;
            child.SetActive(false);
            return line;
        }

        /// <summary>The code-built ring line's settings (the weapon ring prefabs are built with these, HackathonWeaponPrefabs).</summary>
        public static void ConfigureRingLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
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
