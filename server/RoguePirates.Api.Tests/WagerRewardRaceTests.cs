using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>Hackathon: 1v1 SOL wagers, weekly leaderboard rewards and the daily race, on the fake chain.</summary>
public sealed class WagerRewardRaceTests
{
    const long Stake = 10_000_000; // 0.01 SOL
    const string AdminKey = "test-admin-key-123";

    sealed record Wallet(Account Account, Guid Id, HttpClient Http)
    {
        public string Key => Account.PublicKey.Key;
    }

    sealed class World : IDisposable
    {
        public required ApiHost Host { get; init; }
        public required FakeSolanaRpc Rpc { get; init; }
        public required string Treasury { get; init; }
        public void Dispose() => Host.Dispose();

        /// <summary>A confirmed transfer from the wallet to the treasury on the fake chain; returns its signature.</summary>
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

        public async Task SettleAsync(Guid lobbyId)
        {
            using var scope = Host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WagerService>().SettleLobbyAsync(lobbyId);
        }

        public async Task DecideAsync(Guid matchId, Guid? winner, string reason = "sunk")
        {
            using var scope = Host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MatchService>().DecideByServerAsync(matchId,
                new RelayEnd(winner, reason, true, 120, new(), new()));
        }

        public List<Payout> Payouts()
        {
            using var scope = Host.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<GameDb>().Payouts.OrderBy(p => p.Id).ToList();
        }
    }

