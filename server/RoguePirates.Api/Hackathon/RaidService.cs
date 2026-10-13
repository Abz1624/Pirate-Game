using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

/// <summary>
/// Hackathon raids: a fight against an island's AI-captain defender. Start (not your own island, not shielded, not
/// listed, one raid at a time per island, a RUM fee), then the client reports the result within 20 minutes (a 16-minute fight + grace). A win loots
/// 25% of the exposed RUM (half on a partial win) and shields the island for 4 hours; attacking never touches the
/// attacker's own shield (D24). Raids have no SOL stake (owner, 12 Oct: SOL wagers are only in 1v1 PvP): a start with
/// stakeLamports &gt; 0 and the old /wager deposit are refused with raid_stakes_off. Raids staked before that keep their
/// rules so no deposit is lost: a full win pays 2x, a partial win refunds the stake, a loss (or an expired raid) keeps it,
/// and /settle retries a failed payout.
/// </summary>
public sealed class RaidService
{
    public const string Partial = "partial";
    public const string WagerKind = "raid", DepositPurpose = "raid_wager";

    readonly GameDb _db;
    readonly IslandService _islands;
    readonly LedgerService _ledger;
    readonly SolanaService _sol;
    readonly IClock _clock;
    readonly ApiSettings _settings;
    readonly ILogger<RaidService> _log;

    public RaidService(GameDb db, IslandService islands, LedgerService ledger, SolanaService sol, IClock clock, ApiSettings settings,
        ILogger<RaidService> log)
    {
        _settings = settings;
        _db = db;
        _islands = islands;
        _ledger = ledger;
        _sol = sol;
        _clock = clock;
        _log = log;
    }

    public static string Memo(Guid raidId) => "raid:" + raidId;

    /// <summary>Marks started raids older than the raid timeout (20 minutes) abandoned; their unfunded wagers are cancelled and funded ones
    /// are kept by the house (an abandoned raid is a loss).</summary>
    public async Task ExpireStaleAsync()
    {
        var cutoff = _clock.UtcNow - IslandRules.RaidTimeout;
        var stale = await _db.Raids.Where(r => r.Result == Raid.Started && r.CreatedAt <= cutoff).ToListAsync();
        if (stale.Count == 0) return;
        foreach (var r in stale) await AbandonAsync(r);
        await _db.SaveChangesAsync();
    }

    async Task AbandonAsync(Raid r)
    {
        r.Result = Raid.Abandoned;
        r.FinishedAt = _clock.UtcNow;
        if (r.WagerId is { } wid && await _db.Wagers.FirstOrDefaultAsync(w => w.Id == wid) is { } w)
        {
            if (w.Status == Wager.AwaitingDeposits) w.Status = Wager.Cancelled;
            else if (w.Status == Wager.Funded) { w.Status = Wager.Settled; w.SettledAt = _clock.UtcNow; }
        }
    }

    // ---------- start ----------

