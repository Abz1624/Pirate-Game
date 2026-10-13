using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

public sealed record WeeklyRewardsRequest(DateTime? WeekStart, bool? DryRun, string? MapId);

/// <summary>
/// Hackathon: devnet SOL rewards for the weekly kills leaderboard. The payout table comes from WEEKLY_REWARD_SOL
/// (default "0.5,0.3,0.2": 1st, 2nd, 3rd). Only wallet profiles are eligible (places go to the best wallet players, in
/// the leaderboard's order); WEEKLY_REWARD_CHECKED_ONLY=true also requires the best run to have passed the run checks.
/// A week is paid once: the winners are written to WeeklyRewards (unique per week, map and rank) before any payout, and
/// a later pass only retries failed payouts. A background job pays the previous week; the owner can trigger any week.
/// </summary>
public sealed class RewardService
{
    public const string Reason = "weekly_reward";
    public static readonly long[] DefaultTable = { 500_000_000, 300_000_000, 200_000_000 };

    readonly GameDb _db;
    readonly SolanaService _sol;
    readonly LedgerService _ledger;
    readonly IClock _clock;
    readonly IConfiguration _config;
    readonly ILogger<RewardService> _log;

    public RewardService(GameDb db, SolanaService sol, LedgerService ledger, IClock clock, IConfiguration config, ILogger<RewardService> log)
    {
        _db = db;
        _sol = sol;
        _ledger = ledger;
        _clock = clock;
        _config = config;
        _log = log;
    }

