using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>An in-memory chain for tests: transactions, balances and token holdings are set by the test; sends are recorded.</summary>
public sealed class FakeSolanaRpc : ISolanaRpc
{
    public ConcurrentDictionary<string, ChainTransaction> Transactions { get; } = new();
    public ConcurrentDictionary<string, long> Balances { get; } = new();
    public ConcurrentBag<byte[]> Sent { get; } = new();
    public List<TokenHolding> Holdings { get; } = new();

    public Task<long> GetBalanceAsync(string address, CancellationToken ct = default) =>
        Task.FromResult(Balances.TryGetValue(address, out var b) ? b : 0);
    public Task<ChainTransaction?> GetTransactionAsync(string signature, CancellationToken ct = default) =>
        Task.FromResult(Transactions.TryGetValue(signature, out var t) ? t : null);
    public Task<string> GetLatestBlockhashAsync(CancellationToken ct = default) => Task.FromResult("EETubP5AKHgjPAhzPAFcb8BAY1hMH639CWCFTqi3hq1k");
    public Task<string> SendTransactionAsync(byte[] transaction, CancellationToken ct = default)
    {
        Sent.Add(transaction);
        return Task.FromResult(HackathonTests.RandomSignature());
    }
    public Task<string?> GetSignatureStatusAsync(string signature, CancellationToken ct = default) => Task.FromResult<string?>("confirmed");
    public Task<long> GetMinimumBalanceForRentExemptionAsync(int dataLength, CancellationToken ct = default) => Task.FromResult(1461600L);
    public Task<IReadOnlyList<TokenHolding>> GetTokenAccountsByOwnerAsync(string owner, string mint, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TokenHolding>>(Holdings.Where(h => h.Owner == owner && h.Mint == mint).ToList());
    public Task<bool> AccountExistsAsync(string address, CancellationToken ct = default) => Task.FromResult(true);
    public Task<string> RequestAirdropAsync(string address, long lamports, CancellationToken ct = default) => Task.FromResult(HackathonTests.RandomSignature());
}

/// <summary>Hackathon: wallet sign-in, signature checks, deposits, the treasury and the AddHackathon migration.</summary>
public sealed class HackathonTests
{
    public static string RandomSignature() => Encoders.Base58.EncodeData(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray())
        .Concat(Guid.NewGuid().ToByteArray()).Concat(Guid.NewGuid().ToByteArray()).ToArray());

    static string Secret(Account a) => Encoders.Base58.EncodeData(a.PrivateKey.KeyBytes);

