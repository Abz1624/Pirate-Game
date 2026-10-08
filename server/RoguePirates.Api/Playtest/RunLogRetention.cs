using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;

namespace RoguePirates.Api.Playtest;

/// <summary>
/// V5 M9a (review): run-log retention. Logs of runs submitted more than <see cref="ApiSettings.RunLogRetentionDays"/> ago
/// (default 8 weeks) are deleted, unless the run's verdict is flagged or log_rejected (kept for diagnosis and disputes).
/// The run and its verdict stay; a later re-score of a run without its log gives outdated_client (no_log).
/// Runs once at startup, then every <see cref="ApiSettings.RunLogRetentionSweepHours"/>.
/// </summary>
public sealed class RunLogRetention : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly ApiSettings _settings;
    readonly ILogger<RunLogRetention> _log;

    public RunLogRetention(IServiceScopeFactory scopes, ApiSettings settings, ILogger<RunLogRetention> log)
    {
        _scopes = scopes;
        _settings = settings;
        _log = log;
    }

    /// <summary>Deletes the logs older than <paramref name="keep"/> at <paramref name="now"/> whose run is neither flagged nor
    /// log_rejected. Returns how many were deleted.</summary>
    public static async Task<int> PurgeAsync(GameDb db, DateTime now, TimeSpan keep, CancellationToken ct = default)
    {
        var cutoff = now - keep;
        await GameLocks.Runs.WaitAsync(ct); // not while the re-scorer or an upload is reading or writing logs
        try
        {
            return await db.RunLogs
                .Where(l => l.RejectCode == null
                            && db.Runs.Any(r => r.Id == l.RunId && r.SubmittedAt < cutoff)
                            && !db.RunVerdicts.Any(v => v.RunId == l.RunId && (v.Status == Verdicts.Flagged || v.Status == Verdicts.LogRejected)))
                .ExecuteDeleteAsync(ct);
        }
        finally
        {
            GameLocks.Runs.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var keep = TimeSpan.FromDays(_settings.RunLogRetentionDays);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_settings.RunLogRetentionSweepHours));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var n = await PurgeAsync(scope.ServiceProvider.GetRequiredService<GameDb>(), scope.ServiceProvider.GetRequiredService<IClock>().UtcNow, keep, stoppingToken);
                if (n > 0) _log.LogInformation("Run-log retention: {Count} log(s) older than {Days} days deleted (flagged and rejected kept)", n, _settings.RunLogRetentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Run-log retention sweep failed (the next sweep tries again)");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
