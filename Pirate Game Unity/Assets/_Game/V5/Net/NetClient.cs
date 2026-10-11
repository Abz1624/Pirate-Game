using System;
using System.Text;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Ports;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): a player's connection to the match server. It sends one input per 30 Hz tick (the newest
    /// <see cref="NetProtocol.InputRedundancy"/> in each packet) and moves its own copy of the ship on them at once
    /// (prediction, on the ship's one handling code through <see cref="ShipMotionPort"/>). Each snapshot names the
    /// newest input the server applied and its ship's full motion state; the client compares its prediction for that
    /// tick (the prediction error), rewinds to the server's state and replays its later inputs (the correction is how far
    /// that moved the ship now; the view eases it out). The hull, the run's counters and the enemies are the server's,
    /// shown as received. A dropped socket reconnects with the session's resume token for up to
    /// <see cref="ReconnectWindowSeconds"/>.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class NetClient : MonoBehaviour
    {
        public NetworkManager Manager { get; private set; }
        public UnityTransport Transport { get; private set; }
        public readonly NetMetrics Metrics = new NetMetrics("client");
        public readonly NetEnemyMirror Enemies = new NetEnemyMirror();
        public NetEndpoint Endpoint { get; private set; }

        /// <summary>The player's steer and throttle (tests script it; the keyboard when null).</summary>
        public IShipSteerInput Input { get; set; }
        public Func<bool> Boost { get; set; }
        /// <summary>Makes the prediction ship at the first welcome (it may return null: the ship comes later through
        /// <see cref="AttachShip"/>, once the stage built from the welcome's seed is loaded).</summary>
        public Func<ShipMotionPort> ShipFactory { get; set; }
        public ShipMotionPort Predicted { get; private set; }
        /// <summary>V5 M9b part 2b: the first welcome (its seed builds the client's stage).</summary>
        public WelcomeMessage FirstWelcome { get; private set; }
        public event Action<WelcomeMessage> WelcomeReceived;

        /// <summary>V5 M9b part 2b: the level-up cards up now (null when none), and when they change.</summary>
        public OfferMessage PendingOffer { get; private set; }
        public float PendingOfferSince { get; private set; }
        public event Action<OfferMessage> OfferReceived;
        public event Action<OfferClosedMessage> OfferResolved;
        public int OffersReceived { get; private set; }
        public int PicksSent { get; private set; }
        public OfferClosedMessage LastResolution { get; private set; }
        /// <summary>The level-up pause on the client's tick line: from <see cref="_pauseFrom"/> (0: none) until
        /// <see cref="_resumeAt"/> (0: still paused), the ship holds, as the server's does.</summary>
        uint _pauseFrom, _resumeAt;
        bool _resumeConfirmed;

        public bool Welcomed { get; private set; }
        public bool Ended { get; private set; }
        public NetProtocol.EndReason EndReason { get; private set; }
        public ShipSnapshot Last { get; private set; }
        public int Snapshots { get; private set; }
        public float Hull => Last.hull;
        public float MaxHull => Last.maxHull;
        public uint Tick { get; private set; }
        public uint LastAck { get; private set; }
        public int Welcomes { get; private set; }
        public string LastDisconnectReason { get; private set; }
        public bool Connected => Manager != null && Manager.IsConnectedClient;
        public bool AutoReconnect = true;
        public float ReconnectWindowSeconds = 10f;
        /// <summary>The view's offset that eases a correction out (the ship drawn at <see cref="RenderPosition"/>).</summary>
        public Vector3 VisualOffset { get; private set; }
        /// <summary>
        /// V5 M9b playtest fixes (A): where the ship is drawn this frame. The ship is stepped at the 30 Hz net tick; drawn
        /// at its stepped pose it moved on every other frame of a 60 Hz display (the owner's "stuttery"). It is now drawn
        /// between its last two ticks by the time since the last one (a tick behind, as a physics body is interpolated), plus
        /// <see cref="VisualOffset"/>, which eases a correction out over a few frames; the camera follows the drawn ship.
        /// </summary>
        public Vector3 RenderPosition { get; private set; }
        public Quaternion RenderRotation { get; private set; } = Quaternion.identity;
        /// <summary>Tests: false draws the ship at its stepped pose (the drawing before the fix), to measure both.</summary>
        public bool InterpolateDrawnShip { get; set; } = true;
        /// <summary>A correction this large is not eased out: the ship is drawn where it is at once.</summary>
        public const float SnapCorrectionMetres = 5f;

        const int History = 256;
        readonly NetInput[] _inputs = new NetInput[History];
        readonly Vector3[] _predicted = new Vector3[History];
        readonly NetInput[] _send = new NetInput[NetProtocol.InputRedundancy];
        readonly NetWriter _writer = new NetWriter(64);
        readonly ShipKeyboardInput _keyboard = new ShipKeyboardInput();
        uint _historyStart = 1;
        float _accumulator;
        string _ticket;
        byte[] _resume;
        float _droppedAt = -1f, _nextAttemptAt;
        bool _stopped;
        byte[] _appendToNextInput;

        public static NetClient Create(NetEndpoint endpoint, string ticket, Func<ShipMotionPort> shipFactory, string name = "V5NetClient")
        {
            var go = new GameObject(name);
            DontDestroyOnLoad(go);
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = NetProtocol.TickRate;
            transport.ConnectTimeoutMS = 1000;
            transport.DisconnectTimeoutMS = NetProtocol.DisconnectTimeoutMs;
            transport.MaxConnectAttempts = 5;
            var client = go.AddComponent<NetClient>();
            go.AddComponent<NetShipDraw>();
            client.Manager = manager;
            client.Transport = transport;
            client.Endpoint = endpoint;
            client._ticket = ticket;
            client.ShipFactory = shipFactory;
            client.Metrics.Reset();
            endpoint.ApplyClient(transport);
            manager.OnClientDisconnectCallback += client.OnDisconnected;
            return client;
        }

        /// <summary>Connects with the join ticket (first time) or the resume token (after a drop).</summary>
        public bool Connect()
        {
            _stopped = false;
            byte[] payload;
            if (_resume != null)
            {
                payload = new byte[1 + _resume.Length];
                payload[0] = NetProtocol.ConnectResume;
                Array.Copy(_resume, 0, payload, 1, _resume.Length);
            }
            else
            {
                var ticket = Encoding.UTF8.GetBytes(_ticket ?? string.Empty);
                payload = new byte[1 + ticket.Length];
                payload[0] = NetProtocol.ConnectTicket;
                Array.Copy(ticket, 0, payload, 1, ticket.Length);
            }
            Manager.NetworkConfig.ConnectionData = payload;
            Endpoint.ApplyClient(Transport);
            if (!Manager.StartClient()) return false;
            Handle(NetProtocol.Welcome, OnWelcome);
            Handle(NetProtocol.Snapshot, OnSnapshot);
            Handle(NetProtocol.Enemies, OnEnemies);
            Handle(NetProtocol.Defs, OnDefs);
            Handle(NetProtocol.End, OnEnd);
            Handle(NetProtocol.Offer, OnOffer);
            Handle(NetProtocol.OfferClosed, OnOfferClosed);
            return true;
        }

        /// <summary>Leaves for good (no reconnect).</summary>
        public void Stop()
        {
            _stopped = true;
            if (Manager != null && Manager.IsListening) Manager.Shutdown();
        }

        void OnDestroy()
        {
            if (Manager != null) Manager.OnClientDisconnectCallback -= OnDisconnected;
            UndrawShip();
            Predicted?.Destroy();
        }

        /// <summary>Tests: this side's socket drops (the client reconnects with its resume token).</summary>
        public void DropForTest()
        {
            if (Manager == null || !Manager.IsListening) return;
            Manager.Shutdown();
            MarkDropped("dropped (test)");
        }

        void OnDisconnected(ulong clientId)
        {
            if (Manager == null || clientId != Manager.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            MarkDropped(Manager.DisconnectReason);
        }

        void MarkDropped(string reason)
        {
            LastDisconnectReason = reason;
            Welcomed = false; // no ticks until the next welcome says where to start
            if (_droppedAt < 0f) _droppedAt = Time.realtimeSinceStartup;
            _nextAttemptAt = Time.realtimeSinceStartup + 0.25f;
        }

        /// <summary>
        /// V5 M9b part 2a: the voyage is over for this client and the game goes back to the lobby: the server ended it
        /// (<see cref="Ended"/>, e.g. ServerClosing on a redeploy), or the socket dropped and no resume came within
        /// <see cref="ReconnectWindowSeconds"/> (or there was never a session to resume).
        /// </summary>
        public bool SessionOver =>
            Ended || _stopped ||
            !Connected && _droppedAt >= 0f && !Away && (_resume == null || !AutoReconnect || Time.realtimeSinceStartup - _droppedAt > ReconnectWindowSeconds);

        // M9b playtest fixes (E): an away page does not reconnect (the server keeps an away session); back, the reconnect
        // window starts then.
        bool ShouldReconnect =>
            AutoReconnect && !_stopped && !Ended && !Away && _resume != null && _droppedAt >= 0f &&
            Time.realtimeSinceStartup - _droppedAt <= ReconnectWindowSeconds;

        // ---- Server messages ----------------------------------------------------------------------------------------

        /// <summary>A server message's handler on its bytes. M9b playtest fixes: while a test throttles this client (a
        /// hidden tab's page runs about once a second) the messages wait in arrival order for its next frame.</summary>
        void Handle(string message, Action<NetReader> handler)
        {
            Manager.CustomMessagingManager.RegisterNamedMessageHandler(message, (sender, reader) =>
            {
                var length = reader.Length - reader.Position;
                var bytes = new byte[Math.Max(0, length)];
                if (length > 0) reader.ReadBytesSafe(ref bytes, length);
                Metrics.Down(NetworkManager.ServerClientId, length);
                if (_throttleUntil > 0) { _queued.Add((handler, bytes)); return; }
                handler(new NetReader(bytes));
            });
        }

        readonly System.Collections.Generic.List<(Action<NetReader> handler, byte[] bytes)> _queued = new System.Collections.Generic.List<(Action<NetReader>, byte[])>();
        double _throttleUntil, _nextThrottledFrame;

        /// <summary>Tests: for <paramref name="seconds"/> this client runs as a page in a hidden browser tab does (Chrome
        /// runs it about once a second): one frame a second, the server's messages handled in that frame, its socket open
        /// (the transport keeps it alive). Nothing tells the server.</summary>
        public void ThrottleForTest(float seconds)
        {
            _throttleUntil = Time.realtimeSinceStartupAsDouble + seconds;
            _nextThrottledFrame = Time.realtimeSinceStartupAsDouble + 1.0;
        }

        public bool Throttled => _throttleUntil > 0;

        void FlushQueued()
        {
            var queued = _queued.ToArray();
            _queued.Clear();
            foreach (var (handler, bytes) in queued) handler(new NetReader(bytes));
        }

        void OnWelcome(NetReader reader)
        {
            if (!NetCodec.ReadWelcome(reader, out var m)) return;
            _resume = m.resumeToken;
            var first = Welcomes == 0;
            if (first) FirstWelcome = m;
            if (Predicted == null) Predicted = ShipFactory?.Invoke();
            if (_droppedAt >= 0f) Metrics.ResumeSeconds.Add(Time.realtimeSinceStartup - _droppedAt);
            _droppedAt = -1f;
            UndrawShip();
            Predicted?.Restore(m.ship.motion);
            Predicted?.RestoreBoost(m.ship.boost);
            SnapDrawnShip();
            // V5 M9b review: the level-up state before the drop is not trusted across a resume (the server may have resolved
            // the cards meanwhile, e.g. its 30 s timeout): the cards go and the ship holds no more; if cards are still up,
            // the server sends them again right after this welcome and the hold comes back with them.
            ClearOffer();
            Last = m.ship;
            _hasPendingSnapshot = false; // a snapshot from before the welcome is not reconciled after it
            LastAck = 0;
            _historyStart = Tick + 1; // inputs from here on are the ones the server will apply
            _accumulator = 0f;
            _lastRealtime = -1;
            _resyncNext = false;
            _lateSnapshots = 0;
            _lateMask = 0;
            _behindGuard = Tick;
            // M9b playtest fixes (C): the enemies stay (a welcome after a resume or an away no longer clears them, which made
            // every enemy vanish and spawn again with its spawn effect); the next full snapshot drops the ones gone meanwhile.
            Welcomed = true;
            Welcomes++;
            _awaitingWelcome = false;
            // M9b playtest fixes (E): a (re)connection counts as present on the server; a page still hidden says so again.
            if (Away) SendPresence(true);
            WelcomeReceived?.Invoke(m);
        }

        /// <summary>
        /// V5 M9b part 2b: the prediction ship arrives after the welcome (the client's stage was built from its seed). It
        /// starts from the newest snapshot; the server's ship has waited for the first input since the welcome, so the
        /// prediction starts in step.
        /// </summary>
        public void AttachShip(ShipMotionPort ship)
        {
            if (ship == null) throw new ArgumentNullException(nameof(ship));
            Predicted = ship;
            ship.Restore(Last.motion);
            ship.RestoreBoost(Last.boost);
            SnapDrawnShip();
            LastAck = 0;
            _historyStart = Tick + 1;
            _accumulator = 0f;
            _lastRealtime = -1;
        }

        // ---- Level-up cards (V5 M9b part 2b) -----------------------------------------------------------------------

        void OnOffer(NetReader reader)
        {
            OfferMessage m;
            try { if (!NetCodec.ReadOffer(reader, out m)) return; }
            catch (Exception) { return; }
            var again = PendingOffer != null && PendingOffer.seq == m.seq;
            PendingOffer = m;
            if (!again) PendingOfferSince = Time.realtimeSinceStartup;
            OffersReceived++;
            _resumeAt = 0; // held until the server says where the ship moves again
            _resumeConfirmed = false;
            if (m.pauseFrom != 0) _pauseFrom = m.pauseFrom; // the server's hold tick (a chained set, a clamp, a resend)
            else
            {
                // V5 M9b review (D9b.1): the ship holds from the tick after the one this client has reached (its inputs up
                // to there are sent); the server applies them, then holds, so no predicted tick is taken back.
                _pauseFrom = Tick + 1;
                _writer.Clear();
                NetCodec.WritePick(_writer, new PickMessage { seq = m.seq, action = NetProtocol.PickAction.Hold, resumeTick = _pauseFrom });
                Send(NetProtocol.Pick, NetworkDelivery.ReliableSequenced);
                HoldsSent++;
            }
            OfferReceived?.Invoke(m);
        }

        /// <summary>V5 M9b review: snapshots not used for a correction because the server had not had the hold or the pick yet.</summary>
        public int SkippedSnapshots { get; private set; }
        /// <summary>V5 M9b review: hold ticks this client named (one per new card set).</summary>
        public int HoldsSent { get; private set; }

        /// <summary>No cards and no hold (a resume: the server sends the cards again if they are still up).</summary>
        void ClearOffer()
        {
            var had = PendingOffer;
            PendingOffer = null;
            _pauseFrom = _resumeAt = 0;
            _resumeConfirmed = false;
            if (had != null) OfferResolved?.Invoke(new OfferClosedMessage { seq = had.seq, how = NetProtocol.OfferResolution.Cancelled });
        }

        void OnOfferClosed(NetReader reader)
        {
            OfferClosedMessage m;
            try { if (!NetCodec.ReadOfferClosed(reader, out m)) return; }
            catch (Exception) { return; }
            LastResolution = m;
            if (PendingOffer != null && PendingOffer.seq != m.seq) return; // a newer set is already up (a chained level-up)
            PendingOffer = null;
            if (m.resumeTick != 0) { _resumeAt = m.resumeTick; _resumeConfirmed = true; }
            // M9b playtest fixes (D): a set the server resolved without this client's answer (its 30 s timeout while the tab
            // was hidden, a cancel) means the server's ship and tick line moved on without this client: the next snapshot
            // is taken as it is (the ship resynced, the tick moved ahead of the server's), so the player sails at once.
            if (_pickSentSeq != m.seq) _resyncNext = true;
            OfferResolved?.Invoke(m);
        }

        /// <summary>The player's choice on the cards up: a card (<paramref name="index"/>), a skip or a refresh. The ship
        /// moves again from the next tick (the server may name a later one).</summary>
        public bool SendPick(NetProtocol.PickAction action, int index = 0)
        {
            var offer = PendingOffer;
            if (offer == null || !Connected) return false;
            var resume = Tick + 1;
            _writer.Clear();
            NetCodec.WritePick(_writer, new PickMessage { seq = offer.seq, action = action, index = (byte)Mathf.Clamp(index, 0, 255), resumeTick = resume });
            Send(NetProtocol.Pick, NetworkDelivery.ReliableSequenced);
            PicksSent++;
            _pickSentSeq = offer.seq;
            // A refresh, or a pick with more level-ups waiting (the next cards follow at once), keeps the ship held.
            if (action != NetProtocol.PickAction.Refresh && offer.pendingLevelUps <= 1) _resumeAt = resume;
            return true;
        }

        /// <summary>Tests: a pick for any card set and index (the server checks it).</summary>
        public void SendRawPickForTest(ushort seq, NetProtocol.PickAction action, int index)
        {
            _writer.Clear();
            NetCodec.WritePick(_writer, new PickMessage { seq = seq, action = action, index = (byte)Mathf.Clamp(index, 0, 255), resumeTick = Tick + 1 });
            Send(NetProtocol.Pick, NetworkDelivery.ReliableSequenced);
        }

        /// <summary>The ship holds on this tick (the level-up pause, as the server holds it).</summary>
        public bool Holds(uint tick)
        {
            if (_pauseFrom == 0 || tick < _pauseFrom) return false;
            return _resumeAt == 0 || tick < _resumeAt;
        }

        void Step(uint tick, in NetInput input, bool replay)
        {
            if (Holds(tick)) return;
            if (!Last.Alive || Last.RunEnded) return; // the server's ship sank or the voyage ended: it moves no more
            Predicted.Step(input.SteerThrottle, input.Boost, NetProtocol.TickSeconds, replay);
        }

        void OnSnapshot(NetReader reader)
        {
            if (!NetCodec.ReadShip(reader, out var s)) return;
            // V5 M9b review: a snapshot that overtakes the resume's welcome (another channel) belongs to the new session but
            // this client has not restarted its tick line yet: replaying against it would run the whole input history.
            if (!Welcomed || _awaitingWelcome) return;
            if (s.serverTick <= Last.serverTick && Snapshots > 0) return; // late
            Snapshots++;
            Last = s;
            // V5 M9b owner re-test fixes (10 Oct): the prediction is reconciled once a frame, on the newest snapshot
            // (ApplyPendingSnapshot, early in Update). A page that ran no frame for a while (a tab switch: Chrome runs a hidden
            // page's first frame up to a second later; a hitch such as the cards' first show) handles a second of snapshots
            // in one frame; each one reconciled in turn found the server's ack past the client's standing tick and resynced
            // again (the owner's log: ack 1745, 1755, 1765, 1775, each 10 ticks on, the tick never moving in between).
            if (_hasPendingSnapshot) SnapshotsCoalesced++;
            _pendingSnapshot = s;
            _hasPendingSnapshot = true;
        }

        ShipSnapshot _pendingSnapshot;
        bool _hasPendingSnapshot;
        /// <summary>Snapshots that arrived in the same frame as a newer one (only the newest is reconciled).</summary>
        public int SnapshotsCoalesced { get; private set; }

        void ApplyPendingSnapshot()
        {
            if (!_hasPendingSnapshot) return;
            _hasPendingSnapshot = false;
            if (!Welcomed || _awaitingWelcome || Away) return; // away: the welcome when back resyncs the ship
            UndrawShip();
            Reconcile(_pendingSnapshot);
        }

        /// <summary>
        /// V5 M9b owner re-test fixes (10 Oct): the enemies' clock stands still while this client's ship holds for the
        /// level-up cards (and while the page is away or waits for a welcome), as the whole game stops for the cards in a
        /// normal voyage; with the pick it runs on from the same place (the server's world tick stood still meanwhile).
        /// </summary>
        public bool WorldHeld => Away || _awaitingWelcome || !Welcomed || _pauseFrom != 0 && Holds(Tick + 1);

        void OnEnemies(NetReader r)
        {
            // A batch handled in a slow frame (a hitch, a hidden page) is late because this page was, not the link: its
            // lateness does not widen the enemies' render delay (the owner's 350 ms after a return).
            var steady = Time.unscaledDeltaTime <= SlowFrameSeconds && NetFrameClock.Now - _slowFrameAt > SlowFrameQuietSeconds && !Throttled;
            try { Enemies.Apply(r, Predicted != null ? Predicted.Position.y : 0f, Time.realtimeSinceStartupAsDouble, steady); }
            catch (Exception) { /* malformed batch: dropped */ }
        }

        void OnDefs(NetReader reader) => NetCodec.ReadDefs(reader, Enemies.SetDefinition);

        void OnEnd(NetReader reader)
        {
            if (!NetCodec.ReadEnd(reader, out var reason, out _, out _)) return;
            Ended = true;
            EndReason = reason;
        }

        // ---- Prediction -----------------------------------------------------------------------------------------------

        void Update()
        {
            if (_throttleUntil > 0)
            {
                var t = Time.realtimeSinceStartupAsDouble;
                _slowFrameAt = NetFrameClock.Now; // the emulated page's frames are a second long
                if (t >= _throttleUntil) { _throttleUntil = 0; FlushQueued(); }
                else if (t < _nextThrottledFrame) return; // the hidden page's frame has not come yet
                else { _nextThrottledFrame = t + 1.0; FlushQueued(); }
            }
            if (ShouldReconnect && Manager != null && !Manager.IsListening && !Manager.ShutdownInProgress && Time.realtimeSinceStartup >= _nextAttemptAt)
            {
                _nextAttemptAt = Time.realtimeSinceStartup + 0.5f;
                Connect();
            }
            if (_delayed.Count > 0) FlushDelayedForTest();
            if (Time.unscaledDeltaTime > SlowFrameSeconds) _slowFrameAt = NetFrameClock.Now;
            UpdatePresence();
            ApplyPendingSnapshot();
            UndrawShip();
            if (VisualOffset != Vector3.zero)
            {
                VisualOffset *= Mathf.Exp(-12f * Time.unscaledDeltaTime);
                if (VisualOffset.sqrMagnitude < 1e-6f) VisualOffset = Vector3.zero;
            }
            if (_awaitingWelcome && Time.realtimeSinceStartup - _backSentAt > 3f) _awaitingWelcome = false; // no welcome came: tick on
            if (!Welcomed || Ended || Predicted == null || !Connected || Away || _awaitingWelcome)
            {
                _lastRealtime = -1;
                _accumulator = 0f;
                HoldDrawnShip();
                return;
            }
            // Real time (Time.deltaTime is capped at a third of a second): after a hitch the client catches its ticks up,
            // so its tick stays in step with the server's wall clock. A stall over 2 s is dropped; the next snapshot
            // then names a tick ahead of this one and the client jumps to it (Reconcile).
            var now = NetFrameClock.Now; // M9b playtest fixes: the frame's start, so the drawn ship moves evenly
            if (_stallUntil > 0 && now < _stallUntil) { _slowFrameAt = now; return; } // tests: this side alone freezes (one long frame, as a hitch is)
            _stallUntil = 0;
            if (_lastRealtime >= 0)
            {
                // M9b playtest fixes: a lead trimmed (below) is shed by running the tick clock 5% slow for a moment, so the
                // drawn ship slows imperceptibly instead of standing still for a tick.
                var elapsed = (float)(now - _lastRealtime);
                var shed = Mathf.Min(_trimOwed, elapsed * 0.05f);
                _trimOwed -= shed;
                _accumulator += elapsed - shed;
            }
            _lastRealtime = now;
            if (_accumulator > 2f) _accumulator = 0f;
            var steps = 0;
            while (_accumulator >= NetProtocol.TickSeconds && steps < 8)
            {
                _accumulator -= NetProtocol.TickSeconds;
                for (var i = 0; i < Mathf.Max(1, TickScaleForTest); i++) StepTick();
                steps++;
            }
        }

        double _lastRealtime = -1, _stallUntil;

        /// <summary>Tests: this client alone stops for <paramref name="seconds"/> (a GC pause, a background tab).</summary>
        public void StallForTest(float seconds)
        {
            _stallUntil = NetFrameClock.Now + seconds;
            _lastRealtime = NetFrameClock.Now;
        }

        /// <summary>Tests: a forged client clock (this many ticks per real tick, numbered ahead).</summary>
        public int TickScaleForTest { get; set; } = 1;
        /// <summary>Corrections over 1 m (the first 20 are logged with their context).</summary>
        public int LargeCorrections { get; private set; }
        /// <summary>Times the client was so far behind that it jumped to the server's tick.</summary>
        public int Resyncs { get; private set; }
        int _lead = 6;

        /// <summary>The session's current resume token (a second connection may present it, as after a silent drop).</summary>
        public byte[] ResumeToken => _resume;
        public void UseResumeToken(byte[] token) => _resume = token;

        void StepTick()
        {
            Tick++;
            var raw = Input != null ? Input.ReadSteerThrottle() : _keyboard.ReadSteerThrottle();
            var input = NetInput.From(raw, Boost != null && Boost());
            var slot = (int)(Tick % History);
            _inputs[slot] = input;
            _drawFromPos = _simPos;
            _drawFromRot = _simRot;
            Step(Tick, input, false); // on the quantized input, as the server will
            _predicted[slot] = Predicted.Position;
            _simPos = Predicted.Position;
            _simRot = Predicted.Rotation;
            SendInputs();
        }

        void SendInputs()
        {
            var count = (int)Math.Min((uint)NetProtocol.InputRedundancy, Tick - _historyStart + 1);
            for (var i = 0; i < count; i++) _send[i] = _inputs[(Tick - (uint)i) % History];
            _writer.Clear();
            NetCodec.WriteInputs(_writer, Tick, _send, count);
            if (_appendToNextInput != null)
            {
                _writer.Bytes(_appendToNextInput, _appendToNextInput.Length);
                _appendToNextInput = null;
            }
            Send(NetProtocol.Input, NetworkDelivery.Unreliable);
        }

        void Reconcile(in ShipSnapshot s)
        {
            if (Predicted == null) return;
            var ack = s.ackTick;
            if (ack != 0 && ack < LastAck) return;
            // M9b playtest fixes (D): the server says how far ahead of the tick it applied its newest received input was. A
            // negative margin means this client's inputs arrive after their tick (the server guesses them): its tick line
            // is behind the server's. Before, only an ack ahead of the client's own tick showed that, and a lead measured
            // while the server was guessing stayed too short for a 270 ms round trip: every input arrived late, for good.
            // Not while this page itself runs slowly (a hidden tab's one frame a second, a long hitch, and a second after
            // it): its inputs are late because it is, and the snapshots in flight still show it; the ack overtaking its
            // tick catches that up with the lead measured on time.
            var steady = Time.unscaledDeltaTime <= SlowFrameSeconds && NetFrameClock.Now - _slowFrameAt > SlowFrameQuietSeconds;
            var late = ack != 0 && s.inputMargin < 0 && ack > _behindGuard && steady;
            // V5 M9b close: late snapshots counted over the last LateWindowSnapshots (were counted only in a row). A lead a
            // tick short of the link leaves some ticks' inputs late and others not: snapshots alternating late and on time,
            // never three in a row, and the server guessed every few ticks for up to 5 s (a hitch's resync, 11 Oct).
            _lateMask = ((_lateMask << 1) | (late ? 1 : 0)) & ((1 << LateWindowSnapshots) - 1);
            _lateSnapshots = 0;
            for (var m = _lateMask; m != 0; m &= m - 1) _lateSnapshots++;
            var behind = late && (s.inputMargin <= -LateTicksToResync || _lateSnapshots >= LateSnapshotsToResync);
            var resync = ack != 0 && _resyncNext && !s.HoldPending;
            if (ack > Tick || behind || resync)
            {
                // Take the server's state and move the tick ahead of the server's by enough that the inputs arrive in time
                // (the lead measured on time, or the lateness plus a margin), predicting those ticks on the last input.
                var deficit = s.inputMargin < 0 ? -s.inputMargin : 0;
                var lead = behind ? Mathf.Clamp(Mathf.Max(_lead, (int)((long)Tick - ack) + deficit + ResyncSlackTicks), 2, 60) : _lead;
                var held = _inputs[Tick % History];
                var resyncFrom = _simPos;
                var resyncFromTick = Tick;
                Predicted.Restore(s.motion);
                Predicted.RestoreBoost(s.boost);
                LastAck = ack;
                Tick = ack + (uint)lead;
                _historyStart = ack + 1;
                for (var t = ack + 1; t <= Tick; t++)
                {
                    _inputs[t % History] = held;
                    Step(t, held, true);
                    _predicted[t % History] = Predicted.Position;
                }
                if (_resumeConfirmed && ack >= _resumeAt) { _pauseFrom = _resumeAt = 0; _resumeConfirmed = false; }
                Resyncs++;
                if (behind) LateResyncs++;
                if (Resyncs <= 20)
                    Debug.Log($"[V5 Net] resync ({(behind ? "inputs late" : resync ? "cards resolved by the server" : "behind the server's ack")}): ack {ack}, margin {s.inputMargin}, " +
                              $"tick {resyncFromTick} -> {Tick} (lead {lead})");
                _resyncNext = false;
                _lateSnapshots = 0;
                _lateMask = 0;
                _behindGuard = Tick; // the snapshots on the way still show the old lateness
                _lead = lead;
                _accumulator = 0f; // the time owed is covered by the jump
                _lastRealtime = NetFrameClock.Now;
                Corrected(resyncFrom);
                return;
            }
            // V5 M9b review: a snapshot from before the server acted on this client's hold or pick (the hold or the pick still
            // on its way, e.g. a lost packet resent) shows the server's ship past the hold or still held where this client
            // moves again; the server rewinds or catches up once it arrives (NetLevelUps), so such a snapshot is not used
            // to correct the prediction.
            if (_pauseFrom != 0 && ack >= _pauseFrom && (s.HoldPending || !_resumeConfirmed && _resumeAt != 0 && ack >= _resumeAt && s.Paused))
            {
                SkippedSnapshots++;
                return;
            }
            LastAck = ack;
            // The lead is measured on time only: not from a snapshot the server guessed for, nor while this page runs slowly
            // (a hidden tab handles a second's snapshots in one frame, its tick standing still while their acks climb).
            if (ack != 0 && s.inputMargin >= 0 && steady) _lead = Mathf.Clamp((int)(Tick - ack), 2, 60);
            TrimExcessLead(s, steady);
            var before = _simPos;
            if (ack >= _historyStart && Tick - ack < History)
            {
                Metrics.PredictionError.Add(Flat(_predicted[ack % History] - s.motion.position));
                Metrics.InputLagMs.Add((Tick - ack) * NetProtocol.TickSeconds * 1000f);
            }
            Predicted.Restore(s.motion);
            Predicted.RestoreBoost(s.boost);
            if (_resumeConfirmed && ack >= _resumeAt) { _pauseFrom = _resumeAt = 0; _resumeConfirmed = false; } // the server has moved past the pause
            var from = ack >= _historyStart ? ack + 1 : _historyStart;
            if (Tick >= from && Tick - from >= History - 1) from = Tick - (History - 2); // far behind: the server's state, then the latest inputs
            for (var t = from; t <= Tick; t++)
            {
                Step(t, _inputs[t % History], true);
                _predicted[t % History] = Predicted.Position;
            }
            var after = Predicted.Position;
            var correction = Flat(after - before);
            Metrics.Correction.Add(correction);
            if (correction > 1f && LargeCorrections++ < 20)
                Debug.Log($"[V5 Net] large correction {correction:0.00} m: tick {Tick} ack {ack} from {from} historyStart {_historyStart} pause {_pauseFrom}-{_resumeAt} " +
                          $"confirmed {_resumeConfirmed} paused {s.Paused} alive {s.Alive} welcomes {Welcomes} predictionError {(ack >= _historyStart ? Flat(_predicted[ack % History] - s.motion.position) : -1f):0.00}");
            Corrected(before);
        }

        /// <summary>
        /// M9b playtest fixes: the opposite of late. Inputs that wait at the server longer than the link's jitter needs are
        /// input delay for nothing (a resync's slack, a lead taken on a slow link that got faster). When the least margin
        /// over a second of steady snapshots is <see cref="ExcessMarginTicks"/> or more, the tick line moves back a tick.
        /// </summary>
        void TrimExcessLead(in ShipSnapshot s, bool steady)
        {
            if (!steady || s.ackTick == 0 || Holds(s.ackTick) || _pauseFrom != 0) { _marginWindow = 0; _marginMin = sbyte.MaxValue; return; }
            _marginMin = Math.Min(_marginMin, (int)s.inputMargin);
            if (++_marginWindow < MarginWindowSnapshots) return;
            if (_marginMin >= ExcessMarginTicks && _trimOwed <= 0f) { _trimOwed += NetProtocol.TickSeconds; LeadTrims++; }
            _marginWindow = 0;
            _marginMin = sbyte.MaxValue;
        }

        const int ExcessMarginTicks = 3, MarginWindowSnapshots = 15;
        int _marginWindow, _marginMin = sbyte.MaxValue;
        float _trimOwed;
        /// <summary>Ticks of lead given back because the inputs waited at the server longer than needed.</summary>
        public int LeadTrims { get; private set; }

        /// <summary>Snapshots (of the last <see cref="LateWindowSnapshots"/>) showing this client's inputs late before it moves its tick ahead (or at once when
        /// they were this many ticks late); the extra ticks of lead it then takes.</summary>
        const int LateSnapshotsToResync = 3, LateTicksToResync = 3, ResyncSlackTicks = 3;
        /// <summary>V5 M9b close: the late snapshots are counted over this many (about half a second at 15 Hz).</summary>
        const int LateWindowSnapshots = 8;
        int _lateMask;
        /// <summary>A frame longer than this is this page running slowly (not the link); late inputs are not acted on until
        /// <see cref="SlowFrameQuietSeconds"/> after the last one.</summary>
        const float SlowFrameSeconds = 0.2f, SlowFrameQuietSeconds = 1f;
        double _slowFrameAt = double.NegativeInfinity;
        int _lateSnapshots;
        uint _behindGuard;
        bool _resyncNext;
        int _pickSentSeq = -1;
        /// <summary>M9b playtest fixes: times the client moved its tick ahead because its inputs arrived late.</summary>
        public int LateResyncs { get; private set; }

        // ---- The drawn ship (M9b playtest fixes, A) ------------------------------------------------------------------

        Vector3 _simPos, _drawFromPos;
        Quaternion _simRot = Quaternion.identity, _drawFromRot = Quaternion.identity;
        bool _drawn, _haveSimPose;

        /// <summary>The simulated pose moved by a rewind and replay from <paramref name="before"/>: the drawn ship keeps its
        /// place (the tick it is drawn from moves with the correction, the visual offset takes the difference and eases it out);
        /// a correction over <see cref="SnapCorrectionMetres"/> is drawn at once.</summary>
        void Corrected(Vector3 before)
        {
            var after = Predicted.Position;
            var afterRot = Predicted.Rotation;
            var delta = after - before;
            if (Flat(delta) > SnapCorrectionMetres || !_haveSimPose) { SnapDrawnShip(); return; }
            _drawFromPos += delta;
            _drawFromRot = afterRot * Quaternion.Inverse(_simRot) * _drawFromRot;
            _simPos = after;
            _simRot = afterRot;
            VisualOffset += new Vector3(-delta.x, 0f, -delta.z);
        }

        /// <summary>The drawn ship is where the simulated one is (a welcome, a resync, a large correction).</summary>
        void SnapDrawnShip()
        {
            if (Predicted == null) return;
            _simPos = _drawFromPos = Predicted.Position;
            _simRot = _drawFromRot = Predicted.Rotation;
            VisualOffset = Vector3.zero;
            _haveSimPose = true;
        }

        /// <summary>Not ticking (held for a welcome, away): the ship is drawn still at its simulated pose.</summary>
        void HoldDrawnShip()
        {
            if (!_haveSimPose) return;
            _drawFromPos = _simPos;
            _drawFromRot = _simRot;
        }

        /// <summary>The transform and the body back on the simulated pose before anything steps, rewinds or reads it.</summary>
        void UndrawShip()
        {
            if (!_drawn || Predicted == null || !_haveSimPose) return;
            _drawn = false;
            Predicted.SetSimulatedPose(_simPos, _simRot);
        }

        /// <summary>Late in the frame (<see cref="NetShipDraw"/>, before the camera follows): the ship drawn between its last
        /// two ticks by the time since the last one, plus the easing correction.</summary>
        internal void DrawShip()
        {
            if (Predicted == null || !_haveSimPose) return;
            var alpha = Mathf.Clamp01(_accumulator / NetProtocol.TickSeconds);
            var position = Vector3.LerpUnclamped(_drawFromPos, _simPos, alpha) + VisualOffset;
            var rotation = Quaternion.Slerp(_drawFromRot, _simRot, alpha);
            if (!InterpolateDrawnShip) { position = _simPos + VisualOffset; rotation = _simRot; }
            RenderPosition = position;
            RenderRotation = rotation;
            Predicted.Draw(position, rotation);
            _drawn = true;
        }

        // ---- Away (M9b playtest fixes, E) ----------------------------------------------------------------------------

        /// <summary>Whether the player's page is hidden now (the voyage client reads the browser's visibility; tests set it).
        /// Away, the client stops ticking and tells the server, which holds the solo voyage; back, it tells the server again
        /// and waits for the welcome that resyncs its ship (or reconnects first if the socket went meanwhile).</summary>
        public Func<bool> AwaySource { get; set; }
        public bool Away { get; private set; }
        public int AwaysSent { get; private set; }
        public int BacksSent { get; private set; }
        public float AwaySince { get; private set; } = -1f;
        bool _awaitingWelcome;
        float _backSentAt;

        void UpdatePresence()
        {
            var away = AwaySource != null && AwaySource();
            if (away == Away) return;
            Away = away;
            var now = Time.realtimeSinceStartup;
            if (away)
            {
                AwaySince = now;
                if (Connected && Welcomed) SendPresence(true);
                return;
            }
            AwaySince = -1f;
            if (Connected && Welcomed)
            {
                SendPresence(false);
                _awaitingWelcome = true; // no ticks until the server's welcome says where the ship is
                _backSentAt = now;
            }
            else if (_droppedAt >= 0f)
            {
                _droppedAt = now; // the socket went while away: the reconnect window starts now
                _nextAttemptAt = now;
            }
        }

        void SendPresence(bool away)
        {
            _writer.Clear();
            NetCodec.WritePresence(_writer, away);
            SendNow(NetProtocol.Presence, NetworkDelivery.ReliableSequenced);
            if (away) AwaysSent++; else BacksSent++;
        }

        static float Flat(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);

        // ---- Things a client is not trusted with ------------------------------------------------------------------

        /// <summary>Tests: what a tampered client would send, its own hull (the server has no use for it).</summary>
        public void SendHullReport(float hull)
        {
            _writer.Clear();
            _writer.U8(NetProtocol.Version);
            _writer.F32(hull);
            Send(NetProtocol.ClientReport, NetworkDelivery.ReliableSequenced);
        }

        /// <summary>Tests: extra bytes after the next input packet's inputs (a hull value smuggled in an input).</summary>
        public void AppendToNextInput(byte[] bytes) => _appendToNextInput = bytes;

        /// <summary>Tests: level-up messages (holds and picks) leave this many seconds late, as a lost packet resent would.</summary>
        public float DelayLevelUpMessagesForTest { get; set; }
        readonly System.Collections.Generic.List<(float at, byte[] bytes)> _delayed = new System.Collections.Generic.List<(float, byte[])>();

        void FlushDelayedForTest()
        {
            for (var i = 0; i < _delayed.Count; i++)
            {
                if (Time.realtimeSinceStartup < _delayed[i].at) continue;
                _writer.Clear();
                _writer.Bytes(_delayed[i].bytes, _delayed[i].bytes.Length);
                _delayed.RemoveAt(i--);
                SendNow(NetProtocol.Pick, NetworkDelivery.ReliableSequenced);
            }
        }

        void Send(string message, NetworkDelivery delivery)
        {
            if (message == NetProtocol.Pick && DelayLevelUpMessagesForTest > 0f)
            {
                var bytes = new byte[_writer.Length];
                Array.Copy(_writer.Buffer, bytes, bytes.Length);
                _delayed.Add((Time.realtimeSinceStartup + DelayLevelUpMessagesForTest, bytes));
                return;
            }
            SendNow(message, delivery);
        }

        void SendNow(string message, NetworkDelivery delivery)
        {
            if (!Connected) return;
            var length = _writer.Length;
            using (var w = new FastBufferWriter(length, Allocator.Temp))
            {
                w.WriteBytesSafe(_writer.Buffer, length);
                Manager.CustomMessagingManager.SendNamedMessage(message, NetworkManager.ServerClientId, w, delivery);
            }
            Metrics.Up(NetworkManager.ServerClientId, length);
        }
    }
}
