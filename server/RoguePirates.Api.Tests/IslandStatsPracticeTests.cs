using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoguePirates.Api.Solana;
using Xunit;
using static RoguePirates.Api.Tests.IslandTests;

namespace RoguePirates.Api.Tests;

/// <summary>Hackathon (owner, 12 Oct): practice raids on your own island, the island's defence record (shown on the
/// world map, island pages, market listings and the NFT metadata) and renaming a wallet profile.</summary>
public sealed class IslandStatsPracticeTests
{
    static JsonElement Trait(JsonElement meta, string name)
    {
        foreach (var a in meta.GetProperty("attributes").EnumerateArray())
            if (a.GetProperty("trait_type").GetString() == name) return a.GetProperty("value");
        throw new Xunit.Sdk.XunitException("no trait " + name);
    }

    [Fact]
    public async Task Practice_OnYourOwnIsland_UsesTheSavedDesign_AndChargesAndRecordsNothing()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var other = await SignInAsync(host);
        var id = (await ClaimAsync(owner)).GetProperty("islandId").GetGuid();
        host.Clock.Advance(TimeSpan.FromHours(4));

        // owner has 0 RUM: a practice raid still starts (no fee)
        Assert.Equal(0, await RumAsync(owner));
        var p = await Ok(await owner.Http.PostAsJsonAsync("/api/raids/practice", new { islandId = id }));
        Assert.True(p.GetProperty("practice").GetBoolean());
        Assert.Equal(JsonValueKind.Null, p.GetProperty("raidId").ValueKind);
        Assert.Equal(0, p.GetProperty("feePaid").GetInt64());
        Assert.Equal(0, p.GetProperty("loot").GetProperty("ifWon").GetInt64());
        var defence = p.GetProperty("defence");
        Assert.NotEqual(0, defence.GetProperty("seed").GetInt32());
        Assert.Equal(15, defence.GetProperty("design").GetProperty("windows").GetArrayLength());
        Assert.True(defence.GetProperty("design").GetProperty("guardian").GetProperty("minute").GetInt32() > 0);
        Assert.Equal(0, await RumAsync(owner));

        // nothing recorded: no history, no raid in progress, the island is still raidable by others
        var history = await Ok(await owner.Http.GetAsync("/api/raids/mine"));
        Assert.Equal(0, history.GetProperty("attacks").GetArrayLength());
        Assert.Equal(0, history.GetProperty("defences").GetArrayLength());
        var island = await Ok(await other.Http.GetAsync($"/api/islands/{id}"));
        Assert.False(island.GetProperty("raidInProgress").GetBoolean());
        Assert.True(island.GetProperty("raidable").GetBoolean());
        Assert.Equal(0, island.GetProperty("defenceRecord").GetProperty("raidsAgainst").GetInt32());

