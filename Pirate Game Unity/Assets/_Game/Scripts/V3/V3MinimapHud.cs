using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Placeholder minimap — explored water, player heading, POI/beacon markers.</summary>
    public sealed class V3MinimapHud : MonoBehaviour
    {
        const float MapScale = 0.42f;
        const float MapSize = 168f;

        RectTransform _mapRoot;
        RectTransform _playerIcon;
        PlayerShipController _player;

        public static V3MinimapHud Ensure(Transform canvas, StageLayout layout)
        {
            var existing = canvas.GetComponentInChildren<V3MinimapHud>(true);
            if (existing != null)
            {
                existing.BuildMarkers(layout);
                return existing;
            }

            var root = new GameObject("PLACEHOLDER_v3_ui_minimap");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(MapSize, MapSize);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.12f, 0.2f, 0.9f);

            var mapGo = new GameObject("MapRoot");
            mapGo.transform.SetParent(root.transform, false);
            var mapRect = mapGo.AddComponent<RectTransform>();
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = new Vector2(8f, 8f);
            mapRect.offsetMax = new Vector2(-8f, -8f);

            var playerGo = new GameObject("PlayerIcon");
            playerGo.transform.SetParent(mapRect, false);
            var playerRect = playerGo.AddComponent<RectTransform>();
            playerRect.sizeDelta = new Vector2(10f, 10f);
            var playerImg = playerGo.AddComponent<Image>();
            playerImg.color = Color.white;

            var hud = root.AddComponent<V3MinimapHud>();
            hud._mapRoot = mapRect;
            hud._playerIcon = playerRect;
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
        }

        public void BuildMarkers(StageLayout layout)
        {
            if (_mapRoot == null || layout?.pois == null) return;

            for (var i = _mapRoot.childCount - 1; i >= 0; i--)
            {
                var child = _mapRoot.GetChild(i);
                if (child == _playerIcon) continue;
                Destroy(child.gameObject);
            }

            for (var i = 0; i < layout.pois.Count; i++)
            {
                var poi = layout.pois[i];
                var marker = new GameObject(poi.type.ToString());
                marker.transform.SetParent(_mapRoot, false);
                var rect = marker.AddComponent<RectTransform>();
                rect.sizeDelta = poi.type == POIType.BossBeacon ? new Vector2(12f, 12f) : new Vector2(8f, 8f);
                rect.anchoredPosition = WorldToMap(poi.position);
                var img = marker.AddComponent<Image>();
                img.color = poi.type switch
                {
                    POIType.SalvageChest => new Color(0.85f, 0.65f, 0.2f),
                    POIType.BossBeacon => new Color(0.85f, 0.25f, 0.95f),
                    POIType.GildedWreck => new Color(0.55f, 0.4f, 0.3f),
                    POIType.MerchantSkiff => new Color(0.9f, 0.55f, 0.2f),
                    POIType.TrialBuoy => new Color(0.2f, 0.7f, 0.95f),
                    POIType.CursedIdol => new Color(0.75f, 0.35f, 0.95f),
                    _ => Color.gray
                };
            }
        }

        Vector2 WorldToMap(Vector3 world) =>
            new Vector2(world.x, world.z) * MapScale;
    }
}
