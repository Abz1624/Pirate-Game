using HighSeas.Core;
using HighSeas.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Top-center run stats: gold, XP, and rum (hull regen proxy).</summary>
    public sealed class V3RunStatsHud : MonoBehaviour
    {
        TextMeshProUGUI _goldText;
        TextMeshProUGUI _xpText;
        TextMeshProUGUI _rumText;
        LevelSystem _levels;

        public static V3RunStatsHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3RunStatsHud>(true);
            if (existing != null) return existing;

            var root = new GameObject("PLACEHOLDER_v3_ui_stats_bar");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(520f, 40f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.88f);

            var hud = root.AddComponent<V3RunStatsHud>();
            hud._goldText = CreateStat(root.transform, "GOLD", new Vector2(-170f, 0f), RunHudTheme.Gold);
            hud._xpText = CreateStat(root.transform, "XP", new Vector2(0f, 0f), RunHudTheme.XpFill);
            hud._rumText = CreateStat(root.transform, "RUM", new Vector2(170f, 0f), new Color(0.95f, 0.55f, 0.35f));
            return hud;
        }

        static TextMeshProUGUI CreateStat(Transform parent, string label, Vector2 pos, Color accent)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(150f, 32f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = accent;
            text.text = $"{label}: —";
            return text;
        }

        void Update()
        {
            if (RunManager.Instance == null) return;
            if (_levels == null) _levels = LevelSystem.Instance;

            if (_goldText != null)
                _goldText.text = $"GOLD {RunManager.Instance.Gold:N0}";

            if (_xpText != null && _levels != null)
                _xpText.text = $"XP LV {_levels.Level}  {Mathf.FloorToInt(_levels.CurrentXP)}/{Mathf.FloorToInt(_levels.XPToNext)}";

            if (_rumText != null)
            {
                var regen = RunManager.Instance.Stats.Get(StatId.HullRegen);
                _rumText.text = regen > 0.01f ? $"RUM +{regen:0.1}/s" : "RUM —";
            }
        }
    }
}
