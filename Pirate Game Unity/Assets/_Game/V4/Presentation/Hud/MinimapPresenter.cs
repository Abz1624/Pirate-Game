using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.V3;
using HighSeas.World;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The top-right minimap (M6): port of V3MinimapHud (circular clip, fog of war, playable-bounds veil, POI glyphs,
    /// beacon sector hint, boss/miniboss glyphs, spawn pings, Megabonk-style clamped view), same settings and art
    /// assets. Reads the ship, stage and enemy views; spawn pings come from the spawn warning presenter.
    /// Fix (R-86): V3 rebuilt the boss/miniboss and spawn-ping glyphs every frame (hundreds of UI objects a second);
    /// each glyph is now built once per icon and only rebuilt when its kind changes.
    /// </summary>
    public sealed class MinimapPresenter : MonoBehaviour
    {
        [Header("Screen placement")]
        public float cornerInset = 24f;
        [Range(0.08f, 0.35f)] public float heightFraction = 0.18f;
        public float minMapSize = 140f;
        public float maxMapSize = 220f;

        [Header("Frame mask")]
        [Range(0.05f, 0.25f)] public float ringInsetFraction = 0.135f;

        [Header("Zoom (Megabonk ortho ratio)")]
        public float megabonkNormalZoom = 100f;
        public float megabonkFullMapZoom = 300f;

        [Header("Icons")]
        public float headingOffsetDegrees = 0f;

        [Header("Fog of war")]
        [Range(64, 256)] public int fogMaskResolution = 160;
        public Color fogTint = new Color(0.1f, 0.16f, 0.26f, 0.88f);
        [Range(2f, 24f)] public float fogEdgeSoftnessWorld = 10f;

        [Header("Playable bounds")]
        public Color outOfBoundsVeilColor = new Color(0.02f, 0.05f, 0.1f, 0.9f);
        [Range(0.01f, 0.12f)] public float outOfBoundsEdgeSoftnessUv = 0.045f;

        const float MapContentPadding = 2.2f;
        const string BeaconMarkerTag = "BossBeacon";

        float VisibleWorldAxisFraction => megabonkFullMapZoom > 0.01f ? megabonkNormalZoom / megabonkFullMapZoom : 0.33f;

        sealed class Glyph
        {
            public RectTransform rect;
            public int kind = -1;
        }

        struct PoiMarkerView
        {
            public RectTransform rect;
            public CanvasGroup group;
            public bool isBeacon;
        }

        RectTransform _mapClip;
        RectTransform _contentRoot;
        RectTransform _mapRoot;
        RectTransform _playerIcon;
        RectTransform _fogRoot;
        RectTransform _threatRoot;
        RectTransform _beaconSector;
        RawImage _fogRawImage;
        RawImage _boundsVeilImage;
        Texture2D _fogTexture;
        Texture2D _boundsVeilTexture;
        Color32[] _fogPixels;
        Color32[] _boundsVeilPixels;
        Color _lastOutOfBoundsVeilColor;
        float _lastOutOfBoundsEdgeSoftnessUv;
        int _lastFogRevision = -1;
        int _lastFogMaskResolution;
        float _lastFogEdgeSoftness;
        Color _lastFogTint;
        int _builtLayoutVersion = -1;
        readonly List<PoiMarkerView> _poiMarkers = new List<PoiMarkerView>();
        readonly List<Glyph> _threatIcons = new List<Glyph>(8);
        readonly List<Glyph> _spawnPingIcons = new List<Glyph>(4);
        readonly List<(Vector3 world, float expires)> _spawnPings = new List<(Vector3, float)>(6);
        readonly List<ThreatMarker> _threats = new List<ThreatMarker>(4);
        IRunViews _views;
        float _beaconPulse;
        float _mapScale;

        public int PoiMarkerCount => _poiMarkers.Count;
        public int ActiveSpawnPings => _spawnPings.Count;

        /// <summary>Creates the minimap under the run canvas (or reuses one), with the project minimap settings.</summary>
        public static MinimapPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<MinimapPresenter>(true);
            if (existing != null) return existing;
            var root = new GameObject("v4_ui_minimap", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var hud = root.AddComponent<MinimapPresenter>();
            hud.ApplyProjectSettings();
            var rect = (RectTransform)root.transform;
            hud.ApplyTopRightLayout(rect);
            hud.BuildChrome(rect);
            return hud;
        }

        public void Bind(IRunViews views)
        {
            _views = views;
            _builtLayoutVersion = -1;
            _lastFogRevision = -1;
        }

        public void RegisterSpawnPing(Vector3 worldPosition, float durationSeconds = 3f) =>
            _spawnPings.Add((worldPosition, Time.unscaledTime + durationSeconds));

        void ApplyProjectSettings()
        {
            var s = V3MinimapHudSettings.Load();
            if (s == null) return;
            cornerInset = s.cornerInset;
            heightFraction = s.heightFraction;
            minMapSize = s.minMapSize;
            maxMapSize = s.maxMapSize;
            ringInsetFraction = s.ringInsetFraction;
            megabonkNormalZoom = s.megabonkNormalZoom;
            megabonkFullMapZoom = s.megabonkFullMapZoom;
            headingOffsetDegrees = s.headingOffsetDegrees;
            fogMaskResolution = s.fogMaskResolution;
            fogTint = s.fogTint;
            fogEdgeSoftnessWorld = s.fogEdgeSoftnessWorld;
        }

        void OnDestroy()
        {
            DestroyTexture(ref _fogTexture);
            _fogPixels = null;
            DestroyTexture(ref _boundsVeilTexture);
            _boundsVeilPixels = null;
        }

        void LateUpdate()
        {
            if (_views == null) return;
            RefreshMapSize();
            var ship = _views.Ship;
            if (ship == null || !ship.Exists || _playerIcon == null || _contentRoot == null) return;

            _mapScale = ComputeMapScale();
            var stage = _views.Stage;
            if (stage != null && stage.Exists && stage.LayoutVersion != _builtLayoutVersion) BuildMarkers(stage);

            var pos = ship.Position;
            var viewCenter = ComputeClampedMapViewCenter(pos);
            _contentRoot.anchoredPosition = -WorldToMap(viewCenter);
            _contentRoot.sizeDelta = new Vector2(PlayBounds.HalfExtentX * _mapScale * 2.2f, PlayBounds.HalfExtentZ * _mapScale * 2.2f);
            _playerIcon.anchoredPosition = WorldToMap(pos) - WorldToMap(viewCenter);

            var forward = PlayerHeading(ship);
            var headingDeg = Mathf.Atan2(forward.x, -forward.z) * Mathf.Rad2Deg + headingOffsetDegrees;
            _playerIcon.localRotation = Quaternion.Euler(0f, 0f, headingDeg);

            RefreshFog(stage);
            RefreshBoundsVeil();
            RefreshBeaconSector(stage, pos);
            RefreshMarkers(stage, ship);
            RefreshThreatMarkers(stage);
            RefreshSpawnPings();
            PulseBossBeacon(stage);
        }

        void BuildChrome(RectTransform root)
        {
            var art = V3MinimapArtSettings.Load();

            _mapClip = CreateChild(root, "MapClip");
            Stretch(_mapClip);
            var maskImage = _mapClip.gameObject.AddComponent<Image>();
            maskImage.sprite = V3MinimapUiSprites.Circle;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            _mapClip.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _contentRoot = CreateChild(_mapClip, "ContentRoot");
            _contentRoot.sizeDelta = new Vector2(800f, 800f);

            var ocean = CreateChild(_contentRoot, "OceanBackground");
            Stretch(ocean);
            var oceanImg = ocean.gameObject.AddComponent<Image>();
            oceanImg.raycastTarget = false;
            if (art != null && art.background != null)
            {
                oceanImg.sprite = art.background;
                oceanImg.type = Image.Type.Simple;
                oceanImg.preserveAspect = false;
            }
            else oceanImg.color = new Color(0.05f, 0.22f, 0.42f, 1f);

            var boundsVeilRoot = CreateChild(_contentRoot, "BoundsVeilRoot");
            Stretch(boundsVeilRoot);
            _boundsVeilImage = CreateRawImage(boundsVeilRoot, "BoundsVeil");

            _fogRoot = CreateChild(_contentRoot, "FogRoot");
            Stretch(_fogRoot);
            _fogRawImage = CreateRawImage(_fogRoot, "FogMask");

            _mapRoot = CreateChild(_contentRoot, "MapRoot");
            _mapRoot.sizeDelta = Vector2.zero;
            _threatRoot = CreateChild(_contentRoot, "ThreatRoot");
            _threatRoot.sizeDelta = Vector2.zero;
            _threatRoot.SetAsLastSibling();

            _beaconSector = CreateChild(_mapClip, "BeaconSector");
            _beaconSector.sizeDelta = new Vector2(52f, 52f);
            var sectorImg = _beaconSector.gameObject.AddComponent<Image>();
            sectorImg.sprite = V3MinimapUiSprites.Triangle;
            sectorImg.color = new Color(0.85f, 0.25f, 0.95f, 0.22f);
            sectorImg.raycastTarget = false;
            _beaconSector.gameObject.SetActive(false);

            _playerIcon = CreateChild(_mapClip, "PlayerIcon");
            _playerIcon.sizeDelta = new Vector2(16f, 16f);
            V3MinimapMarkerGraphics.Apply(_playerIcon, V3MinimapMarkerGraphics.GlyphKind.Player);

            var overlay = CreateChild(root, "FrameOverlay");
            Stretch(overlay);
            var overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.raycastTarget = false;
            if (art != null && art.overlay != null)
            {
                overlayImage.sprite = art.overlay;
                overlayImage.type = Image.Type.Simple;
                overlayImage.preserveAspect = false;
            }
            else overlayImage.color = new Color(0.85f, 0.7f, 0.25f, 0.85f);

            // Frame art must sit behind the clipped map; drawing it on top hides the whole minimap.
            overlay.SetAsFirstSibling();
            _mapClip.SetAsLastSibling();
            RefreshClipInsets(root);
        }

        public void ApplyTopRightLayout(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-cornerInset, -cornerInset);
            rect.sizeDelta = new Vector2(maxMapSize, maxMapSize);
        }

        void RefreshMapSize()
        {
            var rect = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (rect == null || canvas == null) return;
            var logicalHeight = canvas.pixelRect.height / Mathf.Max(canvas.scaleFactor, 0.01f);
            var size = Mathf.Clamp(logicalHeight * heightFraction, minMapSize, maxMapSize);
            rect.sizeDelta = new Vector2(size, size);
            RefreshClipInsets(rect);
        }

        void RefreshClipInsets(RectTransform root)
        {
            if (_mapClip == null || root == null) return;
            var pad = Mathf.Max(8f, root.rect.width * ringInsetFraction);
            _mapClip.offsetMin = new Vector2(pad, pad);
            _mapClip.offsetMax = new Vector2(-pad, -pad);
        }

        float InnerHalf()
        {
            var innerHalf = Mathf.Min(_mapClip.rect.width, _mapClip.rect.height) * 0.5f;
            return innerHalf <= 1f ? 60f : innerHalf;
        }

        float ComputeMapScale()
        {
            if (_mapClip == null) return 1f;
            var worldHalf = Mathf.Max(32f, Mathf.Max(PlayBounds.HalfExtentX, PlayBounds.HalfExtentZ) * VisibleWorldAxisFraction);
            return InnerHalf() / worldHalf;
        }

        /// <summary>Keeps the player centred until the visible window reaches a playable edge, then slides the map.</summary>
        Vector3 ComputeClampedMapViewCenter(Vector3 playerWorld)
        {
            if (_mapClip == null) return playerWorld;
            var visibleHalfWorld = InnerHalf() / Mathf.Max(_mapScale, 0.0001f);
            var local = playerWorld - PlayBounds.Center;
            var maxOffsetX = Mathf.Max(0f, PlayBounds.HalfExtentX - visibleHalfWorld);
            var maxOffsetZ = Mathf.Max(0f, PlayBounds.HalfExtentZ - visibleHalfWorld);
            return PlayBounds.Center + new Vector3(Mathf.Clamp(local.x, -maxOffsetX, maxOffsetX), 0f, Mathf.Clamp(local.z, -maxOffsetZ, maxOffsetZ));
        }

        /// <summary>One glyph per POI of the current layout (rebuilt only when the layout changes).</summary>
        void BuildMarkers(IStageView stage)
        {
            _builtLayoutVersion = stage.LayoutVersion;
            if (_mapRoot == null) return;
            if (_mapScale <= 0.01f) _mapScale = ComputeMapScale();
            for (var i = _mapRoot.childCount - 1; i >= 0; i--) Destroy(_mapRoot.GetChild(i).gameObject);
            _poiMarkers.Clear();
            for (var i = 0; i < stage.PoiCount; i++)
            {
                var poi = stage.Poi(i);
                var marker = CreateChild(_mapRoot, $"POI_{i}_{poi.type}");
                marker.sizeDelta = new Vector2(20f, 20f);
                marker.anchoredPosition = WorldToMap(poi.position);
                V3MinimapMarkerGraphics.Apply(marker, V3MinimapMarkerGraphics.FromPoi(poi.type));
                var group = marker.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                _poiMarkers.Add(new PoiMarkerView { rect = marker, group = group, isBeacon = poi.type == POIType.BossBeacon });
            }
        }

        /// <summary>Positions and visibility (V3 RefreshMarkerPositions + RefreshMarkerVisibility).</summary>
        void RefreshMarkers(IStageView stage, IShipView ship)
        {
            if (stage == null || !stage.Exists) return;
            var count = Mathf.Min(_poiMarkers.Count, stage.PoiCount);
            for (var i = 0; i < count; i++)
            {
                var view = _poiMarkers[i];
                if (view.rect == null) continue;
                var poi = stage.Poi(i);
                view.rect.anchoredPosition = WorldToMap(poi.position);
                if (!stage.HasDiscovery) continue; // V3 left visibility untouched without the discovery system
                if (poi.used)
                {
                    if (view.rect.gameObject.activeSelf) view.rect.gameObject.SetActive(false);
                    continue;
                }
                if (!view.rect.gameObject.activeSelf) view.rect.gameObject.SetActive(true);
                var discovered = stage.IsPoiVisible(i);
                var beaconDiscovered = poi.type == POIType.BossBeacon && stage.Beacon >= BeaconReveal.Discovered;
                var beaconHint = poi.type == POIType.BossBeacon && stage.Beacon >= BeaconReveal.ExactMarker;
                var exploredNearPoi = stage.IsCellExplored(poi.position);
                var nearPlayer = Vector3.Distance(ship.Position, poi.position) <= stage.DiscoveryRadius;
                view.group.alpha = discovered || beaconHint || beaconDiscovered ? 1f : exploredNearPoi || nearPlayer ? 0.92f : 0f;
            }
        }

        void RefreshFog(IStageView stage)
        {
            if (_fogRawImage == null) return;
            if (stage == null || !stage.HasDiscovery)
            {
                _fogRawImage.enabled = false;
                return;
            }

            _fogRawImage.enabled = true;
            var revision = stage.ExplorationRevision;
            var changed = revision != _lastFogRevision || fogMaskResolution != _lastFogMaskResolution
                          || !Mathf.Approximately(fogEdgeSoftnessWorld, _lastFogEdgeSoftness) || _lastFogTint != fogTint;
            if (!changed) return;
            _lastFogRevision = revision;
            _lastFogMaskResolution = fogMaskResolution;
            _lastFogEdgeSoftness = fogEdgeSoftnessWorld;
            _lastFogTint = fogTint;
            EnsureFogTexture();
            RebuildFogMask(stage);
        }

        void RefreshBoundsVeil()
        {
            if (_boundsVeilImage == null) return;
            var changed = _boundsVeilTexture == null || _lastOutOfBoundsVeilColor != outOfBoundsVeilColor
                          || !Mathf.Approximately(_lastOutOfBoundsEdgeSoftnessUv, outOfBoundsEdgeSoftnessUv);
            if (!changed) return;
            _lastOutOfBoundsVeilColor = outOfBoundsVeilColor;
            _lastOutOfBoundsEdgeSoftnessUv = outOfBoundsEdgeSoftnessUv;

            const int size = 128;
            if (_boundsVeilTexture == null || _boundsVeilTexture.width != size)
            {
                DestroyTexture(ref _boundsVeilTexture);
                _boundsVeilTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _boundsVeilPixels = new Color32[size * size];
                _boundsVeilImage.texture = _boundsVeilTexture;
            }

            var innerHalf = 0.5f / MapContentPadding;
            var softness = Mathf.Max(0.005f, outOfBoundsEdgeSoftnessUv);
            var r = ToByte(outOfBoundsVeilColor.r);
            var g = ToByte(outOfBoundsVeilColor.g);
            var b = ToByte(outOfBoundsVeilColor.b);
            for (var y = 0; y < size; y++)
            {
                var dy = Mathf.Abs((y + 0.5f) / size - 0.5f);
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs((x + 0.5f) / size - 0.5f);
                    var outside = Mathf.Max(dx - innerHalf, dy - innerHalf);
                    var alpha = outside <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, outside / softness) * outOfBoundsVeilColor.a;
                    _boundsVeilPixels[y * size + x] = new Color32(r, g, b, ToByte(alpha));
                }
            }
            _boundsVeilTexture.SetPixels32(_boundsVeilPixels);
            _boundsVeilTexture.Apply(false);
        }

        void EnsureFogTexture()
        {
            var width = Mathf.Clamp(fogMaskResolution, 64, 256);
            var aspect = PlayBounds.HalfExtentZ / Mathf.Max(1f, PlayBounds.HalfExtentX);
            var height = Mathf.Clamp(Mathf.RoundToInt(width * aspect), 64, 256);
            if (_fogTexture != null && _fogTexture.width == width && _fogTexture.height == height) return;
            DestroyTexture(ref _fogTexture);
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _fogPixels = new Color32[width * height];
            _fogRawImage.texture = _fogTexture;
        }

        void RebuildFogMask(IStageView stage)
        {
            if (_fogTexture == null || _fogPixels == null) return;
            var width = _fogTexture.width;
            var height = _fogTexture.height;
            var center = PlayBounds.Center;
            var halfX = Mathf.Max(1f, PlayBounds.HalfExtentX);
            var halfZ = Mathf.Max(1f, PlayBounds.HalfExtentZ);
            var r = ToByte(fogTint.r);
            var g = ToByte(fogTint.g);
            var b = ToByte(fogTint.b);
            for (var y = 0; y < height; y++)
            {
                var wz = center.z + (((y + 0.5f) / height) * 2f - 1f) * halfZ;
                for (var x = 0; x < width; x++)
                {
                    var wx = center.x + (((x + 0.5f) / width) * 2f - 1f) * halfX;
                    _fogPixels[y * width + x] = new Color32(r, g, b, ToByte(FogAlpha(stage, new Vector3(wx, 0f, wz))));
                }
            }
            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply(false);
        }

        float FogAlpha(IStageView stage, Vector3 world)
        {
            if (stage.IsCellExplored(world)) return 0f;
            if (fogEdgeSoftnessWorld <= 0.01f) return fogTint.a;
            var cs = stage.ExploreCellSize;
            var cx = Mathf.FloorToInt(world.x / cs);
            var cz = Mathf.FloorToInt(world.z / cs);
            var minDist = float.MaxValue;
            for (var dx = -2; dx <= 2; dx++)
            for (var dz = -2; dz <= 2; dz++)
            {
                var cellCenter = new Vector3((cx + dx) * cs + cs * 0.5f, 0f, (cz + dz) * cs + cs * 0.5f);
                if (!stage.IsCellExplored(cellCenter)) continue;
                var dist = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(cellCenter.x, cellCenter.z));
                if (dist < minDist) minDist = dist;
            }
            if (minDist >= float.MaxValue * 0.5f) return fogTint.a;
            return fogTint.a * Mathf.SmoothStep(0f, 1f, minDist / fogEdgeSoftnessWorld);
        }

        void RefreshBeaconSector(IStageView stage, Vector3 playerPos)
        {
            if (_beaconSector == null || stage == null || !stage.HasDiscovery) return;
            var show = stage.Beacon == BeaconReveal.DirectionSector;
            if (_beaconSector.gameObject.activeSelf != show) _beaconSector.gameObject.SetActive(show);
            if (!show) return;
            _beaconSector.anchoredPosition = Vector2.zero;
            _beaconSector.localRotation = Quaternion.Euler(0f, 0f, BeaconHeadingDegrees(stage, playerPos) + headingOffsetDegrees);
        }

        void RefreshSpawnPings()
        {
            if (_threatRoot == null) return;
            var now = Time.unscaledTime;
            for (var i = _spawnPings.Count - 1; i >= 0; i--)
                if (_spawnPings[i].expires <= now) _spawnPings.RemoveAt(i);

            for (var i = 0; i < _spawnPings.Count; i++)
            {
                var glyph = GlyphAt(_spawnPingIcons, i, "SpawnPing_", 14f);
                SetGlyph(glyph, V3MinimapMarkerGraphics.GlyphKind.GenericPoi);
                glyph.rect.anchoredPosition = WorldToMap(_spawnPings[i].world);
            }
            HideFrom(_spawnPingIcons, _spawnPings.Count);
        }

        void RefreshThreatMarkers(IStageView stage)
        {
            if (_threatRoot == null || _views.Enemies == null) return;
            _views.Enemies.CollectThreats(_threats);
            var write = 0;
            for (var i = 0; i < _threats.Count; i++)
            {
                var threat = _threats[i];
                if (stage != null && stage.HasDiscovery && !stage.IsCellExplored(threat.position)) continue;
                var glyph = GlyphAt(_threatIcons, write++, "Threat_", 18f);
                SetGlyph(glyph, threat.isBoss ? V3MinimapMarkerGraphics.GlyphKind.Boss : V3MinimapMarkerGraphics.GlyphKind.Miniboss);
                glyph.rect.anchoredPosition = WorldToMap(threat.position);
            }
            HideFrom(_threatIcons, write);
        }

        Glyph GlyphAt(List<Glyph> icons, int index, string prefix, float size)
        {
            while (icons.Count <= index)
            {
                var rect = CreateChild(_threatRoot, prefix + icons.Count);
                rect.sizeDelta = new Vector2(size, size);
                icons.Add(new Glyph { rect = rect });
            }
            var glyph = icons[index];
            if (!glyph.rect.gameObject.activeSelf) glyph.rect.gameObject.SetActive(true);
            return glyph;
        }

        /// <summary>Builds the glyph's shapes only when its kind changes (R-86).</summary>
        static void SetGlyph(Glyph glyph, V3MinimapMarkerGraphics.GlyphKind kind)
        {
            if (glyph.kind == (int)kind) return;
            glyph.kind = (int)kind;
            V3MinimapMarkerGraphics.Apply(glyph.rect, kind);
        }

        static void HideFrom(List<Glyph> icons, int start)
        {
            for (var i = start; i < icons.Count; i++)
                if (icons[i].rect.gameObject.activeSelf) icons[i].rect.gameObject.SetActive(false);
        }

        void PulseBossBeacon(IStageView stage)
        {
            _beaconPulse += Time.unscaledDeltaTime * 3f;
            if (stage == null || !stage.HasDiscovery) return;
            var highlightDiscovered = stage.Beacon >= BeaconReveal.Discovered;
            foreach (var view in _poiMarkers)
            {
                if (!view.isBeacon || view.rect == null) continue;
                if (view.group != null && view.group.alpha < 0.5f && !highlightDiscovered) continue;
                var scale = 1f + Mathf.Sin(_beaconPulse) * (highlightDiscovered ? 0.16f : 0.08f);
                view.rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        static float BeaconHeadingDegrees(IStageView stage, Vector3 from)
        {
            var beacon = stage.BeaconPosition;
            if (!beacon.HasValue) return 0f;
            var delta = beacon.Value - from;
            return Mathf.Atan2(delta.x, -delta.z) * Mathf.Rad2Deg + 180f;
        }

        static Vector3 PlayerHeading(IShipView ship)
        {
            var velocity = ship.Velocity;
            velocity.y = 0f;
            return velocity.sqrMagnitude > 0.25f ? velocity.normalized : ship.MovementForward;
        }

        Vector2 WorldToMap(Vector3 world)
        {
            if (_mapScale <= 0.01f) _mapScale = ComputeMapScale();
            var local = world - PlayBounds.Center;
            return new Vector2(local.x, local.z) * _mapScale;
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
            texture = null;
        }

        static RawImage CreateRawImage(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        static RectTransform CreateChild(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
