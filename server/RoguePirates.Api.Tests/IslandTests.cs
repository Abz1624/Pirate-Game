using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>The fake chain, with sends that can be made to fail (a mint or transfer that does not go through).</summary>
public sealed class FlakyRpc : ISolanaRpc
{
    public FakeSolanaRpc Inner { get; } = new();
    public bool FailSends { get; set; }

    public Task<long> GetBalanceAsync(string a, CancellationToken ct = default) => Inner.GetBalanceAsync(a, ct);
    public Task<ChainTransaction?> GetTransactionAsync(string s, CancellationToken ct = default) => Inner.GetTransactionAsync(s, ct);
    public Task<string> GetLatestBlockhashAsync(CancellationToken ct = default) => Inner.GetLatestBlockhashAsync(ct);
    public Task<string> SendTransactionAsync(byte[] tx, CancellationToken ct = default) =>
        FailSends ? throw new SolanaRpcException(-32002, "Transaction simulation failed (test)") : Inner.SendTransactionAsync(tx, ct);
    public Task<string?> GetSignatureStatusAsync(string s, CancellationToken ct = default) => Inner.GetSignatureStatusAsync(s, ct);
    public Task<long> GetMinimumBalanceForRentExemptionAsync(int n, CancellationToken ct = default) => Inner.GetMinimumBalanceForRentExemptionAsync(n, ct);
    public Task<IReadOnlyList<TokenHolding>> GetTokenAccountsByOwnerAsync(string o, string m, CancellationToken ct = default) => Inner.GetTokenAccountsByOwnerAsync(o, m, ct);
    public Task<bool> AccountExistsAsync(string a, CancellationToken ct = default) => Inner.AccountExistsAsync(a, ct);
    public Task<string> RequestAirdropAsync(string a, long l, CancellationToken ct = default) => Inner.RequestAirdropAsync(a, l, ct);
}

/// <summary>Hackathon islands: claim and NFT mint, RUM accrual, collect, build, raids (no SOL stake; old staked raids still settle) and the market.</summary>
public sealed class IslandTests
{
    internal sealed record Wallet(Account Account, string Token, HttpClient Http)
    {
        public string Key => Account.PublicKey.Key;
    }

    internal static (ApiHost host, FlakyRpc rpc, Account treasury) NewHost(bool treasuryOn = true)
    {
        var rpc = new FlakyRpc();
        var treasury = new Account();
        var settings = new Dictionary<string, string?> { ["PUBLIC_API_URL"] = "https://api.test" };
        if (treasuryOn) settings["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes);
        var host = new ApiHost(settings, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });
        return (host, rpc, treasury);
    }

