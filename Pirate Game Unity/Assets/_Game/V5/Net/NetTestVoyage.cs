using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RoguePirates.V4;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Ports;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2a: the scripted server voyage's headless test client (the V5NetSpike scene's Client role, started
    /// with <c>-rpClient ws://host:port/</c>; the join ticket in <c>RP_JOIN_TICKET</c>). It loads the run on the same seed
    /// as a still stage (islands for its prediction ship; the stage's own run is held at time scale 0), joins, sails a
    /// scripted course for <c>-rpSeconds</c> (wide S-turns at full throttle), drops its own socket once at
    /// <c>-rpDropAt</c> (resumes with the token), then prints <c>VOYAGE-DONE</c> and keeps sailing until the server ends
    /// the session (the harness stops the container): it then goes back to the lobby state and writes its report to
    /// <c>-rpOut</c>. Exit code 0 when it joined and reached the lobby, 1 otherwise. Not part of any game build: the
    /// V5_NET server image only.
    /// </summary>
    public sealed class NetTestVoyage : MonoBehaviour
    {
        public enum State { Loading, Joining, Sailing, Reconnecting, Lobby }

        public State Current { get; private set; } = State.Loading;
        public NetClient Client { get; private set; }

        sealed class Script : IShipSteerInput
        {
            public Vector2 Value;
            public Vector2 ReadSteerThrottle() => Value;
        }

        readonly Script _script = new Script();
        readonly List<string> _events = new List<string>();
        float _t0;

        static string Arg(string name, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        static float ArgFloat(string name, float fallback) =>
            float.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        void Log(string message)
        {
            var line = $"{Time.realtimeSinceStartup - _t0:0.00}s {message}";
            _events.Add(line);
            Debug.Log("[V5 NetTest] " + line);
        }

        IEnumerator Start()
        {
            _t0 = Time.realtimeSinceStartup;
            var url = Arg("-rpClient", "ws://127.0.0.1:7777/");
            var seconds = ArgFloat("-rpSeconds", 90f);
            var dropAt = ArgFloat("-rpDropAt", 45f);
            var waitEnd = ArgFloat("-rpWaitEnd", 90f);
            var seed = (int)ArgFloat("-rpSeed", 9200);
            var output = Arg("-rpOut", Path.Combine(Application.temporaryCachePath, "client.json"));
            var ticket = Environment.GetEnvironmentVariable("RP_JOIN_TICKET");
            if (!NetEndpoint.TryParse(url, out var endpoint, out var error)) { Finish(output, "bad url: " + error, false); yield break; }

            V4Game stage = null;
            yield return NetServerComposition.LoadRun(seed, g => stage = g);
            if (stage == null) { Finish(output, "the stage did not load", false); yield break; }
            var template = stage.Shell.Player;
            ShipMotionPort.Of(stage).TakeControl();
            Time.timeScale = 0f; // the stage holds still: only its islands matter to the prediction ship
            Log("stage loaded (seed " + seed + ")");

            Current = State.Joining;
            Client = NetClient.Create(endpoint, ticket, () => ShipMotionPort.CreatePredictionCopy(template), "V5NetTestClient");
            Client.Input = _script;
            var joinStart = Time.realtimeSinceStartup;
            if (!Client.Connect()) { Finish(output, "the client did not start", false); yield break; }
            while (!Client.Welcomed && !Client.SessionOver && Time.realtimeSinceStartup - joinStart < 15f) yield return null;
            if (!Client.Welcomed) { Finish(output, "join failed: " + (Client.LastDisconnectReason ?? "timeout"), false); yield break; }
            var joinSeconds = Time.realtimeSinceStartup - joinStart;
            Log($"welcomed in {joinSeconds:0.000} s");
            Current = State.Sailing;

            var sailStart = Time.realtimeSinceStartup;
            var dropped = dropAt <= 0f;
            var welcomesBeforeDrop = 0;
            float droppedAt = -1f, resumedIn = -1f;
            while (Time.realtimeSinceStartup - sailStart < seconds && !Client.SessionOver)
            {
                Steer(Time.realtimeSinceStartup - sailStart);
                if (!dropped && Time.realtimeSinceStartup - sailStart >= dropAt)
                {
                    dropped = true;
                    welcomesBeforeDrop = Client.Welcomes;
                    droppedAt = Time.realtimeSinceStartup;
                    Client.DropForTest();
                    Current = State.Reconnecting;
                    Log("socket dropped (client side)");
                }
                if (Current == State.Reconnecting && Client.Welcomes > welcomesBeforeDrop && Client.Connected)
                {
                    resumedIn = Time.realtimeSinceStartup - droppedAt;
                    Current = State.Sailing;
                    Log($"resumed in {resumedIn:0.000} s");
                }
                yield return null;
            }
            Log($"VOYAGE-DONE snapshots={Client.Snapshots} hull={Client.Hull:0}/{Client.MaxHull:0} kills={Client.Last.kills} level={Client.Last.level}");

            // Keep sailing until the server ends the session (the harness stops the container now).
            var waitStart = Time.realtimeSinceStartup;
            float firstSign = -1f;
            while (Time.realtimeSinceStartup - waitStart < waitEnd)
            {
                if (firstSign < 0f && (Client.Ended || !Client.Connected))
                {
                    firstSign = Time.realtimeSinceStartup;
                    Log(Client.Ended ? "end " + Client.EndReason + " received" : "socket closed: " + Client.LastDisconnectReason);
                }
                if (Client.SessionOver) break;
                Steer(Time.realtimeSinceStartup - sailStart);
                yield return null;
            }
            if (firstSign < 0f) firstSign = Time.realtimeSinceStartup;
            var ended = Client.Ended;
            var reason = ended ? "end " + Client.EndReason : Client.SessionOver ? "disconnected: " + Client.LastDisconnectReason : "timeout waiting for the end";
            Client.Stop();
            Current = State.Lobby;
            var lobbyUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            Log($"lobby ({reason}) {Time.realtimeSinceStartup - firstSign:0.000} s after the server's end showed, at {lobbyUtc}");
            Finish(output, reason, Client.SessionOver || ended, new Dictionary<string, string>
            {
                ["joinSeconds"] = joinSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["resumeSeconds"] = resumedIn.ToString("0.000", CultureInfo.InvariantCulture),
                ["endedByServer"] = ended ? "true" : "false",
                ["endReason"] = ended ? Client.EndReason.ToString() : "",
                ["lobbyUtc"] = lobbyUtc,
                ["secondsFromServerEndToLobby"] = (Time.realtimeSinceStartup - firstSign).ToString("0.000", CultureInfo.InvariantCulture),
                ["snapshots"] = Client.Snapshots.ToString(),
                ["welcomes"] = Client.Welcomes.ToString(),
                ["resyncs"] = Client.Resyncs.ToString(),
                ["finalHull"] = Client.Hull.ToString("0", CultureInfo.InvariantCulture),
                ["kills"] = Client.Last.kills.ToString(),
                ["level"] = Client.Last.level.ToString(),
                ["cardSets"] = Client.OffersReceived.ToString(),
                ["cardsPicked"] = _cardsPicked.ToString(),
            });
        }

        int _pickedSeq = -1;
        int _cardsPicked;

        /// <summary>Wide S-turns at full throttle: the ship keeps moving and turning, near its start. V5 M9b part 2b: level-up
        /// cards are answered after 0.4 s with the first card (the server waits for the pick, as a voyage does).</summary>
        void Steer(float t)
        {
            var offer = Client != null ? Client.PendingOffer : null;
            if (offer != null && offer.seq != _pickedSeq && Time.realtimeSinceStartup - Client.PendingOfferSince > 0.4f && Client.SendPick(NetProtocol.PickAction.Pick, 0))
            {
                _pickedSeq = offer.seq;
                _cardsPicked++;
                Log($"level-up card picked ({offer.offers[0].Kind} {offer.offers[0].Id}, set {offer.seq})");
            }
            var steer = t < 5f ? 0f : Mathf.Sin(t * 0.35f) * 0.8f;
            _script.Value = new Vector2(steer, 1f);
        }

        void Finish(string output, string outcome, bool ok, Dictionary<string, string> context = null)
        {
            Current = State.Lobby;
            context ??= new Dictionary<string, string>();
            context["url"] = Client != null ? Client.Endpoint.Url : Arg("-rpClient", "");
            context["outcome"] = outcome;
            context["ok"] = ok ? "true" : "false";
            context["events"] = string.Join(" | ", _events).Replace("\"", "'");
            try
            {
                var json = Client != null
                    ? NetMetrics.WriteReport(output, "V5 net test client (scripted voyage)", context, Client.Metrics)
                    : NetMetrics.WriteReport(output, "V5 net test client (scripted voyage)", context);
                Debug.Log("[V5 NetTest] report " + output + "\n" + json);
            }
            catch (Exception e) { Debug.LogWarning("[V5 NetTest] report not written: " + e.Message); }
            Debug.Log("[V5 NetTest] EXIT " + (ok ? "ok" : "failed") + ": " + outcome);
            Application.Quit(ok ? 0 : 1);
        }
    }
}
