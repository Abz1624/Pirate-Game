using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Hackathon (12 Oct, owner): the minimap back for the demo, the cheap way. The static world (islands, the play area)
    /// is drawn ONCE per stage layout into a texture by a top-down orthographic camera sized from the play-area bounds;
    /// the camera is then disabled and destroyed (no per-frame render cost). The ship, the boss, minibosses and scheduled
    /// elites, points of interest (Beacon, chests) and ordinary enemies (small dots, capped) are UI icons on top, placed by
    /// normalising their world positions to 0..1 within the bounds and refreshed at <see cref="RefreshHz"/> without
    /// per-frame allocations. Top-right, under the timer and inside the safe area (the touch controls own the bottom corners).
    /// </summary>
    public sealed class SnapshotMinimapPresenter : MonoBehaviour
    {
        public const float RefreshHz = 10f;
        public const int MaxEnemyDots = 48;
        const int MaxThreatIcons = 6;
        /// <summary>Layers the snapshot draws (the islands live on "Island"; Default carries props parented to them).</summary>
        public static int SnapshotCullingMask => LayerMask.GetMask("Island");
        const int SnapshotSize = 256;
        static readonly Color SeaColor = new Color(0.11f, 0.42f, 0.66f, 1f);
        static readonly Color FrameGold = new Color(0.91f, 0.71f, 0.30f, 1f);
        static readonly Color FrameDark = new Color(0.07f, 0.12f, 0.2f, 0.92f);
        static readonly Color EnemyDotColor = new Color(0.93f, 0.27f, 0.22f, 0.95f);

        [Range(0.12f, 0.35f)] public float heightFraction = 0.2f;
        /// <summary>Below the timer and the currency bar (a fraction of the canvas height from the top).</summary>
        [Range(0f, 0.4f)] public float topOffsetFraction = 0.19f;
        public float cornerInset = 16f;

        IRunViews _views;
        RectTransform _frame, _map, _iconRoot;
        RawImage _world;
        Texture2D _snapshot;
        int _attempts;
        int _snapshotLayout = int.MinValue;
        int _pendingLayout = int.MinValue;
        int _pendingFrames;
        float _nextRefresh;
        Rect _bounds;
        RectTransform _player;
        readonly List<RectTransform> _poiIcons = new List<RectTransform>();
        readonly List<RectTransform> _threatIcons = new List<RectTransform>();
        readonly List<int> _threatKinds = new List<int>();
        readonly List<ThreatMarker> _threats = new List<ThreatMarker>(MaxThreatIcons);
        readonly RectTransform[] _dots = new RectTransform[MaxEnemyDots];
        readonly Vector3[] _enemyPositions = new Vector3[MaxEnemyDots];

        /// <summary>Snapshots taken (tests).</summary>
        public int SnapshotCount { get; private set; }
        /// <summary>The snapshot camera of the last snapshot (destroyed after it; tests read that it is gone or disabled).</summary>
        public Camera LastSnapshotCamera { get; private set; }
        public Texture WorldTexture => _snapshot;
        public RectTransform MapRect => _map;
        public RectTransform PlayerIcon => _player;

        public static SnapshotMinimapPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<SnapshotMinimapPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("v4_ui_snapshot_minimap", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var p = go.AddComponent<SnapshotMinimapPresenter>();
            p.Build();
            return p;
        }

        public void Bind(IRunViews views)
        {
            _views = views;
            _snapshotLayout = int.MinValue;
            _pendingLayout = int.MinValue;
        }

        /// <summary>The world rectangle the map covers (x, z): the play area.</summary>
        public static Rect BoundsRect() =>
            new Rect(PlayBounds.Center.x - PlayBounds.HalfExtentX, PlayBounds.Center.z - PlayBounds.HalfExtentZ,
                PlayBounds.HalfExtentX * 2f, PlayBounds.HalfExtentZ * 2f);

        /// <summary>A world position normalised within the bounds: (0,0) the bottom-left (min x, min z), (1,1) the top-right; clamped.</summary>
        public static Vector2 Normalize(Rect bounds, Vector3 world) =>
            new Vector2(
                bounds.width > 0.001f ? Mathf.Clamp01((world.x - bounds.xMin) / bounds.width) : 0.5f,
                bounds.height > 0.001f ? Mathf.Clamp01((world.z - bounds.yMin) / bounds.height) : 0.5f);

        void Build()
        {
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
            _frame = root;
            var border = root.gameObject.AddComponent<Image>();
            border.sprite = MinimapUiSprites.White;
            border.color = FrameGold;
            border.raycastTarget = false;
            var inner = Child(root, "Inner");
            Stretch(inner, 3f);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = MinimapUiSprites.White;
            innerImg.color = FrameDark;
            innerImg.raycastTarget = false;
            _map = Child(inner, "Map");
            Stretch(_map, 3f);
            _map.gameObject.AddComponent<RectMask2D>();
            _world = _map.gameObject.AddComponent<RawImage>();
            _world.color = SeaColor;
            _world.raycastTarget = false;
            _iconRoot = Child(_map, "Icons");
            Stretch(_iconRoot, 0f);
            for (var i = 0; i < MaxEnemyDots; i++)
            {
                var dot = Child(_iconRoot, "Enemy");
                dot.sizeDelta = new Vector2(4f, 4f);
                var img = dot.gameObject.AddComponent<Image>();
                img.sprite = MinimapUiSprites.Circle;
                img.color = EnemyDotColor;
                img.raycastTarget = false;
                dot.gameObject.SetActive(false);
                _dots[i] = dot;
            }
            for (var i = 0; i < MaxThreatIcons; i++)
            {
                var icon = Child(_iconRoot, "Threat");
                icon.sizeDelta = new Vector2(16f, 16f);
                icon.gameObject.SetActive(false);
                _threatIcons.Add(icon);
                _threatKinds.Add(-1);
            }
            _player = Child(_iconRoot, "Player");
            _player.sizeDelta = new Vector2(14f, 14f);
            MinimapMarkerGraphics.Apply(_player, MinimapMarkerGraphics.GlyphKind.Player);
            DisableRaycasts(_player);
        }

        void LateUpdate()
        {
            if (_views == null || _views.Stage == null || !_views.Stage.Exists || !PlayBounds.IsConfigured)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);
            Layout();
            var stage = _views.Stage;
            // A new layout (or play area): wait a few frames (the stage builder places its islands and sizes the play
            // area), then draw the static world once. A snapshot that came back empty (the first frames of a scene can
            // skip a render request) is retried a few times, then given up (the sea colour stays).
            if (stage.LayoutVersion != _snapshotLayout || BoundsRect() != _bounds)
            {
                if (_pendingLayout != stage.LayoutVersion) { _pendingLayout = stage.LayoutVersion; _pendingFrames = 15; _attempts = 0; }
                else if (--_pendingFrames <= 0)
                {
                    var ok = TakeSnapshot();
                    if (ok || ++_attempts >= 10)
                    {
                        _snapshotLayout = stage.LayoutVersion;
                        BuildPoiIcons(stage);
                        _nextRefresh = 0f;
                    }
                    else _pendingFrames = 20;
                }
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / RefreshHz;
            RefreshIcons();
        }

        void SetVisible(bool on)
        {
            if (_map != null && _map.parent.gameObject.activeSelf != on) _map.parent.gameObject.SetActive(on);
            var img = GetComponent<Image>();
            if (img != null && img.enabled != on) img.enabled = on;
        }

        void Layout()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            var h = parent.rect.height;
            var w = parent.rect.width;
            var size = Mathf.Clamp(h * heightFraction, 96f, 240f);
            var aspect = PlayBounds.HalfExtentZ > 0.01f ? PlayBounds.HalfExtentX / PlayBounds.HalfExtentZ : 1f;
            aspect = Mathf.Clamp(aspect, 0.5f, 2f);
            // Safe area (notches, rounded corners): the right and top insets in canvas units.
            var safe = Screen.safeArea;
            var sx = Screen.width > 0 ? w / Screen.width : 1f;
            var sy = Screen.height > 0 ? h / Screen.height : 1f;
            var right = (Screen.width - safe.xMax) * sx;
            var top = (Screen.height - safe.yMax) * sy;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(size * aspect + 12f, size + 12f);
            rect.anchoredPosition = new Vector2(-(cornerInset + right), -(h * topOffsetFraction + top));
        }

        /// <summary>Draws the static world into the texture once: a top-down orthographic camera over the bounds, rendered
        /// one time and destroyed.</summary>
        public bool TakeSnapshot()
        {
            _bounds = BoundsRect();
            var width = SnapshotSize;
            var height = Mathf.Max(64, Mathf.RoundToInt(SnapshotSize * _bounds.height / Mathf.Max(1f, _bounds.width)));
            var rt = RenderTexture.GetTemporary(width, height, 16, RenderTextureFormat.ARGB32);
            var host = new GameObject("MinimapSnapshotCamera");
            host.hideFlags = HideFlags.HideAndDontSave;
            var cam = host.AddComponent<Camera>();
            cam.enabled = false; // never renders on its own
            cam.orthographic = true;
            cam.orthographicSize = _bounds.height * 0.5f;
            cam.aspect = _bounds.width / Mathf.Max(0.01f, _bounds.height);
            cam.transform.position = new Vector3(_bounds.center.x, 300f, _bounds.center.y);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // looking down, world +z up on the texture
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SeaColor;
            cam.cullingMask = SnapshotCullingMask;
            cam.targetTexture = rt;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(cam, request))
                    RenderPipeline.SubmitRenderRequest(cam, request);
                else
                    cam.Render();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Minimap snapshot failed: " + e.Message);
            }
            cam.targetTexture = null;
            LastSnapshotCamera = cam;
            if (Application.isPlaying) Destroy(host); else DestroyImmediate(host);
            // Kept as a plain texture (a render texture's contents can be discarded), read back once.
            if (_snapshot == null || _snapshot.width != width || _snapshot.height != height)
            {
                if (_snapshot != null) DestroyTexture(_snapshot);
                _snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false)
                    { name = "MinimapSnapshot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            _snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            _snapshot.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var drawn = _snapshot.GetPixel(0, 0).a > 0.5f; // the sea colour is opaque; an untouched target is clear
            _world.texture = _snapshot;
            _world.color = drawn ? Color.white : SeaColor;
            SnapshotCount++;
            return drawn;
        }

        static void DestroyTexture(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void BuildPoiIcons(IStageView stage)
        {
            foreach (var icon in _poiIcons) if (icon != null) Destroy(icon.gameObject);
            _poiIcons.Clear();
            for (var i = 0; i < stage.PoiCount; i++)
            {
                var poi = stage.Poi(i);
                var icon = Child(_iconRoot, "POI_" + poi.type);
                icon.sizeDelta = new Vector2(12f, 12f);
                MinimapMarkerGraphics.Apply(icon, MinimapMarkerGraphics.FromPoi(poi.type));
                DisableRaycasts(icon);
                icon.SetSiblingIndex(MaxEnemyDots); // over the dots, under the threats and the ship
                _poiIcons.Add(icon);
            }
        }

        void RefreshIcons()
        {
            var size = _map.rect.size;
            var stage = _views.Stage;
            var count = Mathf.Min(_poiIcons.Count, stage.PoiCount);
            for (var i = 0; i < count; i++)
            {
                var poi = stage.Poi(i);
                var visible = !poi.used && (!stage.HasDiscovery || stage.IsPoiVisible(i)
                    || (poi.type == POIType.BossBeacon && stage.Beacon >= BeaconReveal.ExactMarker));
                if (_poiIcons[i].gameObject.activeSelf != visible) _poiIcons[i].gameObject.SetActive(visible);
                if (visible) Place(_poiIcons[i], poi.position, size);
            }

            var enemies = _views.Enemies;
            var n = enemies != null ? enemies.CopyEnemyPositions(_enemyPositions) : 0;
            for (var i = 0; i < MaxEnemyDots; i++)
            {
                var on = i < n;
                if (_dots[i].gameObject.activeSelf != on) _dots[i].gameObject.SetActive(on);
                if (on) Place(_dots[i], _enemyPositions[i], size);
            }

            if (enemies != null) enemies.CollectThreats(_threats); else _threats.Clear();
            for (var i = 0; i < _threatIcons.Count; i++)
            {
                var on = i < _threats.Count;
                var icon = _threatIcons[i];
                if (icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
                if (!on) continue;
                var t = _threats[i];
                var kind = t.isBoss ? (int)MinimapMarkerGraphics.GlyphKind.Boss : (int)MinimapMarkerGraphics.GlyphKind.Miniboss;
                if (_threatKinds[i] != kind)
                {
                    for (var c = icon.childCount - 1; c >= 0; c--) Destroy(icon.GetChild(c).gameObject);
                    icon.sizeDelta = t.isBoss ? new Vector2(20f, 20f) : new Vector2(15f, 15f);
                    MinimapMarkerGraphics.Apply(icon, (MinimapMarkerGraphics.GlyphKind)kind);
                    DisableRaycasts(icon);
                    _threatKinds[i] = kind;
                }
                Place(icon, t.position, size);
            }

            var ship = _views.Ship;
            if (ship != null && ship.Exists)
            {
                Place(_player, ship.Position, size);
                var f = ship.MovementForward;
                if (f.sqrMagnitude > 0.01f) _player.localRotation = Quaternion.Euler(0f, 0f, 180f - Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg); // the glyph points down
                _player.SetAsLastSibling();
            }
        }

        void Place(RectTransform icon, Vector3 world, Vector2 mapSize)
        {
            var uv = Normalize(_bounds, world);
            icon.anchorMin = icon.anchorMax = Vector2.zero;
            icon.anchoredPosition = new Vector2(uv.x * mapSize.x, uv.y * mapSize.y);
        }

        void OnDestroy()
        {
            if (_snapshot != null) DestroyTexture(_snapshot);
            _snapshot = null;
        }

        static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        static void DisableRaycasts(Transform root)
        {
            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }
    }
}
