using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the 1v1 menu (placeholder look, the online panels' style): the hub (host a public or private
    /// match, join with a code, the public lobby list, play the practice bot), the lobby (both seats with name#tag, rating
    /// and live status; the invite code and a Copy link button; Ready; Leave) and the draft (six weapons and six Logbooks,
    /// picked from the content and ordered by priority with up and down; Ready sends it). The match itself starts from the
    /// session when the server says so (the run scene loads).
    /// </summary>
    public sealed class PvpMenu : MonoBehaviour
    {
        public const int Picks = 6;

        static readonly Color PickedTint = new Color(1f, 0.82f, 0.4f, 0.35f);
        static readonly Color CardTint = new Color(1f, 1f, 1f, 0.06f);

        RectTransform _hub, _lobby, _draft;
        TextMeshProUGUI _hubMessage, _lobbyTitle, _lobbyMessage, _code, _draftStrip, _draftMessage, _weaponCount, _logbookCount, _draftDetail;
        TMP_InputField _codeInput;
        RectTransform _publicList, _seatList, _weaponGrid, _logbookGrid, _weaponOrder, _logbookOrder;
        Button _readyButton, _draftButton, _botButton, _copyButton, _draftReady;
        readonly List<GameObject> _publicRows = new List<GameObject>(), _seatRows = new List<GameObject>(), _orderRows = new List<GameObject>();
        readonly Dictionary<string, Image> _weaponCards = new Dictionary<string, Image>(), _logbookCards = new Dictionary<string, Image>();
        readonly Dictionary<string, TextMeshProUGUI> _weaponBadges = new Dictionary<string, TextMeshProUGUI>(), _logbookBadges = new Dictionary<string, TextMeshProUGUI>();
        readonly List<string> _weapons = new List<string>(), _logbooks = new List<string>();
        List<WeaponConfig> _allWeapons = new List<WeaponConfig>();
        List<LogbookConfig> _allLogbooks = new List<LogbookConfig>();
        Func<ConfigSetRuntime> _config;
        IconCatalog _icons;
        bool _busy, _gridsBuilt;

        public bool HubOpen => _hub != null && _hub.gameObject.activeSelf;
        public bool LobbyOpen => _lobby != null && _lobby.gameObject.activeSelf;
        public bool DraftOpen => _draft != null && _draft.gameObject.activeSelf;
        public IReadOnlyList<string> DraftWeapons => _weapons;
        public IReadOnlyList<string> DraftLogbooks => _logbooks;
        public string HubMessage => _hubMessage != null ? _hubMessage.text : null;
        public string LobbyMessage => _lobbyMessage != null ? _lobbyMessage.text : null;
        public string DraftMessage => _draftMessage != null ? _draftMessage.text : null;
        public string DraftDetail => _draftDetail != null ? _draftDetail.text : null;
        public int PublicRowCount => _publicRows.Count;

        public static PvpMenu Create(Transform canvas, Func<ConfigSetRuntime> config)
        {
            var go = new GameObject("PlaytestPvpMenu", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var menu = go.AddComponent<PvpMenu>();
            menu._config = config;
            menu._icons = IconCatalog.Load();
            menu.Build(canvas);
            return menu;
        }

        public void SetConfig(Func<ConfigSetRuntime> config) => _config = config;

        void Build(Transform canvas)
        {
            BuildHub(canvas);
            BuildLobby(canvas);
            BuildDraft(canvas);
        }

        void OnEnable()
        {
            var s = PvpSession.Instance;
            s.LobbyChanged += OnLobby;
            s.Notice += OnNotice;
        }

        void OnDisable()
        {
            if (!PvpSession.Exists) return;
            PvpSession.Instance.LobbyChanged -= OnLobby;
            PvpSession.Instance.Notice -= OnNotice;
        }

        // ---- Hub ----

        void BuildHub(Transform canvas)
        {
            _hub = OnlineUi.Panel(canvas, "PlaytestPvpHub", new Vector2(0.16f, 0.06f), new Vector2(0.84f, 0.94f), 222);
            var header = OnlineUi.TopStrip(_hub, "Header", 16f, 80f);
            OnlineUi.TextAt(header, "Title", "PvP (1v1)", 44, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.7f, 1f), true);
            OnlineUi.Button(header, "Close", CloseAll, new Vector2(0.76f, 0.05f), new Vector2(1f, 0.95f));

            var buttons = OnlineUi.TopStrip(_hub, "Host", 112f, OnlineUi.RowHeight);
            OnlineUi.Button(buttons, "Host public", () => HostMatch(true), new Vector2(0f, 0f), new Vector2(0.32f, 1f));
            OnlineUi.Button(buttons, "Host private", () => HostMatch(false), new Vector2(0.34f, 0f), new Vector2(0.66f, 1f));
            OnlineUi.Button(buttons, "Play vs bot", PlayBot, new Vector2(0.68f, 0f), new Vector2(1f, 1f));

            var join = OnlineUi.TopStrip(_hub, "Join", 200f, OnlineUi.RowHeight);
            var h = join.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            _codeInput = OnlineUi.Input(join, "Lobby code (6 letters)", 8);
            _codeInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            _codeInput.onSubmit.AddListener(_ => JoinCode(_codeInput.text));
            var joinButton = OnlineUi.Button(join, "Join with code", () => JoinCode(_codeInput.text));
            joinButton.GetComponent<LayoutElement>().minWidth = 300f;

            var listHeader = OnlineUi.TopStrip(_hub, "PublicHeader", 292f, 56f);
            OnlineUi.TextAt(listHeader, "Label", "Public lobbies", 30, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim, Vector2.zero, new Vector2(0.7f, 1f));
            OnlineUi.Button(listHeader, "Refresh", RefreshPublic, new Vector2(0.76f, 0f), new Vector2(1f, 1f));
            var body = OnlineUi.Area(_hub, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 96f, 24f, 360f));
            _publicList = OnlineUi.ScrollList(body, "Scroll", Vector4.zero);

            _hubMessage = OnlineUi.TextAt(_hub, "Message", string.Empty, 26, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _hubMessage.rectTransform.offsetMin = new Vector2(24f, 20f);
            _hubMessage.rectTransform.offsetMax = new Vector2(-24f, 84f);
            _hubMessage.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>Opens the 1v1 menu where the player is: the lobby (or its draft) when in one, else the hub.</summary>
        public void Open()
        {
            var s = PvpSession.Instance;
            if (s.InLobby) { ShowLobby(); return; }
            Show(_hub);
            _hubMessage.text = OnlineService.IsOnline ? string.Empty : "Connecting...";
            RefreshPublic();
        }

        public void CloseAll()
        {
            _hub.gameObject.SetActive(false);
            _lobby.gameObject.SetActive(false);
            _draft.gameObject.SetActive(false);
        }

        void Show(RectTransform panel)
        {
            CloseAll();
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        async void HostMatch(bool isPublic)
        {
            if (_busy) return;
            _busy = true;
            _hubMessage.text = "Creating a lobby...";
            string error;
            try { error = await PvpSession.Instance.Host(isPublic); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            if (error != null) { _hubMessage.text = error; return; }
            ShowLobby();
        }

        async void PlayBot()
        {
            if (_busy) return;
            _busy = true;
            _hubMessage.text = "Calling the practice bot...";
            string error;
            try { error = await PvpSession.Instance.PlayBot(); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            if (error != null) { _hubMessage.text = error; return; }
            ShowDraft();
        }

        /// <summary>Joins a lobby by code (the field, a public row, or an invite link) and opens the draft.</summary>
        public async void JoinCode(string code)
        {
            if (_busy) return;
            _busy = true;
            if (!HubOpen && !LobbyOpen && !DraftOpen) Show(_hub);
            _hubMessage.text = "Joining...";
            string error;
            try { error = await PvpSession.Instance.Join(code); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            if (error != null) { Show(_hub); _hubMessage.text = error; return; }
            ShowDraft();
        }

        async void RefreshPublic()
        {
            ClearRows(_publicRows);
            ApiResult<PublicLobbiesResponse> r;
            try { r = await PvpSession.Instance.PublicLobbies(); }
            catch (Exception e) { if (this != null) _hubMessage.text = e.Message; return; }
            if (this == null || !HubOpen) return;
            ClearRows(_publicRows);
            if (!r.Ok || r.Data == null)
            {
                _hubMessage.text = r.IsOffline ? "Offline: the PvP server is not reachable." : r.Message;
                return;
            }
            var mine = PvpSession.Instance.MyPlayerId;
            var lobbies = r.Data.lobbies ?? Array.Empty<PublicLobby>();
            foreach (var l in lobbies)
            {
                if (l == null || l.host == null) continue;
                var row = OnlineUi.Row(_publicList, OnlineUi.RowHeight, new Color(1f, 1f, 1f, 0.05f));
                OnlineUi.Cell(row, l.host.name + "<color=#9aa4ad>" + l.host.tag + "</color>", 0f, 30, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor);
                OnlineUi.Cell(row, "rating " + l.host.rating, 200f, 26, TextAlignmentOptions.MidlineRight, OnlineUi.Dim);
                var code = l.code;
                var own = l.host.playerId == mine;
                var b = OnlineUi.Button(row, own ? "Yours" : "Join", () => JoinCode(code));
                b.interactable = !own;
                b.GetComponent<LayoutElement>().minWidth = 160f;
                _publicRows.Add(row.gameObject);
            }
            if (_publicRows.Count == 0 && string.IsNullOrEmpty(_hubMessage.text)) _hubMessage.text = "No public lobbies right now. Host one!";
            else if (_hubMessage.text == "Connecting...") _hubMessage.text = string.Empty;
        }

        // ---- Lobby ----

        void BuildLobby(Transform canvas)
        {
            _lobby = OnlineUi.Panel(canvas, "PlaytestPvpLobby", new Vector2(0.16f, 0.1f), new Vector2(0.84f, 0.9f), 223);
            var header = OnlineUi.TopStrip(_lobby, "Header", 16f, 80f);
            _lobbyTitle = OnlineUi.TextAt(header, "Title", "Lobby", 44, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.7f, 1f), true);
            OnlineUi.Button(header, "Leave", Leave, new Vector2(0.76f, 0.05f), new Vector2(1f, 0.95f));

            var seats = OnlineUi.Area(_lobby, "Seats", new Vector2(0f, 1f), new Vector2(1f, 1f));
            seats.pivot = new Vector2(0.5f, 1f);
            seats.offsetMin = new Vector2(24f, -112f - 2f * 96f);
            seats.offsetMax = new Vector2(-24f, -112f);
            var v = seats.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f;
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            _seatList = seats;

            var invite = OnlineUi.TopStrip(_lobby, "Invite", 330f, OnlineUi.RowHeight);
            _code = OnlineUi.TextAt(invite, "Code", string.Empty, 32, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, Vector2.zero, new Vector2(0.55f, 1f));
            _copyButton = OnlineUi.Button(invite, "Copy link", CopyLink, new Vector2(0.56f, 0f), new Vector2(0.78f, 1f));
            OnlineUi.Button(invite, "Copy code", CopyCode, new Vector2(0.8f, 0f), new Vector2(1f, 1f));

            var actions = OnlineUi.TopStrip(_lobby, "Actions", 420f, OnlineUi.RowHeight);
            _draftButton = OnlineUi.Button(actions, "Pick build", ShowDraft, new Vector2(0f, 0f), new Vector2(0.32f, 1f));
            _readyButton = OnlineUi.Button(actions, "Ready", ReadyFromLobby, new Vector2(0.34f, 0f), new Vector2(0.66f, 1f));
            _botButton = OnlineUi.Button(actions, "Add bot", AddBot, new Vector2(0.68f, 0f), new Vector2(1f, 1f));

            _lobbyMessage = OnlineUi.TextAt(_lobby, "Message", string.Empty, 28, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _lobbyMessage.rectTransform.offsetMin = new Vector2(24f, 24f);
            _lobbyMessage.rectTransform.offsetMax = new Vector2(-24f, 150f);
            _lobbyMessage.textWrappingMode = TextWrappingModes.Normal;
        }

        public void ShowLobby()
        {
            Show(_lobby);
            RefreshLobby(PvpSession.Instance.Lobby);
        }

        void OnLobby(LobbyState state)
        {
            if (this == null) return;
            if (LobbyOpen) RefreshLobby(state);
            if (DraftOpen) RefreshDraftStrip(state);
            // Playtest (4 Oct): a closed or expired lobby goes back to the hub too (its notice shows there).
            if ((state == null || !PvpSession.Instance.InLobby) && (LobbyOpen || DraftOpen)) { Show(_hub); RefreshPublic(); }
        }

        void OnNotice(string text)
        {
            if (this == null) return;
            if (LobbyOpen) _lobbyMessage.text = text;
            else if (DraftOpen) _draftMessage.text = text;
            else if (HubOpen) _hubMessage.text = text;
        }

        static string StatusLabel(LobbySeat seat)
        {
            if (seat == null) return "Waiting for a player";
            switch (seat.status)
            {
                case "joined": return "In the lobby";
                case "drafting": return "Picking weapons";
                case "ready": return "Ready";
                case "in_match": return "In the match";
                case "left": return "Left";
                default: return seat.status;
            }
        }

        static Color StatusColor(LobbySeat seat) =>
            seat == null ? OnlineUi.Dim : seat.status == "ready" ? OnlineUi.Good : seat.status == "left" ? OnlineUi.Bad : OnlineUi.Gold;

        void RefreshLobby(LobbyState state)
        {
            var s = PvpSession.Instance;
            ClearRows(_seatRows);
            if (state == null) return;
            _lobbyTitle.text = (state.@public ? "Public lobby " : "Private lobby ") + state.code;
            _code.text = $"Code <color=#ffd166>{state.code}</color>";
            for (var i = 0; i < 2; i++)
            {
                var seat = state.seats != null && i < state.seats.Length ? state.seats[i] : null;
                var mine = seat != null && seat.playerId == s.MyPlayerId;
                var row = OnlineUi.Row(_seatList, 88f, mine ? new Color(1f, 0.82f, 0.4f, 0.14f) : new Color(1f, 1f, 1f, 0.05f));
                OnlineUi.Cell(row, i == 0 ? "Host" : "Guest", 120f, 24, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim);
                var name = seat == null ? "..." : seat.name + "<color=#9aa4ad>" + seat.tag + "</color>" + (seat.bot ? "  <size=70%>(bot)</size>" : string.Empty) + (mine ? "  <size=70%>(you)</size>" : string.Empty);
                OnlineUi.Cell(row, name, 0f, 32, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor);
                OnlineUi.Cell(row, seat != null ? "rating " + seat.rating : string.Empty, 190f, 26, TextAlignmentOptions.MidlineRight, OnlineUi.Dim);
                var chip = OnlineUi.Cell(row, StatusLabel(seat), 260f, 28, TextAlignmentOptions.MidlineRight, StatusColor(seat));
                chip.fontStyle = FontStyles.Bold;
                _seatRows.Add(row.gameObject);
            }
            var me = s.MySeat;
            var started = state.status == "starting" || state.status == "in_match";
            _readyButton.interactable = !started && me != null && me.status != "ready" && DraftComplete(SavedDraftOrNull());
            _draftButton.interactable = !started;
            _botButton.gameObject.SetActive(s.IsHost && state.status == "waiting");
            if (started) _lobbyMessage.text = "Match starting...";
            else if (state.status == "waiting") _lobbyMessage.text = state.@public ? "Waiting for a player (listed in Public lobbies). Share the code or link." : "Share the code or link with a friend.";
            else if (me != null && me.status == "ready") _lobbyMessage.text = "Ready. Waiting for the other captain...";
            else if (!DraftComplete(SavedDraftOrNull())) _lobbyMessage.text = "Pick your build: 6 weapons and 6 Logbooks.";
            else _lobbyMessage.text = "Press Ready when you are set.";
        }

        void CopyLink()
        {
            var lobby = PvpSession.Instance.Lobby;
            if (lobby == null) return;
            LobbySockets.CopyToClipboard(lobby.inviteUrl);
            _lobbyMessage.text = "Invite link copied: " + lobby.inviteUrl;
        }

        void CopyCode()
        {
            var lobby = PvpSession.Instance.Lobby;
            if (lobby == null) return;
            LobbySockets.CopyToClipboard(lobby.code);
            _lobbyMessage.text = "Code copied: " + lobby.code;
        }

        async void AddBot()
        {
            if (_busy) return;
            _busy = true;
            string error;
            try { error = await PvpSession.Instance.AddBot(); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this != null && error != null) _lobbyMessage.text = error;
        }

        async void Leave()
        {
            try { await PvpSession.Instance.Leave(); }
            catch (Exception e) { Debug.LogWarning("PvpMenu.Leave: " + e.Message); }
            if (this == null) return;
            Show(_hub);
            _hubMessage.text = "Left the lobby.";
            RefreshPublic();
        }

        async void ReadyFromLobby()
        {
            EnsureGrids(); // the content's ids, to check the saved draft against
            var draft = SavedDraftOrNull();
            if (!DraftComplete(draft)) { ShowDraft(); return; }
            await SendReady(draft, m => _lobbyMessage.text = m);
        }

        // ---- Draft ----

        void BuildDraft(Transform canvas)
        {
            _draft = OnlineUi.Panel(canvas, "PlaytestPvpDraft", new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), 224);
            var header = OnlineUi.TopStrip(_draft, "Header", 12f, 72f);
            OnlineUi.TextAt(header, "Title", "Your build", 40, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold, Vector2.zero, new Vector2(0.3f, 1f), true);
            _draftStrip = OnlineUi.TextAt(header, "Strip", string.Empty, 26, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, new Vector2(0.3f, 0f), new Vector2(0.62f, 1f));
            _draftStrip.textWrappingMode = TextWrappingModes.Normal;
            var lobbyButton = OnlineUi.Button(header, "Lobby", ShowLobby, new Vector2(0.64f, 0.05f), new Vector2(0.8f, 0.95f));
            _draftReady = OnlineUi.Button(header, "Ready", ReadyFromDraft, new Vector2(0.82f, 0.05f), new Vector2(1f, 0.95f));
            // Hackathon UI (13 Oct, owner): the Build Setup reference page: parchment sheet, wood header with the title, the
            // lobby line and the Lobby / Ready buttons in it.
            var head = OnlineUi.Sheet(_draft, "Your build", 104f, out _, 1180f);
            if (head != null)
            {
                MenuKit.StyleButton(lobbyButton, "Btn_Parchment");
                MenuKit.ButtonIcon(lobbyButton, "Icon_Chevron_Left", 30f);
                MenuKit.HeaderButton(head, lobbyButton, 330f, 250f, 72f);
                MenuKit.ButtonIcon(_draftReady, "Icon_Check", 30f);
                MenuKit.HeaderButton(head, _draftReady, 30f, 280f, 72f);
                var strip = _draftStrip.rectTransform;
                strip.SetParent(head, false);
                strip.anchorMin = new Vector2(1f, 0f);
                strip.anchorMax = new Vector2(1f, 1f);
                strip.pivot = new Vector2(1f, 0.5f);
                strip.sizeDelta = new Vector2(560f, 0f);
                strip.anchoredPosition = new Vector2(-610f, 0f);
                MenuKit.InkText(_draftStrip, MenuKit.OnWood);
                _draftStrip.alignment = TextAlignmentOptions.MidlineRight;
                _draftStrip.enableAutoSizing = true;
                _draftStrip.fontSizeMin = 18f;
                _draftStrip.fontSizeMax = 28f;
                header.gameObject.SetActive(false);
            }

            // Left: the content to pick from (weapons above, Logbooks below). Right: the two priority lists.
            var left = OnlineUi.Area(_draft, "Pick", new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector4(20f, 156f, 8f, 96f));
            var wLabel = OnlineUi.Area(left, "WeaponsLabel", new Vector2(0f, 1f), new Vector2(1f, 1f));
            wLabel.offsetMin = new Vector2(0f, -40f);
            _weaponCount = OnlineUi.Text(wLabel, "Label", "Weapons", 28, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim);
            _weaponGrid = Grid(OnlineUi.Area(left, "Weapons", new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector4(0f, 6f, 0f, 44f)));
            var lLabel = OnlineUi.Area(left, "LogbooksLabel", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            lLabel.offsetMin = new Vector2(0f, -40f);
            _logbookCount = OnlineUi.Text(lLabel, "Label", "Logbooks", 28, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim);
            _logbookGrid = Grid(OnlineUi.Area(left, "Logbooks", new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector4(0f, 0f, 0f, 44f)));

            var right = OnlineUi.Area(_draft, "Order", new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector4(8f, 156f, 20f, 96f));
            var orderLabel = OnlineUi.Area(right, "Label", new Vector2(0f, 1f), new Vector2(1f, 1f));
            orderLabel.offsetMin = new Vector2(0f, -40f);
            OnlineUi.Text(orderLabel, "Text", "Priority (level-ups go weapon 1, Logbook 1, weapon 2, ...)", 22, TextAlignmentOptions.MidlineLeft, OnlineUi.Dim);
            _weaponOrder = OnlineUi.ScrollList(OnlineUi.Area(right, "WeaponOrder", new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector4(0f, 4f, 0f, 44f)), "Scroll", Vector4.zero);
            _logbookOrder = OnlineUi.ScrollList(OnlineUi.Area(right, "LogbookOrder", new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector4(0f, 0f, 0f, 4f)), "Scroll", Vector4.zero);

            _draftMessage = OnlineUi.TextAt(_draft, "Message", string.Empty, 26, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0f), new Vector2(1f, 0f));
            _draftMessage.rectTransform.offsetMin = new Vector2(24f, 12f);
            _draftMessage.rectTransform.offsetMax = new Vector2(-24f, 60f);

            // Playtest (4 Oct, owner): the full description of the hovered or last tapped weapon or Logbook.
            var detail = OnlineUi.Area(_draft, "Detail", new Vector2(0f, 0f), new Vector2(1f, 0f));
            detail.offsetMin = new Vector2(20f, 62f);
            detail.offsetMax = new Vector2(-20f, 150f);
            detail.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            MenuKit.Paint(detail, "Panel_Inset", 2f);
            _draftDetail = OnlineUi.Text(detail, "Text", DetailHint, 24, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor);
            _draftMessage.fontStyle = FontStyles.Bold;
            _draftDetail.margin = new Vector4(16f, 4f, 16f, 4f);
            _draftDetail.textWrappingMode = TextWrappingModes.Normal;
            _draftDetail.enableAutoSizing = true;
            _draftDetail.fontSizeMin = 18f;
            _draftDetail.fontSizeMax = 24f;
        }

        const string DetailHint = "<color=#9aa4ad>Hover or tap a weapon or Logbook to read what it does.</color>";

        static RectTransform Grid(RectTransform area)
        {
            var list = OnlineUi.ScrollList(area, "Scroll", Vector4.zero);
            DestroyImmediate(list.GetComponent<VerticalLayoutGroup>());
            var grid = list.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(262f, 96f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            list.gameObject.AddComponent<GridFit>(); // Hackathon UI: the cards fill the width (5 columns on 20:9)
            return list;
        }

        /// <summary>Hackathon UI (13 Oct, owner): a card grid as wide as its list (columns of at least 250 units, stretched
        /// to fill), so wide screens show more cards per row instead of an empty strip.</summary>
        sealed class GridFit : MonoBehaviour
        {
            const float MinCell = 250f;
            void OnEnable() => Fit();
            void OnRectTransformDimensionsChange() => Fit();

            void Fit()
            {
                var g = GetComponent<GridLayoutGroup>();
                var rt = (RectTransform)transform;
                if (g == null) return;
                var w = rt.rect.width - g.padding.left - g.padding.right;
                if (w <= 1f) return;
                var cols = Mathf.Max(1, Mathf.FloorToInt((w + g.spacing.x) / (MinCell + g.spacing.x)));
                var cell = Mathf.Floor((w - g.spacing.x * (cols - 1)) / cols);
                if (g.constraintCount == cols && Mathf.Abs(g.cellSize.x - cell) < 0.5f) return;
                g.constraintCount = cols;
                g.cellSize = new Vector2(cell, g.cellSize.y);
            }
        }

        public void ShowDraft()
        {
            Show(_draft);
            EnsureGrids();
            LoadDraft();
            RefreshDraft();
            RefreshDraftStrip(PvpSession.Instance.Lobby);
            _draftMessage.text = "Pick 6 weapons and 6 Logbooks (all start at level 1), then order them.";
            if (_draftDetail != null) _draftDetail.text = MenuKit.InkRich(DetailHint);
            var me = PvpSession.Instance.MySeat;
            if (me != null && me.status != "drafting" && PvpSession.Instance.Lobby?.status != "starting") _ = PvpSession.Instance.SetStatus("drafting");
        }

        void EnsureGrids()
        {
            if (_gridsBuilt) return;
            var config = _config?.Invoke();
            if (config == null) { _draftMessage.text = "Content not loaded."; return; }
            _gridsBuilt = true;
            _allWeapons = OfferGenerator.InDatabaseOrder(config.All<WeaponConfig>()).Where(w => w != null && !string.IsNullOrEmpty(w.weaponId)).ToList();
            _allLogbooks = OfferGenerator.InDatabaseOrder(config.All<LogbookConfig>()).Where(l => l != null && !string.IsNullOrEmpty(l.logbookId)).ToList();
            foreach (var w in _allWeapons) if (RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(w.weaponId)) Card(_weaponGrid, w.weaponId, Name(w), ShortLine(w), _icons != null ? _icons.Weapon(w.weaponId) : null, true);
            foreach (var l in _allLogbooks) Card(_logbookGrid, l.logbookId, Name(l), ShortLine(l), _icons != null ? _icons.Logbook(l.logbookId) : null, false);
        }

        static string Name(WeaponConfig w) => string.IsNullOrEmpty(w.displayName) ? w.weaponId : w.displayName;
        static string Name(LogbookConfig l) => string.IsNullOrEmpty(l.displayName) ? l.logbookId : l.displayName;

        // Playtest (4 Oct, owner): the descriptions while drafting (a short line on each card, the full text in the detail strip).
        static string Plain(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(" \u2014 ", ", ").Replace("\u2014", ", ").Replace("\u2013", "-").Trim();

        static string ShortLine(WeaponConfig w) => Plain(!string.IsNullOrEmpty(w.behaviorLine) ? w.behaviorLine : w.description);

        static string ShortLine(LogbookConfig l) =>
            l.randomStat ? "A random Logbook stat per rank" : // Hackathon (13 Oct): Chaos
            l.stat < 0 ? "No effect" : StatDisplay.FormatLogbookBonus((StatId)l.stat, l.perLevelAdditive) + " per rank";

        static string Detail(WeaponConfig w)
        {
            var parts = new List<string>();
            var d = Plain(w.description);
            var b = Plain(w.behaviorLine);
            if (d.Length > 0) parts.Add(d);
            if (b.Length > 0 && !d.Contains(b)) parts.Add(b + ".");
            var stats = CardStatLines.NewWeapon(w);
            if (stats != null && stats.Length > 0) parts.Add("<color=#9aa4ad>Level 1: " + string.Join(", ", stats) + "</color>");
            return "<color=#ffd166><b>" + Name(w) + "</b></color>  <color=#9aa4ad>Weapon</color>\n" + string.Join("  ", parts);
        }

        static string Detail(LogbookConfig l)
        {
            var body = l.randomStat ? "Each rank adds the stat of a random Logbook, rolled when the rank is gained, up to " + l.maxLevel + " ranks." // Hackathon: Chaos
                : l.stat < 0 ? "Does nothing in this build."
                : StatDisplay.FormatLogbookBonus((StatId)l.stat, l.perLevelAdditive) + (l.stat == (int)StatId.Hull ? " (also heals)" : string.Empty)
                  + " for each rank, up to " + l.maxLevel + " ranks.";
            return "<color=#ffd166><b>" + Name(l) + "</b></color>  <color=#9aa4ad>Logbook</color>\n" + body;
        }

        /// <summary>Shows the full description of a weapon or Logbook in the draft's detail strip.</summary>
        public void ShowDetail(string id, bool weapon)
        {
            if (_draftDetail == null) return;
            if (weapon) { var w = _allWeapons.Find(x => x.weaponId == id); if (w != null) _draftDetail.text = MenuKit.InkRich(Detail(w)); }
            else { var l = _allLogbooks.Find(x => x.logbookId == id); if (l != null) _draftDetail.text = MenuKit.InkRich(Detail(l)); }
        }

        void Card(RectTransform grid, string id, string label, string line, Sprite icon, bool weapon)
        {
            var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(grid, false);
            var image = go.GetComponent<Image>();
            image.color = CardTint;
            CardLook(image, false);
            go.GetComponent<Button>().onClick.AddListener(() => { Toggle(id, weapon); ShowDetail(id, weapon); });
            // Hover (desktop) shows the description too; a tap (phone) shows it with the pick.
            go.AddComponent<HoverRelay>().Enter = () => ShowDetail(id, weapon);
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ig.transform.SetParent(go.transform, false);
                var irt = (RectTransform)ig.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.sizeDelta = new Vector2(60f, 60f);
                irt.anchoredPosition = new Vector2(8f, 0f);
                var img = ig.GetComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            var left = icon != null ? 76f : 12f;
            var text = OnlineUi.TextAt(go.transform, "Name", label, 22, TextAlignmentOptions.BottomLeft, OnlineUi.TextColor, new Vector2(0f, 0.56f), Vector2.one);
            text.margin = new Vector4(left, 0f, 30f, 0f);
            text.fontStyle = FontStyles.Bold;
            // Hackathon UI (13 Oct, Seeker): long names ("Broadside Battery", "Flying Cutlasses") shrink to fit instead of "...".
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 22f;
            var desc = OnlineUi.TextAt(go.transform, "Line", line ?? string.Empty, 17, TextAlignmentOptions.TopLeft, OnlineUi.Dim, Vector2.zero, new Vector2(1f, 0.54f));
            desc.margin = new Vector4(left, 0f, 34f, 4f); // Hackathon UI: clear of the pick number
            desc.textWrappingMode = TextWrappingModes.Normal;
            var badge = OnlineUi.TextAt(go.transform, "Badge", string.Empty, 26, TextAlignmentOptions.MidlineRight, OnlineUi.Gold, Vector2.zero, Vector2.one);
            badge.margin = new Vector4(0f, 0f, 10f, 0f);
            badge.fontStyle = FontStyles.Bold;
            (weapon ? _weaponCards : _logbookCards)[id] = image;
            (weapon ? _weaponBadges : _logbookBadges)[id] = badge;
        }

        /// <summary>Pointer enter only (an EventTrigger would also take the drag and scroll events from the card grid's ScrollRect).</summary>
        sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            public Action Enter;
            public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
        }

        /// <summary>The saved draft without switched-off weapons (the Flamethrower) or ids the content no longer has; fewer
        /// than six left makes it incomplete (Ready then asks for the draft again).</summary>
        PvpDraft SavedDraftOrNull()
        {
            var d = PvpSession.Instance.SavedDraft;
            if (d == null) return null;
            return new PvpDraft
            {
                // V5 M8z (8 Oct, close): an old id in the saved draft is its replacement (the Ramming Prow is the Ink Pots).
                weapons = (d.weapons ?? Array.Empty<string>()).Select(RoguePirates.V4.Foundation.Core.ContentIdAliases.Resolve).Where(w => !string.IsNullOrEmpty(w)
                    && RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(w)
                    && (_allWeapons.Count == 0 || _allWeapons.Exists(x => x.weaponId == w))).Distinct().ToArray(),
                logbooks = (d.logbooks ?? Array.Empty<string>()).Select(RoguePirates.V4.Foundation.Core.ContentIdAliases.Resolve).Where(l => !string.IsNullOrEmpty(l)
                    && (_allLogbooks.Count == 0 || _allLogbooks.Exists(x => x.logbookId == l))).Distinct().ToArray(),
            };
        }

        void LoadDraft()
        {
            _weapons.Clear();
            _logbooks.Clear();
            var saved = SavedDraftOrNull();
            if (saved?.weapons != null) foreach (var w in saved.weapons) if (_weaponCards.ContainsKey(w) && !_weapons.Contains(w) && _weapons.Count < Picks) _weapons.Add(w);
            if (saved?.logbooks != null) foreach (var l in saved.logbooks) if (_logbookCards.ContainsKey(l) && !_logbooks.Contains(l) && _logbooks.Count < Picks) _logbooks.Add(l);
        }

        /// <summary>Picks or drops a weapon or Logbook (a pick goes to the end of its priority list).</summary>
        public void Toggle(string id, bool weapon)
        {
            var list = weapon ? _weapons : _logbooks;
            if (list.Remove(id)) { Changed(); return; }
            if (list.Count >= Picks) { _draftMessage.text = weapon ? "Six weapons already: drop one first." : "Six Logbooks already: drop one first."; return; }
            list.Add(id);
            Changed();
        }

        public void Move(string id, bool weapon, int delta)
        {
            var list = weapon ? _weapons : _logbooks;
            var i = list.IndexOf(id);
            var j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count) return;
            (list[i], list[j]) = (list[j], list[i]);
            Changed();
        }

        void Changed()
        {
            _draftMessage.text = string.Empty;
            RefreshDraft();
            // Changing the build un-readies (the server keeps the last draft sent with Ready).
            var me = PvpSession.Instance.MySeat;
            if (me != null && me.status == "ready") _ = PvpSession.Instance.SetStatus("drafting");
        }

        void RefreshDraft()
        {
            foreach (var kv in _weaponCards)
            {
                var i = _weapons.IndexOf(kv.Key);
                kv.Value.color = i >= 0 ? PickedTint : CardTint;
                CardLook(kv.Value, i >= 0);
                _weaponBadges[kv.Key].text = i >= 0 ? (i + 1).ToString() : string.Empty;
            }
            foreach (var kv in _logbookCards)
            {
                var i = _logbooks.IndexOf(kv.Key);
                kv.Value.color = i >= 0 ? PickedTint : CardTint;
                CardLook(kv.Value, i >= 0);
                _logbookBadges[kv.Key].text = i >= 0 ? (i + 1).ToString() : string.Empty;
            }
            _weaponCount.text = MenuKit.InkRich($"Weapons  <color=#ffd166>{_weapons.Count}/{Picks}</color>");
            _logbookCount.text = MenuKit.InkRich($"Logbooks  <color=#ffd166>{_logbooks.Count}/{Picks}</color>");
            ClearRows(_orderRows);
            OrderRows(_weaponOrder, _weapons, true);
            OrderRows(_logbookOrder, _logbooks, false);
            _draftReady.interactable = _weapons.Count == Picks && _logbooks.Count == Picks;
        }

        void OrderRows(RectTransform list, List<string> ids, bool weapon)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var row = OnlineUi.Row(list, 56f, new Color(1f, 1f, 1f, 0.05f));
                OnlineUi.Cell(row, (weapon ? "W" : "L") + (i + 1), 60f, 24, TextAlignmentOptions.MidlineLeft, OnlineUi.Gold);
                OnlineUi.Cell(row, Label(id, weapon), 0f, 24, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor);
                SmallButton(row, "Up", () => Move(id, weapon, -1), i > 0);
                SmallButton(row, "Down", () => Move(id, weapon, 1), i < ids.Count - 1);
                SmallButton(row, "X", () => Toggle(id, weapon), true);
                _orderRows.Add(row.gameObject);
            }
        }

        string Label(string id, bool weapon)
        {
            if (weapon) { var w = _allWeapons.Find(x => x.weaponId == id); return w != null ? Name(w) : id; }
            var l = _allLogbooks.Find(x => x.logbookId == id);
            return l != null ? Name(l) : id;
        }

        /// <summary>Hackathon UI: a draft card on the kit's slot card (gold when picked); without the kit, the tint.</summary>
        static void CardLook(Image image, bool picked)
        {
            if (!MenuKit.Ready) return;
            MenuKit.Paint(image, picked ? "Slot_Card_Selected" : "Slot_Card", 2.4f);
            image.raycastTarget = true;
        }

        static void SmallButton(Transform row, string label, Action click, bool enabled)
        {
            var b = OnlineUi.Button(row, label, click);
            MenuKit.StyleButton(b, label == "X" ? "Btn_Red" : "Btn_Parchment");
            var le = b.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = label.Length > 1 ? 104f : 64f;
            le.minHeight = le.preferredHeight = 48f;
            b.interactable = enabled;
            var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.fontSize = 22;
        }

        void RefreshDraftStrip(LobbyState state)
        {
            if (_draftStrip == null) return;
            var s = PvpSession.Instance;
            if (state == null) { _draftStrip.text = "Not in a lobby."; return; }
            var other = s.OpponentSeat;
            _draftStrip.text = "Lobby " + state.code + "   " + (other == null ? "<color=#d9c6a5>Waiting for a player</color>"
                : other.name + other.tag + ": <b>" + StatusLabel(other) + "</b>");
        }

        async void ReadyFromDraft()
        {
            if (_weapons.Count != Picks || _logbooks.Count != Picks) { _draftMessage.text = "Pick 6 weapons and 6 Logbooks first."; return; }
            var draft = new PvpDraft { weapons = _weapons.ToArray(), logbooks = _logbooks.ToArray() };
            PvpSession.Instance.SavedDraft = draft;
            await SendReady(draft, m => _draftMessage.text = m);
            if (this != null && PvpSession.Instance.MySeat?.status == "ready") ShowLobby();
        }

        async Awaitable SendReady(PvpDraft draft, Action<string> message)
        {
            if (_busy) return;
            _busy = true;
            message("Sending your build...");
            string error;
            try { error = await PvpSession.Instance.SetStatus("ready", draft); }
            catch (Exception e) { error = e.Message; }
            _busy = false;
            if (this == null) return;
            message(error ?? "Ready!");
        }

        static bool DraftComplete(PvpDraft d) => d != null && d.weapons != null && d.logbooks != null && d.weapons.Length == Picks && d.logbooks.Length == Picks;

        static void ClearRows(List<GameObject> rows)
        {
            foreach (var r in rows) if (r != null) { r.SetActive(false); Destroy(r); }
            rows.Clear();
        }
    }
}
