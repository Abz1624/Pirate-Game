using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;

namespace RoguePirates.Api.Playtest;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Thrown by services; turned into a JSON error {error, message} with the given status.</summary>
public sealed class ApiException : Exception
{
    public int Status { get; }
    public string Code { get; }

    public ApiException(int status, string code, string message) : base(message)
    {
        Status = status;
        Code = code;
    }
}

/// <summary>One process-wide write lock for lobby and match state (the API runs as a single instance).</summary>
public static class GameLocks
{
    public static readonly SemaphoreSlim Lobby = new(1, 1);
    public static readonly SemaphoreSlim Runs = new(1, 1);
}

public static class Tokens
{
    public static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public static class PlayerTags
{
    /// <summary>A short display tag derived from the id, e.g. "#4821". Not unique; just tells same-named players apart.</summary>
    public static string For(Guid id)
    {
        var bytes = id.ToByteArray();
        var n = BitConverter.ToUInt32(bytes, 0) % 10000;
        return "#" + n.ToString("D4");
    }
}

public static partial class NameRules
{
    [GeneratedRegex("^[A-Za-z0-9 _-]{3,16}$")]
    private static partial Regex Allowed();

    // Substring matches on the squashed name, so words with common innocent overlaps are left out
    // ("rape" would block "Grapeshot", "cock" "Peacock", "spic" "Spice").
    static readonly string[] Blocked =
    {
        "fuck", "shit", "cunt", "nigger", "nigga", "faggot", "bitch", "whore", "slut", "rapist", "nazi",
        "hitler", "cocksuck", "pussy", "asshole", "retard", "kike", "chink", "penis", "vagina", "porn", "dickhead",
    };

    /// <summary>Returns the trimmed name, or throws 400.</summary>
    public static string Validate(string? raw)
    {
        var name = (raw ?? "").Trim();
        if (!Allowed().IsMatch(name))
            throw new ApiException(400, "invalid_name", "Names are 3-16 characters: letters, digits, space, _ or -.");
        if (IsProfane(name))
            throw new ApiException(400, "name_not_allowed", "Please pick a different name.");
        return name;
    }

    public static bool IsProfane(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.ToLowerInvariant())
        {
            var c = ch switch { '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '8' => 'b', _ => ch };
            if (char.IsLetter(c)) sb.Append(c);
        }
        var squashed = sb.ToString();
        return Blocked.Any(w => squashed.Contains(w, StringComparison.Ordinal));
    }
}

public static class WeekMath
{
    /// <summary>Monday 00:00 UTC of the ISO week containing the instant.</summary>
    public static DateTime WeekStart(DateTime utc)
    {
        var d = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var daysSinceMonday = ((int)d.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(d.Date.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }
}

public static class Elo
{
    public const int K = 32;
    public const int Start = 1000;

    public static (int winner, int loser) Update(int winnerRating, int loserRating)
    {
        var expectedWinner = 1.0 / (1.0 + Math.Pow(10, (loserRating - winnerRating) / 400.0));
        var delta = (int)Math.Round(K * (1 - expectedWinner), MidpointRounding.AwayFromZero);
        return (winnerRating + delta, loserRating - delta);
    }

    /// <summary>A draw: each side scores 0.5 (equal ratings do not move).</summary>
    public static (int a, int b) Draw(int a, int b)
    {
        var expectedA = 1.0 / (1.0 + Math.Pow(10, (b - a) / 400.0));
        var delta = (int)Math.Round(K * (0.5 - expectedA), MidpointRounding.AwayFromZero);
        return (a + delta, b - delta);
    }
}

/// <summary>Resolves the player from "Authorization: Bearer &lt;token&gt;".</summary>
public sealed class PlayerAuth
{
    /// <summary>A session's expiry is moved forward at most this often (one write an hour, not one per request).</summary>
    public static readonly TimeSpan SlideEvery = TimeSpan.FromHours(1);

    readonly GameDb _db;
    readonly IClock _clock;
    readonly ApiSettings _settings;

    public PlayerAuth(GameDb db, IClock clock, ApiSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public TimeSpan SessionLifetime => TimeSpan.FromDays(_settings.SessionLifetimeDays);

    /// <summary>When a session stops working (sessions from before M9h have no ExpiresAt: CreatedAt + the lifetime).</summary>
    public static DateTime ExpiryOf(PlayerSession s, TimeSpan lifetime) => s.ExpiresAt ?? s.CreatedAt + lifetime;

    public async Task<Player?> FromTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return null;
        var hash = Tokens.Hash(token.Trim());
        // a guest profile's own token (guests have no other way back in, so it does not expire; it rotates on revoke-all
        // and stops working when a wallet is linked to the profile)
        var player = await _db.Players.FirstOrDefaultAsync(p => p.TokenHash == hash);
        if (player != null) return player;
        // Hackathon: wallet sign-ins issue one session per sign-in (several devices stay signed in)
        var session = await _db.PlayerSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash);
        if (session == null) return null;
        // V5 M9h: sessions expire SESSION_LIFETIME_DAYS (30) after their last use
        var now = _clock.UtcNow;
        var lifetime = SessionLifetime;
        var expires = ExpiryOf(session, lifetime);
        if (now >= expires) return null;
        if (now + lifetime - expires >= SlideEvery)
            await _db.PlayerSessions.Where(s => s.Id == session.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ExpiresAt, now + lifetime).SetProperty(s => s.LastUsedAt, now));
        return await _db.Players.FirstOrDefaultAsync(p => p.Id == session.PlayerId);
    }

    public Task<Player?> TryGetAsync(HttpContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Task.FromResult<Player?>(null);
        return FromTokenAsync(header[prefix.Length..]);
    }

    public async Task<Player> RequireAsync(HttpContext ctx) =>
        await TryGetAsync(ctx) ?? throw new ApiException(401, "unauthorized", "Missing or unknown bearer token.");
}