    internal static async Task<Wallet> SignInAsync(ApiHost host)
    {
        var account = new Account();
        var anon = host.Anon();
        var nonce = await (await anon.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = account.PublicKey.Key })).ReadJson();
        var message = nonce.GetProperty("message").GetString()!;
        var sig = Encoders.Base58.EncodeData(account.Sign(Encoding.UTF8.GetBytes(message)));
        var res = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = account.PublicKey.Key, signature = sig, message });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var token = (await res.ReadJson()).GetProperty("token").GetString()!;
        return new Wallet(account, token, host.As(token));
    }

    internal static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.ReadJson();
    }

    internal static async Task AssertError(HttpResponseMessage res, HttpStatusCode status, string code)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(status == res.StatusCode, $"expected {(int)status} {code}, got {(int)res.StatusCode}: {body}");
        Assert.Equal(code, JsonDocument.Parse(body).RootElement.GetProperty("error").GetString());
    }

    internal static async Task<JsonElement> ClaimAsync(Wallet w, string name = "Tortuga") =>
        await Ok(await w.Http.PostAsJsonAsync("/api/islands/claim", new { name }));

    internal static async Task<long> RumAsync(Wallet w) => (await Ok(await w.Http.GetAsync("/api/islands/mine"))).GetProperty("rum").GetInt64();

    internal static async Task EarnRumAsync(Wallet w, int kills) =>
        await Ok(await w.Http.PostAsJsonAsync("/api/runs", new { kills, runSeconds = 1200, level = 10 }));

    static string Deposit(FakeSolanaRpc rpc, string from, string to, long lamports, string? memo = null)
    {
        var sig = HackathonTests.RandomSignature();
        rpc.Transactions[sig] = new ChainTransaction(sig, 100, false, new[] { from }, new[] { new SolTransfer(from, to, lamports) },
            memo == null ? Array.Empty<string>() : new[] { memo }, null);
        return sig;
    }

    // ---- claim, mint, metadata ----

    [Fact]
    public async Task Claim_MintsTheIslandNft_OnePerPlayer_WalletOnly_AndServesMetadataAndImage()
    {
        var (host, rpc, _) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);

        var island = await ClaimAsync(a, "Tortuga");
        Assert.Equal("Tortuga", island.GetProperty("name").GetString());
        var nft = island.GetProperty("nft");
        Assert.Equal("minted", nft.GetProperty("status").GetString());
        Assert.True(SolanaService.IsValidAddress(nft.GetProperty("mint").GetString()));
        Assert.Single(rpc.Inner.Sent);
        var id = island.GetProperty("islandId").GetGuid();
        Assert.Equal($"https://api.test/api/islands/{id}/metadata.json", nft.GetProperty("metadataUrl").GetString());
        Assert.True(island.GetProperty("isMine").GetBoolean());
        Assert.Equal(1, island.GetProperty("level").GetInt32());
        Assert.Equal(4, island.GetProperty("buildings").GetArrayLength());

        await AssertError(await a.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Second" }), HttpStatusCode.Conflict, "already_has_island");
        var guest = await host.NewPlayerAsync("Guesty");
        await AssertError(await guest.Http.PostAsJsonAsync("/api/islands/claim", new { name = "Nope" }), HttpStatusCode.Forbidden, "wallet_required");
        await AssertError(await (await SignInAsync(host)).Http.PostAsJsonAsync("/api/islands/claim", new { name = "x" }), HttpStatusCode.BadRequest, "invalid_name");

        var meta = await Ok(await host.Anon().GetAsync($"/api/islands/{id}/metadata.json"));
        Assert.Equal("Rogue Island: Tortuga", meta.GetProperty("name").GetString());
        Assert.Equal("RPISL", meta.GetProperty("symbol").GetString());
        var image = meta.GetProperty("image").GetString()!;
        Assert.StartsWith("https://api.test/static/islands/", image);

        var png = await host.Anon().GetAsync(new Uri(image).AbsolutePath);
        Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);
        var bytes = await png.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);

        var world = await Ok(await host.Anon().GetAsync("/api/islands"));
        Assert.Equal(24, world.GetProperty("slots").GetArrayLength());
        Assert.Equal(23, world.GetProperty("freeSlots").GetInt32());
        var listed = world.GetProperty("islands")[0];
        Assert.Equal(a.Key[..4] + "..." + a.Key[^4..], listed.GetProperty("owner").GetProperty("walletShort").GetString());
        Assert.True(listed.GetProperty("raidable").GetBoolean());
    }

    [Fact]
    public async Task Claim_WhenTheMintFails_IsPending_AndTheRetryMints()
    {
        var (host, rpc, _) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        rpc.FailSends = true;
        var island = await ClaimAsync(a);
        var id = island.GetProperty("islandId").GetGuid();
        Assert.Equal("pending", island.GetProperty("nft").GetProperty("status").GetString());
        Assert.Contains("solana_rpc_error", island.GetProperty("nft").GetProperty("error").GetString());

        await AssertError(await a.Http.PostAsync($"/api/islands/{id}/mint", null), HttpStatusCode.BadGateway, "mint_failed");
        rpc.FailSends = false;
        var retried = await Ok(await a.Http.PostAsync($"/api/islands/{id}/mint", null));
        Assert.Equal("minted", retried.GetProperty("nft").GetProperty("status").GetString());
        await AssertError(await a.Http.PostAsync($"/api/islands/{id}/mint", null), HttpStatusCode.Conflict, "already_minted");
        var b = await SignInAsync(host);
        await AssertError(await b.Http.PostAsync($"/api/islands/{id}/mint", null), HttpStatusCode.Forbidden, "not_your_island");
    }

    [Fact]
    public async Task Claim_WithoutATreasury_IsPending()
    {
        var (host, _, _) = NewHost(treasuryOn: false);
        using var _h = host;
        var island = await ClaimAsync(await SignInAsync(host));
        Assert.Equal("pending", island.GetProperty("nft").GetProperty("status").GetString());
        Assert.StartsWith("treasury_off", island.GetProperty("nft").GetProperty("error").GetString());
    }

    // ---- RUM: accrual, collect, build, voyages ----

    [Fact]
    public async Task Rum_AccruesWithTheVaultSplit_ToTheCap_Collects_Builds_AndVoyagesEarnRum()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        var id = (await ClaimAsync(a)).GetProperty("islandId").GetGuid();

        host.Clock.Advance(TimeSpan.FromHours(2)); // 120/h -> 240: vault 60% = 144, exposed 96
        var rum = (await Ok(await a.Http.GetAsync($"/api/islands/{id}"))).GetProperty("rum");
        Assert.Equal(144, rum.GetProperty("vault").GetInt64());
        Assert.Equal(96, rum.GetProperty("exposed").GetInt64());
        Assert.Equal(480, rum.GetProperty("cap").GetInt64());

        var c1 = await Ok(await a.Http.PostAsync($"/api/islands/{id}/collect", null));
        Assert.Equal(240, c1.GetProperty("collected").GetInt64());
        Assert.Equal(240, c1.GetProperty("rum").GetInt64());

        host.Clock.Advance(TimeSpan.FromHours(10)); // capped at 480
        var c2 = await Ok(await a.Http.PostAsync($"/api/islands/{id}/collect", null));
        Assert.Equal(480, c2.GetProperty("collected").GetInt64());
        Assert.Equal(720, await RumAsync(a));

        var b1 = await Ok(await a.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "rum_store" }));
        Assert.Equal(2, b1.GetProperty("level").GetInt32());
        Assert.Equal(300, b1.GetProperty("cost").GetInt64());
        Assert.Equal(420, b1.GetProperty("rum").GetInt64());
        Assert.Equal(2, b1.GetProperty("island").GetProperty("level").GetInt32());
        Assert.Equal(180, b1.GetProperty("island").GetProperty("rum").GetProperty("ratePerHour").GetInt32());

        var b2 = await Ok(await a.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "cannon_tower" }));
        Assert.Equal(170, b2.GetProperty("rum").GetInt64());
        var defence = b2.GetProperty("island").GetProperty("defence");
        Assert.Equal(1, defence.GetProperty("extraWaves").GetInt32());
        Assert.Equal(3, defence.GetProperty("weaponLevel").GetInt32());
        Assert.Equal(170, await RumAsync(a));
        await AssertError(await a.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "moat" }), HttpStatusCode.BadRequest, "invalid_kind");

        // voyages: kills / 10 RUM for wallet players, nothing for guests
        var run = await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 105, runSeconds = 600, level = 8 }));
        Assert.Equal(10, run.GetProperty("rumEarned").GetInt64());
        var guest = await host.NewPlayerAsync("Guesty");
        var grun = await Ok(await guest.Http.PostAsJsonAsync("/api/runs", new { kills = 105, runSeconds = 600, level = 8 }));
        Assert.Equal(0, grun.GetProperty("rumEarned").GetInt64());

        var other = await SignInAsync(host);
        await AssertError(await other.Http.PostAsync($"/api/islands/{id}/collect", null), HttpStatusCode.Forbidden, "not_your_island");
    }

    [Fact]
    public async Task Build_WithTooLittleRum_IsRefused_AndChargesNothing()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        var id = (await ClaimAsync(a)).GetProperty("islandId").GetGuid();
        await EarnRumAsync(a, 1500); // 150 RUM
        await AssertError(await a.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "walls" }), HttpStatusCode.BadRequest, "insufficient_rum");
        Assert.Equal(150, await RumAsync(a));
        await EarnRumAsync(a, 500); // 200
        var built = await Ok(await a.Http.PostAsJsonAsync($"/api/islands/{id}/build", new { kind = "walls" }));
        Assert.Equal(0, built.GetProperty("rum").GetInt64());
        Assert.Equal(1.25, built.GetProperty("island").GetProperty("defence").GetProperty("hullMultiplier").GetDouble());
    }

    // ---- raids ----

    [Fact]
    public async Task Raid_WinLoots25PercentOfExposed_ShieldsTheIsland_AndHistoryShowsBothSides()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        host.Clock.Advance(TimeSpan.FromHours(4)); // 480: vault 288, exposed 192

        await AssertError(await owner.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }), HttpStatusCode.BadRequest, "own_island");
        await AssertError(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }), HttpStatusCode.BadRequest, "insufficient_rum");
        await EarnRumAsync(raider, 300); // 30 RUM

        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }));
        var raidId = start.GetProperty("raidId").GetGuid();
        Assert.Equal(25, start.GetProperty("feePaid").GetInt64());
        Assert.Equal(5, start.GetProperty("rum").GetInt64());
        var def = start.GetProperty("defender");
        Assert.Equal(1.0, def.GetProperty("hullMultiplier").GetDouble());
        Assert.Equal(0, def.GetProperty("extraWaves").GetInt32());
        Assert.Equal(1, def.GetProperty("weaponLevel").GetInt32());
        Assert.NotEqual(0, def.GetProperty("seed").GetInt32());
        Assert.Equal(192, start.GetProperty("loot").GetProperty("exposedRum").GetInt64());
        Assert.Equal(48, start.GetProperty("loot").GetProperty("ifWon").GetInt64());
        Assert.Equal(24, start.GetProperty("loot").GetProperty("ifPartial").GetInt64());
        Assert.Equal(JsonValueKind.Null, start.GetProperty("wager").ValueKind);

        // one raid at a time per island
        var third = await SignInAsync(host);
        await EarnRumAsync(third, 300);
        await AssertError(await third.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }), HttpStatusCode.Conflict, "raid_in_progress");

        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 500 }), HttpStatusCode.BadRequest, "invalid_seconds");
        host.Clock.Advance(TimeSpan.FromSeconds(90)); // the island is at its cap: nothing more accrues
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 85 }));
        Assert.Equal("won", result.GetProperty("result").GetString());
        Assert.Equal(48, result.GetProperty("rumLooted").GetInt64()); // 192 * 25%
        Assert.Equal(53, result.GetProperty("rum").GetInt64());
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 85 }), HttpStatusCode.Conflict, "raid_finished");

        var island = await Ok(await third.Http.GetAsync($"/api/islands/{id}"));
        Assert.True(island.GetProperty("shielded").GetBoolean());
        Assert.False(island.GetProperty("raidable").GetBoolean());
        Assert.Equal(144, island.GetProperty("rum").GetProperty("exposed").GetInt64());
        Assert.Equal(288, island.GetProperty("rum").GetProperty("vault").GetInt64()); // the vault is never raided
        await AssertError(await third.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }), HttpStatusCode.Conflict, "island_shielded");

        host.Clock.Advance(TimeSpan.FromHours(4));
        Assert.True((await Ok(await third.Http.GetAsync($"/api/islands/{id}"))).GetProperty("raidable").GetBoolean());

        var raiderHistory = await Ok(await raider.Http.GetAsync("/api/raids/mine"));
        Assert.Equal(1, raiderHistory.GetProperty("attacks").GetArrayLength());
        var ownerHistory = await Ok(await owner.Http.GetAsync("/api/raids/mine"));
        Assert.Equal(1, ownerHistory.GetProperty("defences").GetArrayLength());
        Assert.Equal(48, ownerHistory.GetProperty("defences")[0].GetProperty("rumLooted").GetInt64());
    }

    [Fact]
    public async Task Raid_PartialWinTakesHalf_LossTakesNothing_AndAttackingKeepsYourOwnShield()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var a = await SignInAsync(host);
        var b = await SignInAsync(host);
        var ia = (await ClaimAsync(a, "Alpha")).GetProperty("islandId").GetGuid();
        var ib = (await ClaimAsync(b, "Bravo")).GetProperty("islandId").GetGuid();
        await EarnRumAsync(a, 1000);
        await EarnRumAsync(b, 1000);
        host.Clock.Advance(TimeSpan.FromHours(4)); // both: exposed 192

        // a loses against b's island: nothing moves, no shield
        var r1 = (await Ok(await a.Http.PostAsJsonAsync("/api/raids/start", new { islandId = ib }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromSeconds(30));
        var lost = await Ok(await a.Http.PostAsJsonAsync($"/api/raids/{r1}/result", new { won = false, seconds = 30 }));
        Assert.Equal("lost", lost.GetProperty("result").GetString());
        Assert.Equal(0, lost.GetProperty("rumLooted").GetInt64());
        Assert.False((await Ok(await a.Http.GetAsync($"/api/islands/{ib}"))).GetProperty("shielded").GetBoolean());

        // b partially wins against a: 12.5% of exposed, a is shielded
        var r2 = (await Ok(await b.Http.PostAsJsonAsync("/api/raids/start", new { islandId = ia }))).GetProperty("raidId").GetGuid();
        var partial = await Ok(await b.Http.PostAsJsonAsync($"/api/raids/{r2}/result", new { won = true, partial = true, seconds = 1 }));
        Assert.Equal("partial", partial.GetProperty("result").GetString());
        Assert.Equal(24, partial.GetProperty("rumLooted").GetInt64()); // 192 * 12.5%

        // a is shielded; a attacking b does not end a's shield
        var r3 = (await Ok(await a.Http.PostAsJsonAsync("/api/raids/start", new { islandId = ib }))).GetProperty("raidId").GetGuid();
        await Ok(await a.Http.PostAsJsonAsync($"/api/raids/{r3}/result", new { won = true, seconds = 1 }));
        Assert.True((await Ok(await b.Http.GetAsync($"/api/islands/{ia}"))).GetProperty("shielded").GetBoolean());
    }

    [Fact]
    public async Task Raid_ExpiresAfter20Minutes()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await EarnRumAsync(raider, 600);
        var raidId = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromMinutes(20) + TimeSpan.FromSeconds(1));
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 100 }), HttpStatusCode.Conflict, "raid_expired");
        var history = await Ok(await raider.Http.GetAsync("/api/raids/mine"));
        Assert.Equal("abandoned", history.GetProperty("attacks")[0].GetProperty("result").GetString());
        // the island is free to raid again
        Assert.True((await Ok(await raider.Http.GetAsync($"/api/islands/{id}"))).GetProperty("raidable").GetBoolean());
    }

    [Fact]
    public async Task RaidStakes_AreOff_StartRefusesAStake_AndTheWagerDepositIsGone()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await EarnRumAsync(raider, 600);
        var rumBefore = await RumAsync(raider);

        await AssertError(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id, stakeLamports = 10_000_000 }), HttpStatusCode.BadRequest, "raid_stakes_off");
        await AssertError(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id, stakeLamports = -1 }), HttpStatusCode.BadRequest, "invalid_stake");
        Assert.Equal(rumBefore, await RumAsync(raider)); // a refused start charges no fee

        var start = await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id, stakeLamports = 0 }));
        var raidId = start.GetProperty("raidId").GetGuid();
        Assert.Equal(JsonValueKind.Null, start.GetProperty("wager").ValueKind);
        var sig = Deposit(rpc.Inner, raider.Key, treasury.PublicKey.Key, 10_000_000, $"raid:{raidId}");
        await AssertError(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/wager", new { signature = sig }), HttpStatusCode.Gone, "raid_stakes_off");

        host.Clock.Advance(TimeSpan.FromSeconds(60));
        var sentBefore = rpc.Inner.Sent.Count;
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 60 }));
        Assert.Equal("won", result.GetProperty("result").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("wager").ValueKind);
        Assert.Equal(sentBefore, rpc.Inner.Sent.Count); // no SOL moves on a raid
    }

    /// <summary>A raid staked and funded before raid stakes were switched off (seeded in the database).</summary>
    static async Task<Guid> LegacyStakedRaidAsync(ApiHost host, Wallet raider, Guid islandId, long stake)
    {
        var raidId = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId }))).GetProperty("raidId").GetGuid();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var raid = await db.Raids.FirstAsync(r => r.Id == raidId);
        var wager = new Wager
        {
            Id = Guid.NewGuid(), Kind = RoguePirates.Api.Hackathon.RaidService.WagerKind, StakeLamports = stake,
            HostPlayerId = raid.AttackerPlayerId, GuestPlayerId = raid.DefenderPlayerId, VsHouse = true,
            Status = Wager.Funded, HostDepositSig = HackathonTests.RandomSignature(), CreatedAt = host.Clock.UtcNow,
        };
        db.Wagers.Add(wager);
        raid.WagerId = wager.Id;
        await db.SaveChangesAsync();
        return raidId;
    }

    [Fact]
    public async Task LegacyStakedRaid_WinPaysDouble_FailedPayoutIsRetriedBySettle_OnlyOnce()
    {
        var (host, rpc, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        await EarnRumAsync(raider, 600);
        var raidId = await LegacyStakedRaidAsync(host, raider, id, 10_000_000);

        host.Clock.Advance(TimeSpan.FromSeconds(60));
        rpc.FailSends = true;
        var result = await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{raidId}/result", new { won = true, seconds = 60 }));
        Assert.Equal("funded", result.GetProperty("wager").GetProperty("status").GetString());
        Assert.Equal("failed", result.GetProperty("wager").GetProperty("payout").GetProperty("status").GetString());
        rpc.FailSends = false;
        var sentBefore = rpc.Inner.Sent.Count;
        var settled = await Ok(await raider.Http.PostAsync($"/api/raids/{raidId}/settle", null));
        var w = settled.GetProperty("wager");
        Assert.Equal("settled", w.GetProperty("status").GetString());
        Assert.Equal("sent", w.GetProperty("payout").GetProperty("status").GetString());
        Assert.Equal(20_000_000, w.GetProperty("payout").GetProperty("lamports").GetInt64());
        Assert.Equal(sentBefore + 1, rpc.Inner.Sent.Count);
        // settling again pays nothing more
        await Ok(await raider.Http.PostAsync($"/api/raids/{raidId}/settle", null));
        Assert.Equal(sentBefore + 1, rpc.Inner.Sent.Count);
    }

    // ---- marketplace ----

    static async Task<(Guid islandId, string mint)> MintedIslandAsync(Wallet w, string name = "Tortuga")
    {
        var island = await ClaimAsync(w, name);
        return (island.GetProperty("islandId").GetGuid(), island.GetProperty("nft").GetProperty("mint").GetString()!);
    }

    /// <summary>The seller's NFT transfer into escrow: a transaction they signed, and the treasury now holds the mint.</summary>
    static string EscrowNft(FakeSolanaRpc rpc, Wallet seller, Account treasury, string mint)
    {
        var sig = HackathonTests.RandomSignature();
        rpc.Transactions[sig] = new ChainTransaction(sig, 100, false, new[] { seller.Key }, Array.Empty<SolTransfer>(), Array.Empty<string>(), null);
        rpc.Holdings.Add(new TokenHolding(new Account().PublicKey.Key, mint, treasury.PublicKey.Key, 1));
        return sig;
    }

    [Fact]
    public async Task Market_ListLocksTheIsland_BuyMovesNftIslandAndSol()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var seller = await SignInAsync(host);
        var buyer = await SignInAsync(host);
        var (islandId, mint) = await MintedIslandAsync(seller);

        // not in escrow yet
        var notSent = HackathonTests.RandomSignature();
        rpc.Inner.Transactions[notSent] = new ChainTransaction(notSent, 1, false, new[] { seller.Key }, Array.Empty<SolTransfer>(), Array.Empty<string>(), null);
        await AssertError(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 50_000_000, signature = notSent }), HttpStatusCode.BadRequest, "nft_not_received");

        var escrow = EscrowNft(rpc.Inner, seller, treasury, mint);
        await AssertError(await buyer.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 50_000_000, signature = escrow }), HttpStatusCode.Forbidden, "not_your_island");
        await AssertError(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 5, signature = escrow }), HttpStatusCode.BadRequest, "invalid_price");
        var listing = await Ok(await seller.Http.PostAsJsonAsync("/api/market/list", new { islandId, priceLamports = 50_000_000, signature = escrow }));
        var listingId = listing.GetProperty("listingId").GetGuid();
        Assert.Equal("active", listing.GetProperty("status").GetString());
        Assert.Equal($"buy:{listingId}", listing.GetProperty("memo").GetString());

        var market = await Ok(await host.Anon().GetAsync("/api/market"));
        Assert.Equal(1, market.GetProperty("listings").GetArrayLength());
        Assert.Equal("Tortuga", market.GetProperty("listings")[0].GetProperty("island").GetProperty("name").GetString());

        // the listed island is locked
        Assert.True((await Ok(await buyer.Http.GetAsync($"/api/islands/{islandId}"))).GetProperty("listed").GetBoolean());
        await EarnRumAsync(buyer, 600);
        await AssertError(await buyer.Http.PostAsJsonAsync("/api/raids/start", new { islandId }), HttpStatusCode.Conflict, "island_listed");
        await AssertError(await seller.Http.PostAsync($"/api/islands/{islandId}/collect", null), HttpStatusCode.Conflict, "island_listed");
        await AssertError(await seller.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = HackathonTests.RandomSignature() }), HttpStatusCode.BadRequest, "own_listing");

        // the buyer pays (too little first)
        var low = Deposit(rpc.Inner, buyer.Key, treasury.PublicKey.Key, 10_000_000, $"buy:{listingId}");
        await AssertError(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = low }), HttpStatusCode.BadRequest, "amount_too_low");
        var pay = Deposit(rpc.Inner, buyer.Key, treasury.PublicKey.Key, 50_000_000, $"buy:{listingId}");
        var sentBefore = rpc.Inner.Sent.Count;
        var sold = await Ok(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = pay }));
        Assert.Equal("sold", sold.GetProperty("status").GetString());
        Assert.Equal(sentBefore + 2, rpc.Inner.Sent.Count); // the NFT transfer and the seller's payment
        Assert.True(SolanaService.IsValidSignature(sold.GetProperty("transferSignature").GetString()));
        Assert.True(SolanaService.IsValidSignature(sold.GetProperty("payoutSignature").GetString()));

        var island = await Ok(await buyer.Http.GetAsync($"/api/islands/{islandId}"));
        Assert.True(island.GetProperty("isMine").GetBoolean());
        Assert.False(island.GetProperty("listed").GetBoolean());
        Assert.Equal(1, (await Ok(await buyer.Http.GetAsync("/api/islands/mine"))).GetProperty("islands").GetArrayLength());
        Assert.Equal(0, (await Ok(await seller.Http.GetAsync("/api/islands/mine"))).GetProperty("islands").GetArrayLength());
        Assert.Equal(0, (await Ok(await host.Anon().GetAsync("/api/market"))).GetProperty("listings").GetArrayLength());

        var payouts = await Ok(await seller.Http.GetAsync("/api/payouts/mine"));
        Assert.Equal(50_000_000, payouts.GetProperty("payouts")[0].GetProperty("lamports").GetInt64());
        Assert.Equal("market_sale", payouts.GetProperty("payouts")[0].GetProperty("reason").GetString());

        var third = await SignInAsync(host);
        var late = Deposit(rpc.Inner, third.Key, treasury.PublicKey.Key, 50_000_000, $"buy:{listingId}");
        await AssertError(await third.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = late }), HttpStatusCode.Conflict, "listing_not_active");
    }

    [Fact]
    public async Task Market_AHalfDoneTrade_IsPaidWithAnError_AndCompleteFinishesIt()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var seller = await SignInAsync(host);
        var buyer = await SignInAsync(host);
        var (islandId, mint) = await MintedIslandAsync(seller);
        var listingId = (await Ok(await seller.Http.PostAsJsonAsync("/api/market/list",
            new { islandId, priceLamports = 20_000_000, signature = EscrowNft(rpc.Inner, seller, treasury, mint) }))).GetProperty("listingId").GetGuid();

        rpc.FailSends = true;
        var pay = Deposit(rpc.Inner, buyer.Key, treasury.PublicKey.Key, 20_000_000, $"buy:{listingId}");
        var half = await Ok(await buyer.Http.PostAsJsonAsync($"/api/market/{listingId}/buy", new { signature = pay }));
        Assert.Equal("paid", half.GetProperty("status").GetString());
        Assert.Contains("nft_transfer", half.GetProperty("error").GetString());
        Assert.Contains("seller_payout", half.GetProperty("error").GetString());
        // still the seller's island in the database until the NFT moves
        Assert.True((await Ok(await seller.Http.GetAsync($"/api/islands/{islandId}"))).GetProperty("isMine").GetBoolean());
        await AssertError(await seller.Http.PostAsync($"/api/market/{listingId}/cancel", null), HttpStatusCode.Conflict, "listing_not_active");

        rpc.FailSends = false;
        var done = await Ok(await buyer.Http.PostAsync($"/api/market/{listingId}/complete", null));
        Assert.Equal("sold", done.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, done.GetProperty("error").ValueKind);
        Assert.True((await Ok(await buyer.Http.GetAsync($"/api/islands/{islandId}"))).GetProperty("isMine").GetBoolean());
        // the failed payout was recorded; exactly one sale payout was sent
        var payouts = (await Ok(await seller.Http.GetAsync("/api/payouts/mine"))).GetProperty("payouts").EnumerateArray().ToList();
        Assert.Single(payouts, p => p.GetProperty("status").GetString() == "sent");
    }

    [Fact]
    public async Task Market_CancelReturnsTheNft_AndUnlocksTheIsland()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var seller = await SignInAsync(host);
        var (islandId, mint) = await MintedIslandAsync(seller);
        var listingId = (await Ok(await seller.Http.PostAsJsonAsync("/api/market/list",
            new { islandId, priceLamports = 20_000_000, signature = EscrowNft(rpc.Inner, seller, treasury, mint) }))).GetProperty("listingId").GetGuid();
        var other = await SignInAsync(host);
        await AssertError(await other.Http.PostAsync($"/api/market/{listingId}/cancel", null), HttpStatusCode.Forbidden, "not_your_listing");

        rpc.FailSends = true;
        var stuck = await Ok(await seller.Http.PostAsync($"/api/market/{listingId}/cancel", null));
        Assert.Equal("cancelling", stuck.GetProperty("status").GetString());
        Assert.Contains("nft_return", stuck.GetProperty("error").GetString());
        rpc.FailSends = false;
        var cancelled = await Ok(await seller.Http.PostAsync($"/api/market/{listingId}/cancel", null));
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.False((await Ok(await seller.Http.GetAsync($"/api/islands/{islandId}"))).GetProperty("listed").GetBoolean());
        await Ok(await seller.Http.PostAsync($"/api/islands/{islandId}/collect", null));
    }
}
