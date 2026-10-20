using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9j (20 Oct, owner: "make the server decide or verify game results"): the relay's hull estimate and its tie-break,
/// stakes settled only on a server decision, the enforced raid timing (fed with the game client's own formulas), the raid
/// pair limit and defence record switches, raid and race run-log verdicts in shadow mode, and the race's server start.
/// </summary>
public sealed class HullEstimateTests
{
    [Fact]
    public void Hit_LandsScaledByTheAssumedArmour_CappedAt5Percent_ThenPoolsForHalfASecond()
    {
        var e = new HullEstimate();
        e.ReportMax(0, 100, 1);
        e.Hit(1.0, 100, 0.25); // 100 x 0.06 x 0.5 = 3
        Assert.Equal(97, e.Hull, 0); // plus the regeneration allowance between events
        e.Hit(1.2, 100, 0.25); // inside the window: pooled
        e.Hit(1.3, 100, 0.25);
        Assert.Equal(97, e.Hull, 0); // plus the regeneration allowance between events
        e.Flush(1.4, 0.25); // window still open
        Assert.Equal(97, e.Hull, 0); // plus the regeneration allowance between events
        e.Flush(1.5, 0.25); // pooled 6, capped at 5
        Assert.Equal(92, e.Hull, 0);
        e.Hit(1.6, 1000, 0.25); // inside the new window: pooled, not landed
        Assert.Equal(92, e.Hull, 0);
    }

    [Fact]
    public void MaxHull_IsClampedAtTheServerLevel_KeepsTheDamage_AndHealsAreCapped()
    {
        var e = new HullEstimate();
        e.ReportMax(0, 100, 1);
        e.Hit(1, 200, 0.25); // 6 -> capped 5
        e.ReportMax(2, 200, 1); // a level-up: the loss is kept
        Assert.Equal(195, e.Hull, 0);
        e.ReportMax(3, 100_000, 1); // a cheat's max hull is clamped at level 1's ceiling
        Assert.Equal(HullEstimate.MaxHullCeiling(1), e.MaxHull);
        Assert.Equal(1, e.ClampedReports);
        e.Heal(4, ArenaRules.HealthBarrelHealFraction);
        Assert.Equal(e.MaxHull, e.Hull, 6);
    }

    [Fact]
    public void Verdicts_ImmortalClient_IsServerSunkClientAlive_StormDeathIsClientSunkFirst()
    {
        var immortal = new HullEstimate();
        immortal.ReportMax(0, 100, 1);
        for (var t = 1.0; t < 40; t += 0.5) immortal.Hit(t, 500, 0.25);
        Assert.NotNull(immortal.BelowMarginAt);
        Assert.Equal(HullChecks.ServerSunkClientAlive, immortal.Verdict(clientSunk: false, tracked: true));
        Assert.Equal(HullChecks.Agree, immortal.Verdict(clientSunk: true, tracked: true));

        var storm = new HullEstimate();
        storm.ReportMax(0, 100, 1);
        storm.Hit(1, 50, 0.25);
        Assert.Equal(HullChecks.ClientSunkFirst, storm.Verdict(clientSunk: true, tracked: true));
        Assert.Equal(HullChecks.Agree, storm.Verdict(clientSunk: false, tracked: true));
        Assert.Equal(HullChecks.NotTracked, storm.Verdict(clientSunk: false, tracked: false));
    }
}

public sealed class RaidAndRaceCheckTests
{
    /// <summary>The game client's own report (RaidHud.Report and MinuteReached): seconds = min(ceil(run), floor(wall) - 1);
    /// minute = run >= 960 ? 16 : min(15, floor(run / 60) + 1), a sunk guardian's own minute at least.</summary>
    static (int seconds, int minute) Client(double run, double wall, bool sunk, int guardianMinute)
    {
        var seconds = (int)Math.Max(1, Math.Min(Math.Ceiling(run), Math.Floor(wall) - 1));
        var m = run >= IslandRules.RaidTimeLimitSeconds ? 16 : Math.Min(15, (int)Math.Floor(run / 60) + 1);
        return (seconds, sunk ? Math.Max(m, guardianMinute) : m);
    }

    [Theory]
    [InlineData(2, 60.0)]
    [InlineData(5, 240.0)]
    [InlineData(5, 240.4)]
    [InlineData(5, 299.9)]
    [InlineData(15, 840.0)]
    public void HonestGuardianSink_AtItsArrival_PassesTheTimingCheck(int guardianMinute, double run)
    {
        Assert.Equal((guardianMinute - 1) * 60, RaidChecks.GuardianArrivalSeconds(guardianMinute));
        // the wall clock is the run clock (no loading time at all: the tightest case) and the server saw the same
        var (seconds, minute) = Client(run, run, true, guardianMinute);
        Assert.Null(RaidChecks.Timing(Raid.Won, seconds, run, minute, guardianMinute));
    }

