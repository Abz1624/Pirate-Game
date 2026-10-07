using System.Collections.Generic;
using System.Globalization;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Scenarios
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): the server's 1v1 relay in process, for a scenario against the practice bot (no server, no
    /// network). It is the lobby socket <see cref="PvpSession"/> talks through (<see cref="PvpSession.UseRelay"/>) and
    /// answers as server/README.md's match protocol does for a bot match: the bot is simulated by this client (asBot), a
    /// "hit" is relayed to the other player, "dead" ends the match (sunk), the clock ends it at endsAt (time), "leave"
    /// forfeits, pings are answered on this machine's clock. No barrels are dropped and nothing is checked (the server's
    /// range and damage rules have their own tests). V5 M8z (7 Oct, review): <see cref="Drop"/> and <see cref="Reconnect"/>
    /// play a lost connection and the server's answer to the reconnect (the match start again, then the stored result).
    /// </summary>
    public sealed class FakePvpRelay : ILobbySocket
    {
        public const string PlayerId = "scenario-player";
        public const string BotId = "scenario-bot";

        readonly Queue<SocketEvent> _events = new Queue<SocketEvent>();
        readonly long _countdownMs, _durationMs, _stormDelayMs;
        bool _clockStarted;
        float _myHp = 1f, _myMaxHp = 1f, _botHp = 1f, _botMaxHp = 1f;
        int _myLevel = 1, _botLevel = 1;

        public MatchStartMsg Match { get; }
        public LobbyState Lobby { get; }
        public bool IsOpen { get; private set; } = true;
        public bool Ended { get; private set; }
        public string EndReason { get; private set; }
        public string Winner { get; private set; }
        /// <summary>Messages the client sent, by type (tests read them).</summary>
        public readonly Dictionary<string, int> Received = new Dictionary<string, int>();
        public float PlayerDealt { get; private set; }
        public float BotDealt { get; private set; }

        public FakePvpRelay(int seed, ScenarioPvp pvp)
        {
            _countdownMs = (long)(Mathf.Max(0f, pvp.countdownSeconds) * 1000f);
            _durationMs = (long)(Mathf.Max(1f, pvp.matchSeconds) * 1000f);
            _stormDelayMs = pvp.stormStartSeconds > 0f ? (long)(pvp.stormStartSeconds * 1000f) : 0;
            var now = PvpSession.UtcNowMs;
            var draft = new PvpDraft { weapons = pvp.weapons.ToArray(), logbooks = pvp.logbooks.ToArray() };
            Match = new MatchStartMsg
            {
                t = "match_start", matchId = "scenario-" + seed.ToString(CultureInfo.InvariantCulture), lobbyId = "scenario", seed = seed,
                you = 0, youPlayerId = PlayerId, serverNow = now, countdownMs = _countdownMs,
                // The countdown runs from when the run scene is up (StartClock), not from the scene load.
                startsAt = long.MaxValue / 4, endsAt = long.MaxValue / 2, durationMs = _durationMs,
                arena = new ArenaInfo { radius = 70f },
                spawns = new[]
                {
                    new SpawnInfo { seat = 0, playerId = PlayerId, x = -22f, z = 0f, yaw = 90f },
                    new SpawnInfo { seat = 1, playerId = BotId, x = 22f, z = 0f, yaw = 270f },
                },
                storm = new StormInfo { shrinkStartMs = long.MaxValue / 4, shrinkEndMs = long.MaxValue / 2, startRadius = 70f, endRadius = 25f, damagePerSecond = 6f, damageGrowthPerSecond = 2f },
                barrels = new BarrelRules { xpEveryMs = long.MaxValue / 4, healthEveryMs = long.MaxValue / 4, firstXpMs = long.MaxValue / 4, firstHealthMs = long.MaxValue / 4, ttlMs = 20000, claimRadius = 6f, healthHealFraction = 0.2f },
                players = new[]
                {
                    new MatchPlayer { seat = 0, playerId = PlayerId, name = "Scenario", rating = 1000, draft = draft },
                    new MatchPlayer { seat = 1, playerId = BotId, name = "Practice Bot", rating = 1000, bot = true, draft = new PvpDraft { weapons = new string[0], logbooks = new string[0] } },
                },
                bot = new BotInfo { seat = 1, playerId = BotId, simulatedBy = PlayerId },
                limits = new MatchLimits { maxHitDamage = 500f, maxHitsPerSecond = 20 },
            };
            Lobby = new LobbyState
            {
                t = "lobby", lobbyId = "scenario", code = "SCENE", status = "playing", hostPlayerId = PlayerId, guestPlayerId = BotId,
                seats = new[]
                {
                    new LobbySeat { seat = "host", playerId = PlayerId, name = "Scenario", status = "ready", ready = true, hasDraft = true, draft = draft },
                    new LobbySeat { seat = "guest", playerId = BotId, name = "Practice Bot", bot = true, status = "ready", ready = true, hasDraft = true },
                },
                match = new LobbyMatch { matchId = Match.matchId, seed = seed, hostMode = "relay", hostPlayerId = PlayerId, botSeat = 1 },
            };
        }

        /// <summary>The run is up: the countdown starts now, the fight ends matchSeconds after it.</summary>
        public void StartClock()
        {
            if (_clockStarted) return;
            _clockStarted = true;
            var now = PvpSession.UtcNowMs;
            Match.serverNow = now;
            Match.startsAt = now + _countdownMs;
            Match.endsAt = Match.startsAt + _durationMs;
            if (_stormDelayMs > 0)
            {
                Match.storm.shrinkStartMs = Match.startsAt + _stormDelayMs;
                Match.storm.shrinkEndMs = Match.storm.shrinkStartMs + 20000;
            }
        }

        public void Connect(string url) => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Dispose() => IsOpen = false;

        // V5 M8z (7 Oct, review): a dropped socket and the reconnect, as the server's relay answers it.
        bool _dropped;
        string _endJson;

        /// <summary>The connection drops (the client sees the socket close): the match goes on without this client, and
        /// what the relay sends meanwhile (the end on time) never arrives.</summary>
        public void Drop()
        {
            if (_dropped) return;
            _dropped = true;
            IsOpen = false;
            _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Closed });
        }

        /// <summary>
        /// The client is back: as LobbyService.AttachAsync does, the socket opens, the match's "match_start" comes again
        /// (live.ended when it ended meanwhile), then the stored "match_end" when there is one.
        /// </summary>
        public void Reconnect()
        {
            if (!_dropped) return;
            _dropped = false;
            IsOpen = true;
            _events.Clear(); // what was sent while the socket was down is lost
            _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Opened });
            Match.live = new LiveInfo { ended = Ended, barrels = new BarrelMsg[0] };
            Push(JsonUtility.ToJson(Match));
            if (_endJson != null) Push(_endJson);
        }

        public bool Poll(out SocketEvent e)
        {
            if (_clockStarted && !Ended && PvpSession.UtcNowMs >= Match.endsAt)
            {
                var winner = TimeWinner();
                EndMatch(winner, winner == null ? "draw" : "time"); // V5 M8z (7 Oct, review): as the server (MatchRelay.EndOnTime)
            }
            if (_dropped)
            {
                // Only the close reaches the client while the socket is down.
                while (_events.Count > 0 && _events.Peek().Kind != SocketEventKind.Closed) _events.Dequeue();
            }
            if (_events.Count > 0) { e = _events.Dequeue(); return true; }
            e = default;
            return false;
        }

        public void Send(string text)
        {
            if (!IsOpen) return;
            var t = PvpJson.TypeOf(text);
            if (t == null) return;
            Received.TryGetValue(t, out var n);
            Received[t] = n + 1;
            switch (t)
            {
                case "ping":
                {
                    var ping = JsonUtility.FromJson<PingMsg>(text);
                    Push(JsonUtility.ToJson(new PongMsg { t = "pong", cs = ping.cs, st = PvpSession.UtcNowMs }));
                    return;
                }
                case "state":
                {
                    var s = JsonUtility.FromJson<StateMsg>(text);
                    if (s.asBot) { _botHp = s.hp; _botMaxHp = Mathf.Max(1f, s.maxHp); _botLevel = s.level; }
                    else { _myHp = s.hp; _myMaxHp = Mathf.Max(1f, s.maxHp); _myLevel = s.level; }
                    return; // the opponent is the bot on this client: nothing to relay
                }
                case "hit":
                {
                    if (Ended) return;
                    var h = JsonUtility.FromJson<HitMsg>(text);
                    if (h.asBot) BotDealt += h.dmg; else PlayerDealt += h.dmg;
                    Push(JsonUtility.ToJson(new HitMsg
                    {
                        dmg = h.dmg, weaponId = h.weaponId, from = h.asBot ? BotId : PlayerId, to = h.asBot ? PlayerId : BotId, st = PvpSession.UtcNowMs, asBot = h.asBot,
                    }));
                    return;
                }
                case "dead":
                    if (!Ended) EndMatch(JsonUtility.FromJson<PvpMsg>(text).asBot ? PlayerId : BotId, "sunk");
                    return;
                case "leave":
                    if (!Ended) EndMatch(BotId, "forfeit");
                    return;
                case "rematch":
                    Push("{\"t\":\"rematch\",\"status\":\"failed\",\"reason\":\"scenario relay\"}");
                    return;
            }
        }

        string TimeWinner()
        {
            var mine = _myHp / Mathf.Max(1f, _myMaxHp);
            var bot = _botHp / Mathf.Max(1f, _botMaxHp);
            return Mathf.Approximately(mine, bot) ? null : mine > bot ? PlayerId : BotId;
        }

        void EndMatch(string winner, string reason)
        {
            Ended = true;
            Winner = winner;
            EndReason = reason;
            var inv = CultureInfo.InvariantCulture;
            string Stats(float dealt, float taken, int level, float hp, float max, bool bot) =>
                "{\"damageDealt\":" + dealt.ToString("0.##", inv) + ",\"damageTaken\":" + taken.ToString("0.##", inv) + ",\"level\":" + level.ToString(inv) +
                ",\"barrelsTaken\":0,\"finalHp\":" + hp.ToString("0.##", inv) + ",\"maxHp\":" + max.ToString("0.##", inv) + ",\"bot\":" + (bot ? "true" : "false") + "}";
            var duration = (int)Mathf.Max(0f, (PvpSession.UtcNowMs - Match.startsAt) / 1000f);
            _endJson = "{\"t\":\"match_end\",\"matchId\":\"" + Match.matchId + "\",\"winnerPlayerId\":" + (winner == null ? "null" : "\"" + winner + "\"") +
                       ",\"reason\":\"" + reason + "\",\"rated\":false,\"durationSeconds\":" + duration.ToString(inv) +
                       ",\"stats\":{\"" + PlayerId + "\":" + Stats(PlayerDealt, BotDealt, _myLevel, _myHp, _myMaxHp, false) +
                       ",\"" + BotId + "\":" + Stats(BotDealt, PlayerDealt, _botLevel, _botHp, _botMaxHp, true) + "}" +
                       ",\"rematchUntil\":" + (PvpSession.UtcNowMs + 60000).ToString(inv) + "}";
            Push(_endJson);
        }

        void Push(string json) => _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Message, Text = json });
    }
}
