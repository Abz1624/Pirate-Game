using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    readonly Action<IServiceCollection>? _services;

    public ApiHost(Dictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null)
    {
        _services = services;
        _settings = new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "",
            ["SQLITE_PATH"] = _dbPath,
            ["RATE_LIMIT_PER_MINUTE"] = "100000",
            ["SWEEP_SECONDS"] = "3600",
            ["MATCH_SERVER_URL"] = "",
            ["ARENA_TICK_MS"] = "3600000", // tests drive the relay clock with MatchRelay.TickAsync
            ["TREASURY_SECRET_KEY"] = "", // hackathon: no treasury unless a test sets one (never the machine's)
            ["ADMIN_KEY"] = "",
            ["SOLANA_RPC_URL"] = "http://127.0.0.1:9/no-rpc-in-tests",
        };
        if (settings != null)
            foreach (var (k, v) in settings) _settings[k] = v;
    }

    /// <summary>
    /// V5 M8z (7 Oct, review): one host starts at a time. With a top-level Program, WebApplicationFactory finds the app's
    /// host through a process-wide DiagnosticListener while Program runs; two factories starting at once on xUnit's
    /// parallel test classes could each take the other's host, so a test drove a clock or settings its server never used
    /// (flaky: RelayTests.Death_EndsMatch... waiting forever for match_end, MatchTests.DisagreeingReports..., a 429
    /// instead of 401 from the rate-limit test's settings).
    /// </summary>
    static readonly object StartLock = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (StartLock) return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (k, v) in _settings) builder.UseSetting(k, v);
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IClock>();
            s.AddSingleton<IClock>(Clock);
            _services?.Invoke(s);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // V5 M8z (7 Oct, review): only this host's pool (the connection string Program.cs builds). ClearAllPools closed
        // every host's pooled connections, so a test running in parallel failed with a 500 (ObjectDisposedException on
        // SQLitePCL.sqlite3) on its next query.
        using (var mine = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath, Pooling = true }.ToString()))
            SqliteConnection.ClearPool(mine);
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
        // V5 M8z (7 Oct, review): a failure names the server's answer (the Development host returns the exception)
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"POST /api/profiles: {(int)res.StatusCode} {res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
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
