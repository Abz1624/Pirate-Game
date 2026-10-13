using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

/// <summary>
/// Hackathon island NFT marketplace with the treasury as escrow. List: the seller has already sent the NFT to the
/// treasury; the server checks it and opens the listing (the island is locked: no raids, collects or builds). Buy: the
/// buyer pays the price to the treasury with memo buy:&lt;listingId&gt;; the server moves the NFT to the buyer, the
/// island to the buyer in the database, and pays the seller. Cancel returns the NFT. A step that fails leaves a clear
/// status (paid / cancelling, with the error) and POST /api/market/{id}/complete or /cancel finishes it.
/// </summary>
public sealed class MarketService
{
    public const string Paid = "paid", Cancelling = "cancelling";
    public const string DepositPurpose = "market_buy", SaleReason = "market_sale", RefundReason = "market_refund";
    public const long MinPriceLamports = 1_000_000, MaxPriceLamports = 100 * SolanaService.LamportsPerSol; // 0.001 .. 100 SOL
    /// <summary>Stored in TransferSig when a retry finds the NFT already at its destination (the first send timed out).</summary>
    const string TransferredUnknownSig = "confirmed-by-holding";

    readonly GameDb _db;
    readonly IslandService _islands;
    readonly LedgerService _ledger;
    readonly SolanaService _sol;
    readonly IClock _clock;
    readonly ApiSettings _settings;
    readonly ILogger<MarketService> _log;

    public MarketService(GameDb db, IslandService islands, LedgerService ledger, SolanaService sol, IClock clock, ApiSettings settings,
        ILogger<MarketService> log)
    {
        _settings = settings;
        _db = db;
        _islands = islands;
        _ledger = ledger;
        _sol = sol;
        _clock = clock;
        _log = log;
    }

    public static string Memo(Guid listingId) => "buy:" + listingId;

    // ---------- views ----------

    async Task<object> ViewAsync(MarketListing l)
    {
        var island = l.IslandId == null ? null : await _islands.TryLoadAsync(l.IslandId.Value);
        var ids = new[] { l.SellerPlayerId, l.BuyerPlayerId ?? Guid.Empty };
        var players = await _db.Players.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var record = island == null ? null : await _islands.DefenceRecordAsync(island.Island.Id);
        return View(l, island, players, record);
    }

    object View(MarketListing l, IslandState? island, IReadOnlyDictionary<Guid, Player> players, object? record)
    {
        object? Who(Guid? id) => id is { } g && players.TryGetValue(g, out var p)
            ? new { playerId = p.Id, name = p.Name, wallet = p.WalletAddress, walletShort = IslandRules.ShortWallet(p.WalletAddress) }
            : null;
        string? Tx(string? sig) => SolanaService.IsValidSignature(sig) ? _sol.ExplorerTx(sig!) : null;
        var slot = island == null ? null : IslandRules.Slots[island.Island.Slot];
        return new
        {
            listingId = l.Id, status = l.Status, priceLamports = l.PriceLamports, priceSol = l.PriceLamports / (double)SolanaService.LamportsPerSol,
            nftMint = l.NftMint, memo = Memo(l.Id), treasury = _sol.TreasuryAddress,
            island = island == null ? null : new
            {
                islandId = island.Island.Id, name = island.Island.Name, slot = island.Island.Slot, slotName = slot!.Name, region = slot.Region,
                level = island.Island.Level, levels = island.Levels,
                ratePerHour = IslandRules.RatePerHour(island.LevelOf(IslandRules.RumStore)),
                defenceRecord = record,
            },
            seller = Who(l.SellerPlayerId), buyer = Who(l.BuyerPlayerId),
            escrowSignature = l.EscrowSig, paymentSignature = l.PaymentSig, transferSignature = l.TransferSig, payoutSignature = l.PayoutSig,
            transferExplorerUrl = Tx(l.TransferSig), payoutExplorerUrl = Tx(l.PayoutSig),
            error = l.Error, createdAt = l.CreatedAt, closedAt = l.ClosedAt,
        };
    }

    public async Task<object> ActiveAsync()
    {
        var listings = await _db.MarketListings.AsNoTracking().Where(l => l.Status == MarketListing.Active)
            .OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync();
        var list = new List<object>();
        foreach (var l in listings) list.Add(await ViewAsync(l));
        return new { treasury = _sol.TreasuryAddress, cluster = _sol.Cluster, listings = list };
    }

