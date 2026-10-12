using System;
using System.Collections.Generic;
using System.Text;
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
            Arm(defence);
        }

        /// <summary>Sets the spawn schedule from a raid's design (also used by tests).</summary>
        public static void Arm(RaidDefence defence)
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
            RaidSchedule.Set(entries, guardianAt, window);
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRun;
            V4Game.RunStarted += OnRun;
        }

        static void OnRun(V4Game game)
        {
            var raid = _pending;
            _pending = null;
            if (raid == null || game == null || game.IsPvp || game.RunLog == null)
            {
                RaidSchedule.Clear(); // never let a schedule reach a normal voyage
                return;
            }
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
            if (_game.Context != null) _game.Context.TryGet(out _bosses);
            var go = new GameObject("HackathonRaidCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 515;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var box = Box(go.transform, "Raid", new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(900f, 196f), new Color(0.06f, 0.1f, 0.16f, 0.85f));
            box.GetComponent<Image>().raycastTarget = false;
            _goal = HackUi.TextAt(box, "Goal", string.Empty, 24, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.79f), Vector2.one, true);
            // Always visible (owner, 12 Oct): when the guardian rises (no beacon in a raid), with a live countdown.
            _guardian = HackUi.TextAt(box, "Guardian", string.Empty, 25, TextAlignmentOptions.Center, new Color(1f, 0.6f, 0.5f, 1f), new Vector2(0.02f, 0.57f), new Vector2(0.98f, 0.8f));
            _clock = HackUi.TextAt(box, "Clock", string.Empty, 30, TextAlignmentOptions.Center, HackUi.TextColor, new Vector2(0f, 0.3f), new Vector2(1f, 0.58f));
            _clock.fontStyle = FontStyles.Bold;
            _wave = HackUi.TextAt(box, "Wave", string.Empty, 21, TextAlignmentOptions.Center, HackUi.Dim, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.3f));
            _goal.text = _raid.practice ? $"PRACTICE: TEST {_raid.islandName}'S DEFENCES   no fee, no loot" : $"CONQUER {_raid.islandName}   sink the guardian";
            _hullBack = Box(box, "Hull", new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(640f, 22f), new Color(0f, 0f, 0f, 0.6f));
            _hullBack.pivot = new Vector2(0.5f, 1f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(_hullBack, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            _hullFill = fill.GetComponent<Image>();
            _hullFill.color = new Color(0.9f, 0.3f, 0.22f, 1f);
            _hullFill.raycastTarget = false;
            _hullBack.gameObject.SetActive(false);

            _barkBox = Box(go.transform, "Bark", new Vector2(0.5f, 1f), new Vector2(0f, -384f), new Vector2(900f, 80f), new Color(0.35f, 0.07f, 0.06f, 0.82f));
            _barkBox.GetComponent<Image>().raycastTarget = false;
            _bark = HackUi.TextAt(_barkBox, "Text", string.Empty, 27, TextAlignmentOptions.Center, HackUi.TextColor, Vector2.zero, Vector2.one);
            _bark.margin = new Vector4(16f, 4f, 16f, 4f);
            _bark.textWrappingMode = TextWrappingModes.Normal;
            _barkBox.gameObject.SetActive(false);

            _resultBox = Box(go.transform, "Result", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 440f), new Color(0.06f, 0.1f, 0.16f, 0.96f));
            _resultTitle = HackUi.TextAt(_resultBox, "Title", string.Empty, 52, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.76f), new Vector2(1f, 0.97f), true);
            _resultText = HackUi.TextAt(_resultBox, "Text", string.Empty, 28, TextAlignmentOptions.Top, HackUi.TextColor, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.76f));
            _resultText.textWrappingMode = TextWrappingModes.Normal;
            HackUi.Button(_resultBox, _raid.practice ? "Back to my island" : "Return to port", () => V4Game.GoToMenu(), new Vector2(0.27f, 0.05f), new Vector2(0.73f, 0.22f));
            _resultBox.gameObject.SetActive(false);

            Say((_raid.practice ? $"A drill on {_raid.islandName}, Captain? Let us see if yer defences hold. " : $"Ye dare raid {_raid.islandName}? ") +
                IslandRules.GuardianNotice("Barnacle King", GuardianSeconds), 8f);
            _guardian.text = IslandRules.GuardianLine("Barnacle King", 0, GuardianSeconds, false, -1f);
        }

        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
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

            var timeText = (left <= 60 ? "<color=#ff8c73>" : string.Empty) + IslandRules.Clock(left) + (left <= 60 ? "</color>" : string.Empty);
            _guardian.text = IslandRules.GuardianLine("Barnacle King", _raidSeconds, guardianAt, _guardianUp, _guardianHull);
            _clock.text = $"Minute <color=#ffd166>{minute}/15</color>   Time left {timeText}";
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
            _clock.text = won ? "<color=#7fd68b>ISLAND CONQUERED</color>" : partial ? "<color=#ffd166>THE GUARDIAN HOLDS</color>" : "<color=#ff8c73>RAID FAILED</color>";
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
                _resultText.text = reason + "\n" + verdict + "\n<size=85%><color=#9aa4ad>Practice only: no RUM, no loot, no shield, no record.</color></size>";
                LastResult = $"<b>Practice on {_raid.islandName}</b>: {verdict}";
                LastRaid = null;
                return;
            }
            _resultText.text = reason + "\nSending the result...";
            Report(won, minute, reason);
        }

        async void Report(bool guardianSunk, int minuteReached, string reason)
        {
            if (_reported) return;
            _reported = true;
            var raid = _raid;
            // The server allows no more seconds than have passed since the start.
            var wall = Time.realtimeSinceStartup - _wallStart;
            var seconds = (int)Math.Max(1, Math.Min(Math.Ceiling(_raidSeconds), Math.Floor(wall) - 1));
            var (result, error) = await IslandApi.ReportConquest(raid.raidId, guardianSunk, minuteReached, seconds);
            var line = error != null ? $"{raid.islandName}: the result was not accepted ({error})" : Summary(raid, result);
            LastRaid = result;
            LastResult = line;
            if (this == null || _resultText == null) return;
            _resultText.text = reason + "\n" + line;
            if (error != null) _resultText.color = HackUi.Bad;
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
                Report(false, MinuteReached(false), "You left the raid.");
            }
        }
    }
}
