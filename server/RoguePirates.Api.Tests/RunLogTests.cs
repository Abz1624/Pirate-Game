using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9a (I9a.1): the run log uploaded with POST /api/runs. Older clients (no log) are still accepted; a log is stored
/// with its run. Shadow mode (owner decision of 8 Oct): a malformed, oversized or inconsistent log (sums that disagree
/// with the summary, no seed or config hash) does not refuse the run: it is accepted and ranks, its verdict is
/// log_rejected with the structural reason code, and the raw log is kept (capped at RunLogRules.MaxLogBytes). Only a
/// request body over RunLogRules.MaxRequestBodyBytes is refused (413). Structural checks only (plausibility is I9a.3).
/// </summary>
public sealed class RunLogTests : IDisposable
{
    readonly ApiHost _api = new();
    public void Dispose() => _api.Dispose();

    /// <summary>A consistent log: <paramref name="minutes"/> minutes (the last partial), kills spread over them.</summary>
    public static JsonObject Log(int kills = 50, double seconds = 610.4, int level = 10, int seed = 4242)
    {
        var count = (int)Math.Floor(seconds / 60) + 1;
        var weapons = new JsonArray("cannon", "harpoon");
        var minutes = new JsonArray();
        var left = kills;
        for (var i = 0; i < count; i++)
        {
            var k = i == count - 1 ? left : Math.Min(left, kills / count);
            left -= k;
            var wk0 = k / 2;
            minutes.Add(new JsonObject
            {
                ["m"] = i, ["spawns"] = k + 3, ["kills"] = k, ["eliteKills"] = 0, ["bossKills"] = 0, ["gold"] = k * 2, ["xp"] = k * 3,
                ["level"] = Math.Max(1, level - (count - 1 - i)), ["taken"] = 4.5, ["dealt"] = 1234.5, ["healed"] = 0, ["pickups"] = k,
                ["chests"] = 0, ["pois"] = 0, ["alive"] = 3, ["hull"] = 90, ["maxHull"] = 100, ["wk"] = new JsonArray(wk0, k - wk0),
            });
        }
        var inputs = new JsonArray();
        for (var t = 0; t <= (int)seconds; t += 5) inputs.Add(new JsonArray(t, 1.5, -2.5, 90, 0, 100, 0));
        return new JsonObject
        {
            ["format"] = "rp-runlog", ["v"] = 1, ["seed"] = seed, ["seedSource"] = "local", ["issuedRunId"] = null, ["runId"] = "abc123",
            ["configHash"] = "cfg-hash-1", ["balanceVersion"] = "saltwind-v5", ["contentHash"] = "content-1", ["stageProfile"] = "saltwind",
            ["buildId"] = "0.5.0", ["captain"] = "rook", ["startingWeapon"] = "cannon", ["end"] = "sunk", ["seconds"] = seconds,
            ["bossSummoned"] = -1, ["bossDefeated"] = -1, ["minuteSeconds"] = 60, ["weapons"] = weapons, ["minutes"] = minutes,
            ["inputEvery"] = 5, ["inputCols"] = new JsonArray("t", "x", "z", "heading", "steer", "throttle", "boost"), ["inputs"] = inputs,
        };
    }

    static JsonObject Run(JsonNode? log, int kills = 50, int seconds = 610, int level = 10)
    {
        var o = new JsonObject
        {
            ["kills"] = kills, ["runSeconds"] = seconds, ["level"] = level, ["captain"] = "rook", ["mapId"] = "saltwind",
            ["buildHash"] = "abc", ["clientVersion"] = "0.5.0",
        };
        if (log != null) o["log"] = log;
        return o;
    }

    async Task<(HttpResponseMessage res, JsonElement body)> Post(JsonObject run)
    {
        var p = await _api.NewPlayerAsync();
        var res = await p.Http.PostAsJsonAsync("/api/runs", run);
        return (res, await res.ReadJson());
    }

