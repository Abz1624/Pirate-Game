using System;
using RoguePirates.V4.Foundation.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.UI
{
    /// <summary>
    /// V5 M7: keeps a full-screen RectTransform inside the screen's safe area (notches, rounded corners, the home bar).
    /// <see cref="Wrap"/> puts a canvas's content under one such rect, so every panel built on it stays inside.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>The safe area and screen size (tests replace them).</summary>
        public static Func<Rect> SafeArea = () => Screen.safeArea;
        public static Func<Vector2> ScreenSize = () => new Vector2(Screen.width, Screen.height);

        Rect _applied;
        Vector2 _screen;
        RectTransform _rt;

        /// <summary>The anchors that put a stretched rect over the safe area of a screen.</summary>
        public static (Vector2 min, Vector2 max) Anchors(Rect safe, Vector2 screen)
        {
            if (screen.x <= 0f || screen.y <= 0f) return (Vector2.zero, Vector2.one);
            var min = new Vector2(Mathf.Clamp01(safe.xMin / screen.x), Mathf.Clamp01(safe.yMin / screen.y));
            var max = new Vector2(Mathf.Clamp01(safe.xMax / screen.x), Mathf.Clamp01(safe.yMax / screen.y));
            if (max.x <= min.x || max.y <= min.y) return (Vector2.zero, Vector2.one);
            return (min, max);
        }

        void Awake() => _rt = transform as RectTransform;
        void OnEnable() => Apply(true);
        void Update() => Apply(false);

        public void Apply(bool force)
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_rt == null) return;
            var safe = SafeArea();
            var screen = ScreenSize();
            if (!force && safe == _applied && screen == _screen) return;
            _applied = safe;
            _screen = screen;
            var (min, max) = Anchors(safe, screen);
            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }

        /// <summary>Moves every child of the canvas under a "SafeArea" rect that follows the safe area (idempotent).</summary>
        public static RectTransform Wrap(Canvas canvas)
        {
            if (canvas == null) return null;
            var existing = canvas.transform.Find("SafeArea") as RectTransform;
            if (existing != null && existing.GetComponent<SafeAreaFitter>() != null) return existing;
            var go = new GameObject("SafeArea", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            for (var i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = canvas.transform.GetChild(i);
                if (child == rt) continue;
                child.SetParent(rt, false);
                child.SetSiblingIndex(0);
            }
            go.AddComponent<SafeAreaFitter>().Apply(true);
            return rt;
        }
    }

    /// <summary>
    /// V5 M7: the player's UI scale on every screen-size scaler (a larger scale = a smaller reference resolution), kept in
    /// step with the settings.
    /// </summary>
    public static class UiScale
    {
        sealed class Base : MonoBehaviour { public Vector2 reference; }

        static bool _hooked;

        /// <summary>Applies the current UI scale to every canvas scaler in the open scenes.</summary>
        public static void ApplyAll()
        {
            if (!_hooked) { _hooked = true; GameSettings.Changed += ApplyAll; }
            var scale = GameSettings.Current.uiScale;
            foreach (var scaler in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Apply(scaler, scale);
        }

        public static void Apply(CanvasScaler scaler, float scale)
        {
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
            var b = scaler.GetComponent<Base>();
            if (b == null)
            {
                b = scaler.gameObject.AddComponent<Base>();
                b.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                b.reference = scaler.referenceResolution;
            }
            scaler.referenceResolution = b.reference / Mathf.Max(0.1f, scale);
        }

        /// <summary>Screen pixels per canvas unit for a 1920x1080 reference matched half to width and height.</summary>
        public static float PixelsPerUnit(Vector2 screen, float uiScale, Vector2? reference = null, float match = 0.5f)
        {
            var r = (reference ?? UiCanvasConfigurator.ReferenceResolution) / Mathf.Max(0.1f, uiScale);
            var logW = Mathf.Log(screen.x / r.x, 2f);
            var logH = Mathf.Log(screen.y / r.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
        }
    }
}
