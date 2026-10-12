using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Hackathon;

/// <summary>Body of <c>POST /api/admin/demo/reset</c>. <see cref="DryRun"/> defaults to true (count only).</summary>
public sealed record DemoResetRequest(string[]? Wallets, bool? ClearWeeklyRewards, DateTime? WeekStart, bool? DryRun);

/// <summary>
/// Hackathon: owner tool that removes test data (for example the devnet smoke test's wallets) from the live API before
/// a demo. For the listed wallets' players it deletes their runs (with run logs and verdicts), weekly bests, daily race
/// entries and raids (as attacker or defender), releases their islands (Islands and IslandBuildings rows; the Island
/// NFTs stay on chain in the wallets) and deletes their closed (sold or cancelled) market listings. An island with an
/// open listing (NFT in escrow or a trade in progress) is skipped and reported. Optionally deletes the WeeklyRewards rows
/// of one week (default: the current week) so the board's reward column is clean; the Payout rows stay as history.
/// Players, sessions, deposits, wagers and payouts are never touched. Everything runs in one transaction.
/// </summary>
public sealed class DemoResetService
{
    public const int MaxWallets = 50;

    readonly GameDb _db;
    readonly IClock _clock;

    public DemoResetService(GameDb db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<object> ResetAsync(DemoResetRequest req)
    {
        var dryRun = req.DryRun ?? true;
        var clearWeekly = req.ClearWeeklyRewards ?? false;
        var wallets = (req.Wallets ?? Array.Empty<string>())
            .Select(w => (w ?? "").Trim()).Where(w => w.Length > 0).Distinct().ToArray();
        if (wallets.Length == 0 && !clearWeekly)
            throw new ApiException(400, "nothing_to_reset", "Give at least one wallet, or clearWeeklyRewards: true.");
        if (wallets.Length > MaxWallets)
            throw new ApiException(400, "too_many_wallets", $"At most {MaxWallets} wallets per call.");

        var players = await _db.Players.AsNoTracking().Where(p => p.WalletAddress != null && wallets.Contains(p.WalletAddress))
            .Select(p => new { p.Id, p.Name, p.WalletAddress }).ToListAsync();
        var ids = players.Select(p => p.Id).ToList();

        var runIds = await _db.Runs.Where(r => ids.Contains(r.PlayerId)).Select(r => r.Id).ToListAsync();
        var islands = await _db.Islands.AsNoTracking().Where(i => ids.Contains(i.OwnerPlayerId)).OrderBy(i => i.Slot).ToListAsync();
        var islandIds = islands.Select(i => i.Id).ToList();
        var openListings = await _db.MarketListings.AsNoTracking()
            .Where(l => l.IslandId != null && islandIds.Contains(l.IslandId.Value)
                        && l.Status != MarketListing.Sold && l.Status != MarketListing.Cancelled)
            .Select(l => new { l.Id, IslandId = l.IslandId!.Value, l.Status }).ToListAsync();
        var skipped = islands.Where(i => openListings.Any(l => l.IslandId == i.Id)).ToList();
        var released = islands.Except(skipped).ToList();
        var releasedIds = released.Select(i => i.Id).ToList();

        IQueryable<RunLog> RunLogs() => _db.RunLogs.Where(x => runIds.Contains(x.RunId));
        IQueryable<RunVerdict> RunVerdicts() => _db.RunVerdicts.Where(x => runIds.Contains(x.RunId));
        IQueryable<RunRecord> Runs() => _db.Runs.Where(x => ids.Contains(x.PlayerId));
        IQueryable<WeeklyBest> WeeklyBests() => _db.WeeklyBests.Where(x => ids.Contains(x.PlayerId));
        IQueryable<DailyRaceEntry> RaceEntries() => _db.DailyRaceEntries.Where(x => ids.Contains(x.PlayerId));
        IQueryable<Raid> Raids() => _db.Raids.Where(x => ids.Contains(x.AttackerPlayerId) || ids.Contains(x.DefenderPlayerId)
                                                         || releasedIds.Contains(x.IslandId));
        IQueryable<IslandBuilding> Buildings() => _db.IslandBuildings.Where(x => releasedIds.Contains(x.IslandId));
        IQueryable<Island> Islands() => _db.Islands.Where(x => releasedIds.Contains(x.Id));
        IQueryable<MarketListing> Listings() => _db.MarketListings.Where(x =>
            (x.Status == MarketListing.Sold || x.Status == MarketListing.Cancelled)
            && (ids.Contains(x.SellerPlayerId) || (x.BuyerPlayerId != null && ids.Contains(x.BuyerPlayerId.Value))
                || (x.IslandId != null && releasedIds.Contains(x.IslandId.Value))));

        DateTime? week = clearWeekly
            ? (req.WeekStart is { } w ? WeekMath.WeekStart(DateTime.SpecifyKind(w, DateTimeKind.Utc)) : WeekMath.WeekStart(_clock.UtcNow))
            : null;
        IQueryable<WeeklyReward> Rewards() => _db.WeeklyRewards.Where(x => week != null && x.WeekStart == week);
        var rewardPayoutIds = week == null ? new List<long?>() : await Rewards().Select(r => r.PayoutId).ToListAsync();

        int runLogs, runVerdicts, runs, weeklyBests, raceEntries, raids, buildings, islandRows, listings, rewards;
        if (dryRun)
        {
            runLogs = await RunLogs().CountAsync();
            runVerdicts = await RunVerdicts().CountAsync();
            runs = runIds.Count;
            weeklyBests = await WeeklyBests().CountAsync();
            raceEntries = await RaceEntries().CountAsync();
            raids = await Raids().CountAsync();
            buildings = await Buildings().CountAsync();
            islandRows = released.Count;
            listings = await Listings().CountAsync();
            rewards = rewardPayoutIds.Count;
        }
        else
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            // children first (RunLog/RunVerdict cascade from Runs in the schema, but delete them explicitly anyway)
            runLogs = await RunLogs().ExecuteDeleteAsync();
            runVerdicts = await RunVerdicts().ExecuteDeleteAsync();
            raceEntries = await RaceEntries().ExecuteDeleteAsync();
            weeklyBests = await WeeklyBests().ExecuteDeleteAsync();
            runs = await Runs().ExecuteDeleteAsync();
            raids = await Raids().ExecuteDeleteAsync();
            listings = await Listings().ExecuteDeleteAsync();
            buildings = await Buildings().ExecuteDeleteAsync();
            islandRows = await Islands().ExecuteDeleteAsync();
            rewards = await Rewards().ExecuteDeleteAsync();
            await tx.CommitAsync();
        }

        var notes = new List<string>();
        if (released.Count > 0)
            notes.Add("Released islands' Island NFTs stay on chain in their wallets (not burned or moved); their world slots are free again.");
        if (skipped.Count > 0)
            notes.Add("Islands with an open market listing were skipped: close or finish the listing first.");
        if (week != null)
            notes.Add("Payout records stay as history. With the week's rewards deleted, the hourly job pays that week again once it is the previous week.");

        return new
        {
            dryRun,
            wallets = wallets.Select(w =>
            {
                var p = players.FirstOrDefault(x => x.WalletAddress == w);
                return new { wallet = w, found = p != null, playerId = p?.Id, name = p?.Name };
            }),
            deleted = new
            {
                runs, runLogs, runVerdicts, weeklyBests, dailyRaceEntries = raceEntries, raids,
                islands = islandRows, islandBuildings = buildings, marketListings = listings, weeklyRewards = rewards,
            },
            releasedIslands = released.Select(i => new { id = i.Id, name = i.Name, slot = i.Slot, nftMint = i.NftMint }),
            skippedIslands = skipped.Select(i => new
            {
                id = i.Id, name = i.Name, slot = i.Slot, reason = "listing_open",
                listingStatus = openListings.First(l => l.IslandId == i.Id).Status,
            }),
            weeklyRewards = week == null ? null : new
            {
                weekStart = week,
                rows = rewardPayoutIds.Count,
                payoutsKept = rewardPayoutIds.Count(x => x != null),
            },
            notes,
        };
    }
}