        // only the owner can practise; a real raid on your own island is still refused
        await AssertError(await other.Http.PostAsJsonAsync("/api/raids/practice", new { islandId = id }), HttpStatusCode.Forbidden, "not_your_island");
        await AssertError(await owner.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }), HttpStatusCode.BadRequest, "own_island");
        await AssertError(await owner.Http.PostAsJsonAsync("/api/raids/practice", new { islandId = Guid.NewGuid() }), HttpStatusCode.NotFound, "island_not_found");
    }

    [Fact]
    public async Task DefenceRecord_IsNoRaidsYet_ThenCountsHeldRaids_OnTheMapIslandMarketAndMetadata()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var owner = await SignInAsync(host);
        var raider = await SignInAsync(host);
        var island0 = await ClaimAsync(owner);
        var id = island0.GetProperty("islandId").GetGuid();
        var mint = island0.GetProperty("nft").GetProperty("mint").GetString()!;

        var rec = island0.GetProperty("defenceRecord");
        Assert.Equal(0, rec.GetProperty("raidsAgainst").GetInt32());
        Assert.Equal(JsonValueKind.Null, rec.GetProperty("successPercent").ValueKind);
        Assert.Equal("No raids yet", rec.GetProperty("label").GetString());
        var meta0 = await Ok(await host.Anon().GetAsync($"/api/islands/{id}/metadata.json"));
        Assert.Equal("No raids yet", Trait(meta0, "Defence Success").GetString());
        Assert.Equal(120, Trait(meta0, "Rum Per Hour").GetInt32());

        // raid 1: the raider loses (the island held)
        await EarnRumAsync(raider, 1000);
        var r1 = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        host.Clock.Advance(TimeSpan.FromSeconds(30));
        await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r1}/result", new { won = false, seconds = 20, guardianSunk = false, minuteReached = 1 }));
        // a started raid does not count yet
        var r2 = (await Ok(await raider.Http.PostAsJsonAsync("/api/raids/start", new { islandId = id }))).GetProperty("raidId").GetGuid();
        var mid = (await Ok(await owner.Http.GetAsync($"/api/islands/{id}"))).GetProperty("defenceRecord");
        Assert.Equal(1, mid.GetProperty("raidsAgainst").GetInt32());
        Assert.Equal(100, mid.GetProperty("successPercent").GetInt32());
        // raid 2: the raider sinks the guardian (the island fell)
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        await Ok(await raider.Http.PostAsJsonAsync($"/api/raids/{r2}/result", new { won = true, seconds = 590, guardianSunk = true, minuteReached = 10 }));

        var after = (await Ok(await host.Anon().GetAsync($"/api/islands/{id}"))).GetProperty("defenceRecord");
        Assert.Equal(2, after.GetProperty("raidsAgainst").GetInt32());
        Assert.Equal(1, after.GetProperty("defended").GetInt32());
        Assert.Equal(1, after.GetProperty("fallen").GetInt32());
        Assert.Equal(50, after.GetProperty("successPercent").GetInt32());
        Assert.Equal("50% (1 of 2 raids defended)", after.GetProperty("label").GetString());

        var world = await Ok(await host.Anon().GetAsync("/api/islands"));
        Assert.Equal(50, world.GetProperty("islands")[0].GetProperty("defenceRecord").GetProperty("successPercent").GetInt32());

        var meta = await Ok(await host.Anon().GetAsync($"/api/islands/{id}/metadata.json"));
        Assert.Equal("50%", Trait(meta, "Defence Success").GetString());
        Assert.Equal("1 of 2", Trait(meta, "Raids Defended").GetString());

        // the market listing shows RUM per hour and the record
        var sig = HackathonTests.RandomSignature();
        rpc.Inner.Transactions[sig] = new ChainTransaction(sig, 100, false, new[] { owner.Key }, Array.Empty<SolTransfer>(), Array.Empty<string>(), null);
        rpc.Inner.Holdings.Add(new TokenHolding(new Solnet.Wallet.Account().PublicKey.Key, mint, treasury.PublicKey.Key, 1));
        await Ok(await owner.Http.PostAsJsonAsync("/api/market/list", new { islandId = id, priceLamports = 50_000_000, signature = sig }));
        var listing = (await Ok(await host.Anon().GetAsync("/api/market"))).GetProperty("listings")[0].GetProperty("island");
        Assert.Equal(120, listing.GetProperty("ratePerHour").GetInt32());
        Assert.Equal(50, listing.GetProperty("defenceRecord").GetProperty("successPercent").GetInt32());
        Assert.Equal(2, listing.GetProperty("defenceRecord").GetProperty("raidsAgainst").GetInt32());
    }

    [Fact]
    public async Task WalletProfile_StartsWithTheDefaultName_AndCanBeRenamed_WithTheNameRules()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var w = await SignInAsync(host);
        var me = await Ok(await w.Http.GetAsync("/api/profiles/me"));
        Assert.StartsWith("Captain ", me.GetProperty("name").GetString());

        var renamed = await Ok(await w.Http.PatchAsJsonAsync("/api/profiles/me", new { name = "  Red Beard  " }));
        Assert.Equal("Red Beard", renamed.GetProperty("name").GetString());
        await AssertError(await w.Http.PatchAsJsonAsync("/api/profiles/me", new { name = "x" }), HttpStatusCode.BadRequest, "invalid_name");
        await AssertError(await w.Http.PatchAsJsonAsync("/api/profiles/me", new { name = "Captain Fuck" }), HttpStatusCode.BadRequest, "name_not_allowed");
        Assert.Equal("Red Beard", (await Ok(await w.Http.GetAsync("/api/profiles/me"))).GetProperty("name").GetString());
    }
}
