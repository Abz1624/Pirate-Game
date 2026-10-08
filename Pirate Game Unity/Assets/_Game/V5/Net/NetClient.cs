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
        /// <summary>Makes the prediction ship at the first welcome.</summary>
        public Func<ShipMotionPort> ShipFactory { get; set; }
        public ShipMotionPort Predicted { get; private set; }

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
        public Vector3 RenderPosition => Predicted != null ? Predicted.Position + VisualOffset : Vector3.zero;

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
            var messages = Manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(NetProtocol.Welcome, OnWelcome);
            messages.RegisterNamedMessageHandler(NetProtocol.Snapshot, OnSnapshot);
            messages.RegisterNamedMessageHandler(NetProtocol.Enemies, OnEnemies);
            messages.RegisterNamedMessageHandler(NetProtocol.Defs, OnDefs);
            messages.RegisterNamedMessageHandler(NetProtocol.End, OnEnd);
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

        bool ShouldReconnect =>
            AutoReconnect && !_stopped && !Ended && _resume != null && _droppedAt >= 0f &&
            Time.realtimeSinceStartup - _droppedAt <= ReconnectWindowSeconds;

        // ---- Server messages ----------------------------------------------------------------------------------------

        NetReader Read(FastBufferReader reader)
        {
            var length = reader.Length - reader.Position;
            var bytes = new byte[Math.Max(0, length)];
            if (length > 0) reader.ReadBytesSafe(ref bytes, length);
            Metrics.Down(NetworkManager.ServerClientId, length);
            return new NetReader(bytes);
        }

        void OnWelcome(ulong sender, FastBufferReader reader)
        {
            if (!NetCodec.ReadWelcome(Read(reader), out var m)) return;
            _resume = m.resumeToken;
            if (Predicted == null) Predicted = ShipFactory?.Invoke();
            if (_droppedAt >= 0f) Metrics.ResumeSeconds.Add(Time.realtimeSinceStartup - _droppedAt);
            _droppedAt = -1f;
            Predicted?.Restore(m.ship.motion);
            Last = m.ship;
            LastAck = 0;
            _historyStart = Tick + 1; // inputs from here on are the ones the server will apply
            _accumulator = 0f;
            _lastRealtime = -1;
            Enemies.Clear();
            Welcomed = true;
            Welcomes++;
        }

        void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (!NetCodec.ReadShip(Read(reader), out var s)) return;
            if (s.serverTick <= Last.serverTick && Snapshots > 0) return; // late
            Snapshots++;
            Last = s;
            Reconcile(s);
        }

        void OnEnemies(ulong sender, FastBufferReader reader)
        {
            var r = Read(reader);
            try { Enemies.Apply(r, Predicted != null ? Predicted.Position.y : 0f); }
            catch (Exception) { /* malformed batch: dropped */ }
        }

        void OnDefs(ulong sender, FastBufferReader reader) => NetCodec.ReadDefs(Read(reader), Enemies.SetDefinition);

        void OnEnd(ulong sender, FastBufferReader reader)
        {
            if (!NetCodec.ReadEnd(Read(reader), out var reason, out _, out _)) return;
            Ended = true;
            EndReason = reason;
        }

        // ---- Prediction -----------------------------------------------------------------------------------------------

        void Update()
        {
            if (ShouldReconnect && Manager != null && !Manager.IsListening && !Manager.ShutdownInProgress && Time.realtimeSinceStartup >= _nextAttemptAt)
            {
                _nextAttemptAt = Time.realtimeSinceStartup + 0.5f;
                Connect();
            }
            if (VisualOffset != Vector3.zero)
            {
                VisualOffset *= Mathf.Exp(-12f * Time.deltaTime);
                if (VisualOffset.sqrMagnitude < 1e-6f) VisualOffset = Vector3.zero;
            }
            if (!Welcomed || Ended || Predicted == null || !Connected) { _lastRealtime = -1; return; }
            // Real time (Time.deltaTime is capped at a third of a second): after a hitch the client catches its ticks up,
            // so its tick stays in step with the server's wall clock. A stall over 2 s is dropped; the next snapshot
            // then names a tick ahead of this one and the client jumps to it (Reconcile).
            var now = Time.realtimeSinceStartupAsDouble;
            if (_stallUntil > 0 && now < _stallUntil) return; // tests: this side alone freezes
            _stallUntil = 0;
            if (_lastRealtime >= 0) _accumulator += (float)(now - _lastRealtime);
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
            _stallUntil = Time.realtimeSinceStartupAsDouble + seconds;
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>Tests: a forged client clock (this many ticks per real tick, numbered ahead).</summary>
        public int TickScaleForTest { get; set; } = 1;
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
            Predicted.Step(input.SteerThrottle, NetProtocol.TickSeconds); // on the quantized input, as the server will
            _predicted[slot] = Predicted.Position;
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
            if (ack > Tick)
            {
                // This client fell behind the server's input timeline (a long stall): take the server's state and move
                // the tick ahead of it by the usual lead, predicting those ticks on the last input (the server repeats it).
                var held = _inputs[Tick % History];
                Predicted.Restore(s.motion);
                LastAck = ack;
                Tick = ack + (uint)_lead;
                _historyStart = ack + 1;
                for (var t = ack + 1; t <= Tick; t++)
                {
                    _inputs[t % History] = held;
                    Predicted.Step(held.SteerThrottle, NetProtocol.TickSeconds);
                    _predicted[t % History] = Predicted.Position;
                }
                Resyncs++;
                _accumulator = 0f; // the time owed is covered by the jump
                _lastRealtime = Time.realtimeSinceStartupAsDouble;
                return;
            }
            LastAck = ack;
            if (ack != 0) _lead = Mathf.Clamp((int)(Tick - ack), 2, 60);
            var before = Predicted.Position;
            if (ack >= _historyStart && Tick - ack < History)
            {
                Metrics.PredictionError.Add(Flat(_predicted[ack % History] - s.motion.position));
                Metrics.InputLagMs.Add((Tick - ack) * NetProtocol.TickSeconds * 1000f);
            }
            Predicted.Restore(s.motion);
            var from = ack >= _historyStart ? ack + 1 : _historyStart;
            if (Tick >= from && Tick - from >= History - 1) from = Tick - (History - 2); // far behind: the server's state, then the latest inputs
            for (var t = from; t <= Tick; t++)
            {
                Predicted.Step(_inputs[t % History].SteerThrottle, NetProtocol.TickSeconds);
                _predicted[t % History] = Predicted.Position;
            }
            var after = Predicted.Position;
            Metrics.Correction.Add(Flat(after - before));
            VisualOffset += new Vector3(before.x - after.x, 0f, before.z - after.z);
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

        void Send(string message, NetworkDelivery delivery)
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
