using System.Text.Json;
using RoguePirates.Api.Data;
using RoguePirates.Api.Plausibility;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Hackathon;

/// <summary>
/// V5 M9j (20 Oct, owner: "make the server decide or verify game results"): the server's checks on a client-reported raid
/// result. <see cref="Timing"/> is enforced (an honest client cannot fail it: the game puts the guardian at
/// <see cref="GuardianArrivalSeconds"/> of run time, its run clock never runs faster than the wall clock, and the result is
/// posted after the fight); <see cref="ScoreLog"/> is shadow mode (a verdict stored with the raid, never a reason to refuse).
/// </summary>
public static class RaidChecks
{
    /// <summary>Seconds of slack on every timing bound (rounding: the client reports min(ceil(run time), floor(wall) - 1)).</summary>
    public const int SlackSeconds = 5;
    /// <summary>The largest gap between the log's run time and the reported seconds before the log's verdict is time_mismatch.</summary>
    public const double LogSecondsTolerance = 10;

    /// <summary>When the guardian arrives, in run seconds: the start of its one-minute window, (minute - 1) x 60 (the game's
    /// <c>RaidHud.GuardianSeconds</c>).</summary>
    public static int GuardianArrivalSeconds(int guardianMinute) => Math.Max(0, guardianMinute - 1) * IslandRules.DefenceWindowSeconds;

    /// <summary>The latest window a raid of <paramref name="seconds"/> can have reached: minute m starts at (m - 1) x 60;
    /// minute 16 (past the 15 windows) needs the 960 s time limit (the game's <c>RaidHud.MinuteReached</c>).</summary>
    public static int MaxMinuteFor(int seconds)
    {
        var s = seconds + SlackSeconds;
        if (s >= IslandRules.RaidTimeLimitSeconds) return IslandRules.DefenceWindows + 1;
        return Math.Min(IslandRules.DefenceWindows, s / IslandRules.DefenceWindowSeconds + 1);
    }

    /// <summary>The enforced timing check of a raid result, or null when it holds. A win or a partial needs the guardian to
    /// have arrived: the reported seconds and the server's elapsed time since the raid's start both reach its arrival (less
    /// the slack); the old won/partial flags get the same check. <paramref name="minuteReached"/> may not be beyond the
    /// reported time.</summary>
    public static ApiException? Timing(string outcome, int seconds, double elapsedSeconds, int? minuteReached, int guardianMinute)
    {
        if (minuteReached is { } mr && mr > MaxMinuteFor(seconds))
            return new ApiException(400, "invalid_minute", $"minute {mr} cannot be reached in {seconds} seconds.");
        if (outcome == Raid.Lost) return null;
        var arrival = GuardianArrivalSeconds(guardianMinute);
        if (seconds + SlackSeconds < arrival)
            return new ApiException(400, "too_early", $"The guardian arrives {arrival} seconds into the raid; this result reports {seconds}.");
        if (elapsedSeconds + SlackSeconds < arrival)
            return new ApiException(400, "too_early", $"The guardian arrives {arrival} seconds into the raid; only {elapsedSeconds:0} seconds have passed since it started.");
        return null;
    }

