using System.Collections.Concurrent;
using System.Text.Json;

namespace RoguePirates.Api.Playtest;

/// <summary>One seat of a relayed match: the last state its client reported, damage tallies and rate limits.</summary>
public sealed class RelaySeat
{
    public required int Index { get; init; }
    public required Guid PlayerId { get; init; }
    public required bool IsBot { get; init; }
    public required RateBucket StateRate { get; init; }
    public required RateBucket FxRate { get; init; }
    public required RateBucket HitRate { get; init; }
    public required RateBucket PickupRate { get; init; }
    public double X, Z, Yaw;
    public double? Hp, MaxHp;
    public int Level = 1;
    public bool Alive = true;
    public double DamageDealt, DamageTaken;
    public int BarrelsTaken;
    // V5 M8z (6 Oct, owner): anti-cheat. Only XP barrels level a PvP ship, so 1 + XpBarrels is the server's own level.
    public int XpBarrels;
    public DateTime StateAt;
    /// <summary>V5 M8z (6 Oct, audit): the raw damage this seat may still deal (fills by its level from the match start).</summary>
    public required DamageBudget Budget { get; init; }
    /// <summary>V5 M8z (6 Oct, audit): where it was over the last 45 s (what it left behind can still hit).</summary>
    public readonly PositionTrail Trail = new();
    /// <summary>V5 M8z (6 Oct, edge-case audit): X and Z are the speed-checked position (<see cref="Speed"/>); this is what
    /// the client last reported (forwarded to the opponent, whose client draws and hits it there).</summary>
    public double ReportedX, ReportedZ;
    public required SpeedCheck Speed { get; init; }
    /// <summary>V5 M8z (6 Oct, edge-case audit): the seat's game clock in seconds (<see cref="HitRules.Heard"/>): it runs
    /// while its client is sending and stands still while its game is stalled.</summary>
    public double Clock;
    public DateTime HeardAt;
    /// <summary>Its <see cref="Clock"/> the last time the two ships were within reach (this seat's attacks can still land).</summary>
    public double EngagedClock = double.NegativeInfinity;
    /// <summary>Its <see cref="Clock"/> at its last hit that passed <see cref="HitRules.InReach"/>, and at its last accepted
    /// full batch (<see cref="ArenaRules.MaxHitDamage"/>): the rest of a burst is carried in the batches after it.</summary>
    public double ReachClock = double.NegativeInfinity, LastHitFullAt = double.NegativeInfinity;
    public double RejectedDamage;
    public int RejectedHits;
    public bool Flagged;
    public int ServerLevel => 1 + XpBarrels;
    /// <summary>When the player's last socket closed (or match creation if they never connected); null while connected.</summary>
    public DateTime? AwaySince;
    public bool AnnouncedAway;
    public DraftDto? Draft;

    public double HpShare => Hp is { } hp && MaxHp is { } max && max > 0 ? Math.Clamp(hp / max, 0, 1) : 1;
}

public sealed class RelayBarrel
{
    public required int Id { get; init; }
    public required string Kind { get; init; }
    public required double X { get; init; }
    public required double Z { get; init; }
    public required DateTime SpawnedAt { get; init; }
    public DateTime ExpiresAt => SpawnedAt + ArenaRules.BarrelLifetime;
    public Guid? TakenBy;
}

/// <summary>In-memory state of one relayed 1v1 match. Every field is guarded by <c>lock (room)</c>.</summary>
public sealed class MatchRoom
{
    public required Guid LobbyId { get; init; }
    public required Guid MatchId { get; init; }
    public required long Seed { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required RelaySeat[] Seats { get; init; }
    public required object[] PlayerViews { get; init; }
    public required Random Rng { get; init; }
    public DateTime StartAt => CreatedAt + ArenaRules.Countdown;
    public DateTime EndAt => StartAt + ArenaRules.MatchLength;
    public bool GuestIsBot => Seats[1].IsBot;
    public Guid HostId => Seats[0].PlayerId;

    /// <summary>True once a player's socket was seen during the match: from then on the server referees it.</summary>
    public bool Relayed;
    public bool InMatchMarked;
    public bool Ended;
    public DateTime EndedAt;
    public object? EndMessage;
    public readonly Dictionary<int, RelayBarrel> Barrels = new();
    public int NextBarrelId = 1;
    public DateTime NextXpAt;
    public DateTime NextHealthAt;
    public readonly HashSet<Guid> RematchRequests = new();
    public bool RematchCreating;
    public Guid? RematchLobbyId;
    public string? RematchCode;

    public int? SeatOf(Guid playerId) =>
        Seats[0].PlayerId == playerId ? 0 : !Seats[1].IsBot && Seats[1].PlayerId == playerId ? 1 : null;
}

public sealed record RelayEnd(Guid? Winner, string Reason, bool Rated, int DurationSeconds,
    Dictionary<string, PlayerMatchStats> Stats, Dictionary<string, object> StatsView);

/// <summary>
/// Relay and referee for 1v1 matches played on the players' own clients: forwards "state"/"fx", validates and tallies
/// "hit", spawns and awards barrels, runs the countdown, storm clock and the end of the match (sunk, time, forfeit),
/// then writes the result and Elo through <see cref="MatchService"/>. Single instance; state lives in memory.
/// </summary>
public sealed class MatchRelay
{
    readonly ConcurrentDictionary<Guid, MatchRoom> _rooms = new();
    readonly IClock _clock;
    readonly LobbyBroadcaster _broadcaster;
    readonly IServiceScopeFactory _scopes;
    readonly ILogger<MatchRelay> _log;

