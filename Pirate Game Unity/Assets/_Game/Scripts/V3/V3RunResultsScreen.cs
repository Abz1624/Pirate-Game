using HighSeas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>V3 run results — distinguishes objective secured from eventual death.</summary>
    public sealed class V3RunResultsScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI headlineText;
        [SerializeField] TextMeshProUGUI summaryText;
        [SerializeField] Button continueButton;

        void Awake()
        {
            if (panel == null)
                BuildPlaceholderUi();
        }

        void Start()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(() => AppRoot.Instance?.LoadScene(V3SceneNames.MainMenu));
            if (RunManager.Instance != null)
                RunManager.Instance.StateChanged += OnStateChanged;
            if (panel != null)
                panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (RunManager.Instance != null)
                RunManager.Instance.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(RunState state)
        {
            if (state != RunState.RunComplete) return;
            if (panel != null) panel.SetActive(true);
            if (headlineText != null)
            {
                headlineText.text = RunManager.Instance.ObjectiveSecured
                    ? "TERRITORY OBJECTIVE SECURED"
                    : "RUN ENDED";
            }

            if (summaryText != null && RunManager.Instance != null)
            {
                var rm = RunManager.Instance;
                var homePreview = rm.ObjectiveSecured
                    ? "\nFirst-home preview: Ledger rank 1 (local prototype — not saved to account)."
                    : "";
                summaryText.text =
                    $"Seed: {rm.RunSeed}\n" +
                    $"Survived: {rm.ElapsedSeconds:0}s\n" +
                    $"Kills: {rm.KillCount}\n" +
                    $"Boss secured: {(rm.ObjectiveSecured ? "Yes" : "No")}\n" +
                    $"Run gold banked: {rm.BankedRunGold}\n" +
                    $"Unspent run gold: {rm.Gold}" +
                    homePreview;
            }
        }

        void BuildPlaceholderUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            panel = V3PlaceholderFactory.CreateUiPanel(canvas.transform, "run_results_panel", new Vector2(520f, 420f),
                "Full-screen run results layout — replace with production UI art.");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            var headlineGo = new GameObject("PLACEHOLDER_v3_ui_run_results_headline");
            headlineGo.transform.SetParent(panel.transform, false);
            var headlineRect = headlineGo.AddComponent<RectTransform>();
            headlineRect.anchorMin = new Vector2(0.5f, 1f);
            headlineRect.anchorMax = new Vector2(0.5f, 1f);
            headlineRect.anchoredPosition = new Vector2(0f, -40f);
            headlineRect.sizeDelta = new Vector2(480f, 48f);
            headlineText = headlineGo.AddComponent<TextMeshProUGUI>();
            headlineText.alignment = TextAlignmentOptions.Center;
            headlineText.fontSize = 28;
            headlineText.text = "RESULTS";

            var bodyGo = new GameObject("PLACEHOLDER_v3_ui_run_results_body");
            bodyGo.transform.SetParent(panel.transform, false);
            var bodyRect = bodyGo.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 20f);
            bodyRect.sizeDelta = new Vector2(460f, 240f);
            summaryText = bodyGo.AddComponent<TextMeshProUGUI>();
            summaryText.alignment = TextAlignmentOptions.TopLeft;
            summaryText.fontSize = 22;

            continueButton = V3PlaceholderFactory.CreateUiButton(panel.transform, "run_results_continue", "Return to Port",
                new Vector2(220f, 52f), "Main menu return button.");
            var btnRect = continueButton.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 36f);
        }
    }
}