    /// <summary>The run checks' verdict on a raid's run log (shadow mode), with a reason; null without a log. Structure (the
    /// M9a rules against the log's own totals), the raid's seed, the log's time against the reported seconds, a sunk
    /// guardian defeated in the log not before its arrival, then the voyage rate rules (v9; a raid's designed waves are not
    /// the voyage director's, so a flag here is a signal, not proof).</summary>
    public static (string verdict, string? reason)? ScoreLog(JsonElement? log, Raid raid, int seconds, bool guardianSunk, int guardianMinute,
        Player player, PlausibilityService checks, DateTime now)
    {
        if (log is not { } l || l.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var (kills, level, logSeconds) = Totals(l);
        var summary = new SubmitRunRequest(kills, (int)Math.Floor(logSeconds), level, null, null, null, null, l);
        RunLogRules.Checked checkedLog;
        try
        {
            checkedLog = RunLogRules.Check(l, summary)!;
        }
        catch (ApiException ex)
        {
            return (Verdicts.LogRejected, ex.Code + ": " + ex.Message);
        }
        if (checkedLog.Header.Seed != raid.Seed) return ("seed_mismatch", $"log seed {checkedLog.Header.Seed}, raid seed {raid.Seed}");
        if (Math.Abs(logSeconds - seconds) > LogSecondsTolerance)
            return ("time_mismatch", $"log {logSeconds:0.#} s, reported {seconds} s");
        if (guardianSunk)
        {
            var defeated = Number(l, "bossDefeated");
            var arrival = GuardianArrivalSeconds(guardianMinute);
            if (defeated < 0) return ("no_boss_defeated", "the log has no boss defeated");
            if (defeated + SlackSeconds < arrival) return ("boss_before_arrival", $"boss defeated at {defeated:0.#} s, arrival {arrival} s");
        }
        var run = new RunRecord { PlayerId = player.Id, Kills = kills, RunSeconds = (int)Math.Floor(logSeconds), Level = level, MapId = "raid", SubmittedAt = now };
        var v = checks.Score(run, checkedLog.Json, checkedLog.Header.BuildId, checkedLog.Header.ConfigHash, now);
        return (v.Status, string.IsNullOrEmpty(v.ReasonCodes) ? null : v.ReasonCodes);
    }

    /// <summary>The log's own totals (kills summed over its minutes, the last minute's level, its seconds), read leniently:
    /// whatever is missing is 0 and the structural check names the problem.</summary>
    public static (int kills, int level, double seconds) Totals(JsonElement log)
    {
        int kills = 0, level = 1;
        if (log.ValueKind == JsonValueKind.Object && log.TryGetProperty("minutes", out var minutes) && minutes.ValueKind == JsonValueKind.Array)
            foreach (var m in minutes.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object) continue;
                if (m.TryGetProperty("kills", out var k) && k.ValueKind == JsonValueKind.Number && k.TryGetInt32(out var ki)) kills += ki;
                if (m.TryGetProperty("level", out var lv) && lv.ValueKind == JsonValueKind.Number && lv.TryGetInt32(out var li)) level = li;
            }
        return (kills, level, Math.Max(0, Number(log, "seconds")));
    }

    public static double Number(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && double.IsFinite(v.GetDouble()) ? v.GetDouble() : -1;

    public static string? Clip(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}

/// <summary>
/// V5 M9j: the daily race's own checks on an entry (shadow mode: the entry ranks either way; the race pays nothing).
/// </summary>
public static class RaceChecks
{
    public const string Ok = "ok", NoStart = "no_start", TooFast = "too_fast", TimeMismatch = "time_mismatch";
    /// <summary>Seconds of slack between the server's start and the run's clock: the start is posted when the voyage
    /// begins, so a slow link or a retry can land it after the run clock started.</summary>
    public const double StartSlackSeconds = 30;

    /// <summary>The time against the server's elapsed time since the player's start of this race, then against the log:
    /// the target must be reached in the minute the time falls in (kills before that minute below the target, and the
    /// minutes up to it at or above it).</summary>
    public static string Check(double timeSeconds, double? serverSeconds, int target, JsonElement? log)
    {
        if (serverSeconds is not { } elapsed) return NoStart;
        if (elapsed + StartSlackSeconds < timeSeconds) return TooFast;
        if (log is { ValueKind: JsonValueKind.Object } l && l.TryGetProperty("minutes", out var minutes) && minutes.ValueKind == JsonValueKind.Array)
        {
            var minute = (int)Math.Floor(timeSeconds / RunLogRules.MinuteSeconds);
            long before = 0, upTo = 0;
            var i = 0;
            foreach (var m in minutes.EnumerateArray())
            {
                var k = m.ValueKind == JsonValueKind.Object && m.TryGetProperty("kills", out var ke) && ke.ValueKind == JsonValueKind.Number && ke.TryGetInt32(out var ki) ? ki : 0;
                if (i < minute) before += k;
                if (i <= minute) upTo += k;
                i++;
            }
            // a time right on a minute boundary may have its last kills counted in either minute
            var boundary = timeSeconds - minute * RunLogRules.MinuteSeconds < 1.0;
            if (before >= target && !boundary) return TimeMismatch;
            if (upTo < target) return TimeMismatch;
        }
        return Ok;
    }
}
