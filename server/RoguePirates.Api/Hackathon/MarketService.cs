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
    public const string DepositPurpose = "market_buy", SaleReason = "market_sale";
    public const long MinPriceLamports = 1_000_000, MaxPriceLamports = 100 * SolanaService.LamportsPerSol; // 0.001 .. 100 SOL
    /// <summary>Stored in TransferSig when a retry finds the NFT already at its destination (the first send timed out).</summary>
    const string TransferredUnknownSig = "confirmed-by-holding";

    readonly GameDb _db;
    readonly IslandService _islands;
    readonly LedgerService _ledger;
    readonly SolanaService _sol;
    readonly IClock _clock;
    readonly ILogger<MarketService> _log;

    public MarketService(GameDb db, IslandService islands, LedgerService ledger, SolanaService sol, IClock clock, ILogger<MarketService> log)
    {
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
        return View(l, island, players);
    }

    object View(MarketListing l, IslandState? island, IReadOnlyDictionary<Guid, Player> players)
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

    public async Task<object> BuyAsync(Player buyer, Guid listingId, string? signature)
    {
        IslandService.RequireWallet(buyer);
        await IslandLocks.Market.WaitAsync();
        try
        {
            var listing = await FindAsync(listingId);
            if (listing.Status == Paid && listing.BuyerPlayerId == buyer.Id)
                return await CompleteLockedAsync(listing); // a retry of the same purchase
            if (listing.Status != MarketListing.Active)
                throw new ApiException(409, "listing_not_active", $"The listing is {listing.Status}; do not pay for it.");
            if (listing.SellerPlayerId == buyer.Id) throw new ApiException(400, "own_listing", "You cannot buy your own listing (cancel it instead).");

            var (deposit, _) = await _ledger.VerifyDepositAsync(buyer, signature, DepositPurpose, listingId.ToString(), listing.PriceLamports, Memo(listingId));
            deposit.Status = SolanaDeposit.Consumed;
            deposit.ConsumedAt = _clock.UtcNow;
            listing.Status = Paid;
            listing.BuyerPlayerId = buyer.Id;
            listing.PaymentSig = deposit.Signature;
            await _db.SaveChangesAsync();
            _log.LogInformation("Listing {Id}: paid by {Buyer} ({Sig})", listingId, buyer.Id, deposit.Signature);
            return await CompleteLockedAsync(listing);
        }
        finally { IslandLocks.Market.Release(); }
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
                var island = await _db.Islands.FirstOrDefaultAsync(i => i.Id == islandId);
                if (island != null && island.OwnerPlayerId != buyer.Id)
                {
                    island.OwnerPlayerId = buyer.Id;
                    _log.LogInformation("Island {Island} now belongs to {Buyer} (listing {Id})", islandId, buyer.Id, listing.Id);
                }
                await _db.SaveChangesAsync();
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