    public async Task<object> StartAsync(Player player, Guid islandId, long? stakeLamports)
    {
        IslandService.RequireWallet(player);
        if (stakeLamports is > 0) throw RaidStakesOff();
        if (stakeLamports is < 0) throw new ApiException(400, "invalid_stake", "stakeLamports must be 0 or left out (raids have no SOL stake).");

        await IslandLocks.Economy.WaitAsync();
        try
        {
            await ExpireStaleAsync();
            var now = _clock.UtcNow;
            var s = await _islands.LoadAsync(islandId);
            var island = s.Island;
            if (island.OwnerPlayerId == player.Id) throw new ApiException(400, "own_island", "You cannot raid your own island.");
            if (island.ShieldUntil is { } until && until > now)
                throw new ApiException(409, "island_shielded", $"The island is shielded until {until:O}.");
            if (await _islands.IsListedAsync(islandId)) throw new ApiException(409, "island_listed", "The island is listed on the market and cannot be raided.");
            if (await _islands.RaidInProgressAsync(islandId)) throw new ApiException(409, "raid_in_progress", "Someone is raiding this island right now; try again in a few minutes.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            // a new raid replaces the attacker's own unfinished one
            foreach (var old in await _db.Raids.Where(r => r.AttackerPlayerId == player.Id && r.Result == Raid.Started).ToListAsync())
                await AbandonAsync(old);
            var rum = await _islands.AddRumAsync(player.Id, -IslandRules.RaidFeeRum);
            IslandService.Accrue(s, now);
            int ct = s.LevelOf(IslandRules.CannonTower), walls = s.LevelOf(IslandRules.Walls);
            var (design, customDesign) = DefenceService.EffectiveDesign(island, ct, walls);
            var raid = new Raid
            {
                Id = Guid.NewGuid(), AttackerPlayerId = player.Id, IslandId = islandId, DefenderPlayerId = island.OwnerPlayerId,
                Result = Raid.Started, Seed = Random.Shared.Next(1, int.MaxValue), CreatedAt = now,
                GuardianMinute = design.Guardian!.Minute,
            };
            _db.Raids.Add(raid);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            _log.LogInformation("Raid {Id}: {Attacker} on island {Island}", raid.Id, player.Id, islandId);

            var exposed = island.RumStored;
            return new
            {
                raidId = raid.Id, islandId, islandName = island.Name, startedAt = now, expiresAt = now + IslandRules.RaidTimeout,
                feePaid = IslandRules.RaidFeeRum, rum,
                defender = IslandRules.Defender(ct, walls, raid.Seed),
                // the island's level: survive the 15 designed one-minute windows and sink the guardian (AddIslandDefence)
                defence = new
                {
                    isCustom = customDesign, seed = raid.Seed, timeLimitSeconds = IslandRules.RaidTimeLimitSeconds,
                    windowSeconds = IslandRules.DefenceWindowSeconds, design = DefenceService.DesignView(design, ct, walls),
                },
                // V5 M9h switch RAID_LOOT_ENABLED=false: the raid still wins and shields, but loots nothing
                loot = _settings.RaidLootEnabled
                    ? new { exposedRum = exposed, ifWon = Loot(exposed, false), ifPartial = Loot(exposed, true), lootPercent = IslandRules.RaidLootPercent, enabled = true }
                    : new { exposedRum = exposed, ifWon = 0L, ifPartial = 0L, lootPercent = 0, enabled = false },
                wager = (object?)null, // raids have no SOL stake (kept in the JSON for older clients)
                practice = false,
            };
        }
        finally { IslandLocks.Economy.Release(); }
    }

    // ---------- practice (owner, 12 Oct: "test your island's defences") ----------

    /// <summary>A practice raid on the caller's own island with its saved design: the same start JSON as a real raid
    /// (defence design, seed, guardian minute) but nothing is stored or charged: no RUM fee, no raid row, no loot, no
    /// shield, no result to report (raidId is null). Allowed while listed or shielded (it changes nothing).</summary>
    public async Task<object> PracticeAsync(Player player, Guid islandId)
    {
        IslandService.RequireWallet(player);
        var s = await _islands.LoadAsync(islandId);
        var island = s.Island;
        if (island.OwnerPlayerId != player.Id) throw new ApiException(403, "not_your_island", "You can only test the defences of your own island.");
        int ct = s.LevelOf(IslandRules.CannonTower), walls = s.LevelOf(IslandRules.Walls);
        var (design, customDesign) = DefenceService.EffectiveDesign(island, ct, walls);
        var now = _clock.UtcNow;
        var seed = Random.Shared.Next(1, int.MaxValue);
        var rum = await _db.Players.AsNoTracking().Where(p => p.Id == player.Id).Select(p => p.Rum).FirstAsync();
        return new
        {
            raidId = (Guid?)null, islandId, islandName = island.Name, startedAt = now, expiresAt = (DateTime?)null,
            feePaid = 0L, rum,
            defender = IslandRules.Defender(ct, walls, seed),
            defence = new
            {
                isCustom = customDesign, seed, timeLimitSeconds = IslandRules.RaidTimeLimitSeconds,
                windowSeconds = IslandRules.DefenceWindowSeconds, design = DefenceService.DesignView(design, ct, walls),
            },
            loot = new { exposedRum = 0L, ifWon = 0L, ifPartial = 0L, lootPercent = 0, enabled = _settings.RaidLootEnabled },
            wager = (object?)null,
            practice = true,
        };
    }

    static long Loot(long exposed, bool partial) => partial ? exposed * IslandRules.RaidLootPercent / 200 : exposed * IslandRules.RaidLootPercent / 100;

    object? WagerView(Wager? w, Guid raidId, Payout? payout = null) => w == null ? null : new
    {
        wagerId = w.Id, stakeLamports = w.StakeLamports, status = w.Status, memo = Memo(raidId), treasury = _sol.TreasuryAddress,
        payoutIfWon = w.StakeLamports * 2, depositSignature = w.HostDepositSig,
        payout = payout == null ? null : LedgerService.PayoutView(payout, _sol.Cluster),
    };

    // ---------- the old raid SOL wager (off) ----------

    /// <summary>Raid stakes are off (owner, 12 Oct: SOL wagers are only in 1v1 PvP).</summary>
    public static ApiException RaidStakesOff() =>
        new(400, "raid_stakes_off", "Raids have no SOL stake; SOL wagers are only in 1v1 matches.");

    /// <summary>POST /api/raids/{id}/wager: gone. Raids staked before stakes were switched off still settle (result and
    /// /settle pay a funded stake as before).</summary>
    public static Task<object> FundWagerAsync() =>
        throw new ApiException(410, "raid_stakes_off", "Raid stakes are switched off; SOL wagers are only in 1v1 matches. Raid without a stake.");

    // ---------- result ----------

    /// <summary>The raid's outcome. With <paramref name="guardianSunk"/> or <paramref name="minuteReached"/> (clients since
    /// AddIslandDefence): won = the guardian was sunk; partial = the attacker reached the guardian's minute without sinking
    /// it; lost otherwise. Without them (older clients) the won/partial flags decide.</summary>
    public static string Outcome(bool won, bool partial, bool? guardianSunk, int? minuteReached, int guardianMinute)
    {
        if (guardianSunk == null && minuteReached == null) return won ? (partial ? Partial : Raid.Won) : Raid.Lost;
        if (guardianSunk == true) return Raid.Won;
        return (minuteReached ?? 0) >= guardianMinute ? Partial : Raid.Lost;
    }

    public async Task<object> ResultAsync(Player player, Guid raidId, bool won, bool partial, int seconds,
        bool? guardianSunk = null, int? minuteReached = null)
    {
        IslandService.RequireWallet(player);
        Raid raid;
        Wager? wager;
        long rum;
        await IslandLocks.Economy.WaitAsync();
        try
        {
            raid = await OwnRaidAsync(player, raidId);
            if (raid.Result != Raid.Started) throw new ApiException(409, "raid_finished", $"This raid is already over ({raid.Result}).");
            var now = _clock.UtcNow;
            if (raid.CreatedAt + IslandRules.RaidTimeout <= now)
            {
                await AbandonAsync(raid);
                await _db.SaveChangesAsync();
                throw new ApiException(409, "raid_expired", $"This raid expired (results are taken for {IslandRules.RaidTimeout.TotalMinutes:0} minutes after the start).");
            }
            var elapsed = (now - raid.CreatedAt).TotalSeconds;
            const int maxSeconds = IslandRules.RaidTimeLimitSeconds + IslandRules.RaidGraceSeconds;
            if (seconds < 1 || seconds > maxSeconds || seconds > elapsed + 5)
                throw new ApiException(400, "invalid_seconds", $"seconds must be 1..{maxSeconds} and not longer than the time since the raid started.");
            if (minuteReached is < 0 or > IslandRules.DefenceWindows + 1)
                throw new ApiException(400, "invalid_minute", $"minuteReached must be 0..{IslandRules.DefenceWindows + 1}.");
            var guardianMinute = raid.GuardianMinute ?? IslandRules.GuardianMinuteDefault;
            if (guardianSunk == true && minuteReached is { } mr && mr < guardianMinute)
                throw new ApiException(400, "invalid_result", $"The guardian arrives in minute {guardianMinute}; it cannot be sunk by minute {mr}.");
            var outcome = Outcome(won, partial, guardianSunk, minuteReached, guardianMinute);

            wager = raid.WagerId == null ? null : await _db.Wagers.FirstAsync(w => w.Id == raid.WagerId);
            await using var tx = await _db.Database.BeginTransactionAsync();
            raid.FinishedAt = now;
            raid.Seconds = seconds;
            raid.GuardianSunk = guardianSunk;
            raid.MinuteReached = minuteReached;
            won = outcome != Raid.Lost;
            partial = outcome == Partial;
            if (won)
            {
                var s = await _islands.LoadAsync(raid.IslandId);
                IslandService.Accrue(s, now);
                var loot = _settings.RaidLootEnabled ? Math.Min(s.Island.RumStored, Loot(s.Island.RumStored, partial)) : 0;
                s.Island.RumStored -= loot;
                s.Island.ShieldUntil = now + IslandRules.ShieldAfterRaid;
                raid.RumLooted = loot;
                raid.Result = partial ? Partial : Raid.Won;
                await _db.SaveChangesAsync();
                rum = await _islands.AddRumAsync(player.Id, loot);
            }
            else
            {
                raid.Result = Raid.Lost;
                rum = await _islands.AddRumAsync(player.Id, 0);
            }
            if (wager != null)
            {
                if (wager.Status == Wager.AwaitingDeposits) wager.Status = Wager.Cancelled;
                else if (wager.Status == Wager.Funded && !won) { wager.Status = Wager.Settled; wager.SettledAt = now; }
                else if (wager.Status == Wager.Funded) wager.WinnerPlayerId = player.Id; // paid below
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            _log.LogInformation("Raid {Id} {Result}: looted {Loot} RUM in {Seconds}s", raid.Id, raid.Result, raid.RumLooted, seconds);
        }
        finally { IslandLocks.Economy.Release(); }

        var payout = await PayWagerAsync(raid, wager);
        var island = await _db.Islands.AsNoTracking().FirstAsync(i => i.Id == raid.IslandId);
        return new
        {
            raidId = raid.Id, result = raid.Result, rumLooted = raid.RumLooted, rum, seconds = raid.Seconds,
            guardianMinute = raid.GuardianMinute, minuteReached = raid.MinuteReached, guardianSunk = raid.GuardianSunk,
            defenderShieldUntil = island.ShieldUntil,
            wager = WagerView(wager, raid.Id, payout),
        };
    }

    /// <summary>Pays a won wager (2x on a full win, the stake back on a partial win). Safe to call again: it pays only
    /// when no payout for this raid is sent, pending or unconfirmed.</summary>
    async Task<Payout?> PayWagerAsync(Raid raid, Wager? wager)
    {
        if (wager is not { Status: Wager.Funded } || raid.Result is not (Raid.Won or Partial)) return null;
        var refId = raid.Id.ToString();
        var existing = await _db.Payouts.Where(p => p.RefId == refId && (p.Reason == "raid_win" || p.Reason == "raid_refund"))
            .OrderByDescending(p => p.Id).ToListAsync();
        var live = existing.FirstOrDefault(p => p.Status != Payout.Failed);
        if (live != null)
        {
            if (live.Status == Payout.Sent) MarkPaid(wager, live);
            await _db.SaveChangesAsync();
            return live;
        }
        var wallet = await _db.Players.Where(p => p.Id == raid.AttackerPlayerId).Select(p => p.WalletAddress).FirstAsync();
        var full = raid.Result == Raid.Won;
        var payout = await _ledger.PayAsync(raid.AttackerPlayerId, wallet!, full ? wager.StakeLamports * 2 : wager.StakeLamports,
            full ? "raid_win" : "raid_refund", refId);
        if (payout.Status == Payout.Sent) MarkPaid(wager, payout);
        await _db.SaveChangesAsync();
        return payout;
    }

    void MarkPaid(Wager wager, Payout payout)
    {
        wager.Status = Wager.Settled;
        wager.SettledAt = _clock.UtcNow;
        wager.WinnerPlayerId ??= payout.PlayerId;
        wager.PayoutId = payout.Id;
        wager.PayoutSig = payout.Signature;
    }

    /// <summary>Retries a won raid's wager payout after a failure.</summary>
    public async Task<object> SettleAsync(Player player, Guid raidId)
    {
        var raid = await OwnRaidAsync(player, raidId);
        var wager = raid.WagerId == null ? null : await _db.Wagers.FirstAsync(w => w.Id == raid.WagerId);
        if (wager == null) throw new ApiException(400, "no_wager", "This raid has no wager.");
        var payout = await PayWagerAsync(raid, wager);
        return new { raidId, result = raid.Result, wager = WagerView(wager, raidId, payout) };
    }

    async Task<Raid> OwnRaidAsync(Player player, Guid raidId)
    {
        var raid = await _db.Raids.FirstOrDefaultAsync(r => r.Id == raidId) ?? throw new ApiException(404, "raid_not_found", "No such raid.");
        if (raid.AttackerPlayerId != player.Id) throw new ApiException(403, "not_your_raid", "This is not your raid.");
        return raid;
    }

    // ---------- history ----------

    /// <summary>V5 M9h: a read, without the economy lock and without writes: a started raid past its timeout is shown as
    /// abandoned (the next raid start or result on that island stores it so).</summary>
    public async Task<object> MineAsync(Player player)
    {
        var cutoff = _clock.UtcNow - IslandRules.RaidTimeout;
        string ResultOf(Raid r) => r.Result == Raid.Started && r.CreatedAt <= cutoff ? Raid.Abandoned : r.Result;

        var raids = await _db.Raids.AsNoTracking().Where(r => r.AttackerPlayerId == player.Id || r.DefenderPlayerId == player.Id)
            .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();
        var islandIds = raids.Select(r => r.IslandId).Distinct().ToList();
        var names = await _db.Islands.AsNoTracking().Where(i => islandIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
        var playerIds = raids.SelectMany(r => new[] { r.AttackerPlayerId, r.DefenderPlayerId }).Distinct().ToList();
        var players = await _db.Players.AsNoTracking().Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, wallet = IslandRules.ShortWallet(p.WalletAddress) });
        var wagerIds = raids.Where(r => r.WagerId != null).Select(r => r.WagerId!.Value).ToList();
        var wagers = await _db.Wagers.AsNoTracking().Where(w => wagerIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id);

        object Row(Raid r) => new
        {
            raidId = r.Id, islandId = r.IslandId, islandName = names.GetValueOrDefault(r.IslandId),
            attacker = new { playerId = r.AttackerPlayerId, name = players.GetValueOrDefault(r.AttackerPlayerId)?.Name, wallet = players.GetValueOrDefault(r.AttackerPlayerId)?.wallet },
            defender = new { playerId = r.DefenderPlayerId, name = players.GetValueOrDefault(r.DefenderPlayerId)?.Name, wallet = players.GetValueOrDefault(r.DefenderPlayerId)?.wallet },
            result = ResultOf(r), rumLooted = r.RumLooted, seconds = r.Seconds, startedAt = r.CreatedAt, finishedAt = r.FinishedAt,
            guardianMinute = r.GuardianMinute, minuteReached = r.MinuteReached, guardianSunk = r.GuardianSunk,
            wager = r.WagerId is { } wid && wagers.TryGetValue(wid, out var w)
                ? new { wagerId = w.Id, stakeLamports = w.StakeLamports, status = w.Status, payoutSignature = w.PayoutSig }
                : null,
        };

        return new
        {
            attacks = raids.Where(r => r.AttackerPlayerId == player.Id).Select(Row),
            defences = raids.Where(r => r.DefenderPlayerId == player.Id).Select(Row),
        };
    }
}