    public async Task<object> MineAsync(Player player)
    {
        var listings = await _db.MarketListings.AsNoTracking()
            .Where(l => l.SellerPlayerId == player.Id || l.BuyerPlayerId == player.Id)
            .OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync();
        var list = new List<object>();
        foreach (var l in listings) list.Add(await ViewAsync(l));
        return new { listings = list };
    }

    public async Task<object> GetAsync(Guid id) => await ViewAsync(await FindAsync(id));

    async Task<MarketListing> FindAsync(Guid id) =>
        await _db.MarketListings.FirstOrDefaultAsync(l => l.Id == id) ?? throw new ApiException(404, "listing_not_found", "No such listing.");

    // ---------- list ----------

    public async Task<object> ListAsync(Player player, Guid islandId, long priceLamports, string? signature)
    {
        IslandService.RequireWallet(player);
        if (priceLamports < MinPriceLamports || priceLamports > MaxPriceLamports)
            throw new ApiException(400, "invalid_price", $"priceLamports is {MinPriceLamports}..{MaxPriceLamports}.");
        signature = (signature ?? "").Trim();
        if (!SolanaService.IsValidSignature(signature))
            throw new ApiException(400, "invalid_signature", "Send the island NFT to the treasury first, then pass that transaction's signature.");

        await IslandLocks.Market.WaitAsync();
        try
        {
            var s = await _islands.LoadAsync(islandId);
            if (s.Island.OwnerPlayerId != player.Id) throw new ApiException(403, "not_your_island", "This is not your island.");
            if (s.Island.NftStatus != IslandService.NftMinted || s.Island.NftMint == null)
                throw new ApiException(409, "nft_not_minted", "The island's NFT is not minted yet (POST /api/islands/{id}/mint).");
            if (await _islands.IsListedAsync(islandId)) throw new ApiException(409, "already_listed", "The island is already listed.");
            if (await _db.MarketListings.AnyAsync(l => l.EscrowSig == signature))
                throw new ApiException(409, "signature_used", "This transaction was already used for a listing.");
            await _sol.VerifyNftDepositAsync(signature, player.WalletAddress!, s.Island.NftMint);
            var listing = new MarketListing
            {
                Id = Guid.NewGuid(), IslandId = islandId, NftMint = s.Island.NftMint, SellerPlayerId = player.Id,
                PriceLamports = priceLamports, Status = MarketListing.Active, EscrowSig = signature, CreatedAt = _clock.UtcNow,
            };
            _db.MarketListings.Add(listing);
            await _db.SaveChangesAsync();
            _log.LogInformation("Listing {Id}: island {Island} for {Price} lamports by {Seller}", listing.Id, islandId, priceLamports, player.Id);
            return await ViewAsync(listing);
        }
        finally { IslandLocks.Market.Release(); }
    }

    // ---------- buy ----------

