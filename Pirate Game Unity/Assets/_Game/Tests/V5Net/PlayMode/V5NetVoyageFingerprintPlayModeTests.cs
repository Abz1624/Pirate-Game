#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using RoguePirates.V4;
using RoguePirates.V4.DevTools.Scenarios;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V5.Net.Tests
{
    /// <summary>
    /// V5 M9c S0, case (c): the solo server voyage golden. The headless server run (the shipped run at 30 Hz, no
    /// presenters) and a <see cref="NetClient"/> over loopback WebSockets in one Editor, the client sailing a fixed input
    /// track (a constant steer and throttle, so an input the server has to guess is the same input). Made deterministic
    /// without touching the server: the frame time and the physics step are fixed at the net tick (1/30 s); the world is held
    /// (<c>Time.timeScale = 0</c>, as NetServerHost holds a run until its player's inputs come) until the server has the
    /// client's first two inputs; the server's own wall-clock tick loop is switched off and the test runs exactly one
    /// server tick a frame, after the world's frame step; frames are padded to at least 40 ms of wall time so the client
    /// (which ticks on the wall clock) always has its input there before the server needs it. Fingerprint: the server's
    /// per-minute run log, enemies at 60/120/180 s and the ship's final state, against
    /// plan/v5/goldens/voyage-fingerprint-c-server-8282.txt. This guards M9c S1's clock change on the server path.
    /// </summary>
    [Category("V5")]
    public sealed class V5NetVoyageFingerprintPlayModeTests
    {
        const float VoyageSeconds = 180f;
        const double MinFrameSeconds = 0.040;
        static readonly Vector2 Steer = new Vector2(0.35f, 1f);
        static readonly MethodInfo ServerTick = typeof(NetServer).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);

        NetSaveIsolation _isolation;
        NetServer _server;
        NetClient _client;
        byte[] _key;
        ushort _port;
        VoyageFingerprint.PinnedDevice _device;
        float _captureDeltaTime, _timeScale, _fixedDeltaTime;

        sealed class ConstantInput : IShipSteerInput
        {
            public Vector2 ReadSteerThrottle() => Steer;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new NetSaveIsolation();
            _device = new VoyageFingerprint.PinnedDevice();
            _captureDeltaTime = Time.captureDeltaTime;
            _timeScale = Time.timeScale;
            _fixedDeltaTime = Time.fixedDeltaTime;
            NetServerComposition.PrepareHeadlessRun();
            _port = (ushort)Random.Range(20000, 40000);
            var raw = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(raw);
            _key = System.Text.Encoding.UTF8.GetBytes(JoinTicket.Base64Url(raw));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = _captureDeltaTime;
            Time.timeScale = _timeScale;
            Time.fixedDeltaTime = _fixedDeltaTime;
            if (_client != null) _client.Stop();
            if (_server != null) _server.Stop();
            yield return null;
            yield return null;
            if (_client != null) Object.Destroy(_client.gameObject);
            if (_server != null) Object.Destroy(_server.gameObject);
            _client = null;
            _server = null;
            NetLinkSimulator.Clear();
            if (NetVoyageClient.Instance != null) Object.Destroy(NetVoyageClient.Instance.gameObject);
            RunRequest.RemoteSimulation = false;
            NetServerComposition.EndHeadlessRun();
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _device?.Dispose();
            _device = null;
            _isolation?.Dispose();
            _isolation = null;
        }

        static ScenarioConfig ServerVoyage()
        {
            var s = ScriptableObject.CreateInstance<ScenarioConfig>();
            s.name = "fingerprint_server_8282";
            s.seed = 8282;
            s.playtest = ScenarioConfig.PlaytestSwitches.Shipped; // as the net voyage scenarios
            s.invulnerable = false;
            s.skipLevelUpCards = true; // level-ups apply with an empty pick (no card timing over the network)
            s.ambientSpawning = true;
            s.scriptedPhases = ScenarioConfig.ScriptedPhaseMode.AsTheGame;
            s.loadout.replaceWeapons = false;
            return s;
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator SoloServerVoyage_Seed8282_MatchesItsGolden()
        {
            Assert.IsNotNull(ServerTick, "NetServer.Tick (the server's one tick)");
            Time.captureDeltaTime = NetProtocol.TickSeconds;
            // One physics step a frame (the project's 0.02 s step against 1/30 s frames gives one or two steps a frame,
            // and which frames get two depended on how many frames the scene load took: two variants of this voyage).
            Time.fixedDeltaTime = NetProtocol.TickSeconds;
            var loadFrames = Time.frameCount;
            yield return Scenario.Load(ServerVoyage());
            Time.timeScale = 0f;
            loadFrames = Time.frameCount - loadFrames; // the world holds until the player's inputs are in (as NetServerHost)
            Assert.IsNotNull(Scenario.Current, "the scenario loaded");
            CollectionAssert.IsEmpty(Scenario.Current.Errors);
            var game = Scenario.Current.Game;
            Assert.AreEqual(30f, game.Config.Single<RoguePirates.V4.Config.RunConfig>().tickRate, "the server's run ticks at 30 Hz");
            Assert.IsNull(GameObject.Find("V4Presentation"), "the server's run has no presenters");

            _server = NetServer.Create(_port, new JoinTicketVerifier(_key), "127.0.0.1");
            _server.enabled = false; // no wall-clock ticks: the test ticks it once a frame
            _server.Attach(game);
            Assert.IsTrue(_server.StartListening(), "the server listens");
            var serverShip = game.Shell.Player;
            var ticket = new JoinTicketIssuer(_key).Issue("player-1", null, JoinTicket.UnixNow());
            _client = NetClient.Create(NetEndpoint.Parse($"ws://127.0.0.1:{_port}/"), ticket, () => ShipMotionPort.CreatePredictionCopy(serverShip), "V5NetClient");
            _client.Input = new ConstantInput();
            Assert.IsTrue(_client.Connect(), "the client starts");
            var wait = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < wait && !HasFirstInputs(_server.Current)) yield return null;
            Assert.IsTrue(_client.Welcomed, "welcomed (" + _client.LastDisconnectReason + ")");
            Assert.IsTrue(HasFirstInputs(_server.Current), "the client's first two inputs are on the server");
            var held = RunManager.Instance.ElapsedSeconds;

            var fp = new VoyageFingerprint("c-server-8282",
                "Case (c): solo server voyage, seed 8282, shipped playtest switches, ambient waves and scripted phases as the game,\n" +
                "damage on, no cards; headless server run at 30 Hz with an in-process NetClient sailing a constant input (steer 0.35,\n" +
                "throttle 1) over loopback WebSockets; one server tick a frame after the world's step, frame time 1/30 s, until 180 s or the sinking (154 s).");
            fp.Line("seed", 8282);
            fp.Line("heldAt", held);
            fp.Enemies("hold", game);
            var view = RoguePirates.V4.Foundation.Player.FollowCamera.SimulationViewOf(Camera.main);
            if (view != null) fp.Line("simView@hold", VoyageFingerprint.F(view.transform.position.x) + " " + VoyageFingerprint.F(view.transform.position.z) + " fov=" + VoyageFingerprint.F(view.fieldOfView) + " aspect=" + VoyageFingerprint.F(view.aspect));
            Debug.Log($"[V5 M9c S0] server voyage: scene load took {loadFrames} frames; held at {held} s");
            var samples = new Queue<float>(new[] { 60f, 120f, 180f });
            var run = RunManager.Instance;
            var deadline = Time.realtimeSinceStartup + 900f;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Time.timeScale = 1f;
            ServerTick.Invoke(_server, null); // the first tick, then from the next frame: world step, then one tick
            while (!run.IsRunEnded && samples.Count > 0 && Time.realtimeSinceStartup < deadline)
            {
                var left = MinFrameSeconds - clock.Elapsed.TotalSeconds;
                if (left > 0) System.Threading.Thread.Sleep((int)System.Math.Ceiling(left * 1000.0));
                yield return null;
                clock.Restart();
                ServerTick.Invoke(_server, null);
                if (run.ElapsedSeconds >= samples.Peek()) fp.Enemies(samples.Dequeue().ToString("0"), game);
            }
            var sank = run.IsRunEnded;
            Assert.IsTrue(sank || samples.Count == 0, "the voyage reached 180 s or sank");
            var m = _server.Metrics;
            Debug.Log($"[V5 M9c S0] server voyage: guessed inputs {m.GuessedInputs}, catch-up skips {m.CatchUpSteps}, late inputs {m.LateInputs}, " +
                      $"server ticks {_server.ServerTick}, steps applied {_server.StepsApplied}, client lead trims {_client.LeadTrims}");
            var recorder = game.RunLog;
            if (sank) for (var i = 0; i < 10 && recorder.Closed == null; i++) { yield return null; }
            else recorder.Close();
            Assert.IsNotNull(recorder.Log, "the server run keeps a run log");
            fp.Line("end", sank ? "sunk" : "afloat");
            fp.Line("server.worldTick", _server.WorldTick);
            fp.Line("server.stepsApplied", _server.StepsApplied);
            fp.Line("server.chestChoicesSkipped", _server.ChestChoicesSkipped);
            fp.RunLog(recorder.Log);
            fp.Ship(0, game, serverShip);
            Assert.Greater(run.KillCount, 0, "the waves were fought");
            // V5 M9c S0: this voyage has one known, unexplained variant (Y: seen 2 of 13 runs, both in one Editor session after
            // a full suite; evidence and what was ruled out in plan/v5/phase-09c-multiship/s0, stability/c.variant-Y.txt). It
            // is accepted (byte-exact on the fields it records) and reported; anything else fails. M9c S1 must find the cause,
            // make this voyage single-valued and delete voyage-fingerprint-c-server-8282.variant-Y.txt with this fallback.
            var matched = fp.AssertMatchesGoldenOrVariant("variant-Y");
            TestContext.WriteLine($"[V5 M9c S0] case (c) matched: {matched}");
        }

        static bool HasFirstInputs(NetServer.Session s)
        {
            if (s == null || !s.connected || s.expectNext == 0) return false;
            bool Has(uint tick) => s.ringTick[tick % (uint)s.ring.Length] == tick;
            return Has(s.expectNext) && Has(s.expectNext + 1);
        }
    }
}
#endif
