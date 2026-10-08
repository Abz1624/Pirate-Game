using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Plausibility;

/// <summary>
/// V5 M9a (I9a.3): the run checks in shadow mode (D23). Every uploaded run gets a verdict (pass, flagged or
/// outdated_client; log_rejected when the log failed the structural checks) with its reason codes, rule ids, the rules version and the client build; nothing is rejected or
/// withheld because of it, and a flagged run still counts and still pays out (D24). The weekly board shows the verdict as
/// a "checked" flag. When the rules version changes, the stored runs are re-scored (<see cref="RescoreAsync"/>, run by
/// <see cref="PlausibilityRescorer"/> at startup).
/// </summary>
public sealed class PlausibilityService
{
    readonly ILogger<PlausibilityService> _log;

    public PlausibilityService(PlausibilityRuleSet rules, ILogger<PlausibilityService> log)
    {
        Rules = rules;
        _log = log;
    }

    public PlausibilityRuleSet Rules { get; }

    /// <summary>The rule id of a structurally rejected log's verdict.</summary>
    public const string LogRule = "PR-LOG";

    /// <summary>The verdict row for a run and its stored log JSON (null when the client sent no log).
    /// <paramref name="logRejectCode"/> set (the log failed the structural checks) gives <c>log_rejected</c> with that code.</summary>
    public RunVerdict Score(RunRecord run, string? logJson, string? logBuildId, string? logConfigHash, DateTime now, string? logRejectCode = null)
    {
        PlausibilityVerdict v;
        if (logRejectCode != null)
            v = new PlausibilityVerdict(Verdicts.LogRejected, new[] { new PlausibilityReason(logRejectCode, LogRule) }, Rules.RulesVersion);
        else try { v = Rules.Evaluate(logJson); }
        catch (Exception ex) // never let the checker fail an upload (shadow mode)
        {
            _log.LogError(ex, "Plausibility check failed on run {RunId}", run.Id);
            v = new PlausibilityVerdict(Verdicts.OutdatedClient, new[] { new PlausibilityReason("check_failed", "PR-VERSION") }, Rules.RulesVersion);
        }
        var row = new RunVerdict { RunId = run.Id };
        Apply(row, v, logBuildId ?? run.ClientVersion ?? run.BuildHash, logConfigHash, now);
        if (v.Status == Verdicts.Flagged)
            _log.LogWarning("Run {RunId} of player {PlayerId} flagged by the run checks (shadow mode, still counts): {Reasons} (rules v{Version}, build {Build})",
                run.Id, run.PlayerId, string.Join(", ", v.Reasons.Select(r => $"{r.Code} [{r.Rule}] minute {r.Minute}: {r.Value} > {r.Bound}")), v.RulesVersion, row.ClientBuild);
        return row;
    }

    static void Apply(RunVerdict row, PlausibilityVerdict v, string? build, string? configHash, DateTime now)
    {
        row.Status = v.Status;
        row.ReasonCodes = Clip(string.Join(",", v.Reasons.Select(r => r.Code)), 512);
        row.RuleIds = Clip(string.Join(",", v.Reasons.Select(r => r.Rule)), 512);
        row.DetailsJson = JsonSerializer.Serialize(v.Reasons.Select(r => new { code = r.Code, rule = r.Rule, minute = r.Minute, value = r.Value, bound = r.Bound }));
        row.RulesVersion = v.RulesVersion;
        row.ClientBuild = build == null ? null : Clip(build, 64);
        row.ConfigHash = configHash == null ? null : Clip(configHash, 128);
        row.EvaluatedAt = now;
    }

    static string Clip(string s, int max) => s.Length <= max ? s : s[..max];

    /// <summary>
    /// Scores every stored run that has no verdict or one from another rules version, in batches (oldest first). Returns
    /// how many verdicts were written. Safe to run while runs are uploaded (it takes the runs lock per batch).
    /// </summary>
    public async Task<int> RescoreAsync(GameDb db, IClock clock, CancellationToken ct = default, int batch = 200)
    {
        var version = Rules.RulesVersion;
        var done = 0;
        long after = 0;
        while (!ct.IsCancellationRequested)
        {
            var ids = await (from r in db.Runs
                             join v in db.RunVerdicts on r.Id equals v.RunId into vs
                             from v in vs.DefaultIfEmpty()
                             where r.Id > after && (v == null || v.RulesVersion != version)
                             orderby r.Id
                             select r.Id).Take(batch).ToListAsync(ct);
            if (ids.Count == 0) break;
            await GameLocks.Runs.WaitAsync(ct);
            try
            {
                var runs = await db.Runs.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
                var logs = await db.RunLogs.Where(l => ids.Contains(l.RunId)).ToDictionaryAsync(l => l.RunId, ct);
                var verdicts = await db.RunVerdicts.Where(v => ids.Contains(v.RunId)).ToDictionaryAsync(v => v.RunId, ct);
                var now = clock.UtcNow;
                foreach (var run in runs)
                {
                    logs.TryGetValue(run.Id, out var log);
                    var fresh = Score(run, log?.Json, log?.BuildId, log?.ConfigHash, now, log?.RejectCode);
                    if (verdicts.TryGetValue(run.Id, out var row))
                    {
                        row.Status = fresh.Status; row.ReasonCodes = fresh.ReasonCodes; row.RuleIds = fresh.RuleIds; row.DetailsJson = fresh.DetailsJson;
                        row.RulesVersion = fresh.RulesVersion; row.ClientBuild = fresh.ClientBuild; row.ConfigHash = fresh.ConfigHash; row.EvaluatedAt = now;
                    }
                    else db.RunVerdicts.Add(fresh);
                    done++;
                }
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
            finally
            {
                GameLocks.Runs.Release();
            }
            after = ids[^1];
        }
        return done;
    }
}

/// <summary>
/// V5 M9a (I9a.3): re-scores the stored runs once at startup, in the background (no admin step), when the rules version
/// changed since they were scored (or they have no verdict yet, like the runs uploaded before the checks existed).
/// </summary>
public sealed class PlausibilityRescorer : BackgroundService
{
    readonly IServiceProvider _services;
    readonly ILogger<PlausibilityRescorer> _log;

    public PlausibilityRescorer(IServiceProvider services, ILogger<PlausibilityRescorer> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<PlausibilityService>();
            var n = await svc.RescoreAsync(scope.ServiceProvider.GetRequiredService<GameDb>(), scope.ServiceProvider.GetRequiredService<IClock>(), stoppingToken);
            _log.LogInformation("Run checks: rules v{Version} ({Mode} mode); {Count} stored run(s) scored", svc.Rules.RulesVersion, svc.Rules.Mode, n);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogError(ex, "Run checks: re-scoring the stored runs failed (the next start tries again)");
        }
    }
}
