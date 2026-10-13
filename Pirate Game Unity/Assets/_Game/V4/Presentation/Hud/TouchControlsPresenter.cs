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
        public const float BoostSize = 160f, PauseSize = 120f, Margin = 48f, Gap = 24f;

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
            _pause.anchorMin = _pause.anchorMax = new Vector2(0f, 1f);
            _pause.pivot = new Vector2(0f, 1f);
            _pause.anchoredPosition = new Vector2(Margin, -200f);
            KitPause(_pause);
            // Above the stick's touch zone (the left half) and the raid / race HUD canvases, so a tap always reaches it.
            var c = _pause.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = PauseSortingOrder;
            _pause.gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Hackathon (13 Oct, owner): the pause button's canvas order (over the stick zone and the raid / race HUD).</summary>
        public const int PauseSortingOrder = 600;

        /// <summary>
        /// Hackathon (13 Oct, owner: "a pause button so that players can pause or exit the match"): the kit's pause button
        /// art (Pause_2), else the kit's gold square with two ink bars; without the kit the old dark round "II" stays.
        /// </summary>
        static void KitPause(RectTransform pause)
        {
            var image = pause.GetComponent<Image>();
            var art = RoguePirates.V4.Presentation.Menu.MenuKit.S("Pause_2");
            var square = art == null ? RoguePirates.V4.Presentation.Menu.MenuKit.S("Btn_Square_Gold") : null;
            if (art == null && square == null) return;
            var label = pause.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            image.sprite = art != null ? art : square;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            if (art != null) return;
            for (var i = 0; i < 2; i++)
            {
                var bar = new GameObject(i == 0 ? "BarLeft" : "BarRight", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(pause, false);
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(PauseSize * 0.12f, PauseSize * 0.42f);
                rt.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * PauseSize * 0.12f, 0f);
                var img = bar.GetComponent<Image>();
                img.color = RoguePirates.V4.Presentation.Menu.MenuKit.Ink;
                img.raycastTarget = false;
            }
        }

        /// <summary>The pause button just below the captain's panel (top left, clear of the coins, timer and minimap on the
        /// right and the raid / race / 1v1 strip in the middle); without the panel, the top-left corner inside the safe area.</summary>
        void PlacePause()
        {
            if (_playerPanel == null && Time.unscaledTime >= _nextPanelLook)
            {
                _nextPanelLook = Time.unscaledTime + 1f;
                var canvas = GetComponentInParent<Canvas>();
                var top = canvas != null ? canvas.rootCanvas.transform : transform.parent;
                foreach (var t in top.GetComponentsInChildren<RectTransform>(true))
                    if (t.name == "PlayerPanel") { _playerPanel = t; break; }
            }
            var parent = (RectTransform)_pause.parent;
            if (_playerPanel != null && _playerPanel.gameObject.activeInHierarchy)
            {
                _playerPanel.GetWorldCorners(Corners);
                var bottomLeft = (Vector2)parent.InverseTransformPoint(Corners[0]);
                var pivotOffset = new Vector2((0.5f - parent.pivot.x) * parent.rect.width, (0.5f - parent.pivot.y) * parent.rect.height);
                if (_pause.anchorMin != new Vector2(0.5f, 0.5f)) _pause.anchorMin = _pause.anchorMax = new Vector2(0.5f, 0.5f);
                var pos = bottomLeft - pivotOffset + new Vector2(0f, -Gap);
                if ((_pause.anchoredPosition - pos).sqrMagnitude > 0.25f) _pause.anchoredPosition = pos;
                return;
            }
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var safe = Screen.safeArea;
            if (_pause.anchorMin != new Vector2(0f, 1f)) _pause.anchorMin = _pause.anchorMax = new Vector2(0f, 1f);
            _pause.anchoredPosition = new Vector2(Margin + Mathf.Max(0f, safe.xMin) / scale, -(200f + Mathf.Max(0f, Screen.height - safe.yMax) / scale));
        }

        RectTransform _playerPanel;
        float _nextPanelLook;

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
            if (_pause != null)
            {
                // Hidden once the run or match is over (the results screen has its own way back).
                var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
                var pauseShown = run == null || !run.IsRunEnded;
                if (_pause.gameObject.activeSelf != pauseShown) _pause.gameObject.SetActive(pauseShown);
                if (pauseShown) PlacePause();
            }
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
