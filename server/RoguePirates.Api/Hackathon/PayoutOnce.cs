using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

/// <summary>
/// Hackathon: pays a treasury obligation at most once. An obligation is (reason, refId): a wager win, one deposit's refund,
/// one weekly reward. A sent, pending or unconfirmed payout for it means it is done (an unconfirmed one is resolved by
/// signature by <see cref="LedgerService.ReconcileAsync"/>, never re-sent blindly); a failed one (proven not to have moved
/// SOL) is retried, at most <see cref="MaxAttempts"/> times, one minute apart, so an empty treasury only delays payouts.
/// Since the 13 Oct hotfix the database guarantees it (a unique index on (Reason, RefId) over live rows); no lock is held
/// while a transaction is confirmed.
/// </summary>
public static class PayoutOnce
{
    public const int MaxAttempts = 10;
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    public static string Short(string? wallet) =>
        wallet == null ? "" : wallet.Length <= 10 ? wallet : wallet[..4] + "..." + wallet[^4..];

    /// <summary>The payouts recorded for an obligation, newest first.</summary>
    public static Task<List<Payout>> ForAsync(GameDb db, string reason, string refId) =>
        db.Payouts.AsNoTracking().Where(p => p.Reason == reason && p.RefId == refId).OrderByDescending(p => p.Id).ToListAsync();

    /// <summary>
    /// Pays (playerId, wallet, lamports) for (reason, refId) unless that was already done. Returns the latest payout for
    /// the obligation (null when the treasury is off or nothing could be recorded). Never throws for a chain failure.
    /// </summary>
    public static async Task<Payout?> PayAsync(GameDb db, LedgerService ledger, IClock clock, ILogger log,
        Guid? playerId, string wallet, long lamports, string reason, string refId, CancellationToken ct = default)
    {
        var previous = await ForAsync(db, reason, refId);
        var done = previous.FirstOrDefault(p => p.Status != Payout.Failed);
        if (done != null) return done;
        var failed = previous.Where(p => p.Status == Payout.Failed).ToList();
        if (failed.Count >= MaxAttempts) return failed[0];
        if (failed.Count > 0 && failed[0].CreatedAt + RetryAfter > clock.UtcNow) return failed[0];
        try
        {
            return await ledger.PayAsync(playerId, wallet, lamports, reason, refId, ct);
        }
        catch (ApiException ex)
        {
            log.LogWarning("Payout {Reason}/{RefId} of {Lamports} lamports not sent: {Code} {Message}", reason, refId, lamports, ex.Code, ex.Message);
            return previous.FirstOrDefault();
        }
    }
}
