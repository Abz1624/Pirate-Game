using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

public class PlaytestUnitTests
{
    [Fact]
    public void WeekStart_IsMondayMidnightUtc()
    {
        Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), WeekMath.WeekStart(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), WeekMath.WeekStart(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), WeekMath.WeekStart(new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), WeekMath.WeekStart(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Elo_K32_Start1000()
    {
        Assert.Equal((1016, 984), Elo.Update(1000, 1000));
        Assert.Equal((1208, 992), Elo.Update(1200, 1000)); // favourite wins: 32 * (1 - 0.76) = 7.7 -> 8
        Assert.Equal((1024, 1176), Elo.Update(1000, 1200)); // underdog wins: 32 * 0.76 = 24.3 -> 24
    }

    [Theory]
    [InlineData("Grapeshot", false)]
    [InlineData("Peacock", false)]
    [InlineData("Sh1tLord", true)]
    [InlineData("FUCK_you", true)]
    [InlineData("b-i-t-c-h", true)]
    public void Profanity(string name, bool blocked) => Assert.Equal(blocked, NameRules.IsProfane(name));

    [Fact]
    public void Tags_AreStableFourDigits()
    {
        var id = Guid.NewGuid();
        Assert.Equal(PlayerTags.For(id), PlayerTags.For(id));
        Assert.Matches("^#[0-9]{4}$", PlayerTags.For(id));
    }

    [Fact]
    public void Origins_DefaultsAllowSiteAndLocalhost()
    {
        var s = ApiSettings.From(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        Assert.True(s.IsOriginAllowed("https://www.roguepirates.fun"));
        Assert.True(s.IsOriginAllowed("https://roguepirates.fun"));
        Assert.True(s.IsOriginAllowed("http://localhost:5173"));
        Assert.True(s.IsOriginAllowed("http://localhost"));
        Assert.False(s.IsOriginAllowed("http://localhost.evil.com"));
        Assert.False(s.IsOriginAllowed("https://evil.fun"));
        Assert.False(s.IsOriginAllowed("http://www.roguepirates.fun"));
    }

    [Fact]
    public void DatabaseUrl_IsConvertedForNpgsql()
    {
        var cs = new NpgsqlConnectionStringBuilder(ApiSettings.PostgresConnectionString("postgresql://pirate:p%40ss@db.railway.internal:5433/railway"));
        Assert.Equal("db.railway.internal", cs.Host);
        Assert.Equal(5433, cs.Port);
        Assert.Equal("pirate", cs.Username);
        Assert.Equal("p@ss", cs.Password);
        Assert.Equal("railway", cs.Database);
        var ssl = new NpgsqlConnectionStringBuilder(ApiSettings.PostgresConnectionString("postgres://u:p@h/db?sslmode=require"));
        Assert.Equal(SslMode.Require, ssl.SslMode);
        Assert.Equal(5432, ssl.Port);
    }
}

public class ProfileTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Create_Get_Rename()
    {
        var res = await _api.Anon().PostAsJsonAsync("/api/profiles", new { name = "  Anne Bonny  " });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.ReadJson();
        var token = body.GetProperty("token").GetString()!;
        Assert.Equal("Anne Bonny", body.GetProperty("name").GetString());
        Assert.Matches("^#[0-9]{4}$", body.GetProperty("tag").GetString()!);

        var me = await (await _api.As(token).GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(body.GetProperty("playerId").GetGuid(), me.GetProperty("playerId").GetGuid());
        Assert.Equal(1000, me.GetProperty("rating").GetInt32());
        Assert.Equal(0, me.GetProperty("wins").GetInt32());

        var renamed = await _api.As(token).PatchAsJsonAsync("/api/profiles/me", new { name = "Calico_Jack" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Calico_Jack", (await (await _api.As(token).GetAsync("/api/profiles/me")).ReadJson()).GetProperty("name").GetString());

        // the token is stored only as its SHA-256 hash
        using var scope = _api.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<GameDb>().Players.SingleAsync();
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(Tokens.Hash(token), row.TokenHash);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("seventeen_chars_x")]
    [InlineData("Bad!Name")]
    [InlineData("Fuck Ship")]
    [InlineData("   ")]
    public async Task InvalidNames_Rejected(string name)
    {
        var res = await _api.Anon().PostAsJsonAsync("/api/profiles", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var p = await _api.NewPlayerAsync();
        var rename = await p.Http.PatchAsJsonAsync("/api/profiles/me", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
    }

    [Fact]
    public async Task MissingOrUnknownToken_Is401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Anon().GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.As("not-a-token").GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Anon().PostAsJsonAsync("/api/runs", new { kills = 1, runSeconds = 60, level = 1 })).StatusCode);
    }

    [Fact]
    public async Task Health_ReportsSqlite()
    {
        var body = await (await _api.Anon().GetAsync("/health")).ReadJson();
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("sqlite", body.GetProperty("db").GetString());
    }

    [Fact]
    public async Task Cors_AllowsSiteAndLocalhost_NotOthers()
    {
        async Task<string?> Preflight(string origin)
        {
            var req = new HttpRequestMessage(HttpMethod.Options, "/api/profiles");
            req.Headers.Add("Origin", origin);
            req.Headers.Add("Access-Control-Request-Method", "POST");
            req.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
            var res = await _api.Anon().SendAsync(req);
            return res.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v.Single() : null;
        }

        Assert.Equal("https://www.roguepirates.fun", await Preflight("https://www.roguepirates.fun"));
        Assert.Equal("https://roguepirates.fun", await Preflight("https://roguepirates.fun"));
        Assert.Equal("http://localhost:8000", await Preflight("http://localhost:8000"));
        Assert.Null(await Preflight("https://evil.example"));
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task PostsPerIp_AreLimited()
    {
        using var api = new ApiHost(new() { ["RATE_LIMIT_PER_MINUTE"] = "3" });
        var client = api.Anon();
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profiles", new { name = $"Pirate{i}" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/profiles", new { name = "Pirate9" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode); // reads are not limited
    }
}

public class RunAndLeaderboardTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    static object Run(int kills, int seconds = 600, int level = 10, string? map = "saltwind") =>
        new { kills, runSeconds = seconds, level, captain = "anne", mapId = map, buildHash = "abc", clientVersion = "0.5.0" };

    [Theory]
    [InlineData(-1, 600, 10)]
    [InlineData(10, 9, 1)]
    [InlineData(10, 7201, 10)]
    [InlineData(10, 600, 0)]
    [InlineData(10, 600, 201)]
    [InlineData(601, 60, 10)] // 601 kills per minute
    public async Task ImplausibleRuns_Are422(int kills, int seconds, int level)
    {
        var p = await _api.NewPlayerAsync();
        var res = await p.Http.PostAsJsonAsync("/api/runs", Run(kills, seconds, level));
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("implausible_run", (await res.ReadJson()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task EdgeOfSanity_IsAccepted()
    {
        var p = await _api.NewPlayerAsync();
        Assert.Equal(HttpStatusCode.OK, (await p.Http.PostAsJsonAsync("/api/runs", Run(600, 60, 200))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await p.Http.PostAsJsonAsync("/api/runs", Run(0, 10, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await p.Http.PostAsJsonAsync("/api/runs", Run(5, 7200, 1))).StatusCode);
    }

    [Fact]
    public async Task BadMapId_Is400()
    {
        var p = await _api.NewPlayerAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await p.Http.PostAsJsonAsync("/api/runs", Run(5, 600, 1, "Salt Wind!"))).StatusCode);
    }

    [Fact]
    public async Task WeeklyBest_KeepsBestKills_AndStatsCount()
    {
        var p = await _api.NewPlayerAsync();
        var first = await (await p.Http.PostAsJsonAsync("/api/runs", Run(50))).ReadJson();
        Assert.True(first.GetProperty("isNewWeeklyBest").GetBoolean());
        var worse = await (await p.Http.PostAsJsonAsync("/api/runs", Run(30))).ReadJson();
        Assert.False(worse.GetProperty("isNewWeeklyBest").GetBoolean());
        Assert.Equal(50, worse.GetProperty("weeklyBest").GetProperty("kills").GetInt32());
        var better = await (await p.Http.PostAsJsonAsync("/api/runs", Run(80))).ReadJson();
        Assert.True(better.GetProperty("isNewWeeklyBest").GetBoolean());

        var board = await (await p.Http.GetAsync("/api/leaderboard/weekly?mapId=saltwind")).ReadJson();
        var entries = board.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal(80, entries[0].GetProperty("kills").GetInt32());

        var stats = await (await p.Http.GetAsync("/api/profiles/me/stats")).ReadJson();
        Assert.Equal(80, stats.GetProperty("bestKillsAllTime").GetInt32());
        Assert.Equal(80, stats.GetProperty("bestKillsThisWeek").GetInt32());
        Assert.Equal(3, stats.GetProperty("runsPlayed").GetInt32());
    }

    [Fact]
    public async Task WeekBoundary_MondayMidnightUtc_StartsANewBoard()
    {
        var p = await _api.NewPlayerAsync();
        _api.Clock.UtcNow = new DateTime(2026, 10, 4, 23, 59, 50, DateTimeKind.Utc); // Sunday
        await p.Http.PostAsJsonAsync("/api/runs", Run(100));
        _api.Clock.UtcNow = new DateTime(2026, 10, 5, 0, 0, 10, DateTimeKind.Utc); // Monday
        await p.Http.PostAsJsonAsync("/api/runs", Run(20));

        var current = await (await p.Http.GetAsync("/api/leaderboard/weekly")).ReadJson();
        Assert.Equal("2026-10-05T00:00:00Z", current.GetProperty("weekStart").GetString());
        Assert.Equal("2026-10-12T00:00:00Z", current.GetProperty("weekEnd").GetString());
        Assert.Equal(20, current.GetProperty("entries")[0].GetProperty("kills").GetInt32());

        var previous = await (await p.Http.GetAsync("/api/leaderboard/weekly/previous")).ReadJson();
        Assert.Equal("2026-09-28T00:00:00Z", previous.GetProperty("weekStart").GetString());
        Assert.Equal(100, previous.GetProperty("entries")[0].GetProperty("kills").GetInt32());
        Assert.Equal(1, previous.GetProperty("me").GetProperty("rank").GetInt32());

        var stats = await (await p.Http.GetAsync("/api/profiles/me/stats")).ReadJson();
        Assert.Equal(100, stats.GetProperty("bestKillsAllTime").GetInt32());
        Assert.Equal(20, stats.GetProperty("bestKillsThisWeek").GetInt32());
    }

    [Fact]
    public async Task Leaderboard_OrdersByKillsThenTime_AndReportsMe()
    {
        var slow = await _api.NewPlayerAsync("SlowShip");
        var fast = await _api.NewPlayerAsync("FastShip");
        var low = await _api.NewPlayerAsync("LowShip");
        var other = await _api.NewPlayerAsync("OtherMap");
        await slow.Http.PostAsJsonAsync("/api/runs", Run(30, 900));
        await fast.Http.PostAsJsonAsync("/api/runs", Run(30, 600));
        await low.Http.PostAsJsonAsync("/api/runs", Run(10, 600));
        await other.Http.PostAsJsonAsync("/api/runs", Run(999, 600, 10, "frostmere"));

        var board = await (await low.Http.GetAsync("/api/leaderboard/weekly?mapId=saltwind&limit=2")).ReadJson();
        var entries = board.GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());
        Assert.Equal("FastShip", entries[0].GetProperty("name").GetString());
        Assert.Equal(1, entries[0].GetProperty("rank").GetInt32());
        Assert.Equal("SlowShip", entries[1].GetProperty("name").GetString());
        Assert.Matches("^#[0-9]{4}$", entries[0].GetProperty("tag").GetString()!);
        Assert.Equal("anne", entries[0].GetProperty("captain").GetString());
        Assert.Equal(3, board.GetProperty("me").GetProperty("rank").GetInt32());
        Assert.Equal(10, board.GetProperty("me").GetProperty("kills").GetInt32());

        var anon = await (await _api.Anon().GetAsync("/api/leaderboard/weekly")).ReadJson();
        Assert.Equal(JsonValueKind.Null, anon.GetProperty("me").ValueKind);
        Assert.Equal(3, anon.GetProperty("entries").GetArrayLength());

        var frost = await (await other.Http.GetAsync("/api/leaderboard/weekly?mapId=frostmere")).ReadJson();
        Assert.Equal(999, frost.GetProperty("entries")[0].GetProperty("kills").GetInt32());
        Assert.Equal(1, frost.GetProperty("me").GetProperty("rank").GetInt32());
    }
}

public class LobbyTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    async Task<JsonElement> CreateLobby(TestPlayer host, bool isPublic = true)
    {
        var res = await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = isPublic });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.ReadJson();
    }

    static Task<HttpResponseMessage> SetStatus(TestPlayer p, Guid lobbyId, string status, object? draft = null) =>
        p.Http.PutAsJsonAsync($"/api/lobbies/{lobbyId}/status", new { status, draft });

    [Fact]
    public async Task Create_ListPublic_Join_Full()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var third = await _api.NewPlayerAsync("Third");

        var lobby = await CreateLobby(host);
        var code = lobby.GetProperty("code").GetString()!;
        Assert.Matches($"^[{LobbyService.CodeAlphabet}]{{6}}$", code);
        Assert.Equal($"https://www.roguepirates.fun/play/?join={code}", lobby.GetProperty("inviteUrl").GetString());
        Assert.Equal(host.Id, lobby.GetProperty("hostPlayerId").GetGuid());
        Assert.Equal("waiting", lobby.GetProperty("status").GetString());

        var priv = await CreateLobby(host, isPublic: false);
        _api.Clock.Advance(TimeSpan.FromSeconds(1));
        var newer = await CreateLobby(third);
        var list = (await (await _api.Anon().GetAsync("/api/lobbies/public")).ReadJson()).GetProperty("lobbies");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(newer.GetProperty("lobbyId").GetGuid(), list[0].GetProperty("lobbyId").GetGuid()); // newest first
        Assert.DoesNotContain(list.EnumerateArray(), l => l.GetProperty("lobbyId").GetGuid() == priv.GetProperty("lobbyId").GetGuid());

        Assert.Equal(HttpStatusCode.Conflict, (await host.Http.PostAsJsonAsync("/api/lobbies/join", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = "ZZZZZZ" })).StatusCode);

        var joined = await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = code.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        var state = await joined.ReadJson();
        Assert.Equal("full", state.GetProperty("status").GetString());
        Assert.Equal("Guest", state.GetProperty("seats")[1].GetProperty("name").GetString());
        Assert.Equal("joined", state.GetProperty("seats")[1].GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await third.Http.PostAsJsonAsync("/api/lobbies/join", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await third.Http.GetAsync($"/api/lobbies/{lobby.GetProperty("lobbyId").GetGuid()}")).StatusCode);

        var publicNow = (await (await _api.Anon().GetAsync("/api/lobbies/public")).ReadJson()).GetProperty("lobbies");
        Assert.Equal(1, publicNow.GetArrayLength()); // the full lobby left the list
    }

