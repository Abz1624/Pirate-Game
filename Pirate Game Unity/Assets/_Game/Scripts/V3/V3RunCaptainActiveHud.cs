using HighSeas.Combat;
using HighSeas.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    public sealed class V3RunCaptainActiveHud : MonoBehaviour
    {
        Button _button;
        Image _fill;
        TextMeshProUGUI _label;
        bool _built;

        public bool IsReady => _built && _label != null && _fill != null;

        public static V3RunCaptainActiveHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3RunCaptainActiveHud>(true);
            if (existing != null && existing.IsReady) return existing;
            if (existing != null)
                Destroy(existing.gameObject);

            var root = new GameObject("PLACEHOLDER_v3_ui_captain_active");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 150f);
            rect.sizeDelta = new Vector2(168f, 52f);

            var hud = root.AddComponent<V3RunCaptainActiveHud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            if (_built) return;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.1f, 0.16f, 0.92f);

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(OnClick);

            var fillGo = new GameObject("CooldownFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            _fill = fillGo.GetComponent<Image>();
            _fill.color = new Color(0.35f, 0.72f, 1f, 0.35f);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Vertical;
            _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _fill.fillAmount = 0f;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 11f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = RunHudTheme.Value;
            _built = true;
        }

        void OnClick() => V3CaptainActiveController.Instance?.TryActivate();

        void Update()
        {
            if (!IsReady) return;

            var show = CombatExperimentContext.CaptainActivesEnabled && V3CaptainActiveController.Instance != null;
            gameObject.SetActive(show);
            if (!show) return;

            var active = V3CaptainActiveController.Instance;
            var ready = active.IsReady;
            _label.text = ready
                ? $"ACTIVE (Q)\n{active.ActiveDisplayName}"
                : $"ACTIVE (Q)\n{Mathf.CeilToInt(active.CooldownRemaining)}s";

            var duration = Mathf.Max(0.01f, active.CooldownDuration);
            _fill.fillAmount = ready ? 0f : active.CooldownRemaining / duration;
        }
    }
}
