using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>One player's lobby WebSocket: send relay messages, read until a message type arrives.</summary>
public sealed class Peer : IDisposable
{
    public WebSocket Ws { get; }
    public string LastRaw { get; private set; } = "";

    public Peer(WebSocket ws) => Ws = ws;

    public Task Send(object message) => SendRaw(JsonSerializer.Serialize(message));

    public Task SendRaw(string text) =>
        Ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public async Task<JsonElement> Receive()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            r = await Ws.ReceiveAsync(buffer, cts.Token);
            if (r.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("socket closed");
            ms.Write(buffer, 0, r.Count);
        } while (!r.EndOfMessage);
        LastRaw = Encoding.UTF8.GetString(ms.ToArray());
        return JsonDocument.Parse(LastRaw).RootElement.Clone();
    }

    /// <summary>Reads until a message of type <paramref name="t"/> (matching <paramref name="where"/>); returns it and the messages before it.</summary>
    public async Task<(JsonElement match, List<JsonElement> before)> Until(string t, Func<JsonElement, bool>? where = null)
    {
        var before = new List<JsonElement>();
        while (true)
        {
            var msg = await Receive();
            if (msg.GetProperty("t").GetString() == t && (where == null || where(msg))) return (msg, before);
            before.Add(msg);
        }
    }

    public async Task<JsonElement> Next(string t, Func<JsonElement, bool>? where = null) => (await Until(t, where)).match;

    /// <summary>Round trip: once the pong is back, every message sent before it has been processed.</summary>
    public async Task<List<JsonElement>> Sync()
    {
        await Send(new { t = "ping" });
        return (await Until("pong")).before;
    }

    public async Task ExpectClosed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var r = await Ws.ReceiveAsync(buffer, cts.Token);
            if (r.MessageType == WebSocketMessageType.Close) return;
        }
    }

    public void Dispose() => Ws.Dispose();
}

public class ArenaRulesTests
{
    [Theory]
    [InlineData(0, 120)]
    [InlineData(150, 120)]
    [InlineData(225, 72.5)]
    [InlineData(300, 25)]
    [InlineData(400, 25)]
    public void Storm_ShrinksLinearlyFrom2m30To5m(double seconds, double radius) =>
        Assert.Equal(radius, ArenaRules.StormRadius(TimeSpan.FromSeconds(seconds)), 6);

    [Fact]
    public void Spawns_AreMirroredAndFaceTheCentre()
    {
        Assert.Equal((-45.0, 0.0, 90.0), ArenaRules.Spawn(0));
        Assert.Equal((45.0, 0.0, 270.0), ArenaRules.Spawn(1));
    }

    [Fact]
    public void BarrelPlacement_IsEquidistant_InsideTheRing_AndKeepsDistance()
    {
        var rng = new Random(7);
        for (var i = 0; i < 2000; i++)
        {
            var radius = 25 + rng.NextDouble() * 95;
            (double x, double z) Point()
            {
                var a = rng.NextDouble() * Math.PI * 2;
                var d = Math.Sqrt(rng.NextDouble()) * radius;
                return (d * Math.Cos(a), d * Math.Sin(a));
            }
            var a = Point();
            var b = i % 50 == 0 ? a : Point(); // sometimes on top of each other
            var p = ArenaRules.PlaceBarrel(rng, a, b, radius);
            double da = Dist(p, a), db = Dist(p, b);
            Assert.True(Math.Abs(da - db) < 1e-6, $"not equidistant: {da} vs {db}");
            Assert.True(Dist(p, (0, 0)) <= radius - ArenaRules.BarrelRingMargin + 1e-6, "outside the ring");
            // 15 m whenever the ring has room for it: two ships within the 25 m ring may make it impossible
            if (radius >= 60) Assert.True(da >= ArenaRules.BarrelMinShipDistance - 1e-6, $"too close: {da}");
        }
    }