    [Fact]
    public async Task DraftValidation_AndReadyStartsMatch()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var lobby = await CreateLobby(host);
        var id = lobby.GetProperty("lobbyId").GetGuid();
        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() });

        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(host, id, "ready")).StatusCode); // no draft yet
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(host, id, "dancing")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(host, id, "drafting",
            new { weapons = new[] { "a", "b", "c", "d", "e" }, logbooks = new[] { "1", "2", "3", "4", "5", "6" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(host, id, "drafting",
            new { weapons = new[] { "a", "b", "c", "d", "e", "a" }, logbooks = new[] { "1", "2", "3", "4", "5", "6" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(host, id, "drafting",
            new { weapons = new[] { "a", "b", "c", "d", "e", "f" }, logbooks = new[] { "1", "2", "3", "4", "5", " " } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SetStatus(host, id, "in_match")).StatusCode);

        var drafting = await (await SetStatus(host, id, "drafting", JsonHelpers.Draft("h"))).ReadJson();
        Assert.Equal("drafting", drafting.GetProperty("seats")[0].GetProperty("status").GetString());

        // the guest sees that the host has a draft, not what it is
        var guestView = await (await guest.Http.GetAsync($"/api/lobbies/{id}")).ReadJson();
        Assert.True(guestView.GetProperty("seats")[0].GetProperty("hasDraft").GetBoolean());
        Assert.Equal(JsonValueKind.Null, guestView.GetProperty("seats")[0].GetProperty("draft").ValueKind);

        var hostReady = await (await SetStatus(host, id, "ready")).ReadJson(); // uses the stored draft
        Assert.True(hostReady.GetProperty("seats")[0].GetProperty("ready").GetBoolean());
        Assert.Equal("full", hostReady.GetProperty("status").GetString());

        var started = await (await SetStatus(guest, id, "ready", JsonHelpers.Draft("g"))).ReadJson();
        Assert.Equal("starting", started.GetProperty("status").GetString());
        var match = started.GetProperty("match");
        Assert.NotEqual(Guid.Empty, match.GetProperty("matchId").GetGuid());
        Assert.True(match.GetProperty("seed").GetInt64() >= 0);
        Assert.Equal(JsonValueKind.Null, match.GetProperty("matchServer").ValueKind);
        Assert.Equal("client_host", match.GetProperty("hostMode").GetString());
        Assert.Equal("hw1", started.GetProperty("seats")[0].GetProperty("draft").GetProperty("weapons")[0].GetString());
        Assert.Equal("gl6", started.GetProperty("seats")[1].GetProperty("draft").GetProperty("logbooks")[5].GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await SetStatus(host, id, "drafting")).StatusCode);
        await SetStatus(host, id, "in_match");
        var inMatch = await (await SetStatus(guest, id, "in_match")).ReadJson();
        Assert.Equal("in_match", inMatch.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MatchServerUrl_IsReturnedWhenConfigured()
    {
        using var api = new ApiHost(new() { ["MATCH_SERVER_URL"] = "wss://match.example:7777" });
        var host = await api.NewPlayerAsync("Host");
        var guest = await api.NewPlayerAsync("Guest");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = false })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() });
        await SetStatus(host, id, "ready", JsonHelpers.Draft());
        var started = await (await SetStatus(guest, id, "ready", JsonHelpers.Draft())).ReadJson();
        Assert.Equal("wss://match.example:7777", started.GetProperty("match").GetProperty("matchServer").GetString());
        Assert.Equal("dedicated", started.GetProperty("match").GetProperty("hostMode").GetString());
    }

    [Fact]
    public async Task GuestLeavingReopensSeat_HostLeavingCloses()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var lobby = await CreateLobby(host);
        var id = lobby.GetProperty("lobbyId").GetGuid();
        var code = lobby.GetProperty("code").GetString();
        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code });

        var afterGuestLeft = await (await SetStatus(guest, id, "left")).ReadJson();
        Assert.Equal("waiting", afterGuestLeft.GetProperty("status").GetString());
        Assert.Equal(1, afterGuestLeft.GetProperty("seats").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code })).StatusCode);
        var closed = await (await SetStatus(host, id, "left")).ReadJson();
        Assert.Equal("closed", closed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await SetStatus(guest, id, "drafting")).StatusCode);

        var other = await _api.NewPlayerAsync("Other");
        Assert.Equal(HttpStatusCode.Conflict, (await other.Http.PostAsJsonAsync("/api/lobbies/join", new { code })).StatusCode);
    }

    [Fact]
    public async Task IdleLobbies_ExpireAfter15Minutes()
    {
        var host = await _api.NewPlayerAsync("Host");
        var lobby = await CreateLobby(host);
        var id = lobby.GetProperty("lobbyId").GetGuid();

        _api.Clock.Advance(TimeSpan.FromMinutes(14));
        await SetStatus(host, id, "drafting"); // activity resets the idle timer
        _api.Clock.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal("waiting", (await (await host.Http.GetAsync($"/api/lobbies/{id}")).ReadJson()).GetProperty("status").GetString());
        Assert.Equal(1, (await (await _api.Anon().GetAsync("/api/lobbies/public")).ReadJson()).GetProperty("lobbies").GetArrayLength());

        _api.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(0, (await (await _api.Anon().GetAsync("/api/lobbies/public")).ReadJson()).GetProperty("lobbies").GetArrayLength());
        Assert.Equal("expired", (await (await host.Http.GetAsync($"/api/lobbies/{id}")).ReadJson()).GetProperty("status").GetString());
        var guest = await _api.NewPlayerAsync("Guest");
        Assert.Equal(HttpStatusCode.Conflict, (await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() })).StatusCode);
    }
}