    public static readonly TimeSpan UnrelayedRoomLifetime = TimeSpan.FromMinutes(10);

    public MatchRelay(IClock clock, LobbyBroadcaster broadcaster, IServiceScopeFactory scopes, ILogger<MatchRelay> log)
    {
        _clock = clock;
        _broadcaster = broadcaster;
        _scopes = scopes;
        _log = log;
    }

    readonly record struct Out(Guid? To, string Json);

    static string J(object o) => JsonSerializer.Serialize(o, LobbyService.Json);
    long NowMs => ArenaRules.Ms(_clock.UtcNow);

    public MatchRoom? Get(Guid lobbyId) => _rooms.TryGetValue(lobbyId, out var r) ? r : null;

    /// <summary>Creates the room when a relayed match starts (idempotent).</summary>
    public MatchRoom CreateRoom(Guid lobbyId, Guid matchId, long seed, DateTime createdAt, Guid hostId, Guid guestId,
        DraftDto? hostDraft, DraftDto? guestDraft, object[] playerViews)
    {
        return _rooms.GetOrAdd(lobbyId, _ =>
        {
            RelaySeat Seat(int index, Guid playerId, DraftDto? draft)
            {
                var (x, z, yaw) = ArenaRules.Spawn(index);
                var bot = PracticeBot.Is(playerId);
                var connected = !bot && _broadcaster.IsConnected(lobbyId, playerId);
                var seat = new RelaySeat
                {
                    Index = index, PlayerId = playerId, IsBot = bot, X = x, Z = z, Yaw = yaw, Draft = draft, StateAt = createdAt,
                    ReportedX = x, ReportedZ = z, HeardAt = createdAt + ArenaRules.Countdown,
                    Speed = new SpeedCheck(x, z, createdAt + ArenaRules.Countdown - HitRules.StartSkew),
                    Budget = new DamageBudget(createdAt + ArenaRules.Countdown),
                    StateRate = new RateBucket(ArenaRules.StateMessagesPerSecond, createdAt),
                    FxRate = new RateBucket(ArenaRules.FxMessagesPerSecond, createdAt),
                    HitRate = new RateBucket(ArenaRules.HitMessagesPerSecond, createdAt),
                    PickupRate = new RateBucket(ArenaRules.PickupMessagesPerSecond, createdAt),
                    AwaySince = bot || connected ? null : createdAt,
                };
                seat.Trail.Add(0, x, z);
                return seat;
            }

            var room = new MatchRoom
            {
                LobbyId = lobbyId, MatchId = matchId, Seed = seed, CreatedAt = createdAt,
                Seats = new[] { Seat(0, hostId, hostDraft), Seat(1, guestId, guestDraft) },
                PlayerViews = playerViews, Rng = new Random(unchecked((int)seed)),
            };
            room.NextXpAt = room.StartAt + ArenaRules.XpBarrelEvery;
            room.NextHealthAt = room.StartAt + ArenaRules.HealthBarrelEvery;
            room.Relayed = room.Seats.Any(s => !s.IsBot && s.AwaySince == null);
            return room;
        });
    }

    /// <summary>The old two-report result flow is replaced for matches the server referees.</summary>
    public bool IsRelayed(Guid matchId) => _rooms.Values.Any(r => r.MatchId == matchId && r.Relayed);

    /// <summary>A refereed match whose result is not in yet.</summary>
    public bool IsLive(Guid lobbyId) => Get(lobbyId) is { } r && r.Relayed && !r.Ended;

    /// <summary>The "match_end" of an ended room (sent again to a socket that reconnects after the end), or null.</summary>
    public string? EndJson(Guid lobbyId)
    {
        if (Get(lobbyId) is not { } room) return null;
        lock (room) return room.EndMessage is { } m ? J(m) : null;
    }

