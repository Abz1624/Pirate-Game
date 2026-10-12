using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>Island defence designs (AddIslandDefence): the cost table, budgets, validation, the default design and
/// presets, the endpoints, and raids that conquer the island (survive the waves, sink the guardian).</summary>
public sealed class DefenceTests
{
    static async Task SetRumAsync(ApiHost host, Wallet w, long rum)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var p = await db.Players.FirstAsync(x => x.WalletAddress == w.Key);
        p.Rum = rum;
        await db.SaveChangesAsync();
    }

    static object Design(Func<int, object[]> enemies, string guardian = "barnacle_king", int minute = 5) => new
    {
        windows = Enumerable.Range(1, 15).Select(m => new { minute = m, enemies = enemies(m) }),
        guardian = new { kind = guardian, minute },
    };

    // ---- rules ----

    [Fact]
    public void Budgets_GrowWithTheMinute_AndTheDefenceBuildings()
    {
        Assert.Equal(26, IslandRules.DefenceBudget(1));
        Assert.Equal(50, IslandRules.DefenceBudget(5));
        Assert.Equal(110, IslandRules.DefenceBudget(15));
        Assert.Equal(29, IslandRules.DefenceBudget(1, cannonTower: 1)); // 26 x 1.15
        Assert.Equal(37, IslandRules.DefenceBudget(1, cannonTower: 1, walls: 2)); // 26 x 1.45
        Assert.Equal(209, IslandRules.DefenceBudget(15, 3, 3)); // 110 x 1.9
        Assert.Equal(1, IslandRules.DefenceCost("deckhand_dinghy"));
        Assert.Equal(4, IslandRules.DefenceCost("gunner_cog"));
        Assert.Equal(12, IslandRules.DefenceCost("elite_hammerhead"));
        Assert.Equal(30, IslandRules.DefenceCost("reef_crab_miniboss"));
    }

    [Fact]
    public void DefaultDesign_AndEveryPreset_AreValidOnABareIsland_AndUseMostOfTheBudget()
    {
        foreach (var design in DefenceService.Presets().Select(p => p.Design).Prepend(DefenceService.DefaultDesign()))
        {
            var (normalized, errors) = DefenceService.Validate(design, 0, 0);
            Assert.Empty(errors);
            Assert.Equal(15, normalized.Windows!.Count);
            Assert.All(normalized.Windows, w => Assert.NotEmpty(w.Enemies!));
            Assert.All(normalized.Windows, w => Assert.InRange(DefenceService.WindowCost(w), IslandRules.DefenceBudget(w.Minute) / 2, IslandRules.DefenceBudget(w.Minute)));
            Assert.Equal("barnacle_king", normalized.Guardian!.Kind);
        }
        Assert.Equal(5, DefenceService.DefaultDesign().Guardian!.Minute);
        Assert.Equal(new[] { "swarm", "gunline", "elite_guard" }, DefenceService.Presets().Select(p => p.Id));
    }

    [Fact]
    public void Validate_ReportsEveryProblemPerWindow()
    {
        var design = new DefenceDesign(new List<DefenceWindow>
        {
            new(1, new() { new("deckhand_dinghy", 27) }),                         // 27 > 26
            new(2, new() { new("ghost_ship", 1), new("Swordfish", 3) }),          // unknown kind; case is normalized
            new(3, new() { new("swordfish", 61) }),                               // over 60
            new(4, new() { new("shark", -1) }),                                   // negative
            new(5, new() { new("shark", 1), new("shark", 2) }),                   // duplicate
            new(5, new()),                                                        // duplicate minute
            new(16, new()),                                                       // no such minute
            new(6, new() { new("cannonboat", 0) }),                               // zero is fine (dropped)
        }, new DefenceGuardian("kraken", 1));
        var (normalized, errors) = DefenceService.Validate(design, 0, 0);
        Assert.Contains(errors, e => e is { Minute: 1, Code: "over_budget" });
        Assert.Contains(errors, e => e is { Minute: 2, Code: "unknown_kind" });
        Assert.Contains(errors, e => e is { Minute: 3, Code: "invalid_count" });
        Assert.Contains(errors, e => e is { Minute: 4, Code: "invalid_count" });
        Assert.Contains(errors, e => e is { Minute: 5, Code: "duplicate_kind" });
        Assert.Contains(errors, e => e is { Minute: 5, Code: "duplicate_minute" });
        Assert.Contains(errors, e => e is { Minute: null, Code: "invalid_minute" });
        Assert.Contains(errors, e => e is { Minute: null, Code: "unknown_guardian" });
        Assert.Contains(errors, e => e is { Minute: null, Code: "invalid_guardian_minute" });
        Assert.Equal(9, errors.Count);
        Assert.Equal("swordfish", normalized.Windows![1].Enemies!.Single().Kind);
        Assert.Empty(normalized.Windows[5].Enemies!);
        // with walls 1 the first window's budget is 29: 27 dinghies fit
        Assert.DoesNotContain(DefenceService.Validate(design, 0, 1).Errors, e => e.Minute == 1);
    }

    [Fact]
    public void Outcome_WinNeedsTheGuardianSunk_PartialIsReachingIt()
    {
        Assert.Equal("won", RaidService.Outcome(true, false, true, 7, 5));
        Assert.Equal("won", RaidService.Outcome(false, false, true, null, 5));
        Assert.Equal("partial", RaidService.Outcome(true, false, false, 5, 5));
        Assert.Equal("partial", RaidService.Outcome(false, false, false, 12, 5));
        Assert.Equal("lost", RaidService.Outcome(true, false, false, 4, 5));
        Assert.Equal("lost", RaidService.Outcome(true, false, null, 4, 5));
        // older clients (no guardian fields): the won/partial flags
        Assert.Equal("won", RaidService.Outcome(true, false, null, null, 5));
        Assert.Equal("partial", RaidService.Outcome(true, true, null, null, 5));
        Assert.Equal("lost", RaidService.Outcome(false, false, null, null, 5));
    }

    // ---- endpoints ----

    [Fact]
    public async Task Catalog_ListsKindsCostsBossesPresetsAndTheDefault()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var cat = await Ok(await host.Anon().GetAsync("/api/defence/catalog"));
        Assert.Equal(15, cat.GetProperty("windows").GetInt32());
        Assert.Equal(60, cat.GetProperty("maxCount").GetInt32());
        Assert.Equal(960, cat.GetProperty("raidTimeLimitSeconds").GetInt32());
        var kinds = cat.GetProperty("kinds").EnumerateArray().ToDictionary(k => k.GetProperty("kind").GetString()!, k => k.GetProperty("cost").GetInt32());
        Assert.Equal(1, kinds["deckhand_dinghy"]);
        Assert.Equal(30, kinds["reef_crab_miniboss"]);
        Assert.Equal(17, kinds.Count);
        Assert.Equal("barnacle_king", cat.GetProperty("bosses")[0].GetProperty("kind").GetString());
        Assert.True(cat.GetProperty("bosses")[0].GetProperty("isDefault").GetBoolean());
        Assert.Equal(26, cat.GetProperty("budget").GetProperty("baseBudgets")[0].GetInt32());
        Assert.Equal(5, cat.GetProperty("guardianMinute").GetProperty("default").GetInt32());
        Assert.Equal(15, cat.GetProperty("defaultDesign").GetProperty("windows").GetArrayLength());
        var presets = cat.GetProperty("presets");
        Assert.Equal(3, presets.GetArrayLength());
        Assert.Equal("Elite guard", presets[2].GetProperty("name").GetString());
        Assert.Equal(12, presets[2].GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32());
    }

    [Fact]
    public async Task Island_WithoutADesign_ServesTheDefault_OwnerSavesAValidDesign_BuildingsRaiseTheBudget()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var other = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();

        var view = await Ok(await host.Anon().GetAsync($"/api/islands/{id}/defence"));
        Assert.False(view.GetProperty("isCustom").GetBoolean());
        Assert.False(view.GetProperty("isMine").GetBoolean());
        Assert.Equal(26, view.GetProperty("budgets")[0].GetInt32());
        var w1 = view.GetProperty("design").GetProperty("windows")[0];
        Assert.Equal(1, w1.GetProperty("minute").GetInt32());
        Assert.Equal(20, w1.GetProperty("cost").GetInt32());
        Assert.Equal(26, w1.GetProperty("budget").GetInt32());
        Assert.Equal("barnacle_king", view.GetProperty("design").GetProperty("guardian").GetProperty("kind").GetString());
        Assert.Equal(5, view.GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32());
        Assert.Equal(12, view.GetProperty("costs").GetProperty("elite_hammerhead").GetInt32());

        // 28 dinghies in every window: over minute 1's budget (26) on a bare island
        var heavy = Design(_ => new object[] { new { kind = "deckhand_dinghy", count = 28 } }, minute: 9);
        await AssertError(await other.Http.PutAsJsonAsync($"/api/islands/{id}/defence", heavy), HttpStatusCode.Forbidden, "not_your_island");
        var bad = await owner.Http.PutAsJsonAsync($"/api/islands/{id}/defence", heavy);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = await bad.ReadJson();
        Assert.Equal("invalid_defence", body.GetProperty("error").GetString());
        var errs = body.GetProperty("errors");
        Assert.Equal(1, errs.GetArrayLength());
        Assert.Equal(1, errs[0].GetProperty("minute").GetInt32());
        Assert.Equal("over_budget", errs[0].GetProperty("code").GetString());

        var bad2 = await (await owner.Http.PutAsJsonAsync($"/api/islands/{id}/defence",
            Design(m => m == 3 ? new object[] { new { kind = "ghost_ship", count = 1 } } : Array.Empty<object>(), minute: 16))).ReadJson();
        Assert.Equal(2, bad2.GetProperty("errors").GetArrayLength());

        // walls level 1: +15%, minute 1's budget is 29
        await SetRumAsync(host, owner, 1000);
        await Ok(await owner.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "walls" }));
        var saved = await Ok(await owner.Http.PutAsJsonAsync($"/api/islands/{id}/defence", heavy));
        Assert.True(saved.GetProperty("isCustom").GetBoolean());
        Assert.True(saved.GetProperty("isMine").GetBoolean());
        Assert.Equal(15, saved.GetProperty("budgetBonusPercent").GetInt32());
        Assert.Equal(29, saved.GetProperty("budgets")[0].GetInt32());

        var read = await Ok(await other.Http.GetAsync($"/api/islands/{id}/defence"));
        Assert.True(read.GetProperty("isCustom").GetBoolean());
        Assert.Equal(9, read.GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32());
        var win = read.GetProperty("design").GetProperty("windows")[14];
        Assert.Equal(28, win.GetProperty("enemies")[0].GetProperty("count").GetInt32());
        Assert.Equal(28, win.GetProperty("cost").GetInt32());

        // a preset from the catalog applies as is
        var cat = await Ok(await host.Anon().GetAsync("/api/defence/catalog"));
        var swarm = cat.GetProperty("presets")[0].GetProperty("design");
        var applied = await Ok(await owner.Http.PutAsync($"/api/islands/{id}/defence",
            new StringContent(swarm.GetRawText(), System.Text.Encoding.UTF8, "application/json")));
        Assert.Equal(8, applied.GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32());

        await AssertError(await owner.Http.GetAsync($"/api/islands/{Guid.NewGuid()}/defence"), HttpStatusCode.NotFound, "island_not_found");
    }

    [Fact]
    public async Task Raid_StartReturnsTheDesign_ResultNeedsTheGuardianSunkToWin()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await Ok(await owner.Http.PutAsJsonAsync($"/api/islands/{id}/defence",
            Design(m => new object[] { new { kind = "gunner_cog", count = m }, new { kind = "swordfish", count = 2 } }, minute: 6)));
        await SetRumAsync(host, raider, 500);
        host.Clock.Advance(TimeSpan.FromHours(4)); // exposed 192

        // 1. lost: did not reach the guardian
        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        Assert.True(start.TryGetProperty("defender", out _)); // the old fields stay
        Assert.Equal(960, start.GetProperty("defender").GetProperty("timeLimitSeconds").GetInt32());
        var defence = start.GetProperty("defence");
        Assert.True(defence.GetProperty("isCustom").GetBoolean());
        Assert.Equal(960, defence.GetProperty("timeLimitSeconds").GetInt32());
        Assert.Equal(start.GetProperty("defender").GetProperty("seed").GetInt32(), defence.GetProperty("seed").GetInt32());
        var guardian = defence.GetProperty("design").GetProperty("guardian");
        Assert.Equal("barnacle_king", guardian.GetProperty("kind").GetString());
        Assert.Equal(6, guardian.GetProperty("minute").GetInt32());
        Assert.Equal(300, guardian.GetProperty("arrivesAtSecond").GetInt32());
        var w3 = defence.GetProperty("design").GetProperty("windows")[2];
        Assert.Equal("gunner_cog", w3.GetProperty("enemies")[0].GetProperty("kind").GetString());
        Assert.Equal(3, w3.GetProperty("enemies")[0].GetProperty("count").GetInt32());
        Assert.Equal(20, (start.GetProperty("expiresAt").GetDateTime() - start.GetProperty("startedAt").GetDateTime()).TotalMinutes);

        var raidId = start.GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromMinutes(4));
        var lost = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, guardianSunk = false, minuteReached = 5, seconds = 240 }));
        Assert.Equal("lost", lost.GetProperty("result").GetString());
        Assert.Equal(0, lost.GetProperty("rumLooted").GetInt64());

        // 2. a sunk guardian before it arrives is refused; a partial for reaching it
        raidId = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromMinutes(16) + TimeSpan.FromSeconds(30));
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, guardianSunk = true, minuteReached = 4, seconds = 300 }),
            HttpStatusCode.BadRequest, "invalid_result");
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, guardianSunk = true, minuteReached = 17, seconds = 300 }),
            HttpStatusCode.BadRequest, "invalid_minute");
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, guardianSunk = true, minuteReached = 9, seconds = 1021 }),
            HttpStatusCode.BadRequest, "invalid_seconds");
        var partial = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = false, guardianSunk = false, minuteReached = 16, seconds = 990 }));
        Assert.Equal("partial", partial.GetProperty("result").GetString());
        Assert.Equal(24, partial.GetProperty("rumLooted").GetInt64()); // 192 x 12.5%
        Assert.Equal(16, partial.GetProperty("minuteReached").GetInt32());
        Assert.Equal(6, partial.GetProperty("guardianMinute").GetInt32());

        // 3. won: the guardian sunk (after the shield)
        host.Clock.Advance(TimeSpan.FromHours(4));
        raidId = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromMinutes(8));
        var won = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, guardianSunk = true, minuteReached = 8, seconds = 470 }));
        Assert.Equal("won", won.GetProperty("result").GetString());
        Assert.True(won.GetProperty("rumLooted").GetInt64() > 0);
        Assert.True(won.GetProperty("guardianSunk").GetBoolean());
        var history = await Ok(await owner.Http.GetAsync("/api/raids/mine"));
        Assert.Equal(new[] { "won", "partial", "lost" }, history.GetProperty("defences").EnumerateArray().Select(r => r.GetProperty("result").GetString()));
    }

    [Fact]
    public async Task Raid_OnAnIslandWithoutADesign_UsesTheDefault()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await SetRumAsync(host, raider, 100);
        var defence = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("defence");
        Assert.False(defence.GetProperty("isCustom").GetBoolean());
        Assert.Equal(5, defence.GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32());
        Assert.Equal(15, defence.GetProperty("design").GetProperty("windows").GetArrayLength());
        Assert.Equal("deckhand_dinghy", defence.GetProperty("design").GetProperty("windows")[0].GetProperty("enemies")[0].GetProperty("kind").GetString());
    }
}
