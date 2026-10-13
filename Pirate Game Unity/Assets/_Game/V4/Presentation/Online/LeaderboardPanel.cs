using System;
using System.Collections.Generic;
using RoguePirates.UI;
using RoguePirates.V4.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the weekly kills leaderboard (Saltwind): this week's top 50 (rank, name#tag, kills, run
    /// time, level, Captain), the player's own rank, last week's final board on a toggle, and the countdown to the weekly
    /// reset (Monday 00:00 UTC). "Offline" when the API cannot be reached.
    /// </summary>
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        RectTransform _list, _content, _body;
        TextMeshProUGUI _title, _reset, _me, _status;
        Button _toggle, _close;
        bool _previous;
        int _requests;
        DateTime? _weekEnd;
        float _nextTick;
        float _reserved;
        readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => gameObject.activeSelf;
        public bool ShowingPrevious => _previous;
        public int RowCount { get; private set; }
        public string StatusText => _status != null ? _status.text : null;
        public string MeText => _me != null ? _me.text : null;
        /// <summary>The last board read (tests and the scripted check).</summary>
        public LeaderboardResponse Last { get; private set; }

        public static LeaderboardPanel Create(Transform canvas)
        {
            var rt = OnlineUi.Panel(canvas, "PlaytestLeaderboard", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), 220);
            rt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var panel = rt.gameObject.AddComponent<LeaderboardPanel>();
            panel.Build(rt);
            return panel;
        }

        const float FooterHeight = 86f, ListTop = 232f;
        // Column widths shared by the header and the rows (0: the player column takes the rest).
        const float RankW = 120f, WalletW = 210f, KillsW = 110f, TimeW = 120f, LevelW = 80f, CaptainW = 140f, RewardW = 190f;

        /// <summary>Hackathon UI (13 Oct): the owner's "Weekly Leaderboard" mockup on the parchment kit: a wooden header with
        /// the skull medallion, the reset clock, parchment rows with crowns for the top three and your own row at the foot.</summary>
        void Build(RectTransform rt)
        {
            UiKit.Paint(UiKit.Fill(rt, "Sheet"), "small_scroll_bg_2", 1.25f, new Color(0.95f, 0.87f, 0.7f, 1f));
            var c = _content = UiKit.Fill(rt, "Content", 104f, 44f, 104f, 44f);

            var head = UiKit.Strip(c, "Header", 0f, 0f, 0f, 100f);
            UiKit.Paint(head, "Header_Wood", 1.4f, new Color(0.42f, 0.26f, 0.14f, 1f));
            UiKit.Medallion(head, -14f, -10f, 120f);
            _title = UiKit.Label(UiKit.Strip(head, "Title", 122f, 0f, 500f, 100f), "Weekly Leaderboard", 54f, UiKit.OnWood, TextAlignmentOptions.MidlineLeft, true, 30f);
            _close = UiKit.KitButton(UiKit.TopRight(head, "Close", 16f, 12f, 200f, 76f), "Close", "Icon_Close", Close, "Btn_Gold", 30f, 34f);
            _toggle = UiKit.KitButton(UiKit.TopRight(head, "Last week", 230f, 12f, 250f, 76f), "Last week", null, Toggle, "Btn_Gold", 30f, 34f);

            var reset = UiKit.Strip(c, "Reset", 0f, 110f, 0f, 56f);
            UiKit.Paint(reset, "Panel_Inset", 2.4f, new Color(0.97f, 0.92f, 0.82f, 1f));
            UiKit.Picture(UiKit.TopLeft(reset, "Clock", 18f, 8f, 40f, 40f), "Icon_Clock", UiKit.Ink);
            _reset = UiKit.Label(UiKit.Strip(reset, "Text", 72f, 0f, 300f, 56f), string.Empty, 26f, UiKit.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            _status = UiKit.Label(UiKit.TopRight(reset, "Status", 20f, 0f, 280f, 56f), string.Empty, 22f, UiKit.InkDim, TextAlignmentOptions.MidlineRight, true, 14f);

            var columns = UiKit.Strip(c, "Columns", 0f, 174f, 22f, 52f);
            UiKit.Paint(columns, "Panel_Inset", 2.4f, new Color(0.86f, 0.76f, 0.6f, 1f));
            UiKit.Columns(columns);
            Cells(columns, UiKit.Ink, 24, "#", "Player", "Wallet", "Kills", "Time", "Lvl", "Captain", "Reward");

            _body = UiKit.Fill(c, "Body", 0f, FooterHeight + 10f, 0f, ListTop);
            _list = UiKit.Scroll(_body, 6f, new RectOffset(0, 0, 2, 4), 14f, 22f);

            var foot = UiKit.BottomStrip(c, "Footer", 0f, 0f, 0f, FooterHeight);
            UiKit.Paint(foot, "Panel_Inset", 2.4f, new Color(1f, 0.86f, 0.45f, 1f));
            var you = UiKit.Node(foot, "You");
            you.anchorMin = you.anchorMax = you.pivot = new Vector2(0f, 0.5f);
            you.anchoredPosition = new Vector2(10f, 0f);
            you.sizeDelta = new Vector2(150f, 60f);
            UiKit.Paint(you, "Banner_Maroon", 2.4f, null, new Color(0.45f, 0.16f, 0.12f, 1f));
            UiKit.Label(UiKit.Fill(you, "Text"), "You", 32f, UiKit.OnWood, TextAlignmentOptions.Center, true);
            _me = UiKit.Label(UiKit.Fill(foot, "Me", 182f, 0f, 20f, 0f), string.Empty, 28f, UiKit.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
        }

        /// <summary>Hackathon (12 Oct): strips other code adds to the panel's root (the weekly SOL rewards strip,
        /// "HackathonRewards") move inside the parchment above the "You" row, keeping their height; the list ends above them.</summary>
        void AdoptStrips()
        {
            if (_content == null) return;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child == null || !child.name.StartsWith("Hackathon", StringComparison.Ordinal)) continue;
                var height = Mathf.Max(40f, child.offsetMax.y - child.offsetMin.y);
                child.SetParent(_content, false);
                child.anchorMin = Vector2.zero;
                child.anchorMax = new Vector2(1f, 0f);
                child.pivot = new Vector2(0.5f, 0f);
                child.offsetMin = new Vector2(0f, FooterHeight + 10f + _reserved);
                child.offsetMax = new Vector2(-22f, FooterHeight + 10f + _reserved + height);
                _reserved += height + 8f;
                _body.offsetMin = new Vector2(_body.offsetMin.x, FooterHeight + 10f + _reserved);
            }
        }

        void OnTransformChildrenChanged() => AdoptStrips();

        /// <summary>Rank, name, wallet, kills, time, level, Captain, reward (Hackathon, 12 Oct): the column widths shared by
        /// the header and the rows.</summary>
        static void Cells(Transform row, Color color, float size, string rank, string name, string wallet, string kills, string time, string level, string captain, string reward)
        {
            UiKit.TextCell(row, rank, RankW, size, TextAlignmentOptions.MidlineLeft, color, true);
            UiKit.TextCell(row, name, 0f, size, TextAlignmentOptions.MidlineLeft, color, true);
            UiKit.TextCell(row, wallet, WalletW, size, TextAlignmentOptions.MidlineLeft, color, true);
            UiKit.TextCell(row, kills, KillsW, size, TextAlignmentOptions.Center, color, true);
            UiKit.TextCell(row, time, TimeW, size, TextAlignmentOptions.Center, color, true);
            UiKit.TextCell(row, level, LevelW, size, TextAlignmentOptions.Center, color, true);
            UiKit.TextCell(row, captain, CaptainW, size, TextAlignmentOptions.Center, color, true);
            UiKit.TextCell(row, reward, RewardW, size, TextAlignmentOptions.Center, color, true);
        }

        /// <summary>Hackathon (12 Oct): the wallet (short form).</summary>
        static string Wallet(LeaderboardEntry e) =>
            string.IsNullOrEmpty(e.wallet) ? "-" : e.wallet.Length > 12 ? e.wallet.Substring(0, 4) + "..." + e.wallet.Substring(e.wallet.Length - 4) : e.wallet;

        /// <summary>Hackathon (12 Oct): the place's devnet SOL reward.</summary>
        static string Reward(LeaderboardEntry e)
        {
            if (e.reward == null || e.reward.lamports <= 0) return "-";
            var sol = "<color=" + UiKit.GoldHex + ">" + e.reward.sol.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " SOL</color>";
            return e.reward.status == "sent" ? sol + " <size=70%><color=" + UiKit.GoodHex + ">paid</color></size>" : sol;
        }

        public void Open(bool previous = false)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            AdoptStrips();
            _previous = previous;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        void Toggle()
        {
            _previous = !_previous;
            Refresh();
        }

        /// <summary>Reads the board again (this week's or last week's).</summary>
        public async void Refresh()
        {
            var request = ++_requests;
            _title.text = _previous ? "Last Week (final)" : "Weekly Leaderboard";
            UiKit.SetLabel(_toggle, _previous ? "This week" : "Last week");
            _status.text = "Loading...";
            _status.color = UiKit.InkDim;
            ApiResult<LeaderboardResponse> r;
            try { r = await OnlineService.Leaderboard(_previous); }
            catch (Exception e)
            {
                Debug.LogWarning("LeaderboardPanel: " + e.Message);
                r = new ApiResult<LeaderboardResponse> { Outcome = ApiOutcome.Offline, Message = e.Message };
            }
            if (this == null || request != _requests) return; // closed scene or a newer read
            Show(r);
        }

        void Show(ApiResult<LeaderboardResponse> r)
        {
            ClearRows();
            Last = r.Ok ? r.Data : null;
            if (!r.Ok || r.Data == null)
            {
                _status.text = r.IsOffline ? "Offline" : "Unavailable";
                _status.color = UiKit.InkBad;
                Message(r.IsOffline ? "Offline: the leaderboard needs a connection. Your runs still count on this device." : "The leaderboard is unavailable: " + r.Message);
                _me.text = OnlineService.Active != null ? OnlineService.Active.Display : string.Empty;
                _weekEnd = null;
                UpdateReset();
                return;
            }
            var data = r.Data;
            _status.text = "Online";
            _status.color = UiKit.InkGood;
            _weekEnd = OnlineRules.ParseUtc(data.weekEnd);
            UpdateReset();
            var myId = OnlineService.Active != null ? OnlineService.Active.playerId : null;
            if (data.entries != null)
            {
                foreach (var e in data.entries)
                {
                    if (e == null) continue;
                    var mine = !string.IsNullOrEmpty(myId) && e.playerId == myId;
                    var tint = mine ? new Color(1f, 0.86f, 0.45f, 1f)
                        : e.rank == 1 ? new Color(0.98f, 0.84f, 0.5f, 1f)
                        : e.rank % 2 == 0 ? new Color(0.92f, 0.86f, 0.74f, 1f) : new Color(0.97f, 0.92f, 0.82f, 1f);
                    var row = UiKit.TableRow(_list, 62f, tint);
                    UiKit.RankCell(row, e.rank, RankW, 30f, false, 50f);
                    // V5 M9a (I9a.3): "checked" when the run passed the server's run checks (shadow mode: the rest still rank)
                    UiKit.TextCell(row, e.name + "<color=" + UiKit.DimHex + ">" + e.tag + "</color>" + (e.@checked ? CheckedMark : "") + (mine ? YouMark : ""), 0f, 28f,
                        TextAlignmentOptions.MidlineLeft, UiKit.Ink, true);
                    UiKit.TextCell(row, Wallet(e), WalletW, 24f, TextAlignmentOptions.MidlineLeft, UiKit.InkDim);
                    UiKit.TextCell(row, e.kills.ToString(), KillsW, 28f, TextAlignmentOptions.Center, UiKit.Ink, true);
                    UiKit.TextCell(row, OnlineRules.RunTime(e.runSeconds), TimeW, 28f, TextAlignmentOptions.Center, UiKit.Ink);
                    UiKit.TextCell(row, e.level.ToString(), LevelW, 28f, TextAlignmentOptions.Center, UiKit.Ink);
                    UiKit.TextCell(row, Captain(e.captain), CaptainW, 28f, TextAlignmentOptions.Center, UiKit.Ink);
                    UiKit.TextCell(row, Reward(e), RewardW, 28f, TextAlignmentOptions.Center, UiKit.Ink, true);
                    _rows.Add(row.gameObject);
                }
            }
            RowCount = _rows.Count;
            if (RowCount == 0) Message(_previous ? "No runs were ranked last week." : "No runs this week yet. Set sail and claim the top spot!");
            var me = OnlineService.Active;
            if (data.HasMe)
                _me.text = $"<b>#{data.me.rank}</b>   {(me != null ? me.Display : "you")}   <b>{data.me.kills}</b> kills   {OnlineRules.RunTime(data.me.runSeconds)}   level {data.me.level}" +
                           (data.me.@checked ? CheckedMark : "");
            else
                _me.text = me == null ? "Make a profile to join the board." : $"{me.Display}: no run {(_previous ? "last week" : "this week yet")}";
        }

        const string CheckedMark = "  <size=65%><color=" + UiKit.GoodHex + ">checked</color></size>";
        const string YouMark = "  <size=65%><color=" + UiKit.GoodHex + ">(you)</color></size>";

        static string Captain(string key) =>
            string.IsNullOrEmpty(key) ? "-" : char.ToUpperInvariant(key[0]) + key.Substring(1);

        void Message(string text)
        {
            var row = UiKit.TableRow(_list, 96f, null);
            var t = UiKit.TextCell(row, text, 0f, 28f, TextAlignmentOptions.Center, UiKit.InkDim);
            t.textWrappingMode = TextWrappingModes.Normal;
            _rows.Add(row.gameObject);
            RowCount = 0;
        }

        void ClearRows()
        {
            foreach (var r in _rows)
            {
                if (r == null) continue;
                r.SetActive(false);
                Destroy(r);
            }
            _rows.Clear();
            RowCount = 0;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            UpdateReset();
        }

        void UpdateReset()
        {
            if (_reset == null) return;
            var now = DateTime.UtcNow;
            var end = _weekEnd.HasValue && _weekEnd.Value > now ? _weekEnd.Value : OnlineRules.NextWeeklyReset(now);
            _reset.text = _previous
                ? $"This week ends in {OnlineRules.Countdown(now, end)}"
                : $"Resets in {OnlineRules.Countdown(now, end)} (Monday 00:00 UTC)";
        }
    }
}
