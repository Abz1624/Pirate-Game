using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>Hackathon: the owner's demo reset (POST /api/admin/demo/reset) on the fake chain.</summary>
public sealed class DemoResetTests
{
    const string AdminKey = "test-admin-key-reset";
    const long Price = 10_000_000;

    sealed record Wallet(string Key, Guid Id, HttpClient Http);

    static async Task<Wallet> SignInAsync(ApiHost host)
    {
        var account = new Account();
        var client = host.Anon();
        var nonce = await (await client.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = account.PublicKey.Key })).ReadJson();
        var message = nonce.GetProperty("message").GetString()!;
        var sig = Encoders.Base58.EncodeData(account.Sign(Encoding.UTF8.GetBytes(message)));
        var body = await Ok(await client.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = account.PublicKey.Key, signature = sig, message }));
        return new Wallet(account.PublicKey.Key, body.GetProperty("playerId").GetGuid(), host.As(body.GetProperty("token").GetString()!));
    }

    static async Task<string> ErrorOf(HttpResponseMessage res) => (await res.ReadJson()).GetProperty("error").GetString()!;

    static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.ReadJson();
    }

    [Fact]
    public async Task DemoReset_DryRunCounts_ThenRemovesOnlyTheListedWalletsData()
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        using var host = new ApiHost(new Dictionary<string, string?>
        {
            ["ADMIN_KEY"] = AdminKey,
            ["WEEKLY_REWARD_CHECKED_ONLY"] = "false", // these runs have no logs
            ["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes),
        }, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });

        var a = await SignInAsync(host);
        var b = await SignInAsync(host);
        var c = await SignInAsync(host); // a real player: keeps everything
        async Task Run(HttpClient http, int kills) =>
            await Ok(await http.PostAsJsonAsync("/api/runs", new { kills, runSeconds = 600, level = 10 }));
        await Run(a.Http, 4000);
        await Run(a.Http, 3000);
        await Run(b.Http, 1500);
        await Run(c.Http, 100);
        var seed = (await Ok(await host.Anon().GetAsync("/api/race/daily"))).GetProperty("race").GetProperty("seed").GetInt32();
        await Ok(await a.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 150, kills = 100 }));
        await Ok(await c.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 160, kills = 100 }));

        var now = host.Clock.UtcNow;
        Guid aIsland = Guid.NewGuid(), aListedIsland = Guid.NewGuid(), cIsland = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Island NewIsland(Guid id, Guid owner, int slot) => new()
            {
                Id = id, OwnerPlayerId = owner, Name = "Isle " + slot, Slot = slot, NftMint = "Mint" + slot, NftStatus = "minted",
                LastAccrualAt = now, CreatedAt = now,
            };
            db.Islands.AddRange(NewIsland(aIsland, a.Id, 1), NewIsland(aListedIsland, a.Id, 2), NewIsland(cIsland, c.Id, 3));
            db.IslandBuildings.AddRange(
                new IslandBuilding { IslandId = aIsland, Kind = "walls", Level = 1, UpdatedAt = now },
                new IslandBuilding { IslandId = aListedIsland, Kind = "walls", Level = 1, UpdatedAt = now },
                new IslandBuilding { IslandId = cIsland, Kind = "walls", Level = 2, UpdatedAt = now });
            db.Raids.AddRange(
                new Raid { Id = Guid.NewGuid(), AttackerPlayerId = a.Id, IslandId = cIsland, DefenderPlayerId = c.Id, Result = Raid.Won, CreatedAt = now },
                new Raid { Id = Guid.NewGuid(), AttackerPlayerId = c.Id, IslandId = aIsland, DefenderPlayerId = a.Id, Result = Raid.Lost, CreatedAt = now },
                new Raid { Id = Guid.NewGuid(), AttackerPlayerId = c.Id, IslandId = Guid.NewGuid(), DefenderPlayerId = Guid.NewGuid(), Result = Raid.Won, CreatedAt = now });
            db.MarketListings.AddRange(
                new MarketListing { Id = Guid.NewGuid(), IslandId = aIsland, NftMint = "Mint1", SellerPlayerId = b.Id, BuyerPlayerId = a.Id, PriceLamports = Price, Status = MarketListing.Sold, CreatedAt = now },
                new MarketListing { Id = Guid.NewGuid(), IslandId = aIsland, NftMint = "Mint1", SellerPlayerId = a.Id, PriceLamports = Price, Status = MarketListing.Cancelled, CreatedAt = now },
                new MarketListing { Id = Guid.NewGuid(), IslandId = aListedIsland, NftMint = "Mint2", SellerPlayerId = a.Id, PriceLamports = Price, Status = MarketListing.Active, CreatedAt = now },
                new MarketListing { Id = Guid.NewGuid(), IslandId = cIsland, NftMint = "Mint3", SellerPlayerId = c.Id, PriceLamports = Price, Status = MarketListing.Cancelled, CreatedAt = now });
            await db.SaveChangesAsync();
        }

        List<Payout> Payouts()
        {
            using var scope = host.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<GameDb>().Payouts.ToList();
        }

        // 13 Oct hotfix: the current week can no longer be paid (the smoke test used to); it is paid once it has ended
        var week = WeekMath.WeekStart(now);
        var admin = host.Anon();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        Assert.Equal("week_not_ended", await ErrorOf(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week.ToString("yyyy-MM-dd") })));
        Assert.Empty(Payouts());
        host.Clock.Advance(TimeSpan.FromDays(7));
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week.ToString("yyyy-MM-dd") }));
        Assert.Equal(3, Payouts().Count);

        var wallets = new[] { a.Key, b.Key, "UnknownWa11et1111111111111111111111111111" };
        var weekText = week.ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Anon().PostAsJsonAsync("/api/admin/demo/reset", new { wallets })).StatusCode);
        Assert.Equal("nothing_to_reset", await ErrorOf(await admin.PostAsJsonAsync("/api/admin/demo/reset", new { wallets = Array.Empty<string>() })));

        void AssertCounts(JsonElement res, bool withRewards = true)
        {
            var d = res.GetProperty("deleted");
            Assert.Equal(3, d.GetProperty("runs").GetInt32());
            Assert.Equal(2, d.GetProperty("weeklyBests").GetInt32());
            Assert.Equal(1, d.GetProperty("dailyRaceEntries").GetInt32());
            Assert.Equal(2, d.GetProperty("raids").GetInt32());
            Assert.Equal(1, d.GetProperty("islands").GetInt32());
            Assert.Equal(1, d.GetProperty("islandBuildings").GetInt32());
            Assert.Equal(2, d.GetProperty("marketListings").GetInt32());
            if (withRewards)
            {
                Assert.Equal(3, d.GetProperty("weeklyRewards").GetInt32());
                Assert.Equal(3, res.GetProperty("weeklyRewards").GetProperty("payoutsKept").GetInt32());
                Assert.Equal(3, res.GetProperty("weeklyRewards").GetProperty("paidPayouts").GetInt32());
            }
            else Assert.Equal(JsonValueKind.Null, res.GetProperty("weeklyRewards").ValueKind);
            Assert.Equal(aIsland, res.GetProperty("releasedIslands")[0].GetProperty("id").GetGuid());
            Assert.Equal(aListedIsland, res.GetProperty("skippedIslands")[0].GetProperty("id").GetGuid());
            Assert.Equal("listing_open", res.GetProperty("skippedIslands")[0].GetProperty("reason").GetString());
            Assert.Equal(new[] { true, true, false }, res.GetProperty("wallets").EnumerateArray().Select(x => x.GetProperty("found").GetBoolean()));
        }

        // dryRun defaults to true: counts only
        var dry = await Ok(await admin.PostAsJsonAsync("/api/admin/demo/reset", new { wallets, clearWeeklyRewards = true, weekStart = weekText }));
        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        AssertCounts(dry);
        Assert.Equal(3, (await Ok(await host.Anon().GetAsync("/api/leaderboard/weekly/previous"))).GetProperty("entries").GetArrayLength());

        // 13 Oct hotfix: a paid week's rewards are never deleted (deleting them let the week be recorded and paid again)
        var refused = await admin.PostAsJsonAsync("/api/admin/demo/reset", new { wallets, clearWeeklyRewards = true, weekStart = weekText, dryRun = false });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("rewards_paid", await ErrorOf(refused));

        var done = await Ok(await admin.PostAsJsonAsync("/api/admin/demo/reset", new { wallets, dryRun = false }));
        Assert.False(done.GetProperty("dryRun").GetBoolean());
        AssertCounts(done, withRewards: false);

        var board = (await Ok(await host.Anon().GetAsync("/api/leaderboard/weekly/previous"))).GetProperty("entries").EnumerateArray().ToList();
        Assert.Single(board);
        Assert.Equal(100, board[0].GetProperty("kills").GetInt32());
        Assert.Equal(3, Payouts().Count); // payouts stay as history

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(new[] { c.Id }, db.Runs.Select(r => r.PlayerId).ToList());
            Assert.Equal(new[] { c.Id }, db.WeeklyBests.Select(r => r.PlayerId).ToList());
            Assert.Equal(3, db.WeeklyRewards.Count()); // kept: they were paid
            Assert.Equal(new[] { c.Id }, db.DailyRaceEntries.Select(e => e.PlayerId).ToList());
            Assert.Equal(new[] { aListedIsland, cIsland }.OrderBy(x => x), db.Islands.Select(i => i.Id).ToList().OrderBy(x => x));
            Assert.Equal(2, db.IslandBuildings.Count());
            Assert.Single(db.Raids.ToList()); // c's raid on an unrelated island
            Assert.Equal(2, db.MarketListings.Count()); // a's open listing and c's cancelled one
            Assert.Equal(3, db.Players.Count()); // profiles are kept
        }

        // paying the week again sends nothing new
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = weekText }));
        Assert.Equal(3, Payouts().Count);

        // a second real run finds nothing more (the skipped island stays skipped)
        var again = await Ok(await admin.PostAsJsonAsync("/api/admin/demo/reset", new { wallets, dryRun = false }));
        Assert.Equal(0, again.GetProperty("deleted").GetProperty("runs").GetInt32());
        Assert.Equal(JsonValueKind.Null, again.GetProperty("weeklyRewards").ValueKind);
        Assert.Equal(1, again.GetProperty("skippedIslands").GetArrayLength());
    }

    /// <summary>13 Oct hotfix: even if a paid week's reward rows were deleted (by hand, or by the old demo reset), paying
    /// the week again pays no place twice: payouts are keyed by (week, map, rank).</summary>
    [Fact]
    public async Task WeeklyRewards_RowsDeletedAfterPaying_AreNeverPaidTwice()
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        using var host = new ApiHost(new Dictionary<string, string?>
        {
            ["ADMIN_KEY"] = AdminKey, ["WEEKLY_REWARD_CHECKED_ONLY"] = "false",
            ["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes),
        }, s => { s.RemoveAll<ISolanaRpc>(); s.AddSingleton<ISolanaRpc>(rpc); });
        var a = await SignInAsync(host);
        await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 50, runSeconds = 600, level = 10 }));
        var week = WeekMath.WeekStart(host.Clock.UtcNow).ToString("yyyy-MM-dd");
        host.Clock.Advance(TimeSpan.FromDays(7));
        var admin = host.Anon();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week }));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Single(db.Payouts.ToList());
            db.WeeklyRewards.RemoveRange(db.WeeklyRewards);
            await db.SaveChangesAsync();
        }
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week }));
        using (var scope = host.Services.CreateScope())
            Assert.Single(scope.ServiceProvider.GetRequiredService<GameDb>().Payouts.ToList());
        Assert.Single(rpc.Sent);
    }
}
