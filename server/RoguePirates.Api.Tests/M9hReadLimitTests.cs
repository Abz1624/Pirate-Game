using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using Solnet.Wallet;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>V5 M9h (the review's read finding): GETs that call the Solana RPC are rate limited per IP, and island and raid
/// reads neither take the economy write lock nor write.</summary>
public sealed class M9hReadLimitTests
{
    [Fact]
    public async Task WalletBalance_IsRateLimitedPerIp_OtherReadsAreNot()
    {
        var (host, rpc, _) = NewHost(extra: new() { ["RPC_READ_RATE_LIMIT_PER_MINUTE"] = "3" });
        using var _h = host;
        var anon = host.Anon();
        var wallet = new Account().PublicKey.Key;
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/wallet/balance?wallet={wallet}")).StatusCode);
        var limited = await anon.GetAsync($"/api/wallet/balance?wallet={wallet}");
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.Equal("rate_limited", (await limited.ReadJson()).GetProperty("error").GetString());
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/islands")).StatusCode);
    }

    [Fact]
    public async Task IslandAndRaidReads_DoNotWaitForTheEconomyLock_AndDoNotWrite()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await EarnRumAsync(host, raider, 300);
        await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        host.Clock.Advance(TimeSpan.FromMinutes(21)); // the raid is past its timeout; the island brewed RUM

        DateTime storedAccrual;
        using (var scope = host.Services.CreateScope())
            storedAccrual = (await scope.ServiceProvider.GetRequiredService<GameDb>().Islands.AsNoTracking().SingleAsync()).LastAccrualAt;

        await IslandLocks.Economy.WaitAsync();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var island = await Ok(await owner.Http.GetAsync($"/api/islands/{id}", cts.Token));
            Assert.True(island.GetProperty("rum").GetProperty("total").GetInt64() > 0); // accrued for display
            var mine = await Ok(await owner.Http.GetAsync("/api/islands/mine", cts.Token));
            Assert.Equal(1, mine.GetProperty("islands").GetArrayLength());
            var raids = await Ok(await raider.Http.GetAsync("/api/raids/mine", cts.Token));
            Assert.Equal("abandoned", raids.GetProperty("attacks")[0].GetProperty("result").GetString());
        }
        finally { IslandLocks.Economy.Release(); }

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(storedAccrual, (await db.Islands.AsNoTracking().SingleAsync()).LastAccrualAt); // nothing written
            Assert.Equal(Raid.Started, (await db.Raids.AsNoTracking().SingleAsync()).Result);
        }

        // a write still accrues from the stored clock: collecting gets everything brewed
        var collected = await Ok(await owner.Http.PostAsync($"/api/islands/{id}/collect", null));
        Assert.True(collected.GetProperty("collected").GetInt64() > 0);
    }
}
