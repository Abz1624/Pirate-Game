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

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

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
            _dropDuringCards = HasArg("-rpDropDuringCards");
            _awayDuringCards = ArgFloat("-rpAwayDuringCards", 0f);
            _hiddenDuringCards = ArgFloat("-rpHiddenDuringCards", 0f);
            _hideAfterPick = ArgFloat("-rpHideAfterPick", 0f);
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
            Client.AwaySource = () => _away || _pickHidden;
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
                ["holdsSent"] = Client.HoldsSent.ToString(),
                ["snapshotsSkippedForHoldOrPick"] = Client.SkippedSnapshots.ToString(),
                ["droppedDuringCards"] = _droppedDuringCards ? "true" : "false",
                ["resumeDuringCardsSeconds"] = _cardsResumeSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["cardsBackAfterResume"] = _cardsBackAfterResume ? "true" : "false",
                ["awayDuringCardsSeconds"] = _awayDuringCards.ToString("0.0", CultureInfo.InvariantCulture),
                ["awayWelcomeSeconds"] = _awayWelcomeSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["cardsBackAfterAway"] = _cardsBackAfterAway ? "true" : "false",
                ["hiddenDuringCardsSeconds"] = _hiddenDuringCards.ToString("0.0", CultureInfo.InvariantCulture),
                ["hiddenCardsResolution"] = _hiddenResolution,
                ["hiddenControlBackSeconds"] = _hiddenControlSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["hideAfterPickSeconds"] = _hideAfterPick.ToString("0.0", CultureInfo.InvariantCulture),
                ["hideAfterPickWelcomeSeconds"] = _pickHideWelcomeSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["hideAfterPickControlSeconds"] = _pickHideControlSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                ["hideAfterPickResyncs"] = _pickHideResyncs.ToString(),
                ["lateResyncs"] = Client.LateResyncs.ToString(),
                ["awaysSent"] = Client.AwaysSent.ToString(),
                ["enemyJumpsMarked"] = Client.Enemies.Jumps.ToString(),
            });
        }

        int _pickedSeq = -1;
        int _cardsPicked;
        bool _dropDuringCards, _droppedDuringCards, _cardsBackAfterResume;
        float _cardsDropAt = -1f, _cardsResumeSeconds = -1f;
        int _welcomesAtCardsDrop;
        // M9b playtest fixes: -rpAwayDuringCards S: on a later card set the page goes away for S s (the server holds the
        // voyage and its card timeout), then comes back (a welcome resyncs it, the cards come back, the pick follows).
        // -rpHiddenDuringCards S: on the set after that the page runs as a hidden tab that never said so (one frame a
        // second) for S s; past 30 s the server's timeout takes the first card and the client must sail on at once.
        float _awayDuringCards, _hiddenDuringCards;
        bool _away, _awayDone, _cardsBackAfterAway, _hiddenDone;
        int _awaySeq = -1, _hiddenSeq = -1, _welcomesAtBack;
        float _awayUntil = -1f, _backAt = -1f, _awayWelcomeSeconds = -1f, _hiddenUntil = -1f, _hiddenControlSeconds = -1f;
        string _hiddenResolution = "";
        // V5 M9b owner re-test fixes: -rpHideAfterPick S: once the phases above are done, the next pick is followed at once by
        // the page hidden for S s (it says it is away from its first hidden frame, one a second); back, the welcome resyncs
        // it and the client must steer within one snapshot, with no resync loop.
        float _hideAfterPick, _pickHideUntil = -1f, _pickBackAt = -1f, _pickHideWelcomeSeconds = -1f, _pickHideControlSeconds = -1f, _pickWelcomedAt = -1f;
        bool _pickHidden, _pickHideStarted, _pickHideDone;
        int _pickHideResyncsAt, _pickHideResyncs = -1, _welcomesAtPickBack;
        uint _pickHideTick;

        /// <summary>The hide-after-pick phase; true while it keeps the script from acting.</summary>
        bool HiddenAfterPick()
        {
            if (!_pickHideStarted || _pickHideDone) return false;
            var now = Time.realtimeSinceStartup;
            if (_pickHidden)
            {
                if (now < _pickHideUntil || Client.Throttled) return true;
                _pickHidden = false;
                _pickBackAt = now;
                _welcomesAtPickBack = Client.Welcomes;
                Log("page back after the pick");
            }
            if (Client.Welcomes <= _welcomesAtPickBack)
            {
                if (now - _pickBackAt > 5f) { _pickHideDone = true; Log("no welcome 5 s after coming back"); }
                return true;
            }
            if (_pickWelcomedAt < 0f) { _pickWelcomedAt = now; _pickHideWelcomeSeconds = now - _pickBackAt; _pickHideTick = Client.Tick; }
            if (Client.Tick > _pickHideTick && !Client.Holds(Client.Tick + 1))
            {
                _pickHideControlSeconds = now - _pickWelcomedAt;
                _pickHideResyncs = Client.Resyncs - _pickHideResyncsAt;
                _pickHideDone = true;
                Log($"control back {_pickHideControlSeconds:0.000} s after the welcome ({_pickHideWelcomeSeconds:0.000} s after the page came back); resyncs over the hide {_pickHideResyncs}");
                return false;
            }
            if (now - _pickWelcomedAt > 5f) { _pickHideDone = true; Log("no control 5 s after the welcome"); }
            return true;
        }

        /// <summary>The away and hidden phases; true while they keep the script from picking.</summary>
        bool AwayOrHidden(OfferMessage offer)
        {
            var now = Time.realtimeSinceStartup;
            if (_awayDuringCards > 0f && !_awayDone && _awaySeq < 0 && offer != null && offer.seq != _pickedSeq &&
                (!_dropDuringCards || _cardsBackAfterResume && offer.seq > 1) && now - Client.PendingOfferSince > 0.2f)
            {
                _awaySeq = offer.seq;
                _away = true;
                _awayUntil = now + _awayDuringCards;
                Log($"page away for {_awayDuringCards:0} s while the cards were up (set {offer.seq})");
            }
            if (_away)
            {
                if (now < _awayUntil) return true;
                _away = false;
                _backAt = now;
                _welcomesAtBack = Client.Welcomes;
                Log("page back");
            }
            if (_backAt >= 0f && !_awayDone)
            {
                if (Client.Welcomes <= _welcomesAtBack) return true;
                if (_awayWelcomeSeconds < 0f) { _awayWelcomeSeconds = now - _backAt; Log($"resynced {_awayWelcomeSeconds:0.000} s after coming back"); }
                if (offer == null && now - _backAt < 3f) return true;
                _cardsBackAfterAway = offer != null && offer.seq == _awaySeq;
                _awayDone = true;
                Log(_cardsBackAfterAway ? $"the cards came back after the away (set {offer.seq}); server timeout paused" : "no cards after the away");
            }
            if (_hiddenDuringCards > 0f && !_hiddenDone && _hiddenSeq < 0 && (_awayDuringCards <= 0f || _awayDone) && offer != null &&
                offer.seq != _pickedSeq && offer.seq != _awaySeq && now - Client.PendingOfferSince > 0.2f)
            {
                _hiddenSeq = offer.seq;
                _hiddenUntil = now + _hiddenDuringCards;
                Client.ThrottleForTest(_hiddenDuringCards);
                Log($"page hidden without telling the server (one frame a second) for {_hiddenDuringCards:0} s (set {offer.seq})");
                return true;
            }
            if (_hiddenSeq >= 0 && !_hiddenDone)
            {
                if (Client.Throttled || now < _hiddenUntil) return true;
                if (Client.PendingOffer == null && !Client.Holds(Client.Tick + 1))
                {
                    _hiddenControlSeconds = now - _hiddenUntil;
                    _hiddenResolution = Client.LastResolution.how.ToString();
                    _hiddenDone = true;
                    Log($"page active again: cards {_hiddenResolution}, control back in {_hiddenControlSeconds:0.000} s (late resyncs {Client.LateResyncs}, resyncs {Client.Resyncs})");
                    return false;
                }
                if (now - _hiddenUntil > 5f) { _hiddenDone = true; _hiddenResolution = "no control after 5 s"; Log("page active again: " + _hiddenResolution); }
                return Client.PendingOffer == null;
            }
            return false;
        }

        /// <summary>Wide S-turns at full throttle: the ship keeps moving and turning, near its start. V5 M9b part 2b: level-up
        /// cards are answered after 0.4 s with the first card (the server waits for the pick, as a voyage does).</summary>
        void Steer(float t)
        {
            var offer = Client != null ? Client.PendingOffer : null;
            // V5 M9b review: -rpDropDuringCards drops the socket once while the first cards are up; the pick waits for the
            // resume (the server sends the cards again after its welcome).
            if (_dropDuringCards && !_droppedDuringCards && offer != null && Time.realtimeSinceStartup - Client.PendingOfferSince > 0.2f)
            {
                _droppedDuringCards = true;
                _welcomesAtCardsDrop = Client.Welcomes;
                _cardsDropAt = Time.realtimeSinceStartup;
                Client.DropForTest();
                Log($"socket dropped while the cards were up (set {offer.seq})");
                return;
            }
            if (_cardsDropAt >= 0f && _cardsResumeSeconds < 0f)
            {
                if (Client.Welcomes <= _welcomesAtCardsDrop || !Client.Connected) return;
                _cardsResumeSeconds = Time.realtimeSinceStartup - _cardsDropAt;
                Log($"resumed during the cards in {_cardsResumeSeconds:0.000} s");
            }
            if (_cardsResumeSeconds >= 0f && !_cardsBackAfterResume && offer != null)
            {
                _cardsBackAfterResume = true;
                Log($"the cards came back after the resume (set {offer.seq})");
            }
            if (AwayOrHidden(offer)) return;
            if (HiddenAfterPick()) return;
            if (offer != null && offer.seq != _pickedSeq && Time.realtimeSinceStartup - Client.PendingOfferSince > 0.4f && Client.SendPick(NetProtocol.PickAction.Pick, 0))
            {
                _pickedSeq = offer.seq;
                _cardsPicked++;
                Log($"level-up card picked ({offer.offers[0].Kind} {offer.offers[0].Id}, set {offer.seq})");
                if (_hideAfterPick > 0f && !_pickHideStarted && (_awayDuringCards <= 0f || _awayDone) && (_hiddenDuringCards <= 0f || _hiddenDone) &&
                    (!_dropDuringCards || _cardsBackAfterResume))
                {
                    _pickHideStarted = _pickHidden = true;
                    _pickHideUntil = Time.realtimeSinceStartup + _hideAfterPick;
                    _pickHideResyncsAt = Client.Resyncs;
                    Client.ThrottleForTest(_hideAfterPick);
                    Log($"page hidden at once after the pick for {_hideAfterPick:0} s (set {offer.seq})");
                    return;
                }
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
