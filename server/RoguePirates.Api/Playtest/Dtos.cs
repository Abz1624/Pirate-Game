namespace RoguePirates.Api.Playtest;

public sealed record CreateProfileRequest(string? Name);
public sealed record RenameProfileRequest(string? Name);

/// <summary>A finished voyage. <paramref name="Log"/> (V5 M9a, I9a.1) is the run log, optional so older clients are still
/// accepted; kept as raw JSON so its size can be checked and it is stored as sent (see <see cref="RunLogRules"/>).</summary>
public sealed record SubmitRunRequest(
    int Kills, int RunSeconds, int Level, string? Captain, string? MapId, string? BuildHash, string? ClientVersion,
    System.Text.Json.JsonElement? Log = null);

/// <param name="StakeLamports">Hackathon: an optional SOL stake for a 1v1 wager (see WagerService).</param>
public sealed record CreateLobbyRequest(bool Public, long? StakeLamports = null);
public sealed record JoinLobbyRequest(string? Code);
public sealed record DraftDto(List<string>? Weapons, List<string>? Logbooks);
public sealed record LobbyStatusRequest(string? Status, DraftDto? Draft);

public sealed record PlayerMatchStats(
    long DamageDealt, long DamageTaken, int? Kills, int Level, List<string>? Weapons, List<string>? Logbooks,
    int? BarrelsTaken = null, double? FinalHp = null);

public sealed record MatchResultRequest(Guid WinnerPlayerId, int DurationSeconds, Dictionary<string, PlayerMatchStats>? Stats);
