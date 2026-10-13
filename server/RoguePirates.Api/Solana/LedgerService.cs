using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Solana;

/// <summary>
/// Hackathon: SOL in and out of the treasury, recorded in the database. Deposits: a player's transfer to the treasury,
/// verified on chain and credited once (unique signature). Payouts: the treasury paying a wallet.
///
/// Payout safety (13 Oct hotfix): a payout row is inserted first (a unique index allows one live row per (reason, refId)),
/// the transaction is signed and its signature and last valid block height are saved BEFORE it is sent; any error after
/// the send leaves the payout "unconfirmed" (never "failed"), and <see cref="ReconcileAsync"/> (on the wager sweep timer)
/// resolves pending and unconfirmed payouts by signature: sent when it landed, failed only once the blockhash has expired
/// and the signature provably never landed. No lock is held while a transaction is polled.
/// </summary>
public sealed partial class LedgerService
{
    /// <summary>Serializes deposit recording in this (single) API instance (the unique signature index is the real guard).</summary>
    static readonly SemaphoreSlim Lock = new(1, 1);

    /// <summary>A pending payout older than this is the reconciler's (its request died between insert and send).</summary>
    public static readonly TimeSpan StalePending = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Deposit purposes only the server creates (each with its own amount, memo and seat checks): the generic
    /// /api/deposits/verify endpoint refuses them, and any purpose starting with these words.
    /// </summary>
    public static readonly string[] ReservedPurposePrefixes = { "wager", "raid", "market", "island", "buy", "refund", "payout" };

    readonly GameDb _db;
    readonly SolanaService _sol;
    readonly IClock _clock;
    readonly ILogger<LedgerService> _log;

    public LedgerService(GameDb db, SolanaService sol, IClock clock, ILogger<LedgerService> log)
    {
        _db = db;
        _sol = sol;
        _clock = clock;
        _log = log;
    }

    [GeneratedRegex("^[a-z0-9_]{1,32}$")]
    private static partial Regex PurposeShape();

    public static bool IsReservedPurpose(string purpose) =>
        ReservedPurposePrefixes.Any(p => purpose.StartsWith(p, StringComparison.Ordinal));

    public static object DepositView(SolanaDeposit d, bool alreadyVerified) => new
    {
        depositId = d.Id, signature = d.Signature, wallet = d.Wallet, purpose = d.Purpose, refId = d.RefId,
        lamports = d.Lamports, sol = d.Lamports / (double)SolanaService.LamportsPerSol, memo = d.Memo, status = d.Status,
        slot = d.Slot, createdAt = d.CreatedAt, alreadyVerified,
    };

    public static object PayoutView(Payout p, string cluster) => new
    {
        payoutId = p.Id, wallet = p.Wallet, lamports = p.Lamports, sol = p.Lamports / (double)SolanaService.LamportsPerSol,
        reason = p.Reason, refId = p.RefId, status = p.Status, signature = p.Signature, error = p.Error,
        explorerUrl = p.Signature == null ? null : $"https://explorer.solana.com/tx/{p.Signature}?cluster={cluster}",
        createdAt = p.CreatedAt, sentAt = p.SentAt,
    };

    /// <summary>
    /// The client-facing deposit check (POST /api/deposits/verify): only non-reserved purposes ("generic" by default).
    /// Wager, market and raid deposits are recorded by their own endpoints, which check amount, memo and seat.
    /// </summary>
    public Task<(SolanaDeposit deposit, bool already)> VerifyClientDepositAsync(Player player, string? signature, string? purpose,
        string? refId, long minLamports, string? expectedMemo, CancellationToken ct = default)
    {
        var p = string.IsNullOrWhiteSpace(purpose) ? "generic" : purpose.Trim().ToLowerInvariant();
        if (IsReservedPurpose(p))
            throw new ApiException(400, "purpose_reserved",
                $"'{p}' deposits are made through their own endpoint (wagers: POST /api/wagers/{{lobbyId}}/deposit, market: POST /api/market/{{id}}/buy).");
        return VerifyDepositAsync(player, signature, p, refId, minLamports, expectedMemo, ct);
    }

