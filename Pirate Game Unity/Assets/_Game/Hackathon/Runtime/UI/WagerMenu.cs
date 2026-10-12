using System;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): 1v1 SOL wagers and the AI captain on V4's 1v1 menu (<see cref="PvpMenu"/>), added from here so
    /// the V4 menu code is unchanged:
    /// <list type="bullet">
    /// <item>the hub gets a stake picker (none, 0.01, 0.05, 0.1 SOL); Host public / private set the stake on the new lobby
    /// (POST /api/wagers/{lobbyId}); "Play vs bot" becomes "Vs Captain Marrow" (the house matches the stake);</item>
    /// <item>the lobby gets the AI captain's banner and a wager strip: the stake and pot and both seats' deposits;</item>
    /// <item>the deposit is part of creating or joining a staked match (owner, 12 Oct): the wallet asks to sign the stake
    /// transfer (to the treasury with the memo wager:&lt;lobbyId&gt;) right away and the server verifies it. A cancelled or
    /// failed deposit leaves the lobby cleanly (nothing was paid) with a clear message. Only when the transfer was sent but
    /// devnet has not confirmed it yet does the lobby stay, with "Check deposit" (no second payment);</item>
    /// <item>the hub shows the last wager's result and payout with an explorer link.</item>
    /// </list>
    /// </summary>
    public sealed class WagerMenu : MonoBehaviour
    {
        PvpMenu _pvp;
        RectTransform _hub, _lobby;
        TextMeshProUGUI _hubMessage, _lobbyMessage;
        readonly Button[] _stakeButtons = new Button[4];
        Color _stakeOn = Color.white;
        int _stakeIndex;
        bool _busy;

        RectTransform _banner, _strip, _status;
        TextMeshProUGUI _bannerText, _wagerText, _statusText;
        Button _deposit, _explorer, _hubExplorer;
        string _explorerUrl, _hubExplorerUrl;
        float _nextPoll;
        string _lastLobbyId;
        // The last deposit attempt: a transfer exists (sent) / the server is refunding it (refunded) / its error.
        bool _lastSent, _lastRefunded;
        string _lastError;

        public long Stake => HackathonRules.Stakes[_stakeIndex];
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

        // ---------------------------------------------------------------- hub

        void BuildHub()
        {
            _hubMessage = _hub.Find("Message")?.GetComponent<TextMeshProUGUI>();
            // Make room for the stake row under the host buttons.
            Shift(_hub, "Join", 88f);
            Shift(_hub, "PublicHeader", 88f);
            var body = _hub.Find("Body") as RectTransform;
            if (body != null) body.offsetMax = new Vector2(body.offsetMax.x, body.offsetMax.y - 88f);

            var host = _hub.Find("Host");
            if (host != null)
            {
                Rewire(host, "Host public", () => HostMatch(true));
                Rewire(host, "Host private", () => HostMatch(false));
                var bot = Rewire(host, "Play vs bot", ChallengeCaptain);
                if (bot != null) HackUi.SetLabel(bot, "Vs " + HackathonRules.CaptainName);
            }

            var row = HackUi.TopStrip(_hub, "HackathonStake", 196f, 72f);
            var label = HackUi.TextAt(row, "Label", "Stake <size=70%><color=#9aa4ad>(devnet SOL, winner takes the pot)</color></size>", 28,
                TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, new Vector2(0.36f, 1f));
            label.textWrappingMode = TextWrappingModes.Normal;
            for (var i = 0; i < HackathonRules.Stakes.Length; i++)
            {
                var index = i;
                var x0 = 0.37f + i * 0.1575f;
                _stakeButtons[i] = HackUi.Button(row, HackathonRules.StakeLabel(HackathonRules.Stakes[i]), () => PickStake(index),
                    new Vector2(x0, 0.06f), new Vector2(x0 + 0.1475f, 0.94f));
            }
            var first = _stakeButtons[0] != null ? _stakeButtons[0].GetComponent<Image>() : null;
            if (first != null) _stakeOn = first.color;
            PickStake(0);

            _hubExplorer = HackUi.Button(_hub, "View payout", () => Open(_hubExplorerUrl), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var ert = (RectTransform)_hubExplorer.transform;
            ert.offsetMin = new Vector2(-284f, 22f);
            ert.offsetMax = new Vector2(-24f, 84f);
            _hubExplorer.gameObject.SetActive(false);
        }

        public void PickStake(int index)
        {
            _stakeIndex = Mathf.Clamp(index, 0, HackathonRules.Stakes.Length - 1);
            for (var i = 0; i < _stakeButtons.Length; i++)
            {
                var b = _stakeButtons[i];
                if (b == null) continue;
                var img = b.GetComponent<Image>();
                if (img != null) img.color = i == _stakeIndex ? _stakeOn : new Color(_stakeOn.r * 0.45f, _stakeOn.g * 0.45f, _stakeOn.b * 0.45f, 0.9f);
                var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null) t.fontStyle = i == _stakeIndex ? FontStyles.Bold : FontStyles.Normal;
            }
            if (_hubMessage != null && _hub.gameObject.activeInHierarchy)
                HubMessage(Stake > 0
                    ? $"Stake {HackathonRules.Sol(Stake)} each, the winner gets {HackathonRules.Sol(Stake * 2)}. Vs {HackathonRules.CaptainName} the house matches it."
                    : "Friendly match: no stake.", HackUi.Gold);
        }

        string StakeGate()
        {
            if (Stake <= 0) return null;
            if (!Web3Session.Instance.SignedIn) return "Connect a wallet (top of the menu) to play for SOL.";
            return null;
        }

        /// <summary>Hosts a lobby with the picked stake (the V4 host call, then the stake).</summary>
        public async void HostMatch(bool isPublic)
        {
            if (_busy) return;
            var gate = StakeGate();
            if (gate != null) { HubMessage(gate, HackUi.Bad); return; }
            _busy = true;
            try
            {
                HubMessage(Stake > 0 ? "Creating the match. Your wallet will ask you to sign the " + HackathonRules.Sol(Stake) + " deposit." : "Creating a lobby...", HackUi.Gold);
                var error = await Session.Host(isPublic);
                if (error == null && Stake > 0) error = await SetStake();
                if (this == null) return;
                if (error != null) { HubMessage(error, HackUi.Bad); if (!Session.InLobby) return; }
                _pvp.ShowLobby();
                if (error == null && Stake > 0 && !await DepositOrLeave(true)) return;
            }
            catch (Exception e) { HubMessage(e.Message, HackUi.Bad); }
            finally { _busy = false; }
        }

        /// <summary>"Vs Captain Marrow": a private lobby, the stake (the house matches it), then the AI captain's seat.</summary>
        public async void ChallengeCaptain()
        {
            if (_busy) return;
            var gate = StakeGate();
            if (gate != null) { HubMessage(gate, HackUi.Bad); return; }
            _busy = true;
            try
            {
                HubMessage(HackathonRules.CaptainName + " is raising his colours...", HackUi.Gold);
                var error = await Session.Host(false);
                if (error == null && Stake > 0) error = await SetStake();
                if (this == null) return;
                if (error == null && Stake > 0)
                {
                    _pvp.ShowLobby();
                    if (!await DepositOrLeave(true)) return;
                }
                if (error == null) error = await Session.AddBot();
                if (this == null) return;
                if (error != null) { HubMessage(error, HackUi.Bad); if (!Session.InLobby) return; }
                if (Stake > 0) _pvp.ShowLobby();
                else _pvp.ShowDraft();
            }
            catch (Exception e) { HubMessage(e.Message, HackUi.Bad); }
            finally { _busy = false; }
        }

        async Awaitable<string> SetStake()
        {
            var lobbyId = Session.Lobby?.lobbyId;
            HubMessage("Setting the stake to " + HackathonRules.Sol(Stake) + "...", HackUi.Gold);
            var (w, error) = await HackathonApi.SetStake(lobbyId, Stake);
            if (error != null) return "Lobby made, but the stake was refused: " + error;
            HackathonPvp.Adopt(lobbyId, w);
            if (w == null) await HackathonPvp.Refresh(lobbyId);
            return null;
        }

        void HubMessage(string text, Color color)
        {
            if (_hubMessage == null) return;
            _hubMessage.text = text ?? string.Empty;
            _hubMessage.color = color;
        }

        // ---------------------------------------------------------------- lobby

        void BuildLobby()
        {
            _lobbyMessage = _lobby.Find("Message")?.GetComponent<TextMeshProUGUI>();

            // The AI captain's banner (vs the bot only): portrait, name, a line of menace.
            _banner = HackUi.TopStrip(_lobby, "HackathonCaptainBanner", 500f, 64f);
            _banner.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.12f, 0.1f, 0.35f);
            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(_banner, false);
            var prt = (RectTransform)portrait.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(60f, 60f);
            prt.anchoredPosition = new Vector2(6f, 0f);
            var pimg = portrait.GetComponent<Image>();
            pimg.preserveAspect = true;
            pimg.raycastTarget = false;
            var sprite = CaptainPortrait();
            if (sprite != null) { pimg.sprite = sprite; pimg.color = new Color(1f, 0.72f, 0.66f, 1f); }
            else pimg.color = new Color(0f, 0f, 0f, 0f);
            _bannerText = HackUi.TextAt(_banner, "Text", string.Empty, 26, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, Vector2.one);
            _bannerText.margin = new Vector4(80f, 0f, 12f, 0f);
            _bannerText.textWrappingMode = TextWrappingModes.Normal;

            // The wager strip: stake, pot, both seats' deposits, and the deposit button.
            _strip = HackUi.TopStrip(_lobby, "HackathonWager", 572f, 76f);
            _strip.gameObject.AddComponent<Image>().color = new Color(1f, 0.82f, 0.4f, 0.1f);
            _wagerText = HackUi.TextAt(_strip, "Text", string.Empty, 23, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, new Vector2(0.64f, 1f));
            _wagerText.margin = new Vector4(14f, 0f, 8f, 0f);
            _wagerText.textWrappingMode = TextWrappingModes.Normal;
            // Creating or joining asks for the deposit by itself. This button is the fallback while the seat is unpaid:
            // "Pay deposit" (the automatic prompt was skipped or failed) or "Check deposit" (a sent transfer that devnet has
            // not confirmed yet; never pays twice).
            _deposit = HackUi.Button(_strip, "Pay deposit", Deposit, new Vector2(0.65f, 0.06f), new Vector2(1f, 0.94f));

            _status = HackUi.TopStrip(_lobby, "HackathonWagerStatus", 654f, 52f);
            _statusText = HackUi.TextAt(_status, "Text", string.Empty, 22, TextAlignmentOptions.MidlineLeft, HackUi.Gold, Vector2.zero, new Vector2(0.76f, 1f));
            _statusText.textWrappingMode = TextWrappingModes.Normal;
            _explorer = HackUi.Button(_status, "View tx", () => Open(_explorerUrl), new Vector2(0.78f, 0f), new Vector2(1f, 1f));

            _banner.gameObject.SetActive(false);
            _strip.gameObject.SetActive(false);
            _status.gameObject.SetActive(false);
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

        void RefreshLobby()
        {
            if (_strip == null) return;
            var lobby = Session.Lobby;
            var inLobby = Session.InLobby;
            var vsCaptain = inLobby && lobby.Guest != null && lobby.Guest.bot;
            _banner.gameObject.SetActive(vsCaptain);
            if (vsCaptain)
                _bannerText.text = $"<color=#ffd166><b>{HackathonRules.CaptainName}</b></color>  <color=#9aa4ad>{HackathonRules.CaptainTitle}</color>\n" +
                                   "<i>\"Sign the articles or feed the sharks, whelp.\"</i>";

            var w = inLobby ? HackathonPvp.For(lobby.lobbyId) : null;
            var hasWager = w != null && w.Exists;
            _strip.gameObject.SetActive(hasWager);
            _status.gameObject.SetActive(hasWager && (!string.IsNullOrEmpty(_statusText.text) || _explorerUrl != null));
            if (!hasWager) return;

            var me = Session.MyPlayerId;
            var iAmHost = lobby.hostPlayerId == me;
            var mySeat = iAmHost ? w.host : w.guest;
            var myDeposited = mySeat != null && mySeat.deposited;
            var stake = HackathonRules.Sol(w.stakeLamports);
            var hostName = lobby.Host != null ? lobby.Host.name : "Host";
            var guestName = lobby.Guest != null ? lobby.Guest.name : "Guest";
            _wagerText.text =
                $"<color=#ffd166><b>Wager {stake}</b></color> each   Pot <b>{HackathonRules.Sol(w.potLamports)}</b>   <size=80%><color=#9aa4ad>{Status(w)}</color></size>\n" +
                $"<size=90%>{hostName}: {Mark(w.host != null && w.host.deposited)}{HackathonRules.SeatFunding(w.host, false)}   " +
                $"{guestName}: {Mark(w.vsHouse || (w.guest != null && w.guest.deposited))}{HackathonRules.SeatFunding(w.guest, w.vsHouse)}</size>";

            var canDeposit = w.Open && !myDeposited && mySeat != null && !string.IsNullOrEmpty(mySeat.playerId);
            var pending = canDeposit && HackathonApi.HasPendingDeposit(lobby.lobbyId);
            _deposit.gameObject.SetActive(canDeposit && !_busy);
            _deposit.interactable = canDeposit && !_busy;
            HackUi.SetLabel(_deposit, pending ? "Check deposit" : "Pay deposit " + stake);

            var result = HackathonRules.ResultLine(w, me);
            if (result != null && !_busy) SetStatus(result, HackathonRules.ExplorerTx(HackathonRules.PayoutSignature(w)));
            else if (!_busy && w.funded && string.IsNullOrEmpty(_statusText.text)) SetStatus("Stake funded. The match starts when both captains are Ready.", null);
            if (_lobbyMessage != null && canDeposit && _busy && lobby.status != "starting" && lobby.status != "in_match")
                _lobbyMessage.text = $"Sign the {stake} deposit in your wallet. The match starts when both stakes are in.";
        }

        static string Mark(bool ok) => ok ? "<color=#7fd68b>[paid]</color> " : "<color=#9aa4ad>[ ]</color> ";

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
            _statusText.color = bad ? HackUi.Bad : HackUi.Gold;
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
        /// deposit; on a cancel or failure the lobby is left (nothing was paid) and the hub says why. A transfer that was
        /// sent but not yet confirmed keeps the lobby with "Check deposit". Returns true when the deposit is in.</summary>
        async Awaitable<bool> DepositOrLeave(bool host)
        {
            var lobby = Session.Lobby;
            var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
            if (w == null || !w.Exists) return true; // no stake: nothing to pay
            var stake = HackathonRules.Sol(w.stakeLamports);
            if (!Web3Session.Instance.WalletConnected)
            {
                await LeaveWith("Reconnect your wallet (Wallet button, top of the menu) to sign the " + stake + " deposit. The match was cancelled; nothing was paid.");
                return false;
            }
            var ok = await RunDeposit();
            if (this == null) return false;
            if (ok) return true;
            var line = HackathonRules.DepositFailureLine(host, stake, _lastSent, _lastRefunded, _lastError);
            if (_lastSent && !_lastRefunded)
            {
                // SOL left the wallet but the server has not taken it yet: stay in the lobby ("Check deposit").
                SetStatus(line, HackathonRules.ExplorerTx(Web3Session.Instance.LastTransferSignature), true);
                return false;
            }
            await LeaveWith(line);
            return false;
        }

        async Awaitable LeaveWith(string message)
        {
            if (Session.InLobby) await Session.Leave();
            if (this == null) return;
            _pvp.Open();
            HubMessage(message, HackUi.Bad);
        }

        /// <summary>"Pay deposit" / "Check deposit": pays this seat's stake, or re-checks a sent transfer (no second
        /// payment). A refused transfer is refunded by the server and the message says so.</summary>
        public async void Deposit()
        {
            if (_busy) return;
            if (await RunDeposit() || this == null) return;
            if (_lastRefunded)
            {
                var lobby = Session.Lobby;
                var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
                SetStatus(HackathonRules.DepositFailureLine(Session.IsHost, HackathonRules.Sol(w != null ? w.stakeLamports : 0), true, true, _lastError), null, true);
            }
        }

        /// <summary>Pays (or re-checks) this seat's stake. Returns true when the server has it.</summary>
        async Awaitable<bool> RunDeposit()
        {
            // No _busy check here: Host / Vs captain hold _busy while they call this (the callers guard).
            var lobby = Session.Lobby;
            var w = lobby != null ? HackathonPvp.For(lobby.lobbyId) : null;
            _lastSent = lobby != null && HackathonApi.HasPendingDeposit(lobby.lobbyId);
            _lastRefunded = false;
            _lastError = null;
            if (w == null) return false;
            if (!Web3Session.Instance.WalletConnected)
            {
                _lastError = "the wallet is not connected";
                var wallet = GetComponent<WalletMenu>();
                if (wallet != null) wallet.Open();
                SetStatus("Connect your wallet to sign the deposit.", null);
                return false;
            }
            _busy = true;
            RefreshLobby();
            try
            {
                var (updated, error, sent, refunded) = await HackathonApi.Deposit(w, m => { if (this != null) SetStatus(m, null); });
                _lastSent = sent;
                _lastRefunded = refunded;
                _lastError = error;
                if (this == null) return false;
                if (error != null) { SetStatus(error, null, true); return false; }
                var sig = Web3Session.Instance.LastTransferSignature;
                SetStatus("Deposit confirmed on devnet. Pick your build and press Ready.", HackathonRules.ExplorerTx(sig));
                if (updated != null) HackathonPvp.Adopt(lobby.lobbyId, updated);
                else await HackathonPvp.Refresh(lobby.lobbyId);
                return true;
            }
            catch (Exception e)
            {
                _lastSent = _lastSent || (lobby != null && HackathonApi.HasPendingDeposit(lobby.lobbyId));
                _lastError = e.Message;
                if (this != null) SetStatus(e.Message, null, true);
                return false;
            }
            finally
            {
                _busy = false;
                if (this != null) RefreshLobby();
            }
        }

        // ---------------------------------------------------------------- hub: the last wager's result

        void RefreshHub()
        {
            if (_hubExplorer == null) return;
            var w = HackathonPvp.Wager;
            var inLobby = Session.InLobby && Session.Lobby.lobbyId == HackathonPvp.WagerLobbyId;
            var result = !inLobby ? HackathonRules.ResultLine(w, Session.MyPlayerId ?? OnlineService.Active?.playerId) : null;
            var sig = result != null ? HackathonRules.PayoutSignature(w) : null;
            _hubExplorerUrl = HackathonRules.ExplorerTx(sig);
            _hubExplorer.gameObject.SetActive(_hubExplorerUrl != null);
            if (result != null && _hubMessage != null && !_busy && (string.IsNullOrEmpty(_hubMessage.text) || _hubMessage.text.StartsWith("Last wager") || _hubMessage.text.StartsWith("Left the lobby") || _hubMessage.text.StartsWith("No public lobbies")))
                HubMessage("Last wager: " + result + (HackathonRules.PayoutPending(w) ? " (payout on its way)" : string.Empty), HackUi.Good);
            if (_hubMessage != null)
            {
                var rt = _hubMessage.rectTransform;
                rt.offsetMax = new Vector2(_hubExplorerUrl != null ? -300f : -24f, rt.offsetMax.y);
            }
        }

        // ---------------------------------------------------------------- bits

        static void Open(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }

        static Button Rewire(Transform parent, string name, Action click)
        {
            var t = parent.Find(name);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) return null;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => click());
            return b;
        }

        static void Shift(Transform parent, string name, float down)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null) return;
            rt.offsetMin -= new Vector2(0f, down);
            rt.offsetMax -= new Vector2(0f, down);
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
