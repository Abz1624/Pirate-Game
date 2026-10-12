using System.Net.Http.Json;
using System.Text.Json;

namespace RoguePirates.Api.Solana;

/// <summary>A parsed SOL transfer instruction (system program) inside a transaction.</summary>
public sealed record SolTransfer(string Source, string Destination, long Lamports);

/// <summary>What the API needs to know about a confirmed transaction (from getTransaction, jsonParsed).</summary>
public sealed record ChainTransaction(
    string Signature,
    long Slot,
    bool Failed,
    IReadOnlyList<string> Signers,
    IReadOnlyList<SolTransfer> Transfers,
    IReadOnlyList<string> Memos,
    DateTime? BlockTime);

/// <summary>A token account holding a mint (getTokenAccountsByOwner, jsonParsed).</summary>
public sealed record TokenHolding(string TokenAccount, string Mint, string Owner, ulong Amount);

/// <summary>
/// The Solana JSON-RPC calls the API makes. The real one talks to <c>SOLANA_RPC_URL</c>; unit tests substitute a fake
/// so no test touches the network. Transactions are built and signed by <see cref="SolanaService"/> with Solnet.
/// </summary>
public interface ISolanaRpc
{
    Task<long> GetBalanceAsync(string address, CancellationToken ct = default);
    /// <summary>The transaction at "confirmed" commitment, or null when the node does not know it (yet).</summary>
    Task<ChainTransaction?> GetTransactionAsync(string signature, CancellationToken ct = default);
    Task<string> GetLatestBlockhashAsync(CancellationToken ct = default);
    /// <summary>Sends a signed, serialized transaction; returns its signature.</summary>
    Task<string> SendTransactionAsync(byte[] transaction, CancellationToken ct = default);
    /// <summary>The signature's confirmation status (processed, confirmed, finalized), "failed", or null if unknown.</summary>
    Task<string?> GetSignatureStatusAsync(string signature, CancellationToken ct = default);
    Task<long> GetMinimumBalanceForRentExemptionAsync(int dataLength, CancellationToken ct = default);
    Task<IReadOnlyList<TokenHolding>> GetTokenAccountsByOwnerAsync(string owner, string mint, CancellationToken ct = default);
    /// <summary>Whether an account exists (getAccountInfo returns a value).</summary>
    Task<bool> AccountExistsAsync(string address, CancellationToken ct = default);
    Task<string> RequestAirdropAsync(string address, long lamports, CancellationToken ct = default);
}

/// <summary>An RPC error from the node (code and message as returned).</summary>
public sealed class SolanaRpcException : Exception
{
    public int Code { get; }
    public SolanaRpcException(int code, string message) : base(message) => Code = code;
}

/// <summary>Plain JSON-RPC over HttpClient (jsonParsed encodings, versioned transactions accepted).</summary>
public sealed class HttpSolanaRpc : ISolanaRpc
{
    readonly HttpClient _http;
    readonly string _url;
    int _id;

    public HttpSolanaRpc(HttpClient http, string url)
    {
        _http = http;
        _url = url;
    }

