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
            Kit.Paint(Kit.Fill(rt, "Sheet"), "small_scroll_bg_2", 1.25f, new Color(0.95f, 0.87f, 0.7f, 1f));
            var c = _content = Kit.Fill(rt, "Content", 104f, 44f, 104f, 44f);

            var head = Kit.Strip(c, "Header", 0f, 0f, 0f, 100f);
            Kit.Paint(head, "Header_Wood", 1.4f, new Color(0.42f, 0.26f, 0.14f, 1f));
            Kit.Medallion(head, -14f, -10f, 120f);
            _title = Kit.Label(Kit.Strip(head, "Title", 122f, 0f, 500f, 100f), "Weekly Leaderboard", 54f, Kit.OnWood, TextAlignmentOptions.MidlineLeft, true, 30f);
            _close = Kit.KitButton(Kit.TopRight(head, "Close", 16f, 12f, 200f, 76f), "Close", "Icon_Close", Close);
            _toggle = Kit.KitButton(Kit.TopRight(head, "Last week", 230f, 12f, 250f, 76f), "Last week", null, Toggle);

            var reset = Kit.Strip(c, "Reset", 0f, 110f, 0f, 56f);
            Kit.Paint(reset, "Panel_Inset", 2.4f, new Color(0.97f, 0.92f, 0.82f, 1f));
            Kit.Picture(Kit.TopLeft(reset, "Clock", 18f, 8f, 40f, 40f), "Icon_Clock", Kit.Ink);
            _reset = Kit.Label(Kit.Strip(reset, "Text", 72f, 0f, 300f, 56f), string.Empty, 26f, Kit.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
            _status = Kit.Label(Kit.TopRight(reset, "Status", 20f, 0f, 280f, 56f), string.Empty, 22f, Kit.InkDim, TextAlignmentOptions.MidlineRight, true, 14f);

            var columns = Kit.Strip(c, "Columns", 0f, 174f, 22f, 52f);
            Kit.Paint(columns, "Panel_Inset", 2.4f, new Color(0.86f, 0.76f, 0.6f, 1f));
            Kit.Columns(columns);
            Cells(columns, Kit.Ink, 24, "#", "Player", "Wallet", "Kills", "Time", "Lvl", "Captain", "Reward");

            _body = Kit.Fill(c, "Body", 0f, FooterHeight + 10f, 0f, ListTop);
            _list = Kit.Scroll(_body);

            var foot = Kit.BottomStrip(c, "Footer", 0f, 0f, 0f, FooterHeight);
            Kit.Paint(foot, "Panel_Inset", 2.4f, new Color(1f, 0.86f, 0.45f, 1f));
            var you = Kit.Node(foot, "You");
            you.anchorMin = you.anchorMax = you.pivot = new Vector2(0f, 0.5f);
            you.anchoredPosition = new Vector2(10f, 0f);
            you.sizeDelta = new Vector2(150f, 60f);
            Kit.Paint(you, "Banner_Maroon", 2.4f, null, new Color(0.45f, 0.16f, 0.12f, 1f));
            Kit.Label(Kit.Fill(you, "Text"), "You", 32f, Kit.OnWood, TextAlignmentOptions.Center, true);
            _me = Kit.Label(Kit.Fill(foot, "Me", 182f, 0f, 20f, 0f), string.Empty, 28f, Kit.Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
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
            Kit.TextCell(row, rank, RankW, size, TextAlignmentOptions.MidlineLeft, color, true);
            Kit.TextCell(row, name, 0f, size, TextAlignmentOptions.MidlineLeft, color, true);
            Kit.TextCell(row, wallet, WalletW, size, TextAlignmentOptions.MidlineLeft, color, true);
            Kit.TextCell(row, kills, KillsW, size, TextAlignmentOptions.Center, color, true);
            Kit.TextCell(row, time, TimeW, size, TextAlignmentOptions.Center, color, true);
            Kit.TextCell(row, level, LevelW, size, TextAlignmentOptions.Center, color, true);
            Kit.TextCell(row, captain, CaptainW, size, TextAlignmentOptions.Center, color, true);
            Kit.TextCell(row, reward, RewardW, size, TextAlignmentOptions.Center, color, true);
        }

        /// <summary>Hackathon (12 Oct): the wallet (short form).</summary>
        static string Wallet(LeaderboardEntry e) =>
            string.IsNullOrEmpty(e.wallet) ? "-" : e.wallet.Length > 12 ? e.wallet.Substring(0, 4) + "..." + e.wallet.Substring(e.wallet.Length - 4) : e.wallet;

        /// <summary>Hackathon (12 Oct): the place's devnet SOL reward.</summary>
        static string Reward(LeaderboardEntry e)
        {
            if (e.reward == null || e.reward.lamports <= 0) return "-";
            var sol = "<color=" + Kit.GoldHex + ">" + e.reward.sol.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " SOL</color>";
            return e.reward.status == "sent" ? sol + " <size=70%><color=" + Kit.GoodHex + ">paid</color></size>" : sol;
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
            Kit.SetLabel(_toggle, _previous ? "This week" : "Last week");
            _status.text = "Loading...";
            _status.color = Kit.InkDim;
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
                _status.color = Kit.InkBad;
                Message(r.IsOffline ? "Offline: the leaderboard needs a connection. Your runs still count on this device." : "The leaderboard is unavailable: " + r.Message);
                _me.text = OnlineService.Active != null ? OnlineService.Active.Display : string.Empty;
                _weekEnd = null;
                UpdateReset();
                return;
            }
            var data = r.Data;
            _status.text = "Online";
            _status.color = Kit.InkGood;
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
                    var row = Kit.Row(_list, 62f, tint);
                    Kit.RankCell(row, e.rank, RankW, 30f);
                    // V5 M9a (I9a.3): "checked" when the run passed the server's run checks (shadow mode: the rest still rank)
                    Kit.TextCell(row, e.name + "<color=" + Kit.DimHex + ">" + e.tag + "</color>" + (e.@checked ? CheckedMark : "") + (mine ? YouMark : ""), 0f, 28f,
                        TextAlignmentOptions.MidlineLeft, Kit.Ink, true);
                    Kit.TextCell(row, Wallet(e), WalletW, 24f, TextAlignmentOptions.MidlineLeft, Kit.InkDim);
                    Kit.TextCell(row, e.kills.ToString(), KillsW, 28f, TextAlignmentOptions.Center, Kit.Ink, true);
                    Kit.TextCell(row, OnlineRules.RunTime(e.runSeconds), TimeW, 28f, TextAlignmentOptions.Center, Kit.Ink);
                    Kit.TextCell(row, e.level.ToString(), LevelW, 28f, TextAlignmentOptions.Center, Kit.Ink);
                    Kit.TextCell(row, Captain(e.captain), CaptainW, 28f, TextAlignmentOptions.Center, Kit.Ink);
                    Kit.TextCell(row, Reward(e), RewardW, 28f, TextAlignmentOptions.Center, Kit.Ink, true);
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

        const string CheckedMark = "  <size=65%><color=" + Kit.GoodHex + ">checked</color></size>";
        const string YouMark = "  <size=65%><color=" + Kit.GoodHex + ">(you)</color></size>";

        static string Captain(string key) =>
            string.IsNullOrEmpty(key) ? "-" : char.ToUpperInvariant(key[0]) + key.Substring(1);

        void Message(string text)
        {
            var row = Kit.Row(_list, 96f, null);
            var t = Kit.TextCell(row, text, 0f, 28f, TextAlignmentOptions.Center, Kit.InkDim);
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

    /// <summary>
    /// Hackathon UI (13 Oct): the parchment kit for the weekly board. V4 does not reference the hackathon assembly, so the
    /// kit's sprites and font are read from the same Resources asset (Resources/HackSkin: its public "sprites" and "font"
    /// fields) by name; without it the board draws with flat colours.
    /// </summary>
    internal static class Kit
    {
        public static readonly Color Ink = new Color(0.24f, 0.14f, 0.06f, 1f);
        public static readonly Color InkDim = new Color(0.48f, 0.35f, 0.23f, 1f);
        public static readonly Color InkGood = new Color(0.17f, 0.45f, 0.14f, 1f);
        public static readonly Color InkBad = new Color(0.66f, 0.16f, 0.09f, 1f);
        public static readonly Color OnWood = new Color(1f, 0.94f, 0.8f, 1f);
        public const string GoldHex = "#8c4d05", DimHex = "#7a5a3a", GoodHex = "#2b7324";

        static Dictionary<string, Sprite> _sprites;
        static TMP_FontAsset _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sprites = null;
            _font = null;
        }

        static void Load()
        {
            if (_sprites != null) return;
            _sprites = new Dictionary<string, Sprite>();
            var skin = Resources.Load<ScriptableObject>("HackSkin");
            if (skin == null) return;
            var type = skin.GetType();
            if (type.GetField("sprites")?.GetValue(skin) is Sprite[] all)
                foreach (var sp in all)
                    if (sp != null) _sprites[sp.name] = sp;
            _font = type.GetField("font")?.GetValue(skin) as TMP_FontAsset;
        }

        public static Sprite S(string name)
        {
            Load();
            return !string.IsNullOrEmpty(name) && _sprites.TryGetValue(name, out var sp) ? sp : null;
        }

        static TMP_FontAsset Font
        {
            get { Load(); return _font != null ? _font : OnlineUi.TitleFont; }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform TopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform TopRight(Transform parent, string name, float right, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Strip(Transform parent, string name, float left, float y, float right, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        public static RectTransform BottomStrip(Transform parent, string name, float left, float bottom, float right, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + h);
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Kit sprite on <paramref name="rt"/> (9-sliced when it has borders); without it, <paramref name="fallback"/>.</summary>
        public static Image Paint(RectTransform rt, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var s = S(sprite);
            if (s != null)
            {
                img.sprite = s;
                img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = thin;
                img.preserveAspect = img.type == Image.Type.Simple;
                img.color = tint ?? Color.white;
            }
            else img.color = fallback ?? tint ?? new Color(0.9f, 0.8f, 0.6f, 1f);
            return img;
        }

        public static Image Picture(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = Paint(rt, sprite, 1f, tint, new Color(0f, 0f, 0f, 0f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, float minSize = 0f)
        {
            // A rect that already draws (a kit sprite) cannot take a text as well: the text goes on a child filling it.
            if (rt.GetComponent<Graphic>() != null) rt = Fill(rt, "Text");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

        /// <summary>A gold kit button filling <paramref name="rt"/>, with an optional kit icon left of the label.</summary>
        public static Button KitButton(RectTransform rt, string label, string icon, Action click)
        {
            var img = Paint(rt, "Btn_Gold", 1f, null, new Color(0.95f, 0.75f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var pressed = S("Btn_Gold_Pressed");
            if (img.sprite != null && pressed != null)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = pressed, highlightedSprite = img.sprite, selectedSprite = img.sprite, disabledSprite = S("Btn_Gold_Disabled") };
            }
            if (click != null) b.onClick.AddListener(() => click());
            var content = Fill(rt, "Content", 10f, 4f, 10f, 2f);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon) && S(icon) != null)
            {
                var ic = Node(content, "Icon");
                Picture(ic, icon, Ink);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 34f;
            }
            Label(Node(content, "Label"), label, 30f, Ink, TextAlignmentOptions.MidlineLeft, true);
            return b;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (t != null) t.text = text;
        }

        /// <summary>The medallion with the skull (a parchment disc under the dark glyph).</summary>
        public static void Medallion(Transform parent, float x, float y, float size)
        {
            var medal = TopLeft(parent, "Medallion", x, y, size, size);
            var frame = Picture(medal, "Medallion_Frame");
            var inset = size * 0.23f;
            if (frame.sprite != null) Picture(Fill(medal, "Disc", inset, inset, inset, inset), "Dot_Beige", new Color(0.96f, 0.88f, 0.7f, 1f));
            var g = size * 0.3f;
            Picture(Fill(medal, "Skull", g, g, g, g), "Icon_Skull_Crossbones", Ink);
        }

        /// <summary>A vertical scroll list filling <paramref name="host"/> (kit scroll bar on the right); returns the content.</summary>
        public static RectTransform Scroll(RectTransform host)
        {
            var sr = host.gameObject.AddComponent<ScrollRect>();
            var viewport = Fill(host, "Viewport", 0f, 0f, 22f, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.padding = new RectOffset(0, 0, 2, 4);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = Node(host, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(14f, 0f);
            bar.anchoredPosition = Vector2.zero;
            Paint(bar, "Scroll_Track", 1f, new Color(0.55f, 0.4f, 0.25f, 0.55f), new Color(0.4f, 0.28f, 0.16f, 0.4f)).raycastTarget = true;
            var handle = Fill(Fill(bar, "Sliding Area"), "Handle");
            var himg = Paint(handle, "Scroll_Thumb", 1f, null, new Color(0.95f, 0.72f, 0.25f, 1f));
            himg.raycastTarget = true;
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = himg;
            sb.direction = Scrollbar.Direction.BottomToTop;

            sr.content = content;
            sr.viewport = viewport;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>A parchment table row (no art when <paramref name="tint"/> is null).</summary>
        public static RectTransform Row(Transform list, float height, Color? tint)
        {
            var rt = Node(list, "Row");
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            if (tint.HasValue) Paint(rt, "Panel_Inset", 2.4f, tint, tint);
            Columns(rt);
            return rt;
        }

        public static void Columns(RectTransform rt)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(18, 18, 0, 0);
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
        }

        public static RectTransform Cell(Transform row, float width)
        {
            var rt = Node(row, "Cell");
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = le.minWidth = width;
            else { le.flexibleWidth = 1f; le.minWidth = 80f; }
            return rt;
        }

        public static TextMeshProUGUI TextCell(Transform row, string text, float width, float size, TextAlignmentOptions align, Color color, bool bold = false) =>
            Label(Cell(row, width), text, size, color, align, bold, size * 0.6f);

        /// <summary>The rank cell: the kit crown for places 1 to 3, then the number.</summary>
        public static void RankCell(Transform row, int rank, float width, float size)
        {
            var cell = Cell(row, width);
            var crown = rank == 1 ? "Crown_Gold" : rank == 2 ? "Crown_Silver" : rank == 3 ? "Crown_Bronze" : null;
            if (crown != null && S(crown) != null)
            {
                var c = Node(cell, "Crown");
                c.anchorMin = new Vector2(0f, 0.12f);
                c.anchorMax = new Vector2(0f, 0.88f);
                c.pivot = new Vector2(0f, 0.5f);
                c.sizeDelta = new Vector2(50f, 0f);
                c.anchoredPosition = Vector2.zero;
                Picture(c, crown);
            }
            Label(Fill(cell, "Rank", 60f), rank.ToString(), size, Ink, TextAlignmentOptions.MidlineLeft, true);
        }
    }
}
