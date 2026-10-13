using System;
using System.Collections.Generic;
using RoguePirates.UI;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static RoguePirates.UI.UiKit;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): 1v1 SOL wagers and the AI captain on V4's 1v1 menu (<see cref="PvpMenu"/>), added from here so
    /// the V4 menu code is unchanged:
    /// <list type="bullet">
    /// <item>the hub gets a stake picker (none, 0.01, 0.05, 0.1 SOL); Host public / private set the stake on the new lobby
    /// (POST /api/wagers/{lobbyId}); "Play vs bot" becomes "Vs Captain Marrow" (13 Oct, server rule: bot matches carry no
    /// stake, so it is offered with No stake only);</item>
    /// <item>the lobby gets the AI captain's banner and a wager strip: the stake and pot and both seats' deposits;</item>
    /// <item>the deposit is part of creating or joining a staked match (owner, 12 Oct): the wallet asks to sign the stake
    /// transfer (to the treasury with the memo wager:&lt;lobbyId&gt;) right away and the server verifies it. A cancelled or
    /// failed deposit leaves the lobby cleanly (nothing was paid) with a clear message. Only when the transfer was sent but
    /// devnet has not confirmed it yet does the lobby stay, with "Check deposit" (no second payment);</item>
    /// <item>the hub shows the last wager's result and payout with an explorer link.</item>
    /// </list>
    /// Hackathon UI (13 Oct, owner's reference pages "PvP", "PvP public lobby", "PvP private lobby"): the hub and the lobby
    /// are redrawn in the parchment kit (<see cref="RoguePirates.UI.UiKit"/>). The V4 panels stay as the containers and the logic: their
    /// own controls are hidden and the kit buttons call them (Close, Leave, Copy link / code, Pick build, Ready, Add bot),
    /// their message lines are kept (moved into the kit layout). The draft screen is unchanged.
    /// </summary>
    public sealed class WagerMenu : MonoBehaviour
    {
        // Both panels (canvas units; the menu canvas is at least 1920 x 1080 on desktop and phones).
        static readonly Vector2 PanelSize = new Vector2(1600f, 990f);
        const float HeadH = 130f;

        PvpMenu _pvp;
        RectTransform _hub, _lobby;
        TextMeshProUGUI _hubMessage, _lobbyMessage;
        string _hubSet, _lobbySet;
        readonly Button[] _stakeButtons = new Button[4];
        readonly Button[] _modeButtons = new Button[3];
        int _modeIndex;
        Button _start;
        int _stakeIndex;
        // 13 Oct (review): Host / Vs captain (_hosting) and the deposit (_depositing) are separate steps: the deposit's end
        // no longer clears the host step's guard. _busy is either.
        bool _hosting, _depositing, _leaving;
        bool _busy => _hosting || _depositing || _leaving;

        // Hub.
        TMP_InputField _codeInput;
        RectTransform _publicList;
        Button _v4Refresh;
        bool _hubWasOpen, _lobbyWasOpen;
        int _listVersion;

        // Lobby.
        TextMeshProUGUI _lobbyTitle, _lobbyTag, _players, _codeBig;
        RectTransform _codeSection;
        readonly SeatRow[] _seats = new SeatRow[2];
        Button _pick, _ready, _addBot, _v4Leave, _v4CopyLink, _v4CopyCode, _v4Pick, _v4Ready, _v4Bot;

        RectTransform _banner, _strip, _status;
        TextMeshProUGUI _bannerText, _wagerText, _statusText;
        Button _deposit, _explorer, _hubExplorer;
        string _explorerUrl, _hubExplorerUrl;
        float _nextPoll;
        string _lastLobbyId;
        // The last deposit attempt (13 Oct, review: the whole result: sent, refunded, final line, signature, pending).
        HackathonApi.DepositResult _last;

        sealed class SeatRow
        {
            public RectTransform Root;
            public Image Icon, Dot, Chip, ChipDot, RatingIcon;
            public TextMeshProUGUI Role, Name, Rating, ChipText;
        }

        /// <summary>The stake Start uses: the picked one, none vs the AI captain (the server keeps bot matches unstaked).</summary>
        public long Stake => _modeIndex == 2 ? 0L : HackathonRules.Stakes[_stakeIndex];
        public string WagerLine => _wagerText != null ? _wagerText.text : null;
        public string StatusLine => _statusText != null ? _statusText.text : null;
        public bool DepositVisible => _deposit != null && _deposit.gameObject.activeInHierarchy;
        static PvpSession Session => PvpSession.Instance;

        public static WagerMenu Attach(OnlineMenuPresenter online, Transform canvas)
        {
            if (online == null || online.Pvp == null) return null;
            var existing = online.GetComponent<WagerMenu>();
            if (existing != null) return existing;
            var hub = FindDeep(canvas, "PlaytestPvpHub") as RectTransform;
            var lobby = FindDeep(canvas, "PlaytestPvpLobby") as RectTransform;
            if (hub == null || lobby == null) { Debug.LogWarning("WagerMenu: the 1v1 menu panels were not found."); return null; }
            var menu = online.gameObject.AddComponent<WagerMenu>();
            menu._pvp = online.Pvp;
            menu._hub = hub;
            menu._lobby = lobby;
            menu.BuildHub();
            menu.BuildLobby();
            return menu;
        }

        /// <summary>Re-frames a V4 panel for the kit: fixed size in the middle, its own look and controls hidden (the
        /// blocker and the message line stay), parchment card under a wood header.</summary>
        static RectTransform Reframe(RectTransform panel, string title, out TextMeshProUGUI titleText, out RectTransform head)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = PanelSize;
            panel.anchoredPosition = Vector2.zero;
            var bg = panel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0f, 0f, 0f, 0f);
            for (var i = 0; i < panel.childCount; i++)
            {
                var c = panel.GetChild(i);
                if (c.name != "Blocker" && c.name != "Message") c.gameObject.SetActive(false);
            }
            var root = Fill(panel, "HackathonKit");
            Paint(Fill(root, "Paper", 14f, 0f, 14f, 60f), "Panel_Card", 1f, null, new Color(0.96f, 0.89f, 0.74f, 1f));
            titleText = Header(root, title, HeadH, out head);
            return root;
        }

        static Button V4Button(Transform panel, string path)
        {
            var t = panel.Find(path);
            return t != null ? t.GetComponent<Button>() : null;
        }

        static void Press(Button b)
        {
            if (b != null && b.interactable) b.onClick.Invoke();
        }

        // ---------------------------------------------------------------- hub

        void BuildHub()
        {
            _hubMessage = _hub.Find("Message")?.GetComponent<TextMeshProUGUI>();
            _v4Refresh = V4Button(_hub, "PublicHeader/Refresh");
            var root = Reframe(_hub, "PvP (1v1)", out _, out var head);
            KitButton(TopRight(head, "Close", 34f, 22f, 230f, 84f), "Close", "Icon_Close", () => _pvp.CloseAll(), "Btn_Gold", 32f, 36f);

            // The three ways to play. 13 Oct (owner): a card only selects the mode and a chip only selects the stake; the
            // Start button below hosts with both (a tap on a card used to host at once with whatever stake was lit).
            const float pad = 40f, gap = 16f;
            var modes = Strip(root, "Modes", pad, 148f, pad, 116f);
            _modeButtons[0] = ModeCard(UiKit.Cols(modes, "HostPublic", 0f, 1f / 3f, 0f, gap * 0.66f), "Host Public", "A lobby anyone can join", "Icon_Swords", "Btn_Gold", () => PickMode(0));
            _modeButtons[1] = ModeCard(UiKit.Cols(modes, "HostPrivate", 1f / 3f, 2f / 3f, gap * 0.33f, gap * 0.33f), "Host Private", "A lobby joined with a code", "Icon_Lock", "Btn_Parchment", () => PickMode(1));
            _modeButtons[2] = ModeCard(UiKit.Cols(modes, "VsCaptain", 2f / 3f, 1f, gap * 0.66f, 0f), "Vs " + HackathonRules.CaptainName, "Fight the legendary pirate", "Icon_Skull_Crossbones", "Btn_Parchment", () => PickMode(2));

            // Stake picker.
            var stake = Strip(root, "HackathonStake", pad, 280f, pad, 92f);
            Paint(stake, "Panel_Inset", 2f);
            RowIcon(stake, "Chest_2", 18f, 70f);
            Label(Strip(stake, "Title", 104f, 8f, 1040f, 44f), "Stake", 36f, Ink, TextAlignmentOptions.BottomLeft, true, 24f);
            Label(Strip(stake, "Sub", 104f, 50f, 1040f, 34f), "(devnet SOL, winner takes the pot)", 22f, InkDim, TextAlignmentOptions.TopLeft, false, 14f);
            for (var i = 0; i < HackathonRules.Stakes.Length; i++)
            {
                var index = i;
                var rt = TopRight(stake, "Stake" + i, 18f + (3 - i) * 222f, 13f, 208f, 66f);
                _stakeButtons[i] = KitButton(rt, HackathonRules.StakeLabel(HackathonRules.Stakes[i]), null, () => PickStake(index), "Btn_Parchment", 28f);
            }

            // Start: the picked mode with the picked stake.
            _start = KitButton(Strip(root, "Start", pad, 384f, pad, 92f), "Host public lobby", "Icon_Check", StartPicked, "Btn_Gold", 36f, 44f);

            // Join with a code.
            var join = Strip(root, "Join", pad, 490f, pad, 92f);
            Paint(join, "Panel_Inset", 2f);
            RowIcon(join, "Chest_2", 18f, 70f);
            var field = Strip(join, "Field", 104f, 12f, 420f, 68f);
            _codeInput = UiKit.Input(field, "Lobby code (6 letters)", 8, 30f);
            _codeInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            _codeInput.onSubmit.AddListener(_ => _pvp.JoinCode(_codeInput.text));
            KitButton(TopRight(join, "JoinCode", 14f, 10f, 390f, 72f), "Join with code", "Icon_Link", () => _pvp.JoinCode(_codeInput.text), "Btn_Gold", 30f, 38f);

            // Public lobbies.
            var lobbies = Fill(root, "PublicLobbies", pad, 84f, pad, 598f);
            Paint(lobbies, "Panel_Inset", 2f);
            Icon(lobbies, "Icon_Group", 22f, 12f, 50f);
            Label(Strip(lobbies, "Title", 88f, 10f, 300f, 54f), "Public lobbies", 34f, Ink, TextAlignmentOptions.MidlineLeft, true, 22f);
            KitButton(TopRight(lobbies, "Refresh", 18f, 10f, 210f, 56f), "Refresh", "Icon_Refresh", RefreshPublic, "Btn_Parchment", 26f, 30f);
            var cols = Strip(lobbies, "Columns", 20f, 70f, 40f, 30f);
            Label(UiKit.Cols(cols, "Code", 0f, 0.16f, 24f), "Code", 22f, InkDim, TextAlignmentOptions.MidlineLeft);
            Label(UiKit.Cols(cols, "Host", 0.18f, 0.5f, 30f), "Host", 22f, InkDim, TextAlignmentOptions.MidlineLeft);
            Label(UiKit.Cols(cols, "Stake", 0.52f, 0.78f, 52f), "Stake", 22f, InkDim, TextAlignmentOptions.MidlineLeft);
            _publicList = Scroll(Fill(lobbies, "List", 14f, 10f, 10f, 104f), 6f, new RectOffset(4, 10, 2, 4), 12f, 20f);

            // The message line (V4's, kept) and the last payout link.
            var msg = BottomStrip(root, "MessageStrip", pad, 16f, pad, 60f);
            RowIcon(msg, "Icon_Info", 8f, 34f);
            var msgText = Fill(msg, "Text", 52f, 0f, 0f, 0f);
            Adopt(_hubMessage, msgText, 26f, Vector4.zero);
            _hubExplorer = KitButton(TopRight(msg, "ViewPayout", 0f, 2f, 240f, 56f), "View payout", "Icon_Link", () => Open(_hubExplorerUrl), "Btn_Gold", 24f, 28f);
            _hubExplorer.gameObject.SetActive(false);

            PickStake(0);
            PickMode(0);
        }

        /// <summary>Selects a way to play (highlight only); Start hosts it.</summary>
        public void PickMode(int index)
        {
            _modeIndex = Mathf.Clamp(index, 0, _modeButtons.Length - 1);
            for (var i = 0; i < _modeButtons.Length; i++) SetStyle(_modeButtons[i], i == _modeIndex ? "Btn_Gold" : "Btn_Parchment");
            // Vs the AI captain: no stake (the server does not stake bot matches); the chips show No stake only.
            for (var i = 0; i < _stakeButtons.Length; i++)
                if (_stakeButtons[i] != null) _stakeButtons[i].interactable = _modeIndex != 2 || i == 0;
            if (_modeIndex == 2) PickStake(0);
            UpdateStart();
        }

        void UpdateStart()
        {
            if (_start == null) return;
            var what = _modeIndex == 0 ? "Host public lobby" : _modeIndex == 1 ? "Host private lobby" : "Challenge " + HackathonRules.CaptainName;
            UiKit.SetLabel(_start, what + "   (" + HackathonRules.StakeLabel(Stake) + ")");
        }

        void StartPicked()
        {
            if (_modeIndex == 2) ChallengeCaptain();
            else HostMatch(_modeIndex == 0);
        }

        static Button ModeCard(RectTransform rt, string title, string sub, string icon, string style, Action click)
        {
            var b = KitButton(rt, null, null, click, style);
            RowIcon(rt, icon, 26f, 76f);
            Label(Strip(rt, "Title", 118f, 14f, 14f, 50f), title, 36f, Ink, TextAlignmentOptions.BottomLeft, true, 22f);
            Label(Strip(rt, "Sub", 118f, 64f, 14f, 36f), sub, 23f, InkDim, TextAlignmentOptions.TopLeft, false, 15f);
            b.name = title;
            return b;
        }

        public void PickStake(int index)
        {
            _stakeIndex = Mathf.Clamp(_modeIndex == 2 ? 0 : index, 0, HackathonRules.Stakes.Length - 1);
            for (var i = 0; i < _stakeButtons.Length; i++)
            {
                var b = _stakeButtons[i];
                if (b == null) continue;
                SetStyle(b, i == _stakeIndex ? "Btn_Gold" : "Btn_Parchment");
            }
            UpdateStart();
            if (_hubMessage != null && _hub.gameObject.activeInHierarchy)
                HubMessage(Stake > 0
                    ? $"Stake {HackathonRules.Sol(Stake)} each, the winner gets {HackathonRules.Sol(Stake * 2)}."
                    : _modeIndex == 2 ? $"Vs {HackathonRules.CaptainName}: a friendly match, no stake." : "Friendly match: no stake.", HackUi.Gold);
        }

        string StakeGate()
        {
            if (Stake <= 0) return null;
            if (!Web3Session.Instance.SignedIn) return "Connect a wallet (top of the menu) to play for SOL.";
            return null;
        }

        /// <summary>Hosts a lobby with the picked stake (the V4 host call, then the stake). 13 Oct (review): a stake the
        /// server refuses leaves the new lobby (it would otherwise sit there without the stake the host picked) and says why.</summary>
        public async void HostMatch(bool isPublic)
        {
            if (_busy) return;
            var gate = StakeGate();
            if (gate != null) { HubMessage(gate, HackUi.Bad); return; }
            _hosting = true;
            try
            {
                var stake = Stake;
                HubMessage(stake > 0 ? "Creating the match. Your wallet will ask you to sign the " + HackathonRules.Sol(stake) + " deposit." : "Creating a lobby...", HackUi.Gold);
                var error = await Session.Host(isPublic);
                if (this == null) return;
                if (error != null) { HubMessage(error, HackUi.Bad); return; }
                if (stake > 0)
                {
                    var stakeError = await SetStake(stake);
                    if (this == null) return;
                    if (stakeError != null) { await LeaveWith(stakeError + " The lobby was closed; nothing was paid."); return; }
                }
                _pvp.ShowLobby();
                if (stake > 0) await DepositOrLeave(true);
            }
            catch (Exception e) { if (this != null) HubMessage(e.Message, HackUi.Bad); }
            finally { _hosting = false; if (this != null) RefreshLobby(); }
        }

        /// <summary>"Vs Captain Marrow": a private lobby and the AI captain's seat. No stake (13 Oct: the server keeps bot
        /// matches unstaked).</summary>
        public async void ChallengeCaptain()
        {
            if (_busy) return;
            _hosting = true;
            try
            {
                HubMessage(HackathonRules.CaptainName + " is raising his colours...", HackUi.Gold);
                var error = await Session.Host(false);
                if (this == null) return;
                if (error == null) error = await Session.AddBot();
                if (this == null) return;
                if (error != null) { HubMessage(error, HackUi.Bad); if (!Session.InLobby) return; }
                _pvp.ShowDraft();
            }
            catch (Exception e) { if (this != null) HubMessage(e.Message, HackUi.Bad); }
            finally { _hosting = false; }
        }

        async Awaitable<string> SetStake(long stake)
        {
            var lobbyId = Session.Lobby?.lobbyId;
            HubMessage("Setting the stake to " + HackathonRules.Sol(stake) + "...", HackUi.Gold);
            var (w, error) = await HackathonApi.SetStake(lobbyId, stake);
            if (error != null) return "The stake was refused: " + error.TrimEnd('.') + ".";
            HackathonPvp.Adopt(lobbyId, w);
            if (w == null) await HackathonPvp.Refresh(lobbyId);
            return null;
        }

        void HubMessage(string text, Color color)
        {
            if (_hubMessage == null) return;
            _hubMessage.text = text ?? string.Empty;
            _hubMessage.color = HackUi.InkFor(color);
            _hubSet = _hubMessage.text;
        }

        /// <summary>Refresh: V4's list call (its messages) and the kit list (with each lobby's stake).</summary>
        void RefreshPublic()
        {
            if (_v4Refresh != null) _v4Refresh.onClick.Invoke();
            FillPublic();
        }

        async void FillPublic()
        {
            if (_publicList == null) return;
            var version = ++_listVersion;
            ApiResult<PublicLobbiesResponse> r;
            try { r = await Session.PublicLobbies(); }
            catch (Exception) { return; }
            if (this == null || version != _listVersion) return;
            Clear(_publicList);
            if (r == null || !r.Ok || r.Data == null || r.Data.lobbies == null) return;
            var mine = Session.MyPlayerId;
            foreach (var l in r.Data.lobbies)
            {
                if (l == null || l.host == null) continue;
                var row = ListRow(_publicList, "Lobby", 66f, "Panel_Inset", 3f, new Color(1f, 0.97f, 0.9f, 1f));
                var codeCell = UiKit.Cols(row, "Code", 0f, 0.16f, 8f, 0f);
                var tag = Fill(codeCell, "Tag", 0f, 9f, 0f, 9f);
                Paint(tag, "Tag_Tan", 1f, null, new Color(0.88f, 0.78f, 0.6f, 1f));
                Label(tag, l.code ?? string.Empty, 26f, Ink, TextAlignmentOptions.Center, true, 16f);
                var host = UiKit.Cols(row, "Host", 0.18f, 0.52f, 0f, 0f);
                RowIcon(host, "Dot_Green", 0f, 20f);
                Label(Fill(host, "Name", 30f), l.host.name + "<color=#7a5a3b>" + l.host.tag + "</color>", 26f, Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
                var stake = UiKit.Cols(row, "Stake", 0.52f, 0.78f, 0f, 0f);
                RowIcon(stake, "Chest_2", 0f, 40f);
                var stakeText = Label(Fill(stake, "Text", 52f), "...", 26f, Ink, TextAlignmentOptions.MidlineLeft, false, 16f);
                var own = l.host.playerId == mine;
                var code = l.code;
                var b = KitButton(TopRight(row, "Join", 8f, 6f, 200f, 54f), own ? "Yours" : "Join", own ? null : "Icon_Swords", () => _pvp.JoinCode(code), "Btn_Gold", 26f, 30f);
                b.interactable = !own;
                LobbyStake(l.lobbyId, stakeText);
            }
        }

        static async void LobbyStake(string lobbyId, TextMeshProUGUI text)
        {
            (WagerView w, string error) r;
            try { r = await HackathonApi.GetWager(lobbyId); }
            catch (Exception) { r = (null, "error"); }
            if (text == null) return;
            text.text = r.error != null ? "?" : r.w != null && r.w.Exists ? HackathonRules.Sol(r.w.stakeLamports) : "No stake";
        }

        // ---------------------------------------------------------------- lobby

        void BuildLobby()
        {
            _lobbyMessage = _lobby.Find("Message")?.GetComponent<TextMeshProUGUI>();
            _v4Leave = V4Button(_lobby, "Header/Leave");
            _v4CopyLink = V4Button(_lobby, "Invite/Copy link");
            _v4CopyCode = V4Button(_lobby, "Invite/Copy code");
            _v4Pick = V4Button(_lobby, "Actions/Pick build");
            _v4Ready = V4Button(_lobby, "Actions/Ready");
            _v4Bot = V4Button(_lobby, "Actions/Add bot");

            var root = Reframe(_lobby, "Lobby", out _lobbyTitle, out var head);
            _lobbyTitle.rectTransform.offsetMax = new Vector2(-900f, _lobbyTitle.rectTransform.offsetMax.y);
            KitButton(TopRight(head, "Leave", 34f, 22f, 230f, 84f), "Leave", "Icon_Close", Leave, "Btn_Gold", 32f, 36f);
            var tag = TopRight(head, "LobbyTag", 300f, 34f, 360f, 62f);
            Paint(tag, "Tag_Tan", 1.4f, null, new Color(0.88f, 0.78f, 0.6f, 1f));
            _lobbyTag = Label(Fill(tag, "Text", 14f, 2f, 14f, 2f), string.Empty, 28f, Ink, TextAlignmentOptions.Center, true, 18f);

            const float pad = 40f;
            Icon(root, "Icon_Group", pad + 6f, 150f, 50f);
            _players = Label(Strip(root, "Players", pad + 70f, 144f, 700f, 60f), "Players (0 / 2)", 38f, Ink, TextAlignmentOptions.MidlineLeft, true, 24f);
            Label(TopRight(root, "RatingCol", pad + 470f, 196f, 200f, 28f), "Rating", 22f, InkDim, TextAlignmentOptions.MidlineLeft);
            Label(TopRight(root, "StatusCol", pad + 12f, 196f, 330f, 28f), "Status", 22f, InkDim, TextAlignmentOptions.Center);
            _seats[0] = Seat(Strip(root, "HostSeat", pad, 226f, pad, 78f));
            _seats[1] = Seat(Strip(root, "GuestSeat", pad, 312f, pad, 78f));

            // The middle: lobby code, wager strip, wager status, the AI captain's banner (stacked; hidden ones close up).
            var middle = Fill(root, "Middle", pad, 186f, pad, 402f);
            var v = middle.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;

            _codeSection = ListRow(middle, "LobbyCode", 118f, "Panel_Inset", 2f);
            RowIcon(_codeSection, "Chest_2", 16f, 86f);
            Label(Strip(_codeSection, "Title", 118f, 14f, 980f, 46f), "Lobby code", 34f, Ink, TextAlignmentOptions.BottomLeft, true, 22f);
            Label(Strip(_codeSection, "Sub", 118f, 62f, 980f, 36f), "Share this code with a friend to join", 23f, InkDim, TextAlignmentOptions.TopLeft, false, 14f);
            var box = TopRight(_codeSection, "CodeBox", 576f, 16f, 390f, 86f);
            Paint(box, "Input_Field", 1f, null, new Color(0.98f, 0.95f, 0.88f, 1f));
            _codeBig = Label(Fill(box, "Code", 12f, 0f, 84f, 0f), string.Empty, 54f, Ink, TextAlignmentOptions.Center, true, 28f);
            _codeBig.characterSpacing = 18f;
            KitButton(TopRight(box, "Copy", 12f, 13f, 60f, 60f), null, "Icon_Copy", () => Press(_v4CopyCode), "Btn_Square_Parchment", 22f, 34f, 2f);
            KitButton(TopRight(_codeSection, "CopyLink", 296f, 20f, 264f, 78f), "Copy link", "Icon_Link", () => Press(_v4CopyLink), "Btn_Gold", 30f, 36f);
            KitButton(TopRight(_codeSection, "CopyCode", 16f, 20f, 264f, 78f), "Copy code", "Icon_Copy", () => Press(_v4CopyCode), "Btn_Gold", 30f, 36f);

            // The wager strip: stake, pot, both seats' deposits, and the deposit button.
            _strip = ListRow(middle, "HackathonWager", 84f, "Panel_Inset", 2f, new Color(1f, 0.93f, 0.7f, 1f));
            RowIcon(_strip, "Chest_2", 14f, 60f);
            _wagerText = Label(Strip(_strip, "Text", 88f, 4f, 400f, 76f), string.Empty, 24f, Ink, TextAlignmentOptions.MidlineLeft, false, 15f);
            _wagerText.textWrappingMode = TextWrappingModes.Normal;
            // Creating or joining asks for the deposit by itself. This button is the fallback while the seat is unpaid:
            // "Pay deposit" (the automatic prompt was skipped or failed) or "Check deposit" (a sent transfer that devnet has
            // not confirmed yet; never pays twice).
            _deposit = KitButton(TopRight(_strip, "Deposit", 14f, 9f, 370f, 66f), "Pay deposit", "Icon_Upload", DepositPressed, "Btn_Gold", 27f, 32f);

            _status = ListRow(middle, "HackathonWagerStatus", 54f, null, 1f);
            RowIcon(_status, "Icon_Info", 8f, 32f);
            _statusText = Label(Strip(_status, "Text", 50f, 0f, 230f, 54f), string.Empty, 23f, InkGold, TextAlignmentOptions.MidlineLeft, false, 14f);
            _statusText.textWrappingMode = TextWrappingModes.Normal;
            _explorer = KitButton(TopRight(_status, "ViewTx", 0f, 2f, 214f, 50f), "View tx", "Icon_Link", () => Open(_explorerUrl), "Btn_Parchment", 24f, 26f);

            // The AI captain's banner (vs the bot only): portrait, name, a line of menace.
            _banner = ListRow(middle, "HackathonCaptainBanner", 120f, "Banner_Maroon", 1.6f, null);
            var frame = TopLeft(_banner, "Portrait", 22f, 10f, 100f, 100f);
            Paint(frame, "Slot_Card", 3f, new Color(0.95f, 0.75f, 0.35f, 1f), new Color(0.6f, 0.4f, 0.2f, 1f));
            var pimg = Fill(frame, "Face", 6f, 6f, 6f, 6f).gameObject.AddComponent<Image>();
            pimg.preserveAspect = true;
            pimg.raycastTarget = false;
            var sprite = CaptainPortrait();
            if (sprite != null) pimg.sprite = sprite;
            else pimg.color = new Color(0f, 0f, 0f, 0f);
            _bannerText = Label(Strip(_banner, "Text", 148f, 8f, 24f, 104f), string.Empty, 30f, OnWood, TextAlignmentOptions.MidlineLeft, false, 16f);
            _bannerText.textWrappingMode = TextWrappingModes.Normal;

            // Actions.
            var actions = BottomStrip(root, "Actions", pad, 88f, pad, 90f);
            _pick = KitButton(UiKit.Cols(actions, "PickBuild", 0f, 1f / 3f, 0f, 12f), "Pick build", "Icon_Swords", () => Press(_v4Pick), "Btn_Gold", 36f, 52f);
            _ready = KitButton(UiKit.Cols(actions, "Ready", 1f / 3f, 2f / 3f, 6f, 6f), "Ready", "Icon_Check", () => Press(_v4Ready), "Btn_Gold", 36f, 46f);
            _addBot = KitButton(UiKit.Cols(actions, "AddBot", 2f / 3f, 1f, 12f, 0f), "Add bot", "Icon_User", () => Press(_v4Bot), "Btn_Gold", 36f, 46f);

            var msg = BottomStrip(root, "MessageStrip", pad, 16f, pad, 62f);
            UiKit.Line(msg, 0f, 0f, 0f);
            RowIcon(msg, "Icon_Info", 8f, 34f);
            Adopt(_lobbyMessage, Fill(msg, "Text", 52f, 0f, 0f, 4f), 26f, Vector4.zero);

            _banner.gameObject.SetActive(false);
            _strip.gameObject.SetActive(false);
            _status.gameObject.SetActive(false);
        }

        /// <summary>The lobby card is as tall as its shown rows (code, wager, status, banner): no empty band above the
        /// actions. Seats end at 390 and the middle stack starts at 402; actions and the message take the bottom 178.</summary>
        void FitLobby()
        {
            if (_codeSection == null) return;
            var stack = 0f;
            var rows = 0;
            foreach (var row in new[] { _codeSection, _strip, _status, _banner })
            {
                if (row == null || !row.gameObject.activeSelf) continue;
                var le = row.GetComponent<LayoutElement>();
                stack += le != null && le.preferredHeight > 0f ? le.preferredHeight : row.rect.height;
                rows++;
            }
            if (rows > 1) stack += (rows - 1) * 8f;
            var height = Mathf.Clamp(402f + stack + 24f + 178f, 720f, PanelSize.y);
            if (!Mathf.Approximately(_lobby.sizeDelta.y, height)) _lobby.sizeDelta = new Vector2(PanelSize.x, height);
        }

        static SeatRow Seat(RectTransform rt)
        {
            var s = new SeatRow { Root = rt };
            Paint(rt, "Panel_Inset", 2.4f);
            s.Icon = RowIcon(rt, "Crown_Gold", 18f, 52f);
            s.Role = Label(TopLeft(rt, "Role", 84f, 0f, 150f, 78f), "Host", 30f, Ink, TextAlignmentOptions.MidlineLeft, true, 18f);
            s.Dot = RowIcon(rt, "Dot_Green", 236f, 22f);
            var name = Strip(rt, "Name", 272f, 0f, 700f, 78f);
            s.Name = Label(name, "...", 30f, Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            var rating = TopRight(rt, "Rating", 470f, 0f, 200f, 78f);
            s.RatingIcon = RowIcon(rating, "Icon_Rating", 0f, 34f);
            s.Rating = Label(Fill(rating, "Text", 46f), string.Empty, 28f, Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
            var chip = TopRight(rt, "Status", 12f, 9f, 330f, 60f);
            s.Chip = Paint(chip, "Btn_Gold", 1.3f);
            s.ChipDot = RowIcon(chip, "Dot_Green", 24f, 24f);
            s.ChipText = Label(Fill(chip, "Text", 56f, 0f, 12f, 0f), string.Empty, 26f, Ink, TextAlignmentOptions.Center, true, 16f);
            return s;
        }

        void SetSeat(SeatRow row, LobbySeat seat, bool host, bool mine)
        {
            row.Role.text = host ? "Host" : "Guest";
            SetSprite(row.Icon, host ? "Crown_Gold" : seat != null && seat.bot ? "Icon_Skull_Crossbones" : "Icon_User", host ? Color.white : Ink);
            var empty = seat == null || string.IsNullOrEmpty(seat.playerId);
            row.Dot.enabled = !empty;
            row.Name.text = empty ? "<color=#7a5a3b>...</color>"
                : seat.name + "<color=#7a5a3b>" + seat.tag + "</color>" + (seat.bot ? "  <size=75%><color=#7a5a3b>(bot)</color></size>" : string.Empty)
                  + (mine ? "  <size=75%><color=#7a5a3b>(you)</color></size>" : string.Empty);
            row.Rating.text = empty ? "<color=#7a5a3b>...</color>" : seat.rating.ToString();
            row.RatingIcon.color = empty ? new Color(Ink.r, Ink.g, Ink.b, 0.35f) : Ink;
            var status = empty ? null : seat.status;
            string chip, dot;
            switch (status)
            {
                case null: chip = "Btn_Parchment"; dot = "Dot_Beige"; row.ChipText.text = "Waiting for player"; break;
                case "ready": chip = "Btn_Green"; dot = null; row.ChipText.text = "Ready"; break;
                case "left": chip = "Btn_Red"; dot = "Dot_Red"; row.ChipText.text = "Left"; break;
                case "drafting": chip = "Btn_Gold"; dot = "Dot_Yellow"; row.ChipText.text = "Picking weapons"; break;
                case "in_match": chip = "Btn_Gold"; dot = "Dot_Green"; row.ChipText.text = "In the match"; break;
                case "joined": chip = "Btn_Gold"; dot = "Dot_Green"; row.ChipText.text = "In the lobby"; break;
                default: chip = "Btn_Gold"; dot = "Dot_Green"; row.ChipText.text = status; break;
            }
            SetSprite(row.Chip, chip, Color.white);
            if (status == "ready") SetSprite(row.ChipDot, "Icon_Check_Green", Color.white);
            else SetSprite(row.ChipDot, dot, Color.white);
            row.ChipText.color = empty ? InkDim : Ink;
            Paint(row.Root, "Panel_Inset", 2.4f, mine ? new Color(1f, 0.9f, 0.62f, 1f) : Color.white);
        }

        static void SetSprite(Image img, string sprite, Color tint)
        {
            if (img == null) return;
            var s = HackSkin.S(sprite);
            img.enabled = s != null;
            if (s == null) return;
            img.sprite = s;
            img.color = tint;
        }

        static Sprite CaptainPortrait()
        {
            var art = PlayerInfoHudArtSettings.Load();
            if (art?.captainPortraits == null) return null;
            for (var i = art.captainPortraits.Length - 1; i >= 0; i--)
                if (art.captainPortraits[i].icon != null) return art.captainPortraits[i].icon;
            return null;
        }

        void OnEnable()
        {
            Session.LobbyChanged += OnLobby;
            HackathonPvp.WagerChanged += OnWager;
            Web3Session.Instance.Changed += OnWallet;
        }

        void OnDisable()
        {
            if (PvpSession.Exists) Session.LobbyChanged -= OnLobby;
            HackathonPvp.WagerChanged -= OnWager;
            Web3Session.Instance.Changed -= OnWallet;
        }

        void OnLobby(LobbyState state)
        {
            if (this == null) return;
            // Left (or the lobby closed): forget it, so rejoining the same lobby asks for the deposit again.
            if (state == null || !Session.InLobby) _lastLobbyId = null;
            else if (state.lobbyId != _lastLobbyId)
            {
                _lastLobbyId = state.lobbyId;
                SetStatus(string.Empty, null);
                // Joined someone else's lobby: if it has a stake, sign the deposit now (bundled with the join).
                if (!string.IsNullOrEmpty(state.hostPlayerId) && state.hostPlayerId != Session.MyPlayerId) GuestDeposit(state.lobbyId);
            }
            _nextPoll = 0f; // read the wager again with the new lobby state
            RefreshLobby();
        }

        void OnWager(WagerView w) { if (this != null) { RefreshLobby(); RefreshHub(); } }
        void OnWallet() { if (this != null) RefreshLobby(); }

        void Update()
        {
            var lobbyOpen = _lobby.gameObject.activeInHierarchy;
            var hubOpen = _hub.gameObject.activeInHierarchy;
            if (hubOpen && !_hubWasOpen) { FillPublic(); ResumeDeposits(); }
            if (lobbyOpen && !_lobbyWasOpen) { RefreshLobby(); ResumeDeposits(); }
            _hubWasOpen = hubOpen;
            _lobbyWasOpen = lobbyOpen;
            // V4 writes its own messages without a colour: ink for those.
            if (_hubMessage != null && _hubMessage.text != _hubSet) { _hubMessage.color = Ink; _hubSet = _hubMessage.text; }
            if (_lobbyMessage != null && _lobbyMessage.text != _lobbySet) { _lobbyMessage.color = Ink; _lobbySet = _lobbyMessage.text; }
            if (lobbyOpen) { MirrorActions(); FitLobby(); }
            if (!lobbyOpen && !hubOpen) return;
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 3f;
            string lobbyId = null;
            if (lobbyOpen && Session.InLobby) lobbyId = Session.Lobby.lobbyId;
            else if (hubOpen && HackathonPvp.Wager != null && !HackathonPvp.Wager.Closed) lobbyId = HackathonPvp.WagerLobbyId;
            else if (hubOpen && HackathonPvp.Wager != null && HackathonRules.PayoutPending(HackathonPvp.Wager)) lobbyId = HackathonPvp.WagerLobbyId;
            if (lobbyId != null) _ = HackathonPvp.Refresh(lobbyId);
            if (hubOpen) RefreshHub();
        }

        /// <summary>The kit buttons follow V4's (enabled, shown).</summary>
        void MirrorActions()
        {
            if (_pick != null && _v4Pick != null) _pick.interactable = _v4Pick.interactable;
            if (_ready != null && _v4Ready != null) _ready.interactable = _v4Ready.interactable;
            if (_addBot != null && _v4Bot != null) _addBot.gameObject.SetActive(_v4Bot.gameObject.activeSelf);
        }

        void RefreshLobby()
        {
            if (_strip == null) return;
            var lobby = Session.Lobby;
            var inLobby = Session.InLobby && lobby != null;
            if (inLobby)
            {
                _lobbyTitle.text = lobby.@public ? "Public Lobby" : "Private Lobby";
                _lobbyTag.text = "Lobby  " + lobby.code;
                _codeBig.text = lobby.code;
                var me = Session.MyPlayerId;
                var count = 0;
                for (var i = 0; i < 2; i++)
                {
                    var seat = lobby.seats != null && i < lobby.seats.Length ? lobby.seats[i] : null;
                    if (seat != null && !string.IsNullOrEmpty(seat.playerId)) count++;
                    SetSeat(_seats[i], seat, i == 0, seat != null && seat.playerId == me);
                }
                _players.text = $"Players ({count} / 2)";
                // The code matters while the lobby waits for a player.
                _codeSection.gameObject.SetActive(lobby.status == "waiting" || lobby.status == null);
                MirrorActions();
            }

            var vsCaptain = inLobby && lobby.Guest != null && lobby.Guest.bot;
            _banner.gameObject.SetActive(vsCaptain);
            if (vsCaptain)
                _bannerText.text = $"<color=#ffd166><b><size=120%>{HackathonRules.CaptainName}</size></b></color>  {HackathonRules.CaptainTitle}\n" +
                                   "<i>\"Sign the articles or feed the sharks, whelp.\"</i>";

            var w = inLobby ? HackathonPvp.For(lobby.lobbyId) : null;
            var hasWager = w != null && w.Exists;
            _strip.gameObject.SetActive(hasWager);
            _status.gameObject.SetActive(hasWager && (!string.IsNullOrEmpty(_statusText.text) || _explorerUrl != null));
            if (!hasWager) return;

            var myId = Session.MyPlayerId;
            var iAmHost = lobby.hostPlayerId == myId;
            var mySeat = iAmHost ? w.host : w.guest;
            var myDeposited = mySeat != null && mySeat.deposited;
            var stake = HackathonRules.Sol(w.stakeLamports);
            var hostName = lobby.Host != null ? lobby.Host.name : "Host";
            var guestName = lobby.Guest != null ? lobby.Guest.name : "Guest";
            _wagerText.text =
                $"<color=#8c4d05><b>Wager {stake}</b></color> each   Pot <b>{HackathonRules.Sol(w.potLamports)}</b>   <size=80%><color=#7a5a3b>{Status(w)}</color></size>\n" +
                $"<size=90%>{hostName}: {Mark(w.host != null && w.host.deposited)}{HackathonRules.SeatFunding(w.host, false)}   " +
                $"{guestName}: {Mark(w.vsHouse || (w.guest != null && w.guest.deposited))}{HackathonRules.SeatFunding(w.guest, w.vsHouse)}</size>";

            var canDeposit = w.Open && !myDeposited && mySeat != null && !string.IsNullOrEmpty(mySeat.playerId);
            var pending = HackathonApi.HasPendingDeposit(lobby.lobbyId);
            // While the wallet is asked to sign, the button cancels the wait (nothing is sent until the wallet signs).
            var cancel = _depositing && Web3Session.Instance.CanCancel;
            _deposit.gameObject.SetActive(cancel || ((canDeposit || pending) && !_busy));
            _deposit.interactable = cancel || ((canDeposit || pending) && !_busy);
            SetLabel(_deposit, cancel ? "Cancel signing" : pending ? "Check deposit" : "Pay deposit " + stake);

            var result = HackathonRules.ResultLine(w, myId);
            if (result != null && !_busy) SetStatus(result, HackathonRules.ExplorerTx(HackathonRules.PayoutSignature(w)));
            else if (!_busy && w.funded && string.IsNullOrEmpty(_statusText.text)) SetStatus("Stake funded. The match starts when both captains are Ready.", null);
            if (_lobbyMessage != null && canDeposit && _busy && lobby.status != "starting" && lobby.status != "in_match")
                _lobbyMessage.text = $"Sign the {stake} deposit in your wallet. The match starts when both stakes are in.";
        }

        static string Mark(bool ok) => ok ? "<color=#2b7324>[paid]</color> " : "<color=#7a5a3b>[ ]</color> ";

        static string Status(WagerView w)
        {
            switch (w.status)
            {
                case "awaiting_deposits": return "waiting for deposits";
                case "funded": return "funded";
                case "settled": return "settled";
                case "refunded": return "refunded";
                case "cancelled": return "cancelled";
                default: return w.status;
            }
        }

        void SetStatus(string text, string url, bool bad = false)
        {
            _statusText.text = text ?? string.Empty;
            _statusText.color = bad ? InkBad : InkGold;
            _explorerUrl = url;
            _explorer.gameObject.SetActive(!string.IsNullOrEmpty(url));
            var w = Session.InLobby ? HackathonPvp.For(Session.Lobby.lobbyId) : null;
            _status.gameObject.SetActive(w != null && (!string.IsNullOrEmpty(text) || url != null));
        }

        /// <summary>The guest's half of "bundle the deposit with the match": reads the joined lobby's wager and, when it has a
        /// stake this seat has not paid, asks the wallet for the deposit (or leaves the lobby if that fails).</summary>
        async void GuestDeposit(string lobbyId)
        {
            // Another wager step still running: wait for it briefly (the Pay deposit button stays as the fallback).
            for (var i = 0; i < 40 && _busy; i++) await Awaitable.WaitForSecondsAsync(0.25f);
            if (this == null || _busy) return;
            var error = await HackathonPvp.Refresh(lobbyId);
            if (this == null || !Session.InLobby || Session.Lobby.lobbyId != lobbyId) return;
            var w = HackathonPvp.For(lobbyId);
            if (error != null || w == null || !w.Exists || !w.Open) return;
            var seat = w.guest;
            if (seat != null && seat.deposited) return;
            if (!Web3Session.Instance.SignedIn)
            {
                await LeaveWith("This match has a " + HackathonRules.Sol(w.stakeLamports) + " stake. Connect a wallet (top of the menu) to join staked matches.");
                return;
            }
            await DepositOrLeave(false);
        }

        /// <summary>Asks the wallet for this seat's stake and has the server verify it. On success the lobby shows the
        /// deposit; on a cancel or a refusal before anything was signed the lobby is left (nothing was paid) and the hub says
        /// why. A signed transfer the server has not answered for yet keeps the lobby with "Check deposit"; one the server
        /// refused for good says what happens to the SOL, with the transaction link. Returns true when the deposit is in.</summary>
        async Awaitable<bool> DepositOrLeave(bool host)
        {
            var lobby = Session.Lobby;
            var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
            if (w == null || !w.Exists) return true; // no stake: nothing to pay
            var stake = HackathonRules.Sol(w.stakeLamports);
            if (!Web3Session.Instance.WalletConnected && !HackathonApi.HasPendingDeposit(lobby.lobbyId))
            {
                await LeaveWith("Reconnect your wallet (Wallet button, top of the menu) to sign the " + stake + " deposit. The match was cancelled; nothing was paid.");
                return false;
            }
            var ok = await RunDeposit();
            if (this == null) return false;
            if (ok) return true;
            var r = _last ?? new HackathonApi.DepositResult();
            var line = HackathonRules.DepositFailureLine(host, stake, r.Sent, r.Refunded, r.Error, r.FinalLine);
            if (r.Pending)
            {
                // Signed (maybe sent) and the server has not answered for good: stay in the lobby ("Check deposit").
                SetStatus(line, HackathonRules.ExplorerTx(r.Signature), true);
                return false;
            }
            await LeaveWith(line, HackathonRules.ExplorerTx(r.Signature));
            return false;
        }

        /// <summary>Leaves the lobby (a pending deposit is posted first: <see cref="HackathonApi.LeaveLobbySafely"/>), opens
        /// the hub and shows <paramref name="message"/> (with a transaction link when given).</summary>
        async Awaitable LeaveWith(string message, string url = null)
        {
            string depositLine = null;
            _leaving = true;
            try { depositLine = await HackathonApi.LeaveLobbySafely(); }
            catch (Exception e) { Debug.LogWarning("WagerMenu.LeaveWith: " + e); }
            finally { _leaving = false; }
            if (this == null) return;
            _pvp.Open();
            HubMessage(depositLine != null && message != null && !message.Contains(depositLine) ? message + " " + depositLine : message ?? depositLine, HackUi.Bad);
            SetHubLink(url);
        }

        /// <summary>The lobby's Leave: a deposit still waiting for the server is posted (and its answer awaited) first, so a
        /// seat left with SOL in flight is refunded by the server; then V4's Leave.</summary>
        async void Leave()
        {
            if (_leaving) return;
            var lobbyId = Session.Lobby?.lobbyId;
            if (lobbyId == null || !HackathonApi.HasPendingDeposit(lobbyId)) { Press(_v4Leave); return; }
            _leaving = true;
            string line = null;
            try
            {
                SetStatus("Checking your deposit with the server before leaving...", null);
                line = await HackathonApi.LeaveLobbySafely(false);
            }
            catch (Exception e) { Debug.LogWarning("WagerMenu.Leave: " + e); }
            finally { _leaving = false; }
            if (this == null) return;
            Press(_v4Leave);
            if (line != null) HubMessage(line, HackUi.Gold);
        }

        /// <summary>"Pay deposit" / "Check deposit" / "Cancel signing".</summary>
        void DepositPressed()
        {
            if (_depositing && Web3Session.Instance.CanCancel) { Web3Session.Instance.CancelWait(); return; }
            Deposit();
        }

        /// <summary>"Pay deposit" / "Check deposit": pays this seat's stake, or re-checks a signed transfer (no second
        /// payment). A refused transfer says what happens to the SOL.</summary>
        public async void Deposit()
        {
            if (_busy) return;
            try
            {
                if (await RunDeposit() || this == null) return;
                var r = _last;
                if (r == null || r.Pending || r.Error == null) return;
                var lobby = Session.Lobby;
                var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
                var stake = HackathonRules.Sol(w != null ? w.stakeLamports : 0);
                if (r.Sent && (r.Refunded || r.FinalLine != null))
                    SetStatus(HackathonRules.DepositFailureLine(Session.IsHost, stake, true, r.Refunded, r.Error, r.FinalLine), HackathonRules.ExplorerTx(r.Signature), true);
            }
            catch (Exception e) { Debug.LogWarning("WagerMenu.Deposit: " + e); if (this != null) SetStatus(e.Message, null, true); }
        }

        /// <summary>Pays (or re-checks) this seat's stake. Returns true when the server has it.</summary>
        async Awaitable<bool> RunDeposit()
        {
            if (_depositing) return false;
            var lobby = Session.Lobby;
            var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
            _last = null;
            if (lobby == null) return false;
            var pending = HackathonApi.HasPendingDeposit(lobby.lobbyId);
            if (w == null && !pending) return false;
            if (!pending && !Web3Session.Instance.WalletConnected)
            {
                _last = new HackathonApi.DepositResult { Error = "the wallet is not connected" };
                var wallet = GetComponent<WalletMenu>();
                if (wallet != null) wallet.Open();
                SetStatus("Connect your wallet to sign the deposit.", null);
                return false;
            }
            _depositing = true;
            RefreshLobby();
            try
            {
                Action<string> progress = m => { if (this != null) { SetStatus(m, null); RefreshLobby(); } };
                var r = pending ? await HackathonApi.CheckDeposit(lobby.lobbyId, progress) : await HackathonApi.Deposit(w, progress);
                _last = r;
                if (this == null) return false;
                if (!r.Ok) { SetStatus(r.Error, HackathonRules.ExplorerTx(r.Signature), true); return false; }
                SetStatus("Deposit confirmed on devnet. Pick your build and press Ready.", HackathonRules.ExplorerTx(r.Signature));
                if (r.Wager != null) HackathonPvp.Adopt(lobby.lobbyId, r.Wager);
                else await HackathonPvp.Refresh(lobby.lobbyId);
                return true;
            }
            catch (Exception e)
            {
                var stillPending = HackathonApi.HasPendingDeposit(lobby.lobbyId);
                _last = new HackathonApi.DepositResult { Error = e.Message, Sent = stillPending, Pending = stillPending };
                if (this != null) SetStatus(e.Message, null, true);
                return false;
            }
            finally
            {
                _depositing = false;
                if (this != null) RefreshLobby();
            }
        }

        bool _resuming;

        /// <summary>Posts deposits signed earlier for other lobbies (the app closed during signing, a lobby left while
        /// devnet was slow): the server takes or refunds them. Final answers show on the hub.</summary>
        async void ResumeDeposits()
        {
            if (_resuming || !Web3Session.Instance.SignedIn) return;
            _resuming = true;
            try
            {
                var current = Session.InLobby ? Session.Lobby.lobbyId : null;
                var lines = await HackathonApi.ResumePendingDeposits(current);
                if (this != null && !string.IsNullOrEmpty(lines) && _hub.gameObject.activeInHierarchy) HubMessage(lines, HackUi.Gold);
            }
            catch (Exception e) { Debug.LogWarning("WagerMenu.ResumeDeposits: " + e); }
            finally { _resuming = false; }
        }

        string _hubLinkOverride;

        /// <summary>A transaction link on the hub's message (the last refused deposit), until the next wager result.</summary>
        void SetHubLink(string url)
        {
            _hubLinkOverride = url;
            RefreshHub();
        }

        // ---------------------------------------------------------------- hub: the last wager's result

        void RefreshHub()
        {
            if (_hubExplorer == null) return;
            var w = HackathonPvp.Wager;
            var inLobby = Session.InLobby && Session.Lobby.lobbyId == HackathonPvp.WagerLobbyId;
            var result = !inLobby ? HackathonRules.ResultLine(w, Session.MyPlayerId ?? OnlineService.Active?.playerId) : null;
            var sig = result != null ? HackathonRules.PayoutSignature(w) : null;
            _hubExplorerUrl = HackathonRules.ExplorerTx(sig) ?? _hubLinkOverride;
            _hubExplorer.gameObject.SetActive(_hubExplorerUrl != null);
            if (_hubExplorerUrl != null) SetLabel(_hubExplorer, sig != null ? "View payout" : "View tx");
            if (result != null && _hubMessage != null && !_busy && (string.IsNullOrEmpty(_hubMessage.text) || _hubMessage.text.StartsWith("Last wager") || _hubMessage.text.StartsWith("Left the lobby") || _hubMessage.text.StartsWith("No public lobbies")))
                HubMessage("Last wager: " + result + (HackathonRules.PayoutPending(w) ? " (payout on its way)" : string.Empty), HackUi.Good);
            if (_hubMessage != null)
            {
                var rt = (RectTransform)_hubMessage.rectTransform.parent;
                rt.offsetMax = new Vector2(_hubExplorerUrl != null ? -256f : 0f, rt.offsetMax.y);
            }
        }

        // ---------------------------------------------------------------- bits

        static void Open(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
