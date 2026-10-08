namespace RoguePirates.Api.Data;

/// <summary>A guest profile. No sign-in; the client holds the bearer token.</summary>
public sealed class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>SHA-256 (hex) of the bearer token. The token itself is never stored.</summary>
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int Rating { get; set; } = 1000;
    public int Wins { get; set; }
    public int Losses { get; set; }
}

/// <summary>Every accepted run (counts runs played).</summary>
public sealed class RunRecord
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public int Kills { get; set; }
    public int RunSeconds { get; set; }
    public int Level { get; set; }
    public string? Captain { get; set; }
    public string MapId { get; set; } = "";
    public string? BuildHash { get; set; }
    public string? ClientVersion { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime SubmittedAt { get; set; }
}

/// <summary>
/// V5 M9a (I9a.1): the compact run log uploaded with a run (one row per run that sent one; older clients send none).
/// The header fields are copied out of the JSON for queries; <see cref="Json"/> is the log as received (structurally
/// checked by <c>RunLogRules</c>, at most <c>RunLogRules.MaxLogBytes</c>). The plausibility checker (I9a.3) reads it.
/// </summary>
public sealed class RunLog
{
    /// <summary>The run's id (<see cref="RunRecord.Id"/>); one log per run.</summary>
    public long RunId { get; set; }
    public int Version { get; set; }
    public int Seed { get; set; }
    /// <summary>Where the seed came from: local, requested or issued (server-issued from M10).</summary>
    public string SeedSource { get; set; } = "";
    public string ConfigHash { get; set; } = "";
    public string? BalanceVersion { get; set; }
    public string? BuildId { get; set; }
    /// <summary>The client's own run id (RunManager.RunId).</summary>
    public string? ClientRunId { get; set; }
    /// <summary>UTF-8 size of the log as uploaded (for a rejected log larger than the cap, more than <see cref="Json"/> holds).</summary>
    public int Bytes { get; set; }
    /// <summary>The log as uploaded; for a rejected log, cut to <c>RunLogRules.MaxLogBytes</c> (then not valid JSON).</summary>
    public string Json { get; set; } = "";
    /// <summary>Owner decision of 8 Oct (shadow mode): null for a log that passed the structural checks; otherwise the
    /// reason code it failed with (malformed_log, log_mismatch, log_too_large, ...). The run is still accepted and ranks,
    /// with the verdict <c>log_rejected</c>; the header columns hold what could be read.</summary>
    public string? RejectCode { get; set; }
    /// <summary>The structural check's message for a rejected log (null otherwise).</summary>
    public string? RejectReason { get; set; }
}

/// <summary>
/// V5 M9a (I9a.3): a run's plausibility verdict (one row per run), in shadow mode (D23): stored and shown as the weekly
/// board's "checked" flag, never used to reject or withhold anything; a flagged run still counts and still pays out (D24).
/// Re-scored when the rules version changes (<c>PlausibilityService.RescoreAsync</c>).
/// </summary>
public sealed class RunVerdict
{
    /// <summary>The run's id (<see cref="RunRecord.Id"/>).</summary>
    public long RunId { get; set; }
    /// <summary>pass, flagged, outdated_client or log_rejected (<c>Plausibility.Verdicts</c>).</summary>
    public string Status { get; set; } = "";
    /// <summary>The reason codes, comma separated (empty on a pass).</summary>
    public string ReasonCodes { get; set; } = "";
    /// <summary>The broken rules' ids, comma separated, in the order of <see cref="ReasonCodes"/>.</summary>
    public string RuleIds { get; set; } = "";
    /// <summary>The reasons with where each rule broke (JSON array of {code, rule, minute, value, bound}).</summary>
    public string DetailsJson { get; set; } = "[]";
    public int RulesVersion { get; set; }
    /// <summary>The client build: the log's buildId, else the run's client version or build hash.</summary>
    public string? ClientBuild { get; set; }
    /// <summary>The config hash the log names (null without a log).</summary>
    public string? ConfigHash { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>A player's best run of one ISO week on one map (one row per player, week and map).</summary>
public sealed class WeeklyBest
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public DateTime WeekStart { get; set; }
    public string MapId { get; set; } = "";
    public int Kills { get; set; }
    public int RunSeconds { get; set; }
    public int Level { get; set; }
    public string? Captain { get; set; }
    public long RunId { get; set; }
    public DateTime AchievedAt { get; set; }
}

public sealed class Lobby
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public bool IsPublic { get; set; }
    /// <summary>waiting, full, starting, in_match, finished, closed, expired.</summary>
    public string Status { get; set; } = LobbyStates.Waiting;
    public Guid HostPlayerId { get; set; }
    public string HostStatus { get; set; } = SeatStates.Joined;
    public string? HostDraftJson { get; set; }
    public Guid? GuestPlayerId { get; set; }
    public string? GuestStatus { get; set; }
    public string? GuestDraftJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public Guid? MatchId { get; set; }
    public long? Seed { get; set; }
    public string? MatchServer { get; set; }
}

public sealed class Match
{
    public Guid Id { get; set; }
    public Guid LobbyId { get; set; }
    public Guid HostPlayerId { get; set; }
    public Guid GuestPlayerId { get; set; }
    public long Seed { get; set; }
    /// <summary>pending, awaiting_opponent, decided, disputed.</summary>
    public string Status { get; set; } = MatchStates.Pending;
    public Guid? WinnerPlayerId { get; set; }
    public int? DurationSeconds { get; set; }
    public string? FinalStatsJson { get; set; }
    /// <summary>agreement or timeout.</summary>
    public string? DecidedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FirstReportAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? HostRatingBefore { get; set; }
    public int? HostRatingAfter { get; set; }
    public int? GuestRatingBefore { get; set; }
    public int? GuestRatingAfter { get; set; }
}

public sealed class MatchReport
{
    public long Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid WinnerPlayerId { get; set; }
    public int DurationSeconds { get; set; }
    public string StatsJson { get; set; } = "{}";
    public DateTime SubmittedAt { get; set; }
}

public static class LobbyStates
{
    public const string Waiting = "waiting";
    public const string Full = "full";
    public const string Starting = "starting";
    public const string InMatch = "in_match";
    public const string Finished = "finished";
    public const string Closed = "closed";
    public const string Expired = "expired";

    public static bool IsTerminal(string s) => s is Finished or Closed or Expired;
}

public static class SeatStates
{
    public const string Joined = "joined";
    public const string Drafting = "drafting";
    public const string Ready = "ready";
    public const string InMatch = "in_match";
    public const string Left = "left";

    public static readonly string[] All = { Joined, Drafting, Ready, InMatch, Left };
}

public static class MatchStates
{
    public const string Pending = "pending";
    public const string AwaitingOpponent = "awaiting_opponent";
    public const string Decided = "decided";
    public const string Disputed = "disputed";
}
