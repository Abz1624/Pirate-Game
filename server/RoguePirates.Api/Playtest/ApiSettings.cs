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
    /// <summary>How often the 1v1 relay ticks (countdown, barrels, storm end, disconnect forfeits).</summary>
    public int ArenaTickMs { get; init; } = 100;
    /// <summary>V5 M9a (review): how long run logs are kept (days); flagged and log_rejected runs keep theirs.</summary>
    public int RunLogRetentionDays { get; init; } = 56;
    /// <summary>How often the run-log retention sweep runs (hours; the first sweep runs at startup).</summary>
    public int RunLogRetentionSweepHours { get; init; } = 24;
    /// <summary>V5 M9b: the join ticket secret shared with the Netcode match server (RP_MATCH_TICKET_SECRET, 32+ characters).
    /// Never logged or returned; without it the ticket endpoint answers 503.</summary>
    public string? MatchTicketSecret { get; init; }
    /// <summary>V5 M9b: the Netcode voyage server the ticket is for (wss://... on Railway, ws://localhost:7777 locally).
    /// Separate from <see cref="MatchServerUrl"/>, which switches the 1v1 lobbies to a dedicated host.</summary>
    public string? VoyageServerUrl { get; init; }
    /// <summary>V5 M9b: the voyage server's match id (its RP_MATCH_ID); empty: a fresh id per ticket.</summary>
    public string? VoyageMatchId { get; init; }
    /// <summary>V5 M9b: join ticket lifetime in seconds (10-120, default 60).</summary>
    public int MatchTicketLifetimeSeconds { get; init; } = 60;

    /// <summary>Hackathon: Solana JSON-RPC endpoint (devnet by default).</summary>
    public string SolanaRpcUrl { get; init; } = "https://api.devnet.solana.com";
    /// <summary>Hackathon: the treasury/escrow keypair (base58 64-byte secret, or a solana-keygen JSON array). Never logged
    /// or returned; unset: the endpoints that need it answer 503 treasury_off.</summary>
    public string? TreasurySecretKey { get; init; }
    /// <summary>Hackathon: the domain named in the sign-in message (SIWS_DOMAIN).</summary>
    public string SiwsDomain { get; init; } = "roguepirates.fun";
    /// <summary>Hackathon: the Solana cluster named in the sign-in message and the explorer links.</summary>
    public string SolanaCluster { get; init; } = "devnet";
    /// <summary>Hackathon: owner key for the admin endpoints (X-Admin-Key header). Unset: the admin endpoints answer 503.</summary>
    public string? AdminKey { get; init; }

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
        ArenaTickMs = int.TryParse(c["ARENA_TICK_MS"], out var t) && t > 0 ? t : 100,
        RunLogRetentionDays = int.TryParse(c["RUNLOG_RETENTION_DAYS"], out var rd) && rd > 0 ? rd : 56,
        RunLogRetentionSweepHours = int.TryParse(c["RUNLOG_RETENTION_SWEEP_HOURS"], out var rh) && rh > 0 ? rh : 24,
        MatchTicketSecret = NullIfEmpty(c["RP_MATCH_TICKET_SECRET"]),
        VoyageServerUrl = NullIfEmpty(c["VOYAGE_SERVER_URL"]),
        VoyageMatchId = NullIfEmpty(c["VOYAGE_MATCH_ID"]),
        SolanaRpcUrl = NullIfEmpty(c["SOLANA_RPC_URL"]) ?? "https://api.devnet.solana.com",
        TreasurySecretKey = NullIfEmpty(c["TREASURY_SECRET_KEY"]),
        SiwsDomain = NullIfEmpty(c["SIWS_DOMAIN"]) ?? "roguepirates.fun",
        SolanaCluster = NullIfEmpty(c["SOLANA_CLUSTER"]) ?? "devnet",
        AdminKey = NullIfEmpty(c["ADMIN_KEY"]),
        MatchTicketLifetimeSeconds = int.TryParse(c["MATCH_TICKET_LIFETIME_SECONDS"], out var lt) ? Math.Clamp(lt, 10, MatchTickets.MaxLifetimeSeconds) : 60,
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
