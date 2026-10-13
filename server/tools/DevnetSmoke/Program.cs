// Rogue Pirates hackathon: devnet end-to-end smoke test against a running API.
//
//   dotnet run --project server/tools/DevnetSmoke -- [--api URL] [--rpc URL] [--keys DIR] [--out FILE] [--skip-weekly-pay]
//
// Two wallets: player A = <keys>/testwallet.json, player B = <keys>/walletb.json (generated and funded with 2 SOL from A
// on the first run). The owner key is <keys>/admin.key (or env RP_ADMIN_KEY). Keys are read at runtime and never printed.
// Defaults: --api env RP_API_URL or https://api-production-dad8.up.railway.app, --rpc env SOLANA_RPC_URL or the public
// devnet node, --keys env RP_DEVNET_DIR or %USERPROFILE%/.rp-devnet. Writes a Markdown report (only public data:
// addresses, ids and transaction signatures) to --out when given. Exit code 0 when every step passed.

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Solnet.Programs;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;

namespace DevnetSmoke;

static class Program
{
    const long Sol = 1_000_000_000;

    static async Task<int> Main(string[] args)
    {
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var api = (Arg("--api") ?? Environment.GetEnvironmentVariable("RP_API_URL") ?? "https://api-production-dad8.up.railway.app").TrimEnd('/');
        var rpcUrl = Arg("--rpc") ?? Environment.GetEnvironmentVariable("SOLANA_RPC_URL") ?? "https://api.devnet.solana.com";
        var keys = Arg("--keys") ?? Environment.GetEnvironmentVariable("RP_DEVNET_DIR")
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rp-devnet");
        var outFile = Arg("--out");
        var skipWeeklyPay = args.Contains("--skip-weekly-pay");

        var s = new Smoke(api, rpcUrl, keys, skipWeeklyPay);
        try { await s.RunAsync(); }
        catch (Exception ex) { s.Fatal(ex); }
        var report = s.Report();
        Console.WriteLine();
        Console.WriteLine(report);
        if (outFile != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
            await File.WriteAllTextAsync(outFile, report);
            Console.WriteLine($"Report written to {outFile}");
        }
        return s.AllPassed ? 0 : 1;
    }
}

sealed class StepResult
{
    public string Name = "";
    public bool Pass = true;
    public List<string> Notes = new();
    public List<(string Label, string Sig)> Txs = new();
    public string? Error;
}

sealed class Player
{
    public string Label = "";
    public Account Key = null!;
    public string Wallet => Key.PublicKey.Key;
    public string? Token;
    public string? PlayerId;
    public string? IslandId;
    public string? IslandMint;
}

sealed class Smoke
{
    const long Sol = 1_000_000_000;
    readonly string _api, _rpcUrl, _keys;
    readonly bool _skipWeeklyPay;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    readonly List<StepResult> _steps = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    string _adminKey = "";
    string _treasury = "";
    readonly Player _a = new() { Label = "A" }, _b = new() { Label = "B" };
    readonly Dictionary<string, long> _startBalances = new(), _endBalances = new();
    StepResult _cur = new();
    int _rpcId;

    public Smoke(string api, string rpcUrl, string keys, bool skipWeeklyPay)
    {
        _api = api; _rpcUrl = rpcUrl; _keys = keys; _skipWeeklyPay = skipWeeklyPay;
    }

    public bool AllPassed => _steps.Count > 0 && _steps.All(s => s.Pass);

    static string Explorer(string sig) => $"https://explorer.solana.com/tx/{sig}?cluster=devnet";
    static string ExplorerAddr(string a) => $"https://explorer.solana.com/address/{a}?cluster=devnet";
    static string SolStr(long lamports) => (lamports / (double)Sol).ToString("0.######", CultureInfo.InvariantCulture);

    void Log(string msg) => Console.WriteLine($"[{_clock.Elapsed:mm\\:ss}] {msg}");
    void Note(string msg) { _cur.Notes.Add(msg); Log("  " + msg); }
    void Tx(string label, string? sig)
    {
        if (string.IsNullOrEmpty(sig)) return;
        _cur.Txs.Add((label, sig));
        Log($"  tx {label}: {Explorer(sig)}");
    }
    void Check(bool ok, string what)
    {
        Note((ok ? "ok: " : "FAILED: ") + what);
        if (!ok) _cur.Pass = false;
    }

    async Task Step(string name, Func<Task> body)
    {
        _cur = new StepResult { Name = name };
        _steps.Add(_cur);
        Log("== " + name);
        try { await body(); }
        catch (Exception ex)
        {
            _cur.Pass = false;
            _cur.Error = ex is SmokeException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            Log("  ERROR " + _cur.Error);
        }
        Log(_cur.Pass ? "  PASS" : "  FAIL");
    }

    public void Fatal(Exception ex)
    {
        _steps.Add(new StepResult { Name = "Setup", Pass = false, Error = $"{ex.GetType().Name}: {ex.Message}" });
        Log("FATAL " + ex.Message);
    }

    // ================= the run =================

