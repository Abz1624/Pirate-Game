using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the 1v1 session across scenes (one object, kept between the menu and the run): the lobby
    /// over the API (host public or private, join with a code or from an invite link, the public list, the practice bot,
    /// seat status and the draft), the lobby WebSocket (lobby pushes and the relay's match messages), the server clock,
    /// and the scene changes (the run scene when the match starts, the menu after it). The run's match controller and the
    /// menu panels listen to its events; everything runs on the main thread.
    /// V5 M8z (7 Oct): owned by the app root (<see cref="OnlineComposition"/> adds it to the app root's object, gives it
    /// the online services and the scene navigation); it was a lazy DontDestroyOnLoad object of its own that called the
    /// scene manager itself.
    /// </summary>
    public sealed class PvpSession : MonoBehaviour
    {
        public const string DraftPrefsKey = "rp.pvp.draft";
        const float PingSeconds = 2f;
        const float ReconnectSeconds = 1.5f;

        /// <summary>The app root's session (composed on first use, kept across scenes with the app root).</summary>
        public static PvpSession Instance => OnlineComposition.Pvp;

        public static bool Exists => OnlineComposition.HasPvp;

        OnlineServices _online;
        PvpSceneNavigation _scenes;

        /// <summary>V5 M8z (7 Oct): the composition root's online services and scene navigation.</summary>
        public void Bind(OnlineServices online, PvpSceneNavigation scenes)
        {
            _online = online;
            _scenes = scenes;
        }

        OnlineServices Online => _online ?? OnlineComposition.Online;
        string ActiveScene => _scenes != null ? _scenes.ActiveScene : string.Empty;
        void LoadScene(string scene) => _scenes?.Load(scene);

        /// <summary>A lobby code from an invite link (?join=CODE on WebGL), waiting for the menu to join it.</summary>
        public static string PendingJoinCode { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PendingJoinCode = null;
        }

        ILobbySocket _socket;
        string _socketLobbyId;
        bool _wantSocket;
        float _reconnectAt, _pingAt;
        long _offsetMs;
        bool _offsetMeasured;
        long _pingSentAt;

        public LobbyState Lobby { get; private set; }
        public MatchStartMsg Match { get; private set; }
        public string LastMatchEndJson { get; private set; }
        public MatchEndMsg LastMatchEnd { get; private set; }
        /// <summary>Who asked for a rematch (null: nobody yet).</summary>
        public string RematchRequestedBy { get; private set; }
        /// <summary>Set by the rematch: the menu opens the new lobby when it loads.</summary>
        public bool OpenLobbyInMenu { get; set; }
        public bool SocketOpen => _socket != null && _socket.IsOpen;
        public string LastError { get; private set; }
        /// <summary>Round trip of the last ping, ms.</summary>
        public long PingMs { get; private set; }

        /// <summary>The lobby state changed (pushes and API replies).</summary>
        public event Action<LobbyState> LobbyChanged;
        /// <summary>"match_start" (the first one, and every resync after a reconnect).</summary>
        public event Action<MatchStartMsg> MatchStarted;
        /// <summary>Any relay message during a match (type, raw JSON): state, fx, hit, barrels, presence, match_end, reject...</summary>
        public event Action<string, string> MatchMessage;
        /// <summary>A player-facing note (errors, the lobby closing).</summary>
        public event Action<string> Notice;

        public string MyPlayerId => Online.Active != null ? Online.Active.playerId : null;
        string Token => Online.Active != null ? Online.Active.token : null;
        public bool IsHost => Lobby != null && !string.IsNullOrEmpty(MyPlayerId) && Lobby.hostPlayerId == MyPlayerId;
        public LobbySeat MySeat => Lobby != null ? Lobby.Seat(MyPlayerId) : null;
        public LobbySeat OpponentSeat
        {
            get
            {
                if (Lobby?.seats == null) return null;
                foreach (var s in Lobby.seats) if (s != null && s.playerId != MyPlayerId) return s;
                return null;
            }
        }

        public bool InLobby => Lobby != null && !string.IsNullOrEmpty(Lobby.lobbyId) && Lobby.status != "closed" && Lobby.status != "expired";

        // ---- Clock ----

        public static long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        /// <summary>The server's clock (UTC ms) as estimated from ping/pong (or match_start's serverNow).</summary>
        public long ServerNowMs => UtcNowMs + _offsetMs;

        // ---- The draft (kept on this device between matches) ----

        public PvpDraft SavedDraft
        {
            get
            {
                var json = PlayerPrefs.GetString(DraftPrefsKey, string.Empty);
                if (string.IsNullOrEmpty(json)) return null;
                try { return JsonUtility.FromJson<PvpDraft>(json); }
                catch (Exception) { return null; }
            }
            set
            {
                PlayerPrefs.SetString(DraftPrefsKey, value != null ? JsonUtility.ToJson(value) : string.Empty);
                PlayerPrefs.Save();
            }
        }

        // ---- API ----

        ApiClient Client => Online.Client;

        /// <summary>Online with a registered profile, or a player-facing reason.</summary>
        public async Awaitable<string> EnsureReady()
        {
            if (!await Online.EnsureConnected()) return "Offline: the PvP server is not reachable.";
            var p = Online.Active;
            if (p == null) return "Make a captain profile first.";
            if (!p.Registered) await Online.SyncActive();
            return Online.Active != null && Online.Active.Registered ? null : "Your profile is not online yet.";
        }

        public async Awaitable<string> Host(bool isPublic)
        {
            var error = await EnsureReady();
            if (error != null) return error;
            var r = await Client.Post<LobbyState>("/api/lobbies", new CreateLobbyRequest { @public = isPublic }, Token);
            return Adopt(r);
        }

        public async Awaitable<string> Join(string code)
        {
            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < 4) return "Enter the lobby code.";
            var error = await EnsureReady();
            if (error != null) return error;
            var r = await Client.Post<LobbyState>("/api/lobbies/join", new JoinLobbyRequest { code = code }, Token);
            if (!r.Ok)
            {
                switch (r.ErrorCode)
                {
                    case "lobby_full": return "That lobby is full.";
                    case "lobby_closed": return "That lobby is closed.";
                    case "own_lobby": return "That is your own lobby.";
                }
                if (r.Status == 404) return "No lobby with that code.";
            }
            return Adopt(r);
        }

        /// <summary>Hosts a private lobby and seats the practice bot.</summary>
        public async Awaitable<string> PlayBot()
        {
            var error = await Host(false);
            if (error != null) return error;
            return await AddBot();
        }

        public async Awaitable<string> AddBot()
        {
            if (!InLobby) return "No lobby.";
            var r = await Client.Send<LobbyState>("POST", "/api/lobbies/" + Lobby.lobbyId + "/bot", null, Token, 0);
            return Adopt(r);
        }

        public async Awaitable<ApiResult<PublicLobbiesResponse>> PublicLobbies()
        {
            await Online.EnsureConnected();
            return await Client.Get<PublicLobbiesResponse>("/api/lobbies/public");
        }

        /// <summary>Seat status (joined, drafting, ready); a draft goes with it when given.</summary>
        public async Awaitable<string> SetStatus(string status, PvpDraft draft = null)
        {
            if (!InLobby) return "No lobby.";
            object body = draft != null ? new LobbyStatusDraftRequest { status = status, draft = draft } : (object)new LobbyStatusRequest { status = status };
            var r = await Client.Send<LobbyState>("PUT", "/api/lobbies/" + Lobby.lobbyId + "/status", body, Token, 0);
            if (!r.Ok && r.ErrorCode == "already_started") return null; // the match is on its way
            return Adopt(r);
        }

        /// <summary>Leaves the lobby (forfeits a running match) and forgets it.</summary>
        public async Awaitable Leave()
        {
            var lobby = Lobby;
            _wantSocket = false;
            if (_socket != null && _socket.IsOpen && Match != null && LastMatchEnd == null) _socket.Send("{\"t\":\"leave\"}");
            if (lobby != null && !string.IsNullOrEmpty(lobby.lobbyId) && Token != null)
            {
                try { await Client.Send<LobbyState>("PUT", "/api/lobbies/" + lobby.lobbyId + "/status", new LobbyStatusRequest { status = "left" }, Token, 0); }
                catch (Exception e) { Debug.LogWarning("PvpSession.Leave: " + e.Message); }
            }
            Forget();
        }

        /// <summary>Drops the lobby, the match and the socket (no API call).</summary>
        public void Forget()
        {
            _wantSocket = false;
            CloseSocket();
            Lobby = null;
            Match = null;
            LastMatchEnd = null;
            LastMatchEndJson = null;
            RematchRequestedBy = null;
            OpenLobbyInMenu = false;
            LobbyChanged?.Invoke(null);
        }

        /// <summary>Asks for a rematch (the other player, or the bot, must agree within 60 s of the end). Returns why it
        /// cannot be sent, or null.</summary>
        public string RequestRematch()
        {
            if (LastMatchEnd != null && ServerNowMs > LastMatchEnd.rematchUntil) return "The rematch window closed (60 s after the end).";
            if (!SocketOpen) return "Not connected to the server.";
            _socket.Send("{\"t\":\"rematch\"}");
            return null;
        }

        string Adopt(ApiResult<LobbyState> r)
        {
            if (!r.Ok || r.Data == null || string.IsNullOrEmpty(r.Data.lobbyId))
            {
                LastError = r.IsOffline ? "Offline: the PvP server is not reachable." : (r.Message ?? "The server refused.");
                return LastError;
            }
            SetLobby(r.Data);
            return null;
        }

        void SetLobby(LobbyState state)
        {
            var newLobby = Lobby == null || Lobby.lobbyId != state.lobbyId;
            Lobby = state;
            if (newLobby)
            {
                Match = null;
                LastMatchEnd = null;
                LastMatchEndJson = null;
                RematchRequestedBy = null;
            }
            _wantSocket = InLobby;
            if (_wantSocket && (_socket == null || _socketLobbyId != state.lobbyId)) OpenSocket();
            LobbyChanged?.Invoke(state);
            if (state.status == "closed" || state.status == "expired")
            {
                Notice?.Invoke(state.status == "closed" ? "The host closed the lobby." : "The lobby expired.");
                _wantSocket = false;
            }
        }

        // ---- Socket ----

        void OpenSocket()
        {
            CloseSocket();
            var ws = LobbySockets.WsBase(Client.BaseUrl);
            if (ws == null || Lobby == null || Token == null) return;
            _socket = LobbySockets.Create();
            _socketLobbyId = Lobby.lobbyId;
            _socket.Connect(ws + "/ws/lobbies/" + Lobby.lobbyId + "?token=" + Uri.EscapeDataString(Token));
        }

        void CloseSocket()
        {
            _socket?.Dispose();
            _socket = null;
            _socketLobbyId = null;
        }

        /// <summary>Sends a match message (JsonUtility DTO) on the lobby socket.</summary>
        public void Send(object message)
        {
            if (_socket == null || !_socket.IsOpen || message == null) return;
            _socket.Send(JsonUtility.ToJson(message));
        }

        public void SendRaw(string json)
        {
            if (_socket == null || !_socket.IsOpen || string.IsNullOrEmpty(json)) return;
            _socket.Send(json);
        }

        void Update()
        {
            if (_socket != null)
            {
                // A burst of messages (a reconnect resync) is handled in one frame.
                for (var i = 0; i < 256 && _socket != null && _socket.Poll(out var e); i++) Handle(e);
            }
            var now = Time.unscaledTime;
            if (_wantSocket && InLobby && (_socket == null || (!_socket.IsOpen && now >= _reconnectAt && _reconnectAt > 0f)))
            {
                _reconnectAt = 0f;
                OpenSocket();
            }
            if (_socket != null && _socket.IsOpen && now >= _pingAt)
            {
                _pingAt = now + PingSeconds;
                _pingSentAt = UtcNowMs;
                Send(new PingMsg { cs = _pingSentAt });
            }
        }

        void Handle(SocketEvent e)
        {
            switch (e.Kind)
            {
                case SocketEventKind.Opened:
                    _pingAt = 0f;
                    break;
                case SocketEventKind.Closed:
                    // The server closes a finished lobby's socket after the rematch window: nothing to reconnect to.
                    if (Lobby != null && Lobby.status == "finished" && LastMatchEnd != null && UtcNowMs + _offsetMs > LastMatchEnd.rematchUntil) _wantSocket = false;
                    if (_wantSocket) _reconnectAt = Time.unscaledTime + ReconnectSeconds;
                    break;
                case SocketEventKind.Message:
                    OnMessage(e.Text);
                    break;
            }
        }

        void OnMessage(string json)
        {
            var t = PvpJson.TypeOf(json);
            switch (t)
            {
                case "lobby":
                {
                    LobbyState state;
                    try { state = JsonUtility.FromJson<LobbyState>(json); }
                    catch (Exception ex) { Debug.LogWarning("PvpSession: unreadable lobby: " + ex.Message); return; }
                    if (state != null && !string.IsNullOrEmpty(state.lobbyId) && (Lobby == null || state.lobbyId == Lobby.lobbyId)) SetLobby(state);
                    return;
                }
                case "pong":
                {
                    var pong = JsonUtility.FromJson<PongMsg>(json);
                    if (pong == null || pong.cs <= 0) return;
                    var now = UtcNowMs;
                    PingMs = now - pong.cs;
                    var offset = PvpRules.ClockOffset(pong.cs, now, pong.st);
                    // Smooth after the first measure (jitter), jump on the first.
                    _offsetMs = _offsetMeasured ? (_offsetMs * 3 + offset) / 4 : offset;
                    _offsetMeasured = true;
                    return;
                }
                case "match_start":
                {
                    var start = JsonUtility.FromJson<MatchStartMsg>(json);
                    if (start == null || string.IsNullOrEmpty(start.matchId)) return;
                    if (!_offsetMeasured) _offsetMs = start.serverNow - UtcNowMs;
                    var fresh = Match == null || Match.matchId != start.matchId;
                    Match = start;
                    if (fresh)
                    {
                        LastMatchEnd = null;
                        LastMatchEndJson = null;
                        RematchRequestedBy = null;
                    }
                    var scene = ActiveScene;
                    if (start.live != null && start.live.ended)
                    {
                        // Playtest (4 Oct): back after the end (the socket dropped): the running match ends too (the
                        // server sends its "match_end" again right after this).
                        if (scene == V4SceneNames.SaltwindRun) MatchStarted?.Invoke(start);
                        MatchMessage?.Invoke(t, json);
                        return;
                    }
                    // Playtest (3 Oct, owner, round 7: a solo voyage came up as a 1v1): a late match_start only opens the
                    // match from the menu, and only for the lobby we are still in; in a run it is the running match's.
                    if (scene == V4SceneNames.SaltwindRun) MatchStarted?.Invoke(start);
                    else if (scene == V4SceneNames.MainMenu && Lobby != null && (string.IsNullOrEmpty(start.lobbyId) || start.lobbyId == Lobby.lobbyId)) StartRunScene(start);
                    return;
                }
                case "match_end":
                    LastMatchEndJson = json;
                    LastMatchEnd = JsonUtility.FromJson<MatchEndMsg>(json);
                    MatchMessage?.Invoke(t, json);
                    return;
                case "rematch":
                {
                    var m = JsonUtility.FromJson<RematchMsg>(json);
                    if (m == null) return;
                    if (m.status == "requested") { RematchRequestedBy = m.by; MatchMessage?.Invoke(t, json); return; }
                    if (m.status == "created" && !string.IsNullOrEmpty(m.lobbyId)) { MatchMessage?.Invoke(t, json); MoveToRematch(m.lobbyId); }
                    // V5 M8z (6 Oct, owner): the server could not create it; the requests stand and Rematch tries again
                    if (m.status == "failed") MatchMessage?.Invoke(t, json);
                    return;
                }
                case null:
                    return;
                default:
                    MatchMessage?.Invoke(t, json);
                    return;
            }
        }

        /// <summary>The rematch lobby: read it, connect to it, and go back to the menu's lobby screen to ready up.</summary>
        async void MoveToRematch(string lobbyId)
        {
            try
            {
                var r = await Client.Get<LobbyState>("/api/lobbies/" + lobbyId, Token);
                if (!r.Ok || r.Data == null) { Notice?.Invoke("Could not open the rematch lobby."); return; }
                Lobby = null; // a new lobby: SetLobby resets the match and reconnects
                SetLobby(r.Data);
                OpenLobbyInMenu = true;
                if (ActiveScene != V4SceneNames.MainMenu) LoadScene(V4SceneNames.MainMenu);
            }
            catch (Exception e) { Debug.LogWarning("PvpSession.MoveToRematch: " + e.Message); }
        }

        void StartRunScene(MatchStartMsg start)
        {
            RunRequest.RequestPvp(start.seed);
            Composition.MenuServices.BeginSaltwindExperiment();
            LoadScene(V4SceneNames.SaltwindRun);
        }

        /// <summary>The run's end screen: back to the menu (the lobby is left when the match is over).</summary>
        public void ReturnToMenu(bool leaveLobby)
        {
            if (leaveLobby) Forget();
            LoadScene(V4SceneNames.MainMenu);
        }

        void OnDestroy()
        {
            CloseSocket();
        }

        void OnApplicationQuit() => CloseSocket();

        /// <summary>The players of the current match by seat (for labels).</summary>
        public IEnumerable<MatchPlayer> MatchPlayers => Match?.players ?? Array.Empty<MatchPlayer>();
    }
}
