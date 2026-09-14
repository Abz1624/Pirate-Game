using HighSeas.Core;
using HighSeas.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Megabonk-style count-up survival timer with overtime label.</summary>
    public sealed class V3RunTimerHud : MonoBehaviour
    {
        TextMeshProUGUI _caption;
        TextMeshProUGUI _timer;

        public static V3RunTimerHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3RunTimerHud>(true);
            if (existing != null) return existing;

            var root = new GameObject("PLACEHOLDER_v3_ui_timer");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -58f);
            rect.sizeDelta = new Vector2(176f, 68f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.9f);

            var hud = root.AddComponent<V3RunTimerHud>();
            hud._caption = CreateLine(root.transform, "SURVIVE", new Vector2(0f, 16f), 12f, RunHudTheme.Label);
            hud._timer = CreateLine(root.transform, "00:00", new Vector2(0f, -12f), 30f, RunHudTheme.Value);
            hud._timer.enableAutoSizing = true;
            hud._timer.fontSizeMin = 22f;
            hud._timer.fontSizeMax = 30f;
            return hud;
        }

        static TextMeshProUGUI CreateLine(
            Transform parent,
            string text,
            Vector2 pos,
            float size,
            Color color)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(160f, 36f);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            return label;
        }

        void Update()
        {
            if (RunManager.Instance == null) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            var mins = Mathf.FloorToInt(elapsed / 60f);
            var secs = Mathf.FloorToInt(elapsed % 60f);
            var overtime = RunManager.Instance.IsOvertime;

            if (_caption != null)
                _caption.text = overtime ? "DREAD TIDE" : "SURVIVE";
            if (_timer != null)
            {
                _timer.text = $"{mins:00}:{secs:00}";
                _timer.color = overtime ? RunHudTheme.TimerOvertime : RunHudTheme.Value;
            }
        }
    }
}
