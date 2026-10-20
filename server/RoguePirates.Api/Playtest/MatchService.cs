using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;

namespace RoguePirates.Api.Playtest;

/// <summary>Match results: both clients report; agreement (or one report after 60 s) decides the match and updates Elo.</summary>
public sealed class MatchService
{
    public static readonly TimeSpan SingleReportTimeout = TimeSpan.FromSeconds(60);
    public const int MaxDurationSeconds = 7200;

    readonly GameDb _db;
    readonly IClock _clock;
    readonly LobbyService _lobbies;
    readonly ILogger<MatchService> _log;
    readonly MatchRelay _relay;

    public const string ServerDecisionPrefix = "server_";

    public MatchService(GameDb db, IClock clock, LobbyService lobbies, ILogger<MatchService> log, MatchRelay relay)
    {
        _db = db;
        _clock = clock;
        _lobbies = lobbies;
        _log = log;
        _relay = relay;
    }

    /// <summary>
    /// Records the relay's decision (sunk, time, forfeit, draw): replaces the two-report agreement for refereed matches.
    /// Rated matches update Elo (a draw scores 0.5 each); bot matches and abandoned matches do not. Returns the rating
    /// change per player id. A match already decided or disputed is left as it is.
    /// </summary>
    public async Task<Dictionary<string, object>> DecideByServerAsync(Guid matchId, RelayEnd end)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var match = await _db.Matches.FirstAsync(m => m.Id == matchId);
            var ratings = new Dictionary<string, object>();
            if (match.Status is MatchStates.Decided or MatchStates.Disputed) return ratings;

            var host = await _db.Players.FirstOrDefaultAsync(p => p.Id == match.HostPlayerId);
            var guest = await _db.Players.FirstOrDefaultAsync(p => p.Id == match.GuestPlayerId);
            match.HostRatingBefore = host?.Rating;
            match.GuestRatingBefore = guest?.Rating;
            if (end.Rated && host != null && guest != null)
            {
                if (end.Winner is { } winnerId)
                {
                    var winner = winnerId == host.Id ? host : guest;
                    var loser = winnerId == host.Id ? guest : host;
                    (winner.Rating, loser.Rating) = Elo.Update(winner.Rating, loser.Rating);
                    winner.Wins++;
                    loser.Losses++;
                }
                else
                    (host.Rating, guest.Rating) = Elo.Draw(host.Rating, guest.Rating);
            }
            match.HostRatingAfter = host?.Rating;
            match.GuestRatingAfter = guest?.Rating;

            match.Status = MatchStates.Decided;
            match.WinnerPlayerId = end.Winner;
            match.DurationSeconds = end.DurationSeconds;
            match.FinalStatsJson = JsonSerializer.Serialize(end.Stats, LobbyService.Json);
            match.DecidedBy = ServerDecisionPrefix + end.Reason;
            match.DecidedAt = _clock.UtcNow;

