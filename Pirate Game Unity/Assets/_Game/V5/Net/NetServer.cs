using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using RoguePirates.V4;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Progression;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the authoritative match server for one ship (a single-player server voyage). It runs on the
    /// server composition (<see cref="NetServerComposition"/>): the run's own modules own hull, damage, armor,
    /// regeneration, items and sinking; this component only
    /// <list type="bullet">
    /// <item>admits a client with a signed one-time join ticket (<see cref="JoinTicketVerifier"/>), or a resume token
    /// after a dropped socket (kept <see cref="ResumeGraceSeconds"/>);</item>
    /// <item>applies its inputs, one per client tick, to the run's ship at a fixed 30 Hz (<see cref="ShipMotionPort"/>),
    /// with a small jitter buffer: a missing input repeats the last one, a backlog is caught up;</item>
    /// <item>sends a snapshot every 2nd tick (15 Hz): the ship's full motion state (the client rewinds to it) with the
    /// newest input applied, the hull and the run's counters; and the enemies as batched, quantized, interest-managed
    /// messages (<see cref="NetProtocol"/>), not NetworkObjects.</item>
    /// </list>
    /// Clients send inputs only. Anything else a client sends (a hull, a hit, a sinking) has no handler that reads it.
    /// </summary>
    [DefaultExecutionOrder(5)]
    public sealed class NetServer : MonoBehaviour
    {
        public NetworkManager Manager { get; private set; }
        public UnityTransport Transport { get; private set; }
        public readonly NetMetrics Metrics = new NetMetrics("server");
        public JoinTicketVerifier Tickets { get; set; }
        public float ResumeGraceSeconds = 30f;
        public uint ServerTick { get; private set; }
        /// <summary>
        /// V5 M9b owner re-test fixes (10 Oct, protocol 5): the server's world tick, the enemy batches' clock. It counts the
        /// server ticks on which the run's world moves (not paused for the level-up cards, not held while the player is
        /// away or before the first inputs), so it stands still while the enemies do. The client's enemy clock stops with
        /// its own level-up hold and, after the pick, runs on from where it stopped: the enemies move again on the client
        /// the moment its ship does, instead of after the pick's round trip plus the render delay standing still.
        /// </summary>
        public uint WorldTick { get; private set; } = 1;
        /// <summary>Tests: false sends the enemy batches on the server tick (protocol 4's clock), to measure both; back on, the
        /// world tick continues from the server tick (the client's enemy clock never goes back).</summary>
        public bool EnemyClockIsWorldTick
        {
            get => _worldClock;
            set { if (value && !_worldClock) WorldTick = Math.Max(WorldTick, ServerTick); _worldClock = value; }
        }
        bool _worldClock = true;
        /// <summary>V5 M9b owner re-test fixes (10 Oct): chest choices (the Salvage Chest's and the Rare Treasure Chest's Take or
        /// Skip, which pause the run until the player answers) skipped by the server: the choice is not mirrored to the
        /// client yet (M9c), so the paused server run held the ship for good while the client sailed on (the owner's
        /// stuck ship, kills frozen). Skipped, the chest's prompt stays closed until the ship leaves its range.</summary>
        public int ChestChoicesSkipped { get; private set; }
        public int Seed { get; private set; }

        /// <summary>Client reports (hull, hits) received and dropped unread; input bytes beyond the inputs, dropped.</summary>
        public int IgnoredClientReports { get; private set; }
        public int IgnoredInputBytes { get; private set; }
        public int RejectedJoins { get; private set; }
        public string LastRejectReason { get; private set; }
        public bool EndSent { get; private set; }

        public V4Game Game { get; private set; }
        public ShipMotionPort Ship { get; private set; }
        /// <summary>V5 M9c S0b: a test-only measurement hook (the load probe), called at the end of every tick, inside the
        /// tick time; null on every real server.</summary>
        public INetServerProbe Probe { get; set; }
        /// <summary>V5 M9c S0b: the payload of the newest ship snapshot sent (the probe's own-snapshot size).</summary>
        public int LastShipSnapshotBytes { get; private set; }
        IEnemyWorldView _enemies;
        RoguePirates.V4.Stage.IStageContent _stage;

        public sealed class Session
        {
            public string playerId;
            public ulong clientId;
            public bool connected;
            public byte[] resumeToken;
            public float detachedAt = -1f;
            public float droppedAt = -1f;
            /// <summary>The next client tick to apply; 0 until the first input after a welcome.</summary>
            public uint expectNext;
            public bool started;
            public uint lastAck;
            /// <summary>V5 M9b review: the newest client input tick received (a named level-up hold is clamped to it).</summary>
            public uint newestReceived;
            public NetInput last;
            public readonly NetInput[] ring = new NetInput[64];
            public readonly uint[] ringTick = new uint[64];
            public readonly HashSet<int> sentDefs = new HashSet<int>();
            public int snapshots;
            public bool expired;
            /// <summary>M9b playtest fixes: the player's page is hidden (the solo run holds) since <see cref="awaySince"/>.</summary>
            public bool away;
            public float awaySince = -1f;
            /// <summary>Ticks guessed in a row (the client's input was not there in time).</summary>
            public int guessRun;
            /// <summary>V5 M9b close: the least input margin at the ticks applied since the last snapshot (negative: a tick
            /// whose input was not there in time, guessed); int.MaxValue when none was applied.</summary>
            public int leastMargin = int.MaxValue;
        }

        public Session Current { get; private set; }

        /// <summary>V5 M9b part 2a: a ticket join went through (the player id).</summary>
        public event Action<string> Joined;
        /// <summary>V5 M9b part 2b: the player's first inputs arrived (its stage is built and it sails): the match server's
        /// host starts the held run then, so loading the client's stage costs the player nothing.</summary>
        public event Action SessionStarted;
        bool _sessionEverStarted;

        /// <summary>V5 M9b part 2b: the run's level-up cards, played with the player over the network.</summary>
        public NetLevelUps LevelUps { get; private set; }
        public int RejectedPicks => LevelUps != null ? LevelUps.Rejected : 0;
        /// <summary>Set once <see cref="CloseGracefully"/> starts (a docker stop, a Railway redeploy).</summary>
        public bool Closing { get; private set; }

        /// <summary>The kind of session this server runs (a ticket for another mode is refused).</summary>
        public string Mode = "voyage";
        /// <summary>Ship steps applied from inputs: at most one per server tick, whatever the client sends.</summary>
        public long StepsApplied { get; private set; }
        public long TicksWithSession { get; private set; }
        ulong _kick;
        bool _kickPending;
        double _lastRealtime = -1;

        float _accumulator;
        int _snapshotIndex;
        readonly NetWriter _writer = new NetWriter(1024);
        readonly NetInput[] _incoming = new NetInput[32];
        readonly List<(float d2, EnemySnapshot e)> _interest = new List<(float, EnemySnapshot)>(512);
        readonly (byte, string)[] _defs = new (byte, string)[256];
        readonly Stopwatch _work = new Stopwatch();
        double _pendingWorkMs;

        /// <summary>A server object (kept across scene loads) listening on plain WebSockets at <paramref name="port"/>.</summary>
        public static NetServer Create(ushort port, JoinTicketVerifier tickets, string listenAddress = "0.0.0.0", string name = "V5NetServer")
        {
            var go = new GameObject(name);
            DontDestroyOnLoad(go);
            go.AddComponent<ServerWorkBegin>();
            go.AddComponent<ServerWorkEnd>();
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = NetProtocol.TickRate;
            transport.DisconnectTimeoutMS = NetProtocol.DisconnectTimeoutMs;
            NetEndpoint.ApplyServer(transport, port, listenAddress);
            var server = go.AddComponent<NetServer>();
            server.Manager = manager;
            server.Transport = transport;
            server.Tickets = tickets;
            return server;
        }

        /// <summary>Takes the run over: its ship is stepped by the net tick from now on.</summary>
        public void Attach(V4Game game)
        {
            Game = game;
            Ship = ShipMotionPort.Of(game);
            if (Ship == null) throw new InvalidOperationException("the run has no ship");
            Ship.TakeControl();
            game.Context.TryGet(out _enemies);
            game.Context.TryGet(out _stage);
            Seed = RunManager.Instance != null ? RunManager.Instance.RunSeed : 0;
            // V5 M9b part 2b: the run deals its cards to the player through the network (the module pauses the run and waits
            // for the pick as in a voyage); a run started with automatic level-ups (a test scenario) keeps them.
            LevelUps = new NetLevelUps
            {
                NextInputTick = () => Current == null ? 1u : Current.expectNext != 0 ? Current.expectNext : Current.lastAck + 1,
                NewestInputTick = () => Current == null ? 0u : Current.newestReceived,
                ClientCanNameHold = () => Current != null && Current.connected && !Current.expired && Current.started,
                CatchUp = CatchUpHeldTicks,
                Rewind = RewindForHold,
                Send = SendOffer,
                SendClosed = SendOfferClosed,
                PendingLevelUps = () => game.Build != null ? game.Build.Progression.PendingLevelUps : 0,
            };
            game.Context.Replace<IOfferPresenter>(LevelUps);
        }

        public bool StartListening()
        {
            if (Tickets == null || !Tickets.HasKey)
            {
                Debug.LogError($"V5 net server: no join ticket key ({JoinTicket.SecretEnvironmentVariable} unset or shorter than {JoinTicket.MinSecretLength} characters); not listening.");
                return false;
            }
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientConnectedCallback += OnConnected;
            Manager.OnClientDisconnectCallback += OnDisconnected;
            if (!Manager.StartServer()) return false;
            var messages = Manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(NetProtocol.Input, OnInput);
            messages.RegisterNamedMessageHandler(NetProtocol.ClientReport, OnClientReport);
            messages.RegisterNamedMessageHandler(NetProtocol.Pick, OnPick);
            messages.RegisterNamedMessageHandler(NetProtocol.Presence, OnPresence);
            Metrics.Reset();
            return true;
        }

        public void Stop()
        {
            ReleaseAwayHold();
            if (Manager == null) return;
            if (Manager.IsListening)
            {
                if (Current != null && Current.connected && !EndSent) SendEnd(NetProtocol.EndReason.ServerClosing);
                Manager.Shutdown();
            }
        }

        /// <summary>
        /// V5 M9b part 2a: the server is going down (SIGTERM from docker stop or a Railway redeploy). The connected player
        /// gets <c>end</c> ServerClosing (no settlement; the client goes back to the lobby), the transport has
        /// <paramref name="flushSeconds"/> of frames to send it, then the server stops listening.
        /// </summary>
        public System.Collections.IEnumerator CloseGracefully(float flushSeconds = 0.5f)
        {
            if (Closing) yield break;
            Closing = true;
            if (Manager == null || !Manager.IsListening) yield break;
            if (Current != null && Current.connected && !EndSent) SendEnd(NetProtocol.EndReason.ServerClosing);
            var until = Time.realtimeSinceStartup + flushSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            Manager.Shutdown();
            yield return null;
            yield return null;
        }

        void OnDestroy()
        {
            ReleaseAwayHold();
            if (Manager != null)
            {
                Manager.OnClientConnectedCallback -= OnConnected;
                Manager.OnClientDisconnectCallback -= OnDisconnected;
            }
        }

        // ---- Admission ------------------------------------------------------------------------------------------------

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            var reason = Admit(request.Payload, request.ClientNetworkId);
            response.Approved = reason == null;
            response.Reason = reason;
            if (reason != null)
            {
                RejectedJoins++;
                LastRejectReason = reason;
                Debug.Log($"[V5 Net] join refused: {reason}");
            }
        }

        /// <summary>Null when admitted; else the reason (sent to the client).</summary>
        string Admit(byte[] payload, ulong clientId)
        {
            if (Ship == null) return "server not ready";
            if (payload == null || payload.Length < 2) return "no join ticket";
            if (payload[0] == NetProtocol.ConnectResume)
            {
                var token = new byte[payload.Length - 1];
                Array.Copy(payload, 1, token, 0, token.Length);
                var s = Current;
                if (s == null || s.expired || !JoinTicket.SameBytes(token, s.resumeToken)) return "resume refused";
                // A silent drop: the server may not have noticed the old socket go yet (the transport's timeout); the
                // resume token proves it is the same player, so the old connection is let go.
                if (s.connected && s.clientId != clientId) { _kick = s.clientId; _kickAt = 0f; _kickPending = true; s.droppedAt = Time.realtimeSinceStartup; }
                s.resumeToken = NewToken(); // a fresh token in every welcome
                Bind(s, clientId);
                return null;
            }
            if (payload[0] != NetProtocol.ConnectTicket) return "no join ticket";
            var ticket = System.Text.Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
            var error = Tickets.Verify(ticket, out var claims, false);
            if (error != JoinTicketError.None) return "ticket " + error;
            if (claims.mode != Mode) return "ticket for mode " + claims.mode;
            if (Current != null && !Current.expired && Current.playerId != claims.sub) return "voyage taken";
            if (Current != null && Current.connected) return "already connected";
            Tickets.Consume(claims); // used only once the join goes through
            var session = Current != null && !Current.expired ? Current : new Session { playerId = claims.sub };
            session.resumeToken = NewToken();
            Current = session;
            Bind(session, clientId);
            Joined?.Invoke(claims.sub);
            return null;
        }

        static byte[] NewToken()
        {
            var token = new byte[NetProtocol.ResumeTokenBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(token);
            return token;
        }

        void Bind(Session s, ulong clientId)
        {
            s.clientId = clientId;
            s.connected = true;
            s.started = false;
            s.expectNext = 0;
            s.lastAck = 0;
            s.newestReceived = 0;
            s.guessRun = 0;
            s.last = default;
            Array.Clear(s.ringTick, 0, s.ringTick.Length);
            s.sentDefs.Clear();
        }

        void OnConnected(ulong clientId)
        {
            var s = Current;
            if (s == null || s.clientId != clientId) return;
            if (s.droppedAt >= 0f) { Metrics.ResumeSeconds.Add(Time.realtimeSinceStartup - s.droppedAt); s.droppedAt = -1f; }
            s.detachedAt = -1f;
            // M9b playtest fixes: a (re)connected client is present (it says so again at once if its page is still hidden).
            if (s.away) SetAway(s, false, false);
            SendWelcome(s);
        }

        /// <summary>The session's current state as a welcome (a join, a resume, or a player back from away), then the cards
        /// still up and the end if it was sent. The ship waits for the client's first input after it.</summary>
        void SendWelcome(Session s)
        {
            var clientId = s.clientId;
            _writer.Clear();
            NetCodec.WriteWelcome(_writer, new WelcomeMessage
            {
                resumeToken = s.resumeToken,
                serverTick = ServerTick,
                tickRate = NetProtocol.TickRate,
                snapshotEvery = NetProtocol.SnapshotEveryTicks,
                seed = Seed,
                ship = BuildShip(s),
            });
            s.leastMargin = int.MaxValue; // V5 M9b close: a welcome starts the margin afresh
            Send(NetProtocol.Welcome, clientId, NetworkDelivery.ReliableSequenced);
            if (LevelUps != null && LevelUps.Pending) SendOffer(LevelUps.Message()); // the cards still up after a resume
            if (EndSent) SendEnd(LastEndReason);
        }

        void OnDisconnected(ulong clientId)
        {
            var s = Current;
            if (s == null || s.clientId != clientId || !s.connected) return;
            s.connected = false;
            s.detachedAt = Time.realtimeSinceStartup;
            s.droppedAt = Time.realtimeSinceStartup;
            s.last = default; // a detached ship coasts
        }

        /// <summary>
        /// V5 M9b review: the session ends without a voyage (a client that joined and never sent an input): <c>end</c>
        /// SessionExpired, the session expired (the voyage is free: the match server's host makes way for a fresh one), and
        /// the socket let go a moment later.
        /// </summary>
        public void ExpireSession(string why)
        {
            var s = Current;
            if (s == null || s.expired) return;
            Debug.Log("[V5 Net] session dropped: " + why);
            if (s.connected && !EndSent) SendEnd(NetProtocol.EndReason.SessionExpired);
            s.expired = true;
            if (s.connected) { _kick = s.clientId; _kickAt = Time.realtimeSinceStartup + 0.5f; _kickPending = true; }
        }

        float _kickAt;

        /// <summary>Tests: the socket drops (the session stays for its resume token).</summary>
        public void DropConnectionForTest()
        {
            if (Current != null && Current.connected) Manager.DisconnectClient(Current.clientId, "connection dropped");
        }

        // ---- Away (M9b playtest fixes) ---------------------------------------------------------------------------------

        /// <summary>An away player who does not come back in this long ends the voyage (Abandoned).</summary>
        public float AwayMaxSeconds = NetProtocol.AwayMaxSeconds;
        public int AwaysReceived { get; private set; }
        public int BacksReceived { get; private set; }
        public bool AwayHolding => _timeScaleBeforeAway >= 0f;
        float _timeScaleBeforeAway = -1f;

        void OnPresence(ulong sender, FastBufferReader reader)
        {
            var bytes = ReadAll(reader);
            Metrics.Up(sender, bytes.Length);
            var s = Current;
            if (s == null || !s.connected || s.clientId != sender || s.expired) return;
            bool away;
            try { if (!NetCodec.ReadPresence(new NetReader(bytes), out away)) return; }
            catch (Exception) { return; }
            if (away == s.away) return;
            SetAway(s, away, true);
        }

        /// <summary>
        /// A solo voyage holds while its player's page is hidden, as the normal game stops in a hidden tab: the run's clock
        /// stops (time scale 0: spawns, enemies, timers; the ship is not stepped), the card timeout waits, no snapshots go
        /// out, and a dropped socket is kept until the player is back. Back (<paramref name="resync"/>): the ship and the
        /// cards still up come again in a welcome, and the ship waits for the client's next input, as after a resume.
        /// A shared run (M9c) cannot hold for one player: it would need a per-session clock and no pause (recorded in the plan).
        /// </summary>
        void SetAway(Session s, bool away, bool resync)
        {
            s.away = away;
            if (away)
            {
                AwaysReceived++;
                s.awaySince = Time.realtimeSinceStartup;
                if (_timeScaleBeforeAway < 0f) { _timeScaleBeforeAway = Time.timeScale; Time.timeScale = 0f; }
                LevelUps?.PauseClock(true);
                Debug.Log("[V5 Net] player away (page hidden): the voyage holds");
                return;
            }
            BacksReceived++;
            var awayFor = s.awaySince >= 0f ? Time.realtimeSinceStartup - s.awaySince : 0f;
            s.awaySince = -1f;
            ReleaseAwayHold();
            LevelUps?.PauseClock(false);
            Debug.Log($"[V5 Net] player back after {awayFor:0.0} s away: the voyage plays on");
            if (!resync || !s.connected) return;
            Bind(s, s.clientId);
            SendWelcome(s);
        }

        void ReleaseAwayHold()
        {
            if (_timeScaleBeforeAway < 0f) return;
            Time.timeScale = _timeScaleBeforeAway;
            _timeScaleBeforeAway = -1f;
        }

        /// <summary>The away player did not come back within <see cref="AwayMaxSeconds"/>: the voyage ends (Abandoned).</summary>
        void Abandon(Session s)
        {
            Debug.Log($"[V5 Net] player away over {AwayMaxSeconds:0} s: the voyage ends (Abandoned)");
            s.away = false;
            ReleaseAwayHold();
            LevelUps?.PauseClock(false);
            if (!EndSent) SendEnd(NetProtocol.EndReason.Abandoned);
            s.expired = true;
            if (s.connected) { _kick = s.clientId; _kickAt = Time.realtimeSinceStartup + 0.5f; _kickPending = true; }
        }

        // ---- Client messages --------------------------------------------------------------------------------------

        void OnInput(ulong sender, FastBufferReader reader)
        {
            var s = Current;
            var bytes = ReadAll(reader);
            Metrics.Up(sender, bytes.Length);
            if (s == null || !s.connected || s.clientId != sender) return;
            NetReader r;
            try
            {
                r = new NetReader(bytes);
                if (!NetCodec.ReadInputs(r, out var newest, _incoming, out var count, out var ignored)) return;
                IgnoredInputBytes += ignored;
                if (count == 0) return;
                if (s.expectNext == 0) s.expectNext = newest - (uint)(count - 1); // the first input after a welcome
                if (newest > s.newestReceived) s.newestReceived = newest;
                for (var i = 0; i < count; i++)
                {
                    var tick = newest - (uint)i;
                    if (tick < s.expectNext) { if (i == 0) Metrics.LateInputs++; continue; }
                    var slot = (int)(tick % (uint)s.ring.Length);
                    if (tick - s.expectNext >= s.ring.Length) continue; // too far ahead
                    s.ring[slot] = _incoming[i];
                    s.ringTick[slot] = tick;
                }
            }
            catch (Exception) { /* malformed: dropped */ }
        }

        /// <summary>V5 M9b part 2b: the player's level-up choice, checked against the cards on the table; a refused one
        /// gets the cards again.</summary>
        void OnPick(ulong sender, FastBufferReader reader)
        {
            var bytes = ReadAll(reader);
            Metrics.Up(sender, bytes.Length);
            var s = Current;
            if (s == null || !s.connected || s.clientId != sender || LevelUps == null) return;
            PickMessage pick;
            try { if (!NetCodec.ReadPick(new NetReader(bytes), out pick)) return; }
            catch (Exception) { return; }
            if (pick.action == NetProtocol.PickAction.Hold)
            {
                // V5 M9b review: the client's hold tick for the cards just dealt (late or unasked ones change nothing)
                LevelUps.ApplyHold(pick.resumeTick);
                return;
            }
            var error = LevelUps.Apply(pick);
            if (error == null) return;
            Debug.Log("[V5 Net] level-up pick refused: " + error);
            if (LevelUps.TakeResendAfterRefusal()) SendOffer(LevelUps.Message()); // the cards again, once a set
        }

        void SendOffer(OfferMessage m)
        {
            var s = Current;
            if (s == null || !s.connected) return;
            _writer.Clear();
            NetCodec.WriteOffer(_writer, m);
            Send(NetProtocol.Offer, s.clientId, NetworkDelivery.ReliableSequenced);
        }

        void SendOfferClosed(OfferClosedMessage m)
        {
            var s = Current;
            if (s == null || !s.connected) return;
            _writer.Clear();
            NetCodec.WriteOfferClosed(_writer, m);
            Send(NetProtocol.OfferClosed, s.clientId, NetworkDelivery.ReliableSequenced);
        }

        /// <summary>The playtest relay let clients report their hull and hits; here such a message is dropped unread.</summary>
        void OnClientReport(ulong sender, FastBufferReader reader)
        {
            Metrics.Up(sender, reader.Length - reader.Position);
            IgnoredClientReports++;
        }

        static byte[] ReadAll(FastBufferReader reader)
        {
            var length = reader.Length - reader.Position;
            var bytes = new byte[Math.Max(0, length)];
            if (length > 0) reader.ReadBytesSafe(ref bytes, length);
            return bytes;
        }

        // ---- The tick -----------------------------------------------------------------------------------------------

        void Update()
        {
            if (Manager == null || !Manager.IsServer || Ship == null) return;
            if (_kickPending && Time.realtimeSinceStartup >= _kickAt)
            {
                _kickPending = false;
                if (Manager.ConnectedClientsIds.Contains(_kick)) Manager.DisconnectClient(_kick, "resumed elsewhere");
            }
            // Real time, not Time.deltaTime (capped at a third of a second a frame): the tick count follows the wall clock,
            // as the client's does.
            var now = Time.realtimeSinceStartupAsDouble;
            if (_lastRealtime >= 0) _accumulator += (float)(now - _lastRealtime);
            _lastRealtime = now;
            var ticks = 0;
            while (_accumulator >= NetProtocol.TickSeconds && ticks < 8)
            {
                _accumulator -= NetProtocol.TickSeconds;
                Tick();
                ticks++;
            }
            if (_accumulator > 1f) _accumulator = 0f; // a server stall over a second is dropped, not replayed
            _ticksThisFrame = ticks;
        }

        int _ticksThisFrame;

        void Tick()
        {
            ServerTick++;
            LevelUps?.Tick(); // an unanswered card set takes the first card
            var s = Current;
            var run = RunManager.Instance;
            if (run != null && run.PauseReason == PauseReason.POIChoice) SkipChestChoice(run);
            var playing = run != null && !run.IsRunEnded && Ship.Alive;
            var away = s != null && s.away && !s.expired;
            // M9b playtest fixes: an away player's voyage holds (no input applied, no coasting, no snapshots) until it is back
            // or the away limit ends it.
            if (away && Time.realtimeSinceStartup - s.awaySince > AwayMaxSeconds) { Abandon(s); away = false; }
            if (!away)
            {
                if (s != null && !s.expired && s.connected && playing) ApplyInputs(s);
                else if ((s == null || !s.connected) && playing && !run.IsPaused) Ship.Step(Vector2.zero, NetProtocol.TickSeconds); // detached: coasts (not through a pause)
            }

            if (s != null && !s.connected && !s.expired && !away && s.detachedAt >= 0f && Time.realtimeSinceStartup - s.detachedAt > ResumeGraceSeconds)
            {
                s.expired = true;
                Debug.Log("[V5 Net] session expired (no resume in time)");
            }
            if (!EndSent && run != null && (run.IsRunEnded || !Ship.Alive))
                SendEnd(Ship.Alive ? NetProtocol.EndReason.RunEnded : NetProtocol.EndReason.Sunk);

            // The world moved on this tick (the run is not paused for cards, held for an away player or for the first inputs).
            var worldMoved = run != null && !run.IsPaused && Time.timeScale > 0f && !away;
            if (worldMoved) WorldTick++;
            if (ServerTick % NetProtocol.SnapshotEveryTicks == 0 && s != null && s.connected && !away) SendSnapshot(s);
            Probe?.OnServerTick(this, worldMoved);
        }

        /// <summary>V5 M9b owner re-test fixes: a chest choice paused the run (the player sailed into a chest's range with the
        /// gold for it). Nobody can answer it on this server yet (M9c mirrors chests), so it is skipped at once, as the
        /// player's Skip (R) would: the run plays on and the prompt stays closed until the ship leaves the chest.</summary>
        void SkipChestChoice(RunManager run)
        {
            _stage?.SkipChest();
            if (run.PauseReason == PauseReason.POIChoice) run.SetPause(PauseReason.None); // not the interaction's own pause
            ChestChoicesSkipped++;
            if (ChestChoicesSkipped <= 10) Debug.Log($"[V5 Net] chest choice skipped (not mirrored to the client until M9c): {ChestChoicesSkipped} so far");
        }

        void ApplyInputs(Session s)
        {
            if (s.expectNext == 0) return; // no input since the welcome: the ship waits where the client's prediction starts
            if (!s.started)
            {
                // Jitter buffer: start once two inputs are in.
                if (!Has(s, s.expectNext) || !Has(s, s.expectNext + 1)) return;
                s.started = true;
                if (!_sessionEverStarted) { _sessionEverStarted = true; SessionStarted?.Invoke(); }
            }
            TicksWithSession++;
            // One step a tick, never more (a client numbering its ticks fast gains no speed): a backlog beyond the jitter
            // buffer is skipped unapplied, oldest first, so the delay stays short.
            var guard = 0;
            while (Buffered(s) > NetProtocol.MaxInputBacklog && guard++ < s.ring.Length)
            {
                s.ringTick[s.expectNext % (uint)s.ring.Length] = 0;
                s.expectNext++;
                Metrics.CatchUpSteps++;
            }
            Consume(s);
        }

        bool Has(Session s, uint tick) => s.ringTick[tick % (uint)s.ring.Length] == tick;

        int Buffered(Session s)
        {
            var n = 0;
            for (var t = s.expectNext; t < s.expectNext + s.ring.Length && Has(s, t); t++) n++;
            return n;
        }

        /// <summary>V5 M9b review: a late pick's resume tick has passed: the ticks [from, to) were held here but the client
        /// moved its ship on them, so they are stepped now on the inputs received for them (bounded by NetLevelUps).</summary>
        void CatchUpHeldTicks(uint from, uint to)
        {
            var s = Current;
            if (s == null || Ship == null) return;
            for (var t = from; t < to; t++)
            {
                var input = Has(s, t) ? s.ring[t % (uint)s.ring.Length] : s.last;
                Ship.Step(input.SteerThrottle, input.Boost, NetProtocol.TickSeconds, true);
            }
        }

        /// <summary>V5 M9b review: the ship's state before each tick applied while the server waits for the client's hold tick
        /// (a late hold rewinds to it).</summary>
        readonly (uint tick, V4.Foundation.Player.PlayerShipController.MotionState motion, V4.Foundation.PlayerSystems.ShipBoostController.NetState boost)[] _beforeTick =
            new (uint, V4.Foundation.Player.PlayerShipController.MotionState, V4.Foundation.PlayerSystems.ShipBoostController.NetState)[64];

        void RewindForHold(uint tick)
        {
            var slot = _beforeTick[tick % (uint)_beforeTick.Length];
            if (slot.tick != tick || Ship == null) { Debug.LogWarning($"[V5 Net] no ship state kept for tick {tick}: the hold starts later"); return; }
            Ship.Restore(slot.motion);
            Ship.RestoreBoost(slot.boost);
        }

        void Consume(Session s)
        {
            var tick = s.expectNext;
            if (LevelUps != null && LevelUps.AwaitingHold) _beforeTick[tick % (uint)_beforeTick.Length] = (tick, Ship.Capture(), Ship.CaptureBoost());
            NetInput input;
            // V5 M9b close: the margin at the moment the tick is applied (newest input received minus this tick; negative when
            // its input is not here), the least since the last snapshot goes out with it. The margin read when the snapshot
            // was built missed inputs that came under a tick late: they had arrived by then, so a client a tick short of
            // lead had most of its inputs guessed while its snapshots read 0 (up to 5 s after a hitch, 11 Oct).
            s.leastMargin = NetLateness.LeastMargin(s.leastMargin, s.newestReceived, tick);
            if (Has(s, tick)) { input = s.ring[tick % (uint)s.ring.Length]; s.guessRun = 0; }
            else
            {
                // Not here in time: the last input again; M9b playtest fixes: after GuessesBeforeNeutral in a row (a silent
                // client) a neutral one, so a frozen page's ship drifts to a stop instead of sailing on at full throttle.
                input = s.guessRun++ < NetProtocol.GuessesBeforeNeutral ? s.last : default;
                Metrics.GuessedInputs++;
            }
            // V5 M9b part 2b: the boost steps with the ship on the tick (held starts it when ready, as in a voyage), so the
            // client predicts it; a tick inside the level-up pause holds the ship (the client holds it too).
            if (LevelUps == null || !LevelUps.Holds(tick)) Ship.Step(input.SteerThrottle, input.Boost, NetProtocol.TickSeconds, true);
            StepsApplied++;
            s.last = input;
            s.lastAck = tick;
            s.expectNext = tick + 1;
        }

        // ---- Snapshots --------------------------------------------------------------------------------------------

        ShipSnapshot BuildShip(Session s)
        {
            var run = RunManager.Instance;
            byte flags = 0;
            if (Ship.Alive) flags |= NetProtocol.ShipAlive;
            if (run != null && run.IsRunEnded) flags |= NetProtocol.ShipRunEnded;
            if (Ship.IsBoosting) flags |= NetProtocol.ShipBoosting;
            if (run != null && run.IsPaused) flags |= NetProtocol.ShipPaused;
            if (LevelUps != null && LevelUps.AwaitingHold) flags |= NetProtocol.ShipHoldPending;
            var motion = Ship.Capture();
            if (motion.movementEnabled) flags |= NetProtocol.ShipMovementOn;
            return new ShipSnapshot
            {
                serverTick = ServerTick,
                ackTick = s != null && s.started ? s.lastAck : 0,
                motion = motion,
                boost = Ship.CaptureBoost(),
                hull = Ship.Hull,
                maxHull = Ship.MaxHull,
                runTime = run != null ? run.ElapsedSeconds : 0f,
                level = (ushort)(Game != null && Game.Build != null ? Game.Build.Progression.Level : 0),
                kills = (uint)(run != null ? run.KillCount : 0),
                gold = (uint)(run != null ? Mathf.Max(0, run.Gold) : 0),
                flags = flags,
                inputMargin = s != null && s.started ? NetLateness.SnapshotMargin(s.newestReceived, s.lastAck, s.leastMargin) : (sbyte)0,
            };
        }

        void SendSnapshot(Session s)
        {
            var bytes = 0;
            _writer.Clear();
            NetCodec.WriteShip(_writer, BuildShip(s));
            LastShipSnapshotBytes = _writer.Length;
            s.leastMargin = int.MaxValue; // the next snapshot reports the ticks applied after this one
            bytes += Send(NetProtocol.Snapshot, s.clientId, NetworkDelivery.Unreliable);
            var enemyCount = 0;
            bytes += SendEnemies(s, ref enemyCount);
            s.snapshots++;
            Metrics.AddSnapshot(bytes, enemyCount);
        }

        int SendEnemies(Session s, ref int sent)
        {
            var full = _snapshotIndex++ % NetProtocol.FarEveryNthSnapshot == 0;
            var origin = Ship.Position;
            _interest.Clear();
            if (_enemies != null && _enemies.Exists)
            {
                var near2 = NetProtocol.NearRadius * NetProtocol.NearRadius;
                var far2 = NetProtocol.FarRadius * NetProtocol.FarRadius;
                for (var i = 0; i < _enemies.Capacity; i++)
                {
                    if (!_enemies.TryGet(i, out var e)) continue;
                    var dx = e.position.x - origin.x;
                    var dz = e.position.z - origin.z;
                    var d2 = dx * dx + dz * dz;
                    if (d2 <= near2 || full && d2 <= far2) _interest.Add((d2, e));
                }
            }
            _interest.Sort((a, b) => a.d2.CompareTo(b.d2));
            var total = Mathf.Min(_interest.Count, NetProtocol.MaxEnemiesPerSnapshot);
            SendDefs(s, total);
            var batches = Mathf.Max(1, (total + NetProtocol.MaxEnemiesPerBatch - 1) / NetProtocol.MaxEnemiesPerBatch);
            var bytes = 0;
            for (var b = 0; b < batches; b++)
            {
                var from = b * NetProtocol.MaxEnemiesPerBatch;
                var count = Mathf.Min(NetProtocol.MaxEnemiesPerBatch, total - from);
                var header = new EnemyBatchHeader
                {
                    serverTick = _worldClock ? WorldTick : ServerTick, // protocol 5: the world's clock (stands still while the run is paused or held)
                    batchIndex = (byte)b,
                    batchCount = (byte)batches,
                    full = full,
                    truncated = _interest.Count > total,
                    originX = origin.x,
                    originZ = origin.z,
                    count = (ushort)count,
                };
                _writer.Clear();
                NetCodec.WriteBatchHeader(_writer, header);
                for (var i = 0; i < count; i++)
                {
                    var e = _interest[from + i].e;
                    byte flags = 0;
                    if (e.isElite) flags |= NetProtocol.EnemyElite;
                    if (e.isBoss) flags |= NetProtocol.EnemyBoss;
                    NetCodec.WriteEnemy(_writer, header, e.unitId, e.definitionIndex, e.position, e.velocity, e.maxHp > 0f ? e.hp / e.maxHp : 0f, flags);
                }
                bytes += Send(NetProtocol.Enemies, s.clientId, NetworkDelivery.Unreliable);
            }
            sent = total;
            return bytes;
        }

        /// <summary>Enemy kinds the client has not been told yet (reliable; their id strings once each).</summary>
        void SendDefs(Session s, int total)
        {
            var n = 0;
            for (var i = 0; i < total; i++)
            {
                var e = _interest[i].e;
                if (e.definitionIndex < 0 || e.definitionIndex > 255 || !s.sentDefs.Add(e.definitionIndex)) continue;
                _defs[n++] = ((byte)e.definitionIndex, e.enemyId);
                if (n == _defs.Length) break;
            }
            if (n == 0) return;
            _writer.Clear();
            NetCodec.WriteDefs(_writer, _defs, n);
            Send(NetProtocol.Defs, s.clientId, NetworkDelivery.ReliableSequenced);
        }

        public NetProtocol.EndReason LastEndReason { get; private set; }

        void SendEnd(NetProtocol.EndReason reason)
        {
            EndSent = true;
            LastEndReason = reason;
            var s = Current;
            if (s == null || !s.connected) return;
            var run = RunManager.Instance;
            _writer.Clear();
            NetCodec.WriteEnd(_writer, reason, run != null ? run.ElapsedSeconds : 0f, (uint)(run != null ? run.KillCount : 0));
            Send(NetProtocol.End, s.clientId, NetworkDelivery.ReliableSequenced);
        }

        int Send(string message, ulong clientId, NetworkDelivery delivery)
        {
            var length = _writer.Length;
            using (var w = new FastBufferWriter(length, Allocator.Temp))
            {
                w.WriteBytesSafe(_writer.Buffer, length);
                Manager.CustomMessagingManager.SendNamedMessage(message, clientId, w, delivery);
            }
            Metrics.Down(clientId, length);
            return length;
        }

        // ---- Tick time ----------------------------------------------------------------------------------------------

        /// <summary>
        /// The server's tick time: every Update between execution order -101 and 11 (the run's composition root at -100,
        /// the run manager and the gameplay components at 0, this server at 5, the frame pipeline at 10), summed since
        /// the previous net tick and shared by the ticks of this frame. The in-process client (order 50) and the test
        /// code are outside it.
        /// </summary>
        internal void BeginWork() { _work.Restart(); }

        internal void EndWork()
        {
            // The run's frame steps (order 10) may have opened a chest choice: skipped before anything else steps a ship this
            // frame (the in-process test client, at order 50, shares the run's pause).
            var run = RunManager.Instance;
            if (Ship != null && run != null && run.PauseReason == PauseReason.POIChoice) SkipChestChoice(run);
            _work.Stop();
            _pendingWorkMs += _work.Elapsed.TotalMilliseconds;
            if (_ticksThisFrame <= 0) return;
            var perTick = (float)(_pendingWorkMs / _ticksThisFrame);
            for (var i = 0; i < _ticksThisFrame; i++) Metrics.AddTick(perTick); // V5 M9b close: a rolling window, not every tick
            _pendingWorkMs = 0;
            _ticksThisFrame = 0;
        }
    }
}
