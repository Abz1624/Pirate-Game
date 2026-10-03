using System;
using System.Collections.Generic;
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
        RectTransform _list;
        TextMeshProUGUI _title, _reset, _me, _status;
        Button _toggle, _close;
        bool _previous;
        int _requests;
        DateTime? _weekEnd;
        float _nextTick;
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
            var rt = OnlineUi.Panel(canvas, "PlaytestLeaderboard", new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f), 220);
            var panel = rt.gameObject.AddComponent<LeaderboardPanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = OnlineUi.TopStrip(rt, "Header", 16f, 80f);
            _title = OnlineUi.TextAt(header, "Title", "Weekly leaderboard", 44, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.62f, 1f), true);
            _close = OnlineUi.Button(header, "Close", Close, new Vector2(0.82f, 0.05f), new Vector2(1f, 0.95f));
            _toggle = OnlineUi.Button(header, "Last week", Toggle, new Vector2(0.62f, 0.05f), new Vector2(0.8f, 0.95f));

            var sub = OnlineUi.TopStrip(rt, "Sub", 100f, 44f);
            _reset = OnlineUi.TextAt(sub, "Reset", string.Empty, 26, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim, Vector2.zero, new Vector2(0.6f, 1f));
            _status = OnlineUi.TextAt(sub, "Status", string.Empty, 26, TextAlignmentOptions.MidlineRight, OnlineUi.Dim, new Vector2(0.6f, 0f), Vector2.one);

            var columns = OnlineUi.TopStrip(rt, "Columns", 150f, 48f, 32f);
            var hl = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 12, 0, 0);
            hl.spacing = 12f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            Cells(columns, OnlineUi.Dim, 24, "#", "Player", "Kills", "Time", "Level", "Captain");

            var body = OnlineUi.Area(rt, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 104f, 24f, 200f));
            _list = OnlineUi.ScrollList(body, "Scroll", Vector4.zero);

            var footer = OnlineUi.Area(rt, "Footer", Vector2.zero, new Vector2(1f, 0f), new Vector4(24f, 16f, 24f, 0f));
            footer.offsetMax = new Vector2(-24f, 88f);
            footer.gameObject.AddComponent<Image>().color = new Color(1f, 0.82f, 0.4f, 0.12f);
            _me = OnlineUi.TextAt(footer, "Me", string.Empty, 30, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, Vector2.zero, Vector2.one);
            _me.margin = new Vector4(20f, 0f, 20f, 0f);
        }

        /// <summary>Rank, name, kills, time, level, Captain: the column widths shared by the header and the rows.</summary>
        static void Cells(Transform row, Color color, float size, string rank, string name, string kills, string time, string level, string captain)
        {
            OnlineUi.Cell(row, rank, 90f, size, TextAlignmentOptions.MidlineLeft, color);
            OnlineUi.Cell(row, name, 0f, size, TextAlignmentOptions.MidlineLeft, color);
            OnlineUi.Cell(row, kills, 140f, size, TextAlignmentOptions.MidlineRight, color);
            OnlineUi.Cell(row, time, 140f, size, TextAlignmentOptions.MidlineRight, color);
            OnlineUi.Cell(row, level, 110f, size, TextAlignmentOptions.MidlineRight, color);
            OnlineUi.Cell(row, captain, 170f, size, TextAlignmentOptions.MidlineRight, color);
        }

        public void Open(bool previous = false)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
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
            _title.text = _previous ? "Last week (final)" : "Weekly leaderboard";
            OnlineUi.SetLabel(_toggle, _previous ? "This week" : "Last week");
            _status.text = "Loading...";
            _status.color = OnlineUi.Dim;
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
                _status.color = OnlineUi.Bad;
                Message(r.IsOffline ? "Offline: the leaderboard needs a connection. Your runs still count on this device." : "The leaderboard is unavailable: " + r.Message);
                _me.text = OnlineService.Active != null ? OnlineService.Active.Display : string.Empty;
                _weekEnd = null;
                UpdateReset();
                return;
            }
            var data = r.Data;
            _status.text = "Online";
            _status.color = OnlineUi.Good;
            _weekEnd = OnlineRules.ParseUtc(data.weekEnd);
            UpdateReset();
            var myId = OnlineService.Active != null ? OnlineService.Active.playerId : null;
            if (data.entries != null)
            {
                foreach (var e in data.entries)
                {
                    if (e == null) continue;
                    var mine = !string.IsNullOrEmpty(myId) && e.playerId == myId;
                    var tint = mine ? new Color(1f, 0.82f, 0.4f, 0.22f) : (e.rank % 2 == 0 ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.07f));
                    var row = OnlineUi.Row(_list, 60f, tint);
                    var color = e.rank == 1 ? OnlineUi.Gold : OnlineUi.TextColor;
                    Cells(row, color, 28, "#" + e.rank, e.name + "<color=#9aa4ad>" + e.tag + "</color>", e.kills.ToString(), OnlineRules.RunTime(e.runSeconds), e.level.ToString(), Captain(e.captain));
                    _rows.Add(row.gameObject);
                }
            }
            RowCount = _rows.Count;
            if (RowCount == 0) Message(_previous ? "No runs were ranked last week." : "No runs this week yet. Set sail and claim the top spot!");
            var me = OnlineService.Active;
            if (data.HasMe)
                _me.text = $"You ({(me != null ? me.Display : "you")}): #{data.me.rank}   {data.me.kills} kills   {OnlineRules.RunTime(data.me.runSeconds)}   level {data.me.level}";
            else
                _me.text = me == null ? "Make a profile to join the board." : $"You ({me.Display}): no run {(_previous ? "last week" : "this week yet")}";
        }

        static string Captain(string key) =>
            string.IsNullOrEmpty(key) ? "-" : char.ToUpperInvariant(key[0]) + key.Substring(1);

        void Message(string text)
        {
            var row = OnlineUi.Row(_list, 96f, new Color(0f, 0f, 0f, 0f));
            var t = OnlineUi.Cell(row, text, 0f, 28, TextAlignmentOptions.Center, OnlineUi.Dim);
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
