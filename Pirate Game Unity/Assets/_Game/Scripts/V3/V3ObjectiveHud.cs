using HighSeas.Core;
using HighSeas.Data;
using HighSeas.POI;
using TMPro;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Megabonk-style objective list (placeholder HUD).</summary>
    public sealed class V3ObjectiveHud : MonoBehaviour
    {
        readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[3];
        readonly RectTransform[] _lineRoots = new RectTransform[3];
        DemoPacingSettings _pacing;

        public static V3ObjectiveHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3ObjectiveHud>(true);
            if (existing != null) return existing;

            var root = new GameObject("PLACEHOLDER_v3_ui_objective_hud");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -140f);
            rect.sizeDelta = new Vector2(300f, 132f);

            var panel = root.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0.05f, 0.08f, 0.14f, 0.86f);

            var hud = root.AddComponent<V3ObjectiveHud>();
            hud._lineRoots[0] = CreateLine(root.transform, "ObjectiveLine0", new Vector2(0f, -12f), out hud._lines[0]);
            hud._lineRoots[1] = CreateLine(root.transform, "ObjectiveLine1", new Vector2(0f, -40f), out hud._lines[1]);
            hud._lineRoots[2] = CreateLine(root.transform, "ObjectiveLine2", new Vector2(0f, -68f), out hud._lines[2]);
            hud._pacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            return hud;
        }

        static RectTransform CreateLine(Transform parent, string name, Vector2 anchoredPos, out TextMeshProUGUI text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(-16f, 24f);
            text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = V3PlaceholderFactory.UiText;
            return rect;
        }

        void Update()
        {
            if (RunManager.Instance == null) return;

            V3MegabonkQuestDisplay.GatherRunState(
                out var beaconState,
                out var bossSummoned,
                out var bossDefeated);
            var visible = V3MegabonkQuestDisplay.BuildVisibleLines(beaconState, bossSummoned, bossDefeated);

            for (var i = 0; i < _lineRoots.Length; i++)
            {
                var show = i < visible.Count;
                _lineRoots[i].gameObject.SetActive(show);
                if (!show || _lines[i] == null) continue;

                _lines[i].text = visible[i];
                _lines[i].color = visible[i].StartsWith("[OK]")
                    ? new Color(0.45f, 0.95f, 0.55f)
                    : V3PlaceholderFactory.UiText;
            }
        }
    }
}
