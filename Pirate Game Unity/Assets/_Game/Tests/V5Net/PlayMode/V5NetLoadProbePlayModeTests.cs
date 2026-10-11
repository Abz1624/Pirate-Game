using System.Collections;
using System.Globalization;
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
    /// V5 M9c S0b: the load200 test preset and the 4-ship bandwidth probe on the M9b server, in one Editor: a headless
    /// server voyage (the open-water scenario), its real client sailing S-turns, and <see cref="NetLoadProbe"/> keeping
    /// 200 enemies around the ship with 3 ghost ships. The short test checks the preset works; the explicit one is the
    /// 5-minute measurement (reports in Temp/V5Net/probe-*.json, copied to plan/v5/phase-09c-multiship/s0b/).
    /// </summary>
    [Category("V5")]
    public sealed class V5NetLoadProbePlayModeTests
    {
        NetSaveIsolation _isolation;
        NetServer _server;
        NetClient _client;
        V4Game _game;
        ushort _port;
        byte[] _key;

        sealed class Steer : IShipSteerInput
        {
            public Vector2 Value = new Vector2(0.4f, 0.8f);
            public Vector2 ReadSteerThrottle() => Value;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new NetSaveIsolation();
            NetServerComposition.PrepareHeadlessRun();
            NetTestPresets.AllowForTests = true;
            _port = (ushort)Random.Range(20000, 40000);
            _key = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(_key);
            _key = System.Text.Encoding.UTF8.GetBytes(JoinTicket.Base64Url(_key));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetTestPresets.AllowForTests = false;
            if (_client != null) _client.Stop();
            if (_server != null) _server.Stop();
            yield return null;
            yield return null;
            if (_client != null) Object.Destroy(_client.gameObject);
            if (_server != null) Object.Destroy(_server.gameObject);
            _client = null;
            _server = null;
            NetServerComposition.EndHeadlessRun();
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        IEnumerator StartProbe(NetLoadProbe.Settings settings, System.Action<NetLoadProbe> ready)
        {
            yield return Scenario.Load(Scenario.Find("net_voyage_open_water"));
            Assert.IsNotNull(Scenario.Current);
            _game = Scenario.Current.Game;
            Assert.AreEqual(30f, _game.Config.Single<RunConfig>().tickRate);
            _server = NetServer.Create(_port, new JoinTicketVerifier(_key), "127.0.0.1");
            _server.Attach(_game);
            Assert.IsTrue(_server.StartListening());
            var probe = NetLoadProbe.Attach(_server, _game, settings);
            var serverShip = _game.Shell.Player;
            var ticket = new JoinTicketIssuer(_key).Issue("probe-player", null, JoinTicket.UnixNow());
            _client = NetClient.Create(NetEndpoint.Parse($"ws://127.0.0.1:{_port}/"), ticket, () => ShipMotionPort.CreatePredictionCopy(serverShip));
            var steer = new Steer();
            _client.Input = steer;
            Assert.IsTrue(_client.Connect());
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!_client.Welcomed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_client.Welcomed, "welcomed");
            ready(probe);
        }

        static IEnumerator Sail(NetClient client, float seconds)
        {
            var input = (Steer)client.Input;
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                var t = Time.realtimeSinceStartup - start;
                input.Value = new Vector2(Mathf.Sin(t * 0.35f) * 0.6f, 0.85f); // wide S-turns
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Load200_KeepsTwoHundredEnemies_AndMeasuresFourViews()
        {
            NetLoadProbe probe = null;
            var settings = new NetLoadProbe.Settings { name = "load200-smoke", whatIfBudgets = new[] { 0 } };
            yield return StartProbe(settings, p => probe = p);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!probe.Measuring && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(probe.Measuring, "200 enemies reached (" + probe.ActiveEnemies + ")");
            yield return Sail(_client, 6f);
            Assert.GreaterOrEqual(probe.ActiveEnemies, 180, "the preset keeps about 200 enemies");
            Assert.IsTrue(_game.Shell.Player.Damage.IsAlive, "the ship is invulnerable");
            var path = probe.WriteReport("smoke");
            var json = System.IO.File.ReadAllText(path);
            StringAssert.Contains("\"ship\": \"ghost3\"", json, "four views measured");
            Assert.Greater(probe.Fx.Calls.Count, 0, "the build's effects were recorded");
        }

        /// <summary>The S0b measurement: RP_PROBE_SECONDS (300 for the memo) at load200 with 3 ghost ships; ignored without it.</summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator Probe_Load200_FourShips_FiveMinutes()
        {
            yield return Measure(new NetLoadProbe.Settings { name = "load200-inprocess" });
        }

        /// <summary>The same without weapons on the real ship: the build's share of the tick (for the 4-ship estimate).</summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator Probe_Load200_NoWeapons()
        {
            yield return Measure(new NetLoadProbe.Settings { name = "load200-noweapons-inprocess", weapons = false, whatIfBudgets = new[] { 0 } });
        }

        IEnumerator Measure(NetLoadProbe.Settings settings)
        {
            // Only on demand: RP_PROBE_SECONDS set in the Editor's environment (the suites skip it).
            if (!float.TryParse(System.Environment.GetEnvironmentVariable("RP_PROBE_SECONDS"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0f)
            {
                Assert.Ignore("the S0b load probe runs only with RP_PROBE_SECONDS set (e.g. 300)");
                yield break;
            }
            NetLoadProbe probe = null;
            yield return StartProbe(settings, p => probe = p);
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!probe.Measuring && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(probe.Measuring, "200 enemies reached");
            yield return Sail(_client, seconds);
            var path = probe.WriteReport("final");
            Debug.Log($"[V5 Net] probe {settings.name}: {path}");
            Assert.IsTrue(_client.Connected, "the real client stayed connected");
        }
    }
}
