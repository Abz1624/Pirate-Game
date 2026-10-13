using System.Text;
using System.Text.Json;
using RoguePirates.Api.Playtest;
using Solnet.Programs;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;

namespace RoguePirates.Api.Solana;

/// <summary>A verified SOL transfer into the treasury.</summary>
public sealed record VerifiedTransfer(string Signature, string Sender, long Lamports, string? Memo, long Slot);

/// <summary>A signed treasury transfer not sent yet: its bytes, its signature and the block height it must land by.</summary>
public sealed record PreparedTransfer(byte[] Tx, string Signature, long LastValidBlockHeight);

/// <summary>An NFT minted by the treasury.</summary>
public sealed record MintedNft(string Mint, string Signature, string Owner);

/// <summary>V5 M9h: a signed NFT mint not sent yet: its bytes, the new mint's address, the signature and the block height it
/// must land by. The caller stores the mint address and signature before sending, so a retry can check the chain first.</summary>
public sealed record PreparedMint(byte[] Tx, string Mint, string Signature, long LastValidBlockHeight);

/// <summary>
/// Hackathon (devnet): Solana operations on Solnet. Signature checks are local (ed25519); chain reads and writes go
/// through <see cref="ISolanaRpc"/>. The treasury (also the escrow) keypair comes from TREASURY_SECRET_KEY and is never
/// logged or returned; without it, operations that need it throw 503 <c>treasury_off</c>.
/// </summary>
public sealed class SolanaService
{
    public const long LamportsPerSol = 1_000_000_000;

    readonly ISolanaRpc _rpc;
    readonly ApiSettings _settings;
    readonly ILogger<SolanaService> _log;
    readonly Account? _treasury;

    public SolanaService(ISolanaRpc rpc, ApiSettings settings, ILogger<SolanaService> log)
    {
        _rpc = rpc;
        _settings = settings;
        _log = log;
        if (settings.TreasurySecretKey != null && settings.IsMainnet && !settings.AllowMainnet)
        {
            // 13 Oct hotfix: a mainnet RPC or cluster needs an explicit ALLOW_MAINNET=true; until then no SOL moves
            _log.LogError("SOLANA_RPC_URL/SOLANA_CLUSTER point at mainnet but ALLOW_MAINNET is not true; treasury off");
        }
        else if (settings.TreasurySecretKey != null)
        {
            try { _treasury = ParseKeypair(settings.TreasurySecretKey); }
            catch (Exception ex) { _log.LogError("TREASURY_SECRET_KEY is set but unreadable ({Type}); treasury off", ex.GetType().Name); }
        }
    }

    public ISolanaRpc Rpc => _rpc;
    public string Cluster => _settings.SolanaCluster;
    public bool TreasuryOn => _treasury != null;
    public string? TreasuryAddress => _treasury?.PublicKey.Key;

    Account Treasury => _treasury ?? throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");

    // ---------- keys and signatures ----------

    /// <summary>A keypair from a base58 64-byte secret (Phantom export) or a solana-keygen JSON array of 64 numbers.</summary>
    public static Account ParseKeypair(string secret)
    {
        secret = secret.Trim();
        byte[] bytes = secret.StartsWith('[')
            ? JsonSerializer.Deserialize<int[]>(secret)!.Select(b => (byte)b).ToArray()
            : Encoders.Base58.DecodeData(secret);
        if (bytes.Length != 64) throw new FormatException("A Solana secret key is 64 bytes.");
        var pub = bytes[32..];
        var account = new Account(bytes, pub);
        // the secret's second half must be the public key of its first half (a corrupted key would sign nothing valid)
        var probe = Encoding.UTF8.GetBytes("rp-keycheck");
        if (!account.PublicKey.Verify(probe, account.Sign(probe))) throw new FormatException("The secret key does not match its public key.");
        return account;
    }