    static (ApiHost host, FakeSolanaRpc rpc, Account treasury) NewHost(bool treasuryOn = true)
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        var settings = new Dictionary<string, string?>();
        if (treasuryOn) settings["TREASURY_SECRET_KEY"] = Secret(treasury);
        var host = new ApiHost(settings, s =>
        {
            s.RemoveAll<ISolanaRpc>();
            s.AddSingleton<ISolanaRpc>(rpc);
        });
        return (host, rpc, treasury);
    }

    static string SignBase58(Account wallet, string message) => Encoders.Base58.EncodeData(wallet.Sign(Encoding.UTF8.GetBytes(message)));

    static async Task<JsonElement> SignInAsync(ApiHost host, Account wallet, HttpClient? client = null)
    {
        client ??= host.Anon();
        var nonce = await (await client.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = wallet.PublicKey.Key })).ReadJson();
        var message = nonce.GetProperty("message").GetString()!;
        var res = await client.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = wallet.PublicKey.Key, signature = SignBase58(wallet, message), message });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.ReadJson();
    }

    // ---- signatures and keys ----

    [Fact]
    public void MessageSignature_VerifiesForTheSigner_AndRejectsTamperingOrOtherKeys()
    {
        var wallet = new Account();
        var other = new Account();
        const string msg = "roguepirates.fun wants you to sign in";
        var sig = SignBase58(wallet, msg);

        Assert.True(SolanaService.VerifyMessageSignature(wallet.PublicKey.Key, msg, sig));
        Assert.False(SolanaService.VerifyMessageSignature(wallet.PublicKey.Key, msg + "!", sig));
        Assert.False(SolanaService.VerifyMessageSignature(other.PublicKey.Key, msg, sig));
        Assert.False(SolanaService.VerifyMessageSignature(wallet.PublicKey.Key, msg, "not-base58-0OIl"));
        Assert.False(SolanaService.VerifyMessageSignature("nope", msg, sig));
    }

    [Fact]
    public void Keypair_ParsesBase58AndKeygenJson_AndRejectsGarbage()
    {
        var a = new Account();
        Assert.Equal(a.PublicKey.Key, SolanaService.ParseKeypair(Secret(a)).PublicKey.Key);
        var json = "[" + string.Join(",", a.PrivateKey.KeyBytes.Select(b => (int)b)) + "]";
        Assert.Equal(a.PublicKey.Key, SolanaService.ParseKeypair(json).PublicKey.Key);
        Assert.ThrowsAny<Exception>(() => SolanaService.ParseKeypair("abc"));
        // a 64-byte secret whose second half is someone else's public key
        var b = new Account();
        var mixed = a.PrivateKey.KeyBytes[..32].Concat(b.PublicKey.KeyBytes).ToArray();
        Assert.ThrowsAny<Exception>(() => SolanaService.ParseKeypair(Encoders.Base58.EncodeData(mixed)));
    }

    [Fact]
    public void Addresses_AndSignatures_AreShapeChecked()
    {
        Assert.True(SolanaService.IsValidAddress(new Account().PublicKey.Key));
        Assert.False(SolanaService.IsValidAddress("11111"));
        Assert.True(SolanaService.IsValidSignature(RandomSignature()));
        Assert.False(SolanaService.IsValidSignature(new Account().PublicKey.Key));
    }

    // ---- wallet sign-in ----

    [Fact]
    public async Task WalletSignIn_CreatesPlayerByWallet_ThenFindsTheSamePlayer_AndSessionsWork()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var wallet = new Account();

        var first = await SignInAsync(host, wallet);
        Assert.True(first.GetProperty("isNew").GetBoolean());
        Assert.Equal(wallet.PublicKey.Key, first.GetProperty("wallet").GetString());
        Assert.StartsWith("Captain ", first.GetProperty("name").GetString());
        var token1 = first.GetProperty("token").GetString()!;

        var me = await (await host.As(token1).GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(first.GetProperty("playerId").GetGuid(), me.GetProperty("playerId").GetGuid());
        Assert.Equal(wallet.PublicKey.Key, me.GetProperty("wallet").GetString());

        var second = await SignInAsync(host, wallet);
        Assert.False(second.GetProperty("isNew").GetBoolean());
        Assert.Equal(first.GetProperty("playerId").GetGuid(), second.GetProperty("playerId").GetGuid());
        // both sessions stay valid (two devices)
        Assert.Equal(HttpStatusCode.OK, (await host.As(token1).GetAsync("/api/profiles/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.As(second.GetProperty("token").GetString()!).GetAsync("/api/profiles/me")).StatusCode);

        // the game's normal endpoints work with the wallet session
        var run = await host.As(token1).PostAsJsonAsync("/api/runs", new { kills = 10, runSeconds = 60, level = 3 });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
    }

    [Fact]
    public async Task WalletSignIn_NonceIsOneTime_Expires_AndMustMatch()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var wallet = new Account();
        var anon = host.Anon();
        var key = wallet.PublicKey.Key;

        // replay
        var n1 = await (await anon.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = key })).ReadJson();
        var m1 = n1.GetProperty("message").GetString()!;
        Assert.Contains("Nonce: " + n1.GetProperty("nonce").GetString(), m1);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(wallet, m1), message = m1 })).StatusCode);
        var replay = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(wallet, m1), message = m1 });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("nonce_used", (await replay.ReadJson()).GetProperty("error").GetString());

        // expiry (5 minutes)
        var n2 = await (await anon.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = key })).ReadJson();
        var m2 = n2.GetProperty("message").GetString()!;
        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var expired = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(wallet, m2), message = m2 });
        Assert.Equal("nonce_expired", (await expired.ReadJson()).GetProperty("error").GetString());

        // wrong signer, tampered message, another wallet's nonce
        var n3 = await (await anon.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = key })).ReadJson();
        var m3 = n3.GetProperty("message").GetString()!;
        var bad = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(new Account(), m3), message = m3 });
        Assert.Equal("bad_signature", (await bad.ReadJson()).GetProperty("error").GetString());
        var tampered = m3.Replace("Rogue Pirates", "Evil Pirates");
        var t = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(wallet, tampered), message = tampered });
        Assert.Equal("unknown_nonce", (await t.ReadJson()).GetProperty("error").GetString());
        var thief = new Account();
        var stolen = await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = thief.PublicKey.Key, signature = SignBase58(thief, m3), message = m3 });
        Assert.Equal("unknown_nonce", (await stolen.ReadJson()).GetProperty("error").GetString());
        // the untouched nonce still works once
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = key, signature = SignBase58(wallet, m3), message = m3 })).StatusCode);

        var invalid = await anon.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = "not-a-wallet" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task WalletSignIn_MessageNamesThePageOrigin_WhenAllowed()
    {
        // Phantom refuses a sign-in message whose domain is not the requesting page's (localhost:8090 desktop test)
        var (host, _, _) = NewHost();
        using var _h = host;
        var wallet = new Account();
        var anon = host.Anon();

        async Task<string> MessageFrom(string? origin)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/wallet/nonce") { Content = JsonContent.Create(new { wallet = wallet.PublicKey.Key }) };
            if (origin != null) req.Headers.Add("Origin", origin);
            return (await (await anon.SendAsync(req)).ReadJson()).GetProperty("message").GetString()!;
        }

        var local = await MessageFrom("http://localhost:8090");
        Assert.StartsWith("localhost:8090 wants you to sign in", local);
        Assert.Contains("URI: http://localhost:8090\n", local);
        Assert.StartsWith("www.roguepirates.fun wants you", await MessageFrom("https://www.roguepirates.fun"));
        Assert.StartsWith("roguepirates.fun wants you", await MessageFrom("https://evil.example"));
        Assert.StartsWith("roguepirates.fun wants you", await MessageFrom(null));
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/wallet/verify",
            new { wallet = wallet.PublicKey.Key, signature = SignBase58(wallet, local), message = local })).StatusCode);
    }

    [Fact]
    public async Task WalletSignIn_WithAGuestToken_LinksTheWalletToTheGuestProfile()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var guest = await host.NewPlayerAsync("Anne Bonny");
        var wallet = new Account();
        var res = await SignInAsync(host, wallet, guest.Http);
        Assert.Equal(guest.Id, res.GetProperty("playerId").GetGuid());
        Assert.Equal("Anne Bonny", res.GetProperty("name").GetString());
        Assert.False(res.GetProperty("isNew").GetBoolean());
        // the guest token keeps working
        var me = await (await guest.Http.GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(wallet.PublicKey.Key, me.GetProperty("wallet").GetString());
    }

    [Fact]
    public async Task Email_IsStored_AndValidated()
    {
        var (host, _, _) = NewHost();
        using var _h = host;
        var s = await SignInAsync(host, new Account());
        var c = host.As(s.GetProperty("token").GetString()!);
        var ok = await (await c.PutAsJsonAsync("/api/profile/email", new { email = "Anne@Example.com" })).ReadJson();
        Assert.Equal("anne@example.com", ok.GetProperty("email").GetString());
        Assert.False(ok.GetProperty("emailVerified").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/profile/email", new { email = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anon().PutAsJsonAsync("/api/profile/email", new { email = "a@b.co" })).StatusCode);
    }

    // ---- treasury, balances, deposits ----

    [Fact]
    public async Task Treasury_ShowsAddressAndBalance_OrOffWithoutAKey()
    {
        var (host, rpc, treasury) = NewHost();
        using (host)
        {
            rpc.Balances[treasury.PublicKey.Key] = 2 * SolanaService.LamportsPerSol;
            var t = await (await host.Anon().GetAsync("/api/treasury")).ReadJson();
            Assert.True(t.GetProperty("enabled").GetBoolean());
            Assert.Equal(treasury.PublicKey.Key, t.GetProperty("address").GetString());
            Assert.Equal(2.0, t.GetProperty("sol").GetDouble());
            Assert.DoesNotContain(Secret(treasury), t.ToString());
        }
        var (off, _, _) = NewHost(treasuryOn: false);
        using (off)
        {
            var t = await (await off.Anon().GetAsync("/api/treasury")).ReadJson();
            Assert.False(t.GetProperty("enabled").GetBoolean());
            var s = await SignInAsync(off, new Account());
            var dep = await off.As(s.GetProperty("token").GetString()!).PostAsJsonAsync("/api/deposits/verify", new { signature = RandomSignature(), purpose = "generic" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, dep.StatusCode);
            Assert.Equal("treasury_off", (await dep.ReadJson()).GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task WalletBalance_UsesTheSignedInWallet()
    {
        var (host, rpc, _) = NewHost();
        using var _h = host;
        var wallet = new Account();
        rpc.Balances[wallet.PublicKey.Key] = 1_500_000_000;
        var s = await SignInAsync(host, wallet);
        var b = await (await host.As(s.GetProperty("token").GetString()!).GetAsync("/api/wallet/balance")).ReadJson();
        Assert.Equal(1_500_000_000, b.GetProperty("lamports").GetInt64());
        Assert.Equal(1.5, b.GetProperty("sol").GetDouble());
        var guest = await host.NewPlayerAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.Http.GetAsync("/api/wallet/balance")).StatusCode);
    }

    [Fact]
    public async Task Deposit_IsVerifiedOnce_AndBadTransfersAreRefused()
    {
        var (host, rpc, treasury) = NewHost();
        using var _h = host;
        var wallet = new Account();
        var s = await SignInAsync(host, wallet);
        var c = host.As(s.GetProperty("token").GetString()!);
        var me = wallet.PublicKey.Key;
        var tre = treasury.PublicKey.Key;

        ChainTransaction Tx(string sig, string from, string to, long lamports, bool failed = false, string? signer = null, string? memo = null) =>
            new(sig, 123, failed, new[] { signer ?? from }, new[] { new SolTransfer(from, to, lamports) },
                memo == null ? Array.Empty<string>() : new[] { memo }, null);

        var good = RandomSignature();
        rpc.Transactions[good] = Tx(good, me, tre, 10_000_000, memo: "wager:abc");
        var res = await c.PostAsJsonAsync("/api/deposits/verify", new { signature = good, purpose = "wager", refId = "abc", minLamports = 10_000_000, memo = "wager:abc" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var d = await res.ReadJson();
        Assert.Equal(10_000_000, d.GetProperty("lamports").GetInt64());
        Assert.Equal("verified", d.GetProperty("status").GetString());
        Assert.False(d.GetProperty("alreadyVerified").GetBoolean());

        // same player, same purpose: idempotent; any other use: 409
        var again = await (await c.PostAsJsonAsync("/api/deposits/verify", new { signature = good, purpose = "wager", refId = "abc" })).ReadJson();
        Assert.True(again.GetProperty("alreadyVerified").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/deposits/verify", new { signature = good, purpose = "market_buy" })).StatusCode);
        var other = await SignInAsync(host, new Account());
        var stolen = await host.As(other.GetProperty("token").GetString()!).PostAsJsonAsync("/api/deposits/verify", new { signature = good, purpose = "wager", refId = "abc" });
        Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);

        async Task<string> Error(object body) => (await (await c.PostAsJsonAsync("/api/deposits/verify", body)).ReadJson()).GetProperty("error").GetString()!;

        var unknown = RandomSignature();
        Assert.Equal("tx_not_confirmed", await Error(new { signature = unknown, purpose = "generic" }));
        var failed = RandomSignature();
        rpc.Transactions[failed] = Tx(failed, me, tre, 10_000_000, failed: true);
        Assert.Equal("tx_failed", await Error(new { signature = failed }));
        var elsewhere = RandomSignature();
        rpc.Transactions[elsewhere] = Tx(elsewhere, me, new Account().PublicKey.Key, 10_000_000);
        Assert.Equal("wrong_recipient", await Error(new { signature = elsewhere }));
        var notMine = RandomSignature();
        var stranger = new Account().PublicKey.Key;
        rpc.Transactions[notMine] = Tx(notMine, stranger, tre, 10_000_000);
        Assert.Equal("wrong_sender", await Error(new { signature = notMine }));
        var small = RandomSignature();
        rpc.Transactions[small] = Tx(small, me, tre, 5_000);
        Assert.Equal("amount_too_low", await Error(new { signature = small, minLamports = 10_000_000 }));
        var noMemo = RandomSignature();
        rpc.Transactions[noMemo] = Tx(noMemo, me, tre, 10_000_000);
        Assert.Equal("memo_mismatch", await Error(new { signature = noMemo, memo = "wager:xyz" }));
        Assert.Equal("invalid_signature", await Error(new { signature = "abc" }));

        // a guest profile has no wallet
        var guest = await host.NewPlayerAsync();
        var g = await guest.Http.PostAsJsonAsync("/api/deposits/verify", new { signature = RandomSignature() });
        Assert.Equal("wallet_required", (await g.ReadJson()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task AdminPayout_SendsFromTheTreasury_AndNeedsTheKey()
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        using var host = new ApiHost(new Dictionary<string, string?> { ["TREASURY_SECRET_KEY"] = Secret(treasury), ["ADMIN_KEY"] = "test-admin-key" },
            s => { s.RemoveAll<ISolanaRpc>(); s.AddSingleton<ISolanaRpc>(rpc); });
        var to = new Account().PublicKey.Key;
        var noKey = await host.Anon().PostAsJsonAsync("/api/admin/payouts", new { wallet = to, lamports = 1000 });
        Assert.Equal(HttpStatusCode.Forbidden, noKey.StatusCode);
        var c = host.Anon();
        c.DefaultRequestHeaders.Add("X-Admin-Key", "test-admin-key");
        var p = await (await c.PostAsJsonAsync("/api/admin/payouts", new { wallet = to, lamports = 10_000_000, reason = "smoke" })).ReadJson();
        Assert.Equal("sent", p.GetProperty("status").GetString());
        Assert.True(SolanaService.IsValidSignature(p.GetProperty("signature").GetString()));
        Assert.Single(rpc.Sent);
    }

    [Fact]
    public async Task MintNft_BuildsASignedTransaction()
    {
        var rpc = new FakeSolanaRpc();
        var treasury = new Account();
        var settings = new ApiSettings { TreasurySecretKey = Secret(treasury) };
        var sol = new SolanaService(rpc, settings, NullLogger<SolanaService>.Instance);
        var owner = new Account().PublicKey.Key;
        var nft = await sol.MintNftAsync(owner, "Skull Isle", "RPISLE", "https://www.roguepirates.fun/nft/island/1.json");
        Assert.True(SolanaService.IsValidAddress(nft.Mint));
        var tx = Assert.Single(rpc.Sent);
        Assert.True(tx.Length < 1232, $"transaction is {tx.Length} bytes");
    }

    // ---- migration ----

    /// <summary>A database at the schema before the hackathon (AddRunLogRejection) with a guest player: the app start
    /// adds the hackathon tables and columns, the player and their token keep working.</summary>
    [Fact]
    public async Task AppStart_OnThePreHackathonSchema_AddsTheTables_AndKeepsGuests()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rp-hack-{Guid.NewGuid():N}.db");
        var cs = new SqliteConnectionStringBuilder { DataSource = path, Pooling = true }.ToString();
        try
        {
            using (var db = new SqliteGameDb(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite(cs).Options))
            {
                var before = db.Database.GetMigrations().Single(m => m.EndsWith("_AddRunLogRejection"));
                db.GetService<IMigrator>().Migrate(before);
                using var conn = new SqliteConnection(cs);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO \"Players\" (\"Id\",\"Name\",\"TokenHash\",\"CreatedAt\",\"Rating\",\"Wins\",\"Losses\") " +
                                  "VALUES ('6F1C1F0E-0000-4000-8000-000000000009','OldSalt',$h,'2026-10-10 10:00:00',1010,1,0)";
                cmd.Parameters.AddWithValue("$h", Tokens.Hash("old-guest-token"));
                cmd.ExecuteNonQuery();
            }

            using var host = new ApiHost(new Dictionary<string, string?> { ["SQLITE_PATH"] = path });
            var me = await (await host.As("old-guest-token").GetAsync("/api/profiles/me")).ReadJson();
            Assert.Equal("OldSalt", me.GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, me.GetProperty("wallet").ValueKind);
            using var scope = host.Services.CreateScope();
            var db2 = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Empty(db2.Database.GetPendingMigrations());
            Assert.Contains(db2.Database.GetAppliedMigrations(), m => m.EndsWith("_AddHackathon"));
            // every hackathon table answers a query
            Assert.Equal(0, await db2.WalletNonces.CountAsync());
            Assert.Equal(0, await db2.PlayerSessions.CountAsync());
            Assert.Equal(0, await db2.SolanaDeposits.CountAsync());
            Assert.Equal(0, await db2.Payouts.CountAsync());
            Assert.Equal(0, await db2.Wagers.CountAsync());
            Assert.Equal(0, await db2.DailyRaces.CountAsync());
            Assert.Equal(0, await db2.DailyRaceEntries.CountAsync());
            Assert.Equal(0, await db2.WeeklyRewards.CountAsync());
            Assert.Equal(0, await db2.Islands.CountAsync());
            Assert.Equal(0, await db2.IslandBuildings.CountAsync());
            Assert.Equal(0, await db2.Raids.CountAsync());
            Assert.Equal(0, await db2.MarketListings.CountAsync());
        }
        finally
        {
            using (var c = new SqliteConnection(cs)) SqliteConnection.ClearPool(c);
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(f); } catch { /* best effort */ }
        }
    }
}

/// <summary>Runs only with RP_DEVNET_TESTS=1 (talks to the public devnet RPC).</summary>
public sealed class DevnetFactAttribute : FactAttribute
{
    public DevnetFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RP_DEVNET_TESTS") != "1") Skip = "Set RP_DEVNET_TESTS=1 to run devnet tests.";
    }
}

public sealed class DevnetTests
{
    [DevnetFact]
    public async Task Devnet_RpcAnswers_BalanceAndBlockhash()
    {
        var url = Environment.GetEnvironmentVariable("SOLANA_RPC_URL") ?? "https://api.devnet.solana.com";
        var rpc = new HttpSolanaRpc(new HttpClient(), url);
        var blockhash = await rpc.GetLatestBlockhashAsync();
        Assert.True(SolanaService.IsValidAddress(blockhash));
        var balance = await rpc.GetBalanceAsync(new Account().PublicKey.Key);
        Assert.Equal(0, balance);
    }

    /// <summary>A real devnet faucet transfer parses into a system transfer with its signer (the jsonParsed shape).</summary>
    [DevnetFact]
    public async Task Devnet_FaucetTransfer_Parses()
    {
        var url = Environment.GetEnvironmentVariable("SOLANA_RPC_URL") ?? "https://api.devnet.solana.com";
        using var http = new HttpClient();
        const string faucet = "9B5XszUGdMaxCZ7uSQhPzdks5ZQSmWxrmzCSvtJ6Ns6g";
        var res = await http.PostAsJsonAsync(url, new { jsonrpc = "2.0", id = 1, method = "getSignaturesForAddress", @params = new object[] { faucet, new { limit = 5 } } });
        var sigs = (await res.ReadJson()).GetProperty("result").EnumerateArray()
            .Where(s => s.GetProperty("err").ValueKind == JsonValueKind.Null).Select(s => s.GetProperty("signature").GetString()!).ToList();
        Assert.NotEmpty(sigs);
        var rpc = new HttpSolanaRpc(http, url);
        var tx = await rpc.GetTransactionAsync(sigs[0]);
        Assert.NotNull(tx);
        Assert.False(tx!.Failed);
        Assert.NotEmpty(tx.Signers);
        Assert.Contains(tx.Transfers, t => (t.Source == faucet || t.Destination == faucet) && t.Lamports > 0);
    }
}