    /// <summary>
    /// Verifies and records a transfer from the player's wallet to the treasury. The same player re-sending the same
    /// signature for the same purpose and refId gets the stored deposit back (alreadyVerified); any other reuse is 409.
    /// A stored deposit returned this way was checked when it was recorded, not now: system callers re-check its amount,
    /// memo, wallet and status against the current state under their own lock before using it.
    /// </summary>
    public async Task<(SolanaDeposit deposit, bool already)> VerifyDepositAsync(Player player, string? signature, string? purpose,
        string? refId = null, long minLamports = 1, string? expectedMemo = null, CancellationToken ct = default, DateTime? notBefore = null)
    {
        if (player.WalletAddress == null)
            throw new ApiException(400, "wallet_required", "Sign in with a wallet first (POST /api/auth/wallet/verify).");
        signature = (signature ?? "").Trim();
        purpose = string.IsNullOrWhiteSpace(purpose) ? "generic" : purpose.Trim().ToLowerInvariant();
        if (!PurposeShape().IsMatch(purpose)) throw new ApiException(400, "invalid_purpose", "purpose: 1-32 of a-z 0-9 _.");
        if (refId is { Length: > 64 }) throw new ApiException(400, "invalid_ref", "refId is at most 64 characters.");
        if (!SolanaService.IsValidSignature(signature)) throw new ApiException(400, "invalid_signature", "Not a transaction signature.");
        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");

        var existing = await _db.SolanaDeposits.FirstOrDefaultAsync(d => d.Signature == signature, ct);
        if (existing != null) return Reuse(existing, player, purpose, refId);

        var verified = await _sol.VerifyTransferToTreasuryAsync(signature, player.WalletAddress, Math.Max(1, minLamports), expectedMemo, ct, notBefore);

        await Lock.WaitAsync(ct);
        try
        {
            existing = await _db.SolanaDeposits.FirstOrDefaultAsync(d => d.Signature == signature, ct);
            if (existing != null) return Reuse(existing, player, purpose, refId);
            var deposit = new SolanaDeposit
            {
                Signature = signature, PlayerId = player.Id, Wallet = player.WalletAddress, Purpose = purpose, RefId = refId,
                Lamports = verified.Lamports, Memo = verified.Memo is { Length: > 256 } m ? m[..256] : verified.Memo,
                Status = SolanaDeposit.Verified, Slot = verified.Slot, CreatedAt = _clock.UtcNow,
            };
            _db.SolanaDeposits.Add(deposit);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                _db.Entry(deposit).State = EntityState.Detached;
                throw new ApiException(409, "signature_used", "This transaction was already used.");
            }
            _log.LogInformation("Deposit {Sig}: {Lamports} lamports from {Wallet} for {Purpose}", signature, deposit.Lamports, deposit.Wallet, purpose);
            return (deposit, false);
        }
        finally { Lock.Release(); }
    }

    static (SolanaDeposit, bool) Reuse(SolanaDeposit existing, Player player, string purpose, string? refId)
    {
        if (existing.PlayerId == player.Id && existing.Wallet == player.WalletAddress && existing.Purpose == purpose && existing.RefId == refId)
            return (existing, true);
        throw new ApiException(409, "signature_used", "This transaction was already used.");
    }

    /// <summary>
    /// Records and sends a payout from the treasury. With a refId, at most one live (not failed) payout exists for
    /// (reason, refId): when one already exists it is returned and nothing is sent. Returns the payout: sent, failed
    /// (nothing moved: safe to retry) or pending/unconfirmed (maybe sent: the reconciler decides). Never throws once
    /// the row is recorded.
    /// </summary>
    public async Task<Payout> PayAsync(Guid? playerId, string wallet, long lamports, string reason, string? refId = null, CancellationToken ct = default)
    {
        if (!SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        if (lamports <= 0) throw new ApiException(400, "invalid_amount", "The amount must be positive.");
        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");

        var payout = new Payout
        {
            PlayerId = playerId, Wallet = wallet, Lamports = lamports, Reason = reason, RefId = refId,
            Status = Payout.Pending, CreatedAt = _clock.UtcNow,
        };
        _db.Payouts.Add(payout);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) when (refId != null)
        {
            // the unique index: another live payout for this obligation exists (a concurrent or earlier pay)
            _db.Entry(payout).State = EntityState.Detached;
            var live = await _db.Payouts.Where(p => p.Reason == reason && p.RefId == refId && p.Status != Payout.Failed)
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync(CancellationToken.None);
            if (live != null)
            {
                _log.LogWarning("Payout {Reason}/{RefId}: a live payout {Id} ({Status}) exists; not paying again", reason, refId, live.Id, live.Status);
                return live;
            }
            throw;
        }

        // 1. sign (nothing sent yet: a failure here is a real failure)
        PreparedTransfer prep;
        try
        {
            prep = await _sol.PrepareSolTransferAsync(wallet, lamports, $"rogue-pirates:{reason}:{payout.Id}", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return await FinishAsync(payout, Payout.Failed, ex is ApiException a ? a.Message : "The transfer could not be prepared.");
        }

        // 2. the signature and its deadline are stored before the send, so whatever happens next can be resolved
        payout.Signature = prep.Signature;
        payout.LastValidBlockHeight = prep.LastValidBlockHeight;
        await _db.SaveChangesAsync(CancellationToken.None);

        // 3. send: only a preflight rejection proves nothing was forwarded
        try { await _sol.SendPreparedAsync(prep.Tx, CancellationToken.None); }
        catch (ApiException ex) when (ex.Code == "tx_rejected")
        {
            return await FinishAsync(payout, Payout.Failed, ex.Message);
        }
        catch (Exception)
        {
            return await FinishAsync(payout, Payout.Unconfirmed, "Sent, result unknown; it is checked by signature before any retry.");
        }

        // 4. confirm (no lock held); unknown after the timeout stays unconfirmed
        string? status;
        try { status = await _sol.WaitForConfirmationAsync(prep.Signature, _sol.ConfirmTimeout, CancellationToken.None); }
        catch (Exception) { status = null; }
        return status switch
        {
            "confirmed" or "finalized" => await FinishAsync(payout, Payout.Sent, null),
            "failed" => await FinishAsync(payout, Payout.Failed, "The transaction failed on chain; no SOL moved."),
            _ => await FinishAsync(payout, Payout.Unconfirmed, "Sent but not confirmed in time; it is checked by signature before any retry."),
        };
    }

    async Task<Payout> FinishAsync(Payout payout, string status, string? error)
    {
        payout.Status = status;
        payout.Error = error is { Length: > 512 } ? error[..512] : error;
        if (status == Payout.Sent) payout.SentAt = _clock.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);
        if (status == Payout.Sent)
            _log.LogInformation("Payout {Id}: {Lamports} lamports to {Wallet} ({Reason}) {Sig}", payout.Id, payout.Lamports, payout.Wallet, payout.Reason, payout.Signature);
        else
            _log.LogWarning("Payout {Id} ({Reason}/{RefId}) {Status}: {Error}", payout.Id, payout.Reason, payout.RefId, status, error);
        return payout;
    }

    /// <summary>
    /// Resolves pending and unconfirmed payouts by signature (the sweep timer runs it): confirmed on chain: sent; failed on
    /// chain: failed; unknown after the blockhash expired: failed (it can never land, a retry is safe); otherwise left as
    /// is. A pending payout without a signature older than <see cref="StalePending"/> was never sent: failed. Payouts from
    /// before the hotfix without a signature stay unconfirmed for the owner to check by hand. Returns how many changed.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        if (!_sol.TreasuryOn) return 0;
        var now = _clock.UtcNow;
        var staleBefore = now - StalePending;
        var open = await _db.Payouts
            .Where(p => (p.Status == Payout.Pending || p.Status == Payout.Unconfirmed) && p.CreatedAt <= staleBefore)
            .OrderBy(p => p.Id).Take(50).ToListAsync(ct);
        var changed = 0;
        foreach (var p in open)
        {
            if (p.Signature == null)
            {
                if (p.Status == Payout.Pending && p.LastValidBlockHeight == null && p.Error == null)
                {
                    // inserted but never signed (the signature is saved before any send)
                    await FinishAsync(p, Payout.Failed, "Never sent (the request stopped before signing).");
                    changed++;
                }
                continue;
            }
            try
            {
                var (status, height) = await _sol.SignatureStatusAsync(p.Signature, ct);
                if (status is "confirmed" or "finalized") { await FinishAsync(p, Payout.Sent, null); changed++; }
                else if (status == "failed") { await FinishAsync(p, Payout.Failed, "The transaction failed on chain; no SOL moved."); changed++; }
                else if (status == null && p.LastValidBlockHeight is { } last && height > last)
                {
                    // re-read after the height: a status that appeared meanwhile wins
                    var (again, _) = await _sol.SignatureStatusAsync(p.Signature, ct);
                    if (again != null) continue;
                    await FinishAsync(p, Payout.Failed, "The blockhash expired and the transaction never landed; no SOL moved.");
                    changed++;
                }
            }
            catch (ApiException ex)
            {
                _log.LogWarning("Payout {Id} not reconciled: {Code}", p.Id, ex.Code);
            }
        }
        return changed;
    }
}
