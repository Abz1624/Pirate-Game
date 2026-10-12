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
        TextMeshProUGUI _sub, _me, _status;
        Button _sail, _close;
        DailyRace _race;
        int _requests;
        readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsOpen => gameObject.activeSelf;
        public DailyRace Race => _race;
        public int RowCount { get; private set; }
        public string MeText => _me != null ? _me.text : null;
        public string StatusText => _status != null ? _status.text : null;

        public static DailyRacePanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonDailyRace", new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.94f), 226);
            var panel = rt.gameObject.AddComponent<DailyRacePanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = HackUi.TopStrip(rt, "Header", 16f, 80f);
            HackUi.TextAt(header, "Title", "Daily island race", 44, TextAlignmentOptions.MidlineLeft, HackUi.Gold, Vector2.zero, new Vector2(0.44f, 1f), true);
            HackUi.Button(header, "Refresh", Refresh, new Vector2(0.45f, 0.05f), new Vector2(0.62f, 0.95f));
            _sail = HackUi.Button(header, "Set sail", SetSail, new Vector2(0.63f, 0.05f), new Vector2(0.81f, 0.95f));
            _close = HackUi.Button(header, "Close", Close, new Vector2(0.82f, 0.05f), new Vector2(1f, 0.95f));

            var sub = HackUi.TopStrip(rt, "Sub", 104f, 64f);
            _sub = HackUi.TextAt(sub, "Text", string.Empty, 25, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, Vector2.one);
            _sub.textWrappingMode = TextWrappingModes.Normal;
            _status = HackUi.StatusBar(rt, 174f, 48f);

            var columns = HackUi.TopStrip(rt, "Columns", 230f, 44f, 32f);
            var hl = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 12, 0, 0);
            hl.spacing = 12f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            Cells(columns, HackUi.Dim, 24, "#", "Captain", "Wallet", "Time", "Tries");

            var body = HackUi.Area(rt, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 104f, 24f, 278f));
            _list = HackUi.ScrollList(body, "Scroll", Vector4.zero);

            var footer = HackUi.Area(rt, "Footer", Vector2.zero, new Vector2(1f, 0f), new Vector4(24f, 16f, 24f, 0f));
            footer.offsetMax = new Vector2(-24f, 88f);
            footer.gameObject.AddComponent<Image>().color = new Color(1f, 0.82f, 0.4f, 0.12f);
            _me = HackUi.TextAt(footer, "Me", string.Empty, 28, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, Vector2.one);
            _me.margin = new Vector4(20f, 0f, 20f, 0f);
            _me.textWrappingMode = TextWrappingModes.Normal;
        }

        static void Cells(Transform row, Color color, float size, string rank, string name, string wallet, string time, string tries)
        {
            HackUi.Cell(row, rank, 90f, size, TextAlignmentOptions.MidlineLeft, color);
            HackUi.Cell(row, name, 0f, size, TextAlignmentOptions.MidlineLeft, color);
            HackUi.Cell(row, wallet, 220f, size, TextAlignmentOptions.MidlineLeft, color);
            HackUi.Cell(row, time, 170f, size, TextAlignmentOptions.MidlineRight, color);
            HackUi.Cell(row, tries, 110f, size, TextAlignmentOptions.MidlineRight, color);
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
            _status.color = HackUi.Dim;
            _sail.interactable = false;
            try
            {
                var (today, error) = await HackathonApi.Race();
                if (this == null || request != _requests) return;
                if (error != null)
                {
                    _status.text = "Unavailable";
                    _status.color = HackUi.Bad;
                    _sub.text = error;
                    ClearRows();
                    return;
                }
                _race = today.race;
                var closes = OnlineRules.ParseUtc(_race.closesAt);
                var left = closes.HasValue ? OnlineRules.Countdown(DateTime.UtcNow, closes.Value) : "-";
                _sub.text = $"<color=#ffd166>{_race.goal}</color>\nSame course for every captain today. Closes in {left} (00:00 UTC). " +
                            $"{today.entries} {(today.entries == 1 ? "captain" : "captains")} raced.";
                _sail.interactable = true;
                var (board, boardError) = await HackathonApi.RaceBoard(_race.date);
                if (this == null || request != _requests) return;
                if (boardError != null) { _status.text = boardError; _status.color = HackUi.Bad; return; }
                _status.text = "Online";
                _status.color = HackUi.Good;
                Show(board);
            }
            catch (Exception e)
            {
                Debug.LogWarning("DailyRacePanel: " + e);
                if (this != null) { _status.text = "Unavailable"; _status.color = HackUi.Bad; }
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
                    var tint = mine ? new Color(1f, 0.82f, 0.4f, 0.22f) : (e.rank % 2 == 0 ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.07f));
                    var row = HackUi.Row(_list, 58f, tint);
                    var color = e.rank == 1 ? HackUi.Gold : HackUi.TextColor;
                    Cells(row, color, 27, "#" + e.rank, e.name + "<color=#9aa4ad>" + e.tag + "</color>" + (mine ? "  <size=70%>(you)</size>" : string.Empty),
                        string.IsNullOrEmpty(e.wallet) ? "-" : e.wallet, HackathonRules.RaceTime(e.timeSeconds), e.attempts.ToString());
                    _rows.Add(row.gameObject);
                }
            }
            RowCount = _rows.Count;
            if (RowCount == 0)
            {
                var row = HackUi.Row(_list, 90f, new Color(0f, 0f, 0f, 0f));
                HackUi.Cell(row, "No times yet today. Set sail and take the top spot!", 0f, 28, TextAlignmentOptions.Center, HackUi.Dim);
                _rows.Add(row.gameObject);
            }
            var last = DailyRaceHud.LastResult;
            if (board.HasMe)
                _me.text = $"You: <color=#ffd166>#{board.me.rank}</color> of {board.total}   best {HackathonRules.RaceTime(board.me.timeSeconds)}   {board.me.attempts} {(board.me.attempts == 1 ? "try" : "tries")}" +
                           (last != null ? "\n<size=80%><color=#9aa4ad>" + last + "</color></size>" : string.Empty);
            else
                _me.text = OnlineService.Active == null || !Web3Session.Instance.SignedIn
                    ? "Connect a wallet to post your race time."
                    : last ?? "No time today yet. Reach 100 kills as fast as you can.";
        }

        void SetSail()
        {
            if (_race == null) return;
            if (!Web3Session.Instance.SignedIn) { _me.text = "Connect a wallet first (top of the menu): race times are posted to your wallet profile."; return; }
            // A voyage leaves any 1v1 lobby first (as the menu's Play does).
            if (PvpSession.Exists && PvpSession.Instance.Lobby != null) _ = PvpSession.Instance.Leave();
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
