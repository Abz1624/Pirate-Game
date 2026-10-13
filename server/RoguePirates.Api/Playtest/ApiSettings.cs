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

    /// <summary>13 Oct hotfix: a mainnet SOLANA_RPC_URL or SOLANA_CLUSTER keeps the treasury off unless ALLOW_MAINNET=true.</summary>
    public bool AllowMainnet { get; init; }
    public bool IsMainnet =>
        SolanaCluster.Contains("mainnet", StringComparison.OrdinalIgnoreCase)
        || SolanaRpcUrl.Contains("mainnet", StringComparison.OrdinalIgnoreCase);
    /// <summary>13 Oct hotfix: the largest owner payout (POST /api/admin/payouts), ADMIN_PAYOUT_MAX_LAMPORTS, default 1 SOL.</summary>
    public long AdminPayoutMaxLamports { get; init; } = 1_000_000_000;

    /// <summary>
    /// 13 Oct hotfix: RUM earned from voyages (POST /api/runs, client-reported kills / 10) goes through the design's soft
    /// cap curve <c>cap * (1 - 0.5^(amount / half))</c> twice: per run (RUM_RUN_CAP 40, RUM_RUN_HALF 20: a 300-kill run's
    /// 30 raw RUM gives 25) and per UTC day over the sum of the capped runs (RUM_DAY_CAP 150, RUM_DAY_HALF 75). So one run
    /// never earns more than 40 RUM and a day never more than 150, whatever the client reports (an island store makes
    /// 120+ RUM an hour).
    /// </summary>
    public int RumRunCap { get; init; } = 40;
    public int RumRunHalf { get; init; } = 20;
    public int RumDayCap { get; init; } = 150;
    public int RumDayHalf { get; init; } = 75;

    // ---- V5 M9h (13 Oct): live-API switches. The owner kept every one on (H2, 13 Oct: "don't switch off anything for
    // now"); each is a variable so it can be flipped on Railway without a code change. ----
    /// <summary>WAGERS_HUMAN_ENABLED (default true): SOL stakes between human players in 1v1 lobbies. false: new stakes and
    /// new deposits are refused (409 wagers_off; a transfer already sent is recorded and refunded); open wagers still settle.</summary>
    public bool WagersHumanEnabled { get; init; } = true;
    /// <summary>RAID_LOOT_ENABLED (default true): a won raid takes RUM from the island. false: raids still win, shield and
    /// count in the defence record, but loot nothing.</summary>
    public bool RaidLootEnabled { get; init; } = true;
    /// <summary>ISLAND_SALE_CARRIES_RUM (default true): a market sale hands the buyer the island as it is (RUM-bought
    /// building levels, store and vault RUM). false (I26.2): the store and vault RUM go to the seller's balance and the
    /// buildings go back to their start levels when the island changes hands.</summary>
    public bool IslandSaleCarriesRum { get; init; } = true;
    /// <summary>DAILY_RACE_FIXED_SALT (default true): the daily race seed uses DAILY_RACE_SALT or, unset, the public default
    /// salt in the code (anyone can compute future seeds). false: DAILY_RACE_SALT or, unset, a secret salt derived from
    /// the treasury key (random per process without one), so seeds cannot be computed ahead.</summary>
    public bool DailyRaceFixedSalt { get; init; } = true;

    // ---- V5 M9h: sessions and treasury bounds ----
    /// <summary>SESSION_LIFETIME_DAYS (default 30): wallet sessions expire this long after their last use (sliding).</summary>
    public int SessionLifetimeDays { get; init; } = 30;
    /// <summary>MINT_DAILY_BUDGET (default 20): Island NFT mints the treasury pays per UTC day (each about 0.018 SOL).</summary>
    public int MintDailyBudget { get; init; } = 20;
    /// <summary>TREASURY_MIN_LAMPORTS (default 2 SOL): below this float the treasury reports "low" and logs an alarm.</summary>
    public long TreasuryMinLamports { get; init; } = 2_000_000_000;
    /// <summary>RPC_READ_RATE_LIMIT_PER_MINUTE (default 30): GET /api/wallet/balance calls per client IP per minute (each
    /// one is a Solana RPC call).</summary>
    public int RpcReadRateLimitPerMinute { get; init; } = 30;

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
        AllowMainnet = bool.TryParse(c["ALLOW_MAINNET"], out var am) && am,
        AdminPayoutMaxLamports = long.TryParse(c["ADMIN_PAYOUT_MAX_LAMPORTS"], out var apm) && apm > 0 ? apm : 1_000_000_000,
        RumRunCap = Positive(c["RUM_RUN_CAP"], 40),
        RumRunHalf = Positive(c["RUM_RUN_HALF"], 20),
        RumDayCap = Positive(c["RUM_DAY_CAP"], 150),
        RumDayHalf = Positive(c["RUM_DAY_HALF"], 75),
        WagersHumanEnabled = Flag(c["WAGERS_HUMAN_ENABLED"], true),
        RaidLootEnabled = Flag(c["RAID_LOOT_ENABLED"], true),
        IslandSaleCarriesRum = Flag(c["ISLAND_SALE_CARRIES_RUM"], true),
        DailyRaceFixedSalt = Flag(c["DAILY_RACE_FIXED_SALT"], true),
        SessionLifetimeDays = Positive(c["SESSION_LIFETIME_DAYS"], 30),
        MintDailyBudget = int.TryParse(c["MINT_DAILY_BUDGET"], out var mb) && mb >= 0 ? mb : 20,
        TreasuryMinLamports = long.TryParse(c["TREASURY_MIN_LAMPORTS"], out var tm) && tm >= 0 ? tm : 2_000_000_000,
        RpcReadRateLimitPerMinute = Positive(c["RPC_READ_RATE_LIMIT_PER_MINUTE"], 30),
    };

    /// <summary>true/false (any case), 1/0, on/off, yes/no; anything else (unset, empty, a typo) keeps the default
    /// (<see cref="IsUnrecognisedFlag"/> lets startup log it).</summary>
    public static bool Flag(string? v, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(v)) return fallback;
        switch (v.Trim().ToLowerInvariant())
        {
            case "true": case "1": case "on": case "yes": return true;
            case "false": case "0": case "off": case "no": return false;
            default: return fallback;
        }
    }

    public static bool IsUnrecognisedFlag(string? v) =>
        !string.IsNullOrWhiteSpace(v) && Flag(v, true) && !Flag(v, false);

    /// <summary>The switch variables (V5 M9h), for the startup check of their values.</summary>
    public static readonly string[] SwitchVariables =
        { "WAGERS_HUMAN_ENABLED", "RAID_LOOT_ENABLED", "ISLAND_SALE_CARRIES_RUM", "DAILY_RACE_FIXED_SALT", "WEEKLY_REWARD_CHECKED_ONLY", "WEEKLY_REWARDS_AUTO" };

    static int Positive(string? v, int fallback) => int.TryParse(v, out var n) && n > 0 ? n : fallback;

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
