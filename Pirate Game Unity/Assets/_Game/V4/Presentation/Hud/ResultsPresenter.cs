using System.Collections.Generic;
using System.Text;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Events;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The run results screen (M6): port of V3RunResultsScreen. Same panel, texts and buttons (Retry voyage, Main menu,
    /// Set unlock goal, Purchase goal) and the same summary (outcome, profile line, run gold, Rum breakdown, goal
    /// progress, final build, weapon contribution). Opens on the RunState event for RunComplete and reads the results
    /// view. Retry restarts the V4 run and Main menu loads the V4 menu. V3 had two copies of this screen in the scene,
    /// stacked (R-63); there is one now, on the run canvas where the top copy was.
    /// </summary>
    public sealed class ResultsPresenter : MonoBehaviour
    {
        GameObject _panel;
        GameObject _backdrop;
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
            if (_backdrop != null) _backdrop.SetActive(true);
            if (_panel != null) _panel.SetActive(true);
            var results = _views.Results;
            if (_headline != null) _headline.text = SaltwindRunResultsLogic.BuildHeadline(results.ObjectiveSecured, results.ObjectiveSecured && results.EndedByDeath);
            if (_summary != null) _summary.text = BuildSummary();
            if (_summaryScroll != null) _summaryScroll.verticalNormalizedPosition = 1f; // open at the top
        }

        public void Hide()
        {
            if (_backdrop != null) _backdrop.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// The results text (playtest round 1: reorganised for reading at a glance): outcome and a one-line run summary,
        /// the Rum earned (total only), the goal, gold, then the build with each weapon's level and damage share on one
        /// line; the profile line last.
        /// </summary>
        public string BuildSummary()
        {
            var r = _views.Results;
            var run = _views.Run;
            var sb = new StringBuilder();
            sb.AppendLine(SaltwindRunResultsLogic.BuildOutcomeSubline(r.ObjectiveSecured, r.EndedByDeath));
            if (run != null)
            {
                var t = Mathf.Max(0f, run.ElapsedSeconds);
                var level = _views.Progression != null ? _views.Progression.Level : 0;
                sb.AppendLine($"Survived {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00} · {run.Kills} kills · level {level}");
            }

            if (r.HasRewardSection)
            {
                var section = r.RewardSection;
                if (section != null) sb.Append(section);
                else sb.AppendLine("\nRUM EARNED  +0  (Rum is paid when the ship sinks)");
                sb.Append(r.GoalProgress);
            }

            sb.AppendLine($"\nGold: earned {r.GoldEarned} · spent {r.GoldSpent} · left {r.GoldRemaining}");

            r.CollectFinalBuild(_scratch);
            var build = new List<NamedValue>(_scratch);
            r.CollectContributions(_scratch);
            if (build.Count > 0 || _scratch.Count > 0)
            {
                sb.Append("\nBuild:\n");
                foreach (var w in build)
                {
                    sb.Append("  · ").Append(w.name).Append(" Lv ").Append((int)w.value);
                    var share = _scratch.Find(c => c.name == w.name);
                    if (share.name != null) sb.Append(" · ").Append(share.value.ToString("0")).Append("% dmg");
                    sb.Append('\n');
                }
                foreach (var c in _scratch)
                    if (!build.Exists(w => w.name == c.name)) sb.Append("  · ").Append(c.name).Append(" · ").Append(c.value.ToString("0")).Append("% dmg\n");
            }
            sb.Append('\n').AppendLine(r.ProfileFooter);
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
            // Playtest (3 Oct, owner): the end screen looked broken (the art HUD hides, stray widgets stayed on the sea).
            // A full-screen dim now sits behind an opaque panel, so the screen reads as one intentional results page.
            _backdrop = new GameObject("run_results_backdrop", typeof(RectTransform), typeof(Image));
            _backdrop.transform.SetParent(transform, false);
            var br = (RectTransform)_backdrop.transform;
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = br.offsetMax = Vector2.zero;
            _backdrop.GetComponent<Image>().color = new Color(0.01f, 0.03f, 0.06f, 0.78f);
            _backdrop.SetActive(false);

            _panel = PlaceholderFactory.CreateUiPanel(transform, "run_results_panel", new Vector2(560f, 600f),
                "Full-screen run results layout: replace with production UI art.");
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.11f, 0.17f, 0.97f);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            _headline = CreateText("PLACEHOLDER_v3_ui_run_results_headline", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(520f, 56f), 26, TextAlignmentOptions.Center);
            _headline.text = "RESULTS";
            _headline.fontStyle = FontStyles.Bold;
            _headline.color = new Color(1f, 0.82f, 0.4f);
            _summary = CreateScrollingBody();

            // Playtest (3 Oct, owner): only Retry Voyage and Main Menu; the unlock goal buttons stay hidden.
            _retry = CreateButton("run_results_restart", "Retry Voyage", "Start a new voyage with a fresh layout.", new Vector2(-115f, 12f), OnRetryClicked);
            _menu = CreateButton("run_results_menu", "Main Menu", "Return to main menu.", new Vector2(115f, 12f), OnMenuClicked);
            _selectGoal = CreateButton("run_results_goal", "Set unlock goal", "Choose next Rum unlock (does not spend Rum).", new Vector2(-115f, 96f), OnSelectGoalClicked);
            _purchaseGoal = CreateButton("run_results_buy", "Purchase goal", "Spend Rum on selected unlock goal.", new Vector2(115f, 96f), OnPurchaseGoalClicked);
            _selectGoal.gameObject.SetActive(false);
            _purchaseGoal.gameObject.SetActive(false);
            _panel.SetActive(false);
        }

        /// <summary>
        /// R-92 fix (approved at the M6 playtest): V3's body (520x300 at the panel centre) ran below the panel and under
        /// the buttons once the Rum breakdown, build and weapon lines were listed. The body now fills the space between
        /// the headline and the two button rows and scrolls (wheel or drag) when the text is longer.
        /// </summary>
        TextMeshProUGUI CreateScrollingBody()
        {
            const float top = 68f;     // below the headline (36 + 56/2 + 4)
            const float bottom = 92f; // above the button row (72-unit buttons from 12, + 8; the goal row is hidden)
            var viewport = new GameObject("run_results_body_viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            viewport.transform.SetParent(_panel.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = new Vector2(0f, 0f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(20f, bottom);
            vr.offsetMax = new Vector2(-20f, -top);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // raycast target for wheel/drag

            var go = new GameObject("PLACEHOLDER_v3_ui_run_results_body", typeof(RectTransform));
            go.transform.SetParent(viewport.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 0f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 21;
            text.color = new Color(0.94f, 0.95f, 0.97f);
            text.raycastTarget = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.content = rect;
            scroll.viewport = vr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            _summaryScroll = scroll;
            return text;
        }

        ScrollRect _summaryScroll;

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
            var button = PlaceholderFactory.CreateUiButton(_panel.transform, id, label, new Vector2(220f, 72f), hint);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f); // V5 M7: the position is the button's bottom edge
            rect.anchoredPosition = position;
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