    async Task<(int runs, int logs)> Counts()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        return (await db.Runs.CountAsync(), await db.RunLogs.CountAsync());
    }

    [Fact]
    public async Task OldClientWithoutLog_IsAccepted()
    {
        var (res, body) = await Post(Run(null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False(body.GetProperty("logStored").GetBoolean());
        Assert.Equal((1, 0), await Counts());

        var (res2, _) = await Post(Run(JsonValue.Create((string?)null))); // "log": null as well
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
    }

    [Fact]
    public async Task ConsistentLog_IsStoredWithItsRun()
    {
        var log = Log();
        var (res, body) = await Post(Run(log));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(body.GetProperty("logStored").GetBoolean());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var stored = await db.RunLogs.SingleAsync();
        Assert.Equal(body.GetProperty("runId").GetInt64(), stored.RunId);
        Assert.Equal((1, 4242, "local", "cfg-hash-1", "saltwind-v5", "0.5.0", "abc123"),
            (stored.Version, stored.Seed, stored.SeedSource, stored.ConfigHash, stored.BalanceVersion, stored.BuildId, stored.ClientRunId));
        Assert.True(stored.Bytes > 1000 && stored.Bytes < RunLogRules.MaxLogBytes);
        Assert.Equal(log.ToJsonString(), JsonNode.Parse(stored.Json)!.ToJsonString());
    }

    /// <summary>The log the game writes (Unity EditMode test V5RunLogTests writes the same builder's output) is accepted.</summary>
    [Fact]
    public async Task GameWrittenSampleLog_IsAccepted()
    {
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "runlog-v1-sample.json")))!.AsObject();
        var minutes = sample["minutes"]!.AsArray();
        var kills = minutes.Sum(m => m!["kills"]!.GetValue<int>());
        var level = minutes[^1]!["level"]!.GetValue<int>();
        var seconds = (int)Math.Floor(sample["seconds"]!.GetValue<double>());
        var (res, body) = await Post(Run(sample, kills, seconds, level));
        Assert.True(res.StatusCode == HttpStatusCode.OK, body.ToString());
        Assert.True(body.GetProperty("logStored").GetBoolean());
    }

    public static IEnumerable<object[]> Broken() => new[]
    {
        new object[] { "kills_sum", (Action<JsonObject>)(l => l["minutes"]![0]!["kills"] = l["minutes"]![0]!["kills"]!.GetValue<int>() + 1), "log_mismatch" },
        new object[] { "level", (Action<JsonObject>)(l => { var ms = l["minutes"]!.AsArray(); ms[ms.Count - 1]!["level"] = 11; }), "log_mismatch" },
        new object[] { "seconds", (Action<JsonObject>)(l => l["seconds"] = 650.0), "log_mismatch" },
        new object[] { "seed_zero", (Action<JsonObject>)(l => l["seed"] = 0), "log_missing_seed" },
        new object[] { "seed_missing", (Action<JsonObject>)(l => l.Remove("seed")), "malformed_log" },
        new object[] { "config_hash_missing", (Action<JsonObject>)(l => l.Remove("configHash")), "log_missing_config_hash" },
        new object[] { "config_hash_empty", (Action<JsonObject>)(l => l["configHash"] = ""), "log_missing_config_hash" },
        new object[] { "version", (Action<JsonObject>)(l => l["v"] = 2), "unsupported_log_version" },
        new object[] { "format", (Action<JsonObject>)(l => l["format"] = "other"), "malformed_log" },
        new object[] { "minute_index", (Action<JsonObject>)(l => l["minutes"]![1]!["m"] = 5), "malformed_log" },
        new object[] { "minute_missing", (Action<JsonObject>)(l => l["minutes"]!.AsArray().RemoveAt(3)), "malformed_log" },
        new object[] { "wk_length", (Action<JsonObject>)(l => l["minutes"]![0]!["wk"]!.AsArray().Add(1)), "malformed_log" },
        new object[] { "wk_over_kills", (Action<JsonObject>)(l => l["minutes"]![0]!["wk"]![0] = 999), "malformed_log" },
        new object[] { "negative_gold", (Action<JsonObject>)(l => l["minutes"]![0]!["gold"] = -1), "malformed_log" },
        new object[] { "level_down", (Action<JsonObject>)(l => l["minutes"]![3]!["level"] = 1), "malformed_log" },
        new object[] { "string_count", (Action<JsonObject>)(l => l["minutes"]![0]!["spawns"] = "7"), "malformed_log" },
        new object[] { "input_cols", (Action<JsonObject>)(l => l["inputCols"]!.AsArray().RemoveAt(6)), "malformed_log" },
        new object[] { "input_row", (Action<JsonObject>)(l => l["inputs"]![0]!.AsArray().RemoveAt(0)), "malformed_log" },
        new object[] { "input_time", (Action<JsonObject>)(l => l["inputs"]![3]![0] = 9999), "malformed_log" },
        new object[] { "end", (Action<JsonObject>)(l => l["end"] = "won"), "malformed_log" },
        new object[] { "seed_source", (Action<JsonObject>)(l => l["seedSource"] = "server"), "malformed_log" },
    };

    async Task<(RunLog log, RunVerdict verdict, WeeklyBest? best)> Stored(long runId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        return (await db.RunLogs.AsNoTracking().SingleAsync(l => l.RunId == runId),
                await db.RunVerdicts.AsNoTracking().SingleAsync(v => v.RunId == runId),
                await db.WeeklyBests.AsNoTracking().SingleOrDefaultAsync(b => b.RunId == runId));
    }

    /// <summary>Asserts the shadow-mode answer to a structurally bad log: 200, accepted, not checked, log_rejected with the
    /// code; the run on the board; the raw log stored with the code. Returns the stored log.</summary>
    async Task<RunLog> AssertAcceptedAsRejected(string name, JsonObject run, string code)
    {
        var (res, body) = await Post(run);
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"{name}: {(int)res.StatusCode} {body}");
        Assert.True(body.GetProperty("accepted").GetBoolean(), name);
        Assert.False(body.GetProperty("checked").GetBoolean(), name);
        Assert.Equal(Verdicts.LogRejected, body.GetProperty("verdict").GetString());
        Assert.Equal(code, body.GetProperty("logRejected").GetString());
        Assert.True(body.GetProperty("logStored").GetBoolean(), name);
        var (log, verdict, best) = await Stored(body.GetProperty("runId").GetInt64());
        Assert.Equal((Verdicts.LogRejected, code, PlausibilityService.LogRule), (verdict.Status, verdict.ReasonCodes, verdict.RuleIds));
        Assert.Equal(code, log.RejectCode);
        Assert.False(string.IsNullOrEmpty(log.RejectReason), name);
        Assert.NotNull(best); // it ranks
        Assert.Equal(run["kills"]!.GetValue<int>(), best!.Kills);
        return log;
    }

    [Theory]
    [MemberData(nameof(Broken))]
    public async Task BrokenLog_IsAccepted_Ranked_AsLogRejected_WithItsReason_AndTheRawLogKept(string name, Action<JsonObject> edit, string code)
    {
        var log = Log();
        edit(log);
        var stored = await AssertAcceptedAsRejected(name, Run(log), code);
        Assert.Equal(log.ToJsonString(), JsonNode.Parse(stored.Json)!.ToJsonString()); // small: kept whole, as uploaded
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(stored.Json), stored.Bytes);
        Assert.Equal((1, 1), await Counts());
    }

    /// <summary>The header columns of a rejected log hold what could be read (here: a mismatch keeps them all).</summary>
    [Fact]
    public async Task MismatchedLog_KeepsItsHeader()
    {
        var log = Log();
        log["minutes"]![0]!["kills"] = log["minutes"]![0]!["kills"]!.GetValue<int>() + 1;
        var stored = await AssertAcceptedAsRejected("kills_sum", Run(log), "log_mismatch");
        Assert.Equal((1, 4242, "local", "cfg-hash-1", "0.5.0", "abc123"), (stored.Version, stored.Seed, stored.SeedSource, stored.ConfigHash, stored.BuildId, stored.ClientRunId));
        Assert.Contains("disagree", stored.RejectReason);
    }

    [Theory]
    [InlineData("\"a log\"")]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    public async Task LogThatIsNotAnObject_IsAccepted_AsLogRejected(string raw)
    {
        var run = Run(null);
        run["log"] = JsonNode.Parse(raw);
        var stored = await AssertAcceptedAsRejected(raw, run, "malformed_log");
        Assert.Equal(raw, stored.Json);
        Assert.Equal((0, ""), (stored.Seed, stored.ConfigHash));
    }

    static JsonObject Padded(JsonObject log, int inputRows)
    {
        log["captain"] = new string('x', 100);
        var inputs = log["inputs"]!.AsArray();
        while (inputs.Count < inputRows) inputs.Add(new JsonArray(610, 123.456789, -123.456789, 359.123456, -100, 100, 1));
        return log;
    }

    /// <summary>A log over the log cap but inside the body cap: accepted as log_rejected, stored cut to the cap.</summary>
    [Fact]
    public async Task OversizedLog_IsAccepted_AsLogRejected_AndStoredCapped()
    {
        var log = Padded(Log(), 1900);
        var size = System.Text.Encoding.UTF8.GetByteCount(log.ToJsonString());
        Assert.True(size > RunLogRules.MaxLogBytes && size < RunLogRules.MaxRequestBodyBytes - 1024, $"{size} bytes");
        var stored = await AssertAcceptedAsRejected("too_large", Run(log), "log_too_large");
        Assert.True(stored.Bytes > RunLogRules.MaxLogBytes, $"{stored.Bytes}"); // the full size is recorded
        Assert.Equal(RunLogRules.MaxLogBytes, System.Text.Encoding.UTF8.GetByteCount(stored.Json)); // the text is capped
        Assert.StartsWith("{\"format\":\"rp-runlog\"", stored.Json);
    }

    /// <summary>Only an oversized request body is refused: 413, nothing stored.</summary>
    [Fact]
    public async Task OversizedBody_Is413_AndNothingIsStored()
    {
        var run = Run(Padded(Log(), 3000));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(run.ToJsonString()) > RunLogRules.MaxRequestBodyBytes);
        var p = await _api.NewPlayerAsync();
        // with its length declared, as the game sends it (a byte-array body); Kestrel also stops a chunked body at the limit
        var res = await p.Http.PostAsync("/api/runs", new StringContent(run.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Equal("body_too_large", (await res.ReadJson()).GetProperty("error").GetString());
        Assert.Equal((0, 0), await Counts());

        // just under the limit is read (and, with a log over the log cap, accepted as log_rejected)
        var near = Run(Padded(Log(), 1900));
        var ok = await p.Http.PostAsync("/api/runs", new StringContent(near.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    /// <summary>V5 M9a (review): run-log retention deletes logs older than the period (default 8 weeks) unless the run was
    /// flagged or its log rejected; the runs and verdicts stay; younger logs stay.</summary>
    [Fact]
    public async Task Retention_DeletesOldLogs_ButKeepsFlaggedAndRejected()
    {
        var settings = _api.Services.GetRequiredService<ApiSettings>();
        Assert.Equal(56, settings.RunLogRetentionDays);
        Assert.Equal(7, ApiSettings.From(Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder(), new Dictionary<string, string?> { ["RUNLOG_RETENTION_DAYS"] = "7" }).Build()).RunLogRetentionDays);

        var (_, plain) = await Post(Run(Log()));                       // outdated_client (cfg-hash-1 is not in the rules): purged
        var broken = Log();
        broken["seed"] = 0;
        var (_, rejected) = await Post(Run(broken));                   // log_rejected: kept
        var (_, flagged) = await Post(Run(Log(seed: 99)));             // marked flagged below: kept
        long Id(JsonElement b) => b.GetProperty("runId").GetInt64();
        var flaggedId = Id(flagged);
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var v = await db.RunVerdicts.SingleAsync(x => x.RunId == flaggedId);
            v.Status = Verdicts.Flagged;
            await db.SaveChangesAsync();
        }

        _api.Clock.Advance(TimeSpan.FromDays(57));
        var (_, young) = await Post(Run(Log(seed: 7)));                 // submitted now: kept

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(1, await RunLogRetention.PurgeAsync(db, _api.Clock.UtcNow, TimeSpan.FromDays(settings.RunLogRetentionDays)));
            var left = await db.RunLogs.Select(l => l.RunId).OrderBy(x => x).ToListAsync();
            Assert.Equal(new[] { Id(rejected), Id(flagged), Id(young) }, left);
            Assert.Equal(4, await db.Runs.CountAsync());
            Assert.Equal(4, await db.RunVerdicts.CountAsync());
            Assert.Equal(0, await RunLogRetention.PurgeAsync(db, _api.Clock.UtcNow, TimeSpan.FromDays(56))); // idempotent
            Assert.Equal(1, await RunLogRetention.PurgeAsync(db, _api.Clock.UtcNow.AddDays(57), TimeSpan.FromDays(56))); // the young one, later
        }
    }

    /// <summary>The summary's own sanity checks still answer 422 before the log is read.</summary>
    [Fact]
    public async Task ImplausibleSummary_StillAnswers422()
    {
        var (res, _) = await Post(Run(Log(), kills: 601, seconds: 60));
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
    }

    /// <summary>AddRunLogs creates the RunLogs table with its key on the run, for both providers (no database needed).</summary>
    [Theory]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void AddRunLogsMigration_CreatesRunLogsTable(string provider)
    {
        using GameDb db = provider == "postgres"
            ? new PostgresGameDb(new DbContextOptionsBuilder<PostgresGameDb>().UseNpgsql("Host=localhost;Database=unused").Options)
            : new SqliteGameDb(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite("Data Source=:memory:").Options);
        var migrations = db.Database.GetMigrations().ToList();
        var add = Assert.Single(migrations, m => m.EndsWith("_AddRunLogs", StringComparison.Ordinal));
        Assert.Equal(1, migrations.IndexOf(add)); // right after the baseline
        var script = db.GetService<IMigrator>().GenerateScript(fromMigration: migrations[0], toMigration: add);
        Assert.Contains("CREATE TABLE \"RunLogs\"", script);
        Assert.Contains("FOREIGN KEY (\"RunId\") REFERENCES \"Runs\" (\"Id\") ON DELETE CASCADE", script);
        Assert.DoesNotContain("DROP", script);
        Assert.DoesNotContain("ALTER TABLE \"Runs\"", script);
    }

    /// <summary>V5 M9a (review): AddRunLogRejection only adds two nullable columns to RunLogs, for both providers.</summary>
    [Theory]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void AddRunLogRejectionMigration_OnlyAddsNullableColumns(string provider)
    {
        using GameDb db = provider == "postgres"
            ? new PostgresGameDb(new DbContextOptionsBuilder<PostgresGameDb>().UseNpgsql("Host=localhost;Database=unused").Options)
            : new SqliteGameDb(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite("Data Source=:memory:").Options);
        var migrations = db.Database.GetMigrations().ToList();
        var add = Assert.Single(migrations, m => m.EndsWith("_AddRunLogRejection", StringComparison.Ordinal));
        var script = db.GetService<IMigrator>().GenerateScript(fromMigration: migrations[migrations.IndexOf(add) - 1], toMigration: add);
        Assert.Contains("ALTER TABLE \"RunLogs\" ADD \"RejectCode\"", script);
        Assert.Contains("ALTER TABLE \"RunLogs\" ADD \"RejectReason\"", script);
        Assert.DoesNotContain("NOT NULL", script);
        Assert.DoesNotContain("DROP", script);
    }
}
