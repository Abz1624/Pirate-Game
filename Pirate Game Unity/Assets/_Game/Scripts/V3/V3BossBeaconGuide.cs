using HighSeas.POI;
using HighSeas.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>World skylight + HUD arrow once the treasury beacon is discovered.</summary>
    public sealed class V3BossBeaconGuide : MonoBehaviour
    {
        const float SkylightHeight = 26f;

        Light _skylight;
        Transform _skylightRoot;
        RectTransform _arrowRoot;
        Image _arrowImage;
        CanvasGroup _arrowGroup;
        PlayerShipController _player;
        POIManager _poiManager;
        float _pulse;

        public static V3BossBeaconGuide Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3BossBeaconGuide>(true);
            if (existing != null)
                return existing;

            var go = new GameObject("V3BossBeaconGuide");
            go.transform.SetParent(canvas, false);
            var guide = go.AddComponent<V3BossBeaconGuide>();
            guide.BuildArrowUi(go.AddComponent<RectTransform>());
            return guide;
        }

        void BuildArrowUi(RectTransform root)
        {
            Stretch(root);

            _arrowRoot = CreateChild(root, "BeaconArrow");
            _arrowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRoot.pivot = new Vector2(0.5f, 0.5f);
            _arrowRoot.anchoredPosition = new Vector2(0f, 120f);
            _arrowRoot.sizeDelta = new Vector2(48f, 48f);

            _arrowImage = _arrowRoot.gameObject.AddComponent<Image>();
            _arrowImage.sprite = V3MinimapUiSprites.Triangle;
            _arrowImage.color = new Color(0.95f, 0.45f, 1f, 0.95f);
            _arrowImage.raycastTarget = false;
            _arrowGroup = _arrowRoot.gameObject.AddComponent<CanvasGroup>();
            _arrowGroup.alpha = 0f;
            _arrowGroup.blocksRaycasts = false;
            _arrowGroup.interactable = false;
        }

        void Update()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_poiManager == null)
                _poiManager = FindFirstObjectByType<POIManager>();

            var discovery = V3DiscoverySystem.Instance;
            var showGuide = discovery != null &&
                            discovery.BeaconState >= BeaconRevealState.Discovered &&
                            (_poiManager == null || !_poiManager.BossSummoned);

            UpdateSkylight(showGuide, discovery?.GetBeaconPosition());
            UpdateArrow(showGuide, discovery);
        }

        void UpdateSkylight(bool active, Vector3? beaconPos)
        {
            if (!active || !beaconPos.HasValue)
            {
                if (_skylightRoot != null)
                    _skylightRoot.gameObject.SetActive(false);
                return;
            }

            EnsureSkylight(beaconPos.Value);
            _skylightRoot.gameObject.SetActive(true);
            _skylightRoot.position = beaconPos.Value + Vector3.up * SkylightHeight;
            _skylightRoot.rotation = Quaternion.Euler(90f, 0f, 0f);

            _pulse += Time.deltaTime * 2.4f;
            if (_skylight != null)
            {
                _skylight.intensity = 1.35f + Mathf.Sin(_pulse) * 0.35f;
                _skylight.color = new Color(0.92f, 0.55f, 1f, 1f);
            }
        }

        void EnsureSkylight(Vector3 beaconPos)
        {
            if (_skylightRoot != null) return;

            var go = new GameObject("BossBeaconSkylight");
            _skylightRoot = go.transform;
            _skylightRoot.position = beaconPos + Vector3.up * SkylightHeight;
            _skylight = go.AddComponent<Light>();
            _skylight.type = LightType.Spot;
            _skylight.range = 42f;
            _skylight.spotAngle = 58f;
            _skylight.innerSpotAngle = 24f;
            _skylight.shadows = LightShadows.None;
            _skylight.renderMode = LightRenderMode.ForcePixel;
        }

        void UpdateArrow(bool showGuide, V3DiscoverySystem discovery)
        {
            if (_arrowRoot == null || _arrowGroup == null) return;
            if (!showGuide || _player == null || discovery == null)
            {
                _arrowGroup.alpha = 0f;
                return;
            }

            var beacon = discovery.GetBeaconPosition();
            if (!beacon.HasValue)
            {
                _arrowGroup.alpha = 0f;
                return;
            }

            var delta = beacon.Value - _player.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 36f)
            {
                _arrowGroup.alpha = 0f;
                return;
            }

            var heading = ComputeScreenRelativeBearing(delta);
            _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, 180f - heading);
            _arrowGroup.alpha = 0.92f;
        }

        static float ComputeScreenRelativeBearing(Vector3 worldDelta)
        {
            var cam = Camera.main;
            if (cam == null)
                return Mathf.Atan2(worldDelta.x, worldDelta.z) * Mathf.Rad2Deg;

            var forward = cam.transform.forward;
            var right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
                return Mathf.Atan2(worldDelta.x, worldDelta.z) * Mathf.Rad2Deg;

            forward.Normalize();
            right.Normalize();
            var dir = worldDelta.normalized;
            return Mathf.Atan2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward)) * Mathf.Rad2Deg;
        }

        static RectTransform CreateChild(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
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
