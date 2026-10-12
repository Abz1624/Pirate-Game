using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Solana;

/// <summary>
/// Hackathon: SOL in and out of the treasury, recorded in the database. Deposits: a player's transfer to the treasury,
/// verified on chain and credited once (unique signature). Payouts: the treasury paying a wallet, recorded before it is
/// sent (pending, then sent or failed). Later features (wagers, market, rewards) build on these two calls.
/// </summary>
public sealed partial class LedgerService
{
    /// <summary>Serializes deposit crediting and payouts in this (single) API instance.</summary>
    static readonly SemaphoreSlim Lock = new(1, 1);

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
    /// Verifies and records a transfer from the player's wallet to the treasury. The same player re-sending the same
    /// signature for the same purpose gets the stored deposit back (alreadyVerified); any other reuse is 409.
    /// </summary>
    public async Task<(SolanaDeposit deposit, bool already)> VerifyDepositAsync(Player player, string? signature, string? purpose,
        string? refId = null, long minLamports = 1, string? expectedMemo = null, CancellationToken ct = default)
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

        var verified = await _sol.VerifyTransferToTreasuryAsync(signature, player.WalletAddress, Math.Max(1, minLamports), expectedMemo, ct);

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
            catch (DbUpdateException) { throw new ApiException(409, "signature_used", "This transaction was already used."); }
            _log.LogInformation("Deposit {Sig}: {Lamports} lamports from {Wallet} for {Purpose}", signature, deposit.Lamports, deposit.Wallet, purpose);
            return (deposit, false);
        }
        finally { Lock.Release(); }
    }

    static (SolanaDeposit, bool) Reuse(SolanaDeposit existing, Player player, string purpose, string? refId)
    {
        if (existing.PlayerId == player.Id && existing.Purpose == purpose && existing.RefId == refId) return (existing, true);
        throw new ApiException(409, "signature_used", "This transaction was already used.");
    }

    /// <summary>Records and sends a payout from the treasury. Returns the payout (status sent, or failed with the error).</summary>
    public async Task<Payout> PayAsync(Guid? playerId, string wallet, long lamports, string reason, string? refId = null, CancellationToken ct = default)
    {
        if (!SolanaService.IsValidAddress(wallet)) throw new ApiException(400, "invalid_wallet", "Not a Solana address.");
        if (lamports <= 0) throw new ApiException(400, "invalid_amount", "The amount must be positive.");
        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");
        await Lock.WaitAsync(ct);
        try
        {
            var payout = new Payout
            {
                PlayerId = playerId, Wallet = wallet, Lamports = lamports, Reason = reason, RefId = refId,
                Status = Payout.Pending, CreatedAt = _clock.UtcNow,
            };
            _db.Payouts.Add(payout);
            await _db.SaveChangesAsync(ct);
            try
            {
                payout.Signature = await _sol.SendSolAsync(wallet, lamports, $"rogue-pirates:{reason}:{payout.Id}", ct);
                payout.Status = Payout.Sent;
                payout.SentAt = _clock.UtcNow;
                _log.LogInformation("Payout {Id}: {Lamports} lamports to {Wallet} ({Reason}) {Sig}", payout.Id, lamports, wallet, reason, payout.Signature);
            }
            catch (ApiException ex)
            {
                // a timeout means sent but unconfirmed: it may still land, so it is not marked failed
                payout.Status = ex.Code == "tx_timeout" ? "unconfirmed" : Payout.Failed;
                payout.Error = ex.Message.Length > 512 ? ex.Message[..512] : ex.Message;
                _log.LogWarning("Payout {Id} failed: {Error}", payout.Id, ex.Message);
            }
            await _db.SaveChangesAsync(CancellationToken.None);
            return payout;
        }
        finally { Lock.Release(); }
    }
}