    public async Task RunAsync()
    {
        await Step("0. Setup: keys, treasury, fund wallet B", SetupAsync);
        if (!_steps[0].Pass) return;
        await Step("1. Wallet sign-in (A and B), profile email for A", SignInAsync);
        if (_a.Token == null || _b.Token == null) return;
        await Step("2. Islands: claim + NFT on chain, collect, RUM from a voyage, build, world list", IslandsAsync);
        await Step("3. Raid: A raids B's island with a 0.01 SOL wager, won -> loot, shield, 2x payout", RaidAsync);
        await Step("4. Market: B lists island NFT, A buys; A lists and cancels", MarketAsync);
        await Step("5. 1v1 wager vs the house: stake, deposit, funded, close lobby -> refund", WagerAsync);
        await Step("6. Weekly kill rewards: dry run, current week refused, real run (previous week), public view", WeeklyAsync);
        await Step("7. Daily island race: race, entry, board", RaceAsync);
        await Step("8. Final balances", FinalAsync);
    }

    async Task SetupAsync()
    {
        _a.Key = LoadKeypair(Path.Combine(_keys, "testwallet.json"));
        var bPath = Path.Combine(_keys, "walletb.json");
        if (File.Exists(bPath)) { _b.Key = LoadKeypair(bPath); Note("wallet B loaded from walletb.json"); }
        else
        {
            var acc = new Account();
            await File.WriteAllTextAsync(bPath, "[" + string.Join(",", acc.PrivateKey.KeyBytes.Select(b => b.ToString())) + "]");
            _b.Key = LoadKeypair(bPath);
            Note("wallet B generated and saved to walletb.json");
        }
        var adminPath = Path.Combine(_keys, "admin.key");
        _adminKey = Environment.GetEnvironmentVariable("RP_ADMIN_KEY") ?? (File.Exists(adminPath) ? (await File.ReadAllTextAsync(adminPath)).Trim() : "");
        Check(_adminKey.Length > 0, "admin key available (value not shown)");
        Note($"A = {_a.Wallet}");
        Note($"B = {_b.Wallet}");

        var health = await Api("GET", "/health");
        Check(health.Status == 200 && health.Body?["ok"]?.GetValue<bool>() == true, $"API health ({_api}): {health.Body?.ToJsonString()}");
        var t = await Api("GET", "/api/treasury");
        _treasury = t.Body?["address"]?.GetValue<string>() ?? throw new SmokeException("treasury off: " + t.Raw);
        Check(t.Body?["cluster"]?.GetValue<string>() == "devnet", $"treasury {_treasury} on devnet, {t.Body?["sol"]} SOL");

        foreach (var (k, w) in new[] { ("A", _a.Wallet), ("B", _b.Wallet), ("treasury", _treasury) })
            _startBalances[k] = await BalanceAsync(w);
        Note($"start balances: A {SolStr(_startBalances["A"])}, B {SolStr(_startBalances["B"])}, treasury {SolStr(_startBalances["treasury"])} SOL");
        if (_startBalances["B"] < 1 * Sol)
        {
            var sig = await SendSolAsync(_a.Key, _b.Wallet, 2 * Sol, "rp-smoke:fund-b");
            Tx("fund B with 2 SOL from A", sig);
            var nb = await BalanceAsync(_b.Wallet);
            Check(nb >= _startBalances["B"] + 2 * Sol, $"B funded: {SolStr(nb)} SOL");
            _startBalances["A"] = await BalanceAsync(_a.Wallet);
            _startBalances["B"] = nb;
        }
        else Note("B already holds at least 1 SOL; not funded again");
    }

    // ---------- 1 ----------

    async Task SignInAsync()
    {
        foreach (var p in new[] { _a, _b })
        {
            var n = await Api("POST", "/api/auth/wallet/nonce", new { wallet = p.Wallet });
            Expect(n, 200, $"nonce for {p.Label}");
            var message = n.Body!["message"]!.GetValue<string>();
            var sig = Encoders.Base58.EncodeData(p.Key.Sign(Encoding.UTF8.GetBytes(message)));
            var v = await Api("POST", "/api/auth/wallet/verify", new { wallet = p.Wallet, signature = sig, message, name = (string?)null });
            Expect(v, 200, $"verify for {p.Label}");
            p.Token = v.Body!["token"]!.GetValue<string>();
            p.PlayerId = v.Body!["playerId"]!.GetValue<string>();
            Check(v.Body!["wallet"]?.GetValue<string>() == p.Wallet, $"{p.Label} signed in as {v.Body["name"]} {v.Body["tag"]} (player {p.PlayerId}, isNew {v.Body["isNew"]})");
            // the same nonce again must be refused
            var replay = await Api("POST", "/api/auth/wallet/verify", new { wallet = p.Wallet, signature = sig, message, name = (string?)null });
            Check(replay.Status == 401, $"{p.Label} replayed signature refused ({replay.Status} {replay.Code})");
        }
        var e = await Api("PUT", "/api/profile/email", new { email = "smoke-a@example.com" }, _a.Token);
        Expect(e, 200, "set email");
        var me = await Api("GET", "/api/profiles/me", token: _a.Token);
        Check(me.Body?["email"]?.GetValue<string>() == "smoke-a@example.com" && me.Body?["wallet"]?.GetValue<string>() == _a.Wallet,
            $"A profile: email {me.Body?["email"]}, wallet {me.Body?["wallet"]}, rum {me.Body?["rum"]}");
    }

    // ---------- 2 ----------

