using HighSeas.Core;
using HighSeas.Data;
using HighSeas.POI;
using TMPro;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Captain, beacon lock, chest progress and objective-secured banner.</summary>
    public sealed class V3ObjectiveHud : MonoBehaviour
    {
        TextMeshProUGUI _captainText;
        TextMeshProUGUI _beaconText;
        TextMeshProUGUI _chestText;
        TextMeshProUGUI _objectiveText;
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
            rect.anchoredPosition = new Vector2(-16f, -16f);
            rect.sizeDelta = new Vector2(300f, 132f);

            var panel = root.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0.05f, 0.08f, 0.14f, 0.86f);

            var hud = root.AddComponent<V3ObjectiveHud>();
            hud._captainText = CreateLine(root.transform, "CaptainLine", new Vector2(0f, -12f));
            hud._beaconText = CreateLine(root.transform, "BeaconLine", new Vector2(0f, -40f));
            hud._chestText = CreateLine(root.transform, "ChestLine", new Vector2(0f, -68f));
            hud._objectiveText = CreateLine(root.transform, "ObjectiveLine", new Vector2(0f, -96f));
            hud._pacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            return hud;
        }

        static TextMeshProUGUI CreateLine(Transform parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(-16f, 24f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = V3PlaceholderFactory.UiText;
            return text;
        }

        void Update()
        {
            if (RunManager.Instance == null) return;

            var profile = V3CaptainProfile.Get(V3RunContext.CaptainId);
            if (_captainText != null)
                _captainText.text = profile.DisplayName;

            var minBeacon = _pacing != null ? _pacing.bossBeaconMinSeconds : 180f;
            var beaconReady = RunManager.Instance.ElapsedSeconds >= minBeacon;
            if (_beaconText != null)
                _beaconText.text = beaconReady ? "Beacon: READY" : $"Beacon: {FormatTime(minBeacon - RunManager.Instance.ElapsedSeconds)}";

            var poi = FindFirstObjectByType<POIManager>();
            var opened = poi != null ? poi.PaidChestCount : 0;
            if (_chestText != null)
                _chestText.text = $"Chests: {opened} / {POIManager.V3SalvageChestCount}";

            if (_objectiveText != null)
            {
                _objectiveText.text = RunManager.Instance.ObjectiveSecured
                    ? "OBJECTIVE SECURED"
                    : "Secure the Treasury Beacon";
                _objectiveText.color = RunManager.Instance.ObjectiveSecured
                    ? new Color(0.45f, 0.95f, 0.55f)
                    : V3PlaceholderFactory.UiText;
            }
        }

        static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var mins = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{mins:00}:{secs:00}";
        }
    }
}