    /// <summary>
    /// A purchase (13 Oct hotfix): the buyer's transfer (memo buy:&lt;listingId&gt;, any amount) is verified and recorded
    /// first; then, under the market lock, the stored deposit is checked against the listing as it is NOW (still active,
    /// not the buyer's own, right wallet and memo, at least the price, not used before). A payment that cannot buy the
    /// listing is refunded in full (reason market_refund); an overpayment above the price is refunded too.
    /// </summary>
    public async Task<object> BuyAsync(Player buyer, Guid listingId, string? signature)
    {
        IslandService.RequireWallet(buyer);
        var first = await FindAsync(listingId);
        if (first.SellerPlayerId == buyer.Id) throw new ApiException(400, "own_listing", "You cannot buy your own listing (cancel it instead).");
        if (first.Status == Paid && first.BuyerPlayerId == buyer.Id && first.PaymentSig == (signature ?? "").Trim())
        {
            await IslandLocks.Market.WaitAsync();
            try { return await CompleteLockedAsync(await FindAsync(listingId)); } // a retry of the same purchase
            finally { IslandLocks.Market.Release(); }
        }

        // the chain check and record, outside the lock; any amount, so a short payment is recorded and refunded
        var (recorded, _) = await _ledger.VerifyDepositAsync(buyer, signature, DepositPurpose, listingId.ToString(), 1, Memo(listingId));

        ApiException? refuse;
        SolanaDeposit d;
        MarketListing listing;
        await IslandLocks.Market.WaitAsync();
        try
        {
            listing = await FindAsync(listingId);
            await _db.Entry(listing).ReloadAsync();
            d = await _db.SolanaDeposits.FirstAsync(x => x.Id == recorded.Id);
            await _db.Entry(d).ReloadAsync();
            if (listing.Status == Paid && listing.PaymentSig == d.Signature && listing.BuyerPlayerId == buyer.Id)
                return await CompleteLockedAsync(listing); // a retry of the same purchase
            refuse = PaymentProblem(d, listing, buyer);
            if (refuse == null)
            {
                d.Status = SolanaDeposit.Consumed;
                d.ConsumedAt = _clock.UtcNow;
                listing.Status = Paid;
                listing.BuyerPlayerId = buyer.Id;
                listing.PaymentSig = d.Signature;
                await _db.SaveChangesAsync();
                _log.LogInformation("Listing {Id}: paid by {Buyer} ({Sig}, {Lamports} lamports)", listingId, buyer.Id, d.Signature, d.Lamports);
                return await CompleteLockedAsync(listing);
            }
            if (d.Status is SolanaDeposit.Verified)
            {
                d.Status = SolanaDeposit.Refunding;
                d.ConsumedAt = _clock.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        finally { IslandLocks.Market.Release(); }

        // refused: the whole payment goes back (once; a deposit used for a purchase is never refunded here)
        var refunded = false;
        if (d.Status is SolanaDeposit.Refunding or SolanaDeposit.Refunded)
            refunded = await RefundDepositAsync(d) is { Status: Payout.Sent };
        throw new ApiException(refuse.Status, refuse.Code, refuse.Message + (d.Status is SolanaDeposit.Refunding or SolanaDeposit.Refunded
            ? refunded ? " Your payment was refunded to your wallet." : " Your payment is being refunded to your wallet."
            : ""));
    }

    /// <summary>Why a recorded payment cannot buy the listing now (null when it can).</summary>
    static ApiException? PaymentProblem(SolanaDeposit d, MarketListing listing, Player buyer)
    {
        if (d.Status != SolanaDeposit.Verified)
            return new ApiException(409, "payment_used", "This payment was already used or refunded.");
        if (d.PlayerId != buyer.Id || d.Wallet != buyer.WalletAddress || d.Purpose != DepositPurpose || d.RefId != listing.Id.ToString())
            return new ApiException(409, "signature_used", "This transaction was already used.");
        if (d.Memo == null || !d.Memo.Contains(Memo(listing.Id), StringComparison.Ordinal))
            return new ApiException(400, "memo_mismatch", "The transaction's memo does not carry the expected reference.");
        if (listing.Status != MarketListing.Active)
            return new ApiException(409, "listing_not_active", $"The listing is {listing.Status}.");
        if (listing.SellerPlayerId == buyer.Id)
            return new ApiException(400, "own_listing", "You cannot buy your own listing (cancel it instead).");
        if (d.Lamports < listing.PriceLamports)
            return new ApiException(400, "amount_too_low", $"The transfer is {d.Lamports} lamports; the price is {listing.PriceLamports}.");
        return null;
    }

    static string DepositRef(long depositId) => "dep:" + depositId;
    static string OverpaymentRef(long depositId) => "over:" + depositId;

    /// <summary>Refunds a refused payment in full (once). Marks the deposit refunded when the payout is sent.</summary>
    async Task<Payout?> RefundDepositAsync(SolanaDeposit d)
    {
        var p = await PayoutOnce.PayAsync(_db, _ledger, _clock, _log, d.PlayerId, d.Wallet, d.Lamports, RefundReason, DepositRef(d.Id));
        if (p is { Status: Payout.Sent } && d.Status != SolanaDeposit.Refunded)
        {
            d.Status = SolanaDeposit.Refunded;
            await _db.SaveChangesAsync();
        }
        return p;
    }

    /// <summary>Refunds what a purchase paid above the price (once).</summary>
    async Task RefundOverpaymentAsync(MarketListing listing)
    {
        if (listing.PaymentSig == null) return;
        var d = await _db.SolanaDeposits.AsNoTracking().FirstOrDefaultAsync(x => x.Signature == listing.PaymentSig);
        if (d == null || d.Lamports <= listing.PriceLamports) return;
        await PayoutOnce.PayAsync(_db, _ledger, _clock, _log, d.PlayerId, d.Wallet, d.Lamports - listing.PriceLamports, RefundReason, OverpaymentRef(d.Id));
    }

    /// <summary>
    /// Sweep (on the wager sweep timer): retries refunds of refused payments, refunds payments recorded but never used
    /// (the buy request died) after a grace period, and retries overpayment refunds of paid listings.
    /// </summary>
    public async Task<int> SweepRefundsAsync()
    {
        if (!_sol.TreasuryOn) return 0;
        var graceBefore = _clock.UtcNow - WagerService.AttachGrace;
        var ids = await _db.SolanaDeposits.AsNoTracking()
            .Where(x => x.Purpose == DepositPurpose && (x.Status == SolanaDeposit.Refunding
                        || (x.Status == SolanaDeposit.Verified && x.CreatedAt <= graceBefore)))
            .OrderBy(x => x.Id).Take(50).Select(x => x.Id).ToListAsync();
        var n = 0;
        foreach (var id in ids)
        {
            SolanaDeposit? refund = null;
            await IslandLocks.Market.WaitAsync();
            try
            {
                var d = await _db.SolanaDeposits.FirstAsync(x => x.Id == id);
                await _db.Entry(d).ReloadAsync();
                if (d.Status == SolanaDeposit.Verified)
                {
                    if (await _db.MarketListings.AnyAsync(l => l.PaymentSig == d.Signature))
                    {
                        d.Status = SolanaDeposit.Consumed; // used by a purchase that did not mark it
                        d.ConsumedAt ??= _clock.UtcNow;
                    }
                    else
                    {
                        d.Status = SolanaDeposit.Refunding;
                        d.ConsumedAt ??= _clock.UtcNow;
                    }
                    await _db.SaveChangesAsync();
                }
                if (d.Status == SolanaDeposit.Refunding) refund = d;
            }
            finally { IslandLocks.Market.Release(); }
            if (refund != null && await RefundDepositAsync(refund) != null) n++;
        }

        var since = _clock.UtcNow.AddDays(-7);
        var paid = await _db.MarketListings.Where(l => l.PaymentSig != null && (l.Status == Paid || (l.Status == MarketListing.Sold && l.ClosedAt >= since)))
            .ToListAsync();
        foreach (var l in paid) await RefundOverpaymentAsync(l);
        return n;
    }

    /// <summary>Finishes a paid trade (buyer or seller): the NFT to the buyer, the island to the buyer, the seller paid.</summary>
    public async Task<object> CompleteAsync(Player player, Guid listingId)
    {
        await IslandLocks.Market.WaitAsync();
        try
        {
            var listing = await FindAsync(listingId);
            if (player.Id != listing.SellerPlayerId && player.Id != listing.BuyerPlayerId)
                throw new ApiException(403, "not_your_trade", "Only the buyer or the seller can finish this trade.");
            if (listing.Status == Cancelling) return await CancelLockedAsync(listing);
            if (listing.Status != Paid) return await ViewAsync(listing);
            return await CompleteLockedAsync(listing);
        }
        finally { IslandLocks.Market.Release(); }
    }

    async Task<object> CompleteLockedAsync(MarketListing listing)
    {
        var buyer = await _db.Players.AsNoTracking().FirstAsync(p => p.Id == listing.BuyerPlayerId);
        var seller = await _db.Players.AsNoTracking().FirstAsync(p => p.Id == listing.SellerPlayerId);
        var errors = new List<string>();

        // 1. the NFT to the buyer, then the island in the database
        if (listing.TransferSig == null)
        {
            try
            {
                listing.TransferSig = await _sol.OwnsNftAsync(buyer.WalletAddress!, listing.NftMint)
                    ? TransferredUnknownSig
                    : await _sol.TransferNftFromTreasuryAsync(listing.NftMint, buyer.WalletAddress!);
            }
            catch (ApiException ex) { errors.Add($"nft_transfer: {ex.Code}: {ex.Message}"); }
        }
        if (listing.TransferSig != null && listing.IslandId is { } islandId)
        {
            await IslandLocks.Economy.WaitAsync();
            try
            {
                var state = await _islands.TryLoadAsync(islandId);
                var island = state?.Island;
                await using var tx = await _db.Database.BeginTransactionAsync();
                if (island != null && island.OwnerPlayerId != buyer.Id)
                {
                    if (!_settings.IslandSaleCarriesRum)
                    {
                        // V5 M9h switch ISLAND_SALE_CARRIES_RUM=false (I26.2): RUM value never moves to a SOL buyer. The
                        // island's store and vault RUM go to the seller and its buildings go back to their start levels.
                        var kept = KeepRumWithSeller(state!, _clock.UtcNow);
                        await _db.SaveChangesAsync();
                        if (kept > 0) await _islands.AddRumAsync(listing.SellerPlayerId, kept);
                        _log.LogInformation("Island {Island} sold: {Rum} RUM kept by the seller {Seller}, buildings reset", islandId, kept, listing.SellerPlayerId);
                    }
                    island.OwnerPlayerId = buyer.Id;
                    _log.LogInformation("Island {Island} now belongs to {Buyer} (listing {Id})", islandId, buyer.Id, listing.Id);
                }
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            finally { IslandLocks.Economy.Release(); }
        }

        // 2. the seller's payment (never twice: a sent, pending or unconfirmed payout for this listing counts)
        if (listing.PayoutSig == null)
        {
            var refId = listing.Id.ToString();
            var previous = await _db.Payouts.Where(p => p.Reason == SaleReason && p.RefId == refId && p.Status != Payout.Failed)
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            if (previous is { Status: Payout.Sent }) listing.PayoutSig = previous.Signature;
            else if (previous != null) errors.Add($"seller_payout: payout {previous.Id} is {previous.Status}; check it on the explorer before paying again.");
            else
            {
                try
                {
                    var payout = await _ledger.PayAsync(seller.Id, seller.WalletAddress!, listing.PriceLamports, SaleReason, refId);
                    if (payout.Status == Payout.Sent) listing.PayoutSig = payout.Signature;
                    else errors.Add($"seller_payout: {payout.Status}: {payout.Error}");
                }
                catch (ApiException ex) { errors.Add($"seller_payout: {ex.Code}: {ex.Message}"); }
            }
        }

        // 3. an overpayment goes back to the buyer (once)
        await RefundOverpaymentAsync(listing);

        if (listing.TransferSig != null && listing.PayoutSig != null)
        {
            listing.Status = MarketListing.Sold;
            listing.ClosedAt = _clock.UtcNow;
            listing.Error = null;
        }
        else listing.Error = Clip(string.Join(" | ", errors), 256);
        await _db.SaveChangesAsync();
        return await ViewAsync(listing);
    }

    /// <summary>For a sale that does not carry RUM (ISLAND_SALE_CARRIES_RUM=false): accrues the island, empties its store
    /// and vault and resets every building to its start level. Returns the RUM the seller keeps (the caller credits it).</summary>
    public static long KeepRumWithSeller(IslandState s, DateTime now)
    {
        IslandService.Accrue(s, now);
        var rum = s.Island.RumStored + s.Island.RumVault;
        s.Island.RumStored = 0;
        s.Island.RumVault = 0;
        s.Island.LastAccrualAt = now;
        foreach (var b in s.Buildings.Values)
        {
            b.Level = IslandRules.StartLevel(b.Kind);
            b.UpdatedAt = now;
        }
        s.Island.Level = IslandRules.IslandLevel(s.Levels);
        return rum;
    }

    // ---------- cancel ----------

    public async Task<object> CancelAsync(Player seller, Guid listingId)
    {
        IslandService.RequireWallet(seller);
        await IslandLocks.Market.WaitAsync();
        try
        {
            var listing = await FindAsync(listingId);
            if (listing.SellerPlayerId != seller.Id) throw new ApiException(403, "not_your_listing", "Only the seller can cancel a listing.");
            if (listing.Status == MarketListing.Cancelled) return await ViewAsync(listing);
            if (listing.Status is not (MarketListing.Active or Cancelling))
                throw new ApiException(409, "listing_not_active", $"The listing is {listing.Status} and cannot be cancelled.");
            listing.Status = Cancelling;
            await _db.SaveChangesAsync();
            return await CancelLockedAsync(listing);
        }
        finally { IslandLocks.Market.Release(); }
    }

    async Task<object> CancelLockedAsync(MarketListing listing)
    {
        var wallet = await _db.Players.Where(p => p.Id == listing.SellerPlayerId).Select(p => p.WalletAddress).FirstAsync();
        try
        {
            listing.TransferSig = await _sol.OwnsNftAsync(wallet!, listing.NftMint)
                ? TransferredUnknownSig
                : await _sol.TransferNftFromTreasuryAsync(listing.NftMint, wallet!);
            listing.Status = MarketListing.Cancelled;
            listing.ClosedAt = _clock.UtcNow;
            listing.Error = null;
        }
        catch (ApiException ex) { listing.Error = Clip($"nft_return: {ex.Code}: {ex.Message}", 256); }
        await _db.SaveChangesAsync();
        return await ViewAsync(listing);
    }

    static string Clip(string s, int max) => s.Length <= max ? s : s[..max];
}
