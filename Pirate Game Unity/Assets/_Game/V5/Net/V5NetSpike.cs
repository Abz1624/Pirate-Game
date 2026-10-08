using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RoguePirates.V4;
using RoguePirates.V4.Ports;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the V5NetSpike scene's driver (Editor, or a build with the V5_NET define). Roles:
    /// <list type="bullet">
    /// <item><b>InProcess</b> (default): a headless server voyage and a client in this one process, the client steering
    /// with the keyboard (WASD, Shift to boost); the client's predicted ship and its enemies (spheres) are drawn; F9 writes
    /// the measurement report to Temp/V5Net.</item>
    /// <item><b>Server</b>: a headless server voyage listening on plain WebSockets at $PORT (7777 locally); the join ticket
    /// key from RP_MATCH_TICKET_SECRET. Part 2 runs this role in the Linux server build.</item>
    /// </list>
    /// The client of a remote server (a browser on Railway) is part 2's WebGL build.
    /// </summary>
    public sealed class V5NetSpike : MonoBehaviour
    {
        public enum Role { InProcess, Server }

        [SerializeField] Role role = Role.InProcess;
        [SerializeField] int seed = 9200;
        [SerializeField] ushort localPort = 7777;
        [Tooltip("InProcess: simulated latency each way (ms) and loss (%), 0 for a clean link.")]
        [SerializeField] int sendDelayMs;
        [SerializeField] int sendDropPercent;

        public NetServer Server { get; private set; }
        public NetClient Client { get; private set; }
        string _status = "starting";
        readonly List<Transform> _spheres = new List<Transform>();

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (Application.isBatchMode || System.Environment.GetCommandLineArgs().Contains("-rpServer")) role = Role.Server;
        }

        IEnumerator Start()
        {
            V4Game game = null;
            yield return NetServerComposition.LoadRun(seed, g => game = g);
            if (game == null) { _status = "the run did not start"; yield break; }
            byte[] key;
            if (role == Role.Server) key = JoinTicket.KeyFromEnvironment();
            else
            {
                // In-process: a key for this session only (never a key written in code).
                key = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
                key = System.Text.Encoding.UTF8.GetBytes(JoinTicket.Base64Url(key));
            }
            var port = role == Role.Server ? NetEndpoint.PortFromEnvironment(localPort) : localPort;
            var verifier = new JoinTicketVerifier(key);
            if (role == Role.Server) verifier.ExpectedMatchId = System.Environment.GetEnvironmentVariable(JoinTicketVerifier.MatchIdEnvironmentVariable);
            Server = NetServer.Create(port, verifier, role == Role.Server ? "0.0.0.0" : "127.0.0.1");
            Server.Attach(game);
            if (role == Role.InProcess && (sendDelayMs > 0 || sendDropPercent > 0))
                NetLinkSimulator.Configure(Server.Transport, new NetLinkSimulator.Link { sendDelayMs = sendDelayMs, sendDropPercent = sendDropPercent, seed = 1 });
            if (!Server.StartListening()) { _status = "the server did not start (see the console)"; yield break; }
            _status = $"server on ws port {port}";
            if (role == Role.Server) yield break;

            var ticket = new JoinTicketIssuer(key).Issue("spike-player", "spike", JoinTicket.UnixNow());
            var serverShip = game.Shell.Player;
            Client = NetClient.Create(NetEndpoint.Parse($"ws://127.0.0.1:{port}/"), ticket, () => ShipMotionPort.CreatePredictionCopy(serverShip));
            Client.Boost = () => UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed;
            if (sendDelayMs > 0 || sendDropPercent > 0)
                NetLinkSimulator.Configure(Client.Transport, new NetLinkSimulator.Link { sendDelayMs = sendDelayMs, sendDropPercent = sendDropPercent, seed = 2 });
            Client.Connect();
            _status = "in-process server and client";
        }

        void Update()
        {
            if (Client == null) return;
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame) WriteReport();
            DrawEnemies();
        }

        void DrawEnemies()
        {
            var i = 0;
            foreach (var e in Client.Enemies.All)
            {
                if (i == _spheres.Count)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(sphere.GetComponent<Collider>());
                    sphere.transform.SetParent(transform, false);
                    _spheres.Add(sphere.transform);
                }
                var t = _spheres[i++];
                t.gameObject.SetActive(true);
                t.position = NetEnemyMirror.Interpolate(e, 1f) + Vector3.up;
                t.localScale = Vector3.one * ((e.flags & NetProtocol.EnemyBoss) != 0 ? 6f : (e.flags & NetProtocol.EnemyElite) != 0 ? 3f : 1.6f);
            }
            for (; i < _spheres.Count; i++) _spheres[i].gameObject.SetActive(false);
        }

        public string WriteReport()
        {
            var path = Path.Combine(Application.dataPath, "..", "Temp", "V5Net", "spike-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            NetMetrics.WriteReport(path, "V5NetSpike in-process", new Dictionary<string, string>
            {
                ["sendDelayMs"] = sendDelayMs.ToString(),
                ["sendDropPercent"] = sendDropPercent.ToString(),
            }, Server.Metrics, Client.Metrics);
            Debug.Log("[V5 Net] report written to " + Path.GetFullPath(path));
            return path;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 420, 220), GUI.skin.box);
            GUILayout.Label("V5 net spike: " + _status);
            if (Server != null) GUILayout.Label($"server tick {Server.ServerTick}, tick time p95 {NetMetrics.Percentile(Server.Metrics.TickMs, 95f):0.00} ms");
            if (Client != null)
            {
                GUILayout.Label($"client: {(Client.Connected ? "connected" : "not connected")}, hull {Client.Hull:0}/{Client.MaxHull:0}, enemies {Client.Enemies.Count}");
                GUILayout.Label($"prediction error p95 {NetMetrics.Percentile(Client.Metrics.PredictionError, 95f):0.000} m, correction max {NetMetrics.Max(Client.Metrics.Correction):0.000} m");
                if (Client.Ended) GUILayout.Label("voyage over: " + Client.EndReason);
                GUILayout.Label("WASD to sail, Shift to boost, F9 writes the report");
            }
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            Client?.Stop();
            Server?.Stop();
            NetLinkSimulator.Clear();
            NetServerComposition.EndHeadlessRun();
        }
    }
}