    async Task IslandsAsync()
    {
        foreach (var p in new[] { _a, _b })
        {
            var mine = await Api("GET", "/api/islands/mine", token: p.Token);
            Expect(mine, 200, $"{p.Label} islands/mine");
            var island = mine.Body!["islands"]!.AsArray().FirstOrDefault(i => i?["listed"]?.GetValue<bool>() != true);
            if (island == null)
            {
                var c = await Api("POST", "/api/islands/claim", new { name = $"Smoke Cove {p.Label}" }, p.Token);
                Expect(c, 200, $"{p.Label} claim");
                island = c.Body!;
                Note($"{p.Label} claimed island {island["name"]} (slot {island["slot"]} {island["slotName"]})");
            }
            else Note($"{p.Label} already owns island {island["name"]} (slot {island["slot"]}); reusing it");
            p.IslandId = island["islandId"]!.GetValue<string>();
            var nft = island["nft"]!;
            if (nft["status"]?.GetValue<string>() != "minted")
            {
                Note($"{p.Label} NFT {nft["status"]} ({nft["error"]}); retrying mint");
                var m = await Api("POST", $"/api/islands/{p.IslandId}/mint", token: p.Token);
                Expect(m, 200, "mint retry");
                nft = m.Body!["nft"]!;
            }
            Check(nft["status"]?.GetValue<string>() == "minted", $"{p.Label} Island NFT minted, mint {nft["mint"]}");
            p.IslandMint = nft["mint"]?.GetValue<string>();
            Tx($"{p.Label} Island NFT mint", nft["mintSignature"]?.GetValue<string>());
            if (p.IslandMint != null)
                Check(await OwnsAsync(p.Wallet, p.IslandMint), $"on chain: {p.Label}'s wallet holds the Island NFT ({ExplorerAddr(p.IslandMint)})");
            var meta = await Api("GET", $"/api/islands/{p.IslandId}/metadata.json");
            Check(meta.Status == 200 && meta.Body?["symbol"]?.GetValue<string>() == "RPISL", $"{p.Label} NFT metadata served: {meta.Body?["name"]}");
        }

        var col = await Api("POST", $"/api/islands/{_a.IslandId}/collect", token: _a.Token);
        Expect(col, 200, "A collect");
        Note($"A collected {col.Body!["collected"]} RUM (store {col.Body["fromStore"]}, vault {col.Body["fromVault"]}); balance {col.Body["rum"]}");

        // RUM from voyages: kills / 10 for wallet players, soft-capped since 13 Oct (at most RUM_RUN_CAP, default 40, per run)
        var ra = await Api("POST", "/api/runs", new { kills = 4000, runSeconds = 900, level = 40, captain = "smoke", mapId = "saltwind", clientVersion = "devnet-smoke" }, _a.Token);
        Expect(ra, 200, "A run");
        Check(ra.Body!["rumEarned"]?.GetValue<long>() is > 0 and <= 40, $"A voyage 4000 kills accepted, rumEarned {ra.Body["rumEarned"]} (capped)");
        var rb = await Api("POST", "/api/runs", new { kills = 1500, runSeconds = 600, level = 25, captain = "smoke", mapId = "saltwind", clientVersion = "devnet-smoke" }, _b.Token);
        Expect(rb, 200, "B run");
        Note($"B voyage 1500 kills accepted, rumEarned {rb.Body!["rumEarned"]}");

        var mineA = await Api("GET", "/api/islands/mine", token: _a.Token);
        var rum = mineA.Body!["rum"]!.GetValue<long>();
        var aIsland = mineA.Body["islands"]!.AsArray().First(i => i!["islandId"]!.GetValue<string>() == _a.IslandId)!;
        var affordable = aIsland["buildings"]!.AsArray()
            .Where(b => b!["nextCost"] is JsonValue v && v.GetValue<long>() <= rum - 25)
            .OrderBy(b => b!["nextCost"]!.GetValue<long>()).FirstOrDefault();
        if (affordable == null) Note($"no building affordable with {rum} RUM (keeping 25 for the raid fee; voyage RUM is capped since 13 Oct)");
        else
        {
            var kind = affordable["kind"]!.GetValue<string>();
            var b = await Api("POST", $"/api/islands/{_a.IslandId}/build", new { kind }, _a.Token);
            Expect(b, 200, "build");
            Check(b.Body!["level"]!.GetValue<int>() == affordable["level"]!.GetValue<int>() + 1,
                $"A upgraded {kind} to level {b.Body["level"]} for {b.Body["cost"]} RUM; RUM {rum} -> {b.Body["rum"]}");
        }

        var world = await Api("GET", "/api/islands", token: _a.Token);
        Expect(world, 200, "world");
        var ids = world.Body!["islands"]!.AsArray().Select(i => i!["islandId"]!.GetValue<string>()).ToHashSet();
        Check(ids.Contains(_a.IslandId!) && ids.Contains(_b.IslandId!),
            $"world list: {ids.Count} islands, {world.Body["freeSlots"]} free slots, both A's and B's present");
    }

    // ---------- 3 ----------

