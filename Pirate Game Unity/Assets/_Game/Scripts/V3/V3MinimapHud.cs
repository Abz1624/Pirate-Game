using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Top-right circular minimap with fog-of-war and abstract objective glyphs.</summary>
    public sealed class V3MinimapHud : MonoBehaviour
    {
        [Header("Screen placement")]
        [Tooltip("Logical pixels from top and right screen edges.")]
        public float cornerInset = 24f;

        [Tooltip("Widget size as a fraction of screen height.")]
        [Range(0.08f, 0.35f)]
        public float heightFraction = 0.18f;

        public float minMapSize = 140f;
        public float maxMapSize = 220f;

        [Header("Frame mask")]
        [Tooltip("Inset from widget edge to the circular clip (match overlay inner hole).")]
        [Range(0.05f, 0.25f)]
        public float ringInsetFraction = 0.135f;

        [Header("Zoom (Megabonk ortho ratio)")]
        [Tooltip("Lower = more zoomed in. Default minimap zoom in Megabonk mods.")]
        public float megabonkNormalZoom = 100f;

        [Tooltip("Full-map minimap zoom reference. Visible axis ≈ normal / full.")]
        public float megabonkFullMapZoom = 300f;

        [Header("Icons")]
        [Tooltip("Extra Z rotation for the player heading arrow (degrees).")]
        public float headingOffsetDegrees = 0f;

        [Header("Fog of war")]
        [Tooltip("Mask texels across playable width. GPU bilinear filtering keeps edges smooth.")]
        [Range(64, 256)]
        public int fogMaskResolution = 160;

        public Color fogTint = new(0.1f, 0.16f, 0.26f, 0.88f);

        [Tooltip("Fade width at explored borders (world meters).")]
        [Range(2f, 24f)]
        public float fogEdgeSoftnessWorld = 10f;

        [Header("Playable bounds")]
        public Color outOfBoundsVeilColor = new(0.02f, 0.05f, 0.1f, 0.9f);

        [Tooltip("UV fade width at the playable rectangle edge (content uses 2.2× bounds).")]
        [Range(0.01f, 0.12f)]
        public float outOfBoundsEdgeSoftnessUv = 0.045f;

        const float MapContentPadding = 2.2f;

        float VisibleWorldAxisFraction =>
            megabonkFullMapZoom > 0.01f ? megabonkNormalZoom / megabonkFullMapZoom : 0.33f;

        RectTransform _mapClip;
        RectTransform _contentRoot;
        RectTransform _mapRoot;
        RectTransform _playerIcon;
        RectTransform _fogRoot;
        RectTransform _threatRoot;
        RectTransform _beaconSector;
        Image _overlayImage;
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
        PlayerShipController _player;
        EnemyWorld _enemyWorld;
        StageLayout _layout;
        readonly List<RectTransform> _threatIcons = new(8);
        readonly List<RectTransform> _spawnPingIcons = new(4);
        static readonly List<(Vector3 world, float expires)> SpawnPings = new(6);
        float _beaconPulse;
        float _mapScale;

        public static void RegisterSpawnPing(Vector3 worldPosition, float durationSeconds = 3f)
        {
            SpawnPings.Add((worldPosition, Time.unscaledTime + durationSeconds));
        }

        public static V3MinimapHud Ensure(Transform canvas, StageLayout layout)
        {
            var existing = canvas.GetComponentInChildren<V3MinimapHud>(true);
            if (existing != null)
            {
                existing._layout = layout;
                existing.EnsureVisualShell();
                existing.BuildMarkers(layout);
                return existing;
            }

            var root = new GameObject("v3_ui_minimap");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            var hud = root.AddComponent<V3MinimapHud>();
            hud.ApplyProjectSettings();
            hud.ApplyTopRightLayout(rect);
            hud.BuildChrome(rect);
            hud._layout = layout;
            hud.BuildMarkers(layout);
            return hud;
        }

        void Awake() => ApplyProjectSettings();

        void Start()
        {
            _player = FindFirstObjectByType<PlayerShipController>();
            _enemyWorld = FindFirstObjectByType<EnemyWorld>();
        }

        void OnDestroy()
        {
            DestroyFogTexture();
            DestroyBoundsVeilTexture();
        }

        public void ApplyProjectSettings()
        {
            var settings = V3MinimapHudSettings.Load();
            settings?.ApplyTo(this);
        }

        void LateUpdate()
        {
            SyncLayout();
            RefreshMapSize();
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_enemyWorld == null)
                _enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (_player == null || _playerIcon == null || _contentRoot == null) return;

            _mapScale = ComputeMapScale();

            var pos = _player.Position;
            var viewCenter = ComputeClampedMapViewCenter(pos);
            _contentRoot.anchoredPosition = -WorldToMap(viewCenter);
            _contentRoot.sizeDelta = new Vector2(
                PlayBounds.HalfExtentX * _mapScale * 2.2f,
                PlayBounds.HalfExtentZ * _mapScale * 2.2f);

            _playerIcon.anchoredPosition = WorldToMap(pos) - WorldToMap(viewCenter);

            var forward = GetPlayerHeading();
            var headingDeg = Mathf.Atan2(forward.x, -forward.z) * Mathf.Rad2Deg + headingOffsetDegrees;
            _playerIcon.localRotation = Quaternion.Euler(0f, 0f, headingDeg);

            RefreshFog(pos);
            RefreshBoundsVeil();
            RefreshBeaconSector(pos);
            RefreshMarkerPositions();
            RefreshMarkerVisibility();
            RefreshThreatMarkers();
            RefreshSpawnPings();
            PulseBossBeacon();
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
            var mask = _mapClip.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _contentRoot = CreateChild(_mapClip, "ContentRoot");
            _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.sizeDelta = new Vector2(800f, 800f);

            var ocean = CreateChild(_contentRoot, "OceanBackground");
            Stretch(ocean);
            var oceanImg = ocean.gameObject.AddComponent<Image>();
            oceanImg.raycastTarget = false;
            if (art?.background != null)
            {
                oceanImg.sprite = art.background;
                oceanImg.type = Image.Type.Simple;
                oceanImg.preserveAspect = false;
            }
            else
                oceanImg.color = new Color(0.05f, 0.22f, 0.42f, 1f);

            var boundsVeilRoot = CreateChild(_contentRoot, "BoundsVeilRoot");
            Stretch(boundsVeilRoot);
            EnsureBoundsVeilImage(boundsVeilRoot);

            _fogRoot = CreateChild(_contentRoot, "FogRoot");
            Stretch(_fogRoot);
            EnsureFogMaskImage();

            _mapRoot = CreateChild(_contentRoot, "MapRoot");
            _mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _mapRoot.pivot = new Vector2(0.5f, 0.5f);
            _mapRoot.sizeDelta = Vector2.zero;

            _threatRoot = CreateChild(_contentRoot, "ThreatRoot");
            _threatRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _threatRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _threatRoot.pivot = new Vector2(0.5f, 0.5f);
            _threatRoot.sizeDelta = Vector2.zero;
            _threatRoot.SetAsLastSibling();

            _beaconSector = CreateChild(_mapClip, "BeaconSector");
            _beaconSector.anchorMin = new Vector2(0.5f, 0.5f);
            _beaconSector.anchorMax = new Vector2(0.5f, 0.5f);
            _beaconSector.pivot = new Vector2(0.5f, 0.5f);
            _beaconSector.anchoredPosition = Vector2.zero;
            _beaconSector.sizeDelta = new Vector2(52f, 52f);
            var sectorImg = _beaconSector.gameObject.AddComponent<Image>();
            sectorImg.sprite = V3MinimapUiSprites.Triangle;
            sectorImg.color = new Color(0.85f, 0.25f, 0.95f, 0.22f);
            sectorImg.raycastTarget = false;
            _beaconSector.gameObject.SetActive(false);

            _playerIcon = CreateChild(_mapClip, "PlayerIcon");
            _playerIcon.anchorMin = new Vector2(0.5f, 0.5f);
            _playerIcon.anchorMax = new Vector2(0.5f, 0.5f);
            _playerIcon.pivot = new Vector2(0.5f, 0.5f);
            _playerIcon.anchoredPosition = Vector2.zero;
            _playerIcon.sizeDelta = new Vector2(16f, 16f);
            V3MinimapMarkerGraphics.Apply(_playerIcon, V3MinimapMarkerGraphics.GlyphKind.Player);

            var overlay = CreateChild(root, "FrameOverlay");
            Stretch(overlay);
            overlay.SetAsLastSibling();
            _overlayImage = overlay.gameObject.AddComponent<Image>();
            _overlayImage.raycastTarget = false;
            if (art?.overlay != null)
            {
                _overlayImage.sprite = art.overlay;
                _overlayImage.type = Image.Type.Simple;
                _overlayImage.preserveAspect = false;
            }
            else
                _overlayImage.color = new Color(0.85f, 0.7f, 0.25f, 0.85f);
        }

        void EnsureVisualShell()
        {
            var rect = transform as RectTransform;
            if (rect != null)
                ApplyTopRightLayout(rect);

            if (transform.Find("MapClip/ContentRoot") != null)
            {
                WireReferences();
                RefreshClipInsets(rect);
                return;
            }

            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _mapClip = null;
            _contentRoot = null;
            _mapRoot = null;
            _fogRoot = null;
            _threatRoot = null;
            _playerIcon = null;
            _beaconSector = null;
            _threatIcons.Clear();
            BuildChrome(rect);
            WireReferences();
            RefreshClipInsets(rect);
        }

        void WireReferences()
        {
            _mapClip = transform.Find("MapClip") as RectTransform;
            if (_mapClip == null) return;
            _contentRoot = _mapClip.Find("ContentRoot") as RectTransform;
            if (_contentRoot == null) return;

            var boundsVeilRoot = _contentRoot.Find("BoundsVeilRoot") as RectTransform;
            if (boundsVeilRoot == null)
            {
                boundsVeilRoot = CreateChild(_contentRoot, "BoundsVeilRoot");
                Stretch(boundsVeilRoot);
                var fogIndex = _contentRoot.Find("FogRoot")?.GetSiblingIndex() ?? 1;
                boundsVeilRoot.SetSiblingIndex(Mathf.Max(0, fogIndex));
            }

            EnsureBoundsVeilImage(boundsVeilRoot);
            _fogRoot = _contentRoot.Find("FogRoot") as RectTransform;
            EnsureFogMaskImage();
            _mapRoot = _contentRoot.Find("MapRoot") as RectTransform;
            _threatRoot = _contentRoot.Find("ThreatRoot") as RectTransform;
            _playerIcon = _mapClip.Find("PlayerIcon") as RectTransform;
            _beaconSector = _mapClip.Find("BeaconSector") as RectTransform;
            _overlayImage = transform.Find("FrameOverlay")?.GetComponent<Image>();
        }

        public void ApplyTopRightLayout(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-cornerInset, -cornerInset);
            rect.sizeDelta = new Vector2(maxMapSize, maxMapSize);
        }

        void OnValidate()
        {
            minMapSize = Mathf.Max(80f, minMapSize);
            maxMapSize = Mathf.Max(minMapSize, maxMapSize);
            megabonkNormalZoom = Mathf.Max(1f, megabonkNormalZoom);
            megabonkFullMapZoom = Mathf.Max(1f, megabonkFullMapZoom);
        }

        void RefreshMapSize()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

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

        float ComputeMapScale()
        {
            if (_mapClip == null) return 1f;
            var innerHalf = Mathf.Min(_mapClip.rect.width, _mapClip.rect.height) * 0.5f;
            if (innerHalf <= 1f) innerHalf = 60f;

            var worldHalf = Mathf.Max(
                32f,
                Mathf.Max(PlayBounds.HalfExtentX, PlayBounds.HalfExtentZ) * VisibleWorldAxisFraction);
            return innerHalf / worldHalf;
        }

        /// <summary>
        /// Keeps the player centered on the minimap until the visible window hits a playable edge,
        /// then slides the map so the icon moves toward that edge (Megabonk-style clamp).
        /// </summary>
        Vector3 ComputeClampedMapViewCenter(Vector3 playerWorld)
        {
            if (_mapClip == null)
                return playerWorld;

            var innerHalf = Mathf.Min(_mapClip.rect.width, _mapClip.rect.height) * 0.5f;
            if (innerHalf <= 1f)
                innerHalf = 60f;

            var visibleHalfWorld = innerHalf / Mathf.Max(_mapScale, 0.0001f);
            var local = playerWorld - PlayBounds.Center;
            local.y = 0f;

            var halfX = PlayBounds.HalfExtentX;
            var halfZ = PlayBounds.HalfExtentZ;
            var maxOffsetX = Mathf.Max(0f, halfX - visibleHalfWorld);
            var maxOffsetZ = Mathf.Max(0f, halfZ - visibleHalfWorld);

            var viewLocal = new Vector3(
                Mathf.Clamp(local.x, -maxOffsetX, maxOffsetX),
                0f,
                Mathf.Clamp(local.z, -maxOffsetZ, maxOffsetZ));

            return PlayBounds.Center + viewLocal;
        }

        public void BuildMarkers(StageLayout layout)
        {
            _layout = layout;
            if (_mapRoot == null || layout?.pois == null) return;

            if (_mapScale <= 0.01f)
                _mapScale = ComputeMapScale();

            for (var i = _mapRoot.childCount - 1; i >= 0; i--)
                Destroy(_mapRoot.GetChild(i).gameObject);

            for (var i = 0; i < layout.pois.Count; i++)
            {
                var poi = layout.pois[i];
                var marker = CreateChild(_mapRoot, $"POI_{i}_{poi.type}");
                marker.sizeDelta = new Vector2(20f, 20f);
                marker.anchoredPosition = WorldToMap(poi.position);
                V3MinimapMarkerGraphics.Apply(marker, V3MinimapMarkerGraphics.FromPoi(poi.type));
                var group = marker.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        void RefreshMarkerPositions()
        {
            if (_layout?.pois == null || _mapRoot == null) return;

            for (var i = 0; i < _mapRoot.childCount; i++)
            {
                var child = _mapRoot.GetChild(i);
                if (!TryParsePoiKey(child.name, out var index)) continue;
                if (index < 0 || index >= _layout.pois.Count) continue;
                var rect = child as RectTransform;
                if (rect != null)
                    rect.anchoredPosition = WorldToMap(_layout.pois[index].position);
            }
        }

        void RefreshFog(Vector3 playerPos)
        {
            if (_fogRawImage == null) return;
            var discovery = V3DiscoverySystem.Instance;
            if (discovery == null) return;

            var revision = discovery.ExplorationRevision;
            var settingsChanged = revision != _lastFogRevision
                                  || fogMaskResolution != _lastFogMaskResolution
                                  || !Mathf.Approximately(fogEdgeSoftnessWorld, _lastFogEdgeSoftness)
                                  || _lastFogTint != fogTint;
            if (!settingsChanged) return;

            _lastFogRevision = revision;
            _lastFogMaskResolution = fogMaskResolution;
            _lastFogEdgeSoftness = fogEdgeSoftnessWorld;
            _lastFogTint = fogTint;
            EnsureFogTexture();
            RebuildFogMask(discovery);
        }

        void EnsureBoundsVeilImage(RectTransform parent)
        {
            if (parent == null) return;

            var existing = parent.Find("BoundsVeil")?.GetComponent<RawImage>();
            if (existing != null)
            {
                _boundsVeilImage = existing;
                return;
            }

            var go = new GameObject("BoundsVeil", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            _boundsVeilImage = go.AddComponent<RawImage>();
            _boundsVeilImage.raycastTarget = false;
            DestroyBoundsVeilTexture();
        }

        void RefreshBoundsVeil()
        {
            if (_boundsVeilImage == null) return;

            var settingsChanged = _boundsVeilTexture == null
                                  || _lastOutOfBoundsVeilColor != outOfBoundsVeilColor
                                  || !Mathf.Approximately(_lastOutOfBoundsEdgeSoftnessUv, outOfBoundsEdgeSoftnessUv);
            if (!settingsChanged) return;

            _lastOutOfBoundsVeilColor = outOfBoundsVeilColor;
            _lastOutOfBoundsEdgeSoftnessUv = outOfBoundsEdgeSoftnessUv;
            EnsureBoundsVeilTexture();
            RebuildBoundsVeilTexture();
        }

        void EnsureBoundsVeilTexture()
        {
            const int size = 128;
            if (_boundsVeilTexture != null && _boundsVeilTexture.width == size) return;

            DestroyBoundsVeilTexture();
            _boundsVeilTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _boundsVeilPixels = new Color32[size * size];
            _boundsVeilImage.texture = _boundsVeilTexture;
        }

        void RebuildBoundsVeilTexture()
        {
            if (_boundsVeilTexture == null || _boundsVeilPixels == null) return;

            var size = _boundsVeilTexture.width;
            var innerHalf = 0.5f / MapContentPadding;
            var softness = Mathf.Max(0.005f, outOfBoundsEdgeSoftnessUv);
            var r = (byte)Mathf.Clamp(Mathf.RoundToInt(outOfBoundsVeilColor.r * 255f), 0, 255);
            var g = (byte)Mathf.Clamp(Mathf.RoundToInt(outOfBoundsVeilColor.g * 255f), 0, 255);
            var b = (byte)Mathf.Clamp(Mathf.RoundToInt(outOfBoundsVeilColor.b * 255f), 0, 255);

            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                var dy = Mathf.Abs(v - 0.5f);
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var dx = Mathf.Abs(u - 0.5f);
                    var outside = Mathf.Max(dx - innerHalf, dy - innerHalf);
                    var alpha = outside <= 0f
                        ? 0f
                        : Mathf.SmoothStep(0f, 1f, outside / softness) * outOfBoundsVeilColor.a;
                    _boundsVeilPixels[y * size + x] = new Color32(r, g, b, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            _boundsVeilTexture.SetPixels32(_boundsVeilPixels);
            _boundsVeilTexture.Apply(false);
        }

        void DestroyBoundsVeilTexture()
        {
            if (_boundsVeilTexture == null) return;
            if (Application.isPlaying) Destroy(_boundsVeilTexture);
            else DestroyImmediate(_boundsVeilTexture);
            _boundsVeilTexture = null;
            _boundsVeilPixels = null;
        }

        void EnsureFogMaskImage()
        {
            if (_fogRoot == null) return;

            var existing = _fogRoot.Find("FogMask")?.GetComponent<RawImage>();
            if (existing != null)
            {
                _fogRawImage = existing;
                return;
            }

            for (var i = _fogRoot.childCount - 1; i >= 0; i--)
                Destroy(_fogRoot.GetChild(i).gameObject);

            var go = new GameObject("FogMask", typeof(RectTransform));
            go.transform.SetParent(_fogRoot, false);
            Stretch(go.GetComponent<RectTransform>());
            _fogRawImage = go.AddComponent<RawImage>();
            _fogRawImage.raycastTarget = false;
            DestroyFogTexture();
            _lastFogRevision = -1;
        }

        void EnsureFogTexture()
        {
            var width = Mathf.Clamp(fogMaskResolution, 64, 256);
            var aspect = PlayBounds.HalfExtentZ / Mathf.Max(1f, PlayBounds.HalfExtentX);
            var height = Mathf.Clamp(Mathf.RoundToInt(width * aspect), 64, 256);

            if (_fogTexture != null && _fogTexture.width == width && _fogTexture.height == height)
                return;

            DestroyFogTexture();
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _fogPixels = new Color32[width * height];
            _fogRawImage.texture = _fogTexture;
        }

        void RebuildFogMask(V3DiscoverySystem discovery)
        {
            if (_fogTexture == null || _fogPixels == null) return;

            var width = _fogTexture.width;
            var height = _fogTexture.height;
            var center = PlayBounds.Center;
            var halfX = Mathf.Max(1f, PlayBounds.HalfExtentX);
            var halfZ = Mathf.Max(1f, PlayBounds.HalfExtentZ);
            var r = (byte)Mathf.Clamp(Mathf.RoundToInt(fogTint.r * 255f), 0, 255);
            var g = (byte)Mathf.Clamp(Mathf.RoundToInt(fogTint.g * 255f), 0, 255);
            var b = (byte)Mathf.Clamp(Mathf.RoundToInt(fogTint.b * 255f), 0, 255);

            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                var wz = center.z + (v * 2f - 1f) * halfZ;
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var wx = center.x + (u * 2f - 1f) * halfX;
                    var world = new Vector3(wx, 0f, wz);
                    var alpha = ComputeFogAlpha(discovery, world);
                    _fogPixels[y * width + x] = new Color32(r, g, b, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply(false);
        }

        float ComputeFogAlpha(V3DiscoverySystem discovery, Vector3 world)
        {
            if (discovery.IsCellExplored(world))
                return 0f;

            if (fogEdgeSoftnessWorld <= 0.01f)
                return fogTint.a;

            var cs = V3DiscoverySystem.ExploreCellSize;
            var cx = Mathf.FloorToInt(world.x / cs);
            var cz = Mathf.FloorToInt(world.z / cs);
            var minDist = float.MaxValue;
            for (var dx = -2; dx <= 2; dx++)
            {
                for (var dz = -2; dz <= 2; dz++)
                {
                    var cellCenter = new Vector3(
                        (cx + dx) * cs + cs * 0.5f,
                        0f,
                        (cz + dz) * cs + cs * 0.5f);
                    if (!discovery.IsCellExplored(cellCenter)) continue;
                    var dist = Vector2.Distance(
                        new Vector2(world.x, world.z),
                        new Vector2(cellCenter.x, cellCenter.z));
                    if (dist < minDist) minDist = dist;
                }
            }

            if (minDist >= float.MaxValue * 0.5f)
                return fogTint.a;

            return fogTint.a * Mathf.SmoothStep(0f, 1f, minDist / fogEdgeSoftnessWorld);
        }

        void DestroyFogTexture()
        {
            if (_fogTexture == null) return;
            if (Application.isPlaying) Destroy(_fogTexture);
            else DestroyImmediate(_fogTexture);
            _fogTexture = null;
            _fogPixels = null;
            if (_fogRawImage != null)
                _fogRawImage.texture = null;
        }

        void RefreshBeaconSector(Vector3 playerPos)
        {
            if (_beaconSector == null || V3DiscoverySystem.Instance == null) return;
            var state = V3DiscoverySystem.Instance.BeaconState;
            if (state == BeaconRevealState.DirectionSector)
            {
                _beaconSector.gameObject.SetActive(true);
                _beaconSector.anchoredPosition = Vector2.zero;
                var bearing = GetBeaconHeadingDegrees(playerPos);
                _beaconSector.localRotation = Quaternion.Euler(0f, 0f, bearing + headingOffsetDegrees);
            }
            else
                _beaconSector.gameObject.SetActive(false);
        }

        void RefreshMarkerVisibility()
        {
            if (_layout?.pois == null || V3DiscoverySystem.Instance == null) return;

            for (var i = 0; i < _mapRoot.childCount; i++)
            {
                var child = _mapRoot.GetChild(i);
                if (!TryParsePoiKey(child.name, out var markerIndex)) continue;
                if (markerIndex < 0 || markerIndex >= _layout.pois.Count) continue;

                var poi = _layout.pois[markerIndex];
                if (poi.used)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                child.gameObject.SetActive(true);
                var discovery = V3DiscoverySystem.Instance;
                var discovered = discovery.IsPoiVisible(markerIndex);
                var beaconDiscovered = poi.type == POIType.BossBeacon &&
                                       discovery.BeaconState >= BeaconRevealState.Discovered;
                var beaconHint = poi.type == POIType.BossBeacon &&
                                 discovery.BeaconState >= BeaconRevealState.ExactMarker;
                var exploredNearPoi = discovery.IsCellExplored(poi.position);
                var nearPlayer = _player != null &&
                                 Vector3.Distance(_player.Position, poi.position) <= V3DiscoverySystem.DiscoveryRadius;

                var alpha = discovered || beaconHint || beaconDiscovered ? 1f :
                    exploredNearPoi || nearPlayer ? 0.92f : 0f;

                var group = child.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = child.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }

                group.alpha = alpha;
            }
        }

        void RefreshSpawnPings()
        {
            if (_threatRoot == null) return;
            var now = Time.unscaledTime;
            for (var i = SpawnPings.Count - 1; i >= 0; i--)
            {
                if (SpawnPings[i].expires <= now)
                    SpawnPings.RemoveAt(i);
            }

            for (var i = 0; i < _spawnPingIcons.Count; i++)
                _spawnPingIcons[i].gameObject.SetActive(false);

            for (var i = 0; i < SpawnPings.Count; i++)
            {
                while (_spawnPingIcons.Count <= i)
                {
                    var icon = CreateChild(_threatRoot, $"SpawnPing_{_spawnPingIcons.Count}");
                    icon.sizeDelta = new Vector2(14f, 14f);
                    _spawnPingIcons.Add(icon);
                }

                var pingIcon = _spawnPingIcons[i];
                pingIcon.gameObject.SetActive(true);
                pingIcon.anchoredPosition = WorldToMap(SpawnPings[i].world);
                V3MinimapMarkerGraphics.Apply(pingIcon, V3MinimapMarkerGraphics.GlyphKind.GenericPoi);
            }
        }

        void RefreshThreatMarkers()
        {
            if (_threatRoot == null || _enemyWorld == null) return;
            var defs = _enemyWorld.Definitions;
            if (defs == null || defs.Length == 0) return;

            var write = 0;
            var agents = _enemyWorld.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (!agent.active) continue;
                if (agent.definitionIndex < 0 || agent.definitionIndex >= defs.Length) continue;

                var def = defs[agent.definitionIndex];
                if (!def.isBoss && !def.isMiniboss) continue;

                if (V3DiscoverySystem.Instance != null && !V3DiscoverySystem.Instance.IsCellExplored(agent.position))
                    continue;

                var icon = GetThreatIcon(write);
                write++;
                icon.gameObject.SetActive(true);
                icon.anchoredPosition = WorldToMap(agent.position);
                V3MinimapMarkerGraphics.Apply(
                    icon,
                    def.isBoss ? V3MinimapMarkerGraphics.GlyphKind.Boss : V3MinimapMarkerGraphics.GlyphKind.Miniboss);
            }

            for (var i = write; i < _threatIcons.Count; i++)
                _threatIcons[i].gameObject.SetActive(false);
        }

        RectTransform GetThreatIcon(int index)
        {
            while (_threatIcons.Count <= index)
            {
                var icon = CreateChild(_threatRoot, $"Threat_{_threatIcons.Count}");
                icon.sizeDelta = new Vector2(18f, 18f);
                _threatIcons.Add(icon);
            }

            return _threatIcons[index];
        }

        void PulseBossBeacon()
        {
            _beaconPulse += Time.unscaledDeltaTime * 3f;
            if (_mapRoot == null || V3DiscoverySystem.Instance == null) return;

            var highlightDiscovered = V3DiscoverySystem.Instance.BeaconState >= BeaconRevealState.Discovered;

            for (var i = 0; i < _mapRoot.childCount; i++)
            {
                var child = _mapRoot.GetChild(i);
                if (!child.name.Contains(nameof(POIType.BossBeacon))) continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group != null && group.alpha < 0.5f && !highlightDiscovered) continue;
                var pulseStrength = highlightDiscovered ? 0.16f : 0.08f;
                var scale = 1f + Mathf.Sin(_beaconPulse) * pulseStrength;
                child.localScale = new Vector3(scale, scale, 1f);
            }
        }

        void SyncLayout()
        {
            if (V3RunContext.Layout != null)
                _layout = V3RunContext.Layout;
        }

        float GetBeaconHeadingDegrees(Vector3 from)
        {
            var beacon = V3DiscoverySystem.Instance?.GetBeaconPosition();
            if (!beacon.HasValue) return 0f;
            var delta = beacon.Value - from;
            delta.y = 0f;
            return Mathf.Atan2(delta.x, -delta.z) * Mathf.Rad2Deg + 180f;
        }

        Vector3 GetPlayerHeading()
        {
            if (_player == null) return Vector3.forward;
            var velocity = _player.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.25f)
                return velocity.normalized;
            return _player.MovementForward;
        }

        static bool TryParsePoiKey(string name, out int index)
        {
            index = -1;
            if (!name.StartsWith("POI_")) return false;
            var end = name.IndexOf('_', 4);
            if (end < 0) return false;
            return int.TryParse(name.Substring(4, end - 4), out index);
        }

        Vector2 WorldToMap(Vector3 world)
        {
            if (_mapScale <= 0.01f)
                _mapScale = ComputeMapScale();
            var local = world - PlayBounds.Center;
            return new Vector2(local.x, local.z) * _mapScale;
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
