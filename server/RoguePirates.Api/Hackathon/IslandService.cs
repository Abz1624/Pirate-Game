using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

/// <summary>An island with its buildings loaded (levels by kind).</summary>
public sealed class IslandState
{
    public required Island Island { get; init; }
    public required Dictionary<string, IslandBuilding> Buildings { get; init; }
    public int LevelOf(string kind) => Buildings.TryGetValue(kind, out var b) ? b.Level : IslandRules.StartLevel(kind);
    public Dictionary<string, int> Levels => IslandRules.Kinds.ToDictionary(k => k, LevelOf);
}

/// <summary>
/// Hackathon islands: claim (one per wallet player, a free slot on the fixed world map, an Island NFT minted on devnet),
/// RUM accrual into the store and vault, collect, building upgrades, the NFT metadata. See server/README.md.
/// </summary>
public sealed class IslandService
{
    public const string NftPending = "pending", NftMinted = "minted";
    /// <summary>Listing statuses during which the island is locked (in escrow or mid-trade).</summary>
    public static readonly string[] LockedListingStatuses = { MarketListing.Active, MarketService.Paid, MarketService.Cancelling };

    readonly GameDb _db;
    readonly SolanaService _sol;
    readonly IClock _clock;
    readonly IConfiguration _config;
    readonly ApiSettings _settings;
    readonly ILogger<IslandService> _log;

    public IslandService(GameDb db, SolanaService sol, IClock clock, IConfiguration config, ApiSettings settings, ILogger<IslandService> log)
    {
        _settings = settings;
        _db = db;
        _sol = sol;
        _clock = clock;
        _config = config;
        _log = log;
    }

    public static Player RequireWallet(Player p) =>
        p.WalletAddress != null ? p : throw new ApiException(403, "wallet_required", "Islands need a wallet profile: sign in with a wallet first (POST /api/auth/wallet/verify).");

    /// <summary>The API's public base URL for NFT metadata: PUBLIC_API_URL, else the request's scheme and host.</summary>
    public string PublicBase(HttpContext ctx)
    {
        var configured = _config["PUBLIC_API_URL"];
        return !string.IsNullOrWhiteSpace(configured) ? configured.Trim().TrimEnd('/') : $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    }

    public static string MetadataUrl(string baseUrl, Guid islandId) => $"{baseUrl}/api/islands/{islandId}/metadata.json";
    public static string ImageUrl(string baseUrl, int slot) => $"{baseUrl}/static/islands/{slot}.png";

    // ---------- loading and accrual ----------

    public async Task<IslandState?> TryLoadAsync(Guid id)
    {
        var island = await _db.Islands.FirstOrDefaultAsync(i => i.Id == id);
        if (island == null) return null;
        var buildings = await _db.IslandBuildings.Where(b => b.IslandId == id).ToListAsync();
        return new IslandState { Island = island, Buildings = buildings.ToDictionary(b => b.Kind) };
    }

    public async Task<IslandState> LoadAsync(Guid id) =>
        await TryLoadAsync(id) ?? throw new ApiException(404, "island_not_found", "No such island.");

    /// <summary>Moves the RUM produced since the last accrual into the island: 60% to the vault, the rest to the
    /// exposed store, up to the cap. Unproduced fractions carry over (the clock only advances by what was produced).</summary>
    public static void Accrue(IslandState s, DateTime now)
    {
        var i = s.Island;
        if (now <= i.LastAccrualAt) return;
        var rate = IslandRules.RatePerHour(s.LevelOf(IslandRules.RumStore));
        var cap = IslandRules.Cap(s.LevelOf(IslandRules.RumStore), s.LevelOf(IslandRules.Vault));
        var total = i.RumStored + i.RumVault;
        if (total >= cap)
        {
            i.LastAccrualAt = now;
            return;
        }
        var hours = (now - i.LastAccrualAt).TotalHours;
        var produced = (long)Math.Floor(rate * hours);
        if (produced <= 0) return;
        if (produced >= cap - total)
        {
            produced = cap - total;
            i.LastAccrualAt = now;
        }
        else i.LastAccrualAt = i.LastAccrualAt.AddHours(produced / (double)rate);
        var toVault = produced * IslandRules.VaultPercent / 100;
        i.RumVault += toVault;
        i.RumStored += produced - toVault;
    }