    [Theory]
    [InlineData(240.0, 5)]   // partial at the guardian's arrival
    [InlineData(959.0, 5)]   // near the time limit
    [InlineData(960.0, 5)]   // the time limit: minute 16
    [InlineData(1000.0, 15)]
    public void HonestPartialOrTimeout_PassesTheTimingCheck(double run, int guardianMinute)
    {
        var (seconds, minute) = Client(run, run + 3, false, guardianMinute);
        var outcome = RaidService.Outcome(false, false, false, minute, guardianMinute);
        Assert.Null(RaidChecks.Timing(outcome, seconds, run + 4, minute, guardianMinute));
    }

    [Fact]
    public void ImpossibleClaims_AreRefused()
    {
        // a guardian (minute 5, 240 s) claimed at 30 s by the report, or by the server's clock
        Assert.Equal("too_early", RaidChecks.Timing(Raid.Won, 30, 300, null, 5)!.Code);
        Assert.Equal("invalid_minute", RaidChecks.Timing(Raid.Won, 30, 300, 5, 5)!.Code); // minute 5 is not reached in 30 s
        Assert.Equal("too_early", RaidChecks.Timing(Raid.Won, 300, 30, 5, 5)!.Code);
        Assert.Equal("too_early", RaidChecks.Timing(RaidService.Partial, 100, 100, null, 5)!.Code); // old flags
        Assert.Equal("invalid_minute", RaidChecks.Timing(Raid.Lost, 85, 85, 6, 5)!.Code);
        Assert.Equal("invalid_minute", RaidChecks.Timing(Raid.Lost, 900, 900, 16, 5)!.Code);
        Assert.Null(RaidChecks.Timing(Raid.Lost, 30, 30, 1, 5)); // a loss needs nothing
        // the 5 s slack, and not one second more
        Assert.Null(RaidChecks.Timing(Raid.Won, 235, 235, 5, 5));
        Assert.NotNull(RaidChecks.Timing(Raid.Won, 234, 300, 5, 5));
    }

    [Fact]
    public void RaceCheck_StartTimeAndLog()
    {
        var log = JsonDocument.Parse(RunLogTests.Log(kills: 100, seconds: 150.3, level: 5).ToJsonString()).RootElement;
        Assert.Equal(RaceChecks.NoStart, RaceChecks.Check(150.3, null, 100, log));
        Assert.Equal(RaceChecks.Ok, RaceChecks.Check(150.3, 160, 100, log));
        Assert.Equal(RaceChecks.Ok, RaceChecks.Check(150.3, 125, 100, log)); // the start landed late: inside the slack
        Assert.Equal(RaceChecks.TooFast, RaceChecks.Check(150.3, 100, 100, log));
        Assert.Equal(RaceChecks.TimeMismatch, RaceChecks.Check(70, 160, 100, log)); // the log had 66 kills by 120 s
        Assert.Equal(RaceChecks.Ok, RaceChecks.Check(150.3, 160, 100, null)); // no log: the time check only
    }
}

public sealed class M9jServerResultTests
{
    // ---------------------------------------------------------------- raids

    static async Task<(ApiHost host, Wallet owner, Wallet raider, Guid islandId)> RaidWorldAsync(Dictionary<string, string?>? extra = null)
    {
        var (host, _, _) = NewHost(extra: extra);
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        host.Clock.Advance(TimeSpan.FromHours(4));
        await EarnRumAsync(host, raider, 1000);
        return (host, owner, raider, id);
    }