    /// <summary>Lamports per place, 1st first (WEEKLY_REWARD_SOL, comma-separated SOL amounts, at most 10 places).</summary>
    public long[] Table
    {
        get
        {
            var raw = _config["WEEKLY_REWARD_SOL"];
            if (string.IsNullOrWhiteSpace(raw)) return DefaultTable;
            var list = new List<long>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(10))
            {
                if (!decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var sol) || sol <= 0 || sol > 100)
                    return DefaultTable;
                list.Add((long)(sol * SolanaService.LamportsPerSol));
            }
            return list.Count == 0 ? DefaultTable : list.ToArray();
        }
    }

    /// <summary>Only runs that passed the server's run checks earn SOL (13 Oct hotfix: default true; WEEKLY_REWARD_CHECKED_ONLY=false
    /// pays on client-reported totals, which a modified client can fake).</summary>
    public bool CheckedOnly => !(bool.TryParse(_config["WEEKLY_REWARD_CHECKED_ONLY"], out var b) && !b);

    /// <summary>The payout key of a reward: (week, map, rank) (13 Oct hotfix), so a week's rows deleted and recorded again
    /// can never pay the same place twice.</summary>
    public static string RewardRef(DateTime week, string map, int rank) => $"wr:{week:yyyyMMdd}:{map}:{rank}";
    /// <summary>The key used before the hotfix (by row id); still checked so rewards paid then are not paid again.</summary>
    static string LegacyRewardRef(long rewardId) => "wr:" + rewardId;

    DateTime PreviousWeek => WeekMath.WeekStart(_clock.UtcNow).AddDays(-7);

    static DateTime NormalizeWeek(DateTime? weekStart, DateTime fallback) =>
        weekStart is { } w ? WeekMath.WeekStart(DateTime.SpecifyKind(w, DateTimeKind.Utc)) : fallback;

    /// <summary>The eligible winners of a week, in place order (not written).</summary>
    async Task<List<(int rank, Guid playerId, string name, string wallet, int kills, int runSeconds, bool isChecked, long lamports)>> WinnersAsync(DateTime week, string map)
    {
        var table = Table;
        var checkedOnly = CheckedOnly;
        var rows = await (from b in _db.WeeklyBests
                          join p in _db.Players on b.PlayerId equals p.Id
                          join v in _db.RunVerdicts on b.RunId equals v.RunId into vs
                          from v in vs.DefaultIfEmpty()
                          where b.WeekStart == week && b.MapId == map && p.WalletAddress != null
                          orderby b.Kills descending, b.RunSeconds, b.AchievedAt, b.Id
                          select new { b.PlayerId, p.Name, p.WalletAddress, b.Kills, b.RunSeconds, Checked = v != null && v.Status == Verdicts.Pass })
            .Take(200)
            .ToListAsync();
        return rows.Where(r => r.Kills > 0 && (!checkedOnly || r.Checked)).Take(table.Length)
            .Select((r, i) => (i + 1, r.PlayerId, r.Name, r.WalletAddress!, r.Kills, r.RunSeconds, r.Checked, table[i]))
            .ToList();
    }

    /// <summary>
    /// Pays a week (default the previous one). Dry run: shows who would be paid, writes nothing. Otherwise the winners are
    /// recorded once and each reward is paid once (failed payouts are retried on the next call).
    /// </summary>
    public async Task<object> PayWeekAsync(DateTime? weekStart, string? mapId, bool dryRun)
    {
        var week = NormalizeWeek(weekStart, PreviousWeek);
        var map = RunService.NormalizeMap(mapId);
        if (week > WeekMath.WeekStart(_clock.UtcNow))
            throw new ApiException(400, "future_week", "That week has not started.");
        // 13 Oct hotfix: a week is paid only once it has ended (its board can still change while it runs)
        if (!dryRun && week.AddDays(7) > _clock.UtcNow)
            throw new ApiException(409, "week_not_ended", $"The week of {week:yyyy-MM-dd} ends {week.AddDays(7):yyyy-MM-dd} 00:00 UTC; it is paid after that (a dry run works now).");

        var existing = await _db.WeeklyRewards.AnyAsync(r => r.WeekStart == week && r.MapId == map);
        if (dryRun)
        {
            if (existing) return await ViewAsync(week, map, dryRun: true);
            var winners = await WinnersAsync(week, map);
            return new
            {
                weekStart = week, weekEnd = week.AddDays(7), mapId = map, dryRun = true, alreadyRecorded = false,
                checkedOnly = CheckedOnly, table = TableView(),
                winners = winners.Select(w => new
                {
                    rank = w.rank, playerId = w.playerId, name = w.name, tag = PlayerTags.For(w.playerId), wallet = w.wallet,
                    walletShort = PayoutOnce.Short(w.wallet), kills = w.kills, runSeconds = w.runSeconds, @checked = w.isChecked,
                    lamports = w.lamports, sol = w.lamports / (double)SolanaService.LamportsPerSol, payout = (object?)null,
                }),
            };
        }

        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");
        if (!existing)
        {
            var now = _clock.UtcNow;
            foreach (var w in await WinnersAsync(week, map))
                _db.WeeklyRewards.Add(new WeeklyReward
                {
                    WeekStart = week, MapId = map, Rank = w.rank, PlayerId = w.playerId, Lamports = w.lamports, CreatedAt = now,
                });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                // another pass recorded this week at the same moment: keep theirs
                _db.ChangeTracker.Clear();
            }
        }

        var rewards = await _db.WeeklyRewards.Where(r => r.WeekStart == week && r.MapId == map).OrderBy(r => r.Rank).ToListAsync();
        foreach (var r in rewards)
        {
            var wallet = await _db.Players.AsNoTracking().Where(p => p.Id == r.PlayerId).Select(p => p.WalletAddress).FirstOrDefaultAsync();
            if (wallet == null) continue;
            var payout = await LegacyPayoutAsync(r, wallet, week)
                         ?? await PayoutOnce.PayAsync(_db, _ledger, _clock, _log, r.PlayerId, wallet, r.Lamports, Reason, RewardRef(week, map, r.Rank));
            if (payout != null && r.PayoutId != payout.Id)
            {
                r.PayoutId = payout.Id;
                await _db.SaveChangesAsync();
            }
        }
        _log.LogInformation("Weekly rewards {Week:yyyy-MM-dd} {Map}: {Count} places", week, map, rewards.Count);
        return await ViewAsync(week, map, dryRun: false);
    }

    /// <summary>
    /// A reward payout made before the hotfix (keyed by row id) that this reward must not repeat: the row's own legacy
    /// payout, or (rows deleted by a demo reset and recorded again) a live legacy-keyed reward payout to the same wallet
    /// of the same amount made since the week started. Conservative on purpose: when in doubt, do not pay again.
    /// </summary>
    async Task<Payout?> LegacyPayoutAsync(WeeklyReward r, string wallet, DateTime week)
    {
        var own = LegacyRewardRef(r.Id);
        var rows = await _db.Payouts.AsNoTracking()
            .Where(p => p.Reason == Reason && p.Status != Payout.Failed && p.RefId != null
                        && (p.RefId == own || (p.Wallet == wallet && p.Lamports == r.Lamports && p.CreatedAt >= week)))
            .OrderByDescending(p => p.Id).ToListAsync();
        return rows.FirstOrDefault(p => p.RefId == own)
               ?? rows.FirstOrDefault(p => p.RefId!.Length > 3 && p.RefId[3..].All(char.IsDigit));
    }

    object TableView() => Table.Select((l, i) => new { rank = i + 1, lamports = l, sol = l / (double)SolanaService.LamportsPerSol });

    /// <summary>The recorded winners of a week with their payouts (default the previous week).</summary>
    public Task<object> GetAsync(DateTime? weekStart, string? mapId) =>
        ViewAsync(NormalizeWeek(weekStart, PreviousWeek), RunService.NormalizeMap(mapId), dryRun: false);

    async Task<object> ViewAsync(DateTime week, string map, bool dryRun)
    {
        var rows = await (from r in _db.WeeklyRewards.AsNoTracking()
                          join p in _db.Players.AsNoTracking() on r.PlayerId equals p.Id
                          where r.WeekStart == week && r.MapId == map
                          orderby r.Rank
                          select new { r.Id, r.Rank, r.PlayerId, p.Name, p.WalletAddress, r.Lamports, r.PayoutId })
            .ToListAsync();
        var payoutIds = rows.Where(r => r.PayoutId != null).Select(r => r.PayoutId!.Value).ToList();
        var payouts = await _db.Payouts.AsNoTracking().Where(p => payoutIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var winnerIds = rows.Select(r => r.PlayerId).ToList();
        var bests = await _db.WeeklyBests.AsNoTracking().Where(b => b.WeekStart == week && b.MapId == map && winnerIds.Contains(b.PlayerId))
            .ToDictionaryAsync(b => b.PlayerId);
        return new
        {
            weekStart = week, weekEnd = week.AddDays(7), mapId = map, dryRun, alreadyRecorded = rows.Count > 0,
            checkedOnly = CheckedOnly, table = TableView(),
            winners = rows.Select(r => new
            {
                rank = r.Rank, playerId = r.PlayerId, name = r.Name, tag = PlayerTags.For(r.PlayerId), wallet = r.WalletAddress,
                walletShort = PayoutOnce.Short(r.WalletAddress),
                kills = bests.TryGetValue(r.PlayerId, out var best) ? best.Kills : (int?)null, runSeconds = best?.RunSeconds, @checked = (bool?)null,
                lamports = r.Lamports, sol = r.Lamports / (double)SolanaService.LamportsPerSol,
                payout = r.PayoutId is { } pid && payouts.TryGetValue(pid, out var p) ? LedgerService.PayoutView(p, _sol.Cluster) : null,
            }),
        };
    }

    /// <summary>The rewards of a week by player (for the leaderboard): lamports, payout status and signature.</summary>
    public static async Task<Dictionary<Guid, (long lamports, string? status, string? signature)>> ByPlayerAsync(GameDb db, DateTime week, string map)
    {
        var rows = await (from r in db.WeeklyRewards.AsNoTracking()
                          join p in db.Payouts.AsNoTracking() on r.PayoutId equals (long?)p.Id into ps
                          from p in ps.DefaultIfEmpty()
                          where r.WeekStart == week && r.MapId == map
                          select new { r.PlayerId, r.Lamports, Status = p == null ? null : p.Status, Signature = p == null ? null : p.Signature })
            .ToListAsync();
        return rows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => (g.Sum(x => x.Lamports), (string?)g.First().Status, (string?)g.First().Signature));
    }

    /// <summary>Background pass: pays the previous week when it has no recorded rewards yet, then retries failed payouts.</summary>
    public async Task AutoPayAsync()
    {
        if (!_sol.TreasuryOn) return;
        var week = PreviousWeek;
        var map = RunService.NormalizeMap(_config["WEEKLY_REWARD_MAP"]);
        var recorded = await _db.WeeklyRewards.Where(r => r.WeekStart == week && r.MapId == map).Select(r => r.PayoutId).ToListAsync();
        if (recorded.Count > 0)
        {
            var ids = recorded.Where(x => x != null).Select(x => x!.Value).ToList();
            var settled = await _db.Payouts.CountAsync(p => ids.Contains(p.Id) && p.Status != Payout.Failed);
            if (settled == recorded.Count) return;
        }
        await PayWeekAsync(week, map, dryRun: false);
    }
}

/// <summary>Hackathon: pays the previous week's leaderboard rewards (WEEKLY_REWARDS_AUTO=false turns it off). Runs every
/// WEEKLY_REWARDS_INTERVAL_MINUTES (default 60), the first pass one interval after startup.</summary>
public sealed class WeeklyRewardJob : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly IConfiguration _config;
    readonly ILogger<WeeklyRewardJob> _log;

    public WeeklyRewardJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<WeeklyRewardJob> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(_config["WEEKLY_REWARDS_AUTO"], out var on) && !on) return;
        var minutes = int.TryParse(_config["WEEKLY_REWARDS_INTERVAL_MINUTES"], out var m) && m > 0 ? m : 60;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RewardService>().AutoPayAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Weekly reward pass failed");
            }
        }
    }
}