    async Task RaidAsync()
    {
        var target = await Api("GET", $"/api/islands/{_b.IslandId}", token: _a.Token);
        Note($"B's island: raidable {target.Body?["raidable"]}, shielded {target.Body?["shielded"]}, exposed RUM {target.Body?["rum"]?["exposed"]}");
        var aBefore = await BalanceAsync(_a.Wallet);
        var start = await Api("POST", "/api/raids/start", new { islandId = _b.IslandId, stakeLamports = 10_000_000L }, _a.Token);
        Expect(start, 200, "raid start");
        var raidId = start.Body!["raidId"]!.GetValue<string>();
        var startedAt = DateTime.UtcNow;
        var memo = start.Body["wager"]!["memo"]!.GetValue<string>();
        Check(memo == "raid:" + raidId, $"raid {raidId} started, fee {start.Body["feePaid"]} RUM, defender difficulty {start.Body["defender"]?["difficulty"]}, wager memo {memo}");

        var dep = await SendSolAsync(_a.Key, _treasury, 10_000_000, memo);
        Tx("A raid stake 0.01 SOL", dep);
        var w = await ApiRetry("POST", $"/api/raids/{raidId}/wager", new { signature = dep }, _a.Token);
        Expect(w, 200, "raid wager deposit");
        Check(w.Body!["wager"]!["status"]!.GetValue<string>() == "funded", "raid wager funded");

        await Task.Delay(3000);
        var secs = Math.Max(1, (int)(DateTime.UtcNow - startedAt).TotalSeconds);
        var res = await Api("POST", $"/api/raids/{raidId}/result", new { won = true, partial = false, seconds = secs, runLog = (object?)null }, _a.Token);
        Expect(res, 200, "raid result");
        Check(res.Body!["result"]!.GetValue<string>() == "won", $"raid won in {secs}s, looted {res.Body["rumLooted"]} RUM, A RUM {res.Body["rum"]}, B shield until {res.Body["defenderShieldUntil"]}");
        var payout = res.Body["wager"]?["payout"];
        var psig = payout?["signature"]?.GetValue<string>();
        Check(payout?["status"]?.GetValue<string>() == "sent" && payout?["lamports"]?.GetValue<long>() == 20_000_000,
            $"2x payout {payout?["sol"]} SOL status {payout?["status"]}");
        Tx("raid 2x payout to A", psig);
        if (psig != null) Check(await TxTransfersAsync(psig, _treasury, _a.Wallet) == 20_000_000, "on chain: payout tx moves 0.02 SOL treasury -> A");
        var aAfter = await BalanceAsync(_a.Wallet);
        Note($"A balance {SolStr(aBefore)} -> {SolStr(aAfter)} SOL (net +0.01 minus fees)");
        var after = await Api("GET", $"/api/islands/{_b.IslandId}", token: _a.Token);
        Check(after.Body?["shielded"]?.GetValue<bool>() == true && after.Body?["raidable"]?.GetValue<bool>() == false, "B's island is shielded and no longer raidable");
        var again = await Api("POST", "/api/raids/start", new { islandId = _b.IslandId }, _a.Token);
        Check(again.Status == 409 && again.Code == "island_shielded", $"raiding again refused: {again.Status} {again.Code}");
    }

    // ---------- 4 ----------

    async Task MarketAsync()
    {
        if (_b.IslandMint == null) throw new SmokeException("B's island has no minted NFT");
        var price = 20_000_000L; // 0.02 SOL
        var esc = await SendNftAsync(_b.Key, _b.IslandMint, _treasury);
        Tx("B sends Island NFT to escrow", esc);
        Check(await OwnsAsync(_treasury, _b.IslandMint), "on chain: treasury (escrow) holds B's NFT");
        var list = await ApiRetry("POST", "/api/market/list", new { islandId = _b.IslandId, priceLamports = price, signature = esc }, _b.Token);
        Expect(list, 200, "B list");
        var listingId = list.Body!["listingId"]!.GetValue<string>();
        Check(list.Body["status"]!.GetValue<string>() == "active", $"listing {listingId} active at {list.Body["priceSol"]} SOL");
        var market = await Api("GET", "/api/market");
        Check(market.Body!["listings"]!.AsArray().Any(l => l!["listingId"]!.GetValue<string>() == listingId), "listing shown in GET /api/market");
        var locked = await Api("POST", $"/api/islands/{_b.IslandId}/collect", token: _b.Token);
        Check(locked.Status == 409 && locked.Code == "island_listed", $"listed island locked (collect -> {locked.Status} {locked.Code})");

        var bBefore = await BalanceAsync(_b.Wallet);
        var pay = await SendSolAsync(_a.Key, _treasury, price, "buy:" + listingId);
        Tx("A pays 0.02 SOL (memo buy:<id>)", pay);
        var buy = await ApiRetry("POST", $"/api/market/{listingId}/buy", new { signature = pay }, _a.Token);
        Expect(buy, 200, "A buy");
        var status = buy.Body!["status"]!.GetValue<string>();
        if (status == "paid")
        {
            Note($"buy left the trade paid ({buy.Body["error"]}); calling complete");
            buy = await Api("POST", $"/api/market/{listingId}/complete", token: _a.Token);
            status = buy.Body?["status"]?.GetValue<string>() ?? "?";
        }
        Check(status == "sold", $"listing {status}");
        Tx("NFT escrow -> A", buy.Body?["transferSignature"]?.GetValue<string>());
        Tx("seller payout to B", buy.Body?["payoutSignature"]?.GetValue<string>());
        Check(await OwnsAsync(_a.Wallet, _b.IslandMint), "on chain: A's wallet now holds the bought Island NFT");
        Check(!await OwnsAsync(_treasury, _b.IslandMint), "on chain: escrow no longer holds it");
        var bAfter = await BalanceAsync(_b.Wallet);
        Check(bAfter - bBefore == price, $"B paid: {SolStr(bBefore)} -> {SolStr(bAfter)} SOL (+{SolStr(bAfter - bBefore)})");
        var isl = await Api("GET", $"/api/islands/{_b.IslandId}", token: _a.Token);
        Check(isl.Body?["owner"]?["playerId"]?.GetValue<string>() == _a.PlayerId && isl.Body?["isMine"]?.GetValue<bool>() == true,
            "island now owned by A in the API");

        // A lists the bought island and cancels: the NFT comes back
        var boughtIsland = _b.IslandId!;
        var mint = _b.IslandMint;
        var esc2 = await SendNftAsync(_a.Key, mint, _treasury);
        Tx("A sends the NFT to escrow", esc2);
        var list2 = await ApiRetry("POST", "/api/market/list", new { islandId = boughtIsland, priceLamports = 50_000_000L, signature = esc2 }, _a.Token);
        Expect(list2, 200, "A list");
        var l2 = list2.Body!["listingId"]!.GetValue<string>();
        Check(list2.Body["status"]!.GetValue<string>() == "active", $"A's listing {l2} active");
        var reuse = await Api("POST", "/api/market/list", new { islandId = boughtIsland, priceLamports = 50_000_000L, signature = esc2 }, _a.Token);
        Check(reuse.Status == 409, $"listing the same island again refused ({reuse.Status} {reuse.Code})");
        var cancel = await Api("POST", $"/api/market/{l2}/cancel", token: _a.Token);
        Expect(cancel, 200, "A cancel");
        Check(cancel.Body!["status"]!.GetValue<string>() == "cancelled", $"listing {cancel.Body["status"]} {cancel.Body["error"]}");
        Tx("NFT escrow -> A (cancel)", cancel.Body["transferSignature"]?.GetValue<string>());
        Check(await OwnsAsync(_a.Wallet, mint), "on chain: the NFT is back in A's wallet");
        // B has no island now: the next run claims a fresh one
    }

