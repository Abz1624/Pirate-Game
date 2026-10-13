using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9h (C9h.1): the live-API switches (the owner kept all of them on, H2 on 13 Oct; each one tested on and off) and
/// the wager deposits refused after a verified transfer (too small, another memo, stakes switched off), which are now
/// recorded and refunded once instead of staying in the treasury.
/// </summary>
public sealed class M9hSwitchTests
{
    const long Stake = 10_000_000;

    static List<Payout> Payouts(ApiHost host)
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<GameDb>().Payouts.AsNoTracking().OrderBy(p => p.Id).ToList();
    }

    static async Task<(string error, string message)> ErrorOf(HttpResponseMessage res)
    {
        var body = await res.ReadJson();
        return (body.GetProperty("error").GetString()!, body.GetProperty("message").GetString()!);
    }

    // ------------------------------------------------------------ read-back

    [Fact]
    public async Task Switches_AreOnByDefault_AndReadBackFromTheApi()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var s = await Ok(await host.Anon().GetAsync("/api/switches"));
        Assert.True(s.GetProperty("wagersHumanEnabled").GetBoolean());
        Assert.True(s.GetProperty("raidLootEnabled").GetBoolean());
        Assert.True(s.GetProperty("islandSaleCarriesRum").GetBoolean());
        Assert.True(s.GetProperty("dailyRaceFixedSalt").GetBoolean());
        Assert.True(s.GetProperty("weeklyRewardCheckedOnly").GetBoolean());
        Assert.False(s.GetProperty("botStakes").GetBoolean());
        Assert.Equal(20, s.GetProperty("mintDailyBudget").GetInt32());
        Assert.Equal(30, s.GetProperty("sessionLifetimeDays").GetInt32());

        var (off, _, _) = NewHost(extra: new()
        {
            ["WAGERS_HUMAN_ENABLED"] = "false", ["RAID_LOOT_ENABLED"] = "0", ["ISLAND_SALE_CARRIES_RUM"] = "False",
            ["DAILY_RACE_FIXED_SALT"] = "false",
        });
        using var _o = off;
        var o = await Ok(await off.Anon().GetAsync("/api/switches"));
        Assert.False(o.GetProperty("wagersHumanEnabled").GetBoolean());
        Assert.False(o.GetProperty("raidLootEnabled").GetBoolean());
        Assert.False(o.GetProperty("islandSaleCarriesRum").GetBoolean());
        Assert.False(o.GetProperty("dailyRaceFixedSalt").GetBoolean());
    }

    [Fact]
    public void Flag_ParsesTrueFalseAndOneZero_AndKeepsTheDefaultOtherwise()
    {
        Assert.True(ApiSettings.Flag(null, true));
        Assert.False(ApiSettings.Flag("", false));
        Assert.False(ApiSettings.Flag("false", true));
        Assert.False(ApiSettings.Flag(" 0 ", true));
        Assert.True(ApiSettings.Flag("TRUE", false));
        Assert.True(ApiSettings.Flag("1", false));
        Assert.True(ApiSettings.Flag("off", true)); // a typo keeps the default (on)
    }

    // ------------------------------------------------------------ wager deposits refused after the transfer (item 1)

    [Fact]
    public async Task WagerDeposit_TooSmallOrAnotherMemo_IsRecordedAndRefundedOnce_AndTheAnswerSaysSo()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        var lobby = await Ok(await a.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var lobbyId = lobby.GetProperty("lobbyId").GetGuid();
        var memo = "wager:" + lobbyId;

        // too small: recorded, refunded in full (what was sent), 409 deposit_refunded naming the reason and the refund
        var small = Deposit(rpc.Inner, a.Key, treasury.PublicKey.Key, Stake - 1, memo);
        var res = await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = small });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var (code, message) = await ErrorOf(res);
        Assert.Equal("deposit_refunded", code);
        Assert.Contains("the stake is " + Stake, message);
        Assert.Contains("refunded to your wallet", message);
        var refund = Assert.Single(Payouts(host));
        Assert.Equal(WagerService.RefundReason, refund.Reason);
        Assert.Equal(Stake - 1, refund.Lamports);
        Assert.Equal(a.Key, refund.Wallet);
        Assert.Equal(Payout.Sent, refund.Status);
        Assert.StartsWith("dep:", refund.RefId);

        // the same signature again: the same answer, never a second refund
        var again = await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = small });
        Assert.Equal("deposit_refunded", (await ErrorOf(again)).error);
        Assert.Single(Payouts(host));

        // another memo (a market payment sent here) and no memo at all: refunded too
        var otherMemo = Deposit(rpc.Inner, a.Key, treasury.PublicKey.Key, Stake, "buy:" + Guid.NewGuid());
        var (c2, m2) = await ErrorOf(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = otherMemo }));
        Assert.Equal("deposit_refunded", c2);
        Assert.Contains("memo", m2);
        var noMemo = Deposit(rpc.Inner, a.Key, treasury.PublicKey.Key, Stake);
        Assert.Equal("deposit_refunded", (await ErrorOf(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = noMemo }))).error);
        Assert.Equal(new[] { Stake - 1, Stake, Stake }, Payouts(host).Select(p => p.Lamports).ToArray());
        Assert.All(Payouts(host), p => Assert.Equal(WagerService.RefundReason, p.Reason));

        var deposits = await Ok(await a.Http.GetAsync("/api/deposits/mine"));
        Assert.All(deposits.GetProperty("deposits").EnumerateArray(), d => Assert.Equal("refunded", d.GetProperty("status").GetString()));

        // the seat is still open: a correct transfer pays the stake as before
        var good = Deposit(rpc.Inner, a.Key, treasury.PublicKey.Key, Stake, memo);
        var state = await Ok(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = good }));
        Assert.True(state.GetProperty("wager").GetProperty("host").GetProperty("deposited").GetBoolean());
        Assert.Equal(3, Payouts(host).Count);
    }

    [Fact]
    public async Task WagerDeposit_NotFromYourWalletOrNotToTheTreasury_IsStillRefused_NothingRecorded()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        var b = await SignInAsync(host);
        var lobbyId = (await Ok(await a.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }))).GetProperty("lobbyId").GetGuid();
        // b's transfer submitted by a: not a's money, nothing to refund to a
        var other = Deposit(rpc.Inner, b.Key, treasury.PublicKey.Key, Stake, "wager:" + lobbyId);
        await AssertError(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = other }), HttpStatusCode.BadRequest, "wrong_sender");
        // a transfer to someone else
        var elsewhere = Deposit(rpc.Inner, a.Key, b.Key, Stake, "wager:" + lobbyId);
        await AssertError(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = elsewhere }), HttpStatusCode.BadRequest, "wrong_recipient");
        Assert.Empty(Payouts(host));
    }

    // ------------------------------------------------------------ WAGERS_HUMAN_ENABLED

    [Fact]
    public async Task WagersHumanOff_RefusesNewStakes_AndRefundsADepositOnAnOpenWager()
    {
        var (host, rpc, treasury) = NewHost(extra: new() { ["WAGERS_HUMAN_ENABLED"] = "false" });
        using var _h = host;
        var a = await SignInAsync(host);
        await AssertError(await a.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }), HttpStatusCode.Conflict, "wagers_off");
        var lobbyId = (await Ok(await a.Http.PostAsJsonAsync("/api/lobbies", new { @public = false }))).GetProperty("lobbyId").GetGuid();
        await AssertError(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}", new { stakeLamports = Stake }), HttpStatusCode.Conflict, "wagers_off");

        // a wager opened before the switch went off: a deposit is recorded and refunded, never attached
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var playerId = await db.Players.Where(p => p.WalletAddress == a.Key).Select(p => p.Id).FirstAsync();
            db.Wagers.Add(WagerService.NewForLobby(lobbyId, playerId, Stake, host.Clock.UtcNow));
            await db.SaveChangesAsync();
        }
        var sig = Deposit(rpc.Inner, a.Key, treasury.PublicKey.Key, Stake, "wager:" + lobbyId);
        var (code, message) = await ErrorOf(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = sig }));
        Assert.Equal("deposit_refunded", code);
        Assert.Contains("switched off", message);
        var refund = Assert.Single(Payouts(host));
        Assert.Equal(Stake, refund.Lamports);
        var view = await Ok(await a.Http.GetAsync($"/api/wagers/{lobbyId}"));
        Assert.False(view.GetProperty("host").GetProperty("deposited").GetBoolean());

        // removing the stake still works (0 never needs the switch)
        var cleared = await Ok(await a.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}", new { stakeLamports = 0 }));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("wager").ValueKind);
    }

    // ------------------------------------------------------------ RAID_LOOT_ENABLED

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RaidLoot_OnTakes25Percent_OffWinsAndShieldsButLootsNothing(bool lootOn)
    {
        var (host, _, _) = NewHost(extra: new() { ["RAID_LOOT_ENABLED"] = lootOn ? "true" : "false" });
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        host.Clock.Advance(TimeSpan.FromHours(4)); // 480: vault 288, exposed 192
        await EarnRumAsync(host, raider, 300);

        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        var loot = start.GetProperty("loot");
        Assert.Equal(lootOn, loot.GetProperty("enabled").GetBoolean());
        Assert.Equal(lootOn ? 48 : 0, loot.GetProperty("ifWon").GetInt64());
        host.Clock.Advance(TimeSpan.FromSeconds(90));
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{start.GetProperty("raidId").GetGuid()}/result",
            new { won = true, seconds = 85, guardianSunk = true, minuteReached = 6 }));
        Assert.Equal("won", result.GetProperty("result").GetString());
        Assert.Equal(lootOn ? 48 : 0, result.GetProperty("rumLooted").GetInt64());
        Assert.Equal(lootOn ? 53 : 5, result.GetProperty("rum").GetInt64());

        var island = await Ok(await owner.Http.GetAsync($"/api/islands/{id}"));
        Assert.True(island.GetProperty("shielded").GetBoolean());
        Assert.Equal(lootOn ? 144 : 192, island.GetProperty("rum").GetProperty("exposed").GetInt64());
    }

    // ------------------------------------------------------------ ISLAND_SALE_CARRIES_RUM

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IslandSale_CarriesRumByDefault_OffTheSellerKeepsTheRumAndBuildingsReset(bool carries)
    {
        var (host, rpc, treasury) = NewHost(extra: new() { ["ISLAND_SALE_CARRIES_RUM"] = carries ? "true" : "false" });
        using var _h = host;
        var seller = await SignInAsync(host);
        var buyer = await SignInAsync(host);
        var (islandId, mint) = await MintedIslandAsync(seller);
        await EarnRumAsync(host, seller, 3000); // 300 RUM
        await Ok(await seller.Http.PostAsJsonAsync($"/api/islands/{islandId}/build", new { kind = "walls" })); // -200
        host.Clock.Advance(TimeSpan.FromHours(4)); // the island fills to its cap, 480
        Assert.Equal(100, await RumAsync(seller));

        var escrow = EscrowNft(rpc.Inner, seller, treasury, mint);
        var listingId = (await Ok(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 50_000_000, signature = escrow })))
            .GetProperty("listingId").GetGuid();
        var pay = Deposit(rpc.Inner, buyer.Key, treasury.PublicKey.Key, 50_000_000, $"buy:{listingId}");
        var sold = await Ok(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = pay }));
        Assert.Equal("sold", sold.GetProperty("status").GetString());

        var island = await Ok(await buyer.Http.GetAsync($"/api/islands/{islandId}"));
        Assert.True(island.GetProperty("isMine").GetBoolean());
        var walls = island.GetProperty("buildings").EnumerateArray().Single(b => b.GetProperty("kind").GetString() == "walls").GetProperty("level").GetInt32();
        if (carries)
        {
            Assert.Equal(480, island.GetProperty("rum").GetProperty("total").GetInt64());
            Assert.Equal(1, walls);
            Assert.Equal(100, await RumAsync(seller));
        }
        else
        {
            Assert.Equal(0, island.GetProperty("rum").GetProperty("total").GetInt64());
            Assert.Equal(0, walls); // RUM-bought levels reset (I26.2)
            Assert.Equal(580, await RumAsync(seller)); // the island's 480 RUM stayed with the seller
            // completing again never credits twice
            await Ok(await seller.Http.PostAsync($"/api/market/{listingId}/complete", null));
            Assert.Equal(580, await RumAsync(seller));
        }
    }

    // ------------------------------------------------------------ DAILY_RACE_FIXED_SALT

    [Fact]
    public void RaceSalt_ConfiguredWins_FixedIsThePublicDefault_OffIsSecretAndStable()
    {
        Assert.Equal("mine", DailyRaceService.SaltFor(" mine ", false, "key"));
        Assert.Equal("mine", DailyRaceService.SaltFor("mine", true, null));
        Assert.Equal(DailyRaceService.DefaultSalt, DailyRaceService.SaltFor(null, true, "key"));
        var derived = DailyRaceService.SaltFor(null, false, "key");
        Assert.NotEqual(DailyRaceService.DefaultSalt, derived);
        Assert.Equal(derived, DailyRaceService.SaltFor("", false, "key")); // stable across restarts (same key)
        Assert.DoesNotContain("key", derived);
        var random = DailyRaceService.SaltFor(null, false, null);
        Assert.NotEqual(DailyRaceService.DefaultSalt, random);
        Assert.Equal(random, DailyRaceService.SaltFor(null, false, null)); // one per process
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RaceSeed_IsTheKnownOneOnlyWithTheFixedSalt(bool fixedSalt)
    {
        var (host, _, _) = NewHost(extra: new() { ["DAILY_RACE_FIXED_SALT"] = fixedSalt ? "true" : "false", ["DAILY_RACE_SALT"] = "" });
        using var _h = host;
        var race = await Ok(await host.Anon().GetAsync("/api/race/daily"));
        var seed = race.GetProperty("race").GetProperty("seed").GetInt32();
        var known = DailyRaceService.SeedFor(DateTime.SpecifyKind(host.Clock.UtcNow.Date, DateTimeKind.Utc), DailyRaceService.DefaultSalt);
        if (fixedSalt) Assert.Equal(known, seed);
        else Assert.NotEqual(known, seed);
    }
}
