using System.Collections.Generic;
using System.Text;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.V3;
using RoguePirates.V4.Events;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The run results screen (M6): port of V3RunResultsScreen. Same panel, texts and buttons (Retry voyage, Main menu,
    /// Set unlock goal, Purchase goal) and the same summary (outcome, profile line, run gold, Marks breakdown, goal
    /// progress, final build, weapon contribution). Opens on the RunState event for RunComplete and reads the results
    /// view. Retry restarts the V4 run and Main menu loads the V4 menu. V3 had two copies of this screen in the scene,
    /// stacked (R-63); there is one now, on the run canvas where the top copy was.
    /// </summary>
    public sealed class ResultsPresenter : MonoBehaviour
    {
        GameObject _panel;
        TextMeshProUGUI _headline;
        TextMeshProUGUI _summary;
        Button _retry;
        Button _menu;
        Button _selectGoal;
        Button _purchaseGoal;
        IRunViews _views;
        ICombatEventReader _events;
        AuthoredHudPresenter _hud;
        readonly List<NamedValue> _scratch = new List<NamedValue>();

        public bool IsShowing => _panel != null && _panel.activeSelf;
        public string SummaryText => _summary != null ? _summary.text : null;
        public string HeadlineText => _headline != null ? _headline.text : null;
        public Button RetryButton => _retry;
        public Button MenuButton => _menu;

        public static ResultsPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<ResultsPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4Results", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<ResultsPresenter>();
            presenter.BuildUi(canvas);
            return presenter;
        }

        public void Bind(IRunViews views, ICombatEventReader events, AuthoredHudPresenter hud)
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _views = views;
            _events = events;
            _hud = hud;
            if (_events != null) _events.TickPublished += OnEvents;
        }

        void OnDestroy()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            for (var i = 0; i < batch.Count; i++)
                if (batch[i].Kind == CombatEventKind.RunState && batch[i].A == (int)RunState.RunComplete) Show();
        }

        public void Show()
        {
            if (_views?.Results == null) return;
            if (_hud != null) _hud.SetProfileVisible(false);
            if (_panel != null) _panel.SetActive(true);
            var results = _views.Results;
            if (_headline != null) _headline.text = SaltwindRunResultsLogic.BuildHeadline(results.ObjectiveSecured, results.ObjectiveSecured && results.EndedByDeath);
            if (_summary != null) _summary.text = BuildSummary();
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>V3RunResultsScreen.BuildSummaryText, line for line.</summary>
        public string BuildSummary()
        {
            var r = _views.Results;
            var sb = new StringBuilder();
            sb.AppendLine(SaltwindRunResultsLogic.BuildOutcomeSubline(r.ObjectiveSecured, r.EndedByDeath));
            sb.AppendLine();
            sb.AppendLine(r.ProfileFooter);
            sb.AppendLine($"Run gold earned: {r.GoldEarned}");
            sb.AppendLine($"Run gold spent: {r.GoldSpent}");
            sb.AppendLine($"Run gold remaining: {r.GoldRemaining} (run-only)");

            if (r.HasRewardSection)
            {
                var section = r.RewardSection;
                if (section != null) sb.Append(section);
                else sb.AppendLine("\nMarks: not settled this run.");
                sb.Append(r.GoalProgress);
            }

            r.CollectFinalBuild(_scratch);
            if (_scratch.Count > 0)
            {
                sb.Append("\nFinal build:\n");
                foreach (var w in _scratch) sb.Append("  · ").Append(w.name).Append(" Lv ").Append((int)w.value).Append('\n');
            }

            r.CollectContributions(_scratch);
            if (_scratch.Count > 0)
            {
                sb.Append("\nWeapon contribution:\n");
                foreach (var w in _scratch) sb.Append("  · ").Append(w.name).Append(": ").Append(w.value.ToString("0")).Append("% dmg\n");
            }
            return sb.ToString();
        }

        void OnRetryClicked()
        {
            Hide();
            if (_hud != null) _hud.SetProfileVisible(true);
            _views?.Commands?.Retry();
        }

        void OnMenuClicked()
        {
            Hide();
            if (_hud != null) _hud.SetProfileVisible(true);
            _views?.Commands?.GoToMenu();
        }

        void OnSelectGoalClicked() => ShowMessage(_views?.Commands?.SelectNextUnlockGoal(), 3f);

        void OnPurchaseGoalClicked()
        {
            var message = _views?.Commands?.PurchaseUnlockGoal();
            ShowMessage(message, message != null && message.StartsWith("Unlocked") ? 4f : 3f);
        }

        void ShowMessage(string message, float seconds)
        {
            if (string.IsNullOrEmpty(message)) return;
            _views?.Toasts?.Show(message, seconds);
            if (IsShowing && _summary != null) _summary.text = BuildSummary();
        }

        void BuildUi(Transform canvas)
        {
            _panel = V3PlaceholderFactory.CreateUiPanel(transform, "run_results_panel", new Vector2(560f, 520f),
                "Full-screen run results layout — replace with production UI art.");
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            _headline = CreateText("PLACEHOLDER_v3_ui_run_results_headline", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(520f, 56f), 26, TextAlignmentOptions.Center);
            _headline.text = "RESULTS";
            _summary = CreateText("PLACEHOLDER_v3_ui_run_results_body", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(520f, 300f), 20, TextAlignmentOptions.TopLeft);

            _retry = CreateButton("run_results_restart", "Retry voyage", "Start a new voyage with a fresh layout.", new Vector2(-180f, 28f), OnRetryClicked);
            _menu = CreateButton("run_results_menu", "Main menu", "Return to main menu.", new Vector2(0f, 28f), OnMenuClicked);
            _selectGoal = CreateButton("run_results_goal", "Set unlock goal", "Choose next Marks unlock (does not spend Marks).", new Vector2(-90f, 78f), OnSelectGoalClicked);
            _purchaseGoal = CreateButton("run_results_buy", "Purchase goal", "Spend Marks on selected unlock goal.", new Vector2(90f, 78f), OnPurchaseGoalClicked);
            _panel.SetActive(false);
        }

        TextMeshProUGUI CreateText(string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_panel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.fontSize = fontSize;
            return text;
        }

        Button CreateButton(string id, string label, string hint, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var button = V3PlaceholderFactory.CreateUiButton(_panel.transform, id, label, new Vector2(160f, 44f), hint);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
