using System;
using System.Collections.Generic;
using System.Text;
using RoguePirates.UI;
using RoguePirates.V4;
using RoguePirates.V4.Online;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the island conquest raid's voyage. The Islands screen starts a raid (POST /api/raids/start) and
    /// asks for it (<see cref="Begin"/>): a voyage on the design's seed where the island's designed defence replaces the
    /// ambient waves (<see cref="RaidSchedule"/>: each one-minute window's enemies spread over its minute) and the guardian
    /// (the Barnacle King) rises at its minute. Sinking the guardian conquers the island; running out of time (16:00), sinking
    /// or leaving reports how far the raid got and the server decides (partial = reached the guardian's minute). The result
    /// goes to POST /api/raids/{id}/result; the HUD then shows the loot and the defender's new shield. The ship, weapons and
    /// level-ups are the normal run's. Captain Marrow taunts along the way.
    /// </summary>
    public sealed class RaidHud : MonoBehaviour
    {
        static RaidStart _pending;
        static float _pendingAt;

        /// <summary>The next voyage is the raid <paramref name="raid"/> (its designed defence replaces the waves).</summary>
        public static void Begin(RaidStart raid)
        {
            _pending = raid;
            _pendingAt = Time.realtimeSinceStartup;
            RunRequest.ClearPvp();
            var defence = raid.defence;
            var seed = defence != null && defence.seed != 0 ? defence.seed : raid.defender != null && raid.defender.seed != 0 ? raid.defender.seed : raid.GetHashCode();
            RunRequest.RequestSeed(seed);
            Arm(defence, raid.practice);
        }

        /// <summary>Requests the next run's raid schedule from a raid's design (also used by tests). The run takes it when it
        /// starts (<see cref="RunRequest.Consume"/>), so it never outlives that one run.</summary>
        public static void Arm(RaidDefence defence, bool practice = false)
        {
            var design = DefenceRules.Normalize(defence?.design);
            var window = defence != null && defence.windowSeconds > 0 ? defence.windowSeconds : DefenceRules.WindowSeconds;
            var entries = new List<RaidSchedule.Entry>();
            foreach (var w in design.windows)
                if (w.enemies != null)
                    foreach (var e in w.enemies)
                        entries.Add(new RaidSchedule.Entry { StartSecond = (w.minute - 1) * window, Kind = e.kind, Count = e.count });
            var guardianAt = defence?.design?.guardian != null && defence.design.guardian.arrivesAtSecond > 0
                ? defence.design.guardian.arrivesAtSecond : (design.guardian.minute - 1) * window;
            RunRequest.RequestRaid(new RaidSchedule.Plan(entries, guardianAt, window, practice));
        }

        /// <summary>The last raid's result line (the Islands screen shows it).</summary>
        public static string LastResult { get; private set; }
        public static RaidResult LastRaid { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pending = null;
            LastResult = null;
            LastRaid = null;
            PracticeActive = false;
        }

        /// <summary>The voyage running now is a practice raid (owner, 12 Oct: it pays no Rum at all).</summary>
        public static bool PracticeActive { get; private set; }

        /// <summary>The results line of a practice raid (it is not sent to the leaderboard).</summary>
        public const string PracticeRunLine = "Practice raid: not ranked, no RUM.";

        /// <summary>The run submitter's skip hook: the practice line during a practice raid, else null (submit).</summary>
        public static string SkipLine() => PracticeActive ? PracticeRunLine : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRun;
            V4Game.RunStarted += OnRun;
            RoguePirates.V4.Presentation.Online.RunSubmitter.SkipReason = SkipLine;
        }

        /// <summary>
        /// A practice raid pays no Rum (owner, 12 Oct): the voyage's own Rum settlement is off (the reward module's no-Rum
        /// mode, as in a 1v1) and the run is not submitted (no server RUM, no weekly rank). A real raid or a normal voyage
        /// switches both back on.
        /// </summary>
        public static void ApplyPracticeRules(RoguePirates.V4.Rewards.RewardModule rewards, bool practice)
        {
            PracticeActive = practice;
            if (practice && rewards != null) rewards.LabMode = true;
        }

        static void OnRun(V4Game game)
        {
            var raid = _pending;
            _pending = null;
            // The run consumed its request before this event: it is a raid only when it took a raid schedule.
            if (raid == null || game == null || game.IsPvp || game.RunLog == null || !RaidSchedule.Active)
            {
                if (raid != null) Debug.LogWarning("RaidHud: the run did not start as the raid; it plays as a normal voyage.");
                RaidSchedule.Clear(); // never let a schedule reach a normal voyage
                PracticeActive = false;
                return;
            }
            RoguePirates.V4.Rewards.IRumView rumView = null;
            if (game.Context != null) game.Context.TryGet(out rumView);
            ApplyPracticeRules(rumView as RoguePirates.V4.Rewards.RewardModule, RaidSchedule.Practice);
            var hud = game.gameObject.AddComponent<RaidHud>();
            hud._game = game;
            hud._raid = raid;
            hud._wallStart = _pendingAt;
            hud.Build();
        }

        V4Game _game;
        RaidStart _raid;
        DefenceDesign _design;
        float _wallStart;
        TextMeshProUGUI _goal, _guardian, _clock, _wave, _bark, _resultTitle, _resultText;
        RectTransform _barkBox, _resultBox, _hullBack;
        Image _hullFill;
        IBossControl _bosses;
        float _barkUntil;
        bool _ended, _reported, _guardianUp, _saidWarn, _saidHalfHull, _saidLate;
        int _lastMinute;
        double _raidSeconds;
        float _guardianHull = -1f;

        public bool Ended => _ended;
        public bool Reported => _reported;
        public string ResultText => _resultText != null ? _resultText.text : null;
        public string ClockText => _clock != null ? _clock.text : null;
        public string WaveText => _wave != null ? _wave.text : null;
        public RaidStart Raid => _raid;
        public int Minute => DefenceRules.MinuteAt(_raidSeconds);
        public int GuardianMinute => _design?.guardian != null ? _design.guardian.minute : 5;
        public float GuardianSeconds => RaidSchedule.Active ? RaidSchedule.GuardianSeconds : (GuardianMinute - 1) * 60f;
        public float GuardianHull => _guardianHull;
        int TimeLimit => _raid.defence != null && _raid.defence.timeLimitSeconds > 0 ? _raid.defence.timeLimitSeconds : 960;

        void Build()
        {
            _design = DefenceRules.Normalize(_raid.defence?.design);
            if (_game != null && _game.Context != null) _game.Context.TryGet(out _bosses);
            var go = new GameObject("HackathonRaidCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 515;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Hackathon UI (13 Oct): the raid strip, Marrow's barks and the result card on the parchment kit (as the race HUD).
            var box = UiKit.Node(go.transform, "Raid");
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -146f);
            box.sizeDelta = new Vector2(900f, 196f);
            UiKit.Paint(box, "Panel_Card", 2.2f, new Color(1f, 1f, 1f, 0.94f), new Color(0.95f, 0.88f, 0.72f, 0.92f));
            UiKit.Picture(UiKit.TopLeft(box, "Icon", 22f, 56f, 76f, 76f), "Icon_Skull_Crossbones", UiKit.Ink);
            _goal = UiKit.Label(UiKit.Strip(box, "Goal", 108f, 14f, 30f, 32f), string.Empty, 24f, UiKit.InkGold, TextAlignmentOptions.Center, true, 15f);
            // Always visible (owner, 12 Oct): when the guardian rises (no beacon in a raid), with a live countdown.
            _guardian = UiKit.Label(UiKit.Strip(box, "Guardian", 108f, 48f, 30f, 34f), string.Empty, 24f, UiKit.InkBad, TextAlignmentOptions.Center, false, 15f);
            _clock = UiKit.Label(UiKit.Strip(box, "Clock", 108f, 84f, 30f, 50f), string.Empty, 34f, UiKit.Ink, TextAlignmentOptions.Center, true, 20f);
            _wave = UiKit.Label(UiKit.Strip(box, "Wave", 108f, 136f, 30f, 44f), string.Empty, 21f, UiKit.InkDim, TextAlignmentOptions.Center, false, 14f);
            _wave.textWrappingMode = TextWrappingModes.Normal;
            _goal.text = _raid.practice ? $"PRACTICE: TEST {_raid.islandName}'S DEFENCES   no fee, no loot" : $"CONQUER {_raid.islandName}   sink the guardian";
            // The guardian's hull, under the strip once it is up.
            _hullBack = UiKit.Node(box, "Hull");
            _hullBack.anchorMin = _hullBack.anchorMax = new Vector2(0.5f, 0f);
            _hullBack.pivot = new Vector2(0.5f, 1f);
            _hullBack.anchoredPosition = new Vector2(0f, -8f);
            _hullBack.sizeDelta = new Vector2(640f, 30f);
            UiKit.Paint(_hullBack, "Bar_Track", 1.6f, null, new Color(0.2f, 0.12f, 0.06f, 0.8f));
            var fill = UiKit.Fill(_hullBack, "Fill", 4f, 4f, 4f, 4f);
            _hullFill = UiKit.Paint(fill, "Bar_Fill_Gold", 1.6f, new Color(1f, 0.45f, 0.35f, 1f), new Color(0.85f, 0.25f, 0.18f, 1f));
            _hullBack.gameObject.SetActive(false);

            _barkBox = UiKit.Node(go.transform, "Bark");
            _barkBox.anchorMin = _barkBox.anchorMax = new Vector2(0.5f, 1f);
            _barkBox.pivot = new Vector2(0.5f, 1f);
            _barkBox.anchoredPosition = new Vector2(0f, -396f); // clear of the guardian's hull bar
            _barkBox.sizeDelta = new Vector2(940f, 96f);
            UiKit.Paint(_barkBox, "Banner_Maroon", 1.6f, null, new Color(0.35f, 0.07f, 0.06f, 0.9f));
            _bark = UiKit.Label(UiKit.Fill(_barkBox, "Text", 60f, 8f, 60f, 8f), string.Empty, 26f, UiKit.OnWood, TextAlignmentOptions.Center, false, 16f);
            _bark.textWrappingMode = TextWrappingModes.Normal;
            _barkBox.gameObject.SetActive(false);

            // The result: a full-screen backdrop and the scroll card (title, text, the way back).
            _resultBox = UiKit.Fill(go.transform, "Result");
            _resultBox.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var card = UiKit.Node(_resultBox, "Card");
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(1060f, 620f);
            var c = UiKit.ScrollSheet(card, 110f, 50f, 50f);
            UiKit.Medallion(c, 0f, 4f, 116f);
            _resultTitle = UiKit.Label(UiKit.Strip(c, "Title", 130f, 14f, 20f, 90f), string.Empty, 60f, UiKit.Ink, TextAlignmentOptions.MidlineLeft, true, 34f);
            UiKit.Line(c, 0f, 134f, 0f);
            _resultText = UiKit.Label(UiKit.Fill(c, "Text", 10f, 120f, 10f, 150f), string.Empty, 30f, UiKit.Ink, TextAlignmentOptions.TopLeft, false, 18f);
            _resultText.textWrappingMode = TextWrappingModes.Normal;
            var home = UiKit.Node(c, "Return");
            home.anchorMin = home.anchorMax = new Vector2(0.5f, 0f);
            home.pivot = new Vector2(0.5f, 0f);
            home.sizeDelta = new Vector2(500f, 96f);
            home.anchoredPosition = new Vector2(0f, 4f);
            UiKit.KitButton(home, _raid.practice ? "Back to my island" : "Return to port", "Icon_House", () => V4Game.GoToMenu(), "Btn_Gold", 36f, 48f);
            _resultBox.gameObject.SetActive(false);

            Say((_raid.practice ? $"A drill on {_raid.islandName}, Captain? Let us see if yer defences hold. " : $"Ye dare raid {_raid.islandName}? ") +
                IslandRules.GuardianNotice("Barnacle King", GuardianSeconds), 8f);
            _guardian.text = UiKit.InkRich(IslandRules.GuardianLine("Barnacle King", 0, GuardianSeconds, false, -1f));
        }

        void Say(string line, float seconds = 4f)
        {
            _bark.text = $"<color=#ffd166><b>{HackathonRules.CaptainName}:</b></color> \"{line}\"";
            _barkBox.gameObject.SetActive(true);
            _barkUntil = Time.unscaledTime + seconds;
        }

        static readonly string[] WaveTaunts =
        {
            "Another wave, whelp. My crews never tire.",
            "Hold yer course if ye can. The sea is mine here.",
            "Every minute ye linger, the reef grows hungrier.",
            "My gunners have yer range now!",
            "Ye fight well. Pity it ends at the bottom.",
        };

        string WaveLine(int minute)
        {
            var w = _design.windows[minute - 1];
            if (w.enemies == null || w.enemies.Length == 0) return $"Minute {minute}: calm waters";
            var sb = new StringBuilder($"Minute {minute}: ");
            for (var i = 0; i < w.enemies.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(w.enemies[i].count).Append(' ').Append(Pretty(w.enemies[i].kind));
            }
            return sb.ToString();
        }

        static string Pretty(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return kind;
            var parts = kind.Replace("_miniboss", string.Empty).Split('_');
            for (var i = 0; i < parts.Length; i++) if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        void Update()
        {
            if (_barkBox != null && _barkBox.gameObject.activeSelf && Time.unscaledTime > _barkUntil) _barkBox.gameObject.SetActive(false);
            var log = _game != null ? _game.RunLog : null;
            if (log == null || _clock == null || _ended) return;
            _raidSeconds = Math.Max(0.0, log.RunTime != null ? log.RunTime() : 0f);
            var minute = Minute;
            var left = TimeLimit - _raidSeconds;
            var guardianAt = GuardianSeconds;

            // The guardian's hull, once it is up.
            _guardianHull = -1f;
            if (_bosses != null && _bosses.Exists && _bosses.TryFindActiveBoss(out var boss) && boss.MaxHp > 0f)
            {
                _guardianHull = Mathf.Clamp01(boss.Hp / boss.MaxHp);
                if (!_guardianUp) { _guardianUp = true; Say("RISE, Barnacle King! Drag this whelp to the deep!", 5f); }
            }
            _hullBack.gameObject.SetActive(_guardianHull >= 0f);
            if (_guardianHull >= 0f) ((RectTransform)_hullFill.transform).anchorMax = new Vector2(_guardianHull, 1f);

            var timeText = (left <= 60 ? "<color=" + UiKit.BadHex + ">" : string.Empty) + IslandRules.Clock(left) + (left <= 60 ? "</color>" : string.Empty);
            _guardian.text = UiKit.InkRich(IslandRules.GuardianLine("Barnacle King", _raidSeconds, guardianAt, _guardianUp, _guardianHull));
            _clock.text = $"Minute <color={UiKit.GoldHex}>{minute}/15</color>   Time left {timeText}";
            _wave.text = WaveLine(minute);

            if (minute != _lastMinute)
            {
                if (_lastMinute > 0 && minute > 1 && (minute - 1) * 60f != guardianAt && minute % 2 == 0) Say(WaveTaunts[(minute / 2) % WaveTaunts.Length]);
                _lastMinute = minute;
            }
            if (!_saidWarn && !_guardianUp && _raidSeconds >= guardianAt - 15f) { _saidWarn = true; Say("Feel that? The Barnacle King rises in 15 seconds, on his own. No beacon can stop him!", 5f); }
            if (!_saidHalfHull && _guardianHull >= 0f && _guardianHull < 0.5f) { _saidHalfHull = true; Say("No! Not my King! Fire everything!", 4f); }
            if (!_saidLate && left <= 60) { _saidLate = true; Say("The tide turns, whelp. One more minute and the island keeps its crown."); }

            var secured = log.ObjectiveSecured != null && log.ObjectiveSecured();
            var sunk = (log.SunkNow != null && log.SunkNow()) || (log.RunEnded != null && log.RunEnded());
            if (secured) End("guardian");
            else if (left <= 0) End("time");
            else if (sunk) End("sunk");
        }

        void CompleteRun(string why)
        {
            if (_game == null || _game.Context == null) return;
            if (_game.Context.TryGet<RoguePirates.V4.Views.IRunViews>(out var views) && views?.Commands != null)
                views.Commands.CompleteGoal(_raid.practice ? "practice_raid_" + why : "raid_" + why);
        }

        /// <summary>The window the raid reached, for the result (a sunk guardian always counts its own minute).</summary>
        int MinuteReached(bool guardianSunk)
        {
            var m = _raidSeconds >= TimeLimit ? 16 : Minute;
            return guardianSunk ? Math.Max(m, GuardianMinute) : m;
        }

        void End(string why)
        {
            if (_ended) return;
            _ended = true;
            var won = why == "guardian";
            var minute = MinuteReached(won);
            var partial = !won && minute >= GuardianMinute;
            RaidSchedule.Clear();
            // 13 Oct (review): the raid's end is the voyage's end (as the daily race's goal): the run's normal end path
            // settles the Rum, closes level-ups and shows the run as complete. A sinking already ended the run.
            if (why != "sunk") CompleteRun(why);
            _clock.text = won ? $"<color={UiKit.GoodHex}>ISLAND CONQUERED</color>" : partial ? $"<color={UiKit.GoldHex}>THE GUARDIAN HOLDS</color>" : $"<color={UiKit.BadHex}>RAID FAILED</color>";
            Say(won ? "My King... sunk. Take yer plunder. Marrow never forgets." : partial ? "Ye reached my King and still ye fled. Scurry home with yer crumbs!" : "Another hull for the reef! Come back when ye can sail.", 6f);
            _resultBox.gameObject.SetActive(true);
            _resultTitle.text = _raid.practice ? (won ? "Practice: defences fell" : partial ? "Practice: partly held" : "Practice: defences held")
                : won ? "Island conquered!" : partial ? "Partial raid" : "Raid failed";
            var reason = why == "time" ? "Time ran out." : why == "sunk" ? "Your ship went down." : why == "left" ? "You left the raid." :
                $"You sank the guardian in {IslandRules.Clock(_raidSeconds)}.";
            reason += $" Reached minute {Math.Min(15, minute)} of 15.";
            if (_raid.practice)
            {
                // A practice raid stores nothing: no result to send.
                _reported = true;
                var verdict = IslandRules.PracticeVerdict(won, minute, GuardianMinute);
                _resultText.text = reason + "\n" + verdict + "\n<size=85%><color=" + UiKit.DimHex + ">Practice only: no RUM, no loot, no shield, no record.</color></size>";
                LastResult = $"<b>Practice on {_raid.islandName}</b>: {verdict}";
                LastRaid = null;
                return;
            }
            _resultText.text = reason + "\nSending the result...";
            Report(won, minute, reason, true);
        }

        async void Report(bool guardianSunk, int minuteReached, string reason, bool waitForLog)
        {
            if (_reported) return;
            _reported = true;
            var raid = _raid;
            // The server allows no more seconds than have passed since the start.
            var wall = Time.realtimeSinceStartup - _wallStart;
            var seconds = (int)Math.Max(1, Math.Min(Math.Ceiling(_raidSeconds), Math.Floor(wall) - 1));
            // V5 M9j: the raid's run log goes with the result (the server checks it in shadow mode); it closes with the
            // voyage's end, a frame or so after the goal (not waited for when the HUD is being destroyed)
            var recorder = _game != null ? _game.RunLog : null;
            RaidResult result;
            string error;
            try
            {
                var closed = waitForLog ? await RunLogWait.Closed(recorder) : recorder?.Closed;
                (result, error) = await IslandApi.ReportConquest(raid.raidId, guardianSunk, minuteReached, seconds, closed?.Json);
            }
            catch (Exception e)
            {
                (result, error) = (null, e.Message);
            }
            var line = error != null ? $"{raid.islandName}: the result was not accepted ({error})" : Summary(raid, result);
            LastRaid = result;
            LastResult = line;
            if (this == null || _resultText == null) return;
            _resultText.text = reason + "\n" + UiKit.InkRich(line);
            if (error != null) _resultText.color = UiKit.InkBad;
        }

        static string Summary(RaidStart raid, RaidResult r)
        {
            var text = $"<b>{raid.islandName}</b>: " + IslandRules.ResultLine(r.result, r.rumLooted) + $" You have {r.rum} RUM.";
            var shield = OnlineRules.ParseUtc(r.defenderShieldUntil);
            var left = IslandRules.TimeLeft(DateTime.UtcNow, shield);
            if (left != null) text += $"\n{raid.islandName} is now shielded for {left}.";
            return text;
        }

        void OnDestroy()
        {
            RaidSchedule.Clear();
            // Leaving the voyage early still reports the raid (how far it got).
            if (_raid != null && !_raid.practice && !_reported && _lastMinute > 0)
            {
                _ended = true;
                Report(false, MinuteReached(false), "You left the raid.", false);
            }
        }
    }
}
