using System;
using System.Collections.Generic;
using System.Globalization;
using RoguePirates.V4;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Online;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RoguePirates.V5.Net
{
    /// <summary>The API's answer to POST /api/lobbies/voyage/ticket (V5 M9b part 2a).</summary>
    [Serializable]
    public sealed class VoyageTicketResponse
    {
        public string ticket;
        public string matchServerUrl;
        public string matchId;
        public string mode;
        public long expiresAt;
    }

    /// <summary>
    /// V5 M9b part 2b: the browser client of a server voyage (the "Server voyage (test)" entry of the V5_NET WebGL build).
    /// <list type="number">
    /// <item>A join ticket from the API (<c>POST /api/lobbies/voyage/ticket</c>, the guest profile's bearer token), which
    /// names the match server (<c>ws://</c> locally, <c>wss://</c> on Railway).</item>
    /// <item>Joins; the welcome's seed builds the client's stage: the run scene loaded as usual (islands, map, ship,
    /// presenters) with <see cref="RunRequest.RemoteSimulation"/> on, so its own simulation never ticks.</item>
    /// <item>The run's ship becomes the prediction ship (the player's inputs move it at once, the server's snapshots
    /// correct it); the server's enemies are drawn by the run's enemy view presenter (<see cref="NetEnemyViews"/>); the
    /// level-up cards the server deals are shown by the run's card presenter and the pick goes back to the server; this
    /// HUD shows hull, level, kills, gold and time from the snapshots.</item>
    /// <item>The server's <c>end</c> (sunk, voyage over, server closing) or a connection that does not come back shows
    /// the end screen, and its button goes back to the menu.</item>
    /// </list>
    /// <c>?netvoyage=auto</c> runs it scripted (S-turns at full throttle, a boost at 45 s and every 35 s after, the first card picked through
    /// the card presenter after 1 s) and logs its measurements (client fps while connected, prediction, bandwidth).
    /// </summary>
    public sealed class NetVoyageClient : MonoBehaviour
    {
        public enum State { Ticket, Joining, LoadingStage, Sailing, Over }

        public State Current { get; private set; } = State.Ticket;
        public string Status { get; private set; } = "starting";
        public NetClient Client { get; private set; }
        public V4Game Stage { get; private set; }
        public bool Autopilot { get; private set; }
        public float AutopilotSeconds { get; private set; }
        public string EndTitle { get; private set; }
        public string EndDetail { get; private set; }
        /// <summary>Frame times (s) while sailing and connected (the D9b.1 fps measurement).</summary>
        public readonly List<float> FrameSeconds = new List<float>();

        public static NetVoyageClient Instance { get; private set; }

        sealed class Script : RoguePirates.V4.Foundation.Platform.IShipSteerInput
        {
            public Vector2 Value;
            public Vector2 ReadSteerThrottle() => Value;
        }

        readonly Script _script = new Script();
        NetEnemyViews _views;
        NetVoyageHud _hud;
        IOfferPresenter _cards;
        IOfferChoice _cardChoice;
        int _shownSeq = -1;
        float _sailStart, _nextStats;
        bool _autoBoost;
        float _autoBoostUntil, _nextAutoBoost;
        bool _reported;

        /// <summary>Starts a server voyage (one at a time).</summary>
        public static NetVoyageClient Begin(bool autopilot = false, float autopilotSeconds = 120f)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("V5NetVoyage");
            DontDestroyOnLoad(go);
            var client = go.AddComponent<NetVoyageClient>();
            client.Autopilot = autopilot;
            client.AutopilotSeconds = autopilotSeconds;
            return client;
        }

        void Awake()
        {
            Instance = this;
            _hud = gameObject.AddComponent<NetVoyageHud>();
            _hud.Bind(this);
        }

        void Start() => _ = Run();

        void Log(string message)
        {
            Status = message;
            Debug.Log("[V5 NetClient] " + message);
        }

        async Awaitable Run()
        {
            try
            {
                // 1. The ticket.
                Current = State.Ticket;
                Log("asking the API for a voyage ticket");
                var online = OnlineComposition.Online;
                if (online == null || !await online.EnsureConnected()) { Over("No connection", "The game server could not be reached (" + (online?.LastError ?? "offline") + ")."); return; }
                if (online.Active == null || !online.Active.Registered)
                {
                    var error = await online.CreateProfile("Tester" + UnityEngine.Random.Range(100, 999));
                    if (error != null || online.Active == null || !online.Active.Registered) { Over("No profile", "A guest profile could not be made: " + error); return; }
                }
                var reply = await online.Client.Post<VoyageTicketResponse>("/api/lobbies/voyage/ticket", null, online.Active.token);
                if (!reply.Ok || reply.Data == null || string.IsNullOrEmpty(reply.Data.ticket))
                {
                    Over("No ticket", reply.ErrorCode == "match_tickets_off" ? "Server voyages are switched off on this API." : "The API gave no ticket: " + reply);
                    return;
                }
                var url = reply.Data.matchServerUrl;
                if (!NetEndpoint.TryParse(url, out var endpoint, out var parseError)) { Over("No match server", "The API named no usable match server (" + parseError + ")."); return; }

                // 2. The join.
                Current = State.Joining;
                Log("joining " + endpoint.Url + " (match " + reply.Data.matchId + ")");
                Client = NetClient.Create(endpoint, reply.Data.ticket, () => null, "V5NetVoyageClient");
                if (Autopilot) Client.Input = _script;
                Client.Boost = () => Autopilot ? _autoBoost : Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                Client.OfferReceived += OnOffer;
                Client.OfferResolved += OnOfferResolved;
                var joinStart = Time.realtimeSinceStartup;
                if (!Client.Connect()) { Over("Could not connect", "The connection did not start."); return; }
                while (!Client.Welcomed && !Client.SessionOver && Time.realtimeSinceStartup - joinStart < 15f) await Awaitable.NextFrameAsync();
                if (!Client.Welcomed) { Over("Could not join", Client.LastDisconnectReason ?? "The match server did not answer."); return; }
                Log($"welcomed in {Time.realtimeSinceStartup - joinStart:0.00} s; seed {Client.FirstWelcome.seed}");

                // 3. The stage from the welcome's seed.
                Current = State.LoadingStage;
                var loadStart = Time.realtimeSinceStartup;
                AppRoot.EnsureInitialized();
                RunRequest.Headless = false;
                RunRequest.RemoteSimulation = true;
                RunRequest.ClearPvp();
                RunRequest.RequestSeed(Client.FirstWelcome.seed);
                await SceneManager.LoadSceneAsync(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
                var deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline && !Client.SessionOver)
                {
                    Stage = FindFirstObjectByType<V4Game>();
                    if (Stage != null && Stage.IsRunning) break;
                    await Awaitable.NextFrameAsync();
                }
                if (Stage == null || !Stage.IsRunning) { Over("The stage did not load", Client.SessionOver ? "The connection ended while loading." : "The run scene did not start."); return; }
                var ship = ShipMotionPort.Of(Stage);
                ship.TakeControl();
                ship.IgnoreLocalDamage();
                Client.AttachShip(ship);
                _views = gameObject.AddComponent<NetEnemyViews>();
                _views.Bind(Stage, Client);
                Stage.Context.TryGet(out _cards);
                Stage.Context.TryGet(out _cardChoice);
                _hud.HideRunHud();
                Log($"stage built in {Time.realtimeSinceStartup - loadStart:0.00} s; sailing");
                Current = State.Sailing;
                _sailStart = Time.realtimeSinceStartup;
                _nextStats = _sailStart + 10f;
                _nextAutoBoost = _sailStart + 45f;
                Client.Metrics.Reset();
                if (Client.PendingOffer != null) OnOffer(Client.PendingOffer); // dealt while the stage loaded
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Over("Something went wrong", e.Message);
            }
        }

        void Update()
        {
            if (Current != State.Sailing || Client == null) return;
            var run = RunManager.Instance;
            if (run != null && run.IsPaused) run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); // the server's pause is the one that counts
            if (Client.Connected && Client.Welcomed) FrameSeconds.Add(Time.unscaledDeltaTime);
            var now = Time.realtimeSinceStartup;
            if (Autopilot) Drive(now - _sailStart);
            if (now >= _nextStats)
            {
                _nextStats = now + 10f;
                Debug.Log("[V5 NetClient] stats " + StatsLine());
            }
            if (Autopilot && !_reported && now - _sailStart >= AutopilotSeconds) WriteReport("autopilot done");
            if (Client.SessionOver)
            {
                var reason = Client.Ended ? Client.EndReason : (NetProtocol.EndReason)0;
                switch (reason)
                {
                    case NetProtocol.EndReason.Sunk: Over("Your ship sank", Summary()); break;
                    case NetProtocol.EndReason.RunEnded: Over("Voyage complete", Summary()); break;
                    case NetProtocol.EndReason.ServerClosing: Over("The server closed", "The match server shut down (a restart). Nothing was settled. " + Summary()); break;
                    case NetProtocol.EndReason.SessionExpired: Over("Session expired", Summary()); break;
                    default: Over("Connection lost", "The connection did not come back within 10 s. " + Summary()); break;
                }
            }
        }

        string Summary()
        {
            var s = Client != null ? Client.Last : default;
            return $"Time {Clock(s.runTime)}, kills {s.kills}, level {s.level}, gold {s.gold}.";
        }

        public static string Clock(float seconds)
        {
            var t = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{t / 60:00}:{t % 60:00}";
        }

        /// <summary>The scripted voyage: wide S-turns at full throttle, a boost whenever it is ready, cards after 1 s.</summary>
        void Drive(float t)
        {
            _script.Value = new Vector2(t < 5f ? 0f : Mathf.Sin(t * 0.35f) * 0.8f, 1f);
            var now = Time.realtimeSinceStartup;
            if (now >= _nextAutoBoost) { _autoBoost = true; _autoBoostUntil = now + 0.3f; _nextAutoBoost = now + 35f; }
            if (_autoBoost && now >= _autoBoostUntil) _autoBoost = false;
            var offer = Client.PendingOffer;
            if (offer != null && now - Client.PendingOfferSince > 1f && _autoPickedSeq != offer.seq)
            {
                _autoPickedSeq = offer.seq;
                if (_cardChoice != null && _cardChoice.IsShowing) _cardChoice.Pick(0); // the card presenter's own pick
                else Client.SendPick(NetProtocol.PickAction.Pick, 0);
            }
        }

        int _autoPickedSeq = -1;

        // ---- Level-up cards ------------------------------------------------------------------------------------------

        void OnOffer(OfferMessage m)
        {
            if (Current != State.Sailing || m == null || m.seq == _shownSeq) return;
            _shownSeq = m.seq;
            var offers = new List<Offer>(m.offers);
            if (_cards != null && _cards.TryShow(offers, o => Choose(m, o), () => Client.SendPick(NetProtocol.PickAction.Skip), () => Client.SendPick(NetProtocol.PickAction.Refresh), m.refreshesRemaining))
                return;
            _hud.ShowCards(m); // no card presenter on this stage: the HUD's plain buttons
        }

        void Choose(OfferMessage m, Offer chosen)
        {
            var index = m.offers.FindIndex(o => o.Key == chosen.Key);
            Client.SendPick(NetProtocol.PickAction.Pick, Mathf.Max(0, index));
        }

        void OnOfferResolved(OfferClosedMessage m)
        {
            _hud.HideCards();
            if (_cardChoice != null && _cardChoice.IsShowing) _cards?.Hide(); // the server moved on (a timeout): the cards go
        }

        // ---- End --------------------------------------------------------------------------------------------------

        void Over(string title, string detail)
        {
            if (Current == State.Over) return;
            Current = State.Over;
            EndTitle = title;
            EndDetail = detail;
            Log("over: " + title + ". " + detail);
            if (Client != null && !_reported) WriteReport("over: " + title);
            if (Client != null) Client.Stop(); // the session is over: the socket closes, the server makes way for the next voyage
            _hud.ShowEnd(title, detail);
        }

        /// <summary>The end screen's button: the voyage is left and the menu loads.</summary>
        public void BackToMenu()
        {
            if (Client != null) { Client.Stop(); Destroy(Client.gameObject); }
            RunRequest.RemoteSimulation = false;
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(V4SceneNames.MainMenu);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Client != null)
            {
                Client.OfferReceived -= OnOffer;
                Client.OfferResolved -= OnOfferResolved;
            }
        }

        // ---- Measurements ---------------------------------------------------------------------------------------------

        public float FpsMean => FrameSeconds.Count == 0 ? 0f : FrameSeconds.Count / Sum(FrameSeconds);

        static float Sum(List<float> values) { var s = 0f; foreach (var v in values) s += v; return s; }

        /// <summary>The frame rate the slowest <paramref name="percent"/>% of frames stayed under (1 / the frame time's percentile).</summary>
        public float FpsLow(float percent) => FrameSeconds.Count == 0 ? 0f : 1f / Mathf.Max(1e-4f, NetMetrics.Percentile(FrameSeconds, 100f - percent));

        public float ShareOfFramesOver(float seconds)
        {
            if (FrameSeconds.Count == 0) return 0f;
            var n = 0;
            foreach (var f in FrameSeconds) if (f > seconds) n++;
            return n / (float)FrameSeconds.Count;
        }

        string StatsLine()
        {
            var m = Client.Metrics;
            var traffic = m.For(Unity.Netcode.NetworkManager.ServerClientId);
            return string.Format(CultureInfo.InvariantCulture,
                "sailing={0:0}s frames={1} fpsMean={2:0.0} fps1pctLow={3:0.0} fps5pctLow={4:0.0} framesOver20ms={5:0.0}% snapshots={6} enemies={7} drawn={8} " +
                "downBps={9:0} upBps={10:0} predErrP95={11:0.000} predErrMax={12:0.000} corrMax={13:0.000} inputLagP50={14:0} cardSets={15} picks={16} hull={17:0}/{18:0} level={19} kills={20}",
                Time.realtimeSinceStartup - _sailStart, FrameSeconds.Count, FpsMean, FpsLow(1f), FpsLow(5f), ShareOfFramesOver(0.02f) * 100f, Client.Snapshots,
                Client.Enemies.Count, _views != null ? _views.Drawn : 0, traffic.bytesDown / m.Elapsed, traffic.bytesUp / m.Elapsed,
                NetMetrics.Percentile(m.PredictionError, 95f), NetMetrics.Max(m.PredictionError), NetMetrics.Max(m.Correction),
                NetMetrics.Percentile(m.InputLagMs, 50f), Client.OffersReceived, Client.PicksSent, Client.Hull, Client.MaxHull, Client.Last.level, Client.Last.kills);
        }

        void WriteReport(string why)
        {
            _reported = true;
            var context = new Dictionary<string, string>
            {
                ["why"] = why,
                ["platform"] = Application.platform.ToString(),
                ["url"] = Client.Endpoint.Url,
                ["autopilot"] = Autopilot ? "true" : "false",
                ["sailingSeconds"] = (Time.realtimeSinceStartup - _sailStart).ToString("0.0", CultureInfo.InvariantCulture),
                ["frames"] = FrameSeconds.Count.ToString(),
                ["fpsMean"] = FpsMean.ToString("0.0", CultureInfo.InvariantCulture),
                ["fps1pctLow"] = FpsLow(1f).ToString("0.0", CultureInfo.InvariantCulture),
                ["fps5pctLow"] = FpsLow(5f).ToString("0.0", CultureInfo.InvariantCulture),
                ["framesOver20msPercent"] = (ShareOfFramesOver(0.02f) * 100f).ToString("0.00", CultureInfo.InvariantCulture),
                ["framesOver33msPercent"] = (ShareOfFramesOver(0.0334f) * 100f).ToString("0.00", CultureInfo.InvariantCulture),
                ["cardSets"] = Client.OffersReceived.ToString(),
                ["picks"] = Client.PicksSent.ToString(),
                ["level"] = Client.Last.level.ToString(),
                ["kills"] = Client.Last.kills.ToString(),
                ["drawnEnemies"] = (_views != null ? _views.Drawn : 0).ToString(),
                ["unknownEnemyKinds"] = (_views != null ? _views.Unknown : 0).ToString(),
                ["resyncs"] = Client.Resyncs.ToString(),
                ["welcomes"] = Client.Welcomes.ToString(),
            };
            var json = NetMetrics.Report("V5 net WebGL client", context, Client.Metrics);
            Debug.Log("[V5 NetClient] REPORT " + json.Replace("\n", " "));
        }
    }
}