    // ---------- 5 ----------

    async Task WagerAsync()
    {
        var stake = 10_000_000L;
        var lob = await Api("POST", "/api/lobbies", new { @public = false, stakeLamports = stake }, _a.Token);
        Expect(lob, 201, "create staked lobby");
        var lobbyId = lob.Body!["lobbyId"]!.GetValue<string>();
        var memo = lob.Body["wager"]?["memo"]?.GetValue<string>();
        Check(memo == "wager:" + lobbyId && lob.Body["wager"]?["stakeLamports"]?.GetValue<long>() == stake, $"lobby {lobbyId} staked 0.01 SOL, memo {memo}");
        // 13 Oct hotfix: bot matches are free play (the house no longer matches stakes)
        var bot = await Api("POST", $"/api/lobbies/{lobbyId}/bot", token: _a.Token);
        Expect(bot, 409, "add bot to a staked lobby is refused");
        Check(bot.Body?["error"]?.GetValue<string>() == "bot_stakes_off", $"add bot: {bot.Body?["error"]}");

        var dep = await SendSolAsync(_a.Key, _treasury, stake, memo!);
        Tx("A wager stake 0.01 SOL (memo wager:<lobbyId>)", dep);
        var d = await ApiRetry("POST", $"/api/wagers/{lobbyId}/deposit", new { signature = dep }, _a.Token);
        Expect(d, 200, "wager deposit");
        var w = await Api("GET", $"/api/wagers/{lobbyId}");
        Check(w.Body?["host"]?["deposited"]?.GetValue<bool>() == true && w.Body?["funded"]?.GetValue<bool>() == false,
            $"host deposited, waiting for an opponent: status {w.Body?["status"]}, funded {w.Body?["funded"]}");

        // the relay match needs a game client; close the lobby before the start instead -> full refund
        var aBefore = await BalanceAsync(_a.Wallet);
        var leave = await Api("PUT", $"/api/lobbies/{lobbyId}/status", new { status = "left" }, _a.Token);
        Expect(leave, 200, "host leaves");
        Check(leave.Body!["status"]?.GetValue<string>() == "closed", $"lobby {leave.Body["status"]}");
        JsonNode? refund = null;
        for (var i = 0; i < 20 && refund == null; i++)
        {
            await Api("POST", "/api/admin/wagers/sweep", admin: true);
            var g = await Api("GET", $"/api/wagers/{lobbyId}");
            refund = g.Body?["payouts"]?.AsArray().FirstOrDefault(p => p?["reason"]?.GetValue<string>() == "wager_refund" && p?["status"]?.GetValue<string>() == "sent");
            if (refund == null) { w = g; await Task.Delay(3000); }
            else w = g;
        }
        Check(w.Body?["status"]?.GetValue<string>() == "refunded", $"wager status {w.Body?["status"]}, result {w.Body?["result"]}");
        Check(refund != null && refund["lamports"]!.GetValue<long>() == stake, $"refund payout {refund?["sol"]} SOL {refund?["status"]}");
        var rsig = refund?["signature"]?.GetValue<string>();
        Tx("wager refund to A", rsig);
        if (rsig != null) Check(await TxTransfersAsync(rsig, _treasury, _a.Wallet) == stake, "on chain: refund tx moves 0.01 SOL treasury -> A");
        Note($"A balance {SolStr(aBefore)} -> {SolStr(await BalanceAsync(_a.Wallet))} SOL");
    }

    // ---------- 6 ----------

