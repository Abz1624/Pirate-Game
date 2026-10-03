using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace RoguePirates.Api.Playtest;

/// <summary>Runtime settings, read from configuration (environment variables on Railway).</summary>
public sealed class ApiSettings
{
    public const string DefaultOrigins = "https://www.roguepirates.fun,https://roguepirates.fun,http://localhost:*,http://127.0.0.1:*";

    public string? DatabaseUrl { get; init; }
    public string SqlitePath { get; init; } = "roguepirates.db";
    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();
    public string? MatchServerUrl { get; init; }
    public string PlayUrl { get; init; } = "https://www.roguepirates.fun/play/";
    public int RateLimitPerMinute { get; init; } = 60;
    public int SweepSeconds { get; init; } = 30;

    public bool UsePostgres => !string.IsNullOrWhiteSpace(DatabaseUrl);
    public string DbKind => UsePostgres ? "postgres" : "sqlite";

    public static ApiSettings From(IConfiguration c) => new()
    {
        DatabaseUrl = NullIfEmpty(c["DATABASE_URL"]),
        SqlitePath = NullIfEmpty(c["SQLITE_PATH"]) ?? "roguepirates.db",
        AllowedOrigins = (NullIfEmpty(c["ALLOWED_ORIGINS"]) ?? DefaultOrigins)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .ToArray(),
        MatchServerUrl = NullIfEmpty(c["MATCH_SERVER_URL"]),
        PlayUrl = NullIfEmpty(c["PLAY_URL"]) ?? "https://www.roguepirates.fun/play/",
        RateLimitPerMinute = int.TryParse(c["RATE_LIMIT_PER_MINUTE"], out var r) && r > 0 ? r : 60,
        SweepSeconds = int.TryParse(c["SWEEP_SECONDS"], out var s) && s > 0 ? s : 30,
    };

    static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Exact origins, or "scheme://host:*" for any port on that host.</summary>
    public bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrEmpty(origin)) return false;
        origin = origin.TrimEnd('/');
        foreach (var allowed in AllowedOrigins)
        {
            if (allowed == "*") return true;
            if (allowed.EndsWith(":*", StringComparison.Ordinal))
            {
                var prefix = allowed[..^1]; // keep the colon
                if (string.Equals(origin, allowed[..^2], StringComparison.OrdinalIgnoreCase)) return true;
                if (origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && origin.Length > prefix.Length
                    && origin[prefix.Length..].All(char.IsDigit)) return true;
            }
            else if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Turns Railway's DATABASE_URL (postgresql://user:pass@host:port/db?sslmode=...) into an Npgsql connection string.
    /// A value that is already a key=value connection string is passed through.</summary>
    public static string PostgresConnectionString(string databaseUrl)
    {
        if (!databaseUrl.Contains("://", StringComparison.Ordinal)) return databaseUrl;
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            SslMode = SslMode.Prefer,
        };
        if (userInfo.Length > 1) csb.Password = Uri.UnescapeDataString(userInfo[1]);
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (query.TryGetValue("sslmode", out var mode)
            && Enum.TryParse<SslMode>(mode.ToString().Replace("-", ""), ignoreCase: true, out var parsed))
            csb.SslMode = parsed;
        return csb.ConnectionString;
    }
}
