using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>One player's lobby WebSocket: send relay messages, read until a message type arrives.</summary>
public sealed class Peer : IDisposable
{
    public WebSocket Ws { get; }
    public string LastRaw { get; private set; } = "";

    public Peer(WebSocket ws) => Ws = ws;

    /// <summary>
    /// V5 M8z (7 Oct, review): the longest a test waits for a message that must come. Not a pacing timeout: every wait in
    /// these tests is for a message the server sends once the test's own (awaited) actions are processed, so this only
    /// turns a hang into a readable failure (it was a bare 10 s cancellation, OperationCanceledException in Receive).
    /// </summary>
    public static readonly TimeSpan Failsafe = TimeSpan.FromSeconds(60);

    public Task Send(object message) => SendRaw(JsonSerializer.Serialize(message));

    /// <summary>V5 M8z (7 Oct, review): the send completes before the next one starts (one send at a time per socket).</summary>
    public async Task SendRaw(string text) =>
        await Ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public async Task<JsonElement> Receive(string? waitingFor = null)
    {
        using var cts = new CancellationTokenSource(Failsafe);
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            try { r = await Ws.ReceiveAsync(buffer, cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"no message in {Failsafe.TotalSeconds:0} s while waiting for \"{waitingFor ?? "any"}\" (last received: {LastRaw})");
            }
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
            var msg = await Receive(t);
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
        using var cts = new CancellationTokenSource(Failsafe);
        var buffer = new byte[16 * 1024];
        while (true)
        {
            WebSocketReceiveResult r;
            try { r = await Ws.ReceiveAsync(buffer, cts.Token); }
            catch (OperationCanceledException) { throw new TimeoutException($"the socket did not close in {Failsafe.TotalSeconds:0} s"); }
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

/// <summary>V5 M8z (6 Oct, owner; audited the same day): the anti-cheat numbers without a server.</summary>
public class HitRulesTests
{
    static readonly DateTime T0 = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The worst legitimate raw damage a second by ship level, measured with the game's weapon code by the Unity
    /// test PvpDamageCapTests (6 Oct; see HitRules): (level, raw/s).</summary>
    static readonly (int level, double worst)[] MeasuredWorst =
    {
        (1, 354), (2, 427), (3, 505), (4, 551), (5, 598), (6, 644), (7, 702), (8, 779), (9, 985), (10, 1075), (11, 1250),
        (12, 1965), (13, 2303), (14, 2646), (15, 2883), (20, 4149), (25, 5095), (30, 6985), (35, 8926), (40, 9640),
        (45, 12193), (50, 13363), (55, 14488), (60, 15884),
    };

    [Fact]
    public void SustainedDps_Is600Plus420ALevel_AtLeastOneAndAHalfTimesTheMeasuredWorst()
    {
        Assert.Equal(600, HitRules.SustainedDps(1), 6);
        Assert.Equal(600 + 420 * 9, HitRules.SustainedDps(10), 6);
        Assert.Equal(600 + 420 * 60, HitRules.SustainedDps(61), 6);
        Assert.True(HitRules.SustainedDps(61) < HitRules.MaxRawDps);
        foreach (var (level, worst) in MeasuredWorst)
            Assert.True(HitRules.SustainedDps(level) >= 1.5 * worst, $"level {level}: {HitRules.SustainedDps(level)} < 1.5 x {worst}");
        // a client sends a batch every 0.1 s: ten of the largest carry the worst level 60 build with the same margin
        Assert.True(10 * ArenaRules.MaxHitDamage >= 1.5 * MeasuredWorst[^1].worst);
    }

    [Fact]
    public void DamageBudget_Starts10sFull_FillsAtTheLevelsRate_Holds50s()
    {
        var b = new DamageBudget(T0);
        Assert.Equal(6000, b.Accept(T0, 6500, 1), 6); // 10 s of 600 at the start; the rest is dropped
        Assert.Equal(0, b.Accept(T0, 10, 1));
        Assert.Equal(600, b.Accept(T0.AddSeconds(1), 1000, 1), 6);
        Assert.Equal(30000, b.Available(T0.AddSeconds(200), 1), 6); // 50 s of 600
        Assert.Equal(51000, b.Available(T0.AddSeconds(300), 2), 6); // level 2: 50 s of 1020
    }

    [Fact]
    public void DamageBudget_LegitWorstCase_WithKegFieldAndFireAll_IsNeverShort()
    {
        // the worst build at each level fights for 60 s in 0.1 s batches, then a fire-all (2.1 s of its damage) and a keg
        // field (30 s of its damage, though kegs are one weapon of six) land at once; then it fights on at the next level
        foreach (var (level, worst) in MeasuredWorst)
        {
            var b = new DamageBudget(T0);
            var t = T0;
            double sent = 0, accepted = 0;
            void Send(double dmg) { sent += dmg; accepted += b.Accept(t, dmg, level); }
            for (var i = 0; i < 600; i++) { t = t.AddSeconds(0.1); Send(worst * 0.1); }
            for (var burst = (2.1 + 30) * worst; burst > 0; burst -= ArenaRules.MaxHitDamage) Send(Math.Min(burst, ArenaRules.MaxHitDamage));
            for (var i = 0; i < 300; i++) { t = t.AddSeconds(0.1); Send(worst * 0.1); }
            Assert.True(Math.Abs(sent - accepted) < 1e-6, $"level {level}: {sent - accepted:0.0} of {sent:0} refused");
        }
    }

    [Fact]
    public void PositionTrail_KeepsOnePointPer200ms_ForTheLast45s()
    {
        var trail = new PositionTrail();
        // times are the seat's game clock (seconds)
        for (var i = 0; i <= 600; i++) trail.Add(i / 15.0, i, 0); // 40 s of 15 states a second
        Assert.InRange(trail.Count, 195, 205);
        Assert.True(trail.AnyWithin(0, 0, 30, 80)); // the start, 30 m off
        trail.Add(50, 600, 0);
        Assert.False(trail.AnyWithin(5, 0, 0, 70)); // the first 5 s left the window (x = 15 t: 75 m and on)
        Assert.True(trail.AnyWithin(5, 100, 0, 10));
    }
}

public class RelayTests : IDisposable
{
    ApiHost _api = new(); // V5 M9j: a test may swap it for a host with other settings
    public void Dispose() => _api.Dispose();

    MatchRelay Relay => _api.Services.GetRequiredService<MatchRelay>();
    LobbyBroadcaster Broadcaster => _api.Services.GetRequiredService<LobbyBroadcaster>();

    sealed record Duel(TestPlayer Host, TestPlayer Guest, Guid LobbyId, Guid MatchId, Peer H, Peer G, JsonElement HostStart, JsonElement GuestStart, DateTime StartAt);

    async Task<Peer> Connect(Guid lobbyId, TestPlayer p)
    {
        var ws = await _api.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri($"ws://localhost/ws/lobbies/{lobbyId}?token={Uri.EscapeDataString(p.Token)}"), CancellationToken.None);
        var peer = new Peer(ws);
        // V5 M8z (7 Oct, review): the handshake returns before the server registers the socket (LobbyService.AttachAsync
        // runs after AcceptWebSocketAsync); a push in between (match_start on ready) missed it. The attach sends the lobby
        // state first: once it is here, the socket receives every later push.
        await peer.Next("lobby");
        return peer;
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

    // V5 M8z (6 Oct, owner): hits must name a drafted weapon (the test drafts are hw1..hw6 and gw1..gw6)
    static object Hit(double dmg, string? weaponId = "hw1", bool asBot = false) => new { t = "hit", dmg, weaponId, asBot };

    /// <summary>Both ships report a position (the range check reads them) and the relay has processed both.</summary>
    static async Task Place(Duel d, (double x, double z) host, (double x, double z) guest)
    {
        await d.H.Send(State(host.x, host.z));
        await d.G.Send(State(guest.x, guest.z));
        await d.H.Sync();
        await d.G.Sync();
    }

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
        await Place(d, (-5, 0), (5, 0));
        await h.Send(Hit(ArenaRules.MaxHitDamage + 1));
        Assert.Equal("bad_damage", (await h.Next("reject")).GetProperty("reason").GetString());
        await h.Send(Hit(-1));
        Assert.Equal("bad_damage", (await h.Next("reject")).GetProperty("reason").GetString());
        await h.Send(Hit(5, weaponId: null));
        Assert.Equal("bad_weapon", (await h.Next("reject")).GetProperty("reason").GetString());

        await h.Send(Hit(40));
        var hit = await g.Next("hit");
        Assert.Equal(40, hit.GetProperty("dmg").GetDouble());
        Assert.Equal("hw1", hit.GetProperty("weaponId").GetString());
        Assert.Equal(d.Host.Id, hit.GetProperty("from").GetGuid());
        Assert.Equal(d.Guest.Id, hit.GetProperty("to").GetGuid());
        await g.Send(Hit(15, "gw2"));
        Assert.Equal(15, (await h.Next("hit")).GetProperty("dmg").GetDouble());

        // rate cap: 20 hits per second (the bucket is full again 2 s later); the rest are rejected and not tallied
        await At(d, 3);
        await Place(d, (-5, 0), (5, 0));
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
    public async Task Time_EqualDamage_ServerHullEstimateDecides_NeverTheReportedHull_ElseDraw()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-5, 0), (5, 0));
        await h.Send(Hit(30));
        await g.Send(Hit(30, "gw1"));
        // V5 M9j: the reported hull says the guest (30 % against 25 %); the server's estimate of the same accepted damage
        // on the larger hull says the host: the estimate decides
        await h.Send(State(-45, 0, hp: 50, maxHp: 200));
        await g.Send(State(45, 0, hp: 30, maxHp: 100));
        await h.Sync();
        await g.Sync();
        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal("time", end.GetProperty("reason").GetString());
        Assert.Equal(d.Host.Id, end.GetProperty("winnerPlayerId").GetGuid());
        var hostStats = end.GetProperty("stats").GetProperty(d.Host.Id.ToString());
        var guestStats = end.GetProperty("stats").GetProperty(d.Guest.Id.ToString());
        Assert.True(hostStats.GetProperty("serverHull").GetDouble() > guestStats.GetProperty("serverHull").GetDouble());
        Assert.Equal(HullChecks.Agree, hostStats.GetProperty("hullCheck").GetString());

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

    /// <summary>V5 M9j: the host hits the guest every half second for 20 s; the guest never reports sinking (an
    /// "immortal" client).</summary>
    async Task<(Duel d, List<JsonElement> hostSaw)> ImmortalGuestAsync()
    {
        var seen = new List<JsonElement>();
        var d = await StartDuel();
        await At(d, 0.5);
        await Place(d, (-5, 0), (5, 0));
        await d.G.Send(State(5, 0, hp: 100, maxHp: 100));
        await d.G.Sync();
        for (var t = 1.0; t <= 20; t += 0.5)
        {
            await At(d, t);
            await d.H.Send(Hit(200)); // 200 x 0.06 x 0.5 = 6, capped at 5 % of 100: 5 a landing, 10 a second
            seen.AddRange(await d.H.Sync());
        }
        return (d, seen);
    }

    [Fact]
    public async Task HullEstimate_ImmortalClient_Shadow_MatchGoesOn_VerdictRecorded()
    {
        var (d, _) = await ImmortalGuestAsync();
        using var h = d.H;
        using var g = d.G;
        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal("time", end.GetProperty("reason").GetString());
        var guest = end.GetProperty("stats").GetProperty(d.Guest.Id.ToString());
        Assert.Equal(HullChecks.ServerSunkClientAlive, guest.GetProperty("hullCheck").GetString());
        Assert.True(guest.GetProperty("serverMinHull").GetDouble() < -HullEstimate.DefaultMargin);
        Assert.Equal(100, guest.GetProperty("finalHp").GetDouble()); // what the client said, kept for the record
        var host = end.GetProperty("stats").GetProperty(d.Host.Id.ToString());
        Assert.Equal(HullChecks.Agree, host.GetProperty("hullCheck").GetString());
        var view = await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson();
        Assert.Equal(HullChecks.ServerSunkClientAlive, view.GetProperty("players")[1].GetProperty("stats").GetProperty("hullCheck").GetString());
    }

    [Fact]
    public async Task HullEstimate_ImmortalClient_Enforced_IsSunkByTheServer()
    {
        _api.Dispose();
        _api = new ApiHost(new() { ["RELAY_HULL_ENFORCE"] = "true" });
        var (d, seen) = await ImmortalGuestAsync();
        using var h = d.H;
        using var g = d.G;
        var end = seen.FirstOrDefault(m => m.GetProperty("t").GetString() == "match_end") is { ValueKind: JsonValueKind.Object } e ? e : await h.Next("match_end");
        Assert.Equal(HullChecks.ServerSunk, end.GetProperty("reason").GetString());
        Assert.Equal(d.Host.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.Equal(HullChecks.ServerSunk, end.GetProperty("stats").GetProperty(d.Guest.Id.ToString()).GetProperty("hullCheck").GetString());
        var view = await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson();
        Assert.Equal("server_server_sunk", view.GetProperty("decidedBy").GetString());
    }

    [Fact]
    public async Task HullEstimate_HonestExchange_NeverSinksAnyone_EvenEnforced()
    {
        _api.Dispose();
        _api = new ApiHost(new() { ["RELAY_HULL_ENFORCE"] = "true" });
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 0.5);
        await Place(d, (-5, 0), (5, 0));
        // six level 1 weapons trade about 70 raw a second for 40 s (the playtest's measured rate), in 0.1 s batches
        for (var t = 1.0; t <= 41; t += 0.1)
        {
            await At(d, t);
            await h.Send(Hit(7));
            await g.Send(Hit(7, "gw1"));
            if (Math.Abs(t - Math.Round(t)) < 0.05) { await h.Sync(); await g.Sync(); }
        }
        await h.Sync();
        await g.Sync();
        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal("draw", end.GetProperty("reason").GetString());
        foreach (var p in new[] { d.Host.Id, d.Guest.Id })
            Assert.Equal(HullChecks.Agree, end.GetProperty("stats").GetProperty(p.ToString()).GetProperty("hullCheck").GetString());
    }

    [Fact]
    public async Task Barrels_SpawnOnSchedule_FairlyPlaced_FirstValidClaimWins_AndDespawn()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1, tick: false); // ships sail from the start (the speed check holds them at the spawn in the countdown)
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
        await g.Send(Hit(120, "gw1"));
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
        await g.Send(Hit(5, "gw1"));
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
    public async Task Reconnecting_AfterTheEnd_GetsTheResultAgain()
    {
        var d = await StartDuel();
        using var h = d.H;
        await At(d, 10);
        await d.G.Ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        d.G.Dispose();
        await h.Send(new { t = "leave" });
        var end = await h.Next("match_end");
        using var g2 = await Connect(d.LobbyId, d.Guest);
        var start = await g2.Next("match_start");
        Assert.True(start.GetProperty("live").GetProperty("ended").GetBoolean());
        var again = await g2.Next("match_end");
        Assert.Equal(end.GetRawText(), again.GetRawText());
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
        await h.Send(State(bx, bz - 4));
        await h.Sync();
        await h.Send(Hit(25));
        var onBot = await h.Next("hit");
        Assert.Equal(botId, onBot.GetProperty("to").GetGuid());
        await h.Send(Hit(10, "flamethrower", asBot: true)); // not in the bot's draft
        var notDrafted = await h.Next("reject");
        Assert.Equal("not_in_draft", notDrafted.GetProperty("reason").GetString());
        Assert.Equal(10, notDrafted.GetProperty("dropped").GetDouble());
        await h.Send(Hit(5, HitRules.BotWeaponId, asBot: true)); // its old cannons tag still counts
        var onHost = await h.Next("hit");
        Assert.Equal(botId, onHost.GetProperty("from").GetGuid());
        Assert.Equal(host.Id, onHost.GetProperty("to").GetGuid());
        await h.Send(Hit(5, "mortar", asBot: true)); // Hackathon (12 Oct): the bot fires its drafted weapons
        var drafted = await h.Next("hit");
        Assert.Equal(botId, drafted.GetProperty("from").GetGuid());
        Assert.Equal("mortar", drafted.GetProperty("weaponId").GetString());

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

    // ---------------------------------------------------------------- V5 M8z (6 Oct, owner): anti-cheat

    static int Count(List<JsonElement> msgs, string t, string? reason = null) =>
        msgs.Count(m => m.GetProperty("t").GetString() == t && (reason == null || m.GetProperty("reason").GetString() == reason));

    static string[] Rejects(List<JsonElement> msgs) =>
        msgs.Where(m => m.GetProperty("t").GetString() == "reject").Select(m => m.GetRawText()).ToArray();

    static double[] Forwarded(List<JsonElement> msgs) =>
        msgs.Where(m => m.GetProperty("t").GetString() == "hit").Select(m => m.GetProperty("dmg").GetDouble()).ToArray();

    [Fact]
    public async Task Hit_WeaponOutsideTheDraft_IsRefused_NotForwarded_NotTallied()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-5, 0), (5, 0));

        await h.Send(Hit(40, "gw1")); // the guest's weapon
        var reject = await h.Next("reject");
        Assert.Equal("not_in_draft", reject.GetProperty("reason").GetString());
        Assert.Equal(40, reject.GetProperty("dropped").GetDouble());
        await h.Send(Hit(40, "lighthouse_lantern"));
        Assert.Equal("not_in_draft", (await h.Next("reject")).GetProperty("reason").GetString());
        await h.Send(Hit(7, "hw6"));
        await h.Send(Hit(3, HitRules.GenericWeaponId)); // burns and hazards carry no weapon id
        Assert.Equal(0, Count(await h.Sync(), "reject"));
        Assert.Equal(new[] { 7.0, 3.0 }, Forwarded(await g.Sync()));

        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal(10, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("damageDealt").GetDouble());
    }

    [Fact]
    public async Task Hit_NeverWithinReach_IsRefused_AndSoIsOneAfterTheEngagementWindow()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-45, 0), (45, 0)); // 90 m apart since the spawn: never within 80 m
        await h.Send(Hit(20));
        var far = await h.Next("reject");
        Assert.Equal("out_of_range", far.GetProperty("reason").GetString());
        Assert.Equal(20, far.GetProperty("dropped").GetDouble());

        await Place(d, (-40, 0), (39, 0)); // 79 m: engaged
        await h.Send(Hit(20));
        Assert.Equal(20, (await g.Next("hit")).GetProperty("dmg").GetDouble());

        // apart from then on: 48 s of play later nothing of the host's can still be hitting the guest (a running client
        // reports at least every 0.085 s: its game clock then runs with the wall clock)
        for (var t = 1.08; t <= 50; t += 0.08)
        {
            await At(d, t, tick: false);
            await Place(d, (-60, 0), (60, 0));
        }
        await h.Send(Hit(20));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Hit_OverTheLevelsDamageBudget_DropsOnlyTheExcess()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-5, 0), (5, 0));

        // level 1: 10 s of 600 a second at the start, plus 1 s of filling: 6600
        for (var i = 0; i < 3; i++) await h.Send(Hit(2500));
        var capped = Assert.Single(await h.Sync(), m => m.GetProperty("t").GetString() == "reject");
        Assert.Equal("dps_cap", capped.GetProperty("reason").GetString());
        Assert.Equal(900, capped.GetProperty("dropped").GetDouble(), 6);
        Assert.Equal(new[] { 2500.0, 2500.0, 1600.0 }, Forwarded(await g.Sync()));

        // an empty bucket refuses the whole batch; a second later it holds 600 more
        await h.Send(Hit(50));
        var empty = await h.Next("reject");
        Assert.Equal("dps_cap", empty.GetProperty("reason").GetString());
        Assert.Equal(50, empty.GetProperty("dropped").GetDouble());
        await At(d, 2, tick: false);
        await h.Send(Hit(700));
        Assert.Equal(100, (await h.Next("reject")).GetProperty("dropped").GetDouble(), 6);
        Assert.Equal(1, Relay.Get(d.LobbyId)!.Seats[0].ServerLevel); // levels come only from XP barrels the server gave

        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal(7200, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("damageDealt").GetDouble(), 6);
    }

    [Fact]
    public async Task Cheat_ClaimingTheMostEveryBatch_IsHeldToTheLevelsRate_AndForfeits()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await Place(d, (-5, 0), (5, 0)); // in the countdown: engaged from the start
        var replies = new List<JsonElement>();
        for (var step = 1; step <= 40 && !replies.Any(m => m.GetProperty("t").GetString() == "match_end"); step++)
        {
            await At(d, step * 0.1, tick: false);
            await h.Send(Hit(ArenaRules.MaxHitDamage));
            replies.AddRange(await h.Sync());
        }
        var seat = Relay.Get(d.LobbyId)!.Seats[0];
        Assert.True(seat.DamageDealt <= 6000 + 600 * 4 + 1e-6, $"accepted {seat.DamageDealt}"); // the bucket, never more
        Assert.True(Count(replies, "reject", "dps_cap") >= 30);
        var end = await g.Next("match_end");
        Assert.Equal("anticheat", end.GetProperty("reason").GetString());
        Assert.Equal(d.Guest.Id, end.GetProperty("winnerPlayerId").GetGuid());
    }

    // ---- V5 M8z (6 Oct, owner: "check if this is applicable for all the weapons and scenarios"): legit play that
    // outlives the contact, bursts, lag and background tabs, none of which may lose a single point of damage ----

    /// <summary>Sends <paramref name="dmg"/> from the host (asks the sync) and returns the replies.</summary>
    static async Task<List<JsonElement>> HostHit(Duel d, double dmg, string weaponId)
    {
        await d.H.Send(Hit(dmg, weaponId));
        return await d.H.Sync();
    }

    [Fact]
    public async Task Scenario_KegsLeftBehind_HitAfterTheShipSailedAway()
    {
        // the host sails through (0, 0) dropping Powder Kegs (30 s afloat) while the guest is 100 m off, and keeps going
        // west; 29 s later the guest sails into the kegs, the host 102 m away: the ships were never within 80 m
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1, tick: false);
        await g.Send(State(100, 0)); // the guest first: the host at (0, 0) would be 45 m from the guest's spawn
        await g.Sync();
        await Place(d, (0, 0), (100, 0));
        var replies = new List<JsonElement>();
        for (var t = 2; t <= 25; t++)
        {
            await At(d, t, tick: false);
            await Place(d, (-4 * t, 0), (100, 0));
        }
        await At(d, 30, tick: false);
        await Place(d, (-100, 0), (2, 0));
        replies.AddRange(await HostHit(d, 450, "hw1")); // a field of kegs goes off together
        await At(d, 30.1, tick: false);
        replies.AddRange(await HostHit(d, 120, HitRules.GenericWeaponId)); // their burn
        Assert.Empty(Rejects(replies));
        Assert.Equal(new[] { 450.0, 120.0 }, Forwarded(await g.Sync()));
    }

    [Fact]
    public async Task Scenario_BurnsAndChasingSummons_HitAfterTheShipsParted()
    {
        // contact at 1 s (5 m), then the ships race apart at 15 m/s each; a full burn ticks for 40 s and parrots chase for
        // 6 s, all of it while the ships are far apart and the guest nowhere near the host's track
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1, tick: false);
        await Place(d, (0, 0), (5, 0));
        var replies = new List<JsonElement>();
        for (var step = 0; step < 80; step++)
        {
            var t = 1.5 + step * 0.5;
            await At(d, t, tick: false);
            await Place(d, (-15 * (t - 1), 0), (5 + 15 * (t - 1), 0));
            replies.AddRange(await HostHit(d, 5, HitRules.GenericWeaponId)); // burn: up to 10 raw a second
            if (t <= 7) replies.AddRange(await HostHit(d, 12, "hw2")); // parrots
        }
        Assert.Empty(Rejects(replies));
        Assert.Equal(0, Relay.Get(d.LobbyId)!.Seats[0].RejectedHits);
    }

    [Fact]
    public async Task Scenario_EvolvedAndMaxedWeapons_AllGunsAndAKegField_AtTheWorstRate()
    {
        // level 12 (the first level a weapon can be maxed and evolved): the worst build deals 1965 raw a second. 30 s of
        // that in 0.1 s batches tagged with base weapon ids (an evolved weapon reports its base id), then "All Guns!" (2.1 s
        // of it) and a keg field of 30 s of it detonate together, sent at the client's pace of one batch each 0.1 s
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        Relay.Get(d.LobbyId)!.Seats[0].XpBarrels = 11;
        await At(d, 0.5, tick: false);
        await Place(d, (-5, 0), (5, 0));
        const double worst = 1965;
        string[] tags = { "hw1", "hw2", "hw3", "hw4", "hw5", "hw6", HitRules.GenericWeaponId };
        var replies = new List<JsonElement>();
        double sent = 0;
        var t = 0.5;
        for (var i = 0; i < 300; i++)
        {
            t += 0.1;
            await At(d, t, tick: false);
            await h.Send(Hit(worst * 0.1, tags[i % tags.Length]));
            sent += worst * 0.1;
            replies.AddRange(await h.Sync());
            if (i % 20 == 19) await Place(d, (-5, 0), (5, 0));
        }
        for (var burst = (2.1 + 30) * worst; burst > 1e-9; burst -= ArenaRules.MaxHitDamage)
        {
            t += 0.1;
            await At(d, t, tick: false);
            var dmg = Math.Min(burst, ArenaRules.MaxHitDamage);
            await h.Send(Hit(dmg, "hw1"));
            sent += dmg;
            replies.AddRange(await h.Sync()); // one at a time: the server reads the test clock when it handles each
        }
        replies.AddRange(await h.Sync());
        Assert.Empty(Rejects(replies));
        Assert.Equal(sent, Relay.Get(d.LobbyId)!.Seats[0].DamageDealt, 3);
    }

    [Fact]
    public async Task Scenario_Level30_SustainedWorstRate()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        Relay.Get(d.LobbyId)!.Seats[0].XpBarrels = 29; // level 30: 6985 raw a second at worst
        await At(d, 1, tick: false);
        await Place(d, (-5, 0), (5, 0));
        var replies = new List<JsonElement>();
        for (var i = 0; i < 200; i++)
        {
            await At(d, 1 + 0.1 * (i + 1), tick: false);
            await h.Send(Hit(698.5, "hw4"));
            replies.AddRange(await h.Sync());
        }
        Assert.Empty(Rejects(replies));
    }

    [Fact]
    public async Task Scenario_LagSpikes_NoStatesForSeconds_ThenQueuedHitsTogether()
    {
        // a 3 s spike on the attacker's side (no states; its hits arrive late and together) and a 3 s one on the victim's
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1, tick: false);
        await Place(d, (-10, 0), (10, 0));
        var replies = new List<JsonElement>();
        await At(d, 4, tick: false);
        for (var i = 0; i < 15; i++) await h.Send(Hit(30, "hw5")); // 3 s of batches at once (under the 20 a second cap)
        replies.AddRange(await h.Sync());
        await h.Send(State(-12, 0));
        await At(d, 7, tick: false); // the victim has not reported since 1 s
        replies.AddRange(await HostHit(d, 30, "hw5"));
        await At(d, 8, tick: false);
        replies.AddRange(await HostHit(d, 30, HitRules.GenericWeaponId));
        Assert.Empty(Rejects(replies));
        Assert.Equal(17, Forwarded(await g.Sync()).Length);
    }

    [Fact]
    public async Task Scenario_BackgroundTab_ThrottledTo1Hz_ThenPausedAMinute_ThenBack()
    {
        // a hidden WebGL tab: the game runs once a second (one state, then one batch of that second's damage), then not at
        // all for 60 s (no states, no hits); back in front, the client reports its position before its hits
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        var replies = new List<JsonElement>();
        for (var t = 1; t <= 20; t++)
        {
            await At(d, t, tick: false);
            await g.Send(State(10 + t, 0));
            await h.Send(State(-10 + t, 0));
            replies.AddRange(await HostHit(d, 300, "hw6"));
        }
        await At(d, 80, tick: false); // paused 60 s; the guest moved on
        await g.Send(State(60, 40));
        await h.Send(State(40, 40));
        replies.AddRange(await HostHit(d, 300, "hw6"));
        Assert.Empty(Rejects(replies));
        Assert.Equal(21, Forwarded(await g.Sync()).Length);
    }

    [Fact]
    public async Task Time_IsDecidedByServerAcceptedDamage_NotByClaims()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-45, 0), (60, 0)); // 105 m (the host first: the guest still at its spawn is 90 m off)

        // the host claims 2000 from out of range and with a weapon it never drafted; the guest lands 50
        for (var i = 0; i < 3; i++) await h.Send(Hit(400));
        for (var i = 0; i < 2; i++) await h.Send(Hit(400, "mortar"));
        var rejected = await h.Sync();
        Assert.Equal(3, Count(rejected, "reject", "out_of_range"));
        Assert.Equal(2, Count(rejected, "reject", "not_in_draft"));
        await Place(d, (-30, 0), (30, 0));
        await g.Send(Hit(50, "gw1"));
        await g.Sync();

        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal("time", end.GetProperty("reason").GetString());
        Assert.Equal(d.Guest.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.Equal(0, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("damageDealt").GetDouble());
        Assert.Equal(50, end.GetProperty("stats").GetProperty(d.Guest.Id.ToString()).GetProperty("damageDealt").GetDouble());
        var seat = Relay.Get(d.LobbyId)!.Seats[0];
        Assert.Equal(2000, seat.RejectedDamage, 6);
        Assert.True(seat.Flagged); // logged: over 500 refused and over half its claims
    }

    [Fact]
    public async Task ManyRefusedHits_ForfeitARatedMatch()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 1);
        await Place(d, (-5, 0), (5, 0));
        await h.Send(Hit(100)); // one real hit: the refused share stays over 75 %
        for (var i = 0; i < 19; i++) await h.Send(Hit(200, "not_mine"));
        await h.Sync();
        await At(d, 2, tick: false);
        await Place(d, (-5, 0), (5, 0));
        for (var i = 0; i < 10; i++) await h.Send(Hit(200, "not_mine"));
        await h.Sync();
        Assert.Equal("pending", (await (await _api.Anon().GetAsync($"/api/matches/{d.MatchId}")).ReadJson()).GetProperty("status").GetString());
        await h.Send(Hit(200, "not_mine")); // the 30th refused hit
        var end = await g.Next("match_end");
        Assert.Equal("anticheat", end.GetProperty("reason").GetString());
        Assert.Equal(d.Guest.Id, end.GetProperty("winnerPlayerId").GetGuid());
        Assert.True(end.GetProperty("rated").GetBoolean());
        Assert.Equal(1016, (await GetJson(d.Guest, "/api/profiles/me")).GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task LegitPlay_IsNeverRefused()
    {
        // 60 s of a close fight at level 1: both ships circle 15 to 40 m apart reporting 10 states a second; each sends a
        // batch in about half of the 0.1 s slots of 5 to 70 raw (about 200 raw a second, over twice the measured 70),
        // across its drafted weapons and untagged burns, and the host adds a 300 crit burst every 7 s
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        var rng = new Random(11);
        string[] hostWeapons = { "hw1", "hw2", "hw3", "hw4", "hw5", "hw6", HitRules.GenericWeaponId };
        string[] guestWeapons = { "gw1", "gw2", "gw3", "gw4", "gw5", "gw6", HitRules.GenericWeaponId };
        double hostSent = 0, guestSent = 0;
        var replies = new List<JsonElement>();
        for (var step = 0; step < 600; step++)
        {
            var t = 0.1 * step;
            await At(d, t, tick: false);
            var angle = t * 0.4;
            var gap = 15 + 25 * (0.5 + 0.5 * Math.Sin(t * 0.7));
            await h.Send(State(Math.Cos(angle) * 30, Math.Sin(angle) * 30));
            await g.Send(State(Math.Cos(angle) * 30 + gap, Math.Sin(angle) * 30));
            if (rng.NextDouble() < 0.5)
            {
                var dmg = step % 70 == 35 ? 300 : Math.Round(5 + rng.NextDouble() * 65, 1);
                await h.Send(Hit(dmg, hostWeapons[rng.Next(hostWeapons.Length)]));
                hostSent += dmg;
            }
            if (rng.NextDouble() < 0.5)
            {
                var dmg = Math.Round(5 + rng.NextDouble() * 65, 1);
                await g.Send(Hit(dmg, guestWeapons[rng.Next(guestWeapons.Length)]));
                guestSent += dmg;
            }
            if (step % 10 == 9)
            {
                replies.AddRange(await h.Sync());
                replies.AddRange(await g.Sync());
            }
        }
        Assert.Empty(Rejects(replies));
        Assert.True(hostSent > 60 * 150, $"the test should send well over the measured rate: {hostSent}");

        await At(d, 300);
        var end = await h.Next("match_end");
        Assert.Equal(hostSent, end.GetProperty("stats").GetProperty(d.Host.Id.ToString()).GetProperty("damageDealt").GetDouble(), 3);
        Assert.Equal(guestSent, end.GetProperty("stats").GetProperty(d.Guest.Id.ToString()).GetProperty("damageDealt").GetDouble(), 3);
        Assert.Equal(0, Relay.Get(d.LobbyId)!.Seats[0].RejectedHits);
    }

    [Fact]
    public async Task Rematch_CreationFails_BothAreTold_AndTheNextRequestRetries()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 5);
        await h.Send(new { t = "leave" });
        await g.Next("match_end");

        // the new lobby's insert fails (a database error) until the trigger is dropped
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var table = db.Model.FindEntityType(typeof(Lobby))!.GetTableName()!;
            var sql = "CREATE TRIGGER fail_lobby BEFORE INSERT ON \"" + table + "\" BEGIN SELECT RAISE(ABORT, 'boom'); END;";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        await h.Send(new { t = "rematch" });
        await g.Next("rematch", m => m.GetProperty("status").GetString() == "requested");
        await g.Send(new { t = "rematch" });
        var failed = await h.Next("rematch", m => m.GetProperty("status").GetString() == "failed");
        Assert.Equal("server_error", failed.GetProperty("reason").GetString());
        Assert.Equal("rematch_failed", (await g.Next("reject")).GetProperty("reason").GetString());
        Assert.False(Relay.Get(d.LobbyId)!.RematchCreating);
        Assert.Null(Relay.Get(d.LobbyId)!.RematchLobbyId);

        using (var scope = _api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<GameDb>().Database.ExecuteSqlRawAsync("DROP TRIGGER fail_lobby;");
        await h.Send(new { t = "rematch" }); // both requests are still on record: this one creates it
        var created = await g.Next("rematch", m => m.GetProperty("status").GetString() == "created");
        var fresh = await GetJson(d.Host, $"/api/lobbies/{created.GetProperty("lobbyId").GetGuid()}");
        Assert.Equal("full", fresh.GetProperty("status").GetString());
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

    // ---- V5 M8z (6 Oct, owner: "ensure that we're not missing any edge case in this. so check all the possibilities"):
    // the range rule's edge cases. Legit play loses nothing; cheats are refused. Each step sets the clock and waits for the
    // relay (the server reads the test clock when it handles a message) ----

    /// <summary>At <paramref name="t"/> s of match time: the given ships report (null: silent) and the relay has read them.</summary>
    async Task Step(Duel d, double t, (double x, double z)? host, (double x, double z)? guest)
    {
        await At(d, t, tick: false);
        if (host is { } hp) await d.H.Send(State(hp.x, hp.z));
        if (guest is { } gp) await d.G.Send(State(gp.x, gp.z));
        if (host != null) await d.H.Sync();
        if (guest != null) await d.G.Sync();
    }

    MatchRoom Room(Duel d) => Relay.Get(d.LobbyId)!;

    [Fact]
    public async Task Edge_Countdown_NoHits_AndATeleportIsHeldNearTheSpawn()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, -3, tick: false); // in the countdown the ships cannot move; a client reports itself next to the victim
        await h.Send(State(40, 0));
        await h.Send(Hit(50));
        Assert.Equal("not_started", (await h.Next("reject")).GetProperty("reason").GetString());
        var host = Room(d).Seats[0];
        Assert.Equal(40, host.ReportedX);
        Assert.Equal(-45 + HitRules.SpeedSlack, host.X, 6); // held within the slack of the spawn
        Assert.Equal(double.NegativeInfinity, host.EngagedClock);
        Assert.Equal(1, host.Speed.Clamps);
    }

    [Fact]
    public async Task Edge_FirstContact_FromTheSpawns90mApart()
    {
        // no state yet: both ships at their spawns, 90 m apart: nothing reaches; once they close to 80 m, hits land
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await At(d, 0.5, tick: false);
        await h.Send(Hit(20));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());
        await Step(d, 1, (-40, 0), null); // 85 m
        await h.Send(Hit(20));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());
        await Step(d, 1.3, (-35, 0), null); // 80 m
        await h.Send(Hit(20));
        Assert.Equal(20, (await g.Next("hit")).GetProperty("dmg").GetDouble());
        // the guest's first hit back, before it ever reported (its own reach runs from its spawn)
        await g.Send(Hit(15, "gw1"));
        Assert.Equal(15, (await h.Next("hit")).GetProperty("dmg").GetDouble());
    }

    [Fact]
    public async Task Edge_SailingAtTheFastestSpeed_WithKnockbacks_IsNeverClamped()
    {
        // 15 states a second at the fastest speed, a 12 m knockback every 2 s, a turn, the arena crossed
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        double x = -45, z = 0, dir = 1;
        for (var i = 1; i <= 120; i++)
        {
            var t = i / 15.0;
            x += dir * HitRules.MaxShipSpeed / 15.0;
            if (i % 30 == 0) z += 12;
            if (x > 100) dir = -1;
            await Step(d, t, (x, z), null);
        }
        Assert.Equal(0, Room(d).Seats[0].Speed.Clamps);
        Assert.Equal(x, Room(d).Seats[0].X, 6);
    }

    [Fact]
    public async Task Edge_LagSpike_HoldsBackAState_ThenTheRestArriveAtOnce_IsNotClamped()
    {
        // the host sails at the fastest speed; a 3 s spike holds back its states: the first arrives 3 s late, the rest
        // (3 s of sailing) right after it, then normal again; and a 6 s spike with a burst of 90 queued states
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        double X(double sentAt) => -100 + HitRules.MaxShipSpeed * sentAt;
        await Step(d, 0.5, (X(0.5), 0), null);
        for (var i = 8; i <= 45; i++) await Step(d, i / 15.0, (X(i / 15.0), 0), null);
        await Step(d, 6, (X(3.07), 0), null); // the delayed anchor
        await Step(d, 6.07, (X(6.07), 0), null); // 3 s of sailing 0.07 s later
        for (var i = 92; i <= 105; i++) await Step(d, i / 15.0, (X(i / 15.0), 0), null);
        // the ship turns back (the arena's edge); 6 s of queued states arrive within 0.1 s
        double Y(double s) => X(7) - HitRules.MaxShipSpeed * (s - 7);
        for (var k = 1; k <= 90; k++) await Step(d, 13 + k * 0.001, (Y(7 + k / 15.0), 0), null);
        Assert.Equal(0, Room(d).Seats[0].Speed.Clamps);
    }

    [Fact]
    public async Task Cheat_TeleportingNextToTheVictim_ForOneState_IsClamped_AndOpensNoEngagement()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        for (var t = 0.1; t <= 3; t += 0.1) await Step(d, t, (-60, 0), (60, 0));
        await Step(d, 3.05, (55, 0), null); // a blink next to the guest
        await Step(d, 3.1, (-60, 0), null); // and back
        var host = Room(d).Seats[0];
        Assert.True(host.Speed.Clamps >= 1);
        Assert.Equal(double.NegativeInfinity, host.EngagedClock);
        await h.Send(Hit(100));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());

        // reporting the blink for good walks the checked position there no faster than the fastest ship x 1.5
        var replies = new List<JsonElement>();
        double t2 = 3.1;
        for (var i = 0; i < 10; i++)
        {
            t2 += 0.1;
            await Step(d, t2, (55, 0), null);
            await h.Send(Hit(10));
            replies.AddRange(await h.Sync());
        }
        Assert.True(Count(replies, "reject", "out_of_range") >= 2, string.Join("\n", Rejects(replies)));
    }

    [Fact]
    public async Task Cheat_TeleportingOntoABarrel_CannotClaimIt()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        for (var t = 0.25; t <= 4.9; t += 0.25) await Step(d, t, (-45, 0), (45, 0));
        await At(d, 5);
        var spawn = await h.Next("barrel_spawn");
        double bx = spawn.GetProperty("x").GetDouble(), bz = spawn.GetProperty("z").GetDouble();
        Assert.True(HitRules.Distance(bx, bz, -45, 0) > 40);
        await At(d, 5.05, tick: false);
        await h.Send(State(bx, bz));
        await h.Send(new { t = "pickup", id = spawn.GetProperty("id").GetInt32() });
        Assert.Equal("too_far", (await h.Next("reject")).GetProperty("reason").GetString());
        Assert.Equal(0, Room(d).Seats[0].XpBarrels);
    }

    [Fact]
    public async Task Edge_VictimBlinks_TheHonestAttackersHitsStillCount_TheBlinkersDoNot()
    {
        // the guest reports itself next to the host for a moment: the host's client draws it there and hits it (accepted:
        // the victim's reported position counts for hits on it); the guest's own hits from there are refused
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        for (var t = 0.1; t <= 3; t += 0.1) await Step(d, t, (-60, 0), (60, 0));
        await Step(d, 3.05, null, (-55, 0));
        await h.Send(Hit(30));
        Assert.Equal(30, (await g.Next("hit")).GetProperty("dmg").GetDouble());
        await g.Send(Hit(30, "gw1"));
        Assert.Equal("out_of_range", (await g.Next("reject")).GetProperty("reason").GetString());
        Assert.True(Room(d).Seats[1].Speed.Clamps >= 1);
    }

    [Fact]
    public async Task Edge_AttackersTabPaused60s_ItsBurnsAndKegsStillCount_ThenTheWindowRunsOnItsPlay()
    {
        // contact at 1 s; the host's tab is hidden and its game paused (no states, no hits) for 60 s while the guest sails
        // 150 m off; back in front the host's burns tick on (its own game time) and count until 45 s of its play passed
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await Step(d, 1, (-10, 0), (10, 0));
        await h.Send(Hit(40));
        await h.Sync();
        for (var t = 2; t <= 61; t += 2) await Step(d, t, null, (Math.Min(140, 10 + 5 * t), 0));
        var replies = new List<JsonElement>();
        double t2 = 61;
        for (var i = 0; i < 525; i++) // 42 s of play at 0.08 s steps: within 48 s of play after the contact
        {
            t2 += 0.08;
            await Step(d, t2, (-10, 0), null);
            await h.Send(Hit(3, HitRules.GenericWeaponId));
            replies.AddRange(await h.Sync());
        }
        Assert.Empty(Rejects(replies));
        for (var i = 0; i < 90; i++) // 7.2 s more: past the window of play
        {
            t2 += 0.08;
            await Step(d, t2, (-10, 0), null);
        }
        await h.Send(Hit(3, HitRules.GenericWeaponId));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Edge_AttackersTabThrottledTo1Hz_ItsSlowedBurnsStillCount()
    {
        // a hidden tab runs one frame a second (0.33 s of game time each): a 40 s burn lasts 2 minutes of wall time
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await Step(d, 1, (-10, 0), (10, 0));
        var replies = new List<JsonElement>();
        for (var t = 2; t <= 121; t++)
        {
            await Step(d, t, (-10 + 0.3 * (t - 1), 0), (Math.Min(140, 10 + 5 * t), 0));
            await h.Send(Hit(3.3, HitRules.GenericWeaponId));
            replies.AddRange(await h.Sync());
        }
        Assert.Empty(Rejects(replies));
    }

    [Fact]
    public async Task Edge_ReconnectMidMatch_KeepsTheEngagementAndTheTrail()
    {
        var d = await StartDuel();
        var g = d.G;
        await Step(d, 1, (-10, 0), (10, 0));
        d.H.Dispose(); // the socket drops (a new seat socket comes back; the seat's server state is the same)
        await At(d, 8);
        using var h2 = await Connect(d.LobbyId, d.Host);
        await h2.Next("match_start");
        await At(d, 9, tick: false);
        await g.Send(State(120, 0));
        await g.Sync();
        await h2.Send(State(-10, 0));
        await h2.Send(Hit(25, "hw3"));
        Assert.Empty(Rejects(await h2.Sync()));
        Assert.Equal(25, (await g.Next("hit")).GetProperty("dmg").GetDouble());
        g.Dispose();
    }

    [Fact]
    public async Task Edge_KegFieldCarriedOver20s_AtLevel60_FromTheEndOfTheWindow_LandsWhole()
    {
        // the worst burst (a keg field of 30 s of the level 60 worst build plus a fire-all: 510k raw) goes off near the end of the
        // 48 s window after the contact; the client sends it as full batches each 0.1 s for 20 s while the ships part
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        Room(d).Seats[0].XpBarrels = 59;
        await Step(d, 1, (-10, 0), (10, 0)); // contact; the host drops its kegs here and sails off
        double t = 1;
        while (t < 49) { t += 0.08; await Step(d, t, (Math.Max(-110, -10 - 10 * (t - 1)), 0), (Math.Min(110, 10 + 10 * (t - 1)), 0)); }
        var burst = (2.1 + 30) * 15884;
        var replies = new List<JsonElement>();
        var i = 0;
        double sent = 0;
        while (burst > 1e-6)
        {
            t += 0.1;
            await At(d, t, tick: false);
            if (i++ % 3 == 0) await h.Send(State(-110, 0));
            var dmg = Math.Min(burst, ArenaRules.MaxHitDamage);
            await h.Send(Hit(dmg, "hw1"));
            burst -= dmg;
            sent += dmg;
            replies.AddRange(await h.Sync());
        }
        Assert.Empty(Rejects(replies));
        var host = Room(d).Seats[0];
        Assert.True(host.Clock - host.EngagedClock > HitRules.EngagementWindow.TotalSeconds + 12, $"the burst should run well past the window: {host.Clock - host.EngagedClock}");
        // a fresh batch after the burst ended is out of reach again
        t += 1;
        await Step(d, t, (-110, 0), null);
        await h.Send(Hit(10, "hw1"));
        Assert.Equal("out_of_range", (await h.Next("reject")).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Cheat_EndlessFullBatches_AfterOneContact_StopAt30sOfCarry()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        Room(d).Seats[0].XpBarrels = 59; // level 60: the bucket alone would allow 25000 raw a second
        await Step(d, 1, (-10, 0), (10, 0));
        double t = 1;
        while (t < 49) { t += 0.08; await Step(d, t, (Math.Max(-110, -10 - 10 * (t - 1)), 0), (Math.Min(110, 10 + 10 * (t - 1)), 0)); }
        var replies = new List<JsonElement>();
        double firstRefusal = 0;
        for (var i = 0; i < 500 && firstRefusal == 0; i++)
        {
            t += 0.1;
            await At(d, t, tick: false);
            if (i % 3 == 0) await h.Send(State(Math.Max(-110, -10 - 10 * (t - 1)), 0));
            await h.Send(Hit(ArenaRules.MaxHitDamage, "hw1"));
            var r = await h.Sync();
            if (Count(r, "reject", "out_of_range") > 0) { firstRefusal = t; break; }
        }
        // the chain began in reach (the last batch before the contact left its 48 s of play) and ran 30 s of play more
        var seat = Room(d).Seats[0];
        Assert.True(firstRefusal > 0);
        Assert.InRange(seat.Clock - seat.ReachClock, HitRules.MaxCarry, HitRules.MaxCarry + 0.2);
        Assert.InRange(seat.ReachClock - seat.EngagedClock, HitRules.EngagementWindow.TotalSeconds - 0.2, HitRules.EngagementWindow.TotalSeconds);
    }

    [Fact]
    public async Task Edge_HitClaimingTheOtherSeatsId_IsTheSendersOwn_AndAfterTheEndRefused()
    {
        var d = await StartDuel();
        using var h = d.H;
        using var g = d.G;
        await Step(d, 1, (-10, 0), (10, 0));
        await g.Send(new { t = "hit", dmg = 30, weaponId = "gw2", from = d.Host.Id, to = d.Guest.Id });
        var hit = await h.Next("hit");
        Assert.Equal(d.Guest.Id, hit.GetProperty("from").GetGuid());
        Assert.Equal(d.Host.Id, hit.GetProperty("to").GetGuid());
        await g.Send(new { t = "state", x = -10.0, z = 0.0, asBot = true }); // the guest cannot speak for a bot
        Assert.Equal("not_bot_host", (await g.Next("reject")).GetProperty("reason").GetString());
        await h.Send(new { t = "leave" });
        await g.Next("match_end");
        await g.Send(Hit(30, "gw1"));
        Assert.Equal("match_over", (await g.Next("reject")).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Edge_BotMatch_BotAndPlayerHitsInReach_ABotTeleportIsClamped()
    {
        var host = await _api.NewPlayerAsync("Host");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = false })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        using var h = await Connect(id, host);
        await host.Http.PostAsync($"/api/lobbies/{id}/bot", null);
        var startAt = _api.Clock.UtcNow + ArenaRules.Countdown;
        await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("h") });
        var start = await h.Next("match_start");
        var d = new Duel(host, host, id, start.GetProperty("matchId").GetGuid(), h, h, start, start, startAt);

        // the host's client reports both ships at 15 a second; the bot closes to its 26 m firing range
        for (var i = 1; i <= 60; i++)
        {
            await At(d, i / 15.0, tick: false);
            await h.Send(State(-45 + i * 0.5, 0));
            await h.Send(State(45 - i * 0.5, 0, asBot: true));
            await h.Sync();
        }
        await h.Send(Hit(50, HitRules.BotWeaponId, asBot: true));
        await h.Send(Hit(40));
        var replies = await h.Sync();
        Assert.Empty(Rejects(replies));
        Assert.Equal(2, Count(replies, "hit"));

        // a bot report beyond its speed is clamped like any other
        await At(d, 4.05, tick: false);
        await h.Send(State(-200, 0, asBot: true));
        await h.Sync();
        Assert.Equal(1, Room(d).Seats[1].Speed.Clamps);
    }
}
