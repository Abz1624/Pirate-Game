using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using RoguePirates.V4;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Ports;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V5.Net.Tests
{
    /// <summary>
    /// V5 M9b (I9b.1, T9b.x): the match server and its clients in one Editor. Each test loads a Scenario Lab state as a
    /// headless server voyage (the shipped run, no presenters, 30 Hz), starts a <see cref="NetServer"/> on plain
    /// WebSockets on a loopback port, and connects <see cref="NetClient"/>s with join tickets. Reports go to
    /// Temp/V5Net/*.json (copied to plan/v5/phase-09b-netcode/ as evidence).
    /// </summary>
    [Category("V5")]
    public sealed class V5NetPlayModeTests
    {
        const float Timeout = 10f;
        NetSaveIsolation _isolation;
        NetServer _server;
        readonly List<NetClient> _clients = new List<NetClient>();
        byte[] _key;
        JoinTicketIssuer _issuer;
        ushort _port;
        V4Game _game;

        sealed class ScriptedInput : IShipSteerInput
        {
            public Vector2 Value;
            public Vector2 ReadSteerThrottle() => Value;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new NetSaveIsolation();
            NetServerComposition.PrepareHeadlessRun();
            _port = (ushort)Random.Range(20000, 40000);
            _key = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(_key);
            _key = System.Text.Encoding.UTF8.GetBytes(JoinTicket.Base64Url(_key)); // a key for this test only
            _issuer = new JoinTicketIssuer(_key);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var c in _clients) if (c != null) c.Stop();
            if (_server != null) _server.Stop();
            yield return null;
            yield return null;
            foreach (var c in _clients) if (c != null) Object.Destroy(c.gameObject);
            if (_server != null) Object.Destroy(_server.gameObject);
            _clients.Clear();
            _server = null;
            NetLinkSimulator.Clear();
            NetServerComposition.EndHeadlessRun();
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        // ---- Helpers --------------------------------------------------------------------------------------------------

        IEnumerator StartVoyage(string scenario, NetLinkSimulator.Link? link = null)
        {
            yield return Scenario.Load(scenario);
            Assert.IsNotNull(Scenario.Current, scenario + " is loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors, scenario + " set up without problems");
            _game = Scenario.Current.Game;
            Assert.AreEqual(30f, _game.Config.Single<RunConfig>().tickRate, "the server's run ticks at 30 Hz");
            Assert.IsNull(GameObject.Find("V4Presentation"), "the server's run has no presenters");
            _server = NetServer.Create(_port, new JoinTicketVerifier(_key), "127.0.0.1");
            _server.Attach(_game);
            if (link.HasValue) NetLinkSimulator.Configure(_server.Transport, link.Value);
            Assert.IsTrue(_server.StartListening(), "the server listens");
            Assert.IsTrue(_server.Ship.Controlled, "the net tick steps the run's ship");
        }

        NetClient NewClient(string ticket, NetLinkSimulator.Link? link = null, string name = "V5NetClient")
        {
            var serverShip = _game.Shell.Player;
            var client = NetClient.Create(NetEndpoint.Parse($"ws://127.0.0.1:{_port}/"), ticket, () => ShipMotionPort.CreatePredictionCopy(serverShip), name);
            if (link.HasValue) NetLinkSimulator.Configure(client.Transport, link.Value);
            _clients.Add(client);
            return client;
        }

        string Ticket(string player = "player-1") => _issuer.Issue(player, null, JoinTicket.UnixNow());

        IEnumerator Join(NetClient client)
        {
            Assert.IsTrue(client.Connect(), "the client starts");
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (!client.Welcomed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(client.Welcomed, "welcomed (" + client.LastDisconnectReason + ")");
        }

        static IEnumerator Seconds(float s)
        {
            var until = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        string Report(string name, params NetMetrics[] sections)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Net", name + ".json"));
            var json = NetMetrics.WriteReport(path, name, new Dictionary<string, string> { ["scenario"] = Scenario.Current != null ? Scenario.Current.Config.name : "" }, sections);
            Debug.Log($"[V5 Net] {name}: {path}\n{json}");
            return json;
        }

        // ---- Tests --------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Spawn_TheClientSailsTheServersShip()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput();
            yield return Join(client);
            yield return Seconds(1f);
            Assert.IsNotNull(client.Predicted, "the prediction ship exists");
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 0.05f, "it starts where the server's ship is");
            Assert.Greater(client.Snapshots, 10, "snapshots arrive (15 Hz)");
            Assert.Greater(client.MaxHull, 0f);
            Assert.AreEqual(_server.Ship.Hull, client.Hull, 0.01f, "the hull shown is the server's");
            Assert.GreaterOrEqual(client.Enemies.Count, 1, "the dinghy is mirrored");
            var any = false;
            foreach (var e in client.Enemies.All) any |= client.Enemies.DefinitionId(e.definition) == "deckhand_dinghy";
            Assert.IsTrue(any, "with its kind");
            Assert.Greater(_server.Metrics.For(client.Manager.LocalClientId).messagesDown, 0, "the server counts its traffic");
        }

        [UnityTest]
        public IEnumerator Input_MovesTheServersShipAuthoritatively()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            var input = new ScriptedInput();
            client.Input = input;
            yield return Join(client);
            var start = _server.Ship.Position;
            yield return Seconds(0.5f);
            Assert.Less(Flat(_server.Ship.Position, start), 0.01f, "no input, no movement");
            input.Value = new Vector2(0f, 1f);
            yield return Seconds(2f);
            input.Value = Vector2.zero;
            var moved = Flat(_server.Ship.Position, start);
            Assert.Greater(moved, 10f, "full throttle for 2 s moved the server's ship");
            Assert.Greater(Vector3.Dot(_server.Ship.Position - start, _server.Ship.Forward), 0f, "forward");
            yield return Seconds(1f); // the acks catch up while both coast
            Assert.Greater(client.LastAck, 30u, "the server applied the client's inputs");
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 0.5f, "the prediction is where the server's ship is");
            Report("input-clean", _server.Metrics, client.Metrics);
        }

        [UnityTest]
        public IEnumerator Damage_OnlyTheServerDealsIt()
        {
            yield return StartVoyage("net_voyage_rammed");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput();
            yield return Join(client);
            var max = _server.Ship.MaxHull;
            yield return Until(() => _server.Ship.Hull < max - 0.5f, 20f);
            Assert.Less(_server.Ship.Hull, max, "the dinghies rammed the server's ship");
            yield return Seconds(0.3f);
            Assert.AreEqual(_server.Ship.Hull, client.Hull, 2f, "the client shows the server's hull");
            Assert.Less(client.Hull, max, "including the damage");
            Assert.AreEqual(1f, client.Predicted.Root.GetComponent<RoguePirates.V4.Foundation.Combat.DamageReceiver>().CurrentHP, 1e-4f,
                "the client's own ship copy never took damage (its hull is not the game's)");
            Report("rammed-clean", _server.Metrics, client.Metrics);
        }

        [UnityTest]
        public IEnumerator ClientSentHull_IsIgnored()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput();
            yield return Join(client);
            yield return Seconds(0.5f);
            var before = _server.Ship.Hull;
            client.SendHullReport(99999f);
            client.SendHullReport(0f);
            var smuggled = new NetWriter();
            smuggled.F32(99999f);
            smuggled.Str("hull");
            client.AppendToNextInput(smuggled.ToArray());
            yield return Seconds(1f);
            Assert.AreEqual(2, _server.IgnoredClientReports, "both reports arrived and were dropped unread");
            Assert.GreaterOrEqual(_server.IgnoredInputBytes, 4, "the smuggled bytes were not read");
            Assert.LessOrEqual(_server.Ship.Hull, before, "the hull was not raised");
            Assert.LessOrEqual(_server.Ship.Hull, _server.Ship.MaxHull);
            Assert.IsTrue(_server.Ship.Alive, "nor sunk by the client's claim");
            Assert.AreEqual(_server.Ship.Hull, client.Hull, 0.01f, "the client still shows the server's hull");
        }

        [UnityTest]
        public IEnumerator Reconnect_Within10Seconds_Resumes()
        {
            yield return StartVoyage("net_voyage_open_water");
            var ticket = Ticket();
            var client = NewClient(ticket);
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            yield return Join(client);
            yield return Seconds(1f);
            var playerBefore = _server.Current.playerId;

            // The server side drops the socket.
            var dropAt = Time.realtimeSinceStartup;
            _server.DropConnectionForTest();
            yield return Until(() => client.Welcomes >= 2, 10f);
            var resumed = Time.realtimeSinceStartup - dropAt;
            Assert.AreEqual(2, client.Welcomes, "resumed after a server-side drop (" + client.LastDisconnectReason + ")");
            Assert.Less(resumed, 10f, "within 10 s");
            Assert.AreEqual(playerBefore, _server.Current.playerId, "the same session");

            // The client side drops it.
            var at = _server.Ship.Position;
            client.DropForTest();
            yield return Until(() => client.Welcomes >= 3, 10f);
            Assert.AreEqual(3, client.Welcomes, "resumed after a client-side drop");
            yield return Seconds(1f);
            Assert.Greater(Flat(_server.Ship.Position, at), 5f, "the inputs move the ship again");
            input.Value = Vector2.zero;
            yield return Seconds(1f);
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 0.5f, "and the prediction follows it");

            // The join ticket was single use: a second client with it is refused.
            var replay = NewClient(ticket, null, "V5NetReplay");
            replay.AutoReconnect = false;
            Assert.IsTrue(replay.Connect());
            yield return Seconds(2f);
            Assert.IsFalse(replay.Welcomed, "a used ticket does not join");
            StringAssert.Contains("Replayed", _server.LastRejectReason);
            Debug.Log($"[V5 Net] resume after a server-side drop {resumed:0.00} s; client resume times {string.Join(", ", client.Metrics.ResumeSeconds)}");
            Report("reconnect", _server.Metrics, client.Metrics);
        }

        [UnityTest]
        public IEnumerator JoinTicket_ForgedExpiredOrForeign_IsRefused()
        {
            yield return StartVoyage("net_voyage_open_water");
            var now = JoinTicket.UnixNow();
            var other = new JoinTicketIssuer(System.Text.Encoding.UTF8.GetBytes(new string('x', 40)));
            var cases = new[]
            {
                ("forged", other.Issue("player-1", null, now), "BadSignature"),
                ("expired", _issuer.Issue("player-1", null, now - 300, 60), "Expired"),
                ("garbage", "v1.not-a-ticket", "Malformed"),
            };
            foreach (var (label, ticket, reason) in cases)
            {
                var c = NewClient(ticket, null, "V5Net_" + label);
                c.AutoReconnect = false;
                Assert.IsTrue(c.Connect());
                yield return Seconds(1.5f);
                Assert.IsFalse(c.Welcomed, label + " does not join");
                StringAssert.Contains(reason, _server.LastRejectReason, label);
                c.Stop();
            }
            Assert.AreEqual(3, _server.RejectedJoins);
            var good = NewClient(Ticket());
            good.Input = new ScriptedInput();
            yield return Join(good);
        }

        [UnityTest]
        public IEnumerator Latency150msLoss2Percent_PredictionWithinHalfAMetre()
        {
            var serverLink = new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 11 };
            var clientLink = new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 12 };
            yield return StartVoyage("net_voyage_open_water", serverLink);
            var client = NewClient(Ticket(), clientLink);
            var input = new ScriptedInput();
            client.Input = input;
            yield return Join(client);
            _server.Metrics.Reset();
            client.Metrics.Reset();
            // A voyage's worth of handling: full ahead, hard turns both ways (the turn reverse pause too), easing off, astern.
            var pattern = new[]
            {
                (2f, new Vector2(0f, 1f)), (2.5f, new Vector2(1f, 1f)), (2.5f, new Vector2(-1f, 1f)), (1.5f, new Vector2(0f, 0.4f)),
                (2f, new Vector2(0.6f, 0.8f)), (1f, new Vector2(-1f, 0f)), (1.5f, new Vector2(0f, -1f)), (2f, new Vector2(1f, 1f)), (2f, Vector2.zero),
            };
            foreach (var (seconds, value) in pattern)
            {
                input.Value = value;
                yield return Seconds(seconds);
            }
            var json = Report("latency-150ms-2pct", _server.Metrics, client.Metrics);
            var errors = client.Metrics.PredictionError;
            Assert.Greater(errors.Count, 150, "about 15 snapshots a second compared");
            Assert.LessOrEqual(NetMetrics.Max(errors), 0.5f, "prediction error at most 0.5 m");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.Correction), 0.5f, "no single correction above 0.5 m");
            Assert.Greater(_server.Metrics.TickMs.Count, 300, "server ticks measured");
            Assert.Greater(NetMetrics.Percentile(client.Metrics.InputLagMs, 50f), 150f, "the link's 150 ms round trip is there");
            Assert.Greater(client.Metrics.PredictionError.Count, 0);
            StringAssert.Contains("bytesDownPerSecond", json);
        }

        /// <summary>Review fix: a client that alone stalls (a GC pause, a background tab) catches its ticks up, or jumps to
        /// the server's after a long stall, and its inputs drive the ship again.</summary>
        [UnityTest]
        public IEnumerator ClientStall_InputsDriveTheShipAgain()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            yield return Join(client);
            yield return Seconds(1f);
            foreach (var stall in new[] { 1f, 3f })
            {
                client.StallForTest(stall);
                yield return Seconds(stall + 1f);
                var guessed = _server.Metrics.GuessedInputs;
                var ack = client.LastAck;
                input.Value = new Vector2(stall > 2f ? -1f : 1f, 1f);
                yield return Seconds(1f);
                Assert.Greater(client.LastAck, ack + 20, "after a " + stall + " s stall the server applies the client's inputs again");
                Assert.AreEqual(guessed, _server.Metrics.GuessedInputs, "after a " + stall + " s stall nothing is guessed any more");
                Assert.Less(Mathf.Abs(_server.Ship.Capture().lastSteer - NetInput.From(input.Value, false).SteerThrottle.x), 0.01f, "the server steers as the client does");
            }
            Assert.GreaterOrEqual(client.Resyncs, 1, "the long stall jumped to the server's tick");
            input.Value = Vector2.zero;
            yield return Seconds(1.5f);
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 0.5f, "the prediction is back on the server's ship");
            Report("client-stall", _server.Metrics, client.Metrics);
        }

        /// <summary>Review fix: a client numbering its ticks three times too fast gets one step per server tick, no more.</summary>
        [UnityTest]
        public IEnumerator ForgedFastClock_GainsNoSpeed()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.TickScaleForTest = 3;
            yield return Join(client);
            var ticks = _server.TicksWithSession;
            var clientTicks = client.Tick;
            yield return Seconds(3f);
            Assert.Greater(client.Tick - clientTicks, 2.5f * (_server.TicksWithSession - ticks), "the client ran its clock fast");
            Assert.LessOrEqual(_server.StepsApplied, _server.TicksWithSession, "the server never stepped faster than its tick");
            Assert.Greater(_server.Metrics.CatchUpSteps, 0, "the surplus inputs were skipped");
        }

        /// <summary>Review fix: after a silent drop the server still holds the old socket; the resume token takes over.</summary>
        [UnityTest]
        public IEnumerator ResumeWhileTheOldSocketLingers_TakesOver()
        {
            yield return StartVoyage("net_voyage_open_water");
            var first = NewClient(Ticket());
            first.Input = new ScriptedInput();
            first.AutoReconnect = false;
            yield return Join(first);
            var token = first.ResumeToken;
            var second = NewClient(null, null, "V5NetSecondSocket");
            second.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            second.UseResumeToken(token);
            var at = Time.realtimeSinceStartup;
            yield return Join(second);
            Assert.Less(Time.realtimeSinceStartup - at, 10f, "resumed within 10 s");
            yield return Until(() => !first.Connected, 3f);
            Assert.IsFalse(first.Connected, "the old socket was let go");
            Assert.AreEqual(second.Manager.LocalClientId, _server.Current.clientId);
            Assert.IsFalse(JoinTicket.SameBytes(token, second.ResumeToken), "the token is fresh in every welcome");
            var start = _server.Ship.Position;
            yield return Seconds(1f);
            Assert.Greater(Flat(_server.Ship.Position, start), 3f, "the new socket's inputs drive the ship");
        }

        /// <summary>Review fix: the hook sees a real divergence: the boost's speed runs on the server's frame clock, which the
        /// prediction does not model (part 2), so boosting gives prediction errors and corrections; measured and reported.</summary>
        [UnityTest]
        public IEnumerator Boost_DivergesAndIsCorrected_Measured()
        {
            var link = new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 31 };
            yield return StartVoyage("net_voyage_open_water", link);
            var client = NewClient(Ticket(), new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 32 });
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            var boost = false;
            client.Boost = () => boost;
            yield return Join(client);
            yield return Seconds(1.5f);
            client.Metrics.Reset();
            boost = true;
            yield return Seconds(0.5f);
            boost = false;
            yield return Seconds(5f);
            Report("boost-150ms-2pct", _server.Metrics, client.Metrics);
            Assert.Greater(NetMetrics.Max(client.Metrics.PredictionError), 0.01f, "the boost is not predicted: the error shows");
            yield return Seconds(2f);
            input.Value = Vector2.zero;
            yield return Seconds(1.5f);
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 0.5f, "and reconciliation brings the ship back");
        }

        /// <summary>A worse link than the gate's (jitter that reorders packets, 5% loss): measured and reported; the
        /// prediction still has to hold.</summary>
        [UnityTest]
        public IEnumerator Jitter40msLoss5Percent_Measured()
        {
            var serverLink = new NetLinkSimulator.Link { sendDelayMs = 75, sendJitterMs = 40, sendDropPercent = 5, seed = 21 };
            var clientLink = new NetLinkSimulator.Link { sendDelayMs = 75, sendJitterMs = 40, sendDropPercent = 5, seed = 22 };
            yield return StartVoyage("net_voyage_open_water", serverLink);
            var client = NewClient(Ticket(), clientLink);
            var input = new ScriptedInput();
            client.Input = input;
            yield return Join(client);
            _server.Metrics.Reset();
            client.Metrics.Reset();
            var pattern = new[] { (2f, new Vector2(0f, 1f)), (2f, new Vector2(1f, 1f)), (2f, new Vector2(-1f, 1f)), (2f, new Vector2(0.5f, -1f)), (2f, Vector2.zero) };
            foreach (var (seconds, value) in pattern)
            {
                input.Value = value;
                yield return Seconds(seconds);
            }
            Report("jitter-40ms-5pct", _server.Metrics, client.Metrics);
            Assert.Greater(client.Metrics.PredictionError.Count, 100);
            Assert.LessOrEqual(NetMetrics.Percentile(client.Metrics.PredictionError, 95f), 0.5f, "p95 prediction error at most 0.5 m");
        }

        [UnityTest]
        public IEnumerator Measure_BusyVoyageAt10Minutes()
        {
            yield return StartVoyage("net_voyage_busy");
            var client = NewClient(Ticket());
            var input = new ScriptedInput { Value = new Vector2(0.3f, 0.7f) };
            client.Input = input;
            yield return Join(client);
            yield return Seconds(2f);
            _server.Metrics.Reset();
            client.Metrics.Reset();
            yield return Seconds(15f);
            Report("busy-10min-clean", _server.Metrics, client.Metrics);
            Debug.Log($"[V5 Net] busy voyage: {client.Last.kills} kills, {client.Enemies.Count} enemies mirrored, up to {NetMetrics.Max(_server.Metrics.SnapshotEnemies)} a snapshot");
            Assert.Greater(NetMetrics.Max(_server.Metrics.SnapshotEnemies), 20f, "enemies were sent");
            Assert.Greater(client.Enemies.Count, 0, "and mirrored");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.Correction), 0.5f, "corrections stay small on a clean link");
        }
    }

    /// <summary>The save swapped for an in-memory one, and the PlayerPrefs a run touches put back (as the V4 tests do).</summary>
    sealed class NetSaveIsolation : System.IDisposable
    {
        static readonly string[] IntKeys = { "v3_home_seed", "v3_first_conquest_complete", "v3_saltwind_runs_completed", "v3_selected_captain", "combat_captain_actives_enabled", "v4_lifetime_gold", "v4_tutorial_progress" };
        const string MetaKey = "highseas_meta_v1", SnapshotKey = "highseas_run_snapshot_v1", RecordsKey = "v4_run_records";
        readonly SaveService _original;
        readonly bool[] _had = new bool[IntKeys.Length];
        readonly int[] _values = new int[IntKeys.Length];
        readonly string _meta, _runSnapshot, _records;
        readonly bool _demoOff;

        public NetSaveIsolation()
        {
            AppRoot.EnsureInitialized();
            _demoOff = DemoMode.Off;
            _original = AppRoot.Instance.Save;
            _meta = PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
            _runSnapshot = PlayerPrefs.HasKey(SnapshotKey) ? PlayerPrefs.GetString(SnapshotKey) : null;
            _records = PlayerPrefs.HasKey(RecordsKey) ? PlayerPrefs.GetString(RecordsKey) : null;
            AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend()));
            for (var i = 0; i < IntKeys.Length; i++)
            {
                _had[i] = PlayerPrefs.HasKey(IntKeys[i]);
                _values[i] = PlayerPrefs.GetInt(IntKeys[i], 0);
            }
        }

        public void Dispose()
        {
            if (AppRoot.Instance != null) AppRoot.Instance.ReplaceSave(_original);
            for (var i = 0; i < IntKeys.Length; i++)
            {
                if (_had[i]) PlayerPrefs.SetInt(IntKeys[i], _values[i]);
                else PlayerPrefs.DeleteKey(IntKeys[i]);
            }
            if (_meta != null) SaveService.RestoreSnapshot(_meta);
            if (_runSnapshot != null) PlayerPrefs.SetString(SnapshotKey, _runSnapshot); else PlayerPrefs.DeleteKey(SnapshotKey);
            if (_records != null) PlayerPrefs.SetString(RecordsKey, _records); else PlayerPrefs.DeleteKey(RecordsKey);
            PlayerPrefs.Save();
            DemoMode.Off = _demoOff;
        }
    }
}
