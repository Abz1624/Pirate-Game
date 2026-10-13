using System;
using System.Collections.Generic;
using RoguePirates.V4;
using RoguePirates.V4.Composition;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the daily island race panel (the menu's "Daily Race" button): today's course (GET
    /// /api/race/daily: seed, goal, closing time, your best), "Set sail" (a voyage on the day's seed with the race HUD,
    /// <see cref="DailyRaceHud"/>) and the day's board (GET /api/race/daily/board) with your rank.
    /// </summary>
    public sealed class DailyRacePanel : MonoBehaviour
    {
        RectTransform _list;
        TextMeshProUGUI _sub, _me, _status, _countdown, _best;
        Button _sail, _close;
        DailyRace _race;
        DateTime? _closes;
        float _nextTick;
        int _requests;
        readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => gameObject.activeSelf;
        public DailyRace Race => _race;
        public int RowCount { get; private set; }
        public string MeText => _me != null ? _me.text : null;
        public string StatusText => _status != null ? _status.text : null;

        public static DailyRacePanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonDailyRace", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), 226);
            rt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var panel = rt.gameObject.AddComponent<DailyRacePanel>();
            panel.Build(rt);
            return panel;
        }

        // Column widths shared by the header bar and the rows (0: the captain column takes the rest).
        const float RankW = 140f, WalletW = 280f, TimeW = 190f, TriesW = 110f;

        /// <summary>Hackathon UI (13 Oct): the owner's "Daily Race" mockup: a parchment scroll with the skull medallion, the
        /// race clock, a wooden column bar, parchment rows with crowns for the top three, and your best with Set sail.</summary>
        void Build(RectTransform rt)
        {
            var c = BoardKit.ScrollSheet(rt);

            // Header: medallion, title and goal, race clock, Refresh and Close.
            BoardKit.Medallion(c, -6f, 0f, 116f);
            BoardKit.Label(BoardKit.Strip(c, "Title", 122f, 2f, 820f, 66f), "DAILY RACE", 60f, BoardKit.Ink, TextAlignmentOptions.MidlineLeft, true, 36f);
            _sub = BoardKit.Label(BoardKit.Strip(c, "Sub", 124f, 68f, 820f, 54f), string.Empty, 24f, BoardKit.InkDim, TextAlignmentOptions.TopLeft, false, 15f);
            _sub.textWrappingMode = TextWrappingModes.Normal;

            var clock = BoardKit.TopRight(c, "Clock", 412f, 4f, 390f, 114f);
            BoardKit.Paint(clock, "Panel_Inset", 2f, new Color(0.93f, 0.86f, 0.72f, 1f));
            BoardKit.Picture(BoardKit.TopLeft(clock, "Icon", 16f, 22f, 70f, 70f), "Icon_Clock", BoardKit.Ink);
            BoardKit.Label(BoardKit.Strip(clock, "Caption", 100f, 8f, 10f, 26f), "RACE ENDS IN", 20f, BoardKit.InkDim, TextAlignmentOptions.MidlineLeft, true);
            _countdown = BoardKit.Label(BoardKit.Strip(clock, "Countdown", 100f, 34f, 10f, 46f), "-", 40f, BoardKit.Ink, TextAlignmentOptions.MidlineLeft, true, 22f);
            BoardKit.Label(BoardKit.Strip(clock, "Utc", 100f, 80f, 10f, 26f), "(00:00 UTC)", 20f, BoardKit.InkDim, TextAlignmentOptions.MidlineLeft);

            BoardKit.KitButton(BoardKit.TopRight(c, "Refresh", 208f, 18f, 196f, 76f), "Refresh", "Icon_Refresh", Refresh, "Btn_Gold", 28f, 34f);
            _close = BoardKit.KitButton(BoardKit.TopRight(c, "Close", 0f, 18f, 196f, 76f), "Close", "Icon_Close", Close, "Btn_Gold", 28f, 32f);
            _status = BoardKit.Label(BoardKit.TopRight(c, "Status", 4f, 96f, 400f, 26f), string.Empty, 20f, BoardKit.InkDim, TextAlignmentOptions.MidlineRight, true);

            // Column bar on wood.
            var bar = BoardKit.Strip(c, "Columns", 0f, 132f, 22f, 56f);
            BoardKit.Paint(bar, "Header_Wood", 1.6f, null, new Color(0.42f, 0.26f, 0.14f, 1f));
            BoardKit.Columns(bar);
            Cells(bar, BoardKit.OnWood, 26f, "#", "Captain", "Wallet", "Time", "Tries");

            // The board.
            var body = BoardKit.Fill(c, "Body", 0f, 118f, 0f, 194f);
            _list = BoardKit.Scroll(body, 6f, new RectOffset(0, 0, 2, 4));

            // Footer: your best, your rank and the last race, Set sail.
            var foot = BoardKit.BottomStrip(c, "Footer", 0f, 0f, 0f, 108f);
            BoardKit.Line(foot, 0f, 0f, 0f);
            BoardKit.Picture(BoardKit.TopLeft(foot, "Trophy", 6f, 14f, 84f, 84f), "Icon_Trophy", BoardKit.InkGold);
            BoardKit.Label(BoardKit.TopLeft(foot, "BestCaption", 100f, 14f, 230f, 28f), "YOUR BEST", 22f, BoardKit.InkDim, TextAlignmentOptions.MidlineLeft, true);
            _best = BoardKit.Label(BoardKit.TopLeft(foot, "Best", 100f, 42f, 230f, 56f), "-", 46f, BoardKit.Ink, TextAlignmentOptions.MidlineLeft, true, 26f);
            var divider = BoardKit.TopLeft(foot, "Divider", 340f, 20f, 3f, 72f).gameObject.AddComponent<Image>();
            divider.color = new Color(0.45f, 0.3f, 0.15f, 0.5f);
            divider.raycastTarget = false;
            _me = BoardKit.Label(BoardKit.Strip(foot, "Me", 362f, 10f, 470f, 90f), string.Empty, 26f, BoardKit.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            _me.textWrappingMode = TextWrappingModes.Normal;
            _sail = BoardKit.KitButton(BoardKit.TopRight(foot, "Set sail", 0f, 10f, 440f, 92f), "SET SAIL", "Icon_Ship_Wheel", SetSail, "Btn_Gold", 40f, 60f);
        }

        static void Cells(Transform row, Color color, float size, string rank, string name, string wallet, string time, string tries)
        {
            BoardKit.TextCell(row, rank, RankW, size, TextAlignmentOptions.MidlineLeft, color, true);
            BoardKit.TextCell(row, name, 0f, size, TextAlignmentOptions.MidlineLeft, color, true);
            BoardKit.TextCell(row, wallet, WalletW, size, TextAlignmentOptions.MidlineLeft, color, true);
            BoardKit.TextCell(row, time, TimeW, size, TextAlignmentOptions.Center, color, true);
            BoardKit.TextCell(row, tries, TriesW, size, TextAlignmentOptions.Center, color, true);
        }

        void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            if (_countdown != null && _closes.HasValue) _countdown.text = OnlineRules.Countdown(DateTime.UtcNow, _closes.Value);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        public async void Refresh()
        {
            var request = ++_requests;
            _status.text = "Loading...";
            _status.color = BoardKit.InkDim;
            _sail.interactable = false;
            try
            {
                var (today, error) = await HackathonApi.Race();
                if (this == null || request != _requests) return;
                if (error != null)
                {
                    _status.text = "Unavailable";
                    _status.color = BoardKit.InkBad;
                    _sub.text = error;
                    ClearRows();
                    return;
                }
                _race = today.race;
                _closes = OnlineRules.ParseUtc(_race.closesAt);
                _countdown.text = _closes.HasValue ? OnlineRules.Countdown(DateTime.UtcNow, _closes.Value) : "-";
                _sub.text = $"{(_race.goal ?? string.Empty).TrimEnd('.')}. Same course for every captain today. " +
                            $"<color={BoardKit.GoldHex}>{today.entries} {(today.entries == 1 ? "captain" : "captains")} raced.</color>";
                _sail.interactable = true;
                var (board, boardError) = await HackathonApi.RaceBoard(_race.date);
                if (this == null || request != _requests) return;
                if (boardError != null) { _status.text = boardError; _status.color = BoardKit.InkBad; return; }
                _status.text = "Online";
                _status.color = BoardKit.InkGood;
                Show(board);
            }
            catch (Exception e)
            {
                Debug.LogWarning("DailyRacePanel: " + e);
                if (this != null) { _status.text = "Unavailable"; _status.color = BoardKit.InkBad; }
            }
        }

        void Show(RaceBoardResponse board)
        {
            ClearRows();
            var myId = OnlineService.Active != null ? OnlineService.Active.playerId : null;
            if (board.entries != null)
            {
                foreach (var e in board.entries)
                {
                    if (e == null) continue;
                    var mine = !string.IsNullOrEmpty(myId) && e.playerId == myId;
                    var tint = mine ? new Color(1f, 0.86f, 0.45f, 1f)
                        : e.rank == 1 ? new Color(0.93f, 0.76f, 0.42f, 1f)
                        : e.rank % 2 == 0 ? new Color(0.9f, 0.84f, 0.72f, 1f) : new Color(0.95f, 0.9f, 0.8f, 1f);
                    var row = BoardKit.Row(_list, 66f, "Panel_Inset", 2.4f, tint);
                    BoardKit.RankCell(row, e.rank, RankW, 30f, true);
                    var name = BoardKit.TextCell(row, e.name + $"<color={BoardKit.DimHex}>{e.tag}</color>", 0f, 28f, TextAlignmentOptions.MidlineLeft, BoardKit.Ink, true);
                    if (mine)
                    {
                        name.margin = new Vector4(0f, 0f, 104f, 0f);
                        BoardKit.Tag((RectTransform)name.transform, "Tag_Green", "YOU", 0f, 92f, 42f, Color.white);
                    }
                    BoardKit.TextCell(row, BoardKit.ShortWallet(e.wallet), WalletW, 26f, TextAlignmentOptions.MidlineLeft, BoardKit.InkDim);
                    BoardKit.TextCell(row, HackathonRules.RaceTime(e.timeSeconds), TimeW, 30f, TextAlignmentOptions.Center, BoardKit.Ink, true);
                    BoardKit.TextCell(row, e.attempts.ToString(), TriesW, 28f, TextAlignmentOptions.Center, BoardKit.Ink);
                    _rows.Add(row.gameObject);
                }
            }
            RowCount = _rows.Count;
            if (RowCount == 0)
            {
                var row = BoardKit.Row(_list, 96f, null, 1f, null);
                BoardKit.TextCell(row, "No times yet today. Set sail and take the top spot!", 0f, 30f, TextAlignmentOptions.Center, BoardKit.InkDim);
                _rows.Add(row.gameObject);
            }
            var last = DailyRaceHud.LastResult;
            _best.text = board.HasMe ? HackathonRules.RaceTime(board.me.timeSeconds) : "-";
            BoardKit.SetLabel(_sail, board.HasMe ? "RACE AGAIN" : "SET SAIL");
            if (board.HasMe)
                _me.text = $"Rank <b>#{board.me.rank}</b> of {board.total}   {board.me.attempts} {(board.me.attempts == 1 ? "try" : "tries")}" +
                           (last != null ? $"\n<size=85%><color={BoardKit.GoodHex}>{last}</color></size>" : string.Empty);
            else
                _me.text = OnlineService.Active == null || !Web3Session.Instance.SignedIn
                    ? "Connect a wallet to post your race time."
                    : last ?? "No time today yet. Reach 100 kills as fast as you can.";
        }

        bool _sailing;

        async void SetSail()
        {
            if (_race == null || _sailing) return;
            if (!Web3Session.Instance.SignedIn) { _me.text = "Connect a wallet first (top of the menu): race times are posted to your wallet profile."; return; }
            _sailing = true;
            try
            {
                // A voyage leaves any 1v1 lobby first (as the menu's Play does). 13 Oct (review): a deposit still waiting for
                // the server is posted first and its answer awaited (the server refunds it once the seat is gone).
                if (PvpSession.Exists && PvpSession.Instance.Lobby != null)
                {
                    _status.text = "Leaving the 1v1 lobby...";
                    var line = await HackathonApi.LeaveLobbySafely();
                    if (this == null) return;
                    if (line != null) Debug.Log("DailyRacePanel: " + line);
                }
            }
            catch (Exception e) { Debug.LogWarning("DailyRacePanel.SetSail: " + e); }
            finally { _sailing = false; }
            if (this == null) return;
            DailyRaceHud.Begin(_race);
            MenuServices.BeginSaltwindExperiment();
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
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
    }
}
