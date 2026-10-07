using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RoguePirates.Api.Data;

/// <summary>What <see cref="DatabaseMigrator.Migrate"/> did.</summary>
public sealed record MigrationOutcome(bool Baselined, IReadOnlyList<string> Applied);

/// <summary>
/// Applies EF Core migrations on startup, safely for the three cases the API meets:
/// <list type="bullet">
/// <item>an empty database: every migration runs, the baseline creates all tables;</item>
/// <item>a database built by the old <c>EnsureCreated</c> (the live Railway Postgres until C9a.0): the baseline tables
/// exist but there is no migration history, so the baseline is recorded in <c>__EFMigrationsHistory</c> without running
/// it (tables and rows kept), then later migrations run;</item>
/// <item>a migrated database: only pending migrations run (none on a plain restart).</item>
/// </list>
/// Anything else (some baseline tables missing, or a baseline table missing a column) stops startup with an error
/// rather than guessing. See server/MIGRATIONS.md.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>Suffix of the first migration in each provider's folder; it creates the schema EnsureCreated built.</summary>
    public const string BaselineSuffix = "_Baseline";

    /// <summary>
    /// The schema EnsureCreated built (EF Core 8 model of 3 Oct 2026, unchanged until C9a.0): tables and their columns.
    /// Frozen on purpose: it describes the pre-migration databases, not the current model.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> BaselineSchema = new Dictionary<string, string[]>
    {
        ["Players"] = new[] { "Id", "Name", "TokenHash", "CreatedAt", "Rating", "Wins", "Losses" },
        ["Runs"] = new[] { "Id", "PlayerId", "Kills", "RunSeconds", "Level", "Captain", "MapId", "BuildHash", "ClientVersion", "WeekStart", "SubmittedAt" },
        ["WeeklyBests"] = new[] { "Id", "PlayerId", "WeekStart", "MapId", "Kills", "RunSeconds", "Level", "Captain", "RunId", "AchievedAt" },
        ["Lobbies"] = new[] { "Id", "Code", "IsPublic", "Status", "HostPlayerId", "HostStatus", "HostDraftJson", "GuestPlayerId", "GuestStatus", "GuestDraftJson", "CreatedAt", "UpdatedAt", "LastActivityAt", "MatchId", "Seed", "MatchServer" },
        ["Matches"] = new[] { "Id", "LobbyId", "HostPlayerId", "GuestPlayerId", "Seed", "Status", "WinnerPlayerId", "DurationSeconds", "FinalStatsJson", "DecidedBy", "CreatedAt", "FirstReportAt", "DecidedAt", "HostRatingBefore", "HostRatingAfter", "GuestRatingBefore", "GuestRatingAfter" },
        ["MatchReports"] = new[] { "Id", "MatchId", "PlayerId", "WinnerPlayerId", "DurationSeconds", "StatsJson", "SubmittedAt" },
    };

    public static MigrationOutcome Migrate(DbContext db, ILogger log)
    {
        var database = db.Database;
        var provider = database.ProviderName ?? "unknown";
        var all = database.GetMigrations().ToList();
        if (all.Count == 0 || !all[0].EndsWith(BaselineSuffix, StringComparison.Ordinal))
            throw new InvalidOperationException($"The first migration for {db.GetType().Name} must be the *{BaselineSuffix} migration; found '{all.FirstOrDefault()}'.");
        var baselineId = all[0];

        var baselined = false;
        database.OpenConnection();
        try
        {
            var history = db.GetService<IHistoryRepository>();
            var hasHistory = history.Exists() && history.GetAppliedMigrations().Count > 0;
            if (!hasHistory)
            {
                var conn = database.GetDbConnection();
                var present = BaselineSchema.Keys.Where(t => TableExists(conn, provider, t)).ToList();
                if (present.Count == BaselineSchema.Count)
                {
                    var missing = BaselineSchema
                        .SelectMany(t => t.Value.Except(Columns(conn, provider, t.Key), StringComparer.Ordinal).Select(c => $"{t.Key}.{c}"))
                        .ToList();
                    if (missing.Count > 0)
                        throw new InvalidOperationException(
                            $"The database has the pre-migration tables but not their full schema (missing {string.Join(", ", missing)}); refusing to baseline. See server/MIGRATIONS.md.");

                    log.LogWarning("Database {Provider}: pre-migration schema found (built by EnsureCreated, no migration history); recording {Baseline} as applied without running it",
                        provider, baselineId);
                    using var tx = conn.BeginTransaction();
                    Execute(conn, tx, history.GetCreateIfNotExistsScript());
                    Execute(conn, tx, history.GetInsertScript(new HistoryRow(baselineId, ProductInfo.GetVersion())));
                    tx.Commit();
                    baselined = true;
                }
                else if (present.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"The database has only some of the pre-migration tables ({string.Join(", ", present)}) and no migration history; refusing to migrate. See server/MIGRATIONS.md.");
                }
                else
                {
                    log.LogInformation("Database {Provider}: empty, creating the schema from migrations", provider);
                }
            }
        }
        finally
        {
            database.CloseConnection();
        }

        var pending = database.GetPendingMigrations().ToList();
        if (pending.Count == 0)
            log.LogInformation("Database {Provider}: schema up to date ({Count} migrations applied, latest {Latest})", provider, all.Count, all[^1]);
        else
        {
            log.LogInformation("Database {Provider}: applying {Count} migration(s): {Pending}", provider, pending.Count, string.Join(", ", pending));
            database.Migrate();
            log.LogInformation("Database {Provider}: migrations applied, latest {Latest}", provider, database.GetAppliedMigrations().LastOrDefault());
        }
        return new MigrationOutcome(baselined, pending);
    }

    static bool TableExists(DbConnection conn, string provider, string table)
    {
        var sql = IsSqlite(provider)
            ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @t"
            : "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = @t";
        return Convert.ToInt64(Scalar(conn, sql, table)) > 0;
    }

    static HashSet<string> Columns(DbConnection conn, string provider, string table)
    {
        var sql = IsSqlite(provider)
            ? "SELECT name FROM pragma_table_info(@t)"
            : "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @t";
        using var cmd = Command(conn, sql, table);
        using var reader = cmd.ExecuteReader();
        var result = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    static bool IsSqlite(string provider) => provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

    static object? Scalar(DbConnection conn, string sql, string table)
    {
        using var cmd = Command(conn, sql, table);
        return cmd.ExecuteScalar();
    }

    static DbCommand Command(DbConnection conn, string sql, string table)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter();
        p.ParameterName = "t";
        p.DbType = DbType.String;
        p.Value = table;
        cmd.Parameters.Add(p);
        return cmd;
    }

    static void Execute(DbConnection conn, DbTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
