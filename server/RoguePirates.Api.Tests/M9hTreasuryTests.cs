using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Data.Migrations;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9h (C9h.2 and the review's double-mint finding): one free island claim per wallet ever, a daily mint budget, Island
/// NFT mints recorded before the send and checked on chain before any retry (never minted twice), the treasury balance
/// cached with a low-float alarm, and the owner's treasury and payouts-and-mints report.
/// </summary>
public sealed class M9hTreasuryTests
{
    const string AdminKey = "test-admin-key-m9h";

    sealed class World : IDisposable
    {
        public required ApiHost Host { get; init; }
        public required FakeSolanaRpc Rpc { get; init; }
        public required Account Treasury { get; init; }
        public void Dispose() => Host.Dispose();
        public HttpClient Admin()
        {
            var c = Host.Anon();
            c.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
            return c;
        }
        public async Task<T> WithDbAsync<T>(Func<GameDb, Task<T>> f)
        {
            using var scope = Host.Services.CreateScope();
            return await f(scope.ServiceProvider.GetRequiredService<GameDb>());
        }
    }

    static World NewWorld(Dictionary<string, string?>? extra = null)
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        var settings = new Dictionary<string, string?>
        {
            ["ADMIN_KEY"] = AdminKey, ["PUBLIC_API_URL"] = "https://api.test",
            ["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes),
        };
        if (extra != null) foreach (var (k, v) in extra) settings[k] = v;
        var host = new ApiHost(settings, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });
        var sol = host.Services.GetRequiredService<SolanaService>();
        sol.PollInterval = TimeSpan.FromMilliseconds(5);
        sol.ConfirmTimeout = TimeSpan.FromMilliseconds(60);
        return new World { Host = host, Rpc = rpc, Treasury = treasury };
    }

    // ------------------------------------------------------------ one claim per wallet ever

    [Fact]
    public async Task Claim_IsOncePerWalletEver_SellingTheIslandDoesNotFreeAnotherClaim()
    {
        using var w = NewWorld();
        var seller = await SignInAsync(w.Host);
        var buyer = await SignInAsync(w.Host);
        var (islandId, mint) = await MintedIslandAsync(seller);
        var escrow = EscrowNft(w.Rpc, seller, w.Treasury, mint);
        var listingId = (await Ok(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 50_000_000, signature = escrow })))
            .GetProperty("listingId").GetGuid();
        var pay = Deposit(w.Rpc, buyer.Key, w.Treasury.PublicKey.Key, 50_000_000, $"buy:{listingId}");
        Assert.Equal("sold", (await Ok(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = pay }))).GetProperty("status").GetString());

        // the seller has no island now, but the free claim is used
        Assert.Equal(0, (await Ok(await seller.Http.GetAsync("/api/islands/mine"))).GetProperty("islands").GetArrayLength());
        var sentBefore = w.Rpc.Sent.Count;
        await AssertError(await seller.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Again" }), HttpStatusCode.Conflict, "island_claim_used");
        Assert.Equal(sentBefore, w.Rpc.Sent.Count); // nothing minted
        // the buyer owns one already
        await AssertError(await buyer.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Mine" }), HttpStatusCode.Conflict, "already_has_island");
    }

    [Fact]
    public async Task Migration_MarksOwnersAndSellersAsHavingClaimed()
    {
        using var w = NewWorld();
        var owner = await SignInAsync(w.Host);
        var fresh = await SignInAsync(w.Host);
        await ClaimAsync(owner);
        await w.WithDbAsync(async db =>
        {
            await db.Players.ExecuteUpdateAsync(u => u.SetProperty(p => p.IslandClaimedAt, (DateTime?)null)); // as before M9h
            await db.Database.ExecuteSqlRawAsync(AddMintSafetySql.MarkClaimed);
            await db.Database.ExecuteSqlRawAsync(AddMintSafetySql.MintedTimes);
            var rows = await db.Players.AsNoTracking().Where(p => p.WalletAddress != null).ToListAsync();
            Assert.NotNull(rows.Single(p => p.WalletAddress == owner.Key).IslandClaimedAt);
            Assert.Null(rows.Single(p => p.WalletAddress == fresh.Key).IslandClaimedAt);
            var island = await db.Islands.AsNoTracking().SingleAsync();
            Assert.Equal(island.CreatedAt, island.MintedAt);
            return 0;
        });
    }

    // ------------------------------------------------------------ daily mint budget

    [Fact]
    public async Task MintBudget_RefusesClaimsPastTheDailyBudget_WithAClearError_AndResetsAtMidnight()
    {
        using var w = NewWorld(new() { ["MINT_DAILY_BUDGET"] = "2" });
        await ClaimAsync(await SignInAsync(w.Host), "One");
        await ClaimAsync(await SignInAsync(w.Host), "Two");
        var third = await SignInAsync(w.Host);
        var res = await third.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Three" });
        await AssertError(res, (HttpStatusCode)429, "mint_budget");
        Assert.Equal(2, w.Rpc.Sent.Count);
        // nothing was claimed: the wallet keeps its free claim
        Assert.Equal(0, (await Ok(await third.Http.GetAsync("/api/islands/mine"))).GetProperty("islands").GetArrayLength());
        var admin = await Ok(await w.Admin().GetAsync("/api/admin/treasury"));
        Assert.Equal(2, admin.GetProperty("mintsToday").GetInt32());
        Assert.Equal(2, admin.GetProperty("mintDailyBudget").GetInt32());

        w.Host.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("minted", (await ClaimAsync(third, "Three")).GetProperty("nft").GetProperty("status").GetString());
    }

    [Fact]
    public async Task MintBudget_AlsoBoundsRetries_ARejectedMintDoesNotCount()
    {
        using var w = NewWorld(new() { ["MINT_DAILY_BUDGET"] = "1" });
        var a = await SignInAsync(w.Host);
        w.Rpc.ThrowAfterSend = new SolanaRpcException(-32002, "Transaction simulation failed (test)");
        w.Rpc.LandSends = false;
        var island = await ClaimAsync(a, "Alpha");
        Assert.Equal("pending", island.GetProperty("nft").GetProperty("status").GetString());
        Assert.StartsWith("tx_rejected", island.GetProperty("nft").GetProperty("error").GetString());
        w.Rpc.ThrowAfterSend = null;
        w.Rpc.LandSends = true;
        // the rejected attempt cost nothing: another wallet can still claim today
        await ClaimAsync(await SignInAsync(w.Host), "Beta");
        await AssertError(await a.Http.PostAsync($"/api/islands/{island.GetProperty("islandId").GetGuid()}/mint", null), (HttpStatusCode)429, "mint_budget");
    }

    // ------------------------------------------------------------ double mint

    [Fact]
    public async Task Mint_SentWithAnUnknownResult_IsPending_AndTheRetryFindsItOnChain_NoSecondMint()
    {
        using var w = NewWorld();
        var a = await SignInAsync(w.Host);
        w.Rpc.ThrowAfterSend = new HttpRequestException("connection reset after the send (test)"); // the send landed, the answer was lost
        var island = await ClaimAsync(a);
        var id = island.GetProperty("islandId").GetGuid();
        var nft = island.GetProperty("nft");
        Assert.Equal("pending", nft.GetProperty("status").GetString());
        Assert.StartsWith("mint_in_flight", nft.GetProperty("error").GetString());
        var pendingMint = nft.GetProperty("pendingMint").GetString()!;
        Assert.True(SolanaService.IsValidAddress(pendingMint));
        Assert.Single(w.Rpc.Sent);

        w.Rpc.ThrowAfterSend = null;
        var retried = await Ok(await a.Http.PostAsync($"/api/islands/{id}/mint", null));
        Assert.Equal("minted", retried.GetProperty("nft").GetProperty("status").GetString());
        Assert.Equal(pendingMint, retried.GetProperty("nft").GetProperty("mint").GetString()); // the first mint, found on chain
        Assert.Single(w.Rpc.Sent); // never minted twice
    }

    [Fact]
    public async Task Mint_ThatNeverLands_IsRetriedOnlyAfterItsBlockhashExpired_WithANewMint()
    {
        using var w = NewWorld();
        var a = await SignInAsync(w.Host);
        w.Rpc.LandSends = false;
        w.Rpc.ThrowAfterSend = new HttpRequestException("timeout (test)");
        var island = await ClaimAsync(a);
        var id = island.GetProperty("islandId").GetGuid();
        var first = island.GetProperty("nft").GetProperty("pendingMint").GetString()!;
        w.Rpc.MissingAccounts[first] = true; // it did not land (yet)
        w.Rpc.ThrowAfterSend = null;
        w.Rpc.LandSends = true;

        // still inside its blockhash window: it may land, so nothing new is sent
        await AssertError(await a.Http.PostAsync($"/api/islands/{id}/mint", null), HttpStatusCode.Conflict, "mint_in_flight");
        Assert.Single(w.Rpc.Sent);

        // the chain passed its last valid block height without it: provably never landing, a new mint is safe
        w.Rpc.BlockHeight = w.Rpc.LastValidBlockHeight + 1;
        var retried = await Ok(await a.Http.PostAsync($"/api/islands/{id}/mint", null));
        Assert.Equal("minted", retried.GetProperty("nft").GetProperty("status").GetString());
        Assert.NotEqual(first, retried.GetProperty("nft").GetProperty("mint").GetString());
        Assert.Equal(2, w.Rpc.Sent.Count);
    }

    [Fact]
    public async Task StuckPendingMint_IsReconciledByTheSweep_WithoutSendingAgain()
    {
        using var w = NewWorld();
        var a = await SignInAsync(w.Host);
        w.Rpc.ThrowAfterSend = new HttpRequestException("lost answer (test)");
        var id = (await ClaimAsync(a)).GetProperty("islandId").GetGuid();
        w.Rpc.ThrowAfterSend = null;

        // too fresh for the sweep (the request that sent it may still be polling)
        using (var scope = w.Host.Services.CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IslandService>().ReconcileMintsAsync());
        w.Host.Clock.Advance(TimeSpan.FromMinutes(3));
        using (var scope = w.Host.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IslandService>().ReconcileMintsAsync());
        var island = await Ok(await a.Http.GetAsync($"/api/islands/{id}"));
        Assert.Equal("minted", island.GetProperty("nft").GetProperty("status").GetString());
        Assert.Single(w.Rpc.Sent);
    }

    // ------------------------------------------------------------ treasury float, alarm, cache

    [Fact]
    public async Task Treasury_IsCached30Seconds_AndReportsLowBelowTheFloat()
    {
        using var w = NewWorld(new() { ["TREASURY_MIN_LAMPORTS"] = "2000000000" });
        var address = w.Treasury.PublicKey.Key;
        w.Rpc.Balances[address] = 1_000_000_000;
        var t = await Ok(await w.Host.Anon().GetAsync("/api/treasury"));
        Assert.True(t.GetProperty("low").GetBoolean());
        Assert.Equal(2_000_000_000, t.GetProperty("floatLamports").GetInt64());
        Assert.Equal(1_000_000_000, t.GetProperty("lamports").GetInt64());
        Assert.NotNull(w.Host.Services.GetRequiredService<TreasuryMonitor>().LowSince);

        w.Rpc.Balances[address] = 3_000_000_000; // topped up
        Assert.Equal(1_000_000_000, (await Ok(await w.Host.Anon().GetAsync("/api/treasury"))).GetProperty("lamports").GetInt64()); // cached
        w.Host.Clock.Advance(TimeSpan.FromSeconds(31));
        var after = await Ok(await w.Host.Anon().GetAsync("/api/treasury"));
        Assert.Equal(3_000_000_000, after.GetProperty("lamports").GetInt64());
        Assert.False(after.GetProperty("low").GetBoolean());
        Assert.Null(w.Host.Services.GetRequiredService<TreasuryMonitor>().LowSince);

        // the owner's view reads fresh
        w.Rpc.Balances[address] = 500_000_000;
        var admin = await Ok(await w.Admin().GetAsync("/api/admin/treasury"));
        Assert.Equal(500_000_000, admin.GetProperty("lamports").GetInt64());
        Assert.True(admin.GetProperty("low").GetBoolean());
        Assert.Contains("top", admin.GetProperty("topUpRule").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Host.Anon().GetAsync("/api/admin/treasury")).StatusCode);
    }

    [Fact]
    public async Task Treasury_WithoutAKey_IsDisabled()
    {
        using var host = new ApiHost();
        var t = await Ok(await host.Anon().GetAsync("/api/treasury"));
        Assert.False(t.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, t.GetProperty("low").ValueKind);
    }

    // ------------------------------------------------------------ the owner's report

    [Fact]
    public async Task AdminReport_ListsPayoutsAndMintsSinceADate_WithTotals()
    {
        using var w = NewWorld();
        var a = await SignInAsync(w.Host);
        await ClaimAsync(a, "Tortuga");
        var pay = w.Admin();
        pay.DefaultRequestHeaders.Add("Idempotency-Key", "m9h-report-test");
        await Ok(await pay.PostAsJsonAsync("/api/admin/payouts", new { wallet = a.Key, lamports = 5_000_000, reason = "admin" }));

        var since = w.Host.Clock.UtcNow.Date.ToString("yyyy-MM-dd");
        var report = await Ok(await w.Admin().GetAsync($"/api/admin/report?since={since}"));
        var payout = Assert.Single(report.GetProperty("payouts").EnumerateArray());
        Assert.Equal(5_000_000, payout.GetProperty("lamports").GetInt64());
        Assert.Equal(5_000_000, report.GetProperty("payoutsSentLamports").GetInt64());
        var mint = Assert.Single(report.GetProperty("mints").EnumerateArray());
        Assert.Equal("minted", mint.GetProperty("status").GetString());
        Assert.Equal(a.Key, mint.GetProperty("ownerWallet").GetString());
        Assert.Equal(1, report.GetProperty("mintsMinted").GetInt32());

        // the default start is 12 Oct 2026 (the test clock is 1 Oct): nothing yet
        var later = await Ok(await w.Admin().GetAsync("/api/admin/report"));
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), later.GetProperty("since").GetDateTime().ToUniversalTime());
        Assert.Equal(0, later.GetProperty("payouts").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Host.Anon().GetAsync("/api/admin/report")).StatusCode);
    }
}
