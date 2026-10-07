using System.Collections.Generic;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Off-screen threat arrows. Playtest (3 Oct, owner): the boss was hard to find once summoned. V5 M8z (6 Oct, owner:
    /// "show an onscreen arrow/pointer towards the miniboss/scheduled elites when they are out of player's view"): while
    /// the boss, a miniboss or a focus unit (a scheduled elite) is alive and off screen, an arrow sits at the safe area's
    /// edge on the line from the screen centre to it, with its name and distance. Red-orange for the boss and minibosses,
    /// gold for elites. Hidden while on screen, dead, and after the run ends. Arrows that would overlap are spread along
    /// the edge. Reads the threat view only (<see cref="IEnemyView.CollectThreats"/>); the PvP match has its own arrows.
    /// </summary>
    public sealed class ThreatArrowPresenter : MonoBehaviour
    {
        const float EdgeMargin = 70f;      // canvas units from the safe area's edge to an arrow's centre
        const float TopMargin = 190f;      // clear of the top HUD strip (portrait, bars, title, counters, timer)
        const float OnScreenInset = 0.04f; // a threat counts as on screen when inside this much of the viewport's edge
        const float GapSide = 90f;         // spacing between arrows along a left or right edge (labels are short)
        const float GapTopBottom = 190f;   // along the top or bottom edge (labels are wide)
        const int InitialArrows = 6; // V5 M8z (7 Oct, owner): no cap; every off-screen threat gets an arrow

        public static readonly Color BossColor = new Color(0.98f, 0.33f, 0.12f, 0.95f);
        public static readonly Color EliteColor = new Color(1f, 0.78f, 0.15f, 0.95f);

        sealed class Arrow
        {
            public RectTransform root, triangle, label;
            public Image image;
            public TextMeshProUGUI text;
            public string shown;
        }

        IRunViews _views;
        RectTransform _root;
        readonly List<Arrow> _arrows = new List<Arrow>(InitialArrows);
        readonly List<ThreatMarker> _threats = new List<ThreatMarker>(8);
        readonly List<ThreatMarker> _off = new List<ThreatMarker>(InitialArrows);
        readonly List<Vector2> _points = new List<Vector2>(InitialArrows);
        readonly List<Vector2> _dirs = new List<Vector2>(InitialArrows);
        int _visible;
        float _pulse;

        /// <summary>Arrows showing now (tests).</summary>
        public int VisibleCount => _visible;
        public bool ArrowVisible => _visible > 0;
        /// <summary>The label of a showing arrow (tests).</summary>
        public string LabelAt(int index) => index < _visible ? _arrows[index].text.text : null;

        public static ThreatArrowPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<ThreatArrowPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4ThreatArrows", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var p = go.AddComponent<ThreatArrowPresenter>();
            p._root = (RectTransform)go.transform;
            p._root.anchorMin = Vector2.zero;
            p._root.anchorMax = Vector2.one;
            p._root.offsetMin = p._root.offsetMax = Vector2.zero;
            return p;
        }

        public void Bind(IRunViews views) => _views = views;

        void LateUpdate()
        {
            _off.Clear();
            _points.Clear();
            _dirs.Clear();
            var cam = Camera.main;
            var run = _views?.Run;
            if (cam != null && _views?.Enemies != null && (run == null || !run.IsRunEnded))
            {
                _views.Enemies.CollectThreats(_threats);
                // The run canvas's content sits in its SafeArea wrapper (V5 M7), so the root already is the safe area.
                var box = EdgeBox(_root.rect, PhoneMarginFraction(), EdgeMargin, TopMargin);
                var screen = new Vector2(Screen.width, Screen.height);
                foreach (var t in _threats)
                {
                    var vp = cam.WorldToViewportPoint(t.position);
                    if (IsOnScreen(vp, OnScreenInset)) continue;
                    _points.Add(EdgePoint(vp, screen, box, out var dir));
                    _dirs.Add(dir);
                    _off.Add(t);
                }
                Separate(_points, box, GapSide, GapTopBottom);
            }

            _pulse += Time.unscaledDeltaTime * 6f;
            var s = 1f + Mathf.Sin(_pulse) * 0.08f;
            var ship = _views?.Ship;
            for (var i = 0; i < _off.Count; i++)
            {
                if (i >= _arrows.Count) _arrows.Add(NewArrow(_root));
                var a = _arrows[i];
                if (!a.root.gameObject.activeSelf) a.root.gameObject.SetActive(true);
                var t = _off[i];
                var dir = _dirs[i];
                a.root.anchoredPosition = _points[i];
                a.triangle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
                a.triangle.localScale = new Vector3(s, s, 1f);
                var color = t.isBoss || t.isMiniboss ? BossColor : EliteColor;
                if (a.image.color != color) a.image.color = color;
                // Just inside the arrow, towards the centre, clear of it whichever side it is on (the label is wide, not tall).
                a.label.anchoredPosition = -dir * (36f + Mathf.Abs(dir.x) * 80f + Mathf.Abs(dir.y) * 22f);
                var label = Label(t, ship != null && ship.Exists ? Flat(t.position - ship.Position) : -1f);
                if (a.shown != label) { a.shown = label; a.text.text = label; }
            }
            for (var i = _off.Count; i < _arrows.Count; i++)
                if (_arrows[i].root.gameObject.activeSelf) _arrows[i].root.gameObject.SetActive(false);
            _visible = _off.Count;
        }

        static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        /// <summary>"BOSS", or the unit's name, upper case, over its distance (rounded to 10 m so the text rarely rebuilds).</summary>
        public static string Label(ThreatMarker t, float distance)
        {
            var name = t.isBoss ? "BOSS"
                : !string.IsNullOrEmpty(t.name) ? t.name.ToUpperInvariant()
                : t.isMiniboss ? "MINIBOSS" : "ELITE";
            if (distance < 0f) return name;
            return name + "\n<size=75%>" + (Mathf.RoundToInt(distance / 10f) * 10) + " m</size>";
        }

        /// <summary>The HUD's rounded-corner margin on phones (as the Salt Wind HUD's), as a fraction of the root.</summary>
        static Rect PhoneMarginFraction()
        {
            if (!Application.isMobilePlatform) return new Rect(0f, 0f, 1f, 1f);
            var m = RoguePirates.V4.Foundation.Core.DemoMode.MobileHudEdgeMargin;
            var x = m * Screen.height / Mathf.Max(1f, Screen.width);
            return Rect.MinMaxRect(x, m * 0.5f, 1f - x, 1f - m * 0.5f);
        }

        // ---- Pure layout (EditMode tested) ----

        /// <summary>Whether a viewport point is in front of the camera and inside the viewport less <paramref name="inset"/>.</summary>
        public static bool IsOnScreen(Vector3 viewport, float inset) =>
            viewport.z >= 0f && viewport.x > inset && viewport.x < 1f - inset && viewport.y > inset && viewport.y < 1f - inset;

        /// <summary>The box the arrows' centres stay in, in canvas units from the canvas centre: the safe part (a fraction of
        /// the canvas) less the margins. It always holds the centre.</summary>
        public static Rect EdgeBox(Rect canvas, Rect safeFraction, float margin, float topMargin)
        {
            var w = canvas.width;
            var h = canvas.height;
            var xMin = Mathf.Min(-1f, safeFraction.xMin * w - w * 0.5f + margin);
            var xMax = Mathf.Max(1f, safeFraction.xMax * w - w * 0.5f - margin);
            var yMin = Mathf.Min(-1f, safeFraction.yMin * h - h * 0.5f + margin);
            var yMax = Mathf.Max(1f, safeFraction.yMax * h - h * 0.5f - topMargin);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Where the ray from the centre towards a viewport point leaves <paramref name="box"/>, and the ray's
        /// direction (a point behind the camera projects mirrored, so it is flipped). The screen's aspect sets the angle.</summary>
        public static Vector2 EdgePoint(Vector3 viewport, Vector2 screenSize, Rect box, out Vector2 dir)
        {
            dir = new Vector2((viewport.x - 0.5f) * screenSize.x, (viewport.y - 0.5f) * screenSize.y);
            if (viewport.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            var tx = dir.x > 0.0001f ? box.xMax / dir.x : dir.x < -0.0001f ? box.xMin / dir.x : float.MaxValue;
            var ty = dir.y > 0.0001f ? box.yMax / dir.y : dir.y < -0.0001f ? box.yMin / dir.y : float.MaxValue;
            return dir * Mathf.Min(tx, ty);
        }

        /// <summary>Spreads points on the box's edge so neighbours are at least a gap apart along it (the gap of the edge a
        /// point is on: <paramref name="gapSide"/> on the left and right, <paramref name="gapTopBottom"/> on the top and
        /// bottom). Walks the perimeter in order and pushes each crowded point onward, round corners if it must.</summary>
        public static void Separate(List<Vector2> points, Rect box, float gapSide, float gapTopBottom)
        {
            var n = points.Count;
            if (n < 2) return;
            var perimeter = 2f * (box.width + box.height);
            var order = new List<(float s, int i)>(n);
            for (var i = 0; i < n; i++) order.Add((ToPerimeter(points[i], box), i));
            order.Sort((a, b) => a.s.CompareTo(b.s));
            var moved = false;
            for (var k = 1; k < n; k++)
            {
                var prev = order[k - 1].s;
                var gap = Mathf.Max(GapAt(prev, box, gapSide, gapTopBottom), GapAt(order[k].s, box, gapSide, gapTopBottom));
                if (order[k].s - prev >= gap) continue;
                order[k] = (prev + gap, order[k].i);
                moved = true;
            }
            if (!moved) return;
            foreach (var (s, i) in order) points[i] = FromPerimeter(Mathf.Repeat(s, perimeter), box);
        }

        static float GapAt(float s, Rect box, float gapSide, float gapTopBottom)
        {
            var w = box.width;
            var h = box.height;
            s = Mathf.Repeat(s, 2f * (w + h));
            return s < w || (s >= w + h && s < 2f * w + h) ? gapTopBottom : gapSide;
        }

        /// <summary>Distance along the box's perimeter, anticlockwise from the bottom-left corner, of the nearest edge point.</summary>
        public static float ToPerimeter(Vector2 p, Rect box)
        {
            var w = box.width;
            var h = box.height;
            var dBottom = Mathf.Abs(p.y - box.yMin);
            var dRight = Mathf.Abs(p.x - box.xMax);
            var dTop = Mathf.Abs(p.y - box.yMax);
            var dLeft = Mathf.Abs(p.x - box.xMin);
            var min = Mathf.Min(Mathf.Min(dBottom, dRight), Mathf.Min(dTop, dLeft));
            if (min == dBottom) return Mathf.Clamp(p.x - box.xMin, 0f, w);
            if (min == dRight) return w + Mathf.Clamp(p.y - box.yMin, 0f, h);
            if (min == dTop) return w + h + Mathf.Clamp(box.xMax - p.x, 0f, w);
            return 2f * w + h + Mathf.Clamp(box.yMax - p.y, 0f, h);
        }

        public static Vector2 FromPerimeter(float s, Rect box)
        {
            var w = box.width;
            var h = box.height;
            if (s < w) return new Vector2(box.xMin + s, box.yMin);
            s -= w;
            if (s < h) return new Vector2(box.xMax, box.yMin + s);
            s -= h;
            if (s < w) return new Vector2(box.xMax - s, box.yMax);
            s -= w;
            return new Vector2(box.xMin, box.yMax - Mathf.Min(s, h));
        }

        static Arrow NewArrow(RectTransform parent)
        {
            var a = new Arrow();
            a.root = (RectTransform)new GameObject("Arrow", typeof(RectTransform)).transform;
            a.root.SetParent(parent, false);
            a.root.anchorMin = a.root.anchorMax = new Vector2(0.5f, 0.5f);
            a.root.sizeDelta = Vector2.zero;

            a.triangle = (RectTransform)new GameObject("Triangle", typeof(RectTransform)).transform;
            a.triangle.SetParent(a.root, false);
            a.triangle.sizeDelta = new Vector2(52f, 52f);
            a.image = a.triangle.gameObject.AddComponent<Image>();
            a.image.sprite = MinimapUiSprites.Triangle; // points down; rotated towards the threat
            a.image.raycastTarget = false;
            var outline = a.triangle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            a.label = (RectTransform)new GameObject("Label", typeof(RectTransform)).transform;
            a.label.SetParent(a.root, false);
            a.label.anchorMin = a.label.anchorMax = new Vector2(0.5f, 0.5f);
            a.label.sizeDelta = new Vector2(180f, 50f);
            a.text = a.label.gameObject.AddComponent<TextMeshProUGUI>();
            a.text.fontSize = 20f;
            a.text.fontStyle = FontStyles.Bold;
            a.text.alignment = TextAlignmentOptions.Center;
            a.text.enableWordWrapping = false;
            a.text.color = Color.white;
            a.text.outlineWidth = 0.25f;
            a.text.outlineColor = new Color32(0, 0, 0, 255);
            a.text.raycastTarget = false;
            return a;
        }
    }
}
