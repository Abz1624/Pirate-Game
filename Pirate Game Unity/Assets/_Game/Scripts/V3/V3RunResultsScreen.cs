using System.Text;

using HighSeas.Balance;

using HighSeas.Combat;

using HighSeas.Core;

using HighSeas.UI;

using TMPro;

using UnityEngine;

using UnityEngine.UI;



namespace HighSeas.V3

{

    /// <summary>V3 run results — objective vs death, Marks, unlock goal, retry/menu.</summary>

    public sealed class V3RunResultsScreen : MonoBehaviour

    {

        [SerializeField] GameObject panel;

        [SerializeField] TextMeshProUGUI headlineText;

        [SerializeField] TextMeshProUGUI summaryText;

        [SerializeField] Button continueButton;

        [SerializeField] Button menuButton;

        [SerializeField] Button selectGoalButton;

        [SerializeField] Button purchaseGoalButton;



        void Awake()

        {

            if (panel == null)

                BuildPlaceholderUi();

        }



        void Start()

        {

            if (continueButton != null)

                continueButton.onClick.AddListener(OnRestartClicked);

            if (menuButton != null)

                menuButton.onClick.AddListener(OnMenuClicked);

            if (selectGoalButton != null)

                selectGoalButton.onClick.AddListener(OnSelectGoalClicked);

            if (purchaseGoalButton != null)

                purchaseGoalButton.onClick.AddListener(OnPurchaseGoalClicked);

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



        void OnRestartClicked()

        {

            HidePanel();

            V3AuthoredGameplayHud.SetProfileVisible(true);

            if (!V3RunBootstrap.TryRequestRestart())

                AppRoot.Instance?.LoadScene(V3SceneNames.MainMenu);

        }



        void OnMenuClicked()

        {

            HidePanel();

            V3AuthoredGameplayHud.SetProfileVisible(true);

            AppRoot.Instance?.LoadScene(V3SceneNames.MainMenu);

        }



        void OnSelectGoalClicked()

        {

            var save = AppRoot.Instance?.Save;

            var export = StageBalanceProfileContext.Export;

            if (save == null || export == null) return;



            var next = SaltwindMarksUnlockLogic.GetNextLockedMarksWeapon(export, save);

            if (string.IsNullOrEmpty(next))

            {

                V3ToastHud.Instance?.Show("All Marks weapons owned.", 3f);

                return;

            }



            SaltwindMarksUnlockLogic.TrySetUnlockGoal(save, next);

            V3ToastHud.Instance?.Show($"Unlock goal: {V3WeaponDisplayNames.GetWeaponName(next)}", 3f);

            RefreshSummaryIfVisible();

        }



        void OnPurchaseGoalClicked()

        {

            var save = AppRoot.Instance?.Save;

            var export = StageBalanceProfileContext.Export;

            if (save == null || export == null) return;



            var goal = save.Meta.selectedUnlockGoal;

            if (string.IsNullOrEmpty(goal))

            {

                V3ToastHud.Instance?.Show("Select an unlock goal first.", 3f);

                return;

            }



            if (SaltwindMarksUnlockLogic.TryPurchaseUnlock(save, export, goal))

            {

                V3ToastHud.Instance?.Show($"Unlocked {V3WeaponDisplayNames.GetWeaponName(goal)}!", 4f);

                RefreshSummaryIfVisible();

                return;

            }



            V3ToastHud.Instance?.Show("Not enough Marks or already owned.", 3f);

        }



        void RefreshSummaryIfVisible()

        {

            if (panel == null || !panel.activeSelf || RunManager.Instance == null) return;

            if (summaryText != null)

                summaryText.text = BuildSummaryText(RunManager.Instance);

        }



        void OnStateChanged(RunState state)

        {

            if (state != RunState.RunComplete) return;

            V3AuthoredGameplayHud.SetProfileVisible(false);

            if (panel != null) panel.SetActive(true);



            var rm = RunManager.Instance;

            if (rm == null) return;



            var diedAfterVictory = rm.ObjectiveSecured && rm.LastRunEndedByDeath;

            if (headlineText != null)

                headlineText.text = SaltwindRunResultsLogic.BuildHeadline(rm.ObjectiveSecured, diedAfterVictory);



            if (summaryText != null)

                summaryText.text = BuildSummaryText(rm);



            RunCombatTelemetry.Instance?.RecordRunResultsShown(rm.ObjectiveSecured, SaltwindRunSettlement.LastMarksAwarded);

            RunCombatTelemetry.Instance?.WriteExportFile();

        }



        static string BuildSummaryText(RunManager rm)

        {

            var sb = new StringBuilder();

            sb.AppendLine(SaltwindRunResultsLogic.BuildOutcomeSubline(rm.ObjectiveSecured, rm.LastRunEndedByDeath));

            sb.AppendLine();

            sb.AppendLine(SaltwindRunResultsLogic.FormatProfileFooter(

                StageBalanceProfileContext.Export, rm.RunSeed, rm.ElapsedSeconds));

            sb.AppendLine($"Run gold earned: {rm.GrossGoldEarned}");

            sb.AppendLine($"Run gold spent: {rm.GrossGoldSpent}");

            sb.AppendLine($"Run gold remaining: {rm.Gold} (run-only)");



            if (StageBalanceProfileContext.IsActive)

            {

                var save = AppRoot.Instance?.Save;

                var treasury = save?.Meta.marks ?? 0;

                var awarded = SaltwindRunSettlement.LastMarksAwarded;

                if (SaltwindRunSettlement.LastSettlementApplied ||

                    awarded > 0 ||

                    SaltwindRunSettlement.LastBreakdown.TotalMarks > 0)

                {

                    sb.Append(SaltwindRunResultsLogic.FormatMarksBreakdownForPlayer(

                        SaltwindRunSettlement.LastBreakdown, awarded, treasury));

                }

                else

                {

                    sb.AppendLine("\nMarks: not settled this run.");

                }



                if (save != null)

                    sb.Append(SaltwindMarksUnlockLogic.FormatSelectedGoalProgress(save, StageBalanceProfileContext.Export));

            }



            sb.Append(BuildFinalLoadoutSection());

            sb.Append(BuildWeaponContributionSection());

            return sb.ToString();

        }



        static string BuildFinalLoadoutSection()

        {

            var levels = LevelSystem.Instance;

            if (levels == null || levels.OwnedWeapons.Count == 0) return string.Empty;



            var sb = new StringBuilder("\nFinal build:\n");

            for (var i = 0; i < levels.OwnedWeapons.Count; i++)

            {

                var id = levels.OwnedWeapons[i];

                sb.Append("  · ").Append(V3WeaponDisplayNames.GetWeaponName(id));

                sb.Append(" Lv ").Append(levels.GetWeaponLevel(id)).Append('\n');

            }



            return sb.ToString();

        }



        static string BuildWeaponContributionSection()

        {

            var lines = SaltwindRunResultsLogic.BuildWeaponContributionLines(WeaponDamageLedger.Snapshot());

            if (lines.Count == 0) return string.Empty;



            var sb = new StringBuilder("\nWeapon contribution:\n");

            for (var i = 0; i < lines.Count; i++)

                sb.Append("  · ").Append(lines[i].DisplayName).Append(": ")

                    .Append(lines[i].DamageSharePercent.ToString("0")).Append("% dmg\n");

            return sb.ToString();

        }



        public void HidePanel()

        {

            if (panel != null)

                panel.SetActive(false);

        }



        void BuildPlaceholderUi()

        {

            var canvas = GetComponentInParent<Canvas>();

            if (canvas == null) return;



            panel = V3PlaceholderFactory.CreateUiPanel(canvas.transform, "run_results_panel", new Vector2(560f, 520f),

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

            headlineRect.anchoredPosition = new Vector2(0f, -36f);

            headlineRect.sizeDelta = new Vector2(520f, 56f);

            headlineText = headlineGo.AddComponent<TextMeshProUGUI>();

            headlineText.alignment = TextAlignmentOptions.Center;

            headlineText.fontSize = 26;

            headlineText.text = "RESULTS";



            var bodyGo = new GameObject("PLACEHOLDER_v3_ui_run_results_body");

            bodyGo.transform.SetParent(panel.transform, false);

            var bodyRect = bodyGo.AddComponent<RectTransform>();

            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);

            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);

            bodyRect.anchoredPosition = new Vector2(0f, 40f);

            bodyRect.sizeDelta = new Vector2(520f, 300f);

            summaryText = bodyGo.AddComponent<TextMeshProUGUI>();

            summaryText.alignment = TextAlignmentOptions.TopLeft;

            summaryText.fontSize = 20;



            continueButton = V3PlaceholderFactory.CreateUiButton(panel.transform, "run_results_restart", "Retry voyage",

                new Vector2(160f, 44f), "Start a new voyage with a fresh layout.");

            var retryRect = continueButton.GetComponent<RectTransform>();

            retryRect.anchorMin = new Vector2(0.5f, 0f);

            retryRect.anchorMax = new Vector2(0.5f, 0f);

            retryRect.anchoredPosition = new Vector2(-180f, 28f);



            menuButton = V3PlaceholderFactory.CreateUiButton(panel.transform, "run_results_menu", "Main menu",

                new Vector2(160f, 44f), "Return to main menu.");

            var menuRect = menuButton.GetComponent<RectTransform>();

            menuRect.anchorMin = new Vector2(0.5f, 0f);

            menuRect.anchorMax = new Vector2(0.5f, 0f);

            menuRect.anchoredPosition = new Vector2(0f, 28f);



            selectGoalButton = V3PlaceholderFactory.CreateUiButton(panel.transform, "run_results_goal", "Set unlock goal",

                new Vector2(160f, 44f), "Choose next Marks unlock (does not spend Marks).");

            var goalRect = selectGoalButton.GetComponent<RectTransform>();

            goalRect.anchorMin = new Vector2(0.5f, 0f);

            goalRect.anchorMax = new Vector2(0.5f, 0f);

            goalRect.anchoredPosition = new Vector2(-90f, 78f);



            purchaseGoalButton = V3PlaceholderFactory.CreateUiButton(panel.transform, "run_results_buy", "Purchase goal",

                new Vector2(160f, 44f), "Spend Marks on selected unlock goal.");

            var buyRect = purchaseGoalButton.GetComponent<RectTransform>();

            buyRect.anchorMin = new Vector2(0.5f, 0f);

            buyRect.anchorMax = new Vector2(0.5f, 0f);

            buyRect.anchoredPosition = new Vector2(90f, 78f);

        }

    }

}