    /// <summary>True for a base58 string that decodes to 32 bytes (a wallet or mint address).</summary>
    public static bool IsValidAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length is < 32 or > 44) return false;
        try { return Encoders.Base58.DecodeData(address).Length == 32; }
        catch { return false; }
    }

    /// <summary>True for a base58 string that decodes to 64 bytes (a transaction signature).</summary>
    public static bool IsValidSignature(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || signature.Length is < 64 or > 88) return false;
        try { return Encoders.Base58.DecodeData(signature).Length == 64; }
        catch { return false; }
    }

    /// <summary>Checks an ed25519 signature (base58, 64 bytes) by the wallet over the UTF-8 message.</summary>
    public static bool VerifyMessageSignature(string wallet, string message, string signatureBase58)
    {
        if (!IsValidAddress(wallet)) return false;
        byte[] sig;
        try { sig = Encoders.Base58.DecodeData(signatureBase58.Trim()); }
        catch { return false; }
        if (sig.Length != 64) return false;
        try { return new PublicKey(wallet).Verify(Encoding.UTF8.GetBytes(message), sig); }
        catch { return false; }
    }

    // ---------- reads ----------

    public async Task<long> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        if (!IsValidAddress(address)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        return await Wrap(() => _rpc.GetBalanceAsync(address, ct));
    }

    /// <summary>Whether the wallet holds the NFT (a token account of that mint with amount 1 or more).</summary>
    public async Task<bool> OwnsNftAsync(string wallet, string mint, CancellationToken ct = default)
    {
        if (!IsValidAddress(wallet) || !IsValidAddress(mint)) return false;
        var holdings = await Wrap(() => _rpc.GetTokenAccountsByOwnerAsync(wallet, mint, ct));
        return holdings.Any(h => h.Mint == mint && h.Amount >= 1);
    }

    /// <summary>
    /// Checks a SOL transfer into the treasury: the transaction is confirmed and succeeded, the sender signed it, the
    /// system transfers from the sender to the treasury add up to at least <paramref name="minLamports"/>, and the memo
    /// matches when one is expected. Whether the signature was already used is the caller's check (the deposits table).
    /// </summary>
    public async Task<VerifiedTransfer> VerifyTransferToTreasuryAsync(string signature, string sender, long minLamports,
        string? expectedMemo = null, CancellationToken ct = default, DateTime? notBefore = null)
    {
        var treasury = Treasury.PublicKey.Key;
        if (!IsValidSignature(signature)) throw new ApiException(400, "invalid_signature", "Not a transaction signature.");
        var tx = await Wrap(() => _rpc.GetTransactionAsync(signature, ct));
        if (tx == null)
            throw new ApiException(409, "tx_not_confirmed", "The transaction is not confirmed yet (or unknown on " + Cluster + "). Retry in a few seconds.");
        if (tx.Failed) throw new ApiException(400, "tx_failed", "The transaction failed on chain.");
        if (!tx.Signers.Contains(sender)) throw new ApiException(400, "wrong_sender", "The transaction was not signed by your wallet.");
        // V5 M9h (review): a transfer older than what it pays for (an old top-up or payment) is not taken, so it cannot be
        // recorded and refunded through a later wager
        if (notBefore != null && tx.BlockTime != null && tx.BlockTime < notBefore)
            throw new ApiException(400, "tx_too_old", "That transfer was made before this stake existed; send a new one.");
        var toTreasury = tx.Transfers.Where(t => t.Destination == treasury).ToList();
        if (toTreasury.Count == 0) throw new ApiException(400, "wrong_recipient", "The transaction sends nothing to the treasury.");
        var fromSender = toTreasury.Where(t => t.Source == sender).Sum(t => t.Lamports);
        if (fromSender == 0) throw new ApiException(400, "wrong_sender", "The transfer to the treasury is not from your wallet.");
        if (fromSender < minLamports)
            throw new ApiException(400, "amount_too_low", $"The transfer is {fromSender} lamports; at least {minLamports} are needed.");
        var memo = tx.Memos.Count > 0 ? string.Join(" ", tx.Memos) : null;
        if (expectedMemo != null && (memo == null || !memo.Contains(expectedMemo, StringComparison.Ordinal)))
            throw new ApiException(400, "memo_mismatch", "The transaction's memo does not carry the expected reference.");
        return new VerifiedTransfer(signature, sender, fromSender, memo, tx.Slot);
    }

    // ---------- writes (treasury) ----------

    /// <summary>
    /// Builds and signs a SOL transfer from the treasury without sending it. The signature is known before the send (it
    /// is the fee payer's signature inside the signed bytes), so a payout can be recorded with it first: whatever happens
    /// after the send, the payout can be resolved by signature and is never paid twice.
    /// </summary>
    public async Task<PreparedTransfer> PrepareSolTransferAsync(string toWallet, long lamports, string? memo = null, CancellationToken ct = default)
    {
        var treasury = Treasury;
        if (!IsValidAddress(toWallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        if (lamports <= 0) throw new ApiException(400, "invalid_amount", "The amount must be positive.");
        var bh = await Wrap(() => _rpc.GetLatestBlockhashInfoAsync(ct));
        var b = new TransactionBuilder().SetRecentBlockHash(bh.Blockhash).SetFeePayer(treasury)
            .AddInstruction(SystemProgram.Transfer(treasury.PublicKey, new PublicKey(toWallet), (ulong)lamports));
        if (!string.IsNullOrEmpty(memo)) b.AddInstruction(MemoProgram.NewMemoV2(memo, treasury.PublicKey));
        var tx = b.Build(new List<Account> { treasury });
        return new PreparedTransfer(tx, SignatureOf(tx), bh.LastValidBlockHeight);
    }

    /// <summary>The first signature of a serialized transaction (compact-u16 count, then 64-byte signatures).</summary>
    public static string SignatureOf(byte[] tx)
    {
        // the count is a compact-u16; a payout has 1 or 2 signers, so it is one byte
        if (tx.Length < 65 || tx[0] == 0 || tx[0] > 0x7f) throw new FormatException("Not a signed transaction.");
        return Encoders.Base58.EncodeData(tx[1..65]);
    }

    /// <summary>
    /// Sends a signed transaction. Throws <c>tx_rejected</c> only when the node answered with a preflight rejection
    /// (simulation or signature check failed: the transaction was not forwarded, nothing can land); any other error
    /// (timeout, HTTP failure, unreadable answer) throws <c>tx_send_unknown</c>: it may have been accepted.
    /// </summary>
    public async Task SendPreparedAsync(byte[] tx, CancellationToken ct = default)
    {
        try { await _rpc.SendTransactionAsync(tx, ct); }
        catch (SolanaRpcException ex) when (ex.Code is -32002 or -32003)
        {
            _log.LogWarning("Solana sendTransaction rejected at preflight ({Code}): {Message}", ex.Code, ex.Message);
            throw new ApiException(502, "tx_rejected", "The Solana node rejected the transaction (preflight failed); nothing was sent.");
        }
        catch (Exception ex)
        {
            _log.LogWarning("Solana sendTransaction result unknown ({Type}): {Message}", ex.GetType().Name, ex.Message);
            throw new ApiException(502, "tx_send_unknown", "The transaction may have been sent; its result is not known yet.");
        }
    }

    /// <summary>
    /// Polls the signature until it is confirmed ("confirmed"/"finalized"), failed on chain ("failed"), or the time is
    /// up (null). RPC errors while polling are logged and polling continues; this never throws except on cancellation.
    /// </summary>
    public async Task<string?> WaitForConfirmationAsync(string signature, TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                var status = await _rpc.GetSignatureStatusAsync(signature, ct);
                if (status is "failed" or "confirmed" or "finalized") return status;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning("Solana signature status for {Sig} not read ({Type})", signature, ex.GetType().Name);
            }
            if (DateTime.UtcNow >= deadline) return null;
            await Task.Delay(PollInterval, ct);
        }
    }

    /// <summary>How long a payout send is polled before it is left unconfirmed for the reconciler (tests shorten it).</summary>
    public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>The time between signature status polls (tests shorten it).</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>One status read for the reconciler: the status (or null when unknown), and the current block height.
    /// Throws 502 solana_rpc_error when the node cannot be read.</summary>
    public async Task<(string? status, long blockHeight)> SignatureStatusAsync(string signature, CancellationToken ct = default)
    {
        var status = await Wrap(() => _rpc.GetSignatureStatusAsync(signature, ct));
        var height = await Wrap(() => _rpc.GetBlockHeightAsync(ct));
        return (status, height);
    }

    /// <summary>
    /// Builds and signs a one-of-one NFT mint (SPL mint with 0 decimals, supply 1, Metaplex metadata and master edition, to
    /// the owner's wallet) without sending it. Every step is one transaction, so either all of it lands or none does. The
    /// mint keypair is fresh and thrown away: once the transaction's blockhash expires unlanded, the mint can never be
    /// created at that address, and a new attempt uses a new one.
    /// </summary>
    public async Task<PreparedMint> PrepareNftMintAsync(string ownerWallet, string name, string symbol, string uri, CancellationToken ct = default)
    {
        var treasury = Treasury;
        if (!IsValidAddress(ownerWallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        name = Clip(name, 32);
        symbol = Clip(symbol, 10);
        uri = Clip(uri, 200);
        var mint = new Account();
        var owner = new PublicKey(ownerWallet);
        var rent = await Wrap(() => _rpc.GetMinimumBalanceForRentExemptionAsync((int)TokenProgram.MintAccountDataSize, ct));
        var bh = await Wrap(() => _rpc.GetLatestBlockhashInfoAsync(ct));
        var tx = new TransactionBuilder().SetRecentBlockHash(bh.Blockhash).SetFeePayer(treasury)
            .AddInstruction(SystemProgram.CreateAccount(treasury.PublicKey, mint.PublicKey, (ulong)rent, TokenProgram.MintAccountDataSize, TokenProgram.ProgramIdKey))
            .AddInstruction(TokenProgram.InitializeMint(mint.PublicKey, 0, treasury.PublicKey, treasury.PublicKey))
            .AddInstruction(Metaplex.CreateAtaIdempotent(treasury.PublicKey, owner, mint.PublicKey))
            .AddInstruction(TokenProgram.MintTo(mint.PublicKey, AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint.PublicKey), 1, treasury.PublicKey))
            .AddInstruction(Metaplex.CreateMetadataAccountV3(mint.PublicKey, treasury.PublicKey, treasury.PublicKey, treasury.PublicKey, name, symbol, uri))
            .AddInstruction(Metaplex.CreateMasterEditionV3(mint.PublicKey, treasury.PublicKey, treasury.PublicKey, treasury.PublicKey))
            .Build(new List<Account> { treasury, mint });
        return new PreparedMint(tx, mint.PublicKey.Key, SignatureOf(tx), bh.LastValidBlockHeight);
    }

    /// <summary>V5 M9h: whether a mint account and its Metaplex metadata account exist on chain (a landed mint has both).</summary>
    public async Task<(bool mint, bool metadata)> MintAccountsAsync(string mint, CancellationToken ct = default)
    {
        if (!IsValidAddress(mint)) return (false, false);
        var m = await Wrap(() => _rpc.AccountExistsAsync(mint, ct));
        if (!m) return (false, false);
        var meta = await Wrap(() => _rpc.AccountExistsAsync(Metaplex.MetadataPda(new PublicKey(mint)).Key, ct));
        return (true, meta);
    }

    /// <summary>Mints a one-of-one NFT to the wallet and waits for it (the devnet smoke test; islands use
    /// <see cref="PrepareNftMintAsync"/> so the mint is recorded before it is sent).</summary>
    public async Task<MintedNft> MintNftAsync(string ownerWallet, string name, string symbol, string uri, CancellationToken ct = default)
    {
        var prep = await PrepareNftMintAsync(ownerWallet, name, symbol, uri, ct);
        var sig = await SendAndConfirmAsync(prep.Tx, ct);
        _log.LogInformation("Minted NFT {Mint} to {Owner} ({Sig})", prep.Mint, ownerWallet, sig);
        return new MintedNft(prep.Mint, sig, ownerWallet);
    }

    /// <summary>Moves an NFT held by the treasury (escrow) to the wallet; creates the wallet's token account if needed.</summary>
    public async Task<string> TransferNftFromTreasuryAsync(string mint, string toWallet, CancellationToken ct = default)
    {
        var treasury = Treasury;
        if (!IsValidAddress(toWallet) || !IsValidAddress(mint)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        var mintKey = new PublicKey(mint);
        var to = new PublicKey(toWallet);
        var holdings = await Wrap(() => _rpc.GetTokenAccountsByOwnerAsync(treasury.PublicKey.Key, mint, ct));
        var source = holdings.FirstOrDefault(h => h.Amount >= 1)
                     ?? throw new ApiException(409, "nft_not_in_escrow", "The escrow wallet does not hold this NFT.");
        var blockhash = await Wrap(() => _rpc.GetLatestBlockhashAsync(ct));
        var tx = new TransactionBuilder().SetRecentBlockHash(blockhash).SetFeePayer(treasury)
            .AddInstruction(Metaplex.CreateAtaIdempotent(treasury.PublicKey, to, mintKey))
            .AddInstruction(TokenProgram.Transfer(new PublicKey(source.TokenAccount),
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(to, mintKey), 1, treasury.PublicKey))
            .Build(new List<Account> { treasury });
        return await SendAndConfirmAsync(tx, ct);
    }

    /// <summary>Checks that a transaction moved the NFT from the wallet into the treasury (for escrow listings): the
    /// transaction succeeded, the wallet signed it, and the treasury now holds the mint.</summary>
    public async Task VerifyNftDepositAsync(string signature, string fromWallet, string mint, CancellationToken ct = default)
    {
        var treasury = Treasury.PublicKey.Key;
        if (!IsValidSignature(signature)) throw new ApiException(400, "invalid_signature", "Not a transaction signature.");
        var tx = await Wrap(() => _rpc.GetTransactionAsync(signature, ct));
        if (tx == null) throw new ApiException(409, "tx_not_confirmed", "The transaction is not confirmed yet. Retry in a few seconds.");
        if (tx.Failed) throw new ApiException(400, "tx_failed", "The transaction failed on chain.");
        if (!tx.Signers.Contains(fromWallet)) throw new ApiException(400, "wrong_sender", "The transaction was not signed by your wallet.");
        if (!await OwnsNftAsync(treasury, mint, ct)) throw new ApiException(400, "nft_not_received", "The escrow wallet does not hold the NFT.");
    }

    public async Task<string> RequestAirdropAsync(string address, long lamports, CancellationToken ct = default) =>
        await Wrap(() => _rpc.RequestAirdropAsync(address, lamports, ct));

    /// <summary>Sends a treasury transaction (NFT mint or transfer) and waits up to 45 s for it. Errors after the send
    /// become tx_timeout (it may still land: callers check the chain before retrying).</summary>
    async Task<string> SendAndConfirmAsync(byte[] tx, CancellationToken ct)
    {
        var sig = SignatureOf(tx);
        try { await SendPreparedAsync(tx, ct); }
        catch (ApiException ex) when (ex.Code == "tx_send_unknown")
        {
            throw new ApiException(504, "tx_timeout", $"The transaction {sig} may have been sent but is not confirmed; check it on the explorer.");
        }
        var status = await WaitForConfirmationAsync(sig, TimeSpan.FromSeconds(45), CancellationToken.None);
        if (status == "failed") throw new ApiException(502, "tx_failed", $"The transaction {sig} failed on chain.");
        if (status is "confirmed" or "finalized") return sig;
        throw new ApiException(504, "tx_timeout", $"The transaction {sig} was sent but not confirmed in time; check it on the explorer.");
    }

    /// <summary>RPC errors become 502 solana_rpc_error with a generic message (the node's raw answer is only logged, never
    /// returned to clients).</summary>
    async Task<T> Wrap<T>(Func<Task<T>> call)
    {
        try { return await call(); }
        catch (ApiException) { throw; }
        catch (SolanaRpcException ex)
        {
            _log.LogWarning("Solana RPC error {Code}: {Message}", ex.Code, ex.Message);
            throw new ApiException(502, "solana_rpc_error", "The Solana RPC call failed; try again shortly.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning("Solana RPC unreachable: {Message}", ex.Message);
            throw new ApiException(502, "solana_rpc_error", "The Solana RPC node is unreachable.");
        }
        catch (TaskCanceledException ex)
        {
            _log.LogWarning("Solana RPC timed out: {Message}", ex.Message);
            throw new ApiException(502, "solana_rpc_error", "The Solana RPC node did not answer in time.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            _log.LogWarning("Solana RPC answer unreadable ({Type}): {Message}", ex.GetType().Name, ex.Message);
            throw new ApiException(502, "solana_rpc_error", "The Solana RPC node gave an unreadable answer.");
        }
    }

    static string Clip(string s, int maxBytes)
    {
        s ??= "";
        while (Encoding.UTF8.GetByteCount(s) > maxBytes) s = s[..^1];
        return s;
    }

    public string ExplorerTx(string signature) => $"https://explorer.solana.com/tx/{signature}?cluster={Cluster}";
}
