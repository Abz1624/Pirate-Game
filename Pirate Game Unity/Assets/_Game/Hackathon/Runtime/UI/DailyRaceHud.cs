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
    /// (12 Oct, owner): the voyage then ends (<see cref="RunLifecycle.CompleteGoalRun"/>, through the run's IHudCommands.CompleteGoal) and a race results panel shows the
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

            // Hackathon UI (13 Oct): the race strip and the results card on the parchment kit.
            var rt = BoardKit.Node(go.transform, "Race");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -146f);
            rt.sizeDelta = new Vector2(660f, 128f);
            BoardKit.Paint(rt, "Panel_Card", 2.2f, new Color(1f, 1f, 1f, 0.94f), new Color(0.95f, 0.88f, 0.72f, 0.92f));
            BoardKit.Picture(BoardKit.TopLeft(rt, "Clock", 18f, 30f, 64f, 64f), "Icon_Clock", BoardKit.Ink);
            _goal = BoardKit.Label(BoardKit.Strip(rt, "Goal", 90f, 10f, 20f, 30f), string.Empty, 22f, BoardKit.InkGold, TextAlignmentOptions.Center, true, 14f);
            _clock = BoardKit.Label(BoardKit.Strip(rt, "Clock", 90f, 38f, 20f, 42f), string.Empty, 34f, BoardKit.Ink, TextAlignmentOptions.Center, true, 20f);
            _result = BoardKit.Label(BoardKit.Strip(rt, "Result", 90f, 82f, 20f, 28f), string.Empty, 19f, BoardKit.InkDim, TextAlignmentOptions.Center, false, 12f);
            _goal.text = $"DAILY RACE {_race.date}   Reach {Target} kills";
            _result.text = "Fastest captain today wins the board";

            // The race results (shown when the goal is reached; the voyage ends there). A full-screen backdrop over the
            // run's own results screen.
            _panel = BoardKit.Fill(go.transform, "RaceResults");
            var back = _panel.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.6f);
            var crt = BoardKit.Node(_panel, "Card");
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1060f, 620f);
            var c = BoardKit.ScrollSheet(crt, 110f, 50f, 50f);
            BoardKit.Medallion(c, 0f, 4f, 116f);
            BoardKit.Label(BoardKit.Strip(c, "Title", 130f, 14f, 20f, 90f), "RACE COMPLETE!", 64f, BoardKit.Ink, TextAlignmentOptions.MidlineLeft, true, 36f);
            BoardKit.Line(c, 0f, 134f, 0f);
            BoardKit.Picture(BoardKit.TopLeft(c, "Trophy", 10f, 160f, 120f, 120f), "Icon_Trophy", BoardKit.InkGold);
            _panelText = BoardKit.Label(BoardKit.Fill(c, "Text", 150f, 120f, 10f, 150f), string.Empty, 32f, BoardKit.Ink, TextAlignmentOptions.TopLeft, false, 18f);
            _panelText.textWrappingMode = TextWrappingModes.Normal;
            var home = BoardKit.BottomStrip(c, "Return", 0f, 4f, 0f, 96f);
            home.anchorMin = new Vector2(0.5f, 0f);
            home.anchorMax = new Vector2(0.5f, 0f);
            home.sizeDelta = new Vector2(460f, 96f);
            home.anchoredPosition = new Vector2(0f, 4f);
            BoardKit.KitButton(home, "Return to port", "Icon_House", () => V4Game.GoToMenu(), "Btn_Gold", 36f, 48f);
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
            var text = $"You reached {Target} kills in <b><color={BoardKit.GoldHex}>{HackathonRules.RaceTime(_finishTime)}</color></b>.\n{_result.text}";
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
                // 13 Oct (review): through the run's command (its normal end path), not a write into the run.
                if (_game.Context != null && _game.Context.TryGet<RoguePirates.V4.Views.IRunViews>(out var views) && views?.Commands != null)
                    _rumSettled = views.Commands.CompleteGoal("daily_race");
                ShowResults();
            }
            _clock.text = _finished
                ? $"<color={BoardKit.GoodHex}>FINISHED</color>  {HackathonRules.RaceTime(_finishTime)}"
                : $"Kills <color={BoardKit.GoldHex}>{Math.Min(kills, Target)}/{Target}</color>   {HackathonRules.RaceTime(time)}";
        }

        async void Post(int kills, int level)
        {
            if (_posting) return;
            _posting = true;
            _result.text = "Posting your time...";
            _result.color = BoardKit.InkGold;
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
                    _result.color = BoardKit.InkBad;
                    RefreshPanel();
                    LastResult = $"Your race: {HackathonRules.RaceTime(_finishTime)} (not posted: {error})";
                    return;
                }
                var best = data.best;
                var rank = best != null && best.rank > 0 ? $"Rank #{best.rank}" : "Posted";
                _result.text = data.improved
                    ? $"{rank} on today's board. New personal best!"
                    : $"{rank}. Your best stays {HackathonRules.RaceTime(best != null ? best.timeSeconds : _finishTime)}.";
                _result.color = BoardKit.InkGood;
                RefreshPanel();
                LastResult = $"Your race: {HackathonRules.RaceTime(_finishTime)}  ({_result.text})";
            }
            catch (Exception e)
            {
                if (this != null) { _result.text = "Not posted: " + e.Message; _result.color = BoardKit.InkBad; RefreshPanel(); }
            }
        }
    }
}
