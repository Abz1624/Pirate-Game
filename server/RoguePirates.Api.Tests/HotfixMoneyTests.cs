using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>
/// 13 Oct hotfix: the money bugs found by the independent review of the hackathon server, each reproduced as an exploit
/// first: deposit amount/memo bypass (wager and market), double pay after RPC errors, refunded deposits re-attached,
/// market payments never refunded, the database-level payout guarantee, RUM caps, the mainnet guard.
/// </summary>
public sealed class HotfixMoneyTests
{
    const long Stake = 10_000_000;
    const string AdminKey = "test-admin-key-hotfix";

    sealed record Wallet(Account Account, Guid Id, HttpClient Http)
    {
        public string Key => Account.PublicKey.Key;
    }

    sealed class World : IDisposable
    {
        public required ApiHost Host { get; init; }
        public required FakeSolanaRpc Rpc { get; init; }
        public required Account TreasuryAccount { get; init; }
        public string Treasury => TreasuryAccount.PublicKey.Key;
        public void Dispose() => Host.Dispose();

        public string Transfer(Wallet from, long lamports, string? memo)
        {
            var sig = HackathonTests.RandomSignature();
            Rpc.Transactions[sig] = new ChainTransaction(sig, 77, false, new[] { from.Key }, new[] { new SolTransfer(from.Key, Treasury, lamports) },
                memo == null ? Array.Empty<string>() : new[] { memo }, null);
            return sig;
        }