            var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == match.LobbyId);
            if (lobby != null && lobby.Status is LobbyStates.Starting or LobbyStates.InMatch)
            {
                lobby.Status = LobbyStates.Finished;
                lobby.UpdatedAt = _clock.UtcNow;
            }
            await _db.SaveChangesAsync();
            if (lobby != null) await _lobbies.PublishAsync(lobby);

            foreach (var (id, before, after) in new[]
                     {
                         (match.HostPlayerId, match.HostRatingBefore, match.HostRatingAfter),
                         (match.GuestPlayerId, match.GuestRatingBefore, match.GuestRatingAfter),
                     })
                if (before != null) ratings[id.ToString()] = new { before, after, change = after - before };
            return ratings;
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    public async Task<object> SubmitAsync(Guid matchId, Player player, MatchResultRequest req)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId)
                        ?? throw new ApiException(404, "match_not_found", "No such match.");
            if (player.Id != match.HostPlayerId && player.Id != match.GuestPlayerId)
                throw new ApiException(403, "not_in_match", "You did not play in this match.");
            if (req.WinnerPlayerId != match.HostPlayerId && req.WinnerPlayerId != match.GuestPlayerId)
                throw new ApiException(400, "invalid_winner", "winnerPlayerId must be one of the two players.");
            if (req.DurationSeconds < 0 || req.DurationSeconds > MaxDurationSeconds)
                throw new ApiException(400, "invalid_duration", $"durationSeconds must be 0..{MaxDurationSeconds}.");
            var statsJson = ValidateStats(req.Stats, match);

            if (await _db.MatchReports.AnyAsync(r => r.MatchId == matchId && r.PlayerId == player.Id))
                throw new ApiException(409, "already_reported", "You already reported this match.");

            var now = _clock.UtcNow;
            _db.MatchReports.Add(new MatchReport
            {
                MatchId = matchId, PlayerId = player.Id, WinnerPlayerId = req.WinnerPlayerId, DurationSeconds = req.DurationSeconds,
                StatsJson = statsJson, SubmittedAt = now,
            });

            // V5 M9j (review): a staked match with a live room is the relay's to decide, even before a socket connected
            // (a report sent first would otherwise decide it by timeout and void the stake)
            var stakedRoom = _relay.HasRoom(matchId) && await _db.Wagers.AnyAsync(w => w.LobbyId == match.LobbyId);
            if (match.Status is MatchStates.Pending or MatchStates.AwaitingOpponent
                && (_relay.IsRelayed(matchId) || PracticeBot.Is(match.GuestPlayerId) || stakedRoom))
            {
                // the server referees this match: the report is kept for the record and decides nothing
                await _db.SaveChangesAsync();
            }
            else if (match.Status is MatchStates.Pending)
            {
                match.Status = MatchStates.AwaitingOpponent;
                match.FirstReportAt = now;
                await _db.SaveChangesAsync();
            }
            else if (match.Status is MatchStates.AwaitingOpponent)
            {
                await _db.SaveChangesAsync();
                var reports = await _db.MatchReports.Where(r => r.MatchId == matchId).OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).ToListAsync();
                if (reports.Select(r => r.WinnerPlayerId).Distinct().Count() == 1)
                    await DecideAsync(match, reports, "agreement");
                else
                {
                    match.Status = MatchStates.Disputed;
                    _log.LogWarning("Match {MatchId} disputed: reports name different winners", matchId);
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                // already decided (by timeout) or disputed: keep the report for the record only
                await _db.SaveChangesAsync();
            }

            return await ViewAsync(match);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    public async Task<object> GetAsync(Guid matchId)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId)
                        ?? throw new ApiException(404, "match_not_found", "No such match.");
            await ResolveTimeoutAsync(match);
            return await ViewAsync(match);
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    /// <summary>Decides every match whose single report is older than 60 s (sweeper).</summary>
    public async Task<int> ResolveTimeoutsAsync()
    {
        var cutoff = _clock.UtcNow - SingleReportTimeout;
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var due = await _db.Matches.Where(m => m.Status == MatchStates.AwaitingOpponent && m.FirstReportAt <= cutoff).ToListAsync();
            foreach (var m in due) await ResolveTimeoutAsync(m);
            return due.Count;
        }
        finally
        {
            GameLocks.Lobby.Release();
        }
    }

    async Task ResolveTimeoutAsync(Match match)
    {
        if (match.Status != MatchStates.AwaitingOpponent || match.FirstReportAt + SingleReportTimeout > _clock.UtcNow) return;
        var reports = await _db.MatchReports.Where(r => r.MatchId == match.Id).ToListAsync();
        if (reports.Count == 1) await DecideAsync(match, reports, "timeout");
    }

    async Task DecideAsync(Match match, List<MatchReport> reports, string decidedBy)
    {
        var winnerId = reports[0].WinnerPlayerId;
        var loserId = winnerId == match.HostPlayerId ? match.GuestPlayerId : match.HostPlayerId;
        var winner = await _db.Players.FirstAsync(p => p.Id == winnerId);
        var loser = await _db.Players.FirstAsync(p => p.Id == loserId);

        var (winnerAfter, loserAfter) = Elo.Update(winner.Rating, loser.Rating);
        var hostIsWinner = winnerId == match.HostPlayerId;
        match.HostRatingBefore = hostIsWinner ? winner.Rating : loser.Rating;
        match.GuestRatingBefore = hostIsWinner ? loser.Rating : winner.Rating;
        match.HostRatingAfter = hostIsWinner ? winnerAfter : loserAfter;
        match.GuestRatingAfter = hostIsWinner ? loserAfter : winnerAfter;
        winner.Rating = winnerAfter;
        winner.Wins++;
        loser.Rating = loserAfter;
        loser.Losses++;

        // Each player's stats come from their own report when they sent one, otherwise from the opponent's.
        var parsed = reports.ToDictionary(r => r.PlayerId,
            r => JsonSerializer.Deserialize<Dictionary<string, PlayerMatchStats>>(r.StatsJson, LobbyService.Json) ?? new());
        var final = new Dictionary<string, PlayerMatchStats>();
        foreach (var pid in new[] { match.HostPlayerId, match.GuestPlayerId })
        {
            var key = pid.ToString();
            if (parsed.TryGetValue(pid, out var own) && own.TryGetValue(key, out var s)) final[key] = s;
            else if (parsed.Values.Select(d => d.GetValueOrDefault(key)).FirstOrDefault(x => x != null) is { } other) final[key] = other;
        }

        match.Status = MatchStates.Decided;
        match.WinnerPlayerId = winnerId;
        match.DurationSeconds = (int)Math.Round(reports.Average(r => r.DurationSeconds));
        match.FinalStatsJson = JsonSerializer.Serialize(final, LobbyService.Json);
        match.DecidedBy = decidedBy;
        match.DecidedAt = _clock.UtcNow;

        var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == match.LobbyId);
        if (lobby != null && lobby.Status is LobbyStates.Starting or LobbyStates.InMatch)
        {
            lobby.Status = LobbyStates.Finished;
            lobby.UpdatedAt = _clock.UtcNow;
        }
        await _db.SaveChangesAsync();
        if (lobby != null) await _lobbies.PublishAsync(lobby);
    }

    static string ValidateStats(Dictionary<string, PlayerMatchStats>? stats, Match match)
    {
        stats ??= new();
        var allowed = new[] { match.HostPlayerId, match.GuestPlayerId };
        var normalized = new Dictionary<string, PlayerMatchStats>();
        foreach (var (key, s) in stats)
        {
            if (!Guid.TryParse(key, out var pid) || !allowed.Contains(pid))
                throw new ApiException(400, "invalid_stats", "stats keys must be the two players' ids.");
            if (s == null || s.DamageDealt < 0 || s.DamageTaken < 0 || s.Kills is < 0 || s.Level < 1 || s.Level > RunService.MaxLevel)
                throw new ApiException(400, "invalid_stats", "stats values must be non-negative and level 1..200.");
            static List<string>? Ids(List<string>? ids, string field)
            {
                if (ids == null) return null;
                if (ids.Count > 12 || ids.Any(i => string.IsNullOrWhiteSpace(i) || i.Length > 64))
                    throw new ApiException(400, "invalid_stats", $"stats {field} must be at most 12 non-empty ids.");
                return ids;
            }
            normalized[pid.ToString()] = s with { Weapons = Ids(s.Weapons, "weapons"), Logbooks = Ids(s.Logbooks, "logbooks") };
        }
        return JsonSerializer.Serialize(normalized, LobbyService.Json);
    }

    async Task<object> ViewAsync(Match match)
    {
        var ids = new[] { match.HostPlayerId, match.GuestPlayerId };
        var players = await _db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var reports = await _db.MatchReports.CountAsync(r => r.MatchId == match.Id);
        var stats = match.FinalStatsJson == null
            ? new Dictionary<string, PlayerMatchStats>()
            : JsonSerializer.Deserialize<Dictionary<string, PlayerMatchStats>>(match.FinalStatsJson, LobbyService.Json) ?? new();

        object PlayerView(Guid id, int? before, int? after)
        {
            players.TryGetValue(id, out var p);
            return new
            {
                playerId = id, name = PracticeBot.Is(id) ? PracticeBot.Name : p?.Name, tag = PlayerTags.For(id), bot = PracticeBot.Is(id),
                won = match.WinnerPlayerId == null ? (bool?)null : match.WinnerPlayerId == id,
                ratingBefore = before, ratingAfter = after, ratingChange = after - before,
                stats = stats.GetValueOrDefault(id.ToString()),
            };
        }

        return new
        {
            matchId = match.Id, lobbyId = match.LobbyId, status = match.Status, winnerPlayerId = match.WinnerPlayerId,
            durationSeconds = match.DurationSeconds, decidedBy = match.DecidedBy, decidedAt = match.DecidedAt,
            reason = match.DecidedBy is { } by && by.StartsWith(ServerDecisionPrefix, StringComparison.Ordinal) ? by[ServerDecisionPrefix.Length..] : null,
            reportsReceived = reports,
            decideAfter = match.Status == MatchStates.AwaitingOpponent ? match.FirstReportAt + SingleReportTimeout : null,
            players = new[]
            {
                PlayerView(match.HostPlayerId, match.HostRatingBefore, match.HostRatingAfter),
                PlayerView(match.GuestPlayerId, match.GuestRatingBefore, match.GuestRatingAfter),
            },
        };
    }
}

/// <summary>Background sweep: expires idle lobbies and decides matches whose second report never came.</summary>
public sealed class PlaytestSweeper : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly ApiSettings _settings;
    readonly ILogger<PlaytestSweeper> _log;

    public PlaytestSweeper(IServiceScopeFactory scopes, ApiSettings settings, ILogger<PlaytestSweeper> log)
    {
        _scopes = scopes;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.SweepSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LobbyService>().ExpireIdleAsync();
                await scope.ServiceProvider.GetRequiredService<MatchService>().ResolveTimeoutsAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Playtest sweep failed");
            }
        }
    }
}
