using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Tests;

public sealed class TestClock : IClock
{
    // Thursday 1 Oct 2026, 12:00 UTC (ISO week starting Monday 28 Sep)
    public DateTime UtcNow { get; set; } = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>The real API on an in-memory test server, a throwaway SQLite file and an injected clock.</summary>
public sealed class ApiHost : WebApplicationFactory<Program>
{
    readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rp-playtest-{Guid.NewGuid():N}.db");
    readonly Dictionary<string, string?> _settings;

    public TestClock Clock { get; } = new();

    public ApiHost(Dictionary<string, string?>? settings = null)
    {
        _settings = new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "",
            ["SQLITE_PATH"] = _dbPath,
            ["RATE_LIMIT_PER_MINUTE"] = "100000",
            ["SWEEP_SECONDS"] = "3600",
            ["MATCH_SERVER_URL"] = "",
        };
        if (settings != null)
            foreach (var (k, v) in settings) _settings[k] = v;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (k, v) in _settings) builder.UseSetting(k, v);
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IClock>();
            s.AddSingleton<IClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            try { File.Delete(f); } catch { /* best effort */ }
    }

    public HttpClient Anon() => CreateClient();

    public HttpClient As(string token)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public async Task<TestPlayer> NewPlayerAsync(string name = "Blackbeard")
    {
        var res = await Anon().PostAsJsonAsync("/api/profiles", new { name });
        res.EnsureSuccessStatusCode();
        var body = await res.ReadJson();
        var token = body.GetProperty("token").GetString()!;
        return new TestPlayer(body.GetProperty("playerId").GetGuid(), token, As(token));
    }
}

public sealed record TestPlayer(Guid Id, string Token, HttpClient Http);

public static class JsonHelpers
{
    public static async Task<JsonElement> ReadJson(this HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    public static object Draft(string prefix = "") => new
    {
        weapons = Enumerable.Range(1, 6).Select(i => $"{prefix}w{i}").ToArray(),
        logbooks = Enumerable.Range(1, 6).Select(i => $"{prefix}l{i}").ToArray(),
    };
}