        public async Task<Wallet> SignInAsync()
        {
            var account = new Account();
            var client = Host.Anon();
            var nonce = await (await client.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = account.PublicKey.Key })).ReadJson();
            var message = nonce.GetProperty("message").GetString()!;
            var sig = Encoders.Base58.EncodeData(account.Sign(Encoding.UTF8.GetBytes(message)));
            var res = await client.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = account.PublicKey.Key, signature = sig, message });
            Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
            var body = await res.ReadJson();
            return new Wallet(account, body.GetProperty("playerId").GetGuid(), Host.As(body.GetProperty("token").GetString()!));
        }

        public List<Payout> Payouts()
        {
            using var scope = Host.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<GameDb>().Payouts.AsNoTracking().OrderBy(p => p.Id).ToList();
        }

        public async Task<T> WithAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = Host.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        public SolanaService Sol => Host.Services.GetRequiredService<SolanaService>();
    }

    static World NewWorld(Dictionary<string, string?>? extra = null)
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        var settings = new Dictionary<string, string?>
        {
            ["ADMIN_KEY"] = AdminKey,
            ["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes),
            ["PUBLIC_API_URL"] = "https://api.test",
        };
        if (extra != null) foreach (var (k, v) in extra) settings[k] = v;
        var host = new ApiHost(settings, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });
        var w = new World { Host = host, Rpc = rpc, TreasuryAccount = treasury };
        w.Sol.PollInterval = TimeSpan.FromMilliseconds(5);
        w.Sol.ConfirmTimeout = TimeSpan.FromMilliseconds(60);
        return w;
    }

    static async Task<string> ErrorOf(HttpResponseMessage res) => (await res.ReadJson()).GetProperty("error").GetString()!;

    static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.ReadJson();
    }

    /// <summary>Seeds a recorded deposit as the old generic endpoint let a client create it (any purpose, refId, amount).</summary>
    static async Task SeedDepositAsync(World w, Wallet p, string sig, string purpose, string refId, long lamports, string? memo)
    {
        await w.WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<GameDb>();
            db.SolanaDeposits.Add(new SolanaDeposit
            {
                Signature = sig, PlayerId = p.Id, Wallet = p.Key, Purpose = purpose, RefId = refId, Lamports = lamports, Memo = memo,
                Status = SolanaDeposit.Verified, CreatedAt = w.Host.Clock.UtcNow,
            });
            return await db.SaveChangesAsync();
        });
    }

    static async Task<Guid> WagerIdAsync(World w, Guid lobbyId) =>
        await w.WithAsync(async sp => (await WagerService.ForLobbyAsync(sp.GetRequiredService<GameDb>(), lobbyId))!.Id);

    // ------------------------------------------------------------ 1. deposit amount/memo bypass

    [Fact]
    public async Task Exploit_OneLamportNoMemoDeposit_CannotPayAWagerSeat()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var lobby = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var lobbyId = lobby.GetProperty("lobbyId").GetGuid();
        var wagerId = await WagerIdAsync(w, lobbyId);

        // the exploit: register a 1-lamport, no-memo transfer as this wager's deposit through the generic endpoint
        var tiny = w.Transfer(host, 1, null);
        var reg = await host.Http.PostAsJsonAsync("/api/deposits/verify", new { signature = tiny, purpose = "wager", refId = wagerId.ToString(), minLamports = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, reg.StatusCode);
        Assert.Equal("purpose_reserved", await ErrorOf(reg));
        var attach = await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = tiny });
        Assert.False(attach.IsSuccessStatusCode);

        // even a deposit row already stored that way (before the hotfix) is re-checked at attach time and refunded
        var old = w.Transfer(host, 1, null);
        await SeedDepositAsync(w, host, old, "wager", wagerId.ToString(), 1, null);
        var res = await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = old });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("deposit_refunded", await ErrorOf(res));
        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{lobbyId}"));
        Assert.False(view.GetProperty("host").GetProperty("deposited").GetBoolean());
        var refund = Assert.Single(w.Payouts());
        Assert.Equal(1, refund.Lamports); // only what was sent comes back
    }

    [Fact]
    public async Task Exploit_StakeRaisedAfterTheDepositWasVerified_ShortDepositIsNotAttached()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var lobby = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var lobbyId = lobby.GetProperty("lobbyId").GetGuid();
        var wagerId = await WagerIdAsync(w, lobbyId);
        // a deposit for the old stake, verified (recorded) but not attached yet
        var sig = w.Transfer(host, Stake, "wager:" + lobbyId);
        await SeedDepositAsync(w, host, sig, WagerService.DepositPurpose, wagerId.ToString(), Stake, "wager:" + lobbyId);
        // the stake is raised meanwhile
        await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}", new { stakeLamports = 5 * Stake }));
        var res = await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = sig });
        Assert.Equal("deposit_refunded", await ErrorOf(res));
        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{lobbyId}"));
        Assert.False(view.GetProperty("host").GetProperty("deposited").GetBoolean());
        Assert.Equal(Stake, Assert.Single(w.Payouts()).Lamports);
    }

    static async Task<(Wallet seller, Guid listingId, long price)> ListingAsync(World w, long price = 50_000_000)
    {
        var seller = await w.SignInAsync();
        var island = await Ok(await seller.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Tortuga" }));
        var islandId = island.GetProperty("islandId").GetGuid();
        var mint = island.GetProperty("nft").GetProperty("mint").GetString()!;
        var escrow = HackathonTests.RandomSignature();
        w.Rpc.Transactions[escrow] = new ChainTransaction(escrow, 100, false, new[] { seller.Key }, Array.Empty<SolTransfer>(), Array.Empty<string>(), null);
        w.Rpc.Holdings.Add(new TokenHolding(new Account().PublicKey.Key, mint, w.Treasury, 1));
        var listing = await Ok(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = price, signature = escrow }));
        return (seller, listing.GetProperty("listingId").GetGuid(), price);
    }

    [Fact]
    public async Task Exploit_OneLamportMarketBuy_GetsNoNft_AndTheSellerIsNotPaidFromTheTreasury()
    {
        using var w = NewWorld();
        var (_, listingId, _) = await ListingAsync(w);
        var buyer = await w.SignInAsync();

        var tiny = w.Transfer(buyer, 1, null);
        var reg = await buyer.Http.PostAsJsonAsync("/api/deposits/verify", new { signature = tiny, purpose = "market_buy", refId = listingId.ToString(), minLamports = 1 });
        Assert.Equal("purpose_reserved", await ErrorOf(reg));

        // a deposit row stored that way before the hotfix
        await SeedDepositAsync(w, buyer, tiny, MarketService.DepositPurpose, listingId.ToString(), 1, null);
        var res = await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = tiny });
        Assert.False(res.IsSuccessStatusCode);
        var listing = await Ok(await buyer.Http.GetAsync($"/api/market/{listingId}"));
        Assert.Equal("active", listing.GetProperty("status").GetString());
        Assert.DoesNotContain(w.Payouts(), p => p.Reason == MarketService.SaleReason);
        Assert.All(w.Payouts(), p => Assert.Equal(1, p.Lamports)); // at most the 1 lamport back
    }

    // ------------------------------------------------------------ 4. market refunds

    [Fact]
    public async Task Market_PaymentForACancelledListing_IsRefunded_AndOverpaymentIsRefunded()
    {
        using var w = NewWorld();
        var (seller, listingId, price) = await ListingAsync(w);
        var buyer = await w.SignInAsync();

        // overpay: the purchase goes through, the extra comes back
        var pay = w.Transfer(buyer, price + 7_000_000, $"buy:{listingId}");
        var sold = await Ok(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = pay }));
        Assert.Equal("sold", sold.GetProperty("status").GetString());
        Assert.Single(w.Payouts(), p => p.Reason == MarketService.SaleReason && p.Lamports == price);
        Assert.Single(w.Payouts(), p => p.Reason == MarketService.RefundReason && p.Lamports == 7_000_000 && p.Wallet == buyer.Key);
        await Ok(await buyer.Http.PostAsync($"/api/market/{listingId}/complete", null));
        Assert.Single(w.Payouts(), p => p.Reason == MarketService.RefundReason);

        // a second listing, cancelled while a buyer was paying: the payment is recorded and refunded in full
        var (seller2, listing2, price2) = await ListingAsync(w);
        await Ok(await seller2.Http.PostAsync($"/api/market/{listing2}/cancel", null));
        var late = w.Transfer(buyer, price2, $"buy:{listing2}");
        var res = await buyer.Http.PostAsJsonAsync($"/api/market/{listing2}/buy", new { signature = late });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.ReadJson();
        Assert.Equal("listing_not_active", body.GetProperty("error").GetString());
        Assert.Contains("refunded", body.GetProperty("message").GetString());
        Assert.Single(w.Payouts(), p => p.Reason == MarketService.RefundReason && p.Lamports == price2);
        // the same signature again refunds nothing more
        await buyer.Http.PostAsJsonAsync($"/api/market/{listing2}/buy", new { signature = late });
        Assert.Equal(2, w.Payouts().Count(p => p.Reason == MarketService.RefundReason));
        _ = seller;
    }

    // ------------------------------------------------------------ 2. double pay after RPC errors

    [Fact]
    public async Task Exploit_SendResponseLost_PayoutIsUnconfirmed_NeverResent_ThenReconciledToSent()
    {
        using var w = NewWorld();
        var to = new Account().PublicKey.Key;
        w.Rpc.ThrowAfterSend = new HttpRequestException("connection reset"); // the node accepted it, the answer was lost
        var p1 = await w.WithAsync(sp => PayoutOnce.PayAsync(sp.GetRequiredService<GameDb>(), sp.GetRequiredService<LedgerService>(),
            w.Host.Clock, NullLogger.Instance, null, to, Stake, "test_win", "obligation-1"));
        Assert.Equal(Payout.Unconfirmed, p1!.Status);
        Assert.NotNull(p1.Signature);
        Assert.NotNull(p1.LastValidBlockHeight);
        w.Rpc.ThrowAfterSend = null;

        // a retry (even minutes later) does not send again
        w.Host.Clock.Advance(TimeSpan.FromMinutes(5));
        var p2 = await w.WithAsync(sp => PayoutOnce.PayAsync(sp.GetRequiredService<GameDb>(), sp.GetRequiredService<LedgerService>(),
            w.Host.Clock, NullLogger.Instance, null, to, Stake, "test_win", "obligation-1"));
        Assert.Equal(p1.Id, p2!.Id);
        Assert.Single(w.Rpc.Sent);

        // the reconciler finds it on chain: sent
        Assert.Equal(1, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync()));
        Assert.Equal(Payout.Sent, Assert.Single(w.Payouts()).Status);
        Assert.Single(w.Rpc.Sent);
    }

    [Fact]
    public async Task Exploit_StatusPollingThrowsAfterSend_IsUnconfirmed_NotFailed_NoSecondSend()
    {
        using var w = NewWorld();
        var to = new Account().PublicKey.Key;
        w.Rpc.ThrowOnStatus = new TaskCanceledException("timeout");
        var p = await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().PayAsync(null, to, Stake, "test_win", "o-2"));
        Assert.Equal(Payout.Unconfirmed, p.Status);
        w.Rpc.ThrowOnStatus = new JsonException("bad json");
        w.Host.Clock.Advance(TimeSpan.FromMinutes(5));
        await w.WithAsync(sp => PayoutOnce.PayAsync(sp.GetRequiredService<GameDb>(), sp.GetRequiredService<LedgerService>(),
            w.Host.Clock, NullLogger.Instance, null, to, Stake, "test_win", "o-2"));
        Assert.Single(w.Rpc.Sent);
        // the reconciler cannot read the node: it changes nothing
        Assert.Equal(0, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync()));
        Assert.Equal(Payout.Unconfirmed, Assert.Single(w.Payouts()).Status);
    }

    [Fact]
    public async Task UnconfirmedPayout_IsRetriedOnlyAfterTheBlockhashExpiredWithoutLanding()
    {
        using var w = NewWorld();
        var to = new Account().PublicKey.Key;
        w.Rpc.LandSends = false; // dropped by the network
        w.Rpc.ThrowAfterSend = new HttpRequestException("lost");
        var p = await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().PayAsync(null, to, Stake, "test_win", "o-3"));
        Assert.Equal(Payout.Unconfirmed, p.Status);
        w.Rpc.ThrowAfterSend = null;
        w.Rpc.LandSends = true;
        w.Host.Clock.Advance(TimeSpan.FromMinutes(5));

        // the blockhash is still valid: it may land, so nothing changes
        Assert.Equal(0, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync()));
        // past its last valid block height and still unknown: proven not sent, failed, and a retry pays once
        w.Rpc.BlockHeight = w.Rpc.LastValidBlockHeight + 1;
        Assert.Equal(1, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync()));
        Assert.Equal(Payout.Failed, Assert.Single(w.Payouts()).Status);
        var retry = await w.WithAsync(sp => PayoutOnce.PayAsync(sp.GetRequiredService<GameDb>(), sp.GetRequiredService<LedgerService>(),
            w.Host.Clock, NullLogger.Instance, null, to, Stake, "test_win", "o-3"));
        Assert.Equal(Payout.Sent, retry!.Status);
        Assert.Equal(2, w.Rpc.Sent.Count);
        Assert.Single(w.Payouts(), x => x.Status == Payout.Sent);
    }

    [Fact]
    public async Task StalePendingPayoutWithoutSignature_IsMarkedFailed_ByTheReconciler()
    {
        using var w = NewWorld();
        await w.WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<GameDb>();
            db.Payouts.Add(new Payout { Wallet = new Account().PublicKey.Key, Lamports = 5, Reason = "test", RefId = "x", Status = Payout.Pending, CreatedAt = w.Host.Clock.UtcNow });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(0, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync())); // too recent: may be in flight
        w.Host.Clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(1, await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().ReconcileAsync()));
        Assert.Equal(Payout.Failed, Assert.Single(w.Payouts()).Status);
    }

    // ------------------------------------------------------------ 7. database-level idempotency

    [Fact]
    public async Task UniqueIndex_RefusesASecondLivePayoutForOneObligation_AndLedgerReturnsTheLiveOne()
    {
        using var w = NewWorld();
        var to = new Account().PublicKey.Key;
        await w.WithAsync(async sp =>
        {
            var db = sp.GetRequiredService<GameDb>();
            db.Payouts.Add(new Payout { Wallet = to, Lamports = 5, Reason = "r", RefId = "same", Status = Payout.Failed, CreatedAt = w.Host.Clock.UtcNow });
            db.Payouts.Add(new Payout { Wallet = to, Lamports = 5, Reason = "r", RefId = "same", Status = Payout.Unconfirmed, CreatedAt = w.Host.Clock.UtcNow });
            await db.SaveChangesAsync(); // a failed and a live row: allowed
            db.Payouts.Add(new Payout { Wallet = to, Lamports = 5, Reason = "r", RefId = "same", Status = Payout.Sent, CreatedAt = w.Host.Clock.UtcNow });
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
            return 0;
        });
        // the ledger called directly (bypassing PayoutOnce's check) still does not pay
        var p = await w.WithAsync(sp => sp.GetRequiredService<LedgerService>().PayAsync(null, to, 5, "r", "same"));
        Assert.Equal(Payout.Unconfirmed, p.Status);
        Assert.Empty(w.Rpc.Sent);
        // concurrent pays of one obligation send once
        var tasks = Enumerable.Range(0, 6).Select(_ => w.WithAsync(sp => PayoutOnce.PayAsync(sp.GetRequiredService<GameDb>(),
            sp.GetRequiredService<LedgerService>(), w.Host.Clock, NullLogger.Instance, null, to, 7, "r", "concurrent")));
        await Task.WhenAll(tasks);
        Assert.Single(w.Rpc.Sent);
    }

    // ------------------------------------------------------------ 6. refunded deposit re-attached

    [Fact]
    public async Task Exploit_GuestLeavesAndRejoins_TheRefundingDepositIsNeverAttachedAgain()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var guest = await w.SignInAsync();
        var created = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var id = created.GetProperty("lobbyId").GetGuid();
        var code = created.GetProperty("code").GetString();
        await Ok(await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code }));
        var sig = w.Transfer(guest, Stake, "wager:" + id);
        await Ok(await guest.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig }));

        // the guest leaves; the refund's result is unknown (lost response), so the deposit is not "refunded" yet
        w.Rpc.ThrowAfterSend = new HttpRequestException("lost");
        await Ok(await guest.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "left" }));
        // "left" starts a settlement in the background (WagerService.Kick) that may be mid-send; wait for it, then settle
        // again ourselves: the second pass finds the live payout and sends nothing
        await w.Host.Services.GetRequiredService<BackgroundSettlements>().WhenIdleAsync();
        await w.WithAsync(async sp => { await sp.GetRequiredService<WagerService>().SettleLobbyAsync(id); return 0; });
        await w.Host.Services.GetRequiredService<BackgroundSettlements>().WhenIdleAsync();
        w.Rpc.ThrowAfterSend = null;
        Assert.Equal(Payout.Unconfirmed, Assert.Single(w.Payouts()).Status);

        // rejoin and re-send the same signature: refused, the seat is not funded by a deposit being refunded
        await Ok(await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code }));
        var res = await guest.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig });
        Assert.Equal("deposit_refunded", await ErrorOf(res));
        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{id}"));
        Assert.False(view.GetProperty("guest").GetProperty("deposited").GetBoolean());
        Assert.Single(w.Payouts());
    }

    // ------------------------------------------------------------ 5 and 9. weekly rewards

    [Fact]
    public async Task WeeklyRewards_TheCurrentWeekIsNeverPaid_AndUncheckedRunsEarnNoSolByDefault()
    {
        using var w = NewWorld();
        var a = await w.SignInAsync();
        await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 300, runSeconds = 600, level = 10 })); // no log: unchecked
        var admin = w.Host.Anon();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        var week = WeekMath.WeekStart(w.Host.Clock.UtcNow).ToString("yyyy-MM-dd");
        var res = await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("week_not_ended", await ErrorOf(res));
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week, dryRun = true })); // a dry run works

        w.Host.Clock.Advance(TimeSpan.FromDays(7));
        var paid = await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week }));
        Assert.True(paid.GetProperty("checkedOnly").GetBoolean());
        Assert.Equal(0, paid.GetProperty("winners").GetArrayLength());
        Assert.Empty(w.Payouts());
    }

    [Fact]
    public async Task Rum_FromVoyages_IsSoftCappedPerRunAndPerDay()
    {
        Assert.Equal(25, IslandRules.SoftCap(30, 40, 20));
        Assert.Equal(39, IslandRules.SoftCap(600, 40, 20));
        // small runs keep their raw RUM (the curve never adds)
        Assert.Equal(10, IslandRules.CappedRumForRun(105, Array.Empty<int>(), 40, 20, 150, 75));
        using var w = NewWorld();
        var a = await w.SignInAsync();
        long total = 0;
        for (var i = 0; i < 12; i++)
        {
            var r = await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 6000, runSeconds = 3600, level = 50 }));
            var earned = r.GetProperty("rumEarned").GetInt64();
            Assert.InRange(earned, 0, 39); // a claimed 600 raw RUM per run
            total += earned;
        }
        Assert.InRange(total, 100, 149); // the day's cap
        // the next UTC day starts a new allowance
        w.Host.Clock.Advance(TimeSpan.FromDays(1));
        var next = await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 6000, runSeconds = 3600, level = 50 }));
        Assert.True(next.GetProperty("rumEarned").GetInt64() > 20);
    }

    // ------------------------------------------------------------ 10. mainnet guard and raw RPC errors

    [Fact]
    public void Mainnet_KeepsTheTreasuryOff_UnlessAllowMainnet()
    {
        var key = Encoders.Base58.EncodeData(new Account().PrivateKey.KeyBytes);
        var rpc = new FakeSolanaRpc();
        var off = new SolanaService(rpc, new ApiSettings { TreasurySecretKey = key, SolanaCluster = "mainnet-beta" }, NullLogger<SolanaService>.Instance);
        Assert.False(off.TreasuryOn);
        var offUrl = new SolanaService(rpc, new ApiSettings { TreasurySecretKey = key, SolanaRpcUrl = "https://api.mainnet-beta.solana.com" }, NullLogger<SolanaService>.Instance);
        Assert.False(offUrl.TreasuryOn);
        var on = new SolanaService(rpc, new ApiSettings { TreasurySecretKey = key, SolanaCluster = "mainnet-beta", AllowMainnet = true }, NullLogger<SolanaService>.Instance);
        Assert.True(on.TreasuryOn);
        Assert.True(new SolanaService(rpc, new ApiSettings { TreasurySecretKey = key }, NullLogger<SolanaService>.Instance).TreasuryOn);
    }

    sealed class ErrorRpc : ISolanaRpc
    {
        readonly FakeSolanaRpc _inner = new();
        public Task<long> GetBalanceAsync(string address, CancellationToken ct = default) =>
            throw new SolanaRpcException(500, "RPC getBalance: HTTP 500 <html>internal node detail secret-ish</html>");
        public Task<ChainTransaction?> GetTransactionAsync(string signature, CancellationToken ct = default) => _inner.GetTransactionAsync(signature, ct);
        public Task<string> GetLatestBlockhashAsync(CancellationToken ct = default) => _inner.GetLatestBlockhashAsync(ct);
        public Task<string> SendTransactionAsync(byte[] transaction, CancellationToken ct = default) => _inner.SendTransactionAsync(transaction, ct);
        public Task<string?> GetSignatureStatusAsync(string signature, CancellationToken ct = default) => _inner.GetSignatureStatusAsync(signature, ct);
        public Task<long> GetMinimumBalanceForRentExemptionAsync(int dataLength, CancellationToken ct = default) => _inner.GetMinimumBalanceForRentExemptionAsync(dataLength, ct);
        public Task<IReadOnlyList<TokenHolding>> GetTokenAccountsByOwnerAsync(string owner, string mint, CancellationToken ct = default) => _inner.GetTokenAccountsByOwnerAsync(owner, mint, ct);
        public Task<bool> AccountExistsAsync(string address, CancellationToken ct = default) => _inner.AccountExistsAsync(address, ct);
        public Task<string> RequestAirdropAsync(string address, long lamports, CancellationToken ct = default) => _inner.RequestAirdropAsync(address, lamports, ct);
    }

    [Fact]
    public async Task RawRpcErrorBodies_AreNotReturnedToClients()
    {
        using var host = new ApiHost(new Dictionary<string, string?>(), s => { s.RemoveAll<ISolanaRpc>(); s.AddSingleton<ISolanaRpc>(new ErrorRpc()); });
        var res = await host.Anon().GetAsync($"/api/wallet/balance?wallet={new Account().PublicKey.Key}");
        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("solana_rpc_error", text);
        Assert.DoesNotContain("internal node detail", text);
        Assert.DoesNotContain("<html>", text);
    }
}
