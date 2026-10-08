using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;

namespace RoguePirates.Api.Playtest;

/// <summary>Run submission, weekly bests and the weekly leaderboard.</summary>
public sealed partial class RunService
{
    public const string DefaultMap = "saltwind";
    public const int MinRunSeconds = 10, MaxRunSeconds = 7200, MaxKillsPerMinute = 600, MinLevel = 1, MaxLevel = 200;

    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex MapIdPattern();

    readonly GameDb _db;
    readonly IClock _clock;
    readonly ILogger<RunService> _log;
    readonly PlausibilityService _checks;

    public RunService(GameDb db, IClock clock, ILogger<RunService> log, PlausibilityService checks)
    {
        _db = db;
        _clock = clock;
        _log = log;
        _checks = checks;
    }

    public static string NormalizeMap(string? mapId)
    {
        var m = string.IsNullOrWhiteSpace(mapId) ? DefaultMap : mapId.Trim().ToLowerInvariant();
        if (!MapIdPattern().IsMatch(m)) throw new ApiException(400, "invalid_map", "mapId must be 1-32 characters of a-z, 0-9, _ or -.");
        return m;
    }

    static string? Short(string? v, int max, string field)
    {
        if (v == null) return null;
        v = v.Trim();
        if (v.Length > max) throw new ApiException(400, "invalid_" + field, $"{field} is longer than {max} characters.");
        return v.Length == 0 ? null : v;
    }

