using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Transient info toasts above the lower HUD (M14; port of V3ToastHud: same box, text and timing — 560 × 104 at 15 % of
    /// the screen height, three lines at most, unscaled time). The run's toast forwarder shows through it.
    /// </summary>
    public sealed class ToastPresenter : MonoBehaviour, Views.IToastSink
    {
        public const string ObjectName = "V4ToastHud";
        const float HeightFromBottom = 0.15f;

        TextMeshProUGUI _text;
        float _timer;

        public static ToastPresenter Create(Transform canvas)
        {
            var root = new GameObject(ObjectName);
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, HeightFromBottom);
            rect.anchorMax = new Vector2(0.5f, HeightFromBottom);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 104f);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            var toast = root.AddComponent<ToastPresenter>();
            toast._text = textGo.AddComponent<TextMeshProUGUI>();
            toast._text.fontSize = 22f;
            toast._text.alignment = TextAlignmentOptions.Center;
            toast._text.textWrappingMode = TextWrappingModes.Normal;
            toast._text.overflowMode = TextOverflowModes.Ellipsis;
            toast._text.maxVisibleLines = 3;
            toast._text.richText = true;
            toast._text.color = RoguePirates.V4.Foundation.World.PlaceholderFactory.UiAccent;
            // Hackathon UI (13 Oct, owner): a parchment card with ink text in the kit font (was a flat navy box).
            if (RoguePirates.V4.Presentation.Menu.MenuKit.Ready)
            {
                rect.sizeDelta = new Vector2(640f, 112f);
                // Clear of the item row at the bottom centre (the toast sat on its slots).
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.23f);
                RoguePirates.V4.Presentation.Menu.MenuKit.Paint(bg, "Panel_Card", 1.8f).raycastTarget = false;
                RoguePirates.V4.Presentation.Menu.MenuKit.InkText(toast._text, RoguePirates.V4.Presentation.Menu.MenuKit.Ink, 23f);
                textRect.offsetMin = new Vector2(24f, 10f);
                textRect.offsetMax = new Vector2(-24f, -10f);
            }
            root.SetActive(false);
            return toast;
        }

        public string Text => _text != null ? _text.text : null;
        public bool Showing => gameObject.activeSelf;

        public void Show(string message, float seconds)
        {
            if (_text != null) _text.text = RoguePirates.V4.Foundation.Core.DemoMode.Text(message); // no em dashes in UI text, whatever the data says
            _timer = seconds;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) gameObject.SetActive(false);
        }
    }
}