    [Fact]
    public async Task Raid_InstantGuardianClaim_IsRefused_TheHonestOneAfterItsArrivalWins_WithItsLogVerdict()
    {
        var (host, _, raider, id) = await RaidWorldAsync();
        using var _h = host;
        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        var raidId = start.GetProperty("raidId").GetGuid();
        var seed = start.GetProperty("defence").GetProperty("seed").GetInt32();
        var guardianMinute = start.GetProperty("defence").GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32();
        var arrival = (guardianMinute - 1) * 60;

        // the old cheat: a sunk guardian 20 s in, reported as minute 5 (the old check only compared the minute)
        host.Clock.Advance(TimeSpan.FromSeconds(20));
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result",
            new { won = true, guardianSunk = true, minuteReached = guardianMinute, seconds = 19 }), HttpStatusCode.BadRequest, "invalid_minute");
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result",
            new { won = true, guardianSunk = true, seconds = 19 }), HttpStatusCode.BadRequest, "too_early");
        // the old won flag with no minute is no way round it
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 19 }),
            HttpStatusCode.BadRequest, "too_early");

        // honest: the guardian sunk 30 s after its arrival, with the raid's run log
        host.Clock.Advance(TimeSpan.FromSeconds(arrival + 30 + 5 - 20));
        var log = RunLogTests.Log(kills: 120, seconds: arrival + 30.4, level: 9, seed: seed);
        log["bossSummoned"] = arrival;
        log["bossDefeated"] = arrival + 30;
        log["end"] = "victory";
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result",
            new { won = true, guardianSunk = true, minuteReached = guardianMinute, seconds = arrival + 31, runLog = log }));
        Assert.Equal("won", result.GetProperty("result").GetString());
        // shadow mode: none of the raid's own checks flags the honest log (the test log's config hash is not the shipped
        // one, so the rate rules answer outdated_client), and the loot moved
        Assert.Contains(result.GetProperty("logVerdict").GetString(), new[] { "pass", "flagged", "outdated_client" });
        Assert.True(result.GetProperty("rumLooted").GetInt64() > 0);
        var mine = await Ok(await raider.Http.GetAsync("/api/raids/mine"));
        Assert.Equal(result.GetProperty("logVerdict").GetString(), mine.GetProperty("attacks")[0].GetProperty("logVerdict").GetString());
    }

    [Theory]
    [InlineData("seed", "seed_mismatch")]
    [InlineData("boss", "boss_before_arrival")]
    [InlineData("time", "time_mismatch")]
    [InlineData("broken", "log_rejected")]
    public async Task Raid_LogThatDisagrees_GetsItsVerdict_AndTheResultStands(string edit, string verdict)
    {
        var (host, _, raider, id) = await RaidWorldAsync();
        using var _h = host;
        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        var raidId = start.GetProperty("raidId").GetGuid();
        var seed = start.GetProperty("defence").GetProperty("seed").GetInt32();
        var guardianMinute = start.GetProperty("defence").GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32();
        var arrival = (guardianMinute - 1) * 60;
        host.Clock.Advance(TimeSpan.FromSeconds(arrival + 60));
        var log = RunLogTests.Log(kills: 120, seconds: arrival + 50.4, level: 9, seed: edit == "seed" ? seed + 1 : seed);
        log["bossDefeated"] = edit == "boss" ? 20 : arrival + 50;
        if (edit == "broken") log["format"] = "nope";
        var seconds = edit == "time" ? arrival + 20 : arrival + 51; // "time": the report is 30 s off the log
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result",
            new { won = true, guardianSunk = true, minuteReached = guardianMinute, seconds, runLog = log }));
        Assert.Equal("won", result.GetProperty("result").GetString());
        Assert.Equal(verdict, result.GetProperty("logVerdict").GetString());
        Assert.False(result.GetProperty("checked").GetBoolean());
    }

    [Fact]
    public async Task Raid_WinWithoutALog_IsRecordedAsNoLog_ALossNeedsNone()
    {
        var (host, _, raider, id) = await RaidWorldAsync();
        using var _h = host;
        var r1 = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        var lost = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r1}/result", new { won = false, seconds = 1 }));
        Assert.Equal(JsonValueKind.Null, lost.GetProperty("logVerdict").ValueKind);
        var r2 = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromSeconds(300));
        var won = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r2}/result", new { won = true, guardianSunk = true, minuteReached = 5, seconds = 290 }));
        Assert.Equal("won", won.GetProperty("result").GetString());
        Assert.Equal(RaidChecks.NoLog, won.GetProperty("logVerdict").GetString());
    }

    [Fact]
    public async Task RaidPairLimit_OffByDefault_OnRefusesTheNextStart_AndTheSwitchesReadBack()
    {
        var (host, _, raider, id) = await RaidWorldAsync();
        using (host)
        {
            for (var i = 0; i < 3; i++)
            {
                var r = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
                await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r}/result", new { won = false, seconds = 1 }));
            }
            var sw = await Ok(await host.Anon().GetAsync("/api/switches"));
            Assert.Equal(0, sw.GetProperty("raidPairLimitPerDay").GetInt32());
            Assert.False(sw.GetProperty("raidRecordOnePerDay").GetBoolean());
            Assert.False(sw.GetProperty("relayHullEnforce").GetBoolean());
        }

        var (on, _, raider2, id2) = await RaidWorldAsync(new() { ["RAID_PAIR_LIMIT_PER_DAY"] = "2" });
        using (on)
        {
            for (var i = 0; i < 2; i++)
            {
                var r = (await Ok(await raider2.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id2 }))).GetProperty("raidId").GetGuid();
                await Ok(await raider2.Http.PostAsJsonAsync($"/api/raids/{r}/result", new { won = false, seconds = 1 }));
            }
            var rumBefore = await RumAsync(raider2);
            await AssertError(await raider2.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id2 }), HttpStatusCode.TooManyRequests, "raid_pair_limit");
            Assert.Equal(rumBefore, await RumAsync(raider2)); // no fee for a refused start
            on.Clock.Advance(TimeSpan.FromHours(24));
            await Ok(await raider2.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id2 }));
        }
    }

    [Fact]
    public async Task DefenceRecord_OnePerAttackerAndDay_WhenSwitchedOn()
    {
        foreach (var on in new[] { false, true })
        {
            var (host, owner, raider, id) = await RaidWorldAsync(new() { ["RAID_RECORD_ONE_PER_DAY"] = on ? "true" : "false" });
            using var _h = host;
            for (var i = 0; i < 3; i++)
            {
                var r = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
                await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r}/result", new { won = false, seconds = 1 }));
            }
            var record = (await Ok(await owner.Http.GetAsync($"/api/islands/{id}"))).GetProperty("defenceRecord");
            Assert.Equal(on ? 1 : 3, record.GetProperty("raidsAgainst").GetInt32());
            Assert.Equal(on ? 1 : 3, record.GetProperty("defended").GetInt32());
        }
    }

    // ---------------------------------------------------------------- daily race

    [Fact]
    public async Task Race_StartThenEntry_IsCheckedAgainstTheServersClock_AndTheLog_ShadowMode()
    {
        using var host = new ApiHost();
        var p = await host.NewPlayerAsync("Racer");
        var race = (await Ok(await host.Anon().GetAsync("/api/race/daily"))).GetProperty("race");
        var seed = race.GetProperty("seed").GetInt32();

        // no start: accepted, unchecked
        var noStart = await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 200, kills = 100 }));
        Assert.Equal(RaceChecks.NoStart, noStart.GetProperty("raceCheck").GetString());
        Assert.False(noStart.GetProperty("checked").GetBoolean());

        // a start, then a time far shorter than the time since: too fast, still ranked
        var started = await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/start", new { }));
        Assert.Equal(1, started.GetProperty("starts").GetInt32());
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        var fast = await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 150, kills = 100 }));
        Assert.Equal(RaceChecks.TooFast, fast.GetProperty("raceCheck").GetString());
        Assert.True(fast.GetProperty("improved").GetBoolean());

        // honest: a start, 150.3 s of racing (plus loading), the log ends at the 100th kill
        await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/start", new { }));
        host.Clock.Advance(TimeSpan.FromSeconds(156));
        var log = RunLogTests.Log(kills: 100, seconds: 140.3, level: 5, seed: seed);
        var honest = await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 140.3, kills = 100, level = 5, log }));
        Assert.Equal(RaceChecks.Ok, honest.GetProperty("raceCheck").GetString());
        Assert.True(honest.GetProperty("serverSeconds").GetDouble() >= 156);
        var board = await Ok(await host.Anon().GetAsync("/api/race/daily/board"));
        Assert.Equal(RaceChecks.Ok, board.GetProperty("entries")[0].GetProperty("raceCheck").GetString());
        Assert.Equal(honest.GetProperty("verdict").GetString() == "pass", board.GetProperty("entries")[0].GetProperty("checked").GetBoolean());
    }

    [Fact]
    public async Task Race_StartIsKeyedByTheRaceDate_YesterdaysRaceInTheGrace()
    {
        using var host = new ApiHost();
        var p = await host.NewPlayerAsync("Racer");
        var today = host.Clock.UtcNow.Date;
        var seed = (await Ok(await host.Anon().GetAsync("/api/race/daily"))).GetProperty("race").GetProperty("seed").GetInt32();
        host.Clock.UtcNow = today.AddDays(1).AddMinutes(-3); // 23:57
        await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/start", new { date = today.ToString("yyyy-MM-dd") }));
        host.Clock.Advance(TimeSpan.FromMinutes(5)); // 00:02, inside the 30-minute grace
        var entry = await Ok(await p.Http.PostAsJsonAsync("/api/race/daily/entry",
            new { date = today.ToString("yyyy-MM-dd"), seed, timeSeconds = 250, kills = 100 }));
        Assert.Equal(RaceChecks.Ok, entry.GetProperty("raceCheck").GetString());
        Assert.Equal("race_not_open", (await (await p.Http.PostAsJsonAsync("/api/race/daily/start",
            new { date = today.AddDays(2).ToString("yyyy-MM-dd") })).ReadJson()).GetProperty("error").GetString());
    }
}
