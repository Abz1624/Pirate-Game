using TMPro;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Top-center transient messages (pressure beats, objective secured).</summary>
    public sealed class V3ToastHud : MonoBehaviour
    {
        public static V3ToastHud Instance { get; private set; }

        TextMeshProUGUI _text;
        float _timer;

        public static V3ToastHud Ensure(Transform canvas)
        {
            if (Instance != null) return Instance;

            var root = new GameObject("PLACEHOLDER_v3_ui_toast_hud");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(720f, 56f);

            var bg = root.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            var toast = root.AddComponent<V3ToastHud>();
            toast._text = textGo.AddComponent<TextMeshProUGUI>();
            toast._text.fontSize = 24f;
            toast._text.alignment = TextAlignmentOptions.Center;
            toast._text.color = V3PlaceholderFactory.UiAccent;
            root.SetActive(false);
            Instance = toast;
            return toast;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(string message, float durationSeconds = 4f)
        {
            if (_text != null)
                _text.text = message;
            _timer = durationSeconds;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
                gameObject.SetActive(false);
        }
    }
}
