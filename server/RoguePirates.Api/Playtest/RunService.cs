using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;

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

    public RunService(GameDb db, IClock clock, ILogger<RunService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
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

        // V5 M9a (I9a.1): the run log, when the client sent one (older clients do not): structural checks only (400).
        RunLogRules.Checked? log;
        try { log = RunLogRules.Check(req.Log, req); }
        catch (ApiException ex)
        {
            _log.LogWarning("Rejected run log from player {PlayerId}: {Code} {Reason} (kills {Kills}, seconds {Seconds}, level {Level}, client {Client})",
                player.Id, ex.Code, ex.Message, req.Kills, req.RunSeconds, req.Level, clientVersion);
            throw;
        }

        var now = _clock.UtcNow;
        var week = WeekMath.WeekStart(now);

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
                if (log != null)
                {
                    var h = log.Header;
                    _db.RunLogs.Add(new RunLog
                    {
                        RunId = run.Id, Version = h.Version, Seed = h.Seed, SeedSource = h.SeedSource, ConfigHash = h.ConfigHash,
                        BalanceVersion = h.BalanceVersion, BuildId = h.BuildId, ClientRunId = h.ClientRunId, Bytes = log.Bytes, Json = log.Json,
                    });
                    await _db.SaveChangesAsync();
                }
                await tx.CommitAsync(); // the run and its log together
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
                logStored = log != null,
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

        var rows = await (from b in _db.WeeklyBests
                          join p in _db.Players on b.PlayerId equals p.Id
                          where b.WeekStart == weekStart && b.MapId == map
                          orderby b.Kills descending, b.RunSeconds, b.AchievedAt, b.Id
                          select new { b.PlayerId, p.Name, b.Kills, b.RunSeconds, b.Level, b.Captain })
            .Take(take)
            .ToListAsync();

        var entries = rows.Select((r, i) => new
        {
            rank = i + 1, playerId = r.PlayerId, name = r.Name, tag = PlayerTags.For(r.PlayerId),
            kills = r.Kills, runSeconds = r.RunSeconds, level = r.Level, captain = r.Captain,
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
                meView = new { rank = better + 1, kills = mine.Kills, runSeconds = mine.RunSeconds, level = mine.Level };
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