    public Task<bool> IsListedAsync(Guid islandId) =>
        _db.MarketListings.AnyAsync(l => l.IslandId == islandId && LockedListingStatuses.Contains(l.Status));

    public Task<bool> RaidInProgressAsync(Guid islandId)
    {
        var since = _clock.UtcNow - IslandRules.RaidTimeout;
        return _db.Raids.AnyAsync(r => r.IslandId == islandId && r.Result == Raid.Started && r.CreatedAt > since);
    }

    /// <summary>Adds (or, negative, takes) RUM atomically. Taking fails with 400 insufficient_rum when the balance is short.</summary>
    public async Task<long> AddRumAsync(Guid playerId, long delta)
    {
        if (delta < 0)
        {
            var need = -delta;
            var n = await _db.Players.Where(p => p.Id == playerId && p.Rum >= need)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Rum, p => p.Rum - need));
            if (n == 0)
            {
                var have = await _db.Players.Where(p => p.Id == playerId).Select(p => p.Rum).FirstOrDefaultAsync();
                throw new ApiException(400, "insufficient_rum", $"This needs {need} RUM; you have {have}.");
            }
        }
        else if (delta > 0)
            await _db.Players.Where(p => p.Id == playerId).ExecuteUpdateAsync(u => u.SetProperty(p => p.Rum, p => p.Rum + delta));
        return await _db.Players.Where(p => p.Id == playerId).Select(p => p.Rum).FirstAsync();
    }

    // ---------- views ----------

    /// <summary>Each island's defence record: (raids it held, finished raids against it). Islands without raids are left out.</summary>
    public async Task<Dictionary<Guid, (int defended, int raids)>> DefenceRecordsAsync(ICollection<Guid> islandIds)
    {
        if (islandIds.Count == 0) return new();
        if (_settings.RaidRecordOnePerDay)
        {
            // V5 M9j (RAID_RECORD_ONE_PER_DAY): one entry per attacker, island and UTC day: a win if any raid of that day
            // won (or partly won), else defended
            var raids = await _db.Raids.AsNoTracking().Where(r => islandIds.Contains(r.IslandId) && r.Result != Raid.Started)
                .Select(r => new { r.IslandId, r.AttackerPlayerId, r.CreatedAt, r.Result }).ToListAsync();
            return raids.GroupBy(r => r.IslandId).ToDictionary(g => g.Key, g =>
            {
                var days = g.GroupBy(r => (r.AttackerPlayerId, r.CreatedAt.Date)).ToList();
                return (days.Count(d => d.All(r => IslandRules.Defended(r.Result))), days.Count);
            });
        }
        var rows = await _db.Raids.AsNoTracking().Where(r => islandIds.Contains(r.IslandId) && r.Result != Raid.Started)
            .GroupBy(r => new { r.IslandId, r.Result }).Select(g => new { g.Key.IslandId, g.Key.Result, Count = g.Count() }).ToListAsync();
        return rows.GroupBy(r => r.IslandId).ToDictionary(g => g.Key,
            g => (g.Where(r => IslandRules.Defended(r.Result)).Sum(r => r.Count), g.Sum(r => r.Count)));
    }

    public async Task<object> DefenceRecordAsync(Guid islandId)
    {
        var (defended, raids) = (await DefenceRecordsAsync(new[] { islandId })).GetValueOrDefault(islandId);
        return IslandRules.DefenceRecord(defended, raids);
    }

    public async Task<object> ViewAsync(IslandState s, Guid? viewerId, string baseUrl)
    {
        var i = s.Island;
        var owner = await _db.Players.AsNoTracking().Where(p => p.Id == i.OwnerPlayerId)
            .Select(p => new { p.Name, p.WalletAddress }).FirstOrDefaultAsync();
        var listing = await _db.MarketListings.AsNoTracking()
            .Where(l => l.IslandId == i.Id && LockedListingStatuses.Contains(l.Status)).Select(l => new { l.Id, l.PriceLamports, l.Status })
            .FirstOrDefaultAsync();
        var raiding = await RaidInProgressAsync(i.Id);
        return View(s, owner?.Name, owner?.WalletAddress, listing?.Id, listing?.PriceLamports, raiding, viewerId, baseUrl,
            await DefenceRecordAsync(i.Id));
    }

    object View(IslandState s, string? ownerName, string? ownerWallet, Guid? listingId, long? price, bool raiding, Guid? viewerId, string baseUrl, object record)
    {
        var i = s.Island;
        var now = _clock.UtcNow;
        var slot = IslandRules.Slots[i.Slot];
        var shielded = i.ShieldUntil != null && i.ShieldUntil > now;
        var mine = viewerId == i.OwnerPlayerId;
        var listed = listingId != null;
        var store = s.LevelOf(IslandRules.RumStore);
        var cannon = s.LevelOf(IslandRules.CannonTower);
        var walls = s.LevelOf(IslandRules.Walls);
        var def = IslandRules.Defender(cannon, walls, 0);
        return new
        {
            islandId = i.Id, name = i.Name, slot = i.Slot, slotName = slot.Name, region = slot.Region, x = slot.X, y = slot.Y,
            level = i.Level,
            owner = new { playerId = i.OwnerPlayerId, name = ownerName, wallet = ownerWallet, walletShort = IslandRules.ShortWallet(ownerWallet) },
            isMine = mine,
            nft = new
            {
                status = i.NftStatus, mint = i.NftMint, mintSignature = i.MintSig, error = i.MintError, pendingMint = i.PendingMint,
                explorerUrl = i.NftMint == null ? null : $"https://explorer.solana.com/address/{i.NftMint}?cluster={_sol.Cluster}",
                metadataUrl = MetadataUrl(baseUrl, i.Id), imageUrl = ImageUrl(baseUrl, i.Slot),
            },
            rum = new
            {
                exposed = i.RumStored, vault = i.RumVault, total = i.RumStored + i.RumVault,
                cap = IslandRules.Cap(store, s.LevelOf(IslandRules.Vault)), ratePerHour = IslandRules.RatePerHour(store),
                vaultPercent = IslandRules.VaultPercent, lastAccrualAt = i.LastAccrualAt,
            },
            buildings = IslandRules.Kinds.Select(k => new
            {
                kind = k, level = s.LevelOf(k), maxLevel = IslandRules.MaxLevel(k), nextCost = IslandRules.NextCost(k, s.LevelOf(k)),
            }),
            defence = new { def.HullMultiplier, def.ExtraWaves, def.WeaponLevel, def.Difficulty, cannonTower = cannon, walls },
            // the island's defence record (raids it held / finished raids against it; "No raids yet" when none)
            defenceRecord = record,
            shieldUntil = shielded ? i.ShieldUntil : null,
            shielded,
            listed,
            listingId,
            priceLamports = price,
            raidInProgress = raiding,
            raidable = !mine && !shielded && !listed && !raiding,
            createdAt = i.CreatedAt,
        };
    }

    // ---------- claim and mint ----------

    /// <summary>V5 M9h: a pending mint attempt younger than this is left to the request that sent it (the sweep skips it).</summary>
    public static readonly TimeSpan MintSweepAfter = TimeSpan.FromMinutes(2);

    /// <summary>Blocks past a mint's last valid block height before it counts as never landing (about a minute).</summary>
    public const long MintExpiryMarginBlocks = 150;

    DateTime DayStart => DateTime.SpecifyKind(_clock.UtcNow.Date, DateTimeKind.Utc);

    /// <summary>Island NFT mints sent today (UTC), landed or in flight: the daily mint budget counts these.</summary>
    public Task<int> MintsTodayAsync()
    {
        var day = DayStart;
        return _db.Islands.CountAsync(i => i.MintSentAt != null && i.MintSentAt >= day);
    }

    ApiException MintBudgetUsed() => new(429, "mint_budget",
        $"Today's Island NFT mints ({_settings.MintDailyBudget}, MINT_DAILY_BUDGET) are used up; try again after 00:00 UTC.");

    public async Task<object> ClaimAsync(Player player, string? rawName, string baseUrl)
    {
        RequireWallet(player);
        var name = NameRules.Validate(rawName);
        var now = _clock.UtcNow;
        Island island;
        await IslandLocks.Economy.WaitAsync();
        try
        {
            if (await _db.Islands.AnyAsync(i => i.OwnerPlayerId == player.Id))
                throw new ApiException(409, "already_has_island", "You already have an island (one per player for now).");
            // V5 M9h (C9h.2): one free claim per wallet ever, so claim, mint, sell, claim again cannot drain the treasury
            var me = await _db.Players.FirstAsync(p => p.Id == player.Id);
            if (me.IslandClaimedAt != null)
                throw new ApiException(409, "island_claim_used",
                    "Each wallet claims one free island, and this one already did; buy an island on the market instead.");
            // V5 M9h: the daily mint budget (the treasury pays each mint); a claim is refused rather than left without its NFT
            if (_sol.TreasuryOn && await MintsTodayAsync() >= _settings.MintDailyBudget) throw MintBudgetUsed();
            var taken = await _db.Islands.Select(i => i.Slot).ToListAsync();
            var free = IslandRules.Slots.Select(s => s.Index).Except(taken).ToList();
            if (free.Count == 0) throw new ApiException(409, "world_full", "Every island on the map is claimed.");
            island = new Island
            {
                Id = Guid.NewGuid(), OwnerPlayerId = player.Id, Name = name, Slot = free[Random.Shared.Next(free.Count)],
                NftStatus = NftPending, LastAccrualAt = now, CreatedAt = now, Level = 1,
            };
            _db.Islands.Add(island);
            foreach (var kind in IslandRules.Kinds)
                _db.IslandBuildings.Add(new IslandBuilding { IslandId = island.Id, Kind = kind, Level = IslandRules.StartLevel(kind), UpdatedAt = now });
            me.IslandClaimedAt = now;
            await _db.SaveChangesAsync();
            _log.LogInformation("Island {Id} '{Name}' claimed by {Player} at slot {Slot}", island.Id, name, player.Id, island.Slot);
        }
        finally { IslandLocks.Economy.Release(); }

        await TryMintAsync(island.Id, baseUrl);
        return await ViewAsync(await LoadAsync(island.Id), player.Id, baseUrl);
    }

    /// <summary>Retries the Island NFT mint (owner only) when it is still pending. A mint sent before is checked on chain
    /// first and never sent twice (V5 M9h).</summary>
    public async Task<object> RetryMintAsync(Player player, Guid islandId, string baseUrl)
    {
        RequireWallet(player);
        var s = await LoadAsync(islandId);
        if (s.Island.OwnerPlayerId != player.Id) throw new ApiException(403, "not_your_island", "This is not your island.");
        if (s.Island.NftStatus == NftMinted) throw new ApiException(409, "already_minted", "This island's NFT is already minted.");
        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");
        await TryMintAsync(islandId, baseUrl);
        var after = await LoadAsync(islandId);
        await _db.Entry(after.Island).ReloadAsync();
        if (after.Island.NftStatus != NftMinted)
        {
            var error = after.Island.MintError ?? "";
            if (error.StartsWith("mint_budget", StringComparison.Ordinal)) throw MintBudgetUsed();
            if (error.StartsWith("mint_in_flight", StringComparison.Ordinal))
                throw new ApiException(409, "mint_in_flight", "The last mint was sent and may still land; it is checked on chain before any new attempt. Try again in a minute.");
            throw new ApiException(502, "mint_failed", "The mint failed: " + error);
        }
        return await ViewAsync(after, player.Id, baseUrl);
    }

    /// <summary>
    /// Mints the Island NFT to the owner's wallet; a failure leaves the island pending with the error. V5 M9h (the review's
    /// double-mint finding): an attempt already sent (<see cref="Island.PendingMint"/>) is resolved on chain first and a new
    /// one is made only when the old one provably never lands; the new mint address and signature are stored before the
    /// send; a send or confirmation whose result is unknown leaves the attempt pending for the sweep.
    /// </summary>
    async Task TryMintAsync(Guid islandId, string baseUrl)
    {
        await IslandLocks.Mint.WaitAsync();
        try
        {
            var island = await _db.Islands.FirstAsync(i => i.Id == islandId);
            await _db.Entry(island).ReloadAsync();
            if (island.NftStatus == NftMinted) return;
            if (!_sol.TreasuryOn)
            {
                island.MintError = "treasury_off: the treasury wallet is not configured on this server.";
                await _db.SaveChangesAsync();
                return;
            }
            if (island.PendingMint != null)
            {
                var open = await ResolvePendingMintAsync(island);
                await _db.SaveChangesAsync();
                if (open || island.NftStatus == NftMinted) return;
            }
            if (await MintsTodayAsync() >= _settings.MintDailyBudget)
            {
                island.MintError = Clip("mint_budget: " + MintBudgetUsed().Message, 256);
                await _db.SaveChangesAsync();
                return;
            }

            var wallet = await _db.Players.Where(p => p.Id == island.OwnerPlayerId).Select(p => p.WalletAddress).FirstAsync();
            PreparedMint prep;
            try { prep = await _sol.PrepareNftMintAsync(wallet!, "Rogue Island: " + island.Name, IslandRules.NftSymbol, MetadataUrl(baseUrl, island.Id)); }
            catch (ApiException ex)
            {
                island.MintError = Clip($"{ex.Code}: {ex.Message}", 256);
                await _db.SaveChangesAsync();
                _log.LogWarning("Island {Id} mint not prepared: {Error}", island.Id, island.MintError);
                return;
            }
            // recorded before the send: whatever happens next, the attempt is found and checked on chain
            island.PendingMint = prep.Mint;
            island.MintSig = prep.Signature;
            island.MintLastValidBlockHeight = prep.LastValidBlockHeight;
            island.MintSentAt = _clock.UtcNow;
            island.MintError = null;
            await _db.SaveChangesAsync();

            try { await _sol.SendPreparedAsync(prep.Tx); }
            catch (ApiException ex) when (ex.Code == "tx_rejected")
            {
                ClearPendingMint(island, $"{ex.Code}: {ex.Message}"); // a preflight rejection: nothing was forwarded
                await _db.SaveChangesAsync();
                _log.LogWarning("Island {Id} mint rejected: {Error}", island.Id, island.MintError);
                return;
            }
            catch (Exception)
            {
                island.MintError = "mint_in_flight: sent, result unknown; it is checked on chain before any new attempt.";
                await _db.SaveChangesAsync();
                return;
            }
            string? status;
            try { status = await _sol.WaitForConfirmationAsync(prep.Signature, _sol.ConfirmTimeout, CancellationToken.None); }
            catch (Exception) { status = null; }
            if (status is "confirmed" or "finalized") MarkMinted(island);
            else if (status == "failed") ClearPendingMint(island, "tx_failed: the mint failed on chain; nothing was created.");
            else island.MintError = "mint_in_flight: sent but not confirmed in time; it is checked on chain before any new attempt.";
            await _db.SaveChangesAsync();
            if (island.NftStatus == NftMinted)
                _log.LogInformation("Island {Id} NFT {Mint} minted to {Owner} ({Sig})", island.Id, island.NftMint, wallet, island.MintSig);
        }
        finally { IslandLocks.Mint.Release(); }
    }

    void MarkMinted(Island island)
    {
        island.NftMint = island.PendingMint;
        island.PendingMint = null;
        island.MintLastValidBlockHeight = null;
        island.NftStatus = NftMinted;
        island.MintError = null;
        island.MintedAt = _clock.UtcNow;
    }

    static void ClearPendingMint(Island island, string error)
    {
        island.PendingMint = null;
        island.MintSig = null;
        island.MintLastValidBlockHeight = null;
        island.MintSentAt = null; // never landed: costs nothing against the budget
        island.MintError = Clip(error, 256);
    }

    /// <summary>
    /// Resolves an attempt already sent (the caller holds the mint lock and saves). Returns true while it may still land
    /// (left pending: no new attempt); false once it is resolved: minted (the mint and its metadata exist, or the signature
    /// confirmed), or cleared (failed on chain, or the blockhash expired and the mint account does not exist).
    /// </summary>
    async Task<bool> ResolvePendingMintAsync(Island island)
    {
        try
        {
            var (mintExists, metadataExists) = await _sol.MintAccountsAsync(island.PendingMint!);
            if (mintExists && metadataExists) { MarkMinted(island); return false; }
            if (island.MintSig != null)
            {
                var (status, height) = await _sol.SignatureStatusAsync(island.MintSig);
                if (status is "confirmed" or "finalized") { MarkMinted(island); return false; }
                if (status == "failed") { ClearPendingMint(island, "tx_failed: the mint failed on chain; nothing was created."); return false; }
                // (review) a margin of MintExpiryMarginBlocks past the deadline, so one lagging RPC node behind a load
                // balancer cannot make a still-landable mint look expired
                if (status == null && !mintExists && island.MintLastValidBlockHeight is { } last && height > last + MintExpiryMarginBlocks)
                {
                    // re-read after the height: a mint that appeared meanwhile wins
                    if ((await _sol.MintAccountsAsync(island.PendingMint!)).mint) { MarkMinted(island); return false; }
                    ClearPendingMint(island, "mint_not_landed: the earlier mint expired without landing; retry.");
                    return false;
                }
            }
            island.MintError = "mint_in_flight: the last mint was sent and may still land; it is checked on chain before any new attempt.";
            return true;
        }
        catch (ApiException ex)
        {
            island.MintError = Clip($"mint_in_flight: could not check the last mint on chain ({ex.Code}); nothing new was sent.", 256);
            return true;
        }
    }

    /// <summary>V5 M9h: the sweep resolves pending mint attempts older than <see cref="MintSweepAfter"/> (landed: minted;
    /// never landing: cleared for the owner's retry; it never sends a new mint). Returns how many changed.</summary>
    public async Task<int> ReconcileMintsAsync()
    {
        if (!_sol.TreasuryOn) return 0;
        var before = _clock.UtcNow - MintSweepAfter;
        var ids = await _db.Islands.AsNoTracking()
            .Where(i => i.NftStatus == NftPending && i.PendingMint != null && (i.MintSentAt == null || i.MintSentAt <= before))
            .OrderBy(i => i.CreatedAt).Take(20).Select(i => i.Id).ToListAsync();
        var changed = 0;
        foreach (var id in ids)
        {
            await IslandLocks.Mint.WaitAsync();
            try
            {
                var island = await _db.Islands.FirstAsync(i => i.Id == id);
                await _db.Entry(island).ReloadAsync();
                if (island.NftStatus == NftMinted || island.PendingMint == null) continue;
                var open = await ResolvePendingMintAsync(island);
                await _db.SaveChangesAsync();
                if (!open) changed++;
                if (island.NftStatus == NftMinted) _log.LogInformation("Island {Id}: pending mint {Mint} found on chain; minted", id, island.NftMint);
            }
            finally { IslandLocks.Mint.Release(); }
        }
        return changed;
    }

    // ---------- reads ----------

    public async Task<object> MineAsync(Player player, string baseUrl)
    {
        var ids = await _db.Islands.Where(i => i.OwnerPlayerId == player.Id).OrderBy(i => i.CreatedAt).Select(i => i.Id).ToListAsync();
        var list = new List<object>();
        foreach (var id in ids) list.Add(await AccruedViewAsync(id, player.Id, baseUrl));
        var rum = await _db.Players.Where(p => p.Id == player.Id).Select(p => p.Rum).FirstAsync();
        return new { rum, islands = list };
    }

    public async Task<object> GetAsync(Guid id, Guid? viewerId, string baseUrl) => await AccruedViewAsync(id, viewerId, baseUrl);

    /// <summary>V5 M9h: a read. The island is loaded untracked and accrued in memory for display only, without the economy
    /// lock and without a write (every write accrues from the stored clock first, so nothing is lost); before, every GET
    /// took the write lock and saved, so reads queued behind collects, builds and raids.</summary>
    async Task<object> AccruedViewAsync(Guid id, Guid? viewerId, string baseUrl)
    {
        var island = await _db.Islands.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id)
                     ?? throw new ApiException(404, "island_not_found", "No such island.");
        var buildings = await _db.IslandBuildings.AsNoTracking().Where(b => b.IslandId == id).ToListAsync();
        var s = new IslandState { Island = island, Buildings = buildings.ToDictionary(b => b.Kind) };
        Accrue(s, _clock.UtcNow);
        return await ViewAsync(s, viewerId, baseUrl);
    }

    /// <summary>The world map: every slot (claimed or free) and every claimed island's summary.</summary>
    public async Task<object> WorldAsync(Guid? viewerId, string baseUrl)
    {
        var now = _clock.UtcNow;
        var islands = await _db.Islands.AsNoTracking().ToListAsync();
        var ids = islands.Select(i => i.Id).ToList();
        var buildings = (await _db.IslandBuildings.AsNoTracking().Where(b => ids.Contains(b.IslandId)).ToListAsync())
            .GroupBy(b => b.IslandId).ToDictionary(g => g.Key, g => g.ToDictionary(b => b.Kind));
        var ownerIds = islands.Select(i => i.OwnerPlayerId).Distinct().ToList();
        var owners = await _db.Players.AsNoTracking().Where(p => ownerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.WalletAddress }).ToDictionaryAsync(p => p.Id);
        var listings = await _db.MarketListings.AsNoTracking()
            .Where(l => l.IslandId != null && LockedListingStatuses.Contains(l.Status))
            .Select(l => new { l.Id, l.IslandId, l.PriceLamports }).ToListAsync();
        var since = now - IslandRules.RaidTimeout;
        var raiding = (await _db.Raids.AsNoTracking().Where(r => r.Result == Raid.Started && r.CreatedAt > since)
            .Select(r => r.IslandId).ToListAsync()).ToHashSet();
        var records = await DefenceRecordsAsync(ids);

        var views = islands.OrderBy(i => i.Slot).Select(i =>
        {
            var s = new IslandState { Island = i, Buildings = buildings.GetValueOrDefault(i.Id) ?? new() };
            Accrue(s, now); // for display only (not saved)
            var o = owners.GetValueOrDefault(i.OwnerPlayerId);
            var l = listings.FirstOrDefault(x => x.IslandId == i.Id);
            return View(s, o?.Name, o?.WalletAddress, l?.Id, l?.PriceLamports, raiding.Contains(i.Id), viewerId, baseUrl,
                records.TryGetValue(i.Id, out var rec) ? IslandRules.DefenceRecord(rec.defended, rec.raids) : IslandRules.DefenceRecord(0, 0));
        }).ToList();
        var bySlot = islands.ToDictionary(i => i.Slot, i => i.Id);
        return new
        {
            mapWidth = 1000, mapHeight = 1000,
            slots = IslandRules.Slots.Select(s => new { slot = s.Index, name = s.Name, region = s.Region, x = s.X, y = s.Y,
                islandId = bySlot.TryGetValue(s.Index, out var id) ? id : (Guid?)null }),
            freeSlots = IslandRules.Slots.Count - islands.Count,
            islands = views,
        };
    }

    // ---------- collect and build ----------

    async Task<IslandState> OwnedUnlockedAsync(Player player, Guid islandId)
    {
        RequireWallet(player);
        var s = await LoadAsync(islandId);
        if (s.Island.OwnerPlayerId != player.Id) throw new ApiException(403, "not_your_island", "This is not your island.");
        if (await IsListedAsync(islandId)) throw new ApiException(409, "island_listed", "The island is listed on the market; cancel the listing first.");
        return s;
    }

    public async Task<object> CollectAsync(Player player, Guid islandId, string baseUrl)
    {
        await IslandLocks.Economy.WaitAsync();
        try
        {
            var s = await OwnedUnlockedAsync(player, islandId);
            Accrue(s, _clock.UtcNow);
            long fromStore = s.Island.RumStored, fromVault = s.Island.RumVault;
            s.Island.RumStored = 0;
            s.Island.RumVault = 0;
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            var rum = await AddRumAsync(player.Id, fromStore + fromVault);
            await tx.CommitAsync();
            return new { collected = fromStore + fromVault, fromStore, fromVault, rum, island = await ViewAsync(s, player.Id, baseUrl) };
        }
        finally { IslandLocks.Economy.Release(); }
    }

    public async Task<object> BuildAsync(Player player, Guid islandId, string? rawKind, string baseUrl)
    {
        var kind = (rawKind ?? "").Trim().ToLowerInvariant();
        if (!IslandRules.IsKind(kind))
            throw new ApiException(400, "invalid_kind", "kind is one of: " + string.Join(", ", IslandRules.Kinds) + ".");
        await IslandLocks.Economy.WaitAsync();
        try
        {
            var s = await OwnedUnlockedAsync(player, islandId);
            var now = _clock.UtcNow;
            Accrue(s, now); // at the old store level, before the upgrade
            var level = s.LevelOf(kind);
            var cost = IslandRules.NextCost(kind, level) ?? throw new ApiException(409, "max_level", $"{kind} is already at its max level ({level}).");
            await using var tx = await _db.Database.BeginTransactionAsync();
            var rum = await AddRumAsync(player.Id, -cost);
            if (!s.Buildings.TryGetValue(kind, out var b))
            {
                b = new IslandBuilding { IslandId = islandId, Kind = kind, Level = level };
                _db.IslandBuildings.Add(b);
                s.Buildings[kind] = b;
            }
            b.Level = level + 1;
            b.UpdatedAt = now;
            s.Island.Level = IslandRules.IslandLevel(s.Levels);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return new { kind, level = b.Level, cost, rum, island = await ViewAsync(s, player.Id, baseUrl) };
        }
        finally { IslandLocks.Economy.Release(); }
    }

    // ---------- NFT metadata ----------

    public async Task<object> MetadataAsync(Guid islandId, string baseUrl)
    {
        var s = await LoadAsync(islandId);
        var slot = IslandRules.Slots[s.Island.Slot];
        var image = ImageUrl(baseUrl, s.Island.Slot);
        var (defended, raids) = (await DefenceRecordsAsync(new[] { islandId })).GetValueOrDefault(islandId);
        var success = IslandRules.SuccessPercent(defended, raids);
        return new
        {
            name = "Rogue Island: " + s.Island.Name,
            symbol = IslandRules.NftSymbol,
            description = $"{s.Island.Name}, an island at {slot.Name} in the {slot.Region} of the Rogue Pirates world. " +
                          "It brews RUM, keeps 60% of it safe in its vault, and is defended by an AI captain. Raid it, defend it, or trade it.",
            image,
            external_url = "https://www.roguepirates.fun/",
            attributes = new object[]
            {
                new { trait_type = "Location", value = slot.Name },
                new { trait_type = "Region", value = slot.Region },
                new { trait_type = "Slot", value = slot.Index },
                new { trait_type = "Level", value = s.Island.Level },
                new { trait_type = "Rum Store", value = s.LevelOf(IslandRules.RumStore) },
                new { trait_type = "Vault", value = s.LevelOf(IslandRules.Vault) },
                new { trait_type = "Cannon Tower", value = s.LevelOf(IslandRules.CannonTower) },
                new { trait_type = "Walls", value = s.LevelOf(IslandRules.Walls) },
                new { trait_type = "Rum Per Hour", value = (object)IslandRules.RatePerHour(s.LevelOf(IslandRules.RumStore)) },
                new { trait_type = "Raids Defended", value = (object)$"{defended} of {raids}" },
                new { trait_type = "Defence Success", value = success is { } p ? (object)$"{p}%" : "No raids yet" },
            },
            properties = new { category = "image", files = new[] { new { uri = image, type = "image/png" } } },
        };
    }

    static string Clip(string s, int max) => s.Length <= max ? s : s[..max];
}
