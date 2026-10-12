using System;
using RoguePirates.V4;
using RoguePirates.V4.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the island raid's voyage. The Islands screen starts a raid (POST /api/raids/start) and asks
    /// for it (<see cref="Begin"/>): a voyage on the defender's seed against Captain Marrow's defence fleet. The goal is to
    /// sink <see cref="IslandRules.RaidTargetKills(DefenderConfig)"/> ships (more with the island's walls and cannon tower)
    /// within the time limit (5:00 of run time); half of that when time runs out, the ship sinks or the player leaves is a
    /// partial win, less is a loss. The result goes to POST /api/raids/{id}/result; the HUD then shows the loot, the wager
    /// payout (with an explorer button) and the defender's new shield. Captain Marrow taunts along the way.
    /// </summary>
    public sealed class RaidHud : MonoBehaviour
    {
        static RaidStart _pending;
        static float _pendingAt;

        /// <summary>The next voyage is the raid <paramref name="raid"/>.</summary>
        public static void Begin(RaidStart raid)
        {
            _pending = raid;
            _pendingAt = Time.realtimeSinceStartup;
            RunRequest.ClearPvp();
            RunRequest.RequestSeed(raid.defender != null && raid.defender.seed != 0 ? raid.defender.seed : raid.GetHashCode());
        }

        /// <summary>The last raid's result line (the Islands screen shows it) and its wager payout link.</summary>
        public static string LastResult { get; private set; }
        public static string LastPayoutUrl { get; private set; }
        public static RaidResult LastRaid { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _pending = null;
            LastResult = LastPayoutUrl = null;
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
            if (raid == null || game == null || game.IsPvp || game.RunLog == null) return;
            var hud = game.gameObject.AddComponent<RaidHud>();
            hud._game = game;
            hud._raid = raid;
            hud._wallStart = _pendingAt;
            hud.Build();
        }

        V4Game _game;
        RaidStart _raid;
        float _wallStart;
        float _runStart = -1f;
        TextMeshProUGUI _goal, _clock, _bark, _resultTitle, _resultText;
        RectTransform _barkBox, _resultBox;
        Button _payout;
        string _payoutUrl;
        float _barkUntil;
        bool _ended, _reported, _saidHalf, _saidClose, _saidLate;
        int _kills;
        double _raidSeconds;

        public int Target => IslandRules.RaidTargetKills(_raid?.defender);
        public bool Ended => _ended;
        public bool Reported => _reported;
        public string ResultText => _resultText != null ? _resultText.text : null;
        public RaidStart Raid => _raid;

        void Build()
        {
            var go = new GameObject("HackathonRaidCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 515;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var box = Box(go.transform, "Raid", new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(760f, 108f), new Color(0.06f, 0.1f, 0.16f, 0.85f));
            box.GetComponent<Image>().raycastTarget = false;
            _goal = HackUi.TextAt(box, "Goal", string.Empty, 24, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.6f), Vector2.one, true);
            _clock = HackUi.TextAt(box, "Clock", string.Empty, 34, TextAlignmentOptions.Center, HackUi.TextColor, new Vector2(0f, 0.08f), new Vector2(1f, 0.64f));
            _clock.fontStyle = FontStyles.Bold;
            _goal.text = $"RAID {_raid.islandName}   Sink {Target} of {HackathonRules.CaptainName}'s fleet";

            _barkBox = Box(go.transform, "Bark", new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(900f, 80f), new Color(0.35f, 0.07f, 0.06f, 0.82f));
            _barkBox.GetComponent<Image>().raycastTarget = false;
            _bark = HackUi.TextAt(_barkBox, "Text", string.Empty, 27, TextAlignmentOptions.Center, HackUi.TextColor, Vector2.zero, Vector2.one);
            _bark.margin = new Vector4(16f, 4f, 16f, 4f);
            _bark.textWrappingMode = TextWrappingModes.Normal;
            _barkBox.gameObject.SetActive(false);

            _resultBox = Box(go.transform, "Result", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 440f), new Color(0.06f, 0.1f, 0.16f, 0.96f));
            _resultTitle = HackUi.TextAt(_resultBox, "Title", string.Empty, 52, TextAlignmentOptions.Center, HackUi.Gold, new Vector2(0f, 0.76f), new Vector2(1f, 0.97f), true);
            _resultText = HackUi.TextAt(_resultBox, "Text", string.Empty, 28, TextAlignmentOptions.Top, HackUi.TextColor, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.76f));
            _resultText.textWrappingMode = TextWrappingModes.Normal;
            HackUi.Button(_resultBox, "Return to port", () => V4Game.GoToMenu(), new Vector2(0.06f, 0.05f), new Vector2(0.48f, 0.22f));
            _payout = HackUi.Button(_resultBox, "View payout", () => { if (_payoutUrl != null) Application.OpenURL(_payoutUrl); }, new Vector2(0.52f, 0.05f), new Vector2(0.94f, 0.22f));
            _payout.gameObject.SetActive(false);
            _resultBox.gameObject.SetActive(false);

            Say("Ye dare raid " + _raid.islandName + "? My fleet will feed ye to the reef!", 5f);
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

        void Update()
        {
            if (_barkBox != null && _barkBox.gameObject.activeSelf && Time.unscaledTime > _barkUntil) _barkBox.gameObject.SetActive(false);
            var log = _game != null ? _game.RunLog : null;
            if (log == null || _clock == null || _ended) return;
            var runTime = log.RunTime != null ? log.RunTime() : 0f;
            if (_runStart < 0f) _runStart = runTime;
            _raidSeconds = Math.Max(0.0, runTime - _runStart);
            _kills = log.Kills != null ? log.Kills() : 0;
            var limit = _raid.defender != null && _raid.defender.timeLimitSeconds > 0 ? _raid.defender.timeLimitSeconds : IslandRules.RaidTimeLimitSeconds;
            var left = limit - _raidSeconds;
            _clock.text = $"Sunk <color=#ffd166>{Math.Min(_kills, Target)}/{Target}</color>   " +
                          (left <= 30 ? "<color=#ff8c73>" : string.Empty) + IslandRules.Clock(left) + (left <= 30 ? "</color>" : string.Empty);

            if (!_saidHalf && _kills >= IslandRules.PartialKills(Target)) { _saidHalf = true; Say("Half my fleet on the bottom?! Ye'll pay for every plank!"); }
            else if (!_saidClose && _kills >= Target * 4 / 5) { _saidClose = true; Say("Belay that! Hold the line, ye dogs!"); }
            else if (!_saidLate && left <= 60) { _saidLate = true; Say("The tide turns, whelp. One more minute and the island is mine to keep."); }

            var sunk = (log.SunkNow != null && log.SunkNow()) || (log.RunEnded != null && log.RunEnded());
            if (_kills >= Target) End("sunk the defence");
            else if (left <= 0) End("time");
            else if (sunk) End("sunk");
        }

        void End(string why)
        {
            if (_ended) return;
            _ended = true;
            var (won, partial) = IslandRules.RaidOutcome(_kills, Target);
            _clock.text = won ? "<color=#7fd68b>DEFENCE SUNK</color>" : partial ? "<color=#ffd166>PARTIAL RAID</color>" : "<color=#ff8c73>RAID FAILED</color>";
            Say(won ? "Curse ye... take yer plunder. Marrow never forgets." : partial ? "Ye scratched my walls. Scurry home with yer crumbs!" : "Another hull for the reef! Come back when ye can sail.", 6f);
            _resultBox.gameObject.SetActive(true);
            _resultTitle.text = won ? "Raid won!" : partial ? "Partial raid" : "Raid failed";
            var reason = why == "time" ? "Time ran out." : why == "sunk" ? "Your ship went down." : why == "left" ? "You left the raid." : $"You sank {_kills} ships in {IslandRules.Clock(_raidSeconds)}.";
            _resultText.text = reason + "\nSending the result...";
            Report(won, partial, reason);
        }

        async void Report(bool won, bool partial, string reason)
        {
            if (_reported) return;
            _reported = true;
            var raid = _raid;
            // The server allows no more seconds than have passed since the start.
            var wall = Time.realtimeSinceStartup - _wallStart;
            var seconds = (int)Math.Max(1, Math.Min(Math.Ceiling(_raidSeconds), Math.Floor(wall) - 1));
            var (result, error) = await IslandApi.ReportRaid(raid.raidId, won, partial, seconds);
            var line = error != null ? $"{raid.islandName}: the result was not accepted ({error})" : Summary(raid, result);
            LastRaid = result;
            var sig = result?.wager?.PayoutSig;
            LastPayoutUrl = HackathonRules.ExplorerTx(sig);
            LastResult = line;
            if (this == null || _resultText == null) return;
            _resultText.text = reason + "\n" + line;
            _payoutUrl = LastPayoutUrl;
            _payout.gameObject.SetActive(_payoutUrl != null);
            if (error != null) _resultText.color = HackUi.Bad;
        }

        static string Summary(RaidStart raid, RaidResult r)
        {
            var text = $"<b>{raid.islandName}</b>: " + IslandRules.ResultLine(r.result, r.rumLooted) + $" You have {r.rum} RUM.";
            var w = r.wager;
            if (w != null && w.Exists)
            {
                if (w.payout != null && !string.IsNullOrEmpty(w.payout.signature))
                    text += $"\nWager paid: <color=#7fd68b>{HackathonRules.Sol(w.payout.lamports)}</color> to your wallet ({HackathonRules.ShortSig(w.payout.signature)}).";
                else if (w.status == "funded" && (r.result == "won" || r.result == "partial"))
                    text += "\nWager payout pending (retry from Raid history).";
                else if (w.status == "cancelled") text += "\nThe stake was never funded: wager cancelled.";
                else if (r.result == "lost") text += $"\nThe house keeps your {HackathonRules.Sol(w.stakeLamports)} stake.";
            }
            var shield = OnlineRules.ParseUtc(r.defenderShieldUntil);
            var left = IslandRules.TimeLeft(DateTime.UtcNow, shield);
            if (left != null) text += $"\n{raid.islandName} is now shielded for {left}.";
            return text;
        }

        void OnDestroy()
        {
            // Leaving the voyage early still reports the raid (the score so far).
            if (_raid != null && !_reported && _runStart >= 0f)
            {
                var (won, partial) = IslandRules.RaidOutcome(_kills, Target);
                _ended = true;
                Report(won, partial, "You left the raid.");
            }
        }
    }
}