    /// <summary>Relayed lobbies keep their sockets open after "finished" for the rematch window.</summary>
    public bool KeepSocketsOpen(Guid lobbyId) => Get(lobbyId) is { Relayed: true };

    // ---------------------------------------------------------------- match start

    object StartMessage(MatchRoom room, int seat)
    {
        var bot = room.GuestIsBot;
        return new
        {
            t = "match_start",
            matchId = room.MatchId, lobbyId = room.LobbyId, seed = room.Seed,
            you = seat, youPlayerId = room.Seats[seat].PlayerId,
            serverNow = NowMs,
            countdownMs = (long)ArenaRules.Countdown.TotalMilliseconds,
            startsAt = ArenaRules.Ms(room.StartAt),
            endsAt = ArenaRules.Ms(room.EndAt),
            durationMs = (long)ArenaRules.MatchLength.TotalMilliseconds,
            arena = ArenaRules.ArenaView(),
            spawns = ArenaRules.SpawnsView(room.Seats[0].PlayerId, room.Seats[1].PlayerId),
            storm = ArenaRules.StormView(room.StartAt),
            barrels = ArenaRules.BarrelRulesView(),
            limits = ArenaRules.LimitsView(),
            players = room.PlayerViews,
            bot = bot ? new { seat = 1, playerId = PracticeBot.Id, simulatedBy = room.HostId } : null,
            live = new
            {
                ended = room.Ended,
                barrels = room.Barrels.Values.Where(b => b.TakenBy == null).OrderBy(b => b.Id).Select(BarrelSpawn).ToArray(),
                tallies = room.Seats.Select(s => new { playerId = s.PlayerId, damageDealt = s.DamageDealt, damageTaken = s.DamageTaken, barrelsTaken = s.BarrelsTaken }).ToArray(),
            },
        };
    }

    /// <summary>Sends "match_start" to each human seat (called when both seats are ready).</summary>
    public async Task AnnounceAsync(Guid lobbyId)
    {
        if (Get(lobbyId) is not { } room) return;
        var outs = new List<Out>();
        lock (room)
        {
            foreach (var s in room.Seats.Where(s => !s.IsBot))
                outs.Add(new Out(s.PlayerId, J(StartMessage(room, s.Index))));
        }
        await SendAsync(lobbyId, outs);
    }

    /// <summary>A socket connected: marks the player present and returns the "match_start" resync for them (null if no live room).</summary>
    public async Task<string?> OnConnectedAsync(Guid lobbyId, Guid playerId)
    {
        if (Get(lobbyId) is not { } room) return null;
        var outs = new List<Out>();
        string? start;
        lock (room)
        {
            if (room.SeatOf(playerId) is not { } seat) return null;
            var s = room.Seats[seat];
            if (!room.Ended)
            {
                room.Relayed = true;
                if (s.AnnouncedAway)
                    outs.Add(new Out(null, J(new { t = "presence", playerId, connected = true, forfeitAt = (long?)null })));
                s.AwaySince = null;
                s.AnnouncedAway = false;
            }
            start = J(StartMessage(room, seat));
        }
        await SendAsync(lobbyId, outs);
        return start;
    }

    // ---------------------------------------------------------------- client messages

    /// <summary>Handles one text message from a seated player's socket. Returns false when it is not a relay message
    /// (no JSON object with a string "t"); the caller then answers with the lobby state as before.</summary>
    public async Task<bool> HandleAsync(Guid lobbyId, Guid playerId, string text)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return false;
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("t", out var tEl) || tEl.ValueKind != JsonValueKind.String)
                return false;
            var t = tEl.GetString()!;

            if (t == "ping")
            {
                object? cs = root.TryGetProperty("cs", out var csEl) && csEl.ValueKind == JsonValueKind.Number ? csEl.GetDouble() : null;
                await _broadcaster.SendToPlayerAsync(lobbyId, playerId, J(new { t = "pong", cs, st = NowMs }));
                return true;
            }

            if (t == "rematch")
            {
                try
                {
                    await RequestRematchAsync(lobbyId, playerId);
                }
                catch (ApiException ex)
                {
                    await Reject(lobbyId, playerId, t, ex.Code);
                }
                return true;
            }

            if (Get(lobbyId) is not { } room)
            {
                await Reject(lobbyId, playerId, t, "no_match");
                return true;
            }

