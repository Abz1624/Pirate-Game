using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Minimap-only fog-of-war and discovery-gated markers. The 3D world stays fully visible.</summary>
    public sealed class V3MinimapHud : MonoBehaviour
    {
        const float MapScale = 0.42f;
        const float MapSize = 168f;

        RectTransform _mapRoot;
        RectTransform _playerIcon;
        RectTransform _fogRoot;
        RectTransform _beaconSector;
        PlayerShipController _player;
        StageLayout _layout;

        public static V3MinimapHud Ensure(Transform canvas, StageLayout layout)
        {
            var existing = canvas.GetComponentInChildren<V3MinimapHud>(true);
            if (existing != null)
            {
                existing._layout = layout;
                existing.BuildMarkers(layout);
                return existing;
            }

            var root = new GameObject("PLACEHOLDER_v3_ui_minimap");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -140f);
            rect.sizeDelta = new Vector2(MapSize, MapSize);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.06f, 0.1f, 0.95f);

            var mapGo = new GameObject("MapRoot");
            mapGo.transform.SetParent(root.transform, false);
            var mapRect = mapGo.AddComponent<RectTransform>();
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = new Vector2(8f, 8f);
            mapRect.offsetMax = new Vector2(-8f, -8f);

            var fogGo = new GameObject("FogRoot");
            fogGo.transform.SetParent(mapRect, false);
            var fogRect = fogGo.AddComponent<RectTransform>();
            fogRect.anchorMin = Vector2.zero;
            fogRect.anchorMax = Vector2.one;
            fogRect.offsetMin = Vector2.zero;
            fogRect.offsetMax = Vector2.zero;

            var playerGo = new GameObject("PlayerIcon");
            playerGo.transform.SetParent(mapRect, false);
            var playerRect = playerGo.AddComponent<RectTransform>();
            playerRect.sizeDelta = new Vector2(10f, 14f);
            var playerImg = playerGo.AddComponent<Image>();
            playerImg.color = Color.white;

            var sectorGo = new GameObject("BeaconSector");
            sectorGo.transform.SetParent(mapRect, false);
            var sectorRect = sectorGo.AddComponent<RectTransform>();
            sectorRect.sizeDelta = new Vector2(40f, 40f);
            var sectorImg = sectorGo.AddComponent<Image>();
            sectorImg.color = new Color(0.85f, 0.25f, 0.95f, 0.25f);
            sectorGo.SetActive(false);

            CreateMapBorder(mapRect);

            var hud = root.AddComponent<V3MinimapHud>();
            hud._mapRoot = mapRect;
            hud._fogRoot = fogRect;
            hud._playerIcon = playerRect;
            hud._beaconSector = sectorRect;
            hud._layout = layout;
            hud.BuildMarkers(layout);
            return hud;
        }

        void Start() => _player = FindFirstObjectByType<PlayerShipController>();

        void Update()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_player == null || _playerIcon == null) return;

            var pos = _player.Position;
            _playerIcon.anchoredPosition = WorldToMap(pos);
            var forward = _player.MovementForward;
            _playerIcon.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);

            RefreshFog();
            RefreshBeaconSector(pos);
            RefreshMarkerVisibility();
        }

        public void BuildMarkers(StageLayout layout)
        {
            _layout = layout;
            if (_mapRoot == null || layout?.pois == null) return;

            for (var i = _mapRoot.childCount - 1; i >= 0; i--)
            {
                var child = _mapRoot.GetChild(i);
                if (child == _playerIcon || child == _fogRoot || child == _beaconSector) continue;
                Destroy(child.gameObject);
            }

            for (var i = 0; i < layout.pois.Count; i++)
            {
                var poi = layout.pois[i];
                var marker = new GameObject($"POI_{i}_{poi.type}");
                marker.transform.SetParent(_mapRoot, false);
                var rect = marker.AddComponent<RectTransform>();
                rect.sizeDelta = GetMarkerSize(poi.type);
                rect.anchoredPosition = WorldToMap(poi.position);
                var img = marker.AddComponent<Image>();
                img.color = GetMarkerColor(poi.type);
                marker.AddComponent<CanvasGroup>();
            }
        }

        void RefreshFog()
        {
            if (_fogRoot == null || V3DiscoverySystem.Instance == null) return;

            for (var x = -10; x <= 10; x++)
            {
                for (var z = -10; z <= 10; z++)
                {
                    var world = new Vector3(x * V3DiscoverySystem.ExploreCellSize, 0f, z * V3DiscoverySystem.ExploreCellSize);
                    var key = $"fog_{x}_{z}";
                    var child = _fogRoot.Find(key);
                    var explored = V3DiscoverySystem.Instance.IsCellExplored(world);
                    if (!explored)
                    {
                        if (child == null)
                        {
                            var fog = new GameObject(key);
                            fog.transform.SetParent(_fogRoot, false);
                            var rect = fog.AddComponent<RectTransform>();
                            rect.sizeDelta = new Vector2(8f, 8f);
                            rect.anchoredPosition = WorldToMap(world);
                            var img = fog.AddComponent<Image>();
                            img.color = new Color(0.02f, 0.05f, 0.08f, 0.85f);
                        }
                    }
                    else if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        void RefreshBeaconSector(Vector3 playerPos)
        {
            if (_beaconSector == null || V3DiscoverySystem.Instance == null) return;
            var state = V3DiscoverySystem.Instance.BeaconState;
            if (state == BeaconRevealState.DirectionSector)
            {
                _beaconSector.gameObject.SetActive(true);
                _beaconSector.anchoredPosition = WorldToMap(playerPos);
                var bearing = V3DiscoverySystem.Instance.GetBeaconBearingDegrees(playerPos);
                _beaconSector.localRotation = Quaternion.Euler(0f, 0f, -bearing);
            }
            else
            {
                _beaconSector.gameObject.SetActive(false);
            }
        }

        void RefreshMarkerVisibility()
        {
            if (_layout?.pois == null || V3DiscoverySystem.Instance == null) return;
            var markerIndex = 0;
            for (var i = 0; i < _mapRoot.childCount; i++)
            {
                var child = _mapRoot.GetChild(i);
                if (child == _playerIcon || child == _fogRoot || child == _beaconSector) continue;
                if (!child.name.StartsWith("POI_")) continue;

                var visible = V3DiscoverySystem.Instance.IsPoiVisible(markerIndex) ||
                              V3DiscoverySystem.Instance.BeaconState >= BeaconRevealState.ExactMarker &&
                              _layout.pois[markerIndex].type == POIType.BossBeacon;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                group.alpha = visible ? 1f : 0f;
                markerIndex++;
            }
        }

        static Vector2 GetMarkerSize(POIType type) =>
            type switch
            {
                POIType.BossBeacon => new Vector2(12f, 12f),
                POIType.SalvageChest => new Vector2(7f, 7f),
                _ => new Vector2(9f, 9f)
            };

        static Color GetMarkerColor(POIType type) =>
            type switch
            {
                POIType.SalvageChest => new Color(0.85f, 0.65f, 0.2f),
                POIType.BossBeacon => new Color(0.85f, 0.25f, 0.95f),
                POIType.GildedWreck => new Color(0.55f, 0.4f, 0.3f),
                POIType.MerchantSkiff => new Color(0.9f, 0.55f, 0.2f),
                POIType.TrialBuoy => new Color(0.2f, 0.7f, 0.95f),
                POIType.CursedIdol => new Color(0.75f, 0.35f, 0.95f),
                _ => Color.gray
            };

        Vector2 WorldToMap(Vector3 world)
        {
            var local = world - PlayBounds.Center;
            return new Vector2(local.x, local.z) * MapScale;
        }

        static void CreateMapBorder(RectTransform mapRoot)
        {
            var borderGo = new GameObject("StormBorder");
            borderGo.transform.SetParent(mapRoot, false);
            var border = borderGo.AddComponent<RectTransform>();
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(1f, 1f);
            border.offsetMax = new Vector2(-1f, -1f);

            var image = borderGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;

            var outline = borderGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.04f, 0.38f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
