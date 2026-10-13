namespace RoguePirates.Api.Data;

// Hackathon (12 Oct 2026, plan/hackathon/PLAN.md): the tables for every web3 demo feature, added in one migration
// (AddHackathon). Kept deliberately simple. Lamports are stored as long (1 SOL = 1_000_000_000 lamports).
// Statuses are short lowercase strings (see the constants in each class).

/// <summary>A one-time sign-in message issued for a wallet (sign-in with Solana). Used once, valid 5 minutes.</summary>
public sealed class WalletNonce
{
    public long Id { get; set; }
    public string Nonce { get; set; } = "";
    public string Wallet { get; set; } = "";
    /// <summary>The exact message the wallet must sign.</summary>
    public string Message { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

/// <summary>A bearer session issued by wallet sign-in (one per sign-in, so several devices can stay signed in).
/// Guest players keep their single token on <see cref="Player.TokenHash"/>.</summary>
public sealed class PlayerSession
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    /// <summary>SHA-256 (hex) of the bearer token.</summary>
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>A SOL transfer from a player's wallet to the treasury, verified on chain. The signature is unique, so a
/// transfer is credited once. Statuses: verified (recorded, not used yet), consumed (attached to a seat or a purchase),
/// refunding (a refund was decided: it can never be attached again), refunded (the refund was sent).</summary>
public sealed class SolanaDeposit
{
    public const string Verified = "verified", Consumed = "consumed", Refunding = "refunding", Refunded = "refunded";

    public long Id { get; set; }
    public string Signature { get; set; } = "";
    public Guid PlayerId { get; set; }
    public string Wallet { get; set; } = "";
    /// <summary>What the deposit is for: wager, raid_wager, market_buy, generic, ...</summary>
    public string Purpose { get; set; } = "";
    /// <summary>The thing it pays for (wager id, listing id, ...), when known.</summary>
    public string? RefId { get; set; }
    public long Lamports { get; set; }
    public string? Memo { get; set; }
    public string Status { get; set; } = Verified;
    public long Slot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

/// <summary>A SOL payment from the treasury to a wallet (wager win, refund, weekly reward, market sale, owner payout).
/// Statuses (13 Oct hotfix): pending (recorded; once <see cref="Signature"/> is set the transaction may be in flight),
/// sent (confirmed on chain), unconfirmed (sent or maybe sent, result unknown: the reconciler resolves it by signature),
/// failed (proven not to have moved SOL: never sent, rejected by the node's preflight, failed on chain, or its blockhash
/// expired without the signature landing). Only a failed payout may be retried. A unique index on (Reason, RefId) over
/// the rows that are not failed makes "at most one live payout per obligation" a database guarantee.</summary>
public sealed class Payout
{
    public const string Pending = "pending", Sent = "sent", Failed = "failed", Unconfirmed = "unconfirmed";

    public long Id { get; set; }
    public Guid? PlayerId { get; set; }
    public string Wallet { get; set; } = "";
    public long Lamports { get; set; }
    /// <summary>wager_win, wager_refund, weekly_reward, market_sale, admin, ...</summary>
    public string Reason { get; set; } = "";
    public string? RefId { get; set; }
    public string? Signature { get; set; }
    public string Status { get; set; } = Pending;
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    // ---- AddPayoutSafety (13 Oct hotfix) ----
    /// <summary>The last block height at which the signed transaction can land (getLatestBlockhash). Once the chain is
    /// past it and the signature is still unknown, the payout provably never happened and may be retried.</summary>
    public long? LastValidBlockHeight { get; set; }
}

/// <summary>A SOL stake on a 1v1 match or a raid. Both sides deposit to the treasury (escrow); the winner is paid.</summary>
public sealed class Wager
{
    public const string AwaitingDeposits = "awaiting_deposits", Funded = "funded", Settled = "settled",
        Refunded = "refunded", Cancelled = "cancelled";

    public Guid Id { get; set; }
    /// <summary>pvp or raid.</summary>
    public string Kind { get; set; } = "pvp";
    public Guid? LobbyId { get; set; }
    public Guid? MatchId { get; set; }
    public long StakeLamports { get; set; }
    public Guid HostPlayerId { get; set; }
    public Guid? GuestPlayerId { get; set; }
    /// <summary>Against the AI captain: the house matches the stake.</summary>
    public bool VsHouse { get; set; }
    public string? HostDepositSig { get; set; }
    public string? GuestDepositSig { get; set; }
    public string Status { get; set; } = AwaitingDeposits;
    public Guid? WinnerPlayerId { get; set; }
    public long? PayoutId { get; set; }
    public string? PayoutSig { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SettledAt { get; set; }
}

/// <summary>The daily async race: one seeded course per UTC day.</summary>
public sealed class DailyRace
{
    public long Id { get; set; }
    /// <summary>The UTC date (00:00).</summary>
    public DateTime Date { get; set; }
    public int Seed { get; set; }
    public string MapId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>A player's best time on a daily race (one row per player and race).</summary>
public sealed class DailyRaceEntry
{
    public long Id { get; set; }
    public long DailyRaceId { get; set; }
    public Guid PlayerId { get; set; }
    public int TimeMs { get; set; }
    public long? RunId { get; set; }
    /// <summary>The run checks' verdict (shadow mode): pass, flagged, ...</summary>
    public string? Verdict { get; set; }
    public int Attempts { get; set; }
    public DateTime SubmittedAt { get; set; }
}

/// <summary>A weekly kills leaderboard reward (devnet SOL) for one rank.</summary>
public sealed class WeeklyReward
{
    public long Id { get; set; }
    public DateTime WeekStart { get; set; }
    public string MapId { get; set; } = "";
    public int Rank { get; set; }
    public Guid PlayerId { get; set; }
    public long Lamports { get; set; }
    public long? PayoutId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A player's island (an Island NFT on devnet).</summary>
public sealed class Island
{
    public Guid Id { get; set; }
    public Guid OwnerPlayerId { get; set; }
    public string Name { get; set; } = "";
    public string? NftMint { get; set; }
    public string? MintSig { get; set; }
    /// <summary>RUM in the store (exposed to raids).</summary>
    public long RumStored { get; set; }
    /// <summary>RUM in the vault (safe from raids).</summary>
    public long RumVault { get; set; }
    public DateTime LastAccrualAt { get; set; }
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    // ---- AddHackathonIslands ----
    /// <summary>The world-map slot (0-based index into IslandRules.Slots). Unique.</summary>
    public int Slot { get; set; }
    /// <summary>pending (not minted yet, or the mint failed: retry), minted.</summary>
    public string NftStatus { get; set; } = "pending";
    /// <summary>The last mint error, when the mint failed.</summary>
    public string? MintError { get; set; }
    /// <summary>No raids until then (set for 4 hours after a successful raid).</summary>
    public DateTime? ShieldUntil { get; set; }
    // ---- AddIslandDefence ----
    /// <summary>The owner's defence design (JSON: 15 one-minute windows + the guardian boss); null = the default design.</summary>
    public string? DefenceJson { get; set; }
    public DateTime? DefenceUpdatedAt { get; set; }
}

/// <summary>A structure on an island (rum_store, vault, defences, ...).</summary>
public sealed class IslandBuilding
{
    public long Id { get; set; }
    public Guid IslandId { get; set; }
    public string Kind { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A raid on an island (a combat run against its AI-captain defender).</summary>
public sealed class Raid
{
    public const string Started = "started", Won = "won", Lost = "lost", Abandoned = "abandoned";

    public Guid Id { get; set; }
    public Guid AttackerPlayerId { get; set; }
    public Guid IslandId { get; set; }
    public string Result { get; set; } = Started;
    public long RumLooted { get; set; }
    public Guid? WagerId { get; set; }
    public int Seed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    // ---- AddHackathonIslands ----
    /// <summary>The island's owner when the raid started (defence history survives a sale).</summary>
    public Guid DefenderPlayerId { get; set; }
    /// <summary>The fight's length the client reported.</summary>
    public int? Seconds { get; set; }
    // ---- AddIslandDefence ----
    /// <summary>The guardian boss's arrival minute in the design the raid started with (null: a raid from before designs).</summary>
    public int? GuardianMinute { get; set; }
    /// <summary>The last one-minute window the attacker reached (reported with the result).</summary>
    public int? MinuteReached { get; set; }
    /// <summary>Whether the attacker reported sinking the guardian.</summary>
    public bool? GuardianSunk { get; set; }
}

/// <summary>An island NFT listed on the escrow marketplace.</summary>
public sealed class MarketListing
{
    public const string PendingEscrow = "pending_escrow", Active = "active", Sold = "sold", Cancelled = "cancelled";

    public Guid Id { get; set; }
    public Guid? IslandId { get; set; }
    public string NftMint { get; set; } = "";
    public Guid SellerPlayerId { get; set; }
    public long PriceLamports { get; set; }
    public string Status { get; set; } = PendingEscrow;
    public Guid? BuyerPlayerId { get; set; }
    /// <summary>The seller's NFT transfer into escrow.</summary>
    public string? EscrowSig { get; set; }
    /// <summary>The buyer's SOL payment.</summary>
    public string? PaymentSig { get; set; }
    /// <summary>The NFT transfer out of escrow (to the buyer, or back to the seller on cancel).</summary>
    public string? TransferSig { get; set; }
    /// <summary>The SOL payment to the seller.</summary>
    public string? PayoutSig { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    // ---- AddHackathonIslands ----
    /// <summary>The last error of a half-done trade (buy or cancel); cleared when it completes.</summary>
    public string? Error { get; set; }
}