            var outs = new List<Out>();
            RelayEnd? end = null;
            lock (room)
            {
                end = Process(room, playerId, t, root, text, outs);
            }
            await SendAsync(lobbyId, outs);
            if (end != null) await FinishAsync(room, end);
            return true;
        }
    }

    Task Reject(Guid lobbyId, Guid playerId, string of, string reason, int? id = null) =>
        _broadcaster.SendToPlayerAsync(lobbyId, playerId, J(new { t = "reject", of, reason, id }));

    RelayEnd? Process(MatchRoom room, Guid playerId, string t, JsonElement root, string raw, List<Out> outs)
    {
        var now = _clock.UtcNow;
        void Rej(string reason, int? id = null) => outs.Add(new Out(playerId, J(new { t = "reject", of = t, reason, id })));

        if (room.SeatOf(playerId) is not { } senderSeat) { Rej("not_in_match"); return null; }
        room.Relayed = true;
        var asBot = root.TryGetProperty("asBot", out var ab) && ab.ValueKind == JsonValueKind.True;
        if (asBot && !(senderSeat == 0 && room.GuestIsBot)) { Rej("not_bot_host"); return null; }
        var actor = room.Seats[asBot ? 1 : senderSeat];
        var opponent = room.Seats[1 - actor.Index];
        var started = now >= room.StartAt;

        if (room.Ended)
        {
            if (t is "hit" or "pickup") Rej("match_over");
            return null;
        }
        // V5 M8z (6 Oct, edge-case audit): the actor's game is running (its engagement window runs on this clock)
        if (t is "state" or "hit" or "fx" or "pickup") HitRules.Heard(actor, now);

        switch (t)
        {
            case "state":
            {
                if (!actor.StateRate.Take(now)) return null; // over the cap: dropped silently
                if (!Num(root, "x", out var x) || !Num(root, "z", out var z) || Math.Abs(x) > 10000 || Math.Abs(z) > 10000) return null;
                // V5 M8z (6 Oct, edge-case audit): the referee reads the speed-checked position; the report is forwarded
                var (cx, cz, clamped) = actor.Speed.Report(now, x, z);
                if (clamped && started && actor.Speed.Clamps is 1 or 10 or 100)
                    _log.LogWarning("Anti-cheat: player {PlayerId} (bot {Bot}) in match {MatchId} reported a position beyond its speed ({Clamps} times)",
                        actor.PlayerId, actor.IsBot, room.MatchId, actor.Speed.Clamps);
                actor.X = cx;
                actor.Z = cz;
                actor.ReportedX = x;
                actor.ReportedZ = z;
                actor.StateAt = now;
                // V5 M8z (6 Oct, audit): the range check's memory (what the ship left behind, when the two last engaged):
                // each side's attacks count from its own checked position to the other's checked or reported one
                // (from the start: in the countdown the ships cannot move or fire, so a report there opens nothing)
                if (started)
                {
                    actor.Trail.Add(actor.Clock, cx, cz);
                    if (HitRules.Near(actor.X, actor.Z, opponent)) actor.EngagedClock = actor.Clock;
                    if (HitRules.Near(opponent.X, opponent.Z, actor)) opponent.EngagedClock = opponent.Clock;
                }
                actor.Yaw = Num(root, "yaw", out var yaw) ? yaw : actor.Yaw;
                var vx = Num(root, "vx", out var v1) ? v1 : 0;
                var vz = Num(root, "vz", out var v2) ? v2 : 0;
                if (Num(root, "maxHp", out var maxHp) && maxHp > 0) actor.MaxHp = maxHp;
                if (Num(root, "hp", out var hp)) actor.Hp = Math.Max(0, hp);
                if (Num(root, "level", out var level)) actor.Level = Math.Clamp((int)level, 1, RunService.MaxLevel);
                var alive = !(root.TryGetProperty("alive", out var al) && al.ValueKind == JsonValueKind.False);
                var boosting = root.TryGetProperty("boosting", out var bo) && bo.ValueKind == JsonValueKind.True;
                if (!opponent.IsBot && opponent.PlayerId != playerId)
                    outs.Add(new Out(opponent.PlayerId, J(new
                    {
                        t = "state", from = actor.PlayerId, st = ArenaRules.Ms(now), x, z, yaw = actor.Yaw, vx, vz,
                        hp = actor.Hp, maxHp = actor.MaxHp, level = actor.Level, alive, boosting,
                    })));
                if (!alive && started) return Sink(room, actor, now);
                return null;
            }
            case "fx":
            {
                if (System.Text.Encoding.UTF8.GetByteCount(raw) > ArenaRules.FxMaxBytes) { Rej("too_large"); return null; }
                if (!actor.FxRate.Take(now)) return null;
                if (!opponent.IsBot && opponent.PlayerId != playerId) outs.Add(new Out(opponent.PlayerId, raw));
                return null;
            }
            case "hit":
            {
                // V5 M8z (6 Oct, owner): every hit reject says how much raw damage was dropped, so the sender's own
                // "damage dealt" counts only what the server accepted
                var claimed = Num(root, "dmg", out var dmg) ? dmg : 0;
                void RejHit(string reason, double dropped) =>
                    outs.Add(new Out(playerId, J(new { t = "reject", of = t, reason, dropped = Math.Max(0, dropped), asBot = actor.IsBot })));
                if (!started) { RejHit("not_started", claimed); return null; }
                if (!actor.HitRate.Take(now)) { RejHit("rate", claimed); return null; }
                if (!actor.Alive) { RejHit("attacker_dead", claimed); return null; }
                if (!Num(root, "dmg", out _) || dmg < 0 || dmg > ArenaRules.MaxHitDamage) { RejHit("bad_damage", Math.Clamp(claimed, 0, ArenaRules.MaxHitDamage)); return null; }
                var weaponId = root.TryGetProperty("weaponId", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString()! : "";
                if (weaponId.Length is 0 or > 64) { RejHit("bad_weapon", dmg); return null; }

                // V5 M8z (6 Oct, owner): the anti-cheat checks; refused damage is counted against the sender
                string? refused = null;
                if (!HitRules.WeaponAllowed(actor, weaponId)) refused = "not_in_draft";
                else if (HitRules.InReach(actor, opponent)) actor.ReachClock = actor.Clock;
                else if (!HitRules.CarriedOn(actor)) refused = "out_of_range";
                // V5 M8z (6 Oct, edge-case audit): a full batch may have more of the same burst carried behind it
                actor.LastHitFullAt = refused == null && dmg >= ArenaRules.MaxHitDamage - 0.01 ? actor.Clock : double.NegativeInfinity;
                var accepted = refused == null ? actor.Budget.Accept(now, dmg, actor.ServerLevel) : 0;
                if (refused == null && accepted <= 0 && dmg > 0) refused = "dps_cap";
                var dropped = dmg - accepted;
                if (dropped > 1e-9)
                {
                    RejHit(refused ?? "dps_cap", dropped);
                    if (Refused(room, actor, dropped, now) is { } cheatEnd) return cheatEnd;
                }
                if (refused != null) return null;

                actor.DamageDealt += accepted;
                opponent.DamageTaken += accepted;
                // the victim's client applies the hit; a bot victim is simulated by the host
                var recipient = opponent.IsBot ? room.HostId : opponent.PlayerId;
                outs.Add(new Out(recipient, J(new { t = "hit", dmg = accepted, weaponId, from = actor.PlayerId, to = opponent.PlayerId, st = ArenaRules.Ms(now) })));
                return null;
            }
            case "pickup":
            {
                int? id = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var i) ? i : null;
                if (!started) { Rej("not_started", id); return null; }
                if (!actor.PickupRate.Take(now)) { Rej("rate", id); return null; }
                if (id == null || !room.Barrels.TryGetValue(id.Value, out var barrel) || barrel.ExpiresAt <= now) { Rej("gone", id); return null; }
                if (barrel.TakenBy != null) { Rej("taken", id); return null; }
                if (!actor.Alive) { Rej("dead", id); return null; }
                var dx = actor.X - barrel.X;
                var dz = actor.Z - barrel.Z;
                if (Math.Sqrt(dx * dx + dz * dz) > ArenaRules.ClaimRadius) { Rej("too_far", id); return null; }
                barrel.TakenBy = actor.PlayerId;
                actor.BarrelsTaken++;
                if (barrel.Kind == "xp") actor.XpBarrels++;
                outs.Add(new Out(null, J(new { t = "barrel_taken", id = barrel.Id, kind = barrel.Kind, by = actor.PlayerId, st = ArenaRules.Ms(now) })));
                return null;
            }
            case "dead":
                if (!started) { Rej("not_started"); return null; }
                return Sink(room, actor, now);
            case "leave":
                return End(room, 1 - senderSeat, "forfeit", now);
            default:
                Rej("unknown_type");
                return null;
        }
    }

    static bool Num(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetDouble(out value)) return false;
        return double.IsFinite(value);
    }

    /// <summary>V5 M8z (6 Oct, owner): counts refused damage; logs a seat once most of its damage is refused, and in a
    /// rated match forfeits it past <see cref="HitRules.ForfeitRejectedDamage"/>, <see cref="HitRules.ForfeitRejectedShare"/>
    /// and <see cref="HitRules.ForfeitRejectedHits"/> together (reason "anticheat").</summary>
    RelayEnd? Refused(MatchRoom room, RelaySeat actor, double dropped, DateTime now)
    {
        actor.RejectedDamage += dropped;
        actor.RejectedHits++;
        var share = actor.RejectedDamage / Math.Max(1e-9, actor.RejectedDamage + actor.DamageDealt);
        if (!actor.Flagged && actor.RejectedDamage >= HitRules.FlagRejectedDamage && share >= HitRules.FlagRejectedShare)
        {
            actor.Flagged = true;
            _log.LogWarning("Anti-cheat: player {PlayerId} (bot {Bot}) in match {MatchId}: {Rejected:0} raw damage refused in {Hits} hits, {Share:P0} of its claims",
                actor.PlayerId, actor.IsBot, room.MatchId, actor.RejectedDamage, actor.RejectedHits, share);
        }
        if (room.GuestIsBot || actor.RejectedDamage < HitRules.ForfeitRejectedDamage || share < HitRules.ForfeitRejectedShare
            || actor.RejectedHits < HitRules.ForfeitRejectedHits) return null;
        _log.LogWarning("Anti-cheat: player {PlayerId} forfeits match {MatchId} ({Rejected:0} raw damage refused, {Share:P0})",
            actor.PlayerId, room.MatchId, actor.RejectedDamage, share);
        return End(room, 1 - actor.Index, "anticheat", now);
    }

    RelayEnd Sink(MatchRoom room, RelaySeat loser, DateTime now)
    {
        loser.Alive = false;
        loser.Hp = 0;
        return End(room, 1 - loser.Index, "sunk", now);
    }

    // ---------------------------------------------------------------- end of match

    /// <summary>Ends the match (caller holds the room lock). <paramref name="winnerSeat"/> null: draw or abandoned.</summary>
    static RelayEnd End(MatchRoom room, int? winnerSeat, string reason, DateTime now)
    {
        room.Ended = true;
        room.EndedAt = now;
        var winner = winnerSeat is { } ws ? room.Seats[ws].PlayerId : (Guid?)null;
        // bot matches are unrated; so is a match both players abandoned
        var rated = !room.GuestIsBot && (winner != null || reason == "draw");
        var played = now - room.StartAt;
        var duration = (int)Math.Round(Math.Clamp(played.TotalSeconds, 0, ArenaRules.MatchLength.TotalSeconds));

        var stats = new Dictionary<string, PlayerMatchStats>();
        var view = new Dictionary<string, object>();
        foreach (var s in room.Seats)
        {
            var key = s.PlayerId.ToString();
            stats[key] = new PlayerMatchStats((long)Math.Round(s.DamageDealt), (long)Math.Round(s.DamageTaken), null, s.Level,
                s.Draft?.Weapons, s.Draft?.Logbooks, s.BarrelsTaken, s.Hp);
            view[key] = new
            {
                damageDealt = s.DamageDealt, damageTaken = s.DamageTaken, level = s.Level, barrelsTaken = s.BarrelsTaken,
                finalHp = s.Hp, maxHp = s.MaxHp, bot = s.IsBot,
            };
        }
        return new RelayEnd(winner, reason, rated, duration, stats, view);
    }

    /// <summary>V5 M8z (6 Oct, owner): DamageDealt counts only damage the server accepted (in the draft, in range, under
    /// the level's damage cap), so a client's claims cannot win on time.</summary>
    static RelayEnd EndOnTime(MatchRoom room, DateTime now)
    {
        RelaySeat a = room.Seats[0], b = room.Seats[1];
        if (Math.Abs(a.DamageDealt - b.DamageDealt) > 1e-9) return End(room, a.DamageDealt > b.DamageDealt ? 0 : 1, "time", now);
        if (Math.Abs(a.HpShare - b.HpShare) > 1e-9) return End(room, a.HpShare > b.HpShare ? 0 : 1, "time", now);
        return End(room, null, "draw", now);
    }

    /// <summary>Forfeits the player's seat (they left the lobby during a refereed match).</summary>
    public async Task ForfeitAsync(Guid lobbyId, Guid playerId)
    {
        if (Get(lobbyId) is not { } room) return;
        RelayEnd? end = null;
        lock (room)
        {
            if (!room.Ended && room.SeatOf(playerId) is { } seat) end = End(room, 1 - seat, "forfeit", _clock.UtcNow);
        }
        if (end != null) await FinishAsync(room, end);
    }

    async Task FinishAsync(MatchRoom room, RelayEnd end)
    {
        object? ratings = null;
        try
        {
            using var scope = _scopes.CreateScope();
            ratings = await scope.ServiceProvider.GetRequiredService<MatchService>().DecideByServerAsync(room.MatchId, end);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not record the relayed result of match {MatchId}", room.MatchId);
        }
        var msg = new
        {
            t = "match_end", matchId = room.MatchId, winnerPlayerId = end.Winner, reason = end.Reason, rated = end.Rated,
            durationSeconds = end.DurationSeconds, stats = end.StatsView, ratings,
            rematchUntil = ArenaRules.Ms(room.EndedAt + ArenaRules.RematchWindow),
        };
        lock (room) room.EndMessage = msg;
        await _broadcaster.PublishAsync(room.LobbyId, J(msg), close: false);
        // hackathon: settle the lobby's SOL wager, if any (in the background: payouts wait for chain confirmation)
        RoguePirates.Api.Hackathon.WagerService.Kick(_scopes, room.LobbyId, _log);
    }

    // ---------------------------------------------------------------- clock

    /// <summary>Advances every room to the current time: countdown, presence and forfeits, barrels, end on time, cleanup.</summary>
    public async Task TickAsync()
    {
        foreach (var room in _rooms.Values.ToArray())
        {
            try
            {
                await TickRoomAsync(room);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Relay tick failed for lobby {LobbyId}", room.LobbyId);
            }
        }
    }

    async Task TickRoomAsync(MatchRoom room)
    {
        var now = _clock.UtcNow;
        var outs = new List<Out>();
        RelayEnd? end = null;
        bool markInMatch = false, remove = false, closeSockets = false;
        lock (room)
        {
            if (room.Ended)
            {
                if (now >= room.EndedAt + ArenaRules.RematchWindow) remove = closeSockets = true;
            }
            else
            {
                if (!room.InMatchMarked && now >= room.StartAt)
                {
                    room.InMatchMarked = true;
                    markInMatch = true;
                }

                var humans = room.Seats.Where(s => !s.IsBot).ToArray();
                foreach (var s in humans)
                {
                    if (_broadcaster.IsConnected(room.LobbyId, s.PlayerId))
                    {
                        room.Relayed = true;
                        if (s.AnnouncedAway)
                            outs.Add(new Out(null, J(new { t = "presence", playerId = s.PlayerId, connected = true, forfeitAt = (long?)null })));
                        s.AwaySince = null;
                        s.AnnouncedAway = false;
                    }
                    else s.AwaySince ??= now;
                }

                if (room.Relayed)
                {
                    foreach (var s in humans.Where(s => s.AwaySince != null && !s.AnnouncedAway))
                    {
                        s.AnnouncedAway = true;
                        outs.Add(new Out(null, J(new
                        {
                            t = "presence", playerId = s.PlayerId, connected = false,
                            forfeitAt = (long?)ArenaRules.Ms(s.AwaySince!.Value + ArenaRules.DisconnectForfeit),
                        })));
                    }
                    var gone = humans.Where(s => s.AwaySince is { } since && now - since > ArenaRules.DisconnectForfeit).ToArray();
                    if (gone.Length == humans.Length) end = End(room, null, "forfeit", now); // abandoned: no winner, unrated
                    else if (gone.Length == 1) end = End(room, 1 - gone[0].Index, "forfeit", now);
                    else
                    {
                        SpawnAndExpireBarrels(room, now, outs);
                        if (now >= room.EndAt) end = EndOnTime(room, now);
                    }
                }
                else if (now >= room.EndAt + UnrelayedRoomLifetime)
                    remove = true; // nobody ever connected: the old two-report flow decides this match
            }
        }

        if (markInMatch)
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LobbyService>().MarkInMatchAsync(room.LobbyId);
        }
        await SendAsync(room.LobbyId, outs);
        if (end != null) await FinishAsync(room, end);
        if (remove)
        {
            _rooms.TryRemove(room.LobbyId, out _);
            if (closeSockets) await _broadcaster.CloseAllAsync(room.LobbyId);
        }
    }

    static object BarrelSpawn(RelayBarrel b) => new
    {
        t = "barrel_spawn", id = b.Id, kind = b.Kind, x = b.X, z = b.Z,
        st = ArenaRules.Ms(b.SpawnedAt), expiresAt = ArenaRules.Ms(b.ExpiresAt),
    };

    static void SpawnAndExpireBarrels(MatchRoom room, DateTime now, List<Out> outs)
    {
        if (now < room.StartAt) return;
        void Spawn(string kind, DateTime at)
        {
            var radius = ArenaRules.StormRadius(at - room.StartAt);
            var (x, z) = ArenaRules.PlaceBarrel(room.Rng, (room.Seats[0].X, room.Seats[0].Z), (room.Seats[1].X, room.Seats[1].Z), radius);
            var barrel = new RelayBarrel { Id = room.NextBarrelId++, Kind = kind, X = x, Z = z, SpawnedAt = at };
            room.Barrels[barrel.Id] = barrel;
            outs.Add(new Out(null, J(BarrelSpawn(barrel))));
        }

        // catch up in time order if a tick was late
        while (true)
        {
            var next = room.NextXpAt <= room.NextHealthAt ? room.NextXpAt : room.NextHealthAt;
            if (next > now || next >= room.EndAt) break;
            if (room.NextXpAt == next)
            {
                Spawn("xp", next);
                room.NextXpAt += ArenaRules.XpBarrelEvery;
            }
            else
            {
                Spawn("health", next);
                room.NextHealthAt += ArenaRules.HealthBarrelEvery;
            }
        }

        foreach (var b in room.Barrels.Values.Where(b => b.ExpiresAt <= now).ToList())
        {
            room.Barrels.Remove(b.Id);
            if (b.TakenBy == null) outs.Add(new Out(null, J(new { t = "barrel_gone", id = b.Id })));
        }
    }

    // ---------------------------------------------------------------- rematch

    /// <summary>Records a rematch request; when both players asked (a bot always agrees) a new lobby with the same two
    /// players is created and announced with "rematch" {status: "created", lobbyId, code}.</summary>
    public async Task<object> RequestRematchAsync(Guid lobbyId, Guid playerId)
    {
        if (Get(lobbyId) is not { } room) throw new ApiException(409, "no_rematch", "This match can no longer be rematched.");
        bool create;
        Guid waitingFor;
        lock (room)
        {
            if (room.SeatOf(playerId) is not { } seat) throw new ApiException(403, "not_in_match", "You did not play in this match.");
            if (!room.Ended) throw new ApiException(409, "match_not_over", "The match is still running.");
            if (room.RematchLobbyId is { } existing)
                return new { status = "created", lobbyId = existing, code = room.RematchCode };
            room.RematchRequests.Add(playerId);
            if (room.GuestIsBot) room.RematchRequests.Add(PracticeBot.Id);
            waitingFor = room.Seats[1 - seat].PlayerId;
            create = room.RematchRequests.Count >= 2 && !room.RematchCreating;
            if (create) room.RematchCreating = true;
        }

        if (!create)
        {
            await _broadcaster.PublishAsync(lobbyId, J(new { t = "rematch", status = "requested", by = playerId }), close: false);
            return new { status = "requested", by = playerId, waitingFor };
        }

        Guid newId;
        string code;
        try
        {
            using var scope = _scopes.CreateScope();
            (newId, code) = await scope.ServiceProvider.GetRequiredService<LobbyService>().CreateRematchAsync(lobbyId);
            lock (room)
            {
                room.RematchLobbyId = newId;
                room.RematchCode = code;
                room.RematchCreating = false;
            }
        }
        catch (Exception ex)
        {
            // V5 M8z (6 Oct, owner): a failed creation must not block the rematch for good: both requests stay, the
            // flag is cleared and both players are told; the next "rematch" from either of them tries again
            lock (room) room.RematchCreating = false;
            _log.LogError(ex, "Could not create the rematch of lobby {LobbyId}", lobbyId);
            await _broadcaster.PublishAsync(lobbyId, J(new { t = "rematch", status = "failed", reason = ex is ApiException api ? api.Code : "server_error" }), close: false);
            if (ex is ApiException) throw;
            throw new ApiException(503, "rematch_failed", "The rematch could not be created; try again.");
        }
        await _broadcaster.PublishAsync(lobbyId, J(new { t = "rematch", status = "created", lobbyId = newId, code }), close: false);
        return new { status = "created", lobbyId = newId, code };
    }

    async Task SendAsync(Guid lobbyId, List<Out> outs)
    {
        foreach (var o in outs)
        {
            if (o.To is { } to) await _broadcaster.SendToPlayerAsync(lobbyId, to, o.Json);
            else await _broadcaster.PublishAsync(lobbyId, o.Json, close: false);
        }
    }
}

/// <summary>Drives <see cref="MatchRelay.TickAsync"/> (every ARENA_TICK_MS, default 100 ms).</summary>
public sealed class ArenaTicker : BackgroundService
{
    readonly MatchRelay _relay;
    readonly ApiSettings _settings;
    readonly ILogger<ArenaTicker> _log;

    public ArenaTicker(MatchRelay relay, ApiSettings settings, ILogger<ArenaTicker> log)
    {
        _relay = relay;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.ArenaTickMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _relay.TickAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Arena tick failed");
            }
        }
    }
}