    async Task<JsonElement> CallAsync(string method, object?[] args, CancellationToken ct)
    {
        var body = new { jsonrpc = "2.0", id = Interlocked.Increment(ref _id), method, @params = args };
        for (var attempt = 0; ; attempt++)
        {
            using var res = await _http.PostAsJsonAsync(_url, body, ct);
            // public devnet RPC rate limits: back off and retry a few times
            if ((int)res.StatusCode == 429 && attempt < 4)
            {
                await Task.Delay(500 * (attempt + 1), ct);
                continue;
            }
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new SolanaRpcException((int)res.StatusCode, $"RPC {method}: HTTP {(int)res.StatusCode} {Truncate(text)}");
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                var code = err.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : 0;
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() ?? "" : err.ToString();
                throw new SolanaRpcException(code, $"RPC {method}: {msg}");
            }
            return root.GetProperty("result").Clone();
        }
    }

    static string Truncate(string s) => s.Length > 300 ? s[..300] : s;

    static readonly object Confirmed = new { commitment = "confirmed" };

    public async Task<long> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        var r = await CallAsync("getBalance", new object?[] { address, Confirmed }, ct);
        return r.GetProperty("value").GetInt64();
    }

    public async Task<ChainTransaction?> GetTransactionAsync(string signature, CancellationToken ct = default)
    {
        var r = await CallAsync("getTransaction", new object?[]
        {
            signature, new { commitment = "confirmed", encoding = "jsonParsed", maxSupportedTransactionVersion = 0 },
        }, ct);
        if (r.ValueKind == JsonValueKind.Null) return null;
        return ParseTransaction(signature, r);
    }

    /// <summary>Parses a jsonParsed getTransaction result (public for tests).</summary>
    public static ChainTransaction ParseTransaction(string signature, JsonElement r)
    {
        var slot = r.TryGetProperty("slot", out var s) ? s.GetInt64() : 0;
        var meta = r.GetProperty("meta");
        var failed = meta.TryGetProperty("err", out var err) && err.ValueKind != JsonValueKind.Null;
        DateTime? blockTime = r.TryGetProperty("blockTime", out var bt) && bt.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(bt.GetInt64()).UtcDateTime : null;
        var message = r.GetProperty("transaction").GetProperty("message");
        var signers = new List<string>();
        foreach (var k in message.GetProperty("accountKeys").EnumerateArray())
        {
            if (k.ValueKind == JsonValueKind.Object && k.TryGetProperty("signer", out var sg) && sg.GetBoolean())
                signers.Add(k.GetProperty("pubkey").GetString()!);
        }
        var transfers = new List<SolTransfer>();
        var memos = new List<string>();
        foreach (var ix in message.GetProperty("instructions").EnumerateArray())
        {
            var program = ix.TryGetProperty("program", out var p) ? p.GetString() : null;
            if (program == "system" && ix.TryGetProperty("parsed", out var parsed) && parsed.ValueKind == JsonValueKind.Object
                && parsed.TryGetProperty("type", out var type) && type.GetString() is "transfer" or "transferWithSeed")
            {
                var info = parsed.GetProperty("info");
                transfers.Add(new SolTransfer(info.GetProperty("source").GetString()!, info.GetProperty("destination").GetString()!,
                    info.GetProperty("lamports").GetInt64()));
            }
            else if (program == "spl-memo" && ix.TryGetProperty("parsed", out var memo) && memo.ValueKind == JsonValueKind.String)
            {
                memos.Add(memo.GetString()!);
            }
        }
        return new ChainTransaction(signature, slot, failed, signers, transfers, memos, blockTime);
    }

    public async Task<string> GetLatestBlockhashAsync(CancellationToken ct = default)
    {
        var r = await CallAsync("getLatestBlockhash", new object?[] { Confirmed }, ct);
        return r.GetProperty("value").GetProperty("blockhash").GetString()!;
    }

    public async Task<string> SendTransactionAsync(byte[] transaction, CancellationToken ct = default)
    {
        var r = await CallAsync("sendTransaction", new object?[]
        {
            Convert.ToBase64String(transaction), new { encoding = "base64", preflightCommitment = "confirmed" },
        }, ct);
        return r.GetString()!;
    }

    public async Task<string?> GetSignatureStatusAsync(string signature, CancellationToken ct = default)
    {
        var r = await CallAsync("getSignatureStatuses", new object?[] { new[] { signature }, new { searchTransactionHistory = true } }, ct);
        var v = r.GetProperty("value")[0];
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.TryGetProperty("err", out var err) && err.ValueKind != JsonValueKind.Null) return "failed";
        return v.TryGetProperty("confirmationStatus", out var cs) ? cs.GetString() : "processed";
    }

    public async Task<long> GetMinimumBalanceForRentExemptionAsync(int dataLength, CancellationToken ct = default)
    {
        var r = await CallAsync("getMinimumBalanceForRentExemption", new object?[] { dataLength }, ct);
        return r.GetInt64();
    }

    public async Task<IReadOnlyList<TokenHolding>> GetTokenAccountsByOwnerAsync(string owner, string mint, CancellationToken ct = default)
    {
        var r = await CallAsync("getTokenAccountsByOwner", new object?[]
        {
            owner, new { mint }, new { commitment = "confirmed", encoding = "jsonParsed" },
        }, ct);
        var list = new List<TokenHolding>();
        foreach (var a in r.GetProperty("value").EnumerateArray())
        {
            var info = a.GetProperty("account").GetProperty("data").GetProperty("parsed").GetProperty("info");
            var amount = ulong.Parse(info.GetProperty("tokenAmount").GetProperty("amount").GetString()!);
            list.Add(new TokenHolding(a.GetProperty("pubkey").GetString()!, info.GetProperty("mint").GetString()!,
                info.GetProperty("owner").GetString()!, amount));
        }
        return list;
    }

    public async Task<bool> AccountExistsAsync(string address, CancellationToken ct = default)
    {
        var r = await CallAsync("getAccountInfo", new object?[] { address, new { commitment = "confirmed", encoding = "base64" } }, ct);
        return r.GetProperty("value").ValueKind != JsonValueKind.Null;
    }

    public async Task<string> RequestAirdropAsync(string address, long lamports, CancellationToken ct = default)
    {
        var r = await CallAsync("requestAirdrop", new object?[] { address, lamports, Confirmed }, ct);
        return r.GetString()!;
    }
}