    [Fact]
    public void BarrelPlacement_MirroredShips_LandsOnTheCentreLine()
    {
        var p = ArenaRules.PlaceBarrel(new Random(1), (-45, 0), (45, 0), 120);
        Assert.Equal(0, p.x, 6);
        Assert.InRange(Math.Abs(p.z), 0, 117);
    }

    [Fact]
    public void Elo_Draw_MovesTowardTheUnderdog()
    {
        Assert.Equal((1000, 1000), Elo.Draw(1000, 1000));
        Assert.Equal((1192, 1008), Elo.Draw(1200, 1000)); // 32 * (0.5 - 0.76) = -8.3 -> -8
    }

    static double Dist((double x, double z) p, (double x, double z) q) => Math.Sqrt((p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z));
}

public class RelayTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    MatchRelay Relay => _api.Services.GetRequiredService<MatchRelay>();
    LobbyBroadcaster Broadcaster => _api.Services.GetRequiredService<LobbyBroadcaster>();

    sealed record Duel(TestPlayer Host, TestPlayer Guest, Guid LobbyId, Guid MatchId, Peer H, Peer G, JsonElement HostStart, JsonElement GuestStart, DateTime StartAt);

    async Task<Peer> Connect(Guid lobbyId, TestPlayer p)
    {
        var ws = await _api.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri($"ws://localhost/ws/lobbies/{lobbyId}?token={Uri.EscapeDataString(p.Token)}"), CancellationToken.None);
        return new Peer(ws);
    }

    async Task<Duel> StartDuel()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = false })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() });
        var h = await Connect(id, host);
        var g = await Connect(id, guest);
        await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("h") });
        var startAt = _api.Clock.UtcNow + ArenaRules.Countdown;
        await guest.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("g") });
        var hs = await h.Next("match_start");
        var gs = await g.Next("match_start");
        return new Duel(host, guest, id, hs.GetProperty("matchId").GetGuid(), h, g, hs, gs, startAt);
    }

    /// <summary>Sets the clock to <paramref name="seconds"/> of match time (after the countdown) and ticks the relay.</summary>
    async Task At(Duel d, double seconds, bool tick = true)
    {
        _api.Clock.UtcNow = d.StartAt + TimeSpan.FromSeconds(seconds);
        if (tick) await Relay.TickAsync();
    }

    static object State(double x, double z, double hp = 100, double maxHp = 100, bool alive = true, int level = 1, bool asBot = false) =>
        new { t = "state", x, z, yaw = 90, vx = 1.5, vz = 0, hp, maxHp, level, alive, boosting = false, asBot };

    static object Hit(double dmg, string? weaponId = "deck_cannons", bool asBot = false) => new { t = "hit", dmg, weaponId, asBot };

    async Task<JsonElement> GetJson(TestPlayer p, string url) => await (await p.Http.GetAsync(url)).ReadJson();

    [Fact]
    public async Task ReadyStartsCountdown_ThenInMatch_WithArenaSpawnsStormAndDrafts()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        var startsAt = ArenaRules.Ms(d.StartAt);

        var s = d.HostStart;
        Assert.Equal(0, s.GetProperty("you").GetInt32());
        Assert.Equal(1, d.GuestStart.GetProperty("you").GetInt32());
        Assert.Equal(startsAt, s.GetProperty("startsAt").GetInt64());
        Assert.Equal(startsAt + 300_000, s.GetProperty("endsAt").GetInt64());
        Assert.Equal((long)ArenaRules.Countdown.TotalMilliseconds, s.GetProperty("countdownMs").GetInt64());
        Assert.Equal(120, s.GetProperty("arena").GetProperty("radius").GetDouble());
        var spawns = s.GetProperty("spawns");
        Assert.Equal(-45, spawns[0].GetProperty("x").GetDouble());
        Assert.Equal(90, spawns[0].GetProperty("yaw").GetDouble());
        Assert.Equal(45, spawns[1].GetProperty("x").GetDouble());
        Assert.Equal(270, spawns[1].GetProperty("yaw").GetDouble());
        Assert.Equal(d.Guest.Id, spawns[1].GetProperty("playerId").GetGuid());
        var storm = s.GetProperty("storm");
        Assert.Equal(startsAt + 150_000, storm.GetProperty("shrinkStartMs").GetInt64());
        Assert.Equal(startsAt + 300_000, storm.GetProperty("shrinkEndMs").GetInt64());
        Assert.Equal(25, storm.GetProperty("endRadius").GetDouble());
        // both drafts, in priority order, for both players
        Assert.Equal("gw1", s.GetProperty("players")[1].GetProperty("draft").GetProperty("weapons")[0].GetString());
        Assert.Equal("hl6", d.GuestStart.GetProperty("players")[0].GetProperty("draft").GetProperty("logbooks")[5].GetString());

        var starting = await GetJson(d.Host, $"/api/lobbies/{d.LobbyId}");
        Assert.Equal("starting", starting.GetProperty("status").GetString());
        Assert.Equal("relay", starting.GetProperty("match").GetProperty("hostMode").GetString());
        Assert.Equal(startsAt, starting.GetProperty("match").GetProperty("startsAt").GetInt64());
        Assert.Equal(JsonValueKind.Null, starting.GetProperty("match").GetProperty("startedAt").ValueKind);
        Assert.Equal("gw1", starting.GetProperty("seats")[1].GetProperty("draft").GetProperty("weapons")[0].GetString());

        await At(d, -1); // 2 s into the countdown
        Assert.Equal("starting", (await GetJson(d.Host, $"/api/lobbies/{d.LobbyId}")).GetProperty("status").GetString());

        await At(d, 0);
        var pushed = await h.Next("lobby", m => m.GetProperty("status").GetString() == "in_match");
        Assert.Equal(startsAt, pushed.GetProperty("match").GetProperty("startedAt").GetInt64());
        Assert.Equal("in_match", (await GetJson(d.Guest, $"/api/lobbies/{d.LobbyId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task State_And_Fx_AreForwarded_WithServerTime_AndCapped()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;

        await h.Send(State(-40, 3, hp: 90, maxHp: 120, level: 4));
        var st = await g.Next("state");
        Assert.Equal(d.Host.Id, st.GetProperty("from").GetGuid());
        Assert.Equal(-40, st.GetProperty("x").GetDouble());
        Assert.Equal(90, st.GetProperty("hp").GetDouble());
        Assert.Equal(4, st.GetProperty("level").GetInt32());
        Assert.Equal(ArenaRules.Ms(_api.Clock.UtcNow), st.GetProperty("st").GetInt64());

        // a full bucket: 30 states in the same instant go through, the rest are dropped
        await At(d, 1, tick: false);
        for (var i = 0; i < 40; i++) await h.Send(State(-40 + i, 0));
        var fx = "{\"t\":\"fx\",\"kind\":\"fired\",\"weaponId\":\"mortar\",\"x\":1.5,\"z\":-2}";
        await h.SendRaw(fx);
        var (_, before) = await g.Until("fx");
        Assert.Equal(30, before.Count(m => m.GetProperty("t").GetString() == "state"));
        Assert.Equal(fx, g.LastRaw); // forwarded as-is

        // over 2 KB: rejected, not forwarded
        await h.SendRaw("{\"t\":\"fx\",\"pad\":\"" + new string('x', 2100) + "\"}");
        var rejects = await h.Sync();
        Assert.Contains(rejects, m => m.GetProperty("t").GetString() == "reject" && m.GetProperty("reason").GetString() == "too_large");
        await h.SendRaw("{\"t\":\"fx\",\"n\":2}");
        Assert.Equal(2, (await g.Next("fx")).GetProperty("n").GetInt32());

        // the bucket refills over time
        await At(d, 2, tick: false);
        await h.Send(State(0, 0));
        Assert.Equal(0, (await g.Next("state")).GetProperty("x").GetDouble());
    }

    [Fact]
    public async Task Hits_AreValidated_Tallied_Forwarded_AndTimeDecidesByDamage()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;

        await h.Send(Hit(10)); // countdown
        Assert.Equal("not_started", (await h.Next("reject")).GetProperty("reason").GetString());

        await At(d, 1);
        await h.Send(Hit(600));
        Assert.Equal("bad_damage", (await h.Next("reject")).GetProperty("reason").GetString());
        await h.Send(Hit(-1));
        Assert.Equal("bad_damage", (await h.Next("reject")).GetProperty("reason").GetString());
        await h.Send(Hit(5, weaponId: null));
        Assert.Equal("bad_weapon", (await h.Next("reject")).GetProperty("reason").GetString());

        await h.Send(Hit(40));
        var hit = await g.Next("hit");
        Assert.Equal(40, hit.GetProperty("dmg").GetDouble());
        Assert.Equal("deck_cannons", hit.GetProperty("weaponId").GetString());
        Assert.Equal(d.Host.Id, hit.GetProperty("from").GetGuid());
        Assert.Equal(d.Guest.Id, hit.GetProperty("to").GetGuid());
        await g.Send(Hit(15, "mortar"));
        Assert.Equal(15, (await h.Next("hit")).GetProperty("dmg").GetDouble());

        // rate cap: 20 hits per second (the bucket is full again 2 s later); the rest are rejected and not tallied
        await At(d, 3);
        for (var i = 0; i < 25; i++) await h.Send(Hit(1));
        var seen = await h.Sync();
        Assert.Equal(5, seen.Count(m => m.GetProperty("t").GetString() == "reject" && m.GetProperty("reason").GetString() == "rate"));

        // the victim reports its hp; a relayed match ignores the old result reports
        await g.Send(State(45, 0, hp: 40, maxHp: 100, level: 7));
        await g.Sync();
        var report = await d.Guest.Http.PostAsJsonAsync($"/api/matches/{d.MatchId}/result", new
        {
            winnerPlayerId = d.Guest.Id, durationSeconds = 300,
            stats = new Dictionary<string, object> { [d.Guest.Id.ToString()] = new { damageDealt = 999, damageTaken = 0, level = 7 } },
        });
        Assert.Equal("pending", (await report.ReadJson()).GetProperty("status").GetString());

        await At(d, 299.9);
        Assert.Equal("pending", (await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson()).GetProperty("status").GetString());
        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal(end.GetRawText(), (await g.Next("match_end")).GetRawText());
        Assert.Equal("time", end.GetProperty("reason").GetString());
        Assert.Equal(d.Host.Id, end.GetProperty("winnerPlayerId").GetGuid());
        var hostStats = end.GetProperty("stats").GetProperty(d.Host.Id.ToString());
        var guestStats = end.GetProperty("stats").GetProperty(d.Guest.Id.ToString());
        Assert.Equal(60, hostStats.GetProperty("damageDealt").GetDouble()); // 40 + 20 x 1
        Assert.Equal(15, hostStats.GetProperty("damageTaken").GetDouble());
        Assert.Equal(60, guestStats.GetProperty("damageTaken").GetDouble());
        Assert.Equal(7, guestStats.GetProperty("level").GetInt32());
        Assert.Equal(40, guestStats.GetProperty("finalHp").GetDouble());
        Assert.Equal(16, end.GetProperty("ratings").GetProperty(d.Host.Id.ToString()).GetProperty("change").GetInt32());

        var view = await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson();
        Assert.Equal("decided", view.GetProperty("status").GetString());
        Assert.Equal("server_time", view.GetProperty("decidedBy").GetString());
        Assert.Equal("time", view.GetProperty("reason").GetString());
        Assert.Equal(300, view.GetProperty("durationSeconds").GetInt32());
        Assert.Equal(60, view.GetProperty("players")[0].GetProperty("stats").GetProperty("damageDealt").GetInt64());
        Assert.Equal(1016, (await GetJson(d.Host, "/api/profiles/me")).GetProperty("rating").GetInt32());
        Assert.Equal(984, (await GetJson(d.Guest, "/api/profiles/me")).GetProperty("rating").GetInt32());
        Assert.Equal("finished", (await GetJson(d.Host, $"/api/lobbies/{d.LobbyId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Time_EqualDamage_HpShareDecides_ElseDraw()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await h.Send(Hit(30));
        await g.Send(Hit(30));
        await h.Send(State(-45, 0, hp: 50, maxHp: 200)); // 25 %
        await g.Send(State(45, 0, hp: 30, maxHp: 100));  // 30 %
        await h.Sync();
        await g.Sync();
        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal("time", end.GetProperty("reason").GetString());
        Assert.Equal(d.Guest.Id, end.GetProperty("winnerPlayerId").GetGuid());

        var draw = await StartDuel();
        using var dh = draw.H;
        using var dg = draw.G;
        await At(draw, 300);
        var drawEnd = await dh.Next("match_end");
        Assert.Equal("draw", drawEnd.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, drawEnd.GetProperty("winnerPlayerId").ValueKind);
        var view = await (await _api.Anon().GetAsync($"/api/matches/{draw.MatchId}")).ReadJson();
        Assert.Equal("decided", view.GetProperty("status").GetString());
        Assert.Equal(0, view.GetProperty("players")[0].GetProperty("ratingChange").GetInt32());
        Assert.Equal(0, (await GetJson(draw.Host, "/api/profiles/me")).GetProperty("losses").GetInt32());
    }

    [Fact]
    public async Task Barrels_SpawnOnSchedule_FairlyPlaced_FirstValidClaimWins_AndDespawn()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await h.Send(State(-30, 20));
        await g.Send(State(40, -10));
        await h.Sync();
        await g.Sync();

        await At(d, 4.9);
        await At(d, 5);
        var (spawn, before) = await h.Until("barrel_spawn");
        Assert.DoesNotContain(before, m => m.GetProperty("t").GetString() == "barrel_spawn");
        Assert.Equal(1, spawn.GetProperty("id").GetInt32());
        Assert.Equal("xp", spawn.GetProperty("kind").GetString());
        Assert.Equal(ArenaRules.Ms(d.StartAt + TimeSpan.FromSeconds(25)), spawn.GetProperty("expiresAt").GetInt64());
        double bx = spawn.GetProperty("x").GetDouble(), bz = spawn.GetProperty("z").GetDouble();
        double da = Math.Sqrt((bx + 30) * (bx + 30) + (bz - 20) * (bz - 20));
        double db = Math.Sqrt((bx - 40) * (bx - 40) + (bz + 10) * (bz + 10));
        Assert.Equal(da, db, 6);
        Assert.True(da >= 15);
        Assert.True(Math.Sqrt(bx * bx + bz * bz) <= 117 + 1e-9);
        Assert.Equal(spawn.GetRawText(), (await g.Next("barrel_spawn")).GetRawText());

        await g.Send(new { t = "pickup", id = 1 });
        Assert.Equal("too_far", (await g.Next("reject")).GetProperty("reason").GetString());
        await h.Send(new { t = "pickup", id = 99 });
        Assert.Equal("gone", (await h.Next("reject")).GetProperty("reason").GetString());

        // both sail to the barrel; the host claims first (5.9 m away), the guest's claim then fails
        await h.Send(State(bx + 5.9, bz));
        await g.Send(State(bx, bz - 1));
        await h.Sync();
        await g.Sync();
        await h.Send(new { t = "pickup", id = 1 });
        var taken = await h.Next("barrel_taken");
        Assert.Equal(d.Host.Id, taken.GetProperty("by").GetGuid());
        Assert.Equal("xp", taken.GetProperty("kind").GetString());
        Assert.Equal(1, (await g.Next("barrel_taken")).GetProperty("id").GetInt32());
        await g.Send(new { t = "pickup", id = 1 });
        Assert.Equal("taken", (await g.Next("reject")).GetProperty("reason").GetString());

        foreach (var t in new[] { 10, 15, 20, 25 }) await At(d, t);
        await At(d, 30);
        var (gone, msgs) = await h.Until("barrel_gone");
        Assert.Equal(2, gone.GetProperty("id").GetInt32()); // spawned at 10 s, unclaimed for 20 s
        var spawns = msgs.Where(m => m.GetProperty("t").GetString() == "barrel_spawn").ToList();
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, spawns.Select(m => m.GetProperty("id").GetInt32()));
        Assert.Equal("health", spawns[^1].GetProperty("kind").GetString());
        Assert.Equal(ArenaRules.Ms(d.StartAt + TimeSpan.FromSeconds(30)), spawns[^1].GetProperty("st").GetInt64());
        Assert.All(spawns.Take(5), m => Assert.Equal("xp", m.GetProperty("kind").GetString()));

        // the end stats count barrels; a reconnect gets the live barrels in its match_start
        var resync = await Connect(d.LobbyId, d.Guest);
        var start = await resync.Next("match_start");
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, start.GetProperty("live").GetProperty("barrels").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()));
        resync.Dispose();
        await h.Send(new { t = "leave" });
        var end = await h.Next("match_end");
        Assert.Equal(1, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("barrelsTaken").GetInt32());
    }

    [Fact]
    public async Task Death_EndsMatch_WritesEloAndResult_ThenRematchCreatesLobby()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;

        await h.Send(State(-45, 0, alive: false)); // during the countdown: ignored
        await h.Sync();
        await At(d, 12);
        await g.Send(Hit(120));
        await h.Send(State(-45, 0, hp: 0, alive: false));
        var end = await g.Next("match_end");
        Assert.Equal("sunk", end.GetProperty("reason").GetString());
        Assert.Equal(d.Guest.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.True(end.GetProperty("rated").GetBoolean());
        Assert.Equal(12, end.GetProperty("durationSeconds").GetInt32());
        Assert.Equal(0, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("finalHp").GetDouble());

        var view = await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson();
        Assert.Equal("server_sunk", view.GetProperty("decidedBy").GetString());
        Assert.Equal(984, view.GetProperty("players")[0].GetProperty("ratingAfter").GetInt32());
        Assert.Equal(1016, view.GetProperty("players")[1].GetProperty("ratingAfter").GetInt32());
        Assert.Equal(1, (await GetJson(d.Guest, "/api/profiles/me")).GetProperty("wins").GetInt32());
        Assert.Equal(1, (await GetJson(d.Host, "/api/profiles/me")).GetProperty("losses").GetInt32());

        // a hit after the end is refused; the old result endpoint still answers with the decided match
        await g.Send(Hit(5));
        Assert.Equal("match_over", (await g.Next("reject")).GetProperty("reason").GetString());

        // rematch: both press it (sockets stay open after "finished" for the rematch window)
        await h.Send(new { t = "rematch" });
        var requested = await g.Next("rematch");
        Assert.Equal("requested", requested.GetProperty("status").GetString());
        var created = await (await d.Guest.Http.PostAsync($"/api/lobbies/{d.LobbyId}/rematch", null)).ReadJson();
        Assert.Equal("created", created.GetProperty("status").GetString());
        var pushed = await h.Next("rematch", m => m.GetProperty("status").GetString() == "created");
        var newId = pushed.GetProperty("lobbyId").GetGuid();
        Assert.Equal(created.GetProperty("lobbyId").GetGuid(), newId);
        var fresh = await GetJson(d.Host, $"/api/lobbies/{newId}");
        Assert.Equal("full", fresh.GetProperty("status").GetString());
        Assert.Equal(d.Guest.Id, fresh.GetProperty("guestPlayerId").GetGuid());
        Assert.True(fresh.GetProperty("seats")[1].GetProperty("hasDraft").GetBoolean());

        // after the window the old lobby's sockets close
        await At(d, 12 + 61);
        await h.ExpectClosed();
    }

    [Theory]
    [InlineData("ws")]
    [InlineData("put")]
    public async Task Leaving_Forfeits(string how)
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 20);
        if (how == "ws") await g.Send(new { t = "leave" });
        else
        {
            var res = await d.Guest.Http.PutAsJsonAsync($"/api/lobbies/{d.LobbyId}/status", new { status = "left" });
            Assert.Equal("finished", (await res.ReadJson()).GetProperty("status").GetString());
        }
        var end = await h.Next("match_end");
        Assert.Equal("forfeit", end.GetProperty("reason").GetString());
        Assert.Equal(d.Host.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.Equal(1016, (await GetJson(d.Host, "/api/profiles/me")).GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task Disconnecting_MoreThan20Seconds_Forfeits()
    {
        var d = await StartDuel();
        using var h = d.H;
        await At(d, 10);
        await d.G.Ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        d.G.Dispose();
        for (var i = 0; i < 200 && Broadcaster.IsConnected(d.LobbyId, d.Guest.Id); i++) await Task.Delay(25);
        Assert.False(Broadcaster.IsConnected(d.LobbyId, d.Guest.Id));

        await At(d, 11);
        var away = await h.Next("presence");
        Assert.False(away.GetProperty("connected").GetBoolean());
        Assert.Equal(ArenaRules.Ms(d.StartAt + TimeSpan.FromSeconds(31)), away.GetProperty("forfeitAt").GetInt64());

        await At(d, 31); // exactly 20 s: not yet
        await h.Sync();
        Assert.Equal("pending", (await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson()).GetProperty("status").GetString());
        await At(d, 31.5);
        var end = await h.Next("match_end");
        Assert.Equal("forfeit", end.GetProperty("reason").GetString());
        Assert.Equal(d.Host.Id, end.GetProperty("winnerPlayerId").GetGuid());
    }

    [Fact]
    public async Task Reconnecting_InTime_KeepsTheMatch()
    {
        var d = await StartDuel();
        using var h = d.H;
        await At(d, 10);
        await d.G.Ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        d.G.Dispose();
        for (var i = 0; i < 200 && Broadcaster.IsConnected(d.LobbyId, d.Guest.Id); i++) await Task.Delay(25);
        await At(d, 11);
        await h.Next("presence");
        await At(d, 25, tick: false);
        using var g2 = await Connect(d.LobbyId, d.Guest);
        Assert.Equal(1, (await g2.Next("match_start")).GetProperty("you").GetInt32());
        Assert.True((await h.Next("presence")).GetProperty("connected").GetBoolean());
        await At(d, 40);
        await h.Sync();
        Assert.Equal("pending", (await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task BotLobby_HostOnly_HostSimulatesBot_Unrated()
    {
        var host = await _api.NewPlayerAsync("Host");
        var other = await _api.NewPlayerAsync("Other");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = true })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        using var h = await Connect(id, host);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Http.PostAsync($"/api/lobbies/{id}/bot", null)).StatusCode);
        var withBot = await (await host.Http.PostAsync($"/api/lobbies/{id}/bot", null)).ReadJson();
        Assert.Equal("full", withBot.GetProperty("status").GetString());
        var botSeat = withBot.GetProperty("seats")[1];
        Assert.True(botSeat.GetProperty("bot").GetBoolean());
        Assert.True(botSeat.GetProperty("ready").GetBoolean());
        Assert.Equal(PracticeBot.Name, botSeat.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await host.Http.PostAsync($"/api/lobbies/{id}/bot", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await other.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() })).StatusCode);

        var startAt = _api.Clock.UtcNow + ArenaRules.Countdown;
        var started = await (await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("h") })).ReadJson();
        Assert.Equal("starting", started.GetProperty("status").GetString());
        Assert.Equal(1, started.GetProperty("match").GetProperty("botSeat").GetInt32());
        var start = await h.Next("match_start");
        Assert.Equal(1, start.GetProperty("bot").GetProperty("seat").GetInt32());
        Assert.Equal(host.Id, start.GetProperty("bot").GetProperty("simulatedBy").GetGuid());
        Assert.Equal("deck_cannons", start.GetProperty("players")[1].GetProperty("draft").GetProperty("weapons")[0].GetString());
        var botId = PracticeBot.Id;

        var d = new Duel(host, other, id, start.GetProperty("matchId").GetGuid(), h, h, start, start, startAt);
        // nobody plays seat 1 from a socket: no disconnect forfeit
        foreach (var t in new[] { 0, 5 }) await At(d, t);
        var spawn = await h.Next("barrel_spawn");
        double bx = spawn.GetProperty("x").GetDouble(), bz = spawn.GetProperty("z").GetDouble();

        // the host reports the bot's ship and claims for it
        await h.Send(State(bx, bz + 2, hp: 80, maxHp: 100, level: 3, asBot: true));
        await h.Send(new { t = "pickup", id = spawn.GetProperty("id").GetInt32(), asBot = true });
        Assert.Equal(botId, (await h.Next("barrel_taken")).GetProperty("by").GetGuid());

        // hits both ways come back to the host, which simulates the bot
        await h.Send(Hit(25));
        var onBot = await h.Next("hit");
        Assert.Equal(botId, onBot.GetProperty("to").GetGuid());
        await h.Send(Hit(10, "mortar", asBot: true));
        var onHost = await h.Next("hit");
        Assert.Equal(botId, onHost.GetProperty("from").GetGuid());
        Assert.Equal(host.Id, onHost.GetProperty("to").GetGuid());

        await At(d, 40); // seat 1 never connects: still no forfeit
        await h.Send(new { t = "dead", asBot = true });
        var end = await h.Next("match_end");
        Assert.Equal("sunk", end.GetProperty("reason").GetString());
        Assert.Equal(host.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.False(end.GetProperty("rated").GetBoolean());
        Assert.Equal(1, end.GetProperty("stats").GetProperty(botId.ToString()).GetProperty("barrelsTaken").GetInt32());
        Assert.Equal(25, end.GetProperty("stats").GetProperty(botId.ToString()).GetProperty("damageTaken").GetDouble());

        var me = await GetJson(host, "/api/profiles/me");
        Assert.Equal(1000, me.GetProperty("rating").GetInt32());
        Assert.Equal(0, me.GetProperty("wins").GetInt32());
        var view = await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson();
        Assert.Equal("decided", view.GetProperty("status").GetString());
        Assert.True(view.GetProperty("players")[1].GetProperty("bot").GetBoolean());
        Assert.Equal(PracticeBot.Name, view.GetProperty("players")[1].GetProperty("name").GetString());

        // a rematch against the bot only needs the host
        await h.Send(new { t = "rematch" });
        var created = await h.Next("rematch", m => m.GetProperty("status").GetString() == "created");
        var fresh = await GetJson(host, $"/api/lobbies/{created.GetProperty("lobbyId").GetGuid()}");
        Assert.True(fresh.GetProperty("seats")[1].GetProperty("bot").GetBoolean());
        Assert.True(fresh.GetProperty("seats")[1].GetProperty("ready").GetBoolean());
    }

    [Fact]
    public async Task AsBot_IsRefusedOutsideBotMatches()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await h.Send(State(0, 0, asBot: true));
        Assert.Equal("not_bot_host", (await h.Next("reject")).GetProperty("reason").GetString());
    }
}
