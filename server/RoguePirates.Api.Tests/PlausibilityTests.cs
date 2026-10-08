using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;
using RoguePirates.Api.Playtest;
using Xunit;
using Xunit.Abstractions;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9a (I9a.3, T9a.x): the run checks in shadow mode. The shipped rules (Plausibility/plausibility-rules.json, made by
/// plan/playtest/montecarlo/plausibility.mjs); every recorded voyage (converted to a run log, Fixtures/plausibility) passes
/// the rules of the config it was played on, and the checker here gives the same verdicts as the generator's; edited logs
/// and a consistent forgery are flagged with their reasons; an upload gets a stored verdict, the weekly board shows
/// "checked", a flagged run still counts (D23, D24); stored runs are re-scored when the rules version changes.
/// </summary>
public sealed class PlausibilityTests : IDisposable
{
    readonly ITestOutputHelper _out;
    readonly ApiHost _api = new();

    public PlausibilityTests(ITestOutputHelper output) => _out = output;
    public void Dispose() => _api.Dispose();

    static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "plausibility");
    static string Fixture(string name) => File.ReadAllText(Path.Combine(FixtureDir, name));
    static readonly PlausibilityRuleSet Shipped = PlausibilityRuleSet.LoadEmbedded();

    static string ShippedJson()
    {
        using var s = typeof(PlausibilityRuleSet).Assembly.GetManifestResourceStream("RoguePirates.Api.plausibility-rules.json")!;
        return new StreamReader(s).ReadToEnd();
    }

    static JsonElement Expected() => JsonDocument.Parse(Fixture("expected-verdicts.json")).RootElement.Clone();
    static IEnumerable<JsonElement> Sessions() => Expected().GetProperty("sessions").EnumerateArray();

    /// <summary>The voyage on today's balance (S5, 16:38) as a log naming today's config (it differs from the shipped config
    /// only in the Unlocks asset, which no rule reads).</summary>
    static JsonObject TodaysVoyage()
    {
        var s5 = Sessions().First(s => s.GetProperty("id").GetString() == "S5");
        var log = JsonNode.Parse(Fixture(s5.GetProperty("file").GetString()!))!.AsObject();
        log["configHash"] = Shipped.ConfigHash;
        return log;
    }

    static PlausibilityVerdict Check(JsonNode log, PlausibilityRuleSet? rules = null) => (rules ?? Shipped).Evaluate(log.ToJsonString());
    static JsonArray Minutes(JsonNode log) => log["minutes"]!.AsArray();
    static double D(JsonNode? n) => JsonSerializer.Deserialize<double>(n!.ToJsonString());
    static string[] Codes(PlausibilityVerdict v) => v.Reasons.Select(r => r.Code).ToArray();

    // ---- the rules file ----------------------------------------------------------------------------------------------

    [Fact]
    public void ShippedRules_AreShadowMode_WithEveryRule_AndPassedTheFalsePositiveBudget()
    {
        Assert.True(Shipped.RulesVersion >= 1);
        Assert.Equal("shadow", Shipped.Mode);
        Assert.Equal(RunLogRules.Version, Shipped.LogVersion);
        Assert.Contains(Shipped.ConfigHash, Shipped.ConfigHashes);
        var ids = Shipped.Rules.Select(r => r.Id).ToHashSet();
        foreach (var id in new[] { "PR-KILLS-MIN", "PR-XP-MIN", "PR-GOLD-MIN", "PR-DEALT-MIN", "PR-CHESTS-MIN", "PR-POIS-MIN", "PR-KILLS-RUN", "PR-XP-RUN",
                     "PR-GOLD-RUN", "PR-DEALT-RUN", "PR-CHESTS-RUN", "PR-POIS-RUN", "PR-LEVEL", "PR-LEVEL-XP", "PR-KILLS-SPAWNS", "PR-HULL", "PR-OVERTIME" })
            Assert.Contains(id, ids);
        // every minute bound covers the longest log the API takes; upper bounds only (all positive)
        foreach (var r in Shipped.Rules.Where(r => r.Bounds.Length > 0))
        {
            Assert.Equal(RunLogRules.MaxMinutes, r.Bounds.Length);
            Assert.All(r.Bounds, b => Assert.True(b > 0, r.Id));
        }
        // T9a.x: the generator's false-positive check (node plausibility.mjs --check) ran on these rules and stayed in budget
        var root = JsonDocument.Parse(ShippedJson()).RootElement;
        var fp = root.GetProperty("checks").GetProperty("falsePositive");
        Assert.Equal(10000, fp.GetProperty("runs").GetInt32());
        Assert.True(fp.GetProperty("share").GetDouble() <= 0.001, "at most 0.1% of the strongest personality's voyages flagged");
        Assert.Equal(root.GetProperty("simulator").GetProperty("personality").GetString(), fp.GetProperty("personality").GetString());
        _out.WriteLine($"rules v{Shipped.RulesVersion}: false positives {fp.GetProperty("flagged").GetInt32()} of {fp.GetProperty("runs").GetInt32()}");
    }

    [Fact]
    public void LevelFromXp_FollowsTheXpTable()
    {
        var t = Shipped.XpToNext;
        Assert.Equal(Shipped.StartLevel, Shipped.LevelFromXp(0));
        Assert.Equal(Shipped.StartLevel, Shipped.LevelFromXp(t[0] - 1));
        Assert.Equal(Shipped.StartLevel + 1, Shipped.LevelFromXp(t[0]));
        Assert.Equal(Shipped.StartLevel + 3, Shipped.LevelFromXp(t[0] + t[1] + t[2]));
    }

    // ---- recorded voyages ----------------------------------------------------------------------------------------------

    [Fact]
    public void EveryRecordedVoyage_PassesTheRulesOfItsOwnConfig_AndTheCheckerAgreesWithTheGenerator()
    {
        var count = 0;
        foreach (var s in Sessions())
        {
            var file = s.GetProperty("file").GetString()!;
            var log = JsonNode.Parse(Fixture(file))!;
            var own = PlausibilityRuleSet.Parse(Fixture(s.GetProperty("rules").GetString()!));
            var mine = Check(log, own);
            Assert.True(mine.Status == Verdicts.Pass, $"{file}: {string.Join(", ", mine.Reasons.Select(r => $"{r.Code} minute {r.Minute} {r.Value} > {r.Bound}"))}");
            Assert.Equal(s.GetProperty("ownVerdict").GetString(), mine.Status);

            // with the shipped rules: another build's config is outdated_client, a voyage on the shipped config (S6) passes;
            // the bounds alone as the generator found them
            var shipped = Check(log);
            Assert.Equal(s.GetProperty("verdict").GetString(), shipped.Status);
            if (Shipped.ConfigHashes.Contains(log["configHash"]!.GetValue<string>()))
                Assert.Equal(Verdicts.Pass, shipped.Status);
            else
            {
                Assert.Equal(Verdicts.OutdatedClient, shipped.Status);
                Assert.Equal("config_not_in_rules", shipped.Reasons.Single().Code);
            }
            var bounds = Shipped.Evaluate(log.ToJsonString(), checkConfig: false);
            Assert.Equal(s.GetProperty("boundsVerdict").GetString(), bounds.Status);
            Assert.Equal(s.GetProperty("boundsReasons").EnumerateArray().Select(r => r.GetProperty("rule").GetString()).ToArray(), bounds.Reasons.Select(r => r.Rule).ToArray());
            Assert.Equal(s.GetProperty("boundsReasons").EnumerateArray().Select(r => r.GetProperty("value").GetDouble()).ToArray(), bounds.Reasons.Select(r => r.Value!.Value).ToArray());
            _out.WriteLine($"{s.GetProperty("id").GetString(),-4} {file}: own config {mine.Status}; shipped rules, bounds only: {bounds.Status} {string.Join(",", Codes(bounds))}");
            count++;
        }
        Assert.Equal(9, count);
    }

    [Fact]
    public void TodaysBalanceVoyage_PassesTheShippedRules()
    {
        var v = Check(TodaysVoyage());
        Assert.True(v.Status == Verdicts.Pass, string.Join(", ", v.Reasons.Select(r => $"{r.Code} minute {r.Minute} {r.Value} > {r.Bound}")));
    }

    /// <summary>
    /// D25 (rules v2): the owner's voyages on the current build pass the shipped rules: S5 as a log naming today's config
    /// (it differs only in the Unlocks asset) and S6 (the M9a playtest, 8 Oct, a Cursed Doubloons build flagged by rules
    /// v1 at minute 4: 200 kills > 194). The generator's verdicts (expected-verdicts.json, currentBuild) agree.
    /// </summary>
    [Fact]
    public void CurrentBuildVoyages_PassTheShippedRules()
    {
        var rows = Expected().GetProperty("currentBuild").EnumerateArray().ToList();
        Assert.Equal(new[] { "S5", "S6" }, rows.Select(r => r.GetProperty("id").GetString()).ToArray());
        foreach (var row in rows)
        {
            var log = JsonNode.Parse(Fixture(row.GetProperty("file").GetString()!))!.AsObject();
            if (row.GetProperty("convertedToShippedConfig").GetBoolean()) log["configHash"] = Shipped.ConfigHash;
            Assert.Contains(log["configHash"]!.GetValue<string>(), Shipped.ConfigHashes);
            var v = Check(log);
            Assert.True(v.Status == Verdicts.Pass, $"{row.GetProperty("id").GetString()}: " + string.Join(", ", v.Reasons.Select(r => $"{r.Code} minute {r.Minute} {r.Value} > {r.Bound}")));
            Assert.Equal(row.GetProperty("verdict").GetString(), v.Status);
            var used = row.GetProperty("headroom").EnumerateObject().Select(h => $"{h.Name} {h.Value.GetProperty("used").GetDouble()}%");
            _out.WriteLine($"{row.GetProperty("id").GetString()}: {v.Status}; most of each bound used: {string.Join(", ", used)}");
        }
    }

    // ---- the shared edge cases ------------------------------------------------------------------------------------------

    /// <summary>
    /// V5 M9a (review): the shared edge-case fixture (Fixtures/plausibility/edge-cases: synthetic rules and logs on the
    /// minute edges, the hull-ledger tolerance, the overtime limit and the level against the XP level, with their expected
    /// verdicts). The generator (<c>node plausibility.mjs --edge-cases</c>) and the game (V5PlausibilityRulesTests) assert
    /// the same file, so the three checkers agree at every boundary.
    /// </summary>
    [Fact]
    public void EdgeCases_GiveTheExpectedVerdicts_TheSameInEveryChecker()
    {
        var dir = Path.Combine(FixtureDir, "edge-cases");
        var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "cases.json"))).RootElement;
        var rules = PlausibilityRuleSet.Parse(File.ReadAllText(Path.Combine(dir, spec.GetProperty("rules").GetString()!)));
        var count = 0;
        foreach (var c in spec.GetProperty("cases").EnumerateArray())
        {
            var name = c.GetProperty("name").GetString()!;
            var v = rules.Evaluate(File.ReadAllText(Path.Combine(dir, c.GetProperty("file").GetString()!)));
            var got = string.Join("; ", v.Reasons.Select(r => $"{r.Rule} m{r.Minute} {r.Value} > {r.Bound}"));
            var want = string.Join("; ", c.GetProperty("reasons").EnumerateArray().Select(r =>
                $"{r.GetProperty("rule").GetString()} m{r.GetProperty("minute").GetInt32()} {r.GetProperty("value").GetDouble()} > {r.GetProperty("bound").GetDouble()}"));
            Assert.True(c.GetProperty("expect").GetString() == v.Status && want == got, $"{name}: expected {c.GetProperty("expect").GetString()} [{want}], got {v.Status} [{got}]");
            _out.WriteLine($"{name,-36} {v.Status} {got}");
            count++;
        }
        Assert.True(count >= 15, "the fixture has its cases");
    }

    // ---- edited logs and forgeries ------------------------------------------------------------------------------------

    [Fact]
    public void EditedLogs_AreFlaggedWithTheirReasons()
    {
        void Expect(string what, Action<JsonObject> edit, params string[] codes)
        {
            var log = TodaysVoyage();
            edit(log);
            var v = Check(log);
            Assert.True(v.Status == Verdicts.Flagged, what + ": " + v.Status);
            foreach (var c in codes) Assert.Contains(c, Codes(v));
            _out.WriteLine($"{what}: {string.Join(", ", v.Reasons.Select(r => $"{r.Code} [{r.Rule}] minute {r.Minute}: {r.Value} > {r.Bound}"))}");
        }
        Expect("kills of minute 5 quadrupled", l => { var m = Minutes(l)[5]!; m["kills"] = (int)D(m["kills"]) * 4; }, "kills_per_minute", "kills_above_spawns");
        Expect("400 kills added to minute 9 (spawns too)", l => { var m = Minutes(l)[9]!; m["kills"] = (int)D(m["kills"]) + 400; m["spawns"] = (int)D(m["spawns"]) + 400; }, "kills_per_minute");
        Expect("gold of minute 3 + 1000", l => { var m = Minutes(l)[3]!; m["gold"] = (int)D(m["gold"]) + 1000; }, "gold_per_minute", "gold_run_total");
        Expect("XP of minute 4 x5", l => { var m = Minutes(l)[4]!; m["xp"] = (int)D(m["xp"]) * 5; }, "xp_per_minute");
        Expect("level of the last minute + 10", l => { var m = Minutes(l)[^1]!; m["level"] = (int)D(m["level"]) + 10; }, "level_above_xp");
        Expect("damage dealt of minute 6 x12", l => { var m = Minutes(l)[6]!; m["dealt"] = D(m["dealt"]) * 12; }, "damage_dealt_per_minute");
        Expect("6 chests in minute 2", l => Minutes(l)[2]!["chests"] = 6, "chests_per_minute");
        Expect("damage taken of minute 15 wiped (hull kept)", l =>
        {
            var m = Minutes(l)[15]!;
            m["taken"] = 0; m["healed"] = 0; m["hull"] = D(Minutes(l)[14]!["hull"]) + 0; // took nothing, healed nothing, but the hull below shows the
            var next = Minutes(l)[16]!; next["taken"] = 0; next["hull"] = D(m["hull"]) + 40;     // ...next minute rising 40 with no heal
        }, "hull_above_damage_and_heals");
        Expect("survived 40 minutes into overtime", l =>
        {
            var minutes = Minutes(l);
            var last = minutes[^1]!;
            for (var i = minutes.Count; i < 56; i++) minutes.Add(new JsonObject
            {
                ["m"] = i, ["spawns"] = 60, ["kills"] = 50, ["eliteKills"] = 0, ["bossKills"] = 0, ["gold"] = 20, ["xp"] = 40, ["level"] = D(last["level"]),
                ["taken"] = 0, ["dealt"] = 1000, ["healed"] = 0, ["pickups"] = 0, ["chests"] = 0, ["pois"] = 0, ["alive"] = 10, ["hull"] = 50, ["maxHull"] = D(last["maxHull"]),
                ["wk"] = new JsonArray(),
            });
            l["seconds"] = 55 * 60 + 30;
        }, "overtime_survival");
    }

    /// <summary>Every output number of the voyage scaled by <paramref name="k"/>, the level raised to what the scaled XP reaches:
    /// a forgery that keeps every sum, the XP and the level consistent.</summary>
    static JsonObject Scaled(double k)
    {
        var log = TodaysVoyage();
        double xp = 0;
        foreach (var n in Minutes(log))
        {
            var m = n!.AsObject();
            foreach (var f in new[] { "kills", "spawns", "gold", "xp", "eliteKills", "bossKills" }) m[f] = (int)Math.Round(D(m[f]) * k);
            m["dealt"] = Math.Round(D(m["dealt"]) * k, 1);
            xp += D(m["xp"]);
            m["level"] = Math.Max(D(m["level"]), Math.Max(1, Shipped.LevelFromXp(xp)));
        }
        return log;
    }

    [Fact]
    public void ConsistentForgery_IsFlagged()
    {
        var v = Check(Scaled(2.0));
        Assert.Equal(Verdicts.Flagged, v.Status);
        Assert.Contains("kills_per_minute", Codes(v));
        _out.WriteLine("x2: " + string.Join(", ", v.Reasons.Select(r => $"{r.Code} [{r.Rule}] minute {r.Minute}: {r.Value} > {r.Bound}")));

        // the smallest consistent scale-up of today's voyage that is caught (for the owner's report)
        double? caught = null;
        for (var k = 1.0; k <= 3.0 && caught == null; k += 0.05)
            if (Check(Scaled(k)).Status == Verdicts.Flagged) caught = k;
        Assert.NotNull(caught);
        Assert.True(Check(Scaled(1.0)).Status == Verdicts.Pass);
        _out.WriteLine($"today's voyage scaled up consistently is first flagged at x{caught:0.00}: {string.Join(", ", Codes(Check(Scaled(caught!.Value))))}");
    }

    // ---- the API: verdicts stored, the board's "checked" flag, shadow mode ---------------------------------------------

    static JsonObject Upload(JsonObject log)
    {
        var minutes = Minutes(log);
        return new JsonObject
        {
            ["kills"] = minutes.Sum(m => (int)D(m!["kills"])), ["runSeconds"] = (int)Math.Floor(D(log["seconds"])), ["level"] = (int)D(minutes[^1]!["level"]),
            ["captain"] = "rook", ["mapId"] = "saltwind", ["buildHash"] = "abc", ["clientVersion"] = "0.1.0", ["log"] = log,
        };
    }

    async Task<RunVerdict> VerdictOf(long runId)
    {
        using var scope = _api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GameDb>().RunVerdicts.AsNoTracking().SingleAsync(v => v.RunId == runId);
    }

    [Fact]
    public async Task Uploads_GetAStoredVerdict_TheBoardShowsChecked_AndAFlaggedRunStillCounts()
    {
        var honest = await _api.NewPlayerAsync("Honest");
        var forger = await _api.NewPlayerAsync("Forger");
        var old = await _api.NewPlayerAsync("OldBuild");

        var res = await honest.Http.PostAsJsonAsync("/api/runs", Upload(TodaysVoyage()));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.ReadJson();
        Assert.True(body.GetProperty("checked").GetBoolean());
        var pass = await VerdictOf(body.GetProperty("runId").GetInt64());
        Assert.Equal((Verdicts.Pass, "", "", Shipped.RulesVersion, "0.1.0+session", Shipped.ConfigHash), (pass.Status, pass.ReasonCodes, pass.RuleIds, pass.RulesVersion, pass.ClientBuild, pass.ConfigHash));

        // a forgery: accepted all the same (shadow mode), stored flagged with its reasons and rule ids, and it leads the board
        res = await forger.Http.PostAsJsonAsync("/api/runs", Upload(Scaled(2.5)));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        body = await res.ReadJson();
        Assert.True(body.GetProperty("accepted").GetBoolean());
        Assert.False(body.GetProperty("checked").GetBoolean());
        var flagged = await VerdictOf(body.GetProperty("runId").GetInt64());
        Assert.Equal(Verdicts.Flagged, flagged.Status);
        Assert.Contains("kills_per_minute", flagged.ReasonCodes.Split(','));
        Assert.Contains("PR-KILLS-MIN", flagged.RuleIds.Split(','));
        Assert.Contains("\"bound\":", flagged.DetailsJson);
        _out.WriteLine($"forgery stored: {flagged.ReasonCodes} ({flagged.RuleIds})");

        // an older client: no log
        res = await old.Http.PostAsJsonAsync("/api/runs", new { kills = 10, runSeconds = 60, level = 2, captain = "rook", mapId = "saltwind" });
        body = await res.ReadJson();
        var outdated = await VerdictOf(body.GetProperty("runId").GetInt64());
        Assert.Equal((Verdicts.OutdatedClient, "no_log"), (outdated.Status, outdated.ReasonCodes));

        var board = await (await honest.Http.GetAsync("/api/leaderboard/weekly?mapId=saltwind")).ReadJson();
        var entries = board.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(3, entries.Count);
        Assert.Equal("Forger", entries[0].GetProperty("name").GetString()); // flagged: still on the board, still first
        Assert.False(entries[0].GetProperty("checked").GetBoolean());
        Assert.Equal("Honest", entries[1].GetProperty("name").GetString());
        Assert.True(entries[1].GetProperty("checked").GetBoolean());
        Assert.False(entries[2].GetProperty("checked").GetBoolean());
        Assert.True(board.GetProperty("me").GetProperty("checked").GetBoolean());
    }

    // ---- re-scoring ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task StoredRuns_AreRescored_WhenTheRulesVersionChanges()
    {
        var p = await _api.NewPlayerAsync();
        var a = (await (await p.Http.PostAsJsonAsync("/api/runs", Upload(TodaysVoyage()))).ReadJson()).GetProperty("runId").GetInt64();
        var b = (await (await p.Http.PostAsJsonAsync("/api/runs", new { kills = 10, runSeconds = 60, level = 2, captain = "rook", mapId = "saltwind" })).ReadJson()).GetProperty("runId").GetInt64();
        Assert.Equal(Verdicts.Pass, (await VerdictOf(a)).Status);
        var broken = TodaysVoyage();
        broken["seed"] = 0; // fails the structural checks: accepted as log_rejected (owner decision of 8 Oct)
        var c = (await (await p.Http.PostAsJsonAsync("/api/runs", Upload(broken))).ReadJson()).GetProperty("runId").GetInt64();
        Assert.Equal((Verdicts.LogRejected, "log_missing_seed"), ((await VerdictOf(c)).Status, (await VerdictOf(c)).ReasonCodes));

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var current = scope.ServiceProvider.GetRequiredService<PlausibilityService>();
        Assert.Equal(0, await current.RescoreAsync(db, clock)); // nothing to do: every run has this version's verdict

        // the next rules version: kills bounds halved (the voyage's kill rate now breaks them)
        var node = JsonNode.Parse(ShippedJson())!.AsObject();
        node["rulesVersion"] = Shipped.RulesVersion + 1;
        foreach (var r in node["rules"]!.AsArray())
            if (r!["id"]!.GetValue<string>() == "PR-KILLS-MIN")
                r["bounds"] = new JsonArray(r["bounds"]!.AsArray().Select(x => (JsonNode?)JsonValue.Create(Math.Max(1, Math.Floor(x!.GetValue<double>() / 2)))).ToArray());
        var next = new PlausibilityService(PlausibilityRuleSet.Parse(node.ToJsonString()), NullLogger<PlausibilityService>.Instance);
        Assert.Equal(3, await next.RescoreAsync(db, clock));

        var va = await VerdictOf(a);
        Assert.Equal((Verdicts.Flagged, Shipped.RulesVersion + 1), (va.Status, va.RulesVersion));
        Assert.Contains("PR-KILLS-MIN", va.RuleIds);
        var vb = await VerdictOf(b);
        Assert.Equal((Verdicts.OutdatedClient, Shipped.RulesVersion + 1), (vb.Status, vb.RulesVersion));
        var vc = await VerdictOf(c); // a rejected log stays rejected under any rules version
        Assert.Equal((Verdicts.LogRejected, "log_missing_seed", "PR-LOG", Shipped.RulesVersion + 1), (vc.Status, vc.ReasonCodes, vc.RuleIds, vc.RulesVersion));
        Assert.Equal(0, await next.RescoreAsync(db, clock)); // idempotent

        var board = await (await p.Http.GetAsync("/api/leaderboard/weekly?mapId=saltwind")).ReadJson();
        Assert.False(board.GetProperty("entries")[0].GetProperty("checked").GetBoolean()); // the board follows the new verdict
    }
}