    public async Task<object> SubmitAsync(Player player, SubmitRunRequest req)
    {
        var map = NormalizeMap(req.MapId);
        var captain = Short(req.Captain, 32, "captain");
        var buildHash = Short(req.BuildHash, 128, "buildHash");
        var clientVersion = Short(req.ClientVersion, 32, "clientVersion");

        string? reason = null;
        if (req.Kills < 0) reason = "kills must be >= 0";
        else if (req.RunSeconds < MinRunSeconds || req.RunSeconds > MaxRunSeconds) reason = $"runSeconds must be {MinRunSeconds}..{MaxRunSeconds}";
        else if (req.Level < MinLevel || req.Level > MaxLevel) reason = $"level must be {MinLevel}..{MaxLevel}";
        else if ((long)req.Kills * 60 > (long)MaxKillsPerMinute * req.RunSeconds) reason = $"more than {MaxKillsPerMinute} kills per minute";
        if (reason != null)
        {
            _log.LogWarning("Rejected run from player {PlayerId}: {Reason} (kills {Kills}, seconds {Seconds}, level {Level}, map {Map}, build {Build}, client {Client})",
                player.Id, reason, req.Kills, req.RunSeconds, req.Level, map, buildHash, clientVersion);
            throw new ApiException(422, "implausible_run", reason);
        }

        // V5 M9a (I9a.1): the run log, when the client sent one (older clients do not), structural checks only. Shadow mode
        // (owner decision of 8 Oct): a log that fails them does not refuse the run; it is accepted and ranks, its verdict is
        // log_rejected with the reason code, and the raw log is kept (capped) for diagnosis. Only an oversized request body is
        // refused (413, before this runs: RunLogRules.MaxRequestBodyBytes on the endpoint).
        RunLogRules.Checked? log = null;
        RunLogRules.Rejected? rejected = null;
        try { log = RunLogRules.Check(req.Log, req); }
        catch (ApiException ex)
        {
            rejected = RunLogRules.Reject(req.Log!.Value, ex);
            _log.LogWarning("Run log of player {PlayerId} failed the structural checks (shadow mode, run accepted as log_rejected): {Code} {Reason} (kills {Kills}, seconds {Seconds}, level {Level}, client {Client}, {Bytes} bytes)",
                player.Id, ex.Code, ex.Message, req.Kills, req.RunSeconds, req.Level, clientVersion, rejected.Bytes);
        }

        var header = log?.Header ?? rejected?.Header;
        var now = _clock.UtcNow;
        var week = WeekMath.WeekStart(now);
        RunVerdict verdict;

        await GameLocks.Runs.WaitAsync();
        try
        {
            var run = new RunRecord
            {
                PlayerId = player.Id, Kills = req.Kills, RunSeconds = req.RunSeconds, Level = req.Level, Captain = captain,
                MapId = map, BuildHash = buildHash, ClientVersion = clientVersion, WeekStart = week, SubmittedAt = now,
            };
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                _db.Runs.Add(run);
                await _db.SaveChangesAsync();
                if (header != null)
                {
                    _db.RunLogs.Add(new RunLog
                    {
                        RunId = run.Id, Version = header.Version, Seed = header.Seed, SeedSource = header.SeedSource, ConfigHash = header.ConfigHash,
                        BalanceVersion = header.BalanceVersion, BuildId = header.BuildId, ClientRunId = header.ClientRunId,
                        Bytes = log?.Bytes ?? rejected!.Bytes, Json = log?.Json ?? rejected!.Json,
                        RejectCode = rejected?.Code, RejectReason = rejected?.Reason,
                    });
                    await _db.SaveChangesAsync();
                }
                // V5 M9a (I9a.3): the run checks' verdict, in shadow mode (D23): stored, never a reason to reject; a flagged
                // run still counts on the board and still pays out (D24).
                verdict = _checks.Score(run, log?.Json, header?.BuildId, header?.ConfigHash, now, rejected?.Code);
                _db.RunVerdicts.Add(verdict);
                await _db.SaveChangesAsync();
                await tx.CommitAsync(); // the run, its log and its verdict together
            }

            var best = await _db.WeeklyBests.FirstOrDefaultAsync(b => b.PlayerId == player.Id && b.WeekStart == week && b.MapId == map);
            var isNewBest = best == null || req.Kills > best.Kills || (req.Kills == best.Kills && req.RunSeconds < best.RunSeconds);
            if (best == null)
            {
                best = new WeeklyBest { PlayerId = player.Id, WeekStart = week, MapId = map };
                _db.WeeklyBests.Add(best);
            }
            if (isNewBest)
            {
                best.Kills = req.Kills;
                best.RunSeconds = req.RunSeconds;
                best.Level = req.Level;
                best.Captain = captain;
                best.RunId = run.Id;
                best.AchievedAt = now;
                await _db.SaveChangesAsync();
            }

            return new
            {
                accepted = true,
                runId = run.Id,
                weekStart = week,
                weekEnd = week.AddDays(7),
                isNewWeeklyBest = isNewBest,
                logStored = header != null,
                @checked = verdict.Status == Verdicts.Pass,
                verdict = verdict.Status,
                logRejected = rejected?.Code,
                weeklyBest = new { kills = best.Kills, runSeconds = best.RunSeconds, level = best.Level, captain = best.Captain },
            };
        }
        finally
        {
            GameLocks.Runs.Release();
        }
    }

    public async Task<object> LeaderboardAsync(DateTime weekStart, string? mapId, int? limit, Player? me)
    {
        var map = NormalizeMap(mapId);
        var take = Math.Clamp(limit ?? 50, 1, 100);

        // V5 M9a (I9a.3): "checked" = the entry's run passed the run checks (shadow mode: unchecked runs still rank, D23).
        var rows = await (from b in _db.WeeklyBests
                          join p in _db.Players on b.PlayerId equals p.Id
                          join v in _db.RunVerdicts on b.RunId equals v.RunId into vs
                          from v in vs.DefaultIfEmpty()
                          where b.WeekStart == weekStart && b.MapId == map
                          orderby b.Kills descending, b.RunSeconds, b.AchievedAt, b.Id
                          select new { b.PlayerId, p.Name, b.Kills, b.RunSeconds, b.Level, b.Captain, Checked = v != null && v.Status == Verdicts.Pass })
            .Take(take)
            .ToListAsync();

        var entries = rows.Select((r, i) => new
        {
            rank = i + 1, playerId = r.PlayerId, name = r.Name, tag = PlayerTags.For(r.PlayerId),
            kills = r.Kills, runSeconds = r.RunSeconds, level = r.Level, captain = r.Captain, @checked = r.Checked,
        }).ToList();

        object? meView = null;
        if (me != null)
        {
            var mine = await _db.WeeklyBests.FirstOrDefaultAsync(b => b.PlayerId == me.Id && b.WeekStart == weekStart && b.MapId == map);
            if (mine != null)
            {
                var better = await _db.WeeklyBests.CountAsync(b => b.WeekStart == weekStart && b.MapId == map && (
                    b.Kills > mine.Kills
                    || (b.Kills == mine.Kills && b.RunSeconds < mine.RunSeconds)
                    || (b.Kills == mine.Kills && b.RunSeconds == mine.RunSeconds && b.AchievedAt < mine.AchievedAt)
                    || (b.Kills == mine.Kills && b.RunSeconds == mine.RunSeconds && b.AchievedAt == mine.AchievedAt && b.Id < mine.Id)));
                var mineChecked = await _db.RunVerdicts.AnyAsync(v => v.RunId == mine.RunId && v.Status == Verdicts.Pass);
                meView = new { rank = better + 1, kills = mine.Kills, runSeconds = mine.RunSeconds, level = mine.Level, @checked = mineChecked };
            }
        }

        return new { mapId = map, weekStart, weekEnd = weekStart.AddDays(7), entries, me = meView };
    }

    public async Task<object> StatsAsync(Player player)
    {
        var week = WeekMath.WeekStart(_clock.UtcNow);
        var runs = await _db.Runs.CountAsync(r => r.PlayerId == player.Id);
        var bestAll = await _db.Runs.Where(r => r.PlayerId == player.Id).MaxAsync(r => (int?)r.Kills) ?? 0;
        var bestWeek = await _db.Runs.Where(r => r.PlayerId == player.Id && r.WeekStart == week).MaxAsync(r => (int?)r.Kills) ?? 0;
        return new
        {
            playerId = player.Id, bestKillsAllTime = bestAll, bestKillsThisWeek = bestWeek, runsPlayed = runs,
            weekStart = week, weekEnd = week.AddDays(7),
        };
    }
}