    async Task WeeklyAsync()
    {
        var now = DateTime.UtcNow.Date;
        var monday = now.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var week = monday.ToString("yyyy-MM-dd");
        var dry = await Api("POST", "/api/admin/rewards/weekly", new { weekStart = week, dryRun = true }, admin: true);
        Expect(dry, 200, "dry run");
        var winners = dry.Body!["winners"]!.AsArray();
        Note($"week {week}: alreadyRecorded {dry.Body["alreadyRecorded"]}, {winners.Count} winners (dry run)");
        foreach (var x in winners) Note($"  #{x!["rank"]} {x["name"]} {x["walletShort"]} {x["kills"]} kills -> {x["sol"]} SOL");
        // since 13 Oct only server-checked runs earn SOL (WEEKLY_REWARD_CHECKED_ONLY defaults to true); the smoke runs send no log
        Note(winners.Any(x => x!["wallet"]?.GetValue<string>() == _a.Wallet) ? "A is among the dry-run winners" : "A is not a winner (its runs are not server-checked)");
        // 13 Oct hotfix: a week is paid only after it has ended, so the current week's real run is refused
        var current = await Api("POST", "/api/admin/rewards/weekly", new { weekStart = week, dryRun = false }, admin: true);
        Expect(current, 409, "real run of the current week is refused");
        Check(current.Body?["error"]?.GetValue<string>() == "week_not_ended", $"current week: {current.Body?["error"]}");
        if (_skipWeeklyPay) { Note("previous-week real run skipped (--skip-weekly-pay)"); return; }
        week = monday.AddDays(-7).ToString("yyyy-MM-dd");

        var real = await Api("POST", "/api/admin/rewards/weekly", new { weekStart = week, dryRun = false }, admin: true);
        Expect(real, 200, "real run (previous week)");
        foreach (var x in real.Body!["winners"]!.AsArray())
        {
            var p = x!["payout"];
            Note($"  #{x["rank"]} {x["walletShort"]}: {p?["sol"]} SOL {p?["status"]}");
            Tx($"weekly reward #{x["rank"]} to {x["walletShort"]}", p?["signature"]?.GetValue<string>());
        }
        var pub = await Api("GET", $"/api/rewards/weekly?weekStart={week}");
        Expect(pub, 200, "public rewards");
        var ws = pub.Body!["winners"]!.AsArray();
        if (ws.Count == 0) Note("previous week: no eligible (server-checked) winners");
        else Check(pub.Body["alreadyRecorded"]?.GetValue<bool>() == true, "week recorded");
        Check(ws.All(x => x!["payout"]?["status"]?.GetValue<string>() == "sent"), $"all {ws.Count} reward payouts sent");
        var aw = ws.FirstOrDefault(x => x!["wallet"]?.GetValue<string>() == _a.Wallet)?["payout"]?["signature"]?.GetValue<string>();
        if (aw != null) Check(await TxTransfersAsync(aw, _treasury, _a.Wallet) > 0, "on chain: A's reward transfer confirmed");
        var again = await Api("POST", "/api/admin/rewards/weekly", new { weekStart = week, dryRun = false }, admin: true);
        var paidTwice = again.Body?["winners"]?.AsArray().Select(x => x!["payout"]?["payoutId"]?.ToString()).ToList();
        var first = ws.Select(x => x!["payout"]?["payoutId"]?.ToString()).ToList();
        Check(again.Status == 200 && paidTwice != null && paidTwice.SequenceEqual(first), "a second real run pays nothing new (same payouts)");
        var lb = await Api("GET", "/api/leaderboard/weekly?limit=5", token: _a.Token);
        Note($"leaderboard top: " + string.Join(", ", lb.Body?["entries"]?.AsArray().Select(e => $"{e!["name"]} {e["kills"]} ({e["wallet"]}, reward {e["reward"]?["sol"]})") ?? Array.Empty<string>()));
    }

    // ---------- 7 ----------

    async Task RaceAsync()
    {
        var r = await Api("GET", "/api/race/daily", token: _a.Token);
        Expect(r, 200, "race");
        var race = r.Body!["race"]!;
        var date = race["date"]!.GetValue<string>();
        var seed = race["seed"]!.GetValue<int>();
        var target = race["targetKills"]!.GetValue<int>();
        Note($"race {date}: seed {seed}, map {race["mapId"]}, target {target} kills, {r.Body["entries"]} entries");
        var bad = await Api("POST", "/api/race/daily/entry", new { date, seed = seed ^ 1, timeSeconds = 200.0, kills = target, level = 12 }, _a.Token);
        Check(bad.Status == 400 && bad.Code == "seed_mismatch", $"wrong seed refused ({bad.Status} {bad.Code})");
        var e = await Api("POST", "/api/race/daily/entry", new { date, seed, timeSeconds = 187.4, kills = target, level = 12, clientVersion = "devnet-smoke" }, _a.Token);
        Expect(e, 200, "A entry");
        Check(e.Body!["accepted"]?.GetValue<bool>() == true, $"A entry accepted: rank {e.Body["best"]?["rank"]}, best {e.Body["best"]?["timeSeconds"]}s, improved {e.Body["improved"]}");
        var eb = await Api("POST", "/api/race/daily/entry", new { date, seed, timeSeconds = 241.9, kills = target, level = 11, clientVersion = "devnet-smoke" }, _b.Token);
        Expect(eb, 200, "B entry");
        var board = await Api("GET", "/api/race/daily/board?limit=10", token: _a.Token);
        Expect(board, 200, "board");
        var entries = board.Body!["entries"]!.AsArray();
        Check(entries.Any(x => x!["playerId"]?.GetValue<string>() == _a.PlayerId) && entries.Any(x => x!["playerId"]?.GetValue<string>() == _b.PlayerId),
            $"board has A and B ({board.Body["total"]} total); me rank {board.Body["me"]?["rank"]}");
    }

