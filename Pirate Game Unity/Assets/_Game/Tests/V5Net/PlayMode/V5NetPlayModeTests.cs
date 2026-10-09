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
            if (NetVoyageClient.Instance != null) Object.Destroy(NetVoyageClient.Instance.gameObject);
            RunRequest.RemoteSimulation = false;
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

        /// <summary>V5 M9b part 2a: a server going down (docker stop, a Railway redeploy) sends end ServerClosing; the client
        /// does not try to resume and is back in the lobby state.</summary>
        [UnityTest]
        public IEnumerator ServerClosing_EndsTheSessionCleanly()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            yield return Join(client);
            yield return Seconds(1f);
            Assert.IsFalse(client.SessionOver, "sailing");
            yield return _server.CloseGracefully();
            yield return Until(() => client.SessionOver, 5f);
            Assert.IsTrue(client.Ended, "the client got end (" + client.LastDisconnectReason + ")");
            Assert.AreEqual(NetProtocol.EndReason.ServerClosing, client.EndReason);
            Assert.IsFalse(_server.Manager.IsListening, "the server stopped listening");
            yield return Seconds(1.5f);
            Assert.IsTrue(client.SessionOver, "back to the lobby");
            Assert.AreEqual(1, client.Welcomes, "no resume after the server's end");
        }

        /// <summary>V5 M9b part 2a: a server that dies without a word (a killed process): the client tries to resume for its
        /// window, then gives up and is back in the lobby state.</summary>
        [UnityTest]
        public IEnumerator ServerGone_ClientGivesUpAfterTheResumeWindow()
        {
            yield return StartVoyage("net_voyage_open_water");
            var client = NewClient(Ticket());
            client.ReconnectWindowSeconds = 2f;
            yield return Join(client);
            yield return Seconds(0.5f);
            _server.Manager.Shutdown(); // no end message
            yield return Seconds(1f);
            Assert.IsFalse(client.Connected);
            Assert.IsFalse(client.SessionOver, "still inside its resume window");
            yield return Until(() => client.SessionOver, 8f);
            Assert.IsTrue(client.SessionOver, "gave up after the window");
            Assert.IsFalse(client.Ended, "no end message came");
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

        /// <summary>
        /// V5 M9b part 2b: the boost is predicted. The boost steps on the net tick on both sides (held starts it when ready,
        /// its ease on the tick's clock, its timers rewound with the motion), so at 150 ms and 2% loss a boost costs no
        /// prediction error or correction above 0.5 m (part 1 measured a 0.67 m correction with the boost unpredicted).
        /// </summary>
        [UnityTest]
        public IEnumerator Boost_PredictedWithinHalfAMetre()
        {
            var link = new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 31 };
            yield return StartVoyage("net_voyage_open_water", link);
            var client = NewClient(Ticket(), new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 32 });
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            var boost = false;
            client.Boost = () => boost;
            yield return Join(client);
            yield return Seconds(2f);
            var cruise = _server.Ship.Capture().velocity.magnitude;
            client.Metrics.Reset();
            boost = true;
            yield return Seconds(0.5f);
            boost = false;
            bool serverBoosted = false, clientBoosted = false;
            var top = 0f;
            var until = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < until)
            {
                serverBoosted |= _server.Ship.IsBoosting;
                clientBoosted |= client.Predicted.IsBoosting;
                top = Mathf.Max(top, _server.Ship.Capture().velocity.magnitude);
                yield return null;
            }
            input.Value = new Vector2(0.6f, 1f); // a turn after the boost
            yield return Seconds(1.5f);
            Report("boost-150ms-2pct", _server.Metrics, client.Metrics);
            Assert.IsTrue(serverBoosted, "the server's ship boosted");
            Assert.IsTrue(clientBoosted, "the client predicted the boost");
            Assert.Greater(top, cruise * 1.2f, "the boost sped the ship up (cruise " + cruise + ", top " + top + ")");
            Assert.Greater(client.Metrics.PredictionError.Count, 60);
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.PredictionError), 0.5f, "prediction error at most 0.5 m through the boost");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.Correction), 0.5f, "no correction above 0.5 m through the boost");
        }

        // ---- Level-up cards (part 2b) ------------------------------------------------------------------------------------

        /// <summary>
        /// V5 M9b part 2b: a level-up on the server's run deals the real cards (the run's offer logic) to the client; the run
        /// pauses as a voyage does and the ship holds on both sides; the client's pick goes back, the server applies exactly
        /// that card and resumes, and the ship sails on in step.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelUp_RealCardsOverTheNetwork_PauseAndPick()
        {
            yield return StartVoyage("net_voyage_levelup");
            var client = NewClient(Ticket());
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            yield return Join(client);
            yield return Seconds(1.5f);
            var progression = _game.Build.Progression;
            var chosen = new List<string>();
            progression.OfferChosen += o => chosen.Add(o.Key);
            var level = progression.Level;
            progression.GrantLevelUp();
            Assert.AreEqual(level + 1, progression.Level);
            Assert.IsTrue(RunManager.Instance.IsPaused, "the server's run pauses on the cards, as a voyage does");
            Assert.IsTrue(_server.LevelUps.Pending);
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer, "the cards reached the client");
            var offer = client.PendingOffer;
            CollectionAssert.AreEqual(_server.LevelUps.Offers.ConvertAll(o => o.Key), offer.offers.ConvertAll(o => o.Key), "the server's real cards");
            Assert.Greater(offer.offers.Count, 1);
            yield return Seconds(0.5f);
            var held = _server.Ship.Position;
            yield return Seconds(1f);
            Assert.Less(Flat(_server.Ship.Position, held), 0.01f, "the ship holds while the cards are up (full throttle held)");
            Assert.Less(Flat(client.Predicted.Position, held), 0.05f, "and the client's prediction holds with it");
            yield return Until(() => client.Last.Paused, 1f);
            Assert.IsTrue(client.Last.Paused, "snapshots say the run is paused");

            client.Metrics.Reset();
            var index = offer.offers.Count - 1;
            Assert.IsTrue(client.SendPick(NetProtocol.PickAction.Pick, index));
            yield return Until(() => !RunManager.Instance.IsPaused, 3f);
            Assert.IsFalse(RunManager.Instance.IsPaused, "the pick resumes the run");
            CollectionAssert.AreEqual(new[] { offer.offers[index].Key }, chosen, "the server applied exactly the picked card");
            Assert.AreEqual(1, _server.LevelUps.Picked);
            yield return Until(() => client.PendingOffer == null && client.LastResolution.how == NetProtocol.OfferResolution.Picked, 2f);
            Assert.AreEqual(NetProtocol.OfferResolution.Picked, client.LastResolution.how);
            Assert.Greater(client.LastResolution.resumeTick, 0u);
            yield return Seconds(2f);
            Assert.Greater(Flat(_server.Ship.Position, held), 3f, "the ship sails again");
            Report("levelup-pick-clean", _server.Metrics, client.Metrics);
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.PredictionError), 0.5f, "in step after the pause");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.Correction), 0.5f);
            Assert.AreEqual(0, _server.RejectedPicks);
        }

        /// <summary>
        /// V5 M9b review (D9b.1, the level-up pull-back fix): a level-up at 150 ms and 2% loss, once sailing at full speed and
        /// once while boosting. The client names the tick it had reached when the cards arrived; the server applies its
        /// inputs up to there, then holds; a late pick's held ticks are caught up on the server. So the gate holds through
        /// the pause entry and the resume: no correction above 0.5 m in a frame and prediction error at most 0.5 m.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelUp_At150ms2Percent_NoCorrectionAboveHalfAMetre()
        {
            yield return StartVoyage("net_voyage_levelup", new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 41 });
            var client = NewClient(Ticket(), new NetLinkSimulator.Link { sendDelayMs = 75, sendDropPercent = 2, seed = 42 });
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            var boost = false;
            client.Boost = () => boost;
            yield return Join(client);
            yield return Seconds(2f);
            client.Metrics.Reset();
            _server.Metrics.Reset();
            var progression = _game.Build.Progression;
            var cruise = _server.Ship.Capture().velocity.magnitude;

            // 1. Sailing at full speed.
            progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer, "the cards reached the client");
            yield return Seconds(1f);
            var entrySailing = NetMetrics.Max(client.Metrics.Correction);
            var held = _server.Ship.Position;
            yield return Seconds(0.5f);
            Assert.Less(Flat(_server.Ship.Position, held), 0.01f, "the server's ship holds on the cards");
            Assert.Less(Flat(client.Predicted.Position, held), 0.05f, "where the client's ship holds");
            Assert.IsTrue(client.SendPick(NetProtocol.PickAction.Pick, 0));
            yield return Until(() => !RunManager.Instance.IsPaused, 3f);
            Assert.IsFalse(RunManager.Instance.IsPaused, "the pick resumes the run");
            yield return Seconds(2f);
            var afterSailing = NetMetrics.Max(client.Metrics.Correction);

            // 2. While boosting.
            boost = true;
            yield return Until(() => _server.Ship.IsBoosting, 3f);
            boost = false;
            Assert.IsTrue(_server.Ship.IsBoosting, "the server's ship boosts");
            yield return Until(() => _server.Ship.Capture().velocity.magnitude > cruise * 1.15f || !_server.Ship.IsBoosting, 2f);
            var boostSpeed = _server.Ship.Capture().velocity.magnitude;
            var boostingAtLevelUp = _server.Ship.IsBoosting;
            progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer, "the second cards reached the client");
            yield return Seconds(1f);
            Assert.IsTrue(client.SendPick(NetProtocol.PickAction.Pick, 0));
            yield return Until(() => !RunManager.Instance.IsPaused, 3f);
            yield return Seconds(3f);

            Report("levelup-150ms-2pct", _server.Metrics, client.Metrics);
            var corrMax = NetMetrics.Max(client.Metrics.Correction);
            var errMax = NetMetrics.Max(client.Metrics.PredictionError);
            Debug.Log($"[V5 Net] level-up at 150 ms / 2%: cruise {cruise:0.0} m/s, boost {boostSpeed:0.0} m/s (boosting at the level-up: {boostingAtLevelUp}); " +
                      $"correction max: sailing entry {entrySailing:0.000} m, through the sailing level-up {afterSailing:0.000} m, overall {corrMax:0.000} m; " +
                      $"prediction error max {errMax:0.000} m; holds named {_server.LevelUps.HoldsNamed} (clamped {_server.LevelUps.HoldsClamped}, timed out {_server.LevelUps.HoldsTimedOut}), " +
                      $"late holds rewound {_server.LevelUps.HoldsRewound}, late picks caught up {_server.LevelUps.LatePicksCaughtUp}; compared {client.Metrics.PredictionError.Count}");
            Assert.IsTrue(boostingAtLevelUp, "the second level-up came while boosting");
            Assert.Greater(boostSpeed, cruise * 1.1f, "the boost was on (cruise " + cruise + ", boost " + boostSpeed + ")");
            Assert.AreEqual(2, client.HoldsSent, "the client named a hold tick for each set");
            Assert.AreEqual(2, _server.LevelUps.Picked);
            Assert.Greater(client.Metrics.PredictionError.Count, 60);
            Assert.LessOrEqual(corrMax, 0.5f, "D9b.1: no correction above 0.5 m in a frame, through both level-ups");
            Assert.LessOrEqual(errMax, 0.5f, "D9b.1: prediction error at most 0.5 m");
        }

        /// <summary>
        /// V5 M9b review (D9b.1): the hold and the pick arrive 0.3 s late (as a lost packet resent would). The late hold
        /// rewinds the server's ship to the client's hold tick, the late pick steps the held ticks on the inputs received,
        /// and the client does not correct against the snapshots sent before the server had them: no correction above 0.5 m.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelUp_LateHoldAndLatePick_StayExact()
        {
            yield return StartVoyage("net_voyage_levelup");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            yield return Join(client);
            yield return Seconds(2f);
            client.Metrics.Reset();
            client.DelayLevelUpMessagesForTest = 0.3f;
            _game.Build.Progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer);
            yield return Seconds(1f);
            var held = _server.Ship.Position;
            Assert.Less(Flat(client.Predicted.Position, held), 0.05f, "the server's ship went back to where the client holds");
            Assert.IsTrue(client.SendPick(NetProtocol.PickAction.Pick, 0));
            yield return Until(() => !RunManager.Instance.IsPaused, 3f);
            Assert.IsFalse(RunManager.Instance.IsPaused);
            yield return Seconds(2f);
            Report("levelup-late-messages", _server.Metrics, client.Metrics);
            Assert.AreEqual(1, _server.LevelUps.HoldsRewound, "the late hold rewound the server's ship");
            Assert.AreEqual(1, _server.LevelUps.LatePicksCaughtUp, "the late pick's held ticks were caught up");
            Assert.Greater(client.SkippedSnapshots, 0, "snapshots from before the server had them were not used");
            Assert.Greater(Flat(_server.Ship.Position, held), 10f, "sailing again");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.Correction), 0.5f, "no correction above 0.5 m");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.PredictionError), 0.5f, "prediction error at most 0.5 m");
        }

        /// <summary>
        /// V5 M9b review (fix 1): a resume while the cards are up. Cards still up after the resume come back with their
        /// hold; cards the server resolved while the socket was down (its 30 s timeout, shortened by the test hook) leave
        /// no stale cards or hold on the client: it sails on.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelUp_ResumeDuringCards_NoStaleCardsAndSailsOn()
        {
            yield return StartVoyage("net_voyage_levelup");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            var resolved = new List<NetProtocol.OfferResolution>();
            client.OfferResolved += m => resolved.Add(m.how);
            yield return Join(client);
            yield return Seconds(1.5f);

            // Cards up across a resume: they come back, and the ship stays held on both sides.
            _game.Build.Progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer);
            var seq = client.PendingOffer.seq;
            yield return Seconds(0.5f);
            _server.DropConnectionForTest();
            yield return Until(() => client.Welcomes >= 2 && client.PendingOffer != null, 5f);
            Assert.AreEqual(2, client.Welcomes, "resumed");
            Assert.IsNotNull(client.PendingOffer, "the cards still up are sent again");
            Assert.AreEqual(seq, client.PendingOffer.seq);
            CollectionAssert.Contains(resolved, NetProtocol.OfferResolution.Cancelled, "the welcome cleared the old cards first");
            var held = _server.Ship.Position;
            yield return Seconds(1f);
            Assert.Less(Flat(_server.Ship.Position, held), 0.01f, "still held on the server");
            Assert.Less(Flat(client.Predicted.Position, held), 0.05f, "and on the client");

            // The cards time out on the server while the socket is down (the 30 s timeout, shortened here).
            _server.DropConnectionForTest();
            yield return Until(() => !_server.Current.connected, 2f);
            Assert.IsFalse(_server.Current.connected);
            _server.LevelUps.ExpireForTest();
            yield return Until(() => !_server.LevelUps.Pending, 2f);
            Assert.AreEqual(1, _server.LevelUps.TimedOut, "the server took the first card");
            yield return Until(() => client.Welcomes >= 3 && client.Connected, 5f);
            Assert.AreEqual(3, client.Welcomes, "resumed again");
            Assert.AreNotEqual(NetProtocol.OfferResolution.TimedOut, client.LastResolution.how, "the client never heard of the timeout (its socket was down)");
            yield return Seconds(0.5f);
            Assert.IsNull(client.PendingOffer, "no stale cards after the resume");
            client.Metrics.Reset();
            var serverAt = _server.Ship.Position;
            var clientAt = client.Predicted.Position;
            yield return Seconds(2f);
            Assert.Greater(Flat(_server.Ship.Position, serverAt), 3f, "the server's ship sails");
            Assert.Greater(Flat(client.Predicted.Position, clientAt), 3f, "the client sails again (no frozen hold)");
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 2f, "in step with the server");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.PredictionError), 0.5f, "prediction back within 0.5 m");
        }

        /// <summary>V5 M9b review (fix 6): a player who joins and never sends an input is dropped after the timeout (30 s on
        /// the match server; 1 s here): <c>end</c> SessionExpired, the socket let go, and the voyage is free for another
        /// ticket.</summary>
        [UnityTest]
        public IEnumerator JoinWithoutInputs_IsDroppedAndTheVoyageFreed()
        {
            yield return StartVoyage("net_voyage_open_water");
            var host = _server.gameObject.AddComponent<NetServerHost>();
            host.NoInputTimeoutSeconds = 1f;
            host.Begin(_server, _port, false);
            var silent = NetClient.Create(NetEndpoint.Parse($"ws://127.0.0.1:{_port}/"), Ticket(), () => null, "V5NetSilentClient"); // no ship: never ticks
            _clients.Add(silent);
            yield return Join(silent);
            yield return Until(() => silent.Ended, 5f);
            Assert.IsTrue(silent.Ended, "the server ended the silent session");
            Assert.AreEqual(NetProtocol.EndReason.SessionExpired, silent.EndReason);
            Assert.IsTrue(_server.Current.expired);
            yield return Until(() => !_server.Current.connected, 3f);
            Assert.IsFalse(_server.Current.connected, "its socket was let go");
            Object.Destroy(host); // the match server's host would now exit for a fresh process; here the same server takes the next ticket
            yield return null;
            var next = NewClient(Ticket("player-2"));
            next.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            yield return Join(next);
            Assert.AreEqual("player-2", _server.Current.playerId, "the voyage is free for another player");
        }

        /// <summary>V5 M9b review (fix 3): <see cref="RunRequest.RemoteSimulation"/> never outlives the net client's stage. A
        /// stage loaded with it on does not tick or record; the server voyage's client going away and every main menu load
        /// turn it off; the voyage after that ticks and records its run log.</summary>
        [UnityTest]
        public IEnumerator RemoteSimulationStage_ThenANormalVoyage_TicksAndRecords()
        {
            RunRequest.RemoteSimulation = true;
            RunRequest.ClearPvp();
            RunRequest.RequestSeed(424242);
            yield return SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            V4Game stage = null;
            yield return Until(() => (stage = Object.FindFirstObjectByType<V4Game>()) != null && stage.IsRunning, 30f);
            Assert.IsNotNull(stage);
            Assert.IsTrue(stage.IsRunning, "the net client's stage is built");
            Assert.IsNull(stage.RunLog, "a net client's stage records no run log");
            yield return Seconds(1f);
            Assert.IsFalse(FrameStepsRan(stage), "and its frame steps do not tick (the server runs them)");

            var voyage = NetVoyageClient.Begin();
            voyage.enabled = false; // no Start: no API call; only its life and end matter here
            Object.Destroy(voyage.gameObject);
            yield return null;
            Assert.IsFalse(RunRequest.RemoteSimulation, "the voyage client going away turns it off");

            RunRequest.RemoteSimulation = true;
            yield return SceneManager.LoadSceneAsync(V4SceneNames.MainMenu, LoadSceneMode.Single);
            yield return null;
            Assert.IsFalse(RunRequest.RemoteSimulation, "the main menu turns it off");

            yield return Scenario.Load("net_voyage_open_water");
            var game = Scenario.Current.Game;
            Assert.IsNotNull(game.RunLog, "the next voyage records its run log");
            yield return Seconds(1f);
            Assert.IsTrue(FrameStepsRan(game), "and its frame steps tick");
        }

        static bool FrameStepsRan(V4Game game)
        {
            foreach (FrameStep step in System.Enum.GetValues(typeof(FrameStep)))
                if (game.Frame.Has(step) && game.Frame.FrameAtTick(step) >= 0) return true;
            return false;
        }

        /// <summary>V5 M9b part 2b: picks that do not match the cards on the table are refused and change nothing (a stale set,
        /// a card that is not there, a refresh with none left); a set nobody answers takes the first card after the
        /// timeout, and the client hears so and sails on.</summary>
        [UnityTest]
        public IEnumerator LevelUp_BadPicksRefused_TimeoutTakesTheFirstCard()
        {
            yield return StartVoyage("net_voyage_levelup");
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            yield return Join(client);
            yield return Seconds(1f);
            var progression = _game.Build.Progression;
            var chosen = new List<string>();
            progression.OfferChosen += o => chosen.Add(o.Key);
            progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            var offer = client.PendingOffer;
            Assert.IsNotNull(offer);
            client.SendRawPickForTest((ushort)(offer.seq + 7), NetProtocol.PickAction.Pick, 0);
            client.SendRawPickForTest(offer.seq, NetProtocol.PickAction.Pick, 40);
            var expected = 2;
            if (_server.LevelUps.RefreshesRemaining == 0) { client.SendRawPickForTest(offer.seq, NetProtocol.PickAction.Refresh, 0); expected++; }
            yield return Until(() => _server.RejectedPicks >= expected, 3f);
            Assert.AreEqual(expected, _server.RejectedPicks, "each bad pick refused (" + _server.LevelUps.LastRejectReason + ")");
            CollectionAssert.IsEmpty(chosen, "nothing applied");
            Assert.IsTrue(RunManager.Instance.IsPaused, "still on the cards");
            Assert.IsTrue(_server.LevelUps.Pending);

            _server.LevelUps.ExpireForTest();
            yield return Until(() => !RunManager.Instance.IsPaused, 2f);
            Assert.AreEqual(1, _server.LevelUps.TimedOut);
            CollectionAssert.AreEqual(new[] { offer.offers[0].Key }, chosen, "the default pick is the first card");
            yield return Until(() => client.LastResolution.how == NetProtocol.OfferResolution.TimedOut, 2f);
            Assert.AreEqual(NetProtocol.OfferResolution.TimedOut, client.LastResolution.how);
            Assert.IsNull(client.PendingOffer);
            var at = _server.Ship.Position;
            yield return Seconds(1.5f);
            Assert.Greater(Flat(_server.Ship.Position, at), 2f, "sailing again after the timeout");
            Assert.Less(Flat(client.Predicted.Position, _server.Ship.Position), 5f, "the client follows");
        }

        /// <summary>
        /// M9b playtest fixes (D, the owner's hidden tab, 9 Oct): the cards are up, the player switches tabs and the page
        /// runs about once a second (Chrome's hidden tab; the socket stays open and nothing tells the server), the server's
        /// 30 s card timeout takes the first card while the client is not answering. Once the page is active again the
        /// client drops the cards and the hold, takes the server's resume tick, moves its tick line ahead of the server's
        /// (its inputs arrive before their tick again) and the ship answers the keys. At 135 ms each way (the owner's
        /// 270 ms round trip from Asia). Before the fix the client stayed behind the server's tick line for good: every
        /// input arrived late, the server repeated the last one, and the ship sailed on by itself until it sank.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelUp_TimedOutWhileTheTabWasHidden_ClientSailsAgain()
        {
            yield return StartVoyage("net_voyage_levelup", new NetLinkSimulator.Link { sendDelayMs = 135, seed = 51 });
            var client = NewClient(Ticket(), new NetLinkSimulator.Link { sendDelayMs = 135, seed = 52 });
            var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            client.Input = input;
            yield return Join(client);
            yield return Seconds(2f);
            _game.Build.Progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer, "the cards reached the client");
            yield return Seconds(0.5f);

            client.ThrottleForTest(35f); // the tab is hidden: one frame a second, no pick
            yield return Until(() => _server.LevelUps.TimedOut > 0, 40f);
            Assert.AreEqual(1, _server.LevelUps.TimedOut, "the server's 30 s card timeout took the first card");
            Assert.IsTrue(_server.Current.connected, "the socket stayed open");
            yield return Until(() => !client.Throttled, 10f);
            var activeAt = Time.realtimeSinceStartup;
            input.Value = new Vector2(1f, 1f); // the player steers at once

            // Within one snapshot of being active again: no cards, no hold, and its tick ahead of the server's next tick.
            float controlAt = -1f;
            var until = activeAt + 3f;
            while (Time.realtimeSinceStartup < until && controlAt < 0f)
            {
                if (client.PendingOffer == null && !client.Holds(client.Tick + 1) && client.Tick >= _server.Current.expectNext) controlAt = Time.realtimeSinceStartup;
                else if (Time.frameCount % 10 == 0)
                    Debug.Log($"[V5 Net] not yet in control: offer {client.PendingOffer != null}, holds {client.Holds(client.Tick + 1)}, tick {client.Tick}, server next {_server.Current.expectNext}, ack {client.LastAck}");
                yield return null;
            }
            var controlSeconds = controlAt < 0f ? -1f : controlAt - activeAt;
            Debug.Log($"[V5 Net] hidden-tab timeout: control back {controlSeconds:0.000} s after the page was active; resyncs {client.Resyncs}, " +
                      $"late inputs {_server.Metrics.LateInputs}, guessed {_server.Metrics.GuessedInputs}");
            Assert.AreEqual(NetProtocol.OfferResolution.TimedOut, client.LastResolution.how, "the client heard of the timeout");
            Assert.IsNull(client.PendingOffer, "no cards left up");
            Assert.GreaterOrEqual(controlAt, 0f, "the client took control again");
            Assert.LessOrEqual(controlSeconds, NetProtocol.SnapshotEveryTicks * NetProtocol.TickSeconds + 0.05f, "within one snapshot of the page being active");

            // The server applies the client's own inputs again (none guessed) and steers as it does.
            yield return Seconds(1.5f);
            var guessed = _server.Metrics.GuessedInputs;
            var ack = client.LastAck;
            var resyncs = client.Resyncs;
            yield return Seconds(1f);
            Assert.Greater(client.LastAck, ack + 20, "the acks move on");
            Assert.LessOrEqual(_server.Metrics.GuessedInputs - guessed, 2, $"the client's inputs arrive in time (nothing guessed; resyncs meanwhile {client.Resyncs - resyncs}, late {client.LateResyncs})");
            Assert.Less(Mathf.Abs(_server.Ship.Capture().lastSteer - NetInput.From(input.Value, false).SteerThrottle.x), 0.01f, "the server's ship answers the keys");
            client.Metrics.Reset();
            yield return Seconds(1.5f);
            Assert.Less(NetMetrics.Percentile(client.Metrics.PredictionError, 95f), 0.5f, "the prediction is back on the server's ship");
            // The lead taken to catch up is given back to what the link needs (a 270 ms round trip and a little).
            yield return Seconds(4f);
            client.Metrics.Reset();
            yield return Seconds(1.5f);
            var lag = NetMetrics.Percentile(client.Metrics.InputLagMs, 50f);
            Debug.Log($"[V5 Net] hidden-tab timeout: input lag p50 {lag:0} ms after {client.LeadTrims} lead trims");
            Assert.LessOrEqual(lag, 270f + 4 * 1000f * NetProtocol.TickSeconds + 1f, "no more input delay than the round trip and the jitter cushion");
            Report("hidden-tab-card-timeout-270ms", _server.Metrics, client.Metrics);
        }

        // ---- M9b playtest fixes (9 Oct): smoothness, teleports, away --------------------------------------------------------

        static readonly NetLinkSimulator.Link Asia = new NetLinkSimulator.Link { sendDelayMs = 135, sendJitterMs = 15, sendDropPercent = 1, seed = 61 };
        static NetLinkSimulator.Link AsiaClient => new NetLinkSimulator.Link { sendDelayMs = 135, sendJitterMs = 15, sendDropPercent = 1, seed = 62 };

        int _targetFrameRate, _vSync;

        void SixtyFps()
        {
            _targetFrameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        void RestoreFps()
        {
            Application.targetFrameRate = _targetFrameRate;
            QualitySettings.vSyncCount = _vSync;
        }

        static LateProbe Probe(System.Action late)
        {
            var probe = new GameObject("V5NetLateProbe").AddComponent<LateProbe>();
            probe.Late = late;
            return probe;
        }

        /// <summary>The run scene's top-down camera (25 m up, 20 m behind, looking at the ship, 50 degree vertical field of
        /// view) at 16:9: whether <paramref name="p"/> is in its view of the sea around the drawn ship.</summary>
        static bool OnScreen(Vector3 ship, Vector3 p)
        {
            var cam = new Vector3(0f, 25f, -20f);
            var look = Quaternion.LookRotation(-cam, Vector3.up);
            var local = Quaternion.Inverse(look) * (new Vector3(p.x - ship.x, 0f, p.z - ship.z) - cam);
            if (local.z <= 0f) return false;
            var tan = Mathf.Tan(25f * Mathf.Deg2Rad);
            return Mathf.Abs(local.y / local.z) <= tan && Mathf.Abs(local.x / local.z) <= tan * 16f / 9f;
        }

        /// <summary>
        /// M9b playtest fixes (A, the owner's "stuttery"): sailing straight at full speed at a 270 ms round trip and 60 fps,
        /// the ship drawn at its stepped pose (the drawing before the fix: the 30 Hz net tick's pose, which the camera
        /// followed) moved on every other frame; drawn between its last two ticks it moves every frame. Measured on the same
        /// frames: the per-frame speed's jitter (how much it changes from frame to frame) and its variation.
        /// </summary>
        [UnityTest]
        public IEnumerator DrawnShip_MovesEveryFrame_Measured()
        {
            SixtyFps();
            try
            {
                yield return StartVoyage("net_voyage_open_water", Asia);
                var client = NewClient(Ticket(), AsiaClient);
                client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
                yield return Join(client);
                yield return Seconds(4f); // up to cruising speed
                var stepped = new NetSmoothness();
                var drawn = new NetSmoothness();
                var lagMax = 0f;
                var probe = Probe(() =>
                {
                    var dt = Time.unscaledDeltaTime;
                    stepped.BeginFrame();
                    drawn.BeginFrame();
                    stepped.Ship(client.Predicted.Position, dt);
                    drawn.Ship(client.Predicted.Root.position, dt);
                    lagMax = Mathf.Max(lagMax, Flat(client.Predicted.Root.position, client.Predicted.Position));
                });
                yield return Seconds(4f);
                Object.Destroy(probe.gameObject);
                var speed = NetSmoothness.Mean(drawn.ShipSpeed);
                var fps = drawn.Frames / 4f;
                var context = new Dictionary<string, string>
                {
                    ["frames"] = drawn.Frames.ToString(),
                    ["fps"] = fps.ToString("0.0"),
                    ["shipSpeedMps"] = speed.ToString("0.00"),
                    ["before_shipJitterMeanMps"] = NetSmoothness.Mean(stepped.ShipJitter).ToString("0.000"),
                    ["before_shipJitterP95Mps"] = NetMetrics.Percentile(stepped.ShipJitter, 95f).ToString("0.000"),
                    ["before_shipSpeedVariation"] = NetSmoothness.Variation(stepped.ShipSpeed).ToString("0.000"),
                    ["after_shipJitterMeanMps"] = NetSmoothness.Mean(drawn.ShipJitter).ToString("0.000"),
                    ["after_shipJitterP95Mps"] = NetMetrics.Percentile(drawn.ShipJitter, 95f).ToString("0.000"),
                    ["after_shipSpeedVariation"] = NetSmoothness.Variation(drawn.ShipSpeed).ToString("0.000"),
                    ["before_shipStallFrames"] = stepped.ShipStallFrames.ToString(),
                    ["after_shipStallFrames"] = drawn.ShipStallFrames.ToString(),
                    ["drawnBehindSteppedMaxM"] = lagMax.ToString("0.000"),
                    ["link"] = "135 ms each way, 15 ms jitter, 1% loss",
                };
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Net", "drawn-ship-smoothness.json"));
                Debug.Log("[V5 Net] drawn ship: " + NetMetrics.WriteReport(path, "drawn-ship-smoothness", context, client.Metrics));
                Assert.Greater(drawn.Frames, 150, "frames measured");
                Assert.Greater(speed, 8f, "sailing at speed");
                Assert.Greater(stepped.ShipStallFrames, drawn.Frames / 4, "drawn at its stepped pose the ship stood still on about every other frame (the stutter)");
                Assert.AreEqual(0, drawn.ShipStallFrames, "drawn between its ticks it moves on every frame");
                Assert.Less(NetSmoothness.Mean(drawn.ShipJitter), 0.1f * NetSmoothness.Mean(stepped.ShipJitter), "per-frame jitter down by over 90%");
                Assert.Less(NetSmoothness.Mean(drawn.ShipJitter), 0.05f * speed, "under 5% of its speed");
                Assert.LessOrEqual(lagMax, speed * NetProtocol.TickSeconds * 1.6f + 0.05f, "drawn at most about a tick behind its stepped pose");
            }
            finally { RestoreFps(); }
        }

        /// <summary>
        /// M9b playtest fixes (B, C, the owner's "enemies stuttery" and "teleports"): a busy voyage (10:00, ambient spawns)
        /// at a 270 ms round trip, 15 ms jitter and 1% loss, 60 fps, sailing S-turns at full speed so enemies fall behind and
        /// the server's leash relocates them. Every frame each mirrored enemy is drawn both ways from the same snapshots:
        /// before (between its own last two snapshots by the time since the newest snapshot, over one interval) and after
        /// (the render-time buffer). On screen (the top-down camera's view of the sea around the drawn ship), no enemy may
        /// move faster than any enemy sails unless the server relocated it then, and a relocation must be one frame (drawn
        /// at once), never a slide over several.
        /// </summary>
        [UnityTest]
        public IEnumerator Enemies_DrawnSmoothly_NoTeleportsOnScreen_Measured()
        {
            SixtyFps();
            try
            {
                yield return StartVoyage("net_voyage_busy", Asia);
                var client = NewClient(Ticket(), AsiaClient);
                var input = new ScriptedInput { Value = new Vector2(0f, 1f) };
                client.Input = input;
                yield return Join(client);
                _game.Context.TryGet(out RoguePirates.V4.Enemies.IEnemyWorldView world);
                Assert.IsNotNull(world, "the server's enemy world");
                yield return Seconds(3f);

                var before = new NetSmoothness();
                var after = new NetSmoothness();
                var interval = NetProtocol.SnapshotEveryTicks * NetProtocol.TickSeconds;
                uint lastComplete = 0;
                var completeAt = 0f;
                var serverLast = new Dictionary<int, Vector3>();
                var relocatedAt = new Dictionary<int, float>();
                var lastDrawn = new Dictionary<int, (Vector3 p, int frame, bool onScreen, bool fast)>();
                int frame = 0, relocations = 0, unexplainedFast = 0, snaps = 0, slides = 0, onScreenSamples = 0;
                var probe = Probe(() =>
                {
                    frame++;
                    var dt = Time.unscaledDeltaTime;
                    var now = Time.realtimeSinceStartup;
                    // The server's truth: a unit that moved faster than any enemy sails since the last frame was relocated.
                    for (var i = 0; i < world.Capacity; i++)
                    {
                        if (!world.TryGet(i, out var e)) continue;
                        if (serverLast.TryGetValue(e.unitId, out var was))
                        {
                            var limit = NetEnemyMirror.MaxEnemySpeed * Mathf.Max(dt, NetProtocol.TickSeconds) * 2f + NetEnemyMirror.JumpSlackMetres;
                            if (Flat(e.position, was) > limit) { relocatedAt[e.unitId] = now; relocations++; }
                        }
                        serverLast[e.unitId] = e.position;
                    }
                    var mirror = client.Enemies;
                    if (mirror.LastCompleteTick != lastComplete) { lastComplete = mirror.LastCompleteTick; completeAt = Time.unscaledTime; }
                    var legacyT = Mathf.Clamp01((Time.unscaledTime - completeAt) / interval);
                    var renderTick = mirror.RenderTick(NetFrameClock.Now);
                    var ship = client.RenderPosition;
                    before.BeginFrame();
                    after.BeginFrame();
                    foreach (var e in mirror.All)
                    {
                        var old = NetEnemyMirror.Interpolate(e, legacyT);
                        before.Enemy(e.unitId, old, dt, OnScreen(ship, old));
                        if (!e.track.Sample(renderTick, out var p, out _, out _)) continue;
                        var onScreen = OnScreen(ship, p);
                        after.Enemy(e.unitId, p, dt, onScreen);
                        if (onScreen) onScreenSamples++;
                        var fast = false;
                        if (lastDrawn.TryGetValue(e.unitId, out var last) && last.frame == frame - 1 && last.onScreen && onScreen)
                        {
                            fast = Flat(p, last.p) / dt > NetEnemyMirror.MaxEnemySpeed;
                            if (fast)
                            {
                                var relocated = relocatedAt.TryGetValue(e.unitId, out var at) && now - at < 1.5f;
                                if (!relocated) unexplainedFast++;
                                else if (last.fast) slides++;
                                else snaps++;
                            }
                        }
                        lastDrawn[e.unitId] = (p, frame, onScreen, fast);
                    }
                });
                var t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    var t = Time.realtimeSinceStartup - t0;
                    input.Value = new Vector2(Mathf.Sin(t * 0.35f) * 0.7f, 1f);
                    yield return null;
                }
                Object.Destroy(probe.gameObject);
                var context = new Dictionary<string, string>
                {
                    ["frames"] = frame.ToString(),
                    ["fps"] = (frame / 20f).ToString("0.0"),
                    ["onScreenEnemySamples"] = onScreenSamples.ToString(),
                    ["before_enemyJitterMeanMps"] = NetSmoothness.Mean(before.EnemyJitter).ToString("0.000"),
                    ["before_enemyJitterP95Mps"] = NetMetrics.Percentile(before.EnemyJitter, 95f).ToString("0.000"),
                    ["before_enemyFastFramesOnScreen"] = before.EnemyFastFrames.ToString(),
                    ["before_enemySlideFramesOnScreen"] = before.EnemySlideFrames.ToString(),
                    ["before_enemyStallFramesOnScreen"] = before.EnemyStallFrames.ToString(),
                    ["after_enemyStallFramesOnScreen"] = after.EnemyStallFrames.ToString(),
                    ["after_enemyJitterMeanMps"] = NetSmoothness.Mean(after.EnemyJitter).ToString("0.000"),
                    ["after_enemyJitterP95Mps"] = NetMetrics.Percentile(after.EnemyJitter, 95f).ToString("0.000"),
                    ["after_enemyFastFramesOnScreen"] = after.EnemyFastFrames.ToString(),
                    ["after_enemySlideFramesOnScreen"] = after.EnemySlideFrames.ToString(),
                    ["after_unexplainedFastFramesOnScreen"] = unexplainedFast.ToString(),
                    ["after_relocationsDrawnAtOnceOnScreen"] = snaps.ToString(),
                    ["after_relocationSlideFramesOnScreen"] = slides.ToString(),
                    ["serverRelocations"] = relocations.ToString(),
                    ["mirrorJumpsMarked"] = client.Enemies.Jumps.ToString(),
                    ["renderDelayMs"] = (client.Enemies.DelaySeconds * 1000f).ToString("0"),
                    ["enemiesMirroredAtEnd"] = client.Enemies.Count.ToString(),
                    ["link"] = "135 ms each way, 15 ms jitter, 1% loss",
                };
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "V5Net", "enemy-smoothness.json"));
                Debug.Log("[V5 Net] enemies: " + NetMetrics.WriteReport(path, "enemy-smoothness", context, _server.Metrics, client.Metrics));
                Assert.Greater(onScreenSamples, 1000, "enemies were on screen");
                Assert.AreEqual(0, unexplainedFast, "no on-screen enemy moved faster than any enemy sails unless the server relocated it");
                Assert.AreEqual(0, slides, "a relocation is drawn at once, never slid across the screen");
                Assert.AreEqual(0, after.EnemySlideFrames);
                Assert.Less(NetSmoothness.Mean(after.EnemyJitter), 0.5f * NetSmoothness.Mean(before.EnemyJitter), "per-frame enemy jitter at least halved");
                Assert.Less(NetMetrics.Percentile(after.EnemyJitter, 95f), 0.5f * NetMetrics.Percentile(before.EnemyJitter, 95f), "and its p95");
                Assert.LessOrEqual(after.EnemyStallFrames, before.EnemyStallFrames / 5, "on-screen enemies stand still for a frame at least 5x less often");
            }
            finally { RestoreFps(); }
        }

        /// <summary>
        /// M9b playtest fixes (E): a hidden tab during a solo voyage's cards. The client says it is away and stops; the
        /// server holds the run (its clock, the enemies, the ship) and the card timeout (3 s here) while it is away (5 s);
        /// back, a welcome resyncs the ship, the cards come back, the pick resumes the voyage and the prediction is in step.
        /// </summary>
        [UnityTest]
        public IEnumerator Away_HoldsTheSoloVoyageAndItsCards_BackResyncs()
        {
            yield return StartVoyage("net_voyage_levelup", Asia);
            var client = NewClient(Ticket(), AsiaClient);
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            var away = false;
            client.AwaySource = () => away;
            yield return Join(client);
            yield return Seconds(1.5f);
            _server.LevelUps.TimeoutSeconds = 3f;
            _game.Build.Progression.GrantLevelUp();
            yield return Until(() => client.PendingOffer != null, 3f);
            Assert.IsNotNull(client.PendingOffer);
            var seq = client.PendingOffer.seq;
            yield return Seconds(0.5f);

            away = true;
            yield return Until(() => _server.Current.away, 2f);
            Assert.IsTrue(_server.Current.away, "the server heard the page is hidden");
            Assert.AreEqual(0f, Time.timeScale, "the solo run holds");
            yield return Seconds(0.5f);
            var runTime = RunManager.Instance.ElapsedSeconds;
            var serverAt = _server.Ship.Position;
            var clientTick = client.Tick;
            var snapshots = client.Snapshots;
            yield return Seconds(5f);
            Assert.AreEqual(runTime, RunManager.Instance.ElapsedSeconds, 1e-4f, "the run's clock stood still");
            Assert.Less(Flat(_server.Ship.Position, serverAt), 0.01f, "the ship held");
            Assert.AreEqual(0, _server.LevelUps.TimedOut, "the 3 s card timeout waited (5 s away)");
            Assert.IsTrue(_server.LevelUps.Pending);
            Assert.AreEqual(clientTick, client.Tick, "the away client does not tick");
            Assert.LessOrEqual(client.Snapshots - snapshots, 1, "no snapshots while away");

            away = false;
            var backAt = Time.realtimeSinceStartup;
            var welcomes = client.Welcomes;
            yield return Until(() => client.Welcomes > welcomes && client.PendingOffer != null, 3f);
            var resync = Time.realtimeSinceStartup - backAt;
            Assert.Greater(client.Welcomes, welcomes, "back: a welcome resynced the ship");
            Assert.IsFalse(_server.Current.away);
            Assert.AreEqual(1f, Time.timeScale, "the run plays again");
            Assert.IsNotNull(client.PendingOffer, "the cards came back");
            Assert.AreEqual(seq, client.PendingOffer.seq);
            Assert.Less(resync, 1f, "within a round trip and a frame or two");
            client.Metrics.Reset();
            Assert.IsTrue(client.SendPick(NetProtocol.PickAction.Pick, 0));
            yield return Until(() => !RunManager.Instance.IsPaused, 3f);
            Assert.AreEqual(1, _server.LevelUps.Picked);
            var at = _server.Ship.Position;
            yield return Seconds(2f);
            Assert.Greater(Flat(_server.Ship.Position, at), 5f, "sailing again");
            Assert.LessOrEqual(NetMetrics.Max(client.Metrics.PredictionError), 0.5f, "in step after the away");
            Debug.Log($"[V5 Net] away 5 s during cards: back to the welcome {resync:0.000} s; aways {_server.AwaysReceived}, backs {_server.BacksReceived}");
        }

        /// <summary>
        /// M9b playtest fixes (E): a hidden tab can lose its socket (Chrome slows a hidden page's timers to once a minute
        /// after 5 minutes, and the server counts a socket silent for 5 s as gone). An away session is kept past the 30 s
        /// resume grace (1 s here); back, the client reconnects with its resume token and sails on. Away past the limit
        /// (10 minutes; 2 s here) the server ends the voyage cleanly: end Abandoned.
        /// </summary>
        [UnityTest]
        public IEnumerator Away_SocketLostWhileAway_Resumes_AndTheAwayLimitAbandons()
        {
            yield return StartVoyage("net_voyage_open_water");
            _server.ResumeGraceSeconds = 1f;
            var client = NewClient(Ticket());
            client.Input = new ScriptedInput { Value = new Vector2(0f, 1f) };
            var away = false;
            client.AwaySource = () => away;
            yield return Join(client);
            yield return Seconds(1f);
            away = true;
            yield return Until(() => _server.Current.away, 2f);
            _server.DropConnectionForTest();
            yield return Until(() => !client.Connected, 3f);
            yield return Seconds(2.5f);
            Assert.IsFalse(_server.Current.expired, "the away session outlives the resume grace");
            Assert.IsFalse(client.SessionOver, "and the away client does not give up");
            Assert.IsFalse(client.Connected, "nor reconnect while hidden");
            away = false;
            yield return Until(() => client.Connected && client.Welcomes >= 2, 10f);
            Assert.AreEqual(2, client.Welcomes, "back: resumed with the token");
            Assert.IsFalse(_server.Current.away, "present again");
            var at = _server.Ship.Position;
            yield return Seconds(1.5f);
            Assert.Greater(Flat(_server.Ship.Position, at), 3f, "sailing again");

            _server.AwayMaxSeconds = 2f;
            away = true;
            yield return Until(() => client.Ended, 6f);
            Assert.IsTrue(client.Ended, "away past the limit ends the voyage");
            Assert.AreEqual(NetProtocol.EndReason.Abandoned, client.EndReason);
            Assert.IsTrue(_server.Current.expired);
            Assert.AreEqual(1f, Time.timeScale, "the hold is released");
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

    /// <summary>M9b playtest fixes: runs a measurement late in the frame, after the ship and the enemies are drawn.</summary>
    [DefaultExecutionOrder(1000)]
    sealed class LateProbe : MonoBehaviour
    {
        public System.Action Late;
        void LateUpdate() => Late?.Invoke();
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