    static World NewWorld(bool treasuryOn = true, Dictionary<string, string?>? extra = null)
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        var settings = new Dictionary<string, string?> { ["ADMIN_KEY"] = AdminKey };
        if (treasuryOn) settings["TREASURY_SECRET_KEY"] = Encoders.Base58.EncodeData(treasury.PrivateKey.KeyBytes);
        if (extra != null) foreach (var (k, v) in extra) settings[k] = v;
        var host = new ApiHost(settings, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });
        return new World { Host = host, Rpc = rpc, Treasury = treasury.PublicKey.Key };
    }

    static async Task<string> ErrorOf(HttpResponseMessage res) => (await res.ReadJson()).GetProperty("error").GetString()!;

    static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.ReadJson();
    }

    /// <summary>Host creates a staked lobby, the guest joins, both send their drafts and are ready.</summary>
    static async Task<(Guid lobbyId, JsonElement state)> StakedPairAsync(World w, Wallet host, Wallet guest)
    {
        var created = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = false, stakeLamports = Stake }));
        var lobbyId = created.GetProperty("lobbyId").GetGuid();
        var wager = created.GetProperty("wager");
        Assert.Equal(Stake, wager.GetProperty("stakeLamports").GetInt64());
        Assert.Equal("awaiting_deposits", wager.GetProperty("status").GetString());
        Assert.Equal("wager:" + lobbyId, wager.GetProperty("memo").GetString());
        Assert.Equal(w.Treasury, wager.GetProperty("treasury").GetString());
        await Ok(await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = created.GetProperty("code").GetString() }));
        await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{lobbyId}/status", new { status = "ready", draft = JsonHelpers.Draft("h") }));
        var state = await Ok(await guest.Http.PutAsJsonAsync($"/api/lobbies/{lobbyId}/status", new { status = "ready", draft = JsonHelpers.Draft("g") }));
        return (lobbyId, state);
    }

    // ---------------------------------------------------------------- wagers

    [Fact]
    public async Task Wager_MatchWaitsForBothDeposits_ThenWinnerIsPaidTwiceTheStake_Once()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var guest = await w.SignInAsync();
        var (lobbyId, state) = await StakedPairAsync(w, host, guest);
        Assert.Equal("full", state.GetProperty("status").GetString()); // both ready, but no stakes yet

        // host deposits: still waiting for the guest
        var afterHost = await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = w.Transfer(host, Stake, "wager:" + lobbyId) }));
        Assert.Equal("full", afterHost.GetProperty("status").GetString());
        Assert.True(afterHost.GetProperty("wager").GetProperty("host").GetProperty("deposited").GetBoolean());
        Assert.False(afterHost.GetProperty("wager").GetProperty("guest").GetProperty("deposited").GetBoolean());

        // the stake cannot change once a deposit is in
        Assert.Equal("wager_locked", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}", new { stakeLamports = 2 * Stake })));

        // guest deposits: funded, the match starts
        var started = await Ok(await guest.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = w.Transfer(guest, Stake, "wager:" + lobbyId) }));
        Assert.Equal("starting", started.GetProperty("status").GetString());
        Assert.Equal("funded", started.GetProperty("wager").GetProperty("status").GetString());
        Assert.Equal(2 * Stake, started.GetProperty("wager").GetProperty("potLamports").GetInt64());
        var matchId = started.GetProperty("match").GetProperty("matchId").GetGuid();

        // a deposit after the start is not credited: it is recorded and refunded (deposit_refunded)
        Assert.Equal("deposit_refunded", await ErrorOf(await guest.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = w.Transfer(guest, Stake, "wager:" + lobbyId) })));
        Assert.Single(w.Payouts(), p => p.Reason == WagerService.RefundReason && p.Wallet == guest.Key);

        await w.DecideAsync(matchId, guest.Id);
        await w.SettleAsync(lobbyId);
        await w.SettleAsync(lobbyId); // idempotent

        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{lobbyId}"));
        Assert.Equal("settled", view.GetProperty("status").GetString());
        Assert.Equal("paid_winner", view.GetProperty("result").GetString());
        Assert.Equal(guest.Id, view.GetProperty("winnerPlayerId").GetGuid());
        Assert.NotNull(view.GetProperty("payoutSignature").GetString());

        var wins = w.Payouts().Where(p => p.Reason == WagerService.WinReason).ToList();
        Assert.Single(wins);
        Assert.Equal(guest.Key, wins[0].Wallet);
        Assert.Equal(2 * Stake, wins[0].Lamports);
        Assert.Equal(Payout.Sent, wins[0].Status);
        Assert.Single(w.Payouts(), p => p.Reason == WagerService.RefundReason); // only the late deposit above

        // the lobby JSON carries the result too
        var lobby = await Ok(await host.Http.GetAsync($"/api/lobbies/{lobbyId}"));
        Assert.Equal("settled", lobby.GetProperty("wager").GetProperty("status").GetString());
        var mine = await Ok(await guest.Http.GetAsync("/api/wagers/mine"));
        Assert.Single(mine.GetProperty("wagers").EnumerateArray());
    }

    [Fact]
    public async Task Wager_Draw_RefundsBoth()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var guest = await w.SignInAsync();
        var (lobbyId, _) = await StakedPairAsync(w, host, guest);
        await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = w.Transfer(host, Stake, "wager:" + lobbyId) }));
        var started = await Ok(await guest.Http.PostAsJsonAsync($"/api/wagers/{lobbyId}/deposit", new { signature = w.Transfer(guest, Stake, "wager:" + lobbyId) }));
        await w.DecideAsync(started.GetProperty("match").GetProperty("matchId").GetGuid(), null, "draw");
        await w.SettleAsync(lobbyId);

        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{lobbyId}"));
        Assert.Equal("refunded", view.GetProperty("status").GetString());
        var refunds = w.Payouts().Where(p => p.Reason == WagerService.RefundReason).ToList();
        Assert.Equal(2, refunds.Count);
        Assert.Equal(new[] { guest.Key, host.Key }.OrderBy(x => x), refunds.Select(r => r.Wallet).OrderBy(x => x));
        Assert.All(refunds, r => Assert.Equal(Stake, r.Lamports));
        Assert.DoesNotContain(w.Payouts(), p => p.Reason == WagerService.WinReason);
        Assert.Equal(2, view.GetProperty("payouts").GetArrayLength());
    }

    [Fact]
    public async Task Wager_GuestLeavingBeforeStart_IsRefunded_HostClosing_RefundsHost()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var guest = await w.SignInAsync();
        var created = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var id = created.GetProperty("lobbyId").GetGuid();
        await Ok(await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = created.GetProperty("code").GetString() }));
        await Ok(await guest.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(guest, Stake, "wager:" + id) }));
        await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(host, Stake, "wager:" + id) }));
        Assert.Equal("full", (await Ok(await host.Http.GetAsync($"/api/lobbies/{id}"))).GetProperty("status").GetString()); // nobody ready

        await Ok(await guest.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "left" }));
        await w.SettleAsync(id);
        var view = await Ok(await host.Http.GetAsync($"/api/wagers/{id}"));
        Assert.Equal("awaiting_deposits", view.GetProperty("status").GetString());
        Assert.False(view.GetProperty("guest").GetProperty("deposited").GetBoolean());
        var refunds = w.Payouts().Where(p => p.Reason == WagerService.RefundReason).ToList();
        Assert.Single(refunds);
        Assert.Equal(guest.Key, refunds[0].Wallet);

        // the host closes the lobby: their deposit comes back, the wager is refunded
        await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "left" }));
        await w.SettleAsync(id);
        view = await Ok(await host.Http.GetAsync($"/api/wagers/{id}"));
        Assert.Equal("refunded", view.GetProperty("status").GetString());
        refunds = w.Payouts().Where(p => p.Reason == WagerService.RefundReason).ToList();
        Assert.Equal(2, refunds.Count);
        Assert.Equal(host.Key, refunds[1].Wallet);
        await w.SettleAsync(id);
        Assert.Equal(2, w.Payouts().Count(p => p.Reason == WagerService.RefundReason));
    }

    /// <summary>13 Oct hotfix: a bot match is decided by the host's client, so stakes against the practice bot (the house
    /// matching the stake from the treasury) are off: seating the bot in a staked lobby, or staking a lobby with the bot,
    /// is refused with bot_stakes_off; a bot match without a stake works as before.</summary>
    [Fact]
    public async Task Wager_VsBot_StakesAreRefused_FreePlayStillWorks()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();

        var staked = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var id = staked.GetProperty("lobbyId").GetGuid();
        await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft() }));
        var res = await host.Http.PostAsync($"/api/lobbies/{id}/bot", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("bot_stakes_off", await ErrorOf(res));

        var free = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { }));
        var freeId = free.GetProperty("lobbyId").GetGuid();
        Assert.Equal("full", (await Ok(await host.Http.PostAsync($"/api/lobbies/{freeId}/bot", null))).GetProperty("status").GetString());
        Assert.Equal("bot_stakes_off", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{freeId}", new { stakeLamports = Stake })));
        var started = await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{freeId}/status", new { status = "ready", draft = JsonHelpers.Draft() }));
        Assert.Equal("starting", started.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, started.GetProperty("wager").ValueKind);
        Assert.Empty(w.Payouts());
    }

    /// <summary>13 Oct hotfix: a vs-house wager funded before the hotfix is refunded whatever the result (the house never
    /// pays a client-decided bot match).</summary>
    [Fact]
    public async Task Wager_LegacyVsHouse_IsRefunded_EvenWhenTheHostWins()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var created = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { }));
        var id = created.GetProperty("lobbyId").GetGuid();
        await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "ready", draft = JsonHelpers.Draft() }));
        var started = await Ok(await host.Http.PostAsync($"/api/lobbies/{id}/bot", null));
        var matchId = started.GetProperty("match").GetProperty("matchId").GetGuid();
        var sig = w.Transfer(host, Stake, "wager:" + id);
        using (var scope = w.Host.Services.CreateScope())
        {
            // the state a staked bot match was left in before the hotfix: host deposit attached, the house "matching" it
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var wager = WagerService.NewForLobby(id, host.Id, Stake, w.Host.Clock.UtcNow);
            wager.Status = Wager.Funded;
            wager.VsHouse = true;
            wager.MatchId = matchId;
            wager.HostDepositSig = sig;
            db.Wagers.Add(wager);
            db.SolanaDeposits.Add(new SolanaDeposit
            {
                Signature = sig, PlayerId = host.Id, Wallet = host.Key, Purpose = WagerService.DepositPurpose, RefId = wager.Id.ToString(),
                Lamports = Stake, Memo = "wager:" + id, Status = SolanaDeposit.Consumed, CreatedAt = w.Host.Clock.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await w.DecideAsync(matchId, host.Id);
        await w.SettleAsync(id);
        Assert.DoesNotContain(w.Payouts(), p => p.Reason == WagerService.WinReason);
        var refund = Assert.Single(w.Payouts(), p => p.Reason == WagerService.RefundReason);
        Assert.Equal(Stake, refund.Lamports);
    }

    [Fact]
    public async Task Wager_LobbyClosedWhileTheGuestSigned_DepositIsRecordedAndRefundedOnce_ReplayRefundsNothing()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        var guest = await w.SignInAsync();
        var created = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var id = created.GetProperty("lobbyId").GetGuid();
        await Ok(await guest.Http.PostAsJsonAsync("/api/lobbies/join", new { code = created.GetProperty("code").GetString() }));

        // the guest's wallet is signing; meanwhile the host closes the lobby
        var sig = w.Transfer(guest, Stake, "wager:" + id);
        await Ok(await host.Http.PutAsJsonAsync($"/api/lobbies/{id}/status", new { status = "left" }));

        var res = await guest.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.ReadJson();
        Assert.Equal("deposit_refunded", body.GetProperty("error").GetString());
        Assert.Contains("refunded", body.GetProperty("message").GetString());
        var refunds = w.Payouts().Where(p => p.Reason == WagerService.RefundReason).ToList();
        Assert.Single(refunds);
        Assert.Equal(guest.Key, refunds[0].Wallet);
        Assert.Equal(Stake, refunds[0].Lamports);
        Assert.Equal(Payout.Sent, refunds[0].Status);

        // the same signature again (a client retry): no second refund
        Assert.Equal("deposit_refunded", await ErrorOf(await guest.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig })));
        await w.SettleAsync(id);
        Assert.Single(w.Payouts(), p => p.Reason == WagerService.RefundReason);
        var mine = await Ok(await guest.Http.GetAsync("/api/deposits/mine"));
        Assert.Equal("refunded", mine.GetProperty("deposits")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Wager_Validation()
    {
        using var w = NewWorld();
        var host = await w.SignInAsync();
        Assert.Equal("invalid_stake", await ErrorOf(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = 600_000_000 })));
        Assert.Equal("invalid_stake", await ErrorOf(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = 5 })));
        var guestProfile = await w.Host.NewPlayerAsync("Guesty");
        Assert.Equal("wallet_required", await ErrorOf(await guestProfile.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake })));

        // a lobby without a stake still works as before (no wager in the JSON) and a stake can be added later
        var plain = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { @public = true }));
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("wager").ValueKind);
        var id = plain.GetProperty("lobbyId").GetGuid();
        Assert.Equal("no_wager", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(host, Stake, "wager:" + id) })));
        var staked = await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{id}", new { stakeLamports = Stake }));
        Assert.Equal(Stake, staked.GetProperty("wager").GetProperty("stakeLamports").GetInt64());
        var listed = await Ok(await w.Host.Anon().GetAsync("/api/lobbies/public"));
        Assert.Equal(Stake, listed.GetProperty("lobbies").EnumerateArray().Single(l => l.GetProperty("lobbyId").GetGuid() == id).GetProperty("stakeLamports").GetInt64());

        // V5 M9h: a wrong memo or a short transfer is recorded and refunded (409 deposit_refunded), no longer left in the treasury
        Assert.Equal("deposit_refunded", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(host, Stake, "wager:other") })));
        Assert.Equal("deposit_refunded", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(host, Stake - 1, "wager:" + id) })));
        Assert.Equal(2, w.Payouts().Count(p => p.Reason == WagerService.RefundReason && p.Wallet == host.Key));
        var stranger = await w.SignInAsync();
        Assert.Equal("deposit_refunded", await ErrorOf(await stranger.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(stranger, Stake, "wager:" + id) })));
        Assert.Single(w.Payouts(), p => p.Reason == WagerService.RefundReason && p.Wallet == stranger.Key);

        // the same signature again is idempotent
        var sig = w.Transfer(host, Stake, "wager:" + id);
        await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig }));
        await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = sig }));
        // a second, different transfer for an already paid seat is refunded, not kept
        Assert.Equal("deposit_refunded", await ErrorOf(await host.Http.PostAsJsonAsync($"/api/wagers/{id}/deposit", new { signature = w.Transfer(host, Stake, "wager:" + id) })));
        Assert.Equal(3, w.Payouts().Count(p => p.Reason == WagerService.RefundReason && p.Wallet == host.Key));

        // removing the stake is refused after a deposit; with 0 on a fresh lobby it cancels
        var other = await Ok(await host.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake }));
        var cleared = await Ok(await host.Http.PostAsJsonAsync($"/api/wagers/{other.GetProperty("lobbyId").GetGuid()}", new { stakeLamports = 0 }));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("wager").ValueKind);

        using var off = NewWorld(treasuryOn: false);
        var p = await off.SignInAsync();
        var res = await p.Http.PostAsJsonAsync("/api/lobbies", new { stakeLamports = Stake });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal("treasury_off", await ErrorOf(res));
    }

    // ---------------------------------------------------------------- weekly rewards

    [Fact]
    public async Task WeeklyRewards_PayTopWalletPlayersOnce_AndShowOnTheBoard()
    {
        // runs without logs are not server-checked: pay on reported totals here (the default is checked-only since 13 Oct)
        using var w = NewWorld(extra: new() { ["WEEKLY_REWARD_CHECKED_ONLY"] = "false" });
        var a = await w.SignInAsync();
        var b = await w.SignInAsync();
        var c = await w.SignInAsync();
        var d = await w.SignInAsync();
        var guest = await w.Host.NewPlayerAsync("NoWallet");
        async Task Run(HttpClient http, int kills) =>
            await Ok(await http.PostAsJsonAsync("/api/runs", new { kills, runSeconds = 600, level = 10 }));
        await Run(guest.Http, 999); // the top, but not eligible (no wallet)
        await Run(a.Http, 300);
        await Run(b.Http, 200);
        await Run(c.Http, 100);
        await Run(d.Http, 50);
        var week = WeekMath.WeekStart(w.Host.Clock.UtcNow);
        w.Host.Clock.Advance(TimeSpan.FromDays(7));

        var admin = w.Host.Anon();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Host.Anon().PostAsJsonAsync("/api/admin/rewards/weekly", new { })).StatusCode);

        var dry = await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { dryRun = true }));
        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        var dryWinners = dry.GetProperty("winners").EnumerateArray().ToList();
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, dryWinners.Select(x => x.GetProperty("playerId").GetGuid()));
        Assert.Empty(w.Payouts());

        var paid = await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { }));
        var winners = paid.GetProperty("winners").EnumerateArray().ToList();
        Assert.Equal(3, winners.Count);
        Assert.Equal(new[] { 500_000_000L, 300_000_000L, 200_000_000L }, winners.Select(x => x.GetProperty("lamports").GetInt64()));
        Assert.All(winners, x => Assert.Equal("sent", x.GetProperty("payout").GetProperty("status").GetString()));
        Assert.Equal(300, winners[0].GetProperty("kills").GetInt32());
        Assert.Equal(3, w.Payouts().Count);

        // again: nothing new is paid
        await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week.ToString("yyyy-MM-dd") }));
        Assert.Equal(3, w.Payouts().Count);
        using (var scope = w.Host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RewardService>().AutoPayAsync();
        Assert.Equal(3, w.Payouts().Count);

        var pub = await Ok(await w.Host.Anon().GetAsync("/api/rewards/weekly"));
        Assert.Equal(3, pub.GetProperty("winners").GetArrayLength());
        Assert.NotNull(pub.GetProperty("winners")[0].GetProperty("payout").GetProperty("signature").GetString());

        var board = await Ok(await w.Host.Anon().GetAsync("/api/leaderboard/weekly/previous"));
        var entries = board.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(JsonValueKind.Null, entries[0].GetProperty("wallet").ValueKind); // the guest
        Assert.Equal(JsonValueKind.Null, entries[0].GetProperty("reward").ValueKind);
        Assert.Equal(PayoutOnce.Short(a.Key), entries[1].GetProperty("wallet").GetString());
        Assert.Equal(500_000_000, entries[1].GetProperty("reward").GetProperty("lamports").GetInt64());
        Assert.Equal(JsonValueKind.Null, entries[4].GetProperty("reward").ValueKind); // 4th wallet player: no prize

        // the current week cannot be dry-run into the future
        Assert.Equal("future_week", await ErrorOf(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { weekStart = week.AddDays(14).ToString("yyyy-MM-dd"), dryRun = true })));
    }

    [Fact]
    public async Task WeeklyRewards_TableAndCheckedOnly_AreConfigurable()
    {
        using var w = NewWorld(extra: new() { ["WEEKLY_REWARD_SOL"] = "0.25", ["WEEKLY_REWARD_CHECKED_ONLY"] = "true" });
        var a = await w.SignInAsync();
        await Ok(await a.Http.PostAsJsonAsync("/api/runs", new { kills = 10, runSeconds = 60, level = 2 })); // no log: not checked
        w.Host.Clock.Advance(TimeSpan.FromDays(7));
        var admin = w.Host.Anon();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        var dry = await Ok(await admin.PostAsJsonAsync("/api/admin/rewards/weekly", new { dryRun = true }));
        Assert.Equal(0, dry.GetProperty("winners").GetArrayLength());
        Assert.Equal(250_000_000, dry.GetProperty("table")[0].GetProperty("lamports").GetInt64());
        Assert.Equal(1, dry.GetProperty("table").GetArrayLength());
    }

    // ---------------------------------------------------------------- daily race

    [Fact]
    public async Task DailyRace_SeedFromDate_BestTimePerPlayer_Board()
    {
        using var w = NewWorld(treasuryOn: false);
        var today = w.Host.Clock.UtcNow.Date;
        var race = await Ok(await w.Host.Anon().GetAsync("/api/race/daily"));
        var seed = race.GetProperty("race").GetProperty("seed").GetInt32();
        Assert.Equal(DailyRaceService.SeedFor(today, DailyRaceService.DefaultSalt), seed);
        Assert.NotEqual(DailyRaceService.SeedFor(today.AddDays(1), DailyRaceService.DefaultSalt), seed);
        Assert.Equal(today.ToString("yyyy-MM-dd"), race.GetProperty("race").GetProperty("date").GetString());
        Assert.Equal(100, race.GetProperty("race").GetProperty("targetKills").GetInt32());
        // the same seed on a second call (stored)
        Assert.Equal(seed, (await Ok(await w.Host.Anon().GetAsync("/api/race/daily"))).GetProperty("race").GetProperty("seed").GetInt32());

        var p1 = await w.Host.NewPlayerAsync("Racer");
        var p2 = await w.SignInAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await w.Host.Anon().PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 100, kills = 100 })).StatusCode);
        Assert.Equal("seed_mismatch", await ErrorOf(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed = seed + 1, timeSeconds = 100, kills = 100 })));
        Assert.Equal("target_not_reached", await ErrorOf(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 100, kills = 99 })));
        Assert.Equal("implausible_time", await ErrorOf(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 2, kills = 100 })));
        Assert.Equal("race_not_open", await ErrorOf(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { date = today.AddDays(1).ToString("yyyy-MM-dd"), seed, timeSeconds = 100, kills = 100 })));

        var first = await Ok(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 150.25, kills = 100 }));
        Assert.True(first.GetProperty("improved").GetBoolean());
        Assert.Equal(1, first.GetProperty("best").GetProperty("rank").GetInt32());
        var slower = await Ok(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 200, kills = 100 }));
        Assert.False(slower.GetProperty("improved").GetBoolean());
        Assert.Equal(150.25, slower.GetProperty("best").GetProperty("timeSeconds").GetDouble());
        Assert.Equal(2, slower.GetProperty("best").GetProperty("attempts").GetInt32());
        await Ok(await p2.Http.PostAsJsonAsync("/api/race/daily/entry", new { date = today.ToString("yyyy-MM-dd"), seed, timeSeconds = 120, kills = 104 }));
        var faster = await Ok(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 110, kills = 100 }));
        Assert.True(faster.GetProperty("improved").GetBoolean());
        Assert.Equal(1, faster.GetProperty("best").GetProperty("rank").GetInt32());

        var board = await Ok(await p2.Http.GetAsync("/api/race/daily/board"));
        var entries = board.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(new[] { p1.Id, p2.Id }, entries.Select(e => e.GetProperty("playerId").GetGuid()));
        Assert.Equal(110, entries[0].GetProperty("timeSeconds").GetDouble());
        Assert.Equal(PayoutOnce.Short(p2.Key), entries[1].GetProperty("wallet").GetString());
        Assert.Equal(2, board.GetProperty("me").GetProperty("rank").GetInt32());

        // a malformed run log is accepted in shadow mode, with a log_rejected verdict
        var withLog = await Ok(await p2.Http.PostAsJsonAsync("/api/race/daily/entry", new { seed, timeSeconds = 100, kills = 100, log = new { format = "nope" } }));
        Assert.Equal("log_rejected", withLog.GetProperty("verdict").GetString());
        Assert.True(withLog.GetProperty("improved").GetBoolean());

        // after midnight (and the grace) yesterday's race is closed; the new day has a new seed
        w.Host.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("race_closed", await ErrorOf(await p1.Http.PostAsJsonAsync("/api/race/daily/entry", new { date = today.ToString("yyyy-MM-dd"), seed, timeSeconds = 90, kills = 100 })));
        var next = await Ok(await w.Host.Anon().GetAsync("/api/race/daily"));
        Assert.NotEqual(seed, next.GetProperty("race").GetProperty("seed").GetInt32());
        Assert.Equal(0, next.GetProperty("entries").GetInt32());
        var old = await Ok(await w.Host.Anon().GetAsync($"/api/race/daily/board?date={today:yyyy-MM-dd}"));
        Assert.Equal(2, old.GetProperty("total").GetInt32());
    }
}
