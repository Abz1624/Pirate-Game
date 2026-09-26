using HighSeas.V3;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Beacon guide (M6): port of V3BossBeaconGuide. Once the Treasury Beacon is discovered and until the boss is
    /// summoned, a pulsing violet spotlight hangs over the beacon and a screen arrow points to it (hidden within 6 m).
    /// Reads the stage and ship views.
    /// </summary>
    public sealed class BeaconGuidePresenter : MonoBehaviour
    {
        const float SkylightHeight = 26f;

        Light _skylight;
        Transform _skylightRoot;
        RectTransform _arrowRoot;
        CanvasGroup _arrowGroup;
        IRunViews _views;
        float _pulse;

        public bool ArrowVisible => _arrowGroup != null && _arrowGroup.alpha > 0f;
        public bool SkylightVisible => _skylightRoot != null && _skylightRoot.gameObject.activeSelf;

        public static BeaconGuidePresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<BeaconGuidePresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4BeaconGuide", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var guide = go.AddComponent<BeaconGuidePresenter>();
            guide.BuildArrowUi((RectTransform)go.transform);
            return guide;
        }

        public void Bind(IRunViews views) => _views = views;

        void BuildArrowUi(RectTransform root)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var arrow = new GameObject("BeaconArrow", typeof(RectTransform));
            arrow.transform.SetParent(root, false);
            _arrowRoot = (RectTransform)arrow.transform;
            _arrowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRoot.pivot = new Vector2(0.5f, 0.5f);
            _arrowRoot.anchoredPosition = new Vector2(0f, 120f);
            _arrowRoot.sizeDelta = new Vector2(48f, 48f);

            var image = arrow.AddComponent<Image>();
            image.sprite = V3MinimapUiSprites.Triangle;
            image.color = new Color(0.95f, 0.45f, 1f, 0.95f);
            image.raycastTarget = false;
            _arrowGroup = arrow.AddComponent<CanvasGroup>();
            _arrowGroup.alpha = 0f;
            _arrowGroup.blocksRaycasts = false;
            _arrowGroup.interactable = false;
        }

        void Update()
        {
            if (_views == null) return;
            var stage = _views.Stage;
            var show = stage != null && stage.HasDiscovery && stage.Beacon >= BeaconReveal.Discovered && !stage.BossSummoned;
            var beacon = show ? stage.BeaconPosition : null;
            UpdateSkylight(show, beacon);
            UpdateArrow(show, beacon);
        }

        void OnDestroy()
        {
            if (_skylightRoot != null) Destroy(_skylightRoot.gameObject);
        }

        void UpdateSkylight(bool active, Vector3? beaconPos)
        {
            if (!active || !beaconPos.HasValue)
            {
                if (_skylightRoot != null && _skylightRoot.gameObject.activeSelf) _skylightRoot.gameObject.SetActive(false);
                return;
            }

            if (_skylightRoot == null)
            {
                var go = new GameObject("BossBeaconSkylight");
                _skylightRoot = go.transform;
                _skylight = go.AddComponent<Light>();
                _skylight.type = LightType.Spot;
                _skylight.range = 42f;
                _skylight.spotAngle = 58f;
                _skylight.innerSpotAngle = 24f;
                _skylight.shadows = LightShadows.None;
                _skylight.renderMode = LightRenderMode.ForcePixel;
            }
            if (!_skylightRoot.gameObject.activeSelf) _skylightRoot.gameObject.SetActive(true);
            _skylightRoot.position = beaconPos.Value + Vector3.up * SkylightHeight;
            _skylightRoot.rotation = Quaternion.Euler(90f, 0f, 0f);

            _pulse += Time.deltaTime * 2.4f;
            if (_skylight != null)
            {
                _skylight.intensity = 1.35f + Mathf.Sin(_pulse) * 0.35f;
                _skylight.color = new Color(0.92f, 0.55f, 1f, 1f);
            }
        }

        void UpdateArrow(bool show, Vector3? beacon)
        {
            if (_arrowRoot == null || _arrowGroup == null) return;
            var ship = _views.Ship;
            if (!show || !beacon.HasValue || ship == null || !ship.Exists)
            {
                _arrowGroup.alpha = 0f;
                return;
            }

            var delta = beacon.Value - ship.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 36f)
            {
                _arrowGroup.alpha = 0f;
                return;
            }

            _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, 180f - ScreenRelativeBearing(delta));
            _arrowGroup.alpha = 0.92f;
        }

        static float ScreenRelativeBearing(Vector3 worldDelta)
        {
            var cam = Camera.main;
            if (cam == null) return Mathf.Atan2(worldDelta.x, worldDelta.z) * Mathf.Rad2Deg;
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
    }
}
