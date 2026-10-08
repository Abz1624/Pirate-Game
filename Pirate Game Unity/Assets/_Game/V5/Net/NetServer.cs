using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using RoguePirates.V4;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Ports;
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
        public int Seed { get; private set; }

        /// <summary>Client reports (hull, hits) received and dropped unread; input bytes beyond the inputs, dropped.</summary>
        public int IgnoredClientReports { get; private set; }
        public int IgnoredInputBytes { get; private set; }
        public int RejectedJoins { get; private set; }
        public string LastRejectReason { get; private set; }
        public bool EndSent { get; private set; }

        public V4Game Game { get; private set; }
        public ShipMotionPort Ship { get; private set; }
        IEnemyWorldView _enemies;

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
            public NetInput last;
            public readonly NetInput[] ring = new NetInput[64];
            public readonly uint[] ringTick = new uint[64];
            public readonly HashSet<int> sentDefs = new HashSet<int>();
            public int snapshots;
            public bool expired;
        }

        public Session Current { get; private set; }

        /// <summary>V5 M9b part 2a: a ticket join went through (the player id); the match server's host starts the held run.</summary>
        public event Action<string> Joined;
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
            Seed = RunManager.Instance != null ? RunManager.Instance.RunSeed : 0;
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
            Metrics.Reset();
            return true;
        }

        public void Stop()
        {
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
                if (s.connected && s.clientId != clientId) { _kick = s.clientId; _kickPending = true; s.droppedAt = Time.realtimeSinceStartup; }
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
            Send(NetProtocol.Welcome, clientId, NetworkDelivery.ReliableSequenced);
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

        /// <summary>Tests: the socket drops (the session stays for its resume token).</summary>
        public void DropConnectionForTest()
        {
            if (Current != null && Current.connected) Manager.DisconnectClient(Current.clientId, "connection dropped");
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
            if (_kickPending)
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
            var s = Current;
            var run = RunManager.Instance;
            var playing = run != null && !run.IsRunEnded && Ship.Alive;
            if (s != null && !s.expired && s.connected && playing) ApplyInputs(s);
            else if (s != null && !s.connected && playing) Ship.Step(Vector2.zero, NetProtocol.TickSeconds); // detached: coasts
            else if (s == null && playing) Ship.Step(Vector2.zero, NetProtocol.TickSeconds);

            if (s != null && !s.connected && !s.expired && s.detachedAt >= 0f && Time.realtimeSinceStartup - s.detachedAt > ResumeGraceSeconds)
            {
                s.expired = true;
                Debug.Log("[V5 Net] session expired (no resume in time)");
            }
            if (!EndSent && run != null && (run.IsRunEnded || !Ship.Alive))
                SendEnd(Ship.Alive ? NetProtocol.EndReason.RunEnded : NetProtocol.EndReason.Sunk);

            if (ServerTick % NetProtocol.SnapshotEveryTicks == 0 && s != null && s.connected) SendSnapshot(s);
        }

        void ApplyInputs(Session s)
        {
            if (s.expectNext == 0) return; // no input since the welcome: the ship waits where the client's prediction starts
            if (!s.started)
            {
                // Jitter buffer: start once two inputs are in.
                if (!Has(s, s.expectNext) || !Has(s, s.expectNext + 1)) return;
                s.started = true;
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

        void Consume(Session s)
        {
            var tick = s.expectNext;
            NetInput input;
            if (Has(s, tick)) input = s.ring[tick % (uint)s.ring.Length];
            else { input = s.last; Metrics.GuessedInputs++; } // not here in time: the last input again
            if (input.Boost && !s.last.Boost) Ship.RequestBoost();
            Ship.Step(input.SteerThrottle, NetProtocol.TickSeconds);
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
            var motion = Ship.Capture();
            if (motion.movementEnabled) flags |= NetProtocol.ShipMovementOn;
            return new ShipSnapshot
            {
                serverTick = ServerTick,
                ackTick = s != null && s.started ? s.lastAck : 0,
                motion = motion,
                hull = Ship.Hull,
                maxHull = Ship.MaxHull,
                runTime = run != null ? run.ElapsedSeconds : 0f,
                level = (ushort)(Game != null && Game.Build != null ? Game.Build.Progression.Level : 0),
                kills = (uint)(run != null ? run.KillCount : 0),
                gold = (uint)(run != null ? Mathf.Max(0, run.Gold) : 0),
                flags = flags,
            };
        }

        void SendSnapshot(Session s)
        {
            var bytes = 0;
            _writer.Clear();
            NetCodec.WriteShip(_writer, BuildShip(s));
            bytes += Send(NetProtocol.Snapshot, s.clientId, NetworkDelivery.Unreliable);
            var enemyCount = 0;
            bytes += SendEnemies(s, ref enemyCount);
            s.snapshots++;
            Metrics.SnapshotBytes.Add(bytes);
            Metrics.SnapshotEnemies.Add(enemyCount);
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
                    serverTick = ServerTick,
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
            _work.Stop();
            _pendingWorkMs += _work.Elapsed.TotalMilliseconds;
            if (_ticksThisFrame <= 0) return;
            var perTick = (float)(_pendingWorkMs / _ticksThisFrame);
            for (var i = 0; i < _ticksThisFrame; i++) Metrics.TickMs.Add(perTick);
            _pendingWorkMs = 0;
            _ticksThisFrame = 0;
        }
    }
}
