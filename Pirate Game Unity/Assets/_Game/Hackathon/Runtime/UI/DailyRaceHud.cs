using System;
using RoguePirates.V4;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the daily island race's run. The menu's Daily Race panel asks for it (<see cref="Begin"/>) and
    /// starts a voyage on the day's seed (V4's requested-seed path); this HUD (added when that run starts, V4's
    /// <see cref="V4Game.RunStarted"/>) shows the goal, the kills and the race clock (the run's own time, so pauses do not
    /// count). At the target kills the time is posted to POST /api/race/daily/entry and the rank shown. Hackathon playtest
    /// (12 Oct, owner): the voyage then ends (<see cref="RunLifecycle.CompleteGoalRun"/>) and a race results panel shows the
    /// time, the rank and the way back to port.
    /// </summary>
    public sealed class DailyRaceHud : MonoBehaviour
    {
        static DailyRace _pending;

        /// <summary>The next voyage is the race on <paramref name="race"/>'s seed.</summary>
        public static void Begin(DailyRace race)
        {
            _pending = race;
            RunRequest.ClearPvp();
            RunRequest.RequestSeed(race.seed);
        }

        /// <summary>The last race run's result (the menu panel shows it).</summary>
        public static string LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pending = null;
            LastResult = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRun;
            V4Game.RunStarted += OnRun;
        }

        static void OnRun(V4Game game)
        {
            var race = _pending;
            _pending = null;
            if (race == null || game == null || game.IsPvp || game.RunLog == null) return;
            // The race only counts on its own seed (a retry plays a new seed).
            if (game.RunLog.Seed != null && game.RunLog.Seed() != race.seed) { Debug.LogWarning("DailyRaceHud: the run is not on the race seed."); return; }
            var hud = game.gameObject.AddComponent<DailyRaceHud>();
            hud._game = game;
            hud._race = race;
            hud.Build();
        }

        V4Game _game;
        DailyRace _race;
        TextMeshProUGUI _goal, _clock, _result, _panelText;
        RectTransform _panel;
        int _rumSettled;
        bool _finished, _posting;
        double _finishTime;

        public bool Finished => _finished;
        public double FinishTime => _finishTime;
        public string ResultText => _result != null ? _result.text : null;
        public bool ResultsShowing => _panel != null && _panel.gameObject.activeSelf;
        public string PanelText => _panelText != null ? _panelText.text : null;
        public int Target => _race != null && _race.targetKills > 0 ? _race.targetKills : 100;

        void Build()
        {
            var go = new GameObject("HackathonRaceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var box = new GameObject("Race", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(go.transform, false);
            var rt = (RectTransform)box.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -146f);
            rt.sizeDelta = new Vector2(620f, 104f);
            var img = box.GetComponent<Image>();
            img.color = new Color(0.06f, 0.1f, 0.16f, 0.82f);
            img.raycastTarget = false;
            _goal = HackUi.TextAt(rt, "Goal", string.Empty, 24, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.62f), Vector2.one, true);
            _clock = HackUi.TextAt(rt, "Clock", string.Empty, 34, TextAlignmentOptions.Center, HackUi.TextColor, new Vector2(0f, 0.2f), new Vector2(1f, 0.66f));
            _clock.fontStyle = FontStyles.Bold;
            _result = HackUi.TextAt(rt, "Result", string.Empty, 20, TextAlignmentOptions.Center, HackUi.Dim, Vector2.zero, new Vector2(1f, 0.26f));
            _goal.text = $"DAILY RACE {_race.date}   Reach {Target} kills";
            _result.text = "Fastest captain today wins the board";

            // The race results (shown when the goal is reached; the voyage ends there). A full-screen backdrop over the
            // run's own results screen.
            var back = new GameObject("RaceResults", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(go.transform, false);
            _panel = (RectTransform)back.transform;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            back.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_panel, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(900f, 420f);
            card.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.97f);
            HackUi.TextAt(crt, "Title", "Race complete!", 52, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.74f), new Vector2(1f, 0.96f), true);
            _panelText = HackUi.TextAt(crt, "Text", string.Empty, 30, TextAlignmentOptions.Top, HackUi.TextColor, new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.74f));
            _panelText.textWrappingMode = TextWrappingModes.Normal;
            HackUi.Button(crt, "Return to port", () => V4Game.GoToMenu(), new Vector2(0.27f, 0.05f), new Vector2(0.73f, 0.22f));
            _panel.gameObject.SetActive(false);
        }

        void ShowResults()
        {
            if (_panel == null) return;
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            RefreshPanel();
        }

        void RefreshPanel()
        {
            if (_panelText == null) return;
            var text = $"You reached {Target} kills in <color=#ffd166>{HackathonRules.RaceTime(_finishTime)}</color>.\n{_result.text}";
            if (_rumSettled > 0) text += $"\nRUM earned this voyage: +{_rumSettled}";
            _panelText.text = text;
        }

        void Update()
        {
            var log = _game != null ? _game.RunLog : null;
            if (log == null || _clock == null) return;
            var kills = log.Kills != null ? log.Kills() : 0;
            var time = log.RunTime != null ? log.RunTime() : 0f;
            if (!_finished && HackathonRules.RaceFinished(kills, Target))
            {
                _finished = true;
                _finishTime = Math.Round(time, 1);
                Post(kills, log.Level != null ? log.Level() : 1);
                // The goal is reached: the voyage ends here with the race results.
                if (_game.Lifecycle != null) _rumSettled = _game.Lifecycle.CompleteGoalRun();
                ShowResults();
            }
            _clock.text = _finished
                ? $"<color=#7fd68b>FINISHED</color>  {HackathonRules.RaceTime(_finishTime)}"
                : $"Kills <color=#ffd166>{Math.Min(kills, Target)}/{Target}</color>   {HackathonRules.RaceTime(time)}";
        }

        async void Post(int kills, int level)
        {
            if (_posting) return;
            _posting = true;
            _result.text = "Posting your time...";
            _result.color = HackUi.Gold;
            try
            {
                var (data, error) = await HackathonApi.SubmitRace(new RaceEntryRequest
                {
                    date = _race.date, seed = _race.seed, timeSeconds = _finishTime, kills = kills, level = level,
                    clientVersion = Application.version,
                });
                if (this == null) return;
                if (error != null)
                {
                    _result.text = "Not posted: " + error;
                    _result.color = HackUi.Bad;
                    RefreshPanel();
                    LastResult = $"Your race: {HackathonRules.RaceTime(_finishTime)} (not posted: {error})";
                    return;
                }
                var best = data.best;
                var rank = best != null && best.rank > 0 ? $"Rank #{best.rank}" : "Posted";
                _result.text = data.improved
                    ? $"{rank} on today's board. New personal best!"
                    : $"{rank}. Your best stays {HackathonRules.RaceTime(best != null ? best.timeSeconds : _finishTime)}.";
                _result.color = HackUi.Good;
                RefreshPanel();
                LastResult = $"Your race: {HackathonRules.RaceTime(_finishTime)}  ({_result.text})";
            }
            catch (Exception e)
            {
                if (this != null) { _result.text = "Not posted: " + e.Message; _result.color = HackUi.Bad; RefreshPanel(); }
            }
        }
    }
}
