using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// V5 M7: the touch boost button (beside the Captain's ability button, on the side away from the stick) and the pause
    /// button (top, shown on every device). The boost press goes through <see cref="TouchActionButton"/> to the ship.
    /// </summary>
    public sealed class TouchControlsPresenter : MonoBehaviour
    {
        public const float BoostSize = 160f, PauseSize = 96f, Margin = 48f, Gap = 24f;

        static readonly Vector3[] Corners = new Vector3[4];
        RectTransform _boost, _pause;
        CanvasGroup _boostGroup;
        CaptainAbilityButtonPresenter _ability;
        Canvas _canvas;

        public RectTransform Boost => _boost;
        public RectTransform Pause => _pause;
        public bool BoostShown => _boostGroup != null && _boostGroup.alpha > 0f;

        public static TouchControlsPresenter Create(Transform hudCanvas, CaptainAbilityButtonPresenter ability)
        {
            var existing = hudCanvas.GetComponentInChildren<TouchControlsPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V5TouchControls", typeof(RectTransform));
            go.transform.SetParent(hudCanvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<TouchControlsPresenter>();
            presenter._ability = ability;
            presenter.Build(rt);
            return presenter;
        }

        void Build(RectTransform root)
        {
            var stick = FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            _boost = Round("BoostButton", root, BoostSize, stick != null ? stick.BaseSprite : null, "BOOST");
            _boost.gameObject.AddComponent<TouchActionButton>().action = TouchAction.Boost;
            _boostGroup = _boost.gameObject.AddComponent<CanvasGroup>();
            _pause = Round("PauseButton", root, PauseSize, null, "II");
            _pause.gameObject.AddComponent<TouchActionButton>().action = TouchAction.Pause;
            _pause.anchorMin = _pause.anchorMax = new Vector2(0.5f, 1f);
            _pause.pivot = new Vector2(0.5f, 1f);
            _pause.anchoredPosition = new Vector2(-360f, -24f);
        }

        static RectTransform Round(string name, RectTransform parent, float size, Sprite sprite, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = sprite != null ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.06f, 0.1f, 0.16f, 0.8f);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(rt, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var t = labelGo.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size * 0.22f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.95f, 0.93f, 0.88f, 1f);
            t.raycastTarget = false;
            return rt;
        }

        void LateUpdate()
        {
            var show = VirtualJoystick.TouchControlsWanted;
            if (BoostShown != show)
            {
                _boostGroup.alpha = show ? 1f : 0f;
                _boostGroup.blocksRaycasts = show;
            }
            if (show) PlaceBoost();
        }

        /// <summary>Beside the ability pad when it shows (towards the screen's middle), else in the corner away from the stick.</summary>
        void PlaceBoost()
        {
            var right = !GameSettings.Current.stickOnRight;
            if (_ability != null && _ability.IsShown && _ability.Pad != null)
            {
                var pad = _ability.Pad;
                var corners = Corners;
                pad.GetWorldCorners(corners);
                var parent = (RectTransform)_boost.parent;
                var anchor = right ? parent.InverseTransformPoint(corners[0]) : parent.InverseTransformPoint(corners[3]);
                _boost.anchorMin = _boost.anchorMax = new Vector2(0.5f, 0.5f);
                _boost.pivot = right ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
                var pivotOffset = new Vector2((0.5f - parent.pivot.x) * parent.rect.width, (0.5f - parent.pivot.y) * parent.rect.height);
                _boost.anchoredPosition = (Vector2)anchor - pivotOffset + new Vector2(right ? -Gap : Gap, 0f);
                return;
            }
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var safe = Screen.safeArea;
            var bottom = Mathf.Max(0f, safe.yMin) / scale;
            _boost.anchorMin = _boost.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            _boost.pivot = new Vector2(right ? 1f : 0f, 0f);
            var side = (right ? Mathf.Max(0f, Screen.width - safe.xMax) : Mathf.Max(0f, safe.xMin)) / scale;
            _boost.anchoredPosition = new Vector2(right ? -(Margin + side) : Margin + side, Margin + bottom);
        }
    }
}