public class MatchTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    async Task<(TestPlayer host, TestPlayer guest, Guid lobbyId, Guid matchId)> StartedMatch()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = true })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() });
        await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("h") });
        var started = await (await guest.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft("g") })).ReadJson();
        return (host, guest, id, started.GetProperty("match").GetProperty("matchId").GetGuid());
    }

    static object Report(Guid winner, TestPlayer host, TestPlayer guest, long hostDamage = 900, long guestDamage = 400) => new
    {
        winnerPlayerId = winner,
        durationSeconds = 180,
        stats = new Dictionary<string, object>
        {
            [host.Id.ToString()] = new { damageDealt = hostDamage, damageTaken = 400, kills = 3, level = 12, weapons = new[] { "hw1", "hw2" }, logbooks = new[] { "hl1" } },
            [guest.Id.ToString()] = new { damageDealt = guestDamage, damageTaken = 900, level = 11, weapons = new[] { "gw1" }, logbooks = Array.Empty<string>() },
        },
    };

    [Fact]
    public async Task AgreeingReports_DecideMatch_AndUpdateElo()
    {
        var (host, guest, lobbyId, matchId) = await StartedMatch();

        var first = await (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest))).ReadJson();
        Assert.Equal("awaiting_opponent", first.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest))).StatusCode);

        // the guest reports its own damage differently: each player's own numbers win
        var decided = await (await guest.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest, hostDamage: 850, guestDamage: 420))).ReadJson();
        Assert.Equal("decided", decided.GetProperty("status").GetString());
        Assert.Equal("agreement", decided.GetProperty("decidedBy").GetString());
        Assert.Equal(host.Id, decided.GetProperty("winnerPlayerId").GetGuid());

        var view = await (await _api.Anon().GetAsync($"/api/matches/{matchId}")).ReadJson();
        var players = view.GetProperty("players");
        Assert.True(players[0].GetProperty("won").GetBoolean());
        Assert.Equal(1016, players[0].GetProperty("ratingAfter").GetInt32());
        Assert.Equal(16, players[0].GetProperty("ratingChange").GetInt32());
        Assert.Equal(984, players[1].GetProperty("ratingAfter").GetInt32());
        Assert.Equal(900, players[0].GetProperty("stats").GetProperty("damageDealt").GetInt64());
        Assert.Equal(420, players[1].GetProperty("stats").GetProperty("damageDealt").GetInt64());
        Assert.Equal("hw1", players[0].GetProperty("stats").GetProperty("weapons")[0].GetString());

        var hostMe = await (await host.Http.GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(1016, hostMe.GetProperty("rating").GetInt32());
        Assert.Equal(1, hostMe.GetProperty("wins").GetInt32());
        var guestMe = await (await guest.Http.GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(984, guestMe.GetProperty("rating").GetInt32());
        Assert.Equal(1, guestMe.GetProperty("losses").GetInt32());

        Assert.Equal("finished", (await (await host.Http.GetAsync($"/api/lobbies/{lobbyId}")).ReadJson()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task SingleReport_DecidesAfter60Seconds()
    {
        var (host, guest, _, matchId) = await StartedMatch();
        await guest.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(guest.Id, host, guest));

        _api.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal("awaiting_opponent", (await (await _api.Anon().GetAsync($"/api/matches/{matchId}")).ReadJson()).GetProperty("status").GetString());

        _api.Clock.Advance(TimeSpan.FromSeconds(2));
        var view = await (await _api.Anon().GetAsync($"/api/matches/{matchId}")).ReadJson();
        Assert.Equal("decided", view.GetProperty("status").GetString());
        Assert.Equal("timeout", view.GetProperty("decidedBy").GetString());
        Assert.Equal(guest.Id, view.GetProperty("winnerPlayerId").GetGuid());
        Assert.Equal(1016, (await (await guest.Http.GetAsync("/api/profiles/me")).ReadJson()).GetProperty("rating").GetInt32());

        // a late report does not change the decided result
        var late = await (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest))).ReadJson();
        Assert.Equal(guest.Id, late.GetProperty("winnerPlayerId").GetGuid());
        Assert.Equal(1, (await (await guest.Http.GetAsync("/api/profiles/me")).ReadJson()).GetProperty("wins").GetInt32());
    }

    [Fact]
    public async Task DisagreeingReports_AreDisputed_NoRatingChange()
    {
        var (host, guest, _, matchId) = await StartedMatch();
        await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest));
        var res = await (await guest.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(guest.Id, host, guest))).ReadJson();
        Assert.Equal("disputed", res.GetProperty("status").GetString());
        _api.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("disputed", (await (await _api.Anon().GetAsync($"/api/matches/{matchId}")).ReadJson()).GetProperty("status").GetString());
        Assert.Equal(1000, (await (await host.Http.GetAsync("/api/profiles/me")).ReadJson()).GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task InvalidReports_AreRejected()
    {
        var (host, guest, _, matchId) = await StartedMatch();
        var outsider = await _api.NewPlayerAsync("Outsider");
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(host.Id, host, guest))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result", Report(outsider.Id, host, guest))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result",
            new { winnerPlayerId = host.Id, durationSeconds = 100, stats = new Dictionary<string, object> { [outsider.Id.ToString()] = new { damageDealt = 1, damageTaken = 1, level = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PostAsJsonAsync($"/api/matches/{matchId}/result",
            new { winnerPlayerId = host.Id, durationSeconds = 100, stats = new Dictionary<string, object> { [host.Id.ToString()] = new { damageDealt = -1, damageTaken = 1, level = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.PostAsJsonAsync($"/api/matches/{Guid.NewGuid()}/result", Report(host.Id, host, guest))).StatusCode);
    }
}

public class LobbyWebSocketTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    static async Task<JsonElement> Receive(WebSocket ws)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(buffer, cts.Token);
            ms.Write(buffer, 0, r.Count);
        } while (!r.EndOfMessage);
        return JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())).RootElement.Clone();
    }

    [Fact]
    public async Task Socket_PushesStateOnEveryChange()
    {
        var host = await _api.NewPlayerAsync("Host");
        var guest = await _api.NewPlayerAsync("Guest");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = true })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();

        var wsClient = _api.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(new Uri($"ws://localhost/ws/lobbies/{id}?token={Uri.EscapeDataString(host.Token)}"), CancellationToken.None);

        var initial = await Receive(ws);
        Assert.Equal("waiting", initial.GetProperty("status").GetString());

        await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = lobby.GetProperty("code").GetString() });
        var joined = await Receive(ws);
        Assert.Equal("full", joined.GetProperty("status").GetString());
        Assert.Equal("Guest", joined.GetProperty("seats")[1].GetProperty("name").GetString());

        await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft() });
        Assert.True((await Receive(ws)).GetProperty("seats")[0].GetProperty("ready").GetBoolean());

        await guest.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft() });
        var starting = await Receive(ws);
        Assert.Equal("starting", starting.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, starting.GetProperty("match").ValueKind);

        // "ping" asks for the current state again
        await ws.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.Equal("starting", (await Receive(ws)).GetProperty("status").GetString());

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task Socket_RejectsUnknownTokenAndOutsiders()
    {
        var host = await _api.NewPlayerAsync("Host");
        var outsider = await _api.NewPlayerAsync("Outsider");
        var lobby = await (await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = true })).ReadJson();
        var id = lobby.GetProperty("lobbyId").GetGuid();
        var wsClient = _api.Server.CreateWebSocketClient();
        await Assert.ThrowsAnyAsync<Exception>(() => wsClient.ConnectAsync(new Uri($"ws://localhost/ws/lobbies/{id}?token=nope"), CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => wsClient.ConnectAsync(new Uri($"ws://localhost/ws/lobbies/{id}?token={outsider.Token}"), CancellationToken.None));
    }
}