    // ---------- 8 ----------

    async Task FinalAsync()
    {
        foreach (var (k, w) in new[] { ("A", _a.Wallet), ("B", _b.Wallet), ("treasury", _treasury) })
        {
            _endBalances[k] = await BalanceAsync(w);
            Note($"{k} {SolStr(_startBalances[k])} -> {SolStr(_endBalances[k])} SOL ({(_endBalances[k] - _startBalances[k] >= 0 ? "+" : "")}{SolStr(_endBalances[k] - _startBalances[k])})");
        }
        var pa = await Api("GET", "/api/payouts/mine", token: _a.Token);
        Note($"A payouts recorded by the API: {pa.Body?["payouts"]?.AsArray().Count}");
    }

    // ================= report =================

    public string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hackathon devnet smoke test");
        sb.AppendLine();
        sb.AppendLine($"Run {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC against `{_api}` (Solana devnet) by `server/tools/DevnetSmoke`.");
        sb.AppendLine($"Player A `{(_a.Key == null ? "?" : _a.Wallet)}`, player B `{(_b.Key == null ? "?" : _b.Wallet)}`, treasury `{_treasury}`.");
        sb.AppendLine("No secrets in this file: addresses, ids and transaction signatures are public.");
        sb.AppendLine();
        sb.AppendLine("| Step | Result |");
        sb.AppendLine("|---|---|");
        foreach (var s in _steps) sb.AppendLine($"| {s.Name} | {(s.Pass ? "PASS" : "FAIL")} |");
        foreach (var s in _steps)
        {
            sb.AppendLine();
            sb.AppendLine($"## {s.Name}: {(s.Pass ? "PASS" : "FAIL")}");
            sb.AppendLine();
            foreach (var n in s.Notes) sb.AppendLine("- " + n);
            if (s.Error != null) sb.AppendLine($"- **Error:** {s.Error}");
            if (s.Txs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Transactions:");
                foreach (var (label, sig) in s.Txs) sb.AppendLine($"- {label}: [{sig[..12]}...]({Explorer(sig)})");
            }
        }
        return sb.ToString();
    }

    // ================= HTTP =================

    sealed record ApiResult(int Status, JsonNode? Body, string Raw)
    {
        public string? Code => Body is JsonObject o && o["error"] is JsonValue v ? v.GetValue<string>() : null;
    }

    void Expect(ApiResult r, int status, string what)
    {
        if (r.Status != status) throw new SmokeException($"{what}: expected {status}, got {r.Status} {Trim(r.Raw)}");
    }

    static string Trim(string s) => s.Length > 400 ? s[..400] + "..." : s;

    async Task<ApiResult> Api(string method, string path, object? body = null, string? token = null, bool admin = false)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), _api + path);
            if (body != null) req.Content = JsonContent.Create(body);
            if (token != null) req.Headers.Authorization = new("Bearer", token);
            if (admin) req.Headers.Add("X-Admin-Key", _adminKey);
            using var res = await _http.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            if ((int)res.StatusCode == 429 && attempt < 5) { await Task.Delay(2000 * (attempt + 1)); continue; }
            JsonNode? node = null;
            try { if (raw.Length > 0) node = JsonNode.Parse(raw); } catch (JsonException) { }
            Log($"  {method} {path} -> {(int)res.StatusCode}{((int)res.StatusCode >= 400 ? " " + Trim(raw) : "")}");
            return new ApiResult((int)res.StatusCode, node, raw);
        }
    }

    /// <summary>Deposit calls answer 409 tx_not_confirmed until the API's node sees the transaction: retry.</summary>
    async Task<ApiResult> ApiRetry(string method, string path, object? body, string? token)
    {
        ApiResult r = null!;
        for (var i = 0; i < 15; i++)
        {
            r = await Api(method, path, body, token);
            if (!(r.Status == 409 && r.Code == "tx_not_confirmed") && !(r.Status == 400 && r.Code == "nft_not_received" && i < 5)) return r;
            await Task.Delay(2000);
        }
        return r;
    }

    // ================= Solana =================

    static Account LoadKeypair(string path)
    {
        var bytes = JsonSerializer.Deserialize<int[]>(File.ReadAllText(path))!.Select(b => (byte)b).ToArray();
        if (bytes.Length != 64) throw new SmokeException($"{Path.GetFileName(path)} is not a 64-byte keypair");
        var acc = new Account(bytes, bytes[32..]);
        var probe = Encoding.UTF8.GetBytes("probe");
        if (!acc.PublicKey.Verify(probe, acc.Sign(probe))) throw new SmokeException($"{Path.GetFileName(path)}: secret does not match its public key");
        return acc;
    }

    async Task<JsonElement> Rpc(string method, params object?[] args)
    {
        var body = new { jsonrpc = "2.0", id = Interlocked.Increment(ref _rpcId), method, @params = args };
        for (var attempt = 0; ; attempt++)
        {
            using var res = await _http.PostAsJsonAsync(_rpcUrl, body);
            if ((int)res.StatusCode == 429 && attempt < 6) { await Task.Delay(1000 * (attempt + 1)); continue; }
            var text = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var err)) throw new SmokeException($"RPC {method}: {err}");
            return doc.RootElement.GetProperty("result").Clone();
        }
    }

    static readonly object Confirmed = new { commitment = "confirmed" };

    async Task<long> BalanceAsync(string address) => (await Rpc("getBalance", address, Confirmed)).GetProperty("value").GetInt64();

    async Task<List<(string Account, ulong Amount)>> TokenAccountsAsync(string owner, string mint)
    {
        var r = await Rpc("getTokenAccountsByOwner", owner, new { mint }, new { commitment = "confirmed", encoding = "jsonParsed" });
        var list = new List<(string, ulong)>();
        foreach (var a in r.GetProperty("value").EnumerateArray())
        {
            var amount = a.GetProperty("account").GetProperty("data").GetProperty("parsed").GetProperty("info").GetProperty("tokenAmount").GetProperty("amount").GetString()!;
            list.Add((a.GetProperty("pubkey").GetString()!, ulong.Parse(amount)));
        }
        return list;
    }

    async Task<bool> OwnsAsync(string owner, string mint) => (await TokenAccountsAsync(owner, mint)).Any(t => t.Amount >= 1);

    /// <summary>Lamports the confirmed transaction moves from one address to another (system transfers).</summary>
    async Task<long> TxTransfersAsync(string sig, string from, string to)
    {
        for (var i = 0; i < 10; i++)
        {
            var r = await Rpc("getTransaction", sig, new { commitment = "confirmed", encoding = "jsonParsed", maxSupportedTransactionVersion = 0 });
            if (r.ValueKind == JsonValueKind.Null) { await Task.Delay(1500); continue; }
            if (r.GetProperty("meta").GetProperty("err").ValueKind != JsonValueKind.Null) return -1;
            long sum = 0;
            foreach (var ix in r.GetProperty("transaction").GetProperty("message").GetProperty("instructions").EnumerateArray())
            {
                if (ix.TryGetProperty("program", out var p) && p.GetString() == "system" && ix.TryGetProperty("parsed", out var parsed)
                    && parsed.ValueKind == JsonValueKind.Object && parsed.GetProperty("type").GetString() == "transfer")
                {
                    var info = parsed.GetProperty("info");
                    if (info.GetProperty("source").GetString() == from && info.GetProperty("destination").GetString() == to)
                        sum += info.GetProperty("lamports").GetInt64();
                }
            }
            return sum;
        }
        return 0;
    }

    async Task<string> BlockhashAsync() => (await Rpc("getLatestBlockhash", Confirmed)).GetProperty("value").GetProperty("blockhash").GetString()!;

    async Task<string> SendSolAsync(Account from, string to, long lamports, string memo)
    {
        var tx = new TransactionBuilder().SetRecentBlockHash(await BlockhashAsync()).SetFeePayer(from)
            .AddInstruction(SystemProgram.Transfer(from.PublicKey, new PublicKey(to), (ulong)lamports))
            .AddInstruction(MemoProgram.NewMemoV2(memo, from.PublicKey))
            .Build(new List<Account> { from });
        return await SendAndConfirmAsync(tx);
    }

    async Task<string> SendNftAsync(Account from, string mint, string to)
    {
        var holdings = await TokenAccountsAsync(from.PublicKey.Key, mint);
        var source = holdings.FirstOrDefault(h => h.Amount >= 1);
        if (source.Account == null) throw new SmokeException($"{from.PublicKey.Key} does not hold {mint}");
        var mintKey = new PublicKey(mint);
        var toKey = new PublicKey(to);
        var tx = new TransactionBuilder().SetRecentBlockHash(await BlockhashAsync()).SetFeePayer(from)
            .AddInstruction(CreateAtaIdempotent(from.PublicKey, toKey, mintKey))
            .AddInstruction(TokenProgram.Transfer(new PublicKey(source.Account), AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(toKey, mintKey), 1, from.PublicKey))
            .Build(new List<Account> { from });
        return await SendAndConfirmAsync(tx);
    }

    static TransactionInstruction CreateAtaIdempotent(PublicKey payer, PublicKey owner, PublicKey mint) => new()
    {
        ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
        Keys = new List<AccountMeta>
        {
            AccountMeta.Writable(payer, true),
            AccountMeta.Writable(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint), false),
            AccountMeta.ReadOnly(owner, false),
            AccountMeta.ReadOnly(mint, false),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
        },
        Data = new byte[] { 1 },
    };

    async Task<string> SendAndConfirmAsync(byte[] tx)
    {
        var sig = (await Rpc("sendTransaction", Convert.ToBase64String(tx), new { encoding = "base64", preflightCommitment = "confirmed" })).GetString()!;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var v = (await Rpc("getSignatureStatuses", new[] { sig }, new { searchTransactionHistory = true })).GetProperty("value")[0];
            if (v.ValueKind != JsonValueKind.Null)
            {
                if (v.GetProperty("err").ValueKind != JsonValueKind.Null) throw new SmokeException($"tx {sig} failed: {v.GetProperty("err")}");
                var cs = v.TryGetProperty("confirmationStatus", out var c) ? c.GetString() : null;
                if (cs is "confirmed" or "finalized") return sig;
            }
            await Task.Delay(800);
        }
        throw new SmokeException($"tx {sig} not confirmed in 60 s");
    }
}

sealed class SmokeException(string message) : Exception(message);
