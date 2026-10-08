using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9b (I9b.1 part 2a): the voyage join ticket endpoint, POST /api/lobbies/voyage/ticket, and the contract shared with
/// Unity's JoinTicketVerifier (plan/v5/phase-09b-netcode/README.md). Replay is refused by the match server (it remembers
/// each tid); the API's part is a fresh tid on every ticket.
/// </summary>
public sealed class MatchTicketTests
{
    const string Secret = "test-secret-for-match-tickets-0123456789";
    const string Path = "/api/lobbies/voyage/ticket";

    static ApiHost Host(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["RP_MATCH_TICKET_SECRET"] = Secret,
            ["VOYAGE_SERVER_URL"] = "wss://match-test.example/",
        };
        if (extra != null) foreach (var (k, v) in extra) settings[k] = v;
        return new ApiHost(settings);
    }

    /// <summary>The checks Unity's JoinTicketVerifier makes, written again here: signature (before the body), version,
    /// audience; returns the payload.</summary>
    static JsonElement Verify(string ticket, string secret = Secret)
    {
        var parts = ticket.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("v1", parts[0]);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
        Assert.True(CryptographicOperations.FixedTimeEquals(expected, MatchTickets.FromBase64Url(parts[2])), "signed with the shared secret");
        Assert.DoesNotContain('=', ticket);
        Assert.DoesNotContain('+', ticket);
        Assert.DoesNotContain('/', ticket);
        var payload = JsonDocument.Parse(MatchTickets.FromBase64Url(parts[1])).RootElement.Clone();
        Assert.Equal(1, payload.GetProperty("v").GetInt32());
        Assert.Equal("rp-match", payload.GetProperty("aud").GetString());
        return payload;
    }

    [Fact]
    public void ContractVector_ReproducesUnitysTicketExactly()
    {
        // V5NetEditModeTests.Ticket_ContractVector (Unity): this key and payload give this ticket, which Unity verifies.
        var key = Encoding.UTF8.GetBytes("test-only-key-0123456789abcdef-0123456789");
        const string json = "{\"v\":1,\"tid\":\"0f8e\",\"sub\":\"guest-42\",\"mid\":\"m-1\",\"mode\":\"voyage\",\"aud\":\"rp-match\",\"iat\":1800000000,\"exp\":1800000060}";
        const string unityTicket =
            "v1.eyJ2IjoxLCJ0aWQiOiIwZjhlIiwic3ViIjoiZ3Vlc3QtNDIiLCJtaWQiOiJtLTEiLCJtb2RlIjoidm95YWdlIiwiYXVkIjoicnAtbWF0Y2giLCJpYXQiOjE4MDAwMDAwMDAsImV4cCI6MTgwMDAwMDA2MH0" +
            ".Fi3NPSq_6kPYiUO_C558vn8MIMfcJEscyL5SZoS2aQs";
        Assert.Equal(unityTicket, MatchTickets.SignJson(key, json));
        // The API's own serializer writes the same bytes for the same payload.
        var payload = new MatchTickets.Payload(1, "0f8e", "guest-42", "m-1", "voyage", "rp-match", 1_800_000_000, 1_800_000_060);
        Assert.Equal(json, MatchTickets.PayloadJson(payload));
        Assert.Equal(unityTicket, MatchTickets.Issue(key, payload));
    }

    [Fact]
    public async Task Ticket_RequiresAuth()
    {
        using var api = Host();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anon().PostAsync(Path, null)).StatusCode);
        var bad = api.As("not-a-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await bad.PostAsync(Path, null)).StatusCode);
    }

    [Fact]
    public async Task Ticket_SignedForThePlayer_ExpiresWithin120Seconds()
    {
        using var api = Host(new() { ["VOYAGE_MATCH_ID"] = "m9b-test" });
        var player = await api.NewPlayerAsync("Anne");
        var res = await player.Http.PostAsync(Path, null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("no-store", res.Headers.CacheControl?.ToString() ?? "");
        var body = await res.ReadJson();
        Assert.Equal("wss://match-test.example/", body.GetProperty("matchServerUrl").GetString());
        Assert.Equal("m9b-test", body.GetProperty("matchId").GetString());
        var payload = Verify(body.GetProperty("ticket").GetString()!);
        Assert.Equal(player.Id.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal("m9b-test", payload.GetProperty("mid").GetString());
        Assert.Equal("voyage", payload.GetProperty("mode").GetString());
        var now = new DateTimeOffset(api.Clock.UtcNow).ToUnixTimeSeconds();
        var iat = payload.GetProperty("iat").GetInt64();
        var exp = payload.GetProperty("exp").GetInt64();
        Assert.Equal(now, iat);
        Assert.Equal(60, exp - iat);
        Assert.True(exp - iat <= MatchTickets.MaxLifetimeSeconds);
        Assert.Equal(exp, body.GetProperty("expiresAt").GetInt64());
        Assert.DoesNotContain(Secret, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ticket_Lifetime_IsCappedAt120Seconds()
    {
        using var api = Host(new() { ["MATCH_TICKET_LIFETIME_SECONDS"] = "600" });
        var player = await api.NewPlayerAsync("Mary");
        var payload = Verify((await (await player.Http.PostAsync(Path, null)).ReadJson()).GetProperty("ticket").GetString()!);
        Assert.Equal(120, payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64());
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchTickets.Issue(Encoding.UTF8.GetBytes(Secret),
            new MatchTickets.Payload(1, "t", "p", "m", "voyage", "rp-match", 1000, 1121)));
    }

    [Fact]
    public async Task Ticket_FreshTidOnEveryCall_AndIatFollowsTheClock()
    {
        using var api = Host();
        var player = await api.NewPlayerAsync("Calico");
        var tids = new HashSet<string>();
        long firstIat = 0;
        for (var i = 0; i < 5; i++)
        {
            var payload = Verify((await (await player.Http.PostAsync(Path, null)).ReadJson()).GetProperty("ticket").GetString()!);
            Assert.True(tids.Add(payload.GetProperty("tid").GetString()!), "a fresh tid each call");
            Assert.True(payload.GetProperty("tid").GetString()!.Length >= 16);
            Assert.StartsWith("v-", payload.GetProperty("mid").GetString()); // no VOYAGE_MATCH_ID: a fresh match id
            if (i == 0) firstIat = payload.GetProperty("iat").GetInt64();
        }
        api.Clock.Advance(TimeSpan.FromMinutes(5));
        var later = Verify((await (await player.Http.PostAsync(Path, null)).ReadJson()).GetProperty("ticket").GetString()!);
        Assert.Equal(firstIat + 300, later.GetProperty("iat").GetInt64());
        // A ticket from 5 minutes ago is past its exp (plus the server's 5 s skew): the match server refuses it as Expired.
        Assert.True(firstIat + 60 + 5 < later.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task Ticket_Unavailable_WithoutSecret()
    {
        using var api = Host(new() { ["RP_MATCH_TICKET_SECRET"] = "" });
        var player = await api.NewPlayerAsync("Ching");
        var res = await player.Http.PostAsync(Path, null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal("match_tickets_off", (await res.ReadJson()).GetProperty("error").GetString());

        using var weak = Host(new() { ["RP_MATCH_TICKET_SECRET"] = "short-secret" });
        var p2 = await weak.NewPlayerAsync("Grace");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await p2.Http.PostAsync(Path, null)).StatusCode);
    }
}
