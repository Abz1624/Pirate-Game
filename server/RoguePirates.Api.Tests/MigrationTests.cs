#pragma warning disable EF1001 // MigrationsAssembly is internal API; used here only to add a test-only "later" migration.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>
/// V5 M9a C9a.0: EF Core migrations replace EnsureCreated. Covers the empty database, the database the old
/// EnsureCreated built (the live Railway Postgres: baselined, data kept, later migrations applied), idempotent restarts,
/// and that the baseline migrations create exactly the EnsureCreated schema for both providers.
/// </summary>
public sealed class MigrationTests : IDisposable
{
    readonly List<string> _files = new();

    string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rp-migrate-{Guid.NewGuid():N}.db");
        _files.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _files)
        {
            using (var c = new SqliteConnection(ConnString(path))) SqliteConnection.ClearPool(c);
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(f); } catch { /* best effort */ }
        }
    }

    static string ConnString(string path) => new SqliteConnectionStringBuilder { DataSource = path, Pooling = true }.ToString();

    static SqliteGameDb Sqlite(string path, bool withLaterMigration = false)
    {
        var b = new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite(ConnString(path));
        if (withLaterMigration) b.ReplaceService<IMigrationsAssembly, WithProbeMigration>();
        return new SqliteGameDb(b.Options);
    }

    static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>Builds the pre-migration database exactly as the old API's EnsureCreated did, with one player in it.</summary>
    // (EF Core stores Guids in SQLite as upper-case text, as the old API did.)
    static void CreateEnsureCreatedDatabase(string path, string token)
    {
        using var conn = new SqliteConnection(ConnString(path));
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = Fixture("ensurecreated-sqlite.sql");
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO \"Players\" (\"Id\",\"Name\",\"TokenHash\",\"CreatedAt\",\"Rating\",\"Wins\",\"Losses\") " +
                              "VALUES ('6F1C1F0E-0000-4000-8000-000000000001','OldSalt',$h,'2026-10-04 10:00:00',1032,3,1)";
            cmd.Parameters.AddWithValue("$h", Tokens.Hash(token));
            cmd.ExecuteNonQuery();
        }
    }

    static List<string> History(GameDb db) => db.Database.GetAppliedMigrations().ToList();

    static bool TableExists(string path, string table)
    {
        using var conn = new SqliteConnection(ConnString(path));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t";
        cmd.Parameters.AddWithValue("$t", table);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    [Fact]
    public void EmptyDatabase_CreatesSchemaFromMigrations()
    {
        var path = NewDbPath();
        using var db = Sqlite(path);

        var outcome = DatabaseMigrator.Migrate(db, NullLogger.Instance);

        Assert.False(outcome.Baselined);
        var baseline = Assert.Single(outcome.Applied);
        Assert.EndsWith(DatabaseMigrator.BaselineSuffix, baseline);
        Assert.Equal(new[] { baseline }, History(db));
        foreach (var table in DatabaseMigrator.BaselineSchema.Keys) Assert.True(TableExists(path, table), table);
        Assert.Empty(db.Database.GetPendingMigrations());
    }

    [Fact]
    public void EnsureCreatedDatabase_IsBaselined_KeepsData_ThenLaterMigrationsApply_AndRestartIsIdempotent()
    {
        var path = NewDbPath();
        CreateEnsureCreatedDatabase(path, "old-token");
        Assert.False(TableExists(path, "__EFMigrationsHistory"));

        using (var db = Sqlite(path, withLaterMigration: true))
        {
            var outcome = DatabaseMigrator.Migrate(db, NullLogger.Instance);

            Assert.True(outcome.Baselined);
            Assert.Equal(new[] { WithProbeMigration.Id }, outcome.Applied); // the baseline was recorded, not re-run
            var history = History(db);
            Assert.Equal(2, history.Count);
            Assert.EndsWith(DatabaseMigrator.BaselineSuffix, history[0]);
            Assert.Equal(WithProbeMigration.Id, history[1]);
            Assert.True(TableExists(path, "MigrationProbe"));
            var old = db.Players.Single();
            Assert.Equal("OldSalt", old.Name);
            Assert.Equal(1032, old.Rating);
        }

        using (var db = Sqlite(path, withLaterMigration: true))
        {
            var again = DatabaseMigrator.Migrate(db, NullLogger.Instance);
            Assert.False(again.Baselined);
            Assert.Empty(again.Applied);
            Assert.Equal(2, History(db).Count);
            Assert.Single(db.Players);
        }
    }

    [Fact]
    public void MigratedDatabase_RestartAppliesNothing()
    {
        var path = NewDbPath();
        using (var db = Sqlite(path)) DatabaseMigrator.Migrate(db, NullLogger.Instance);
        using (var db = Sqlite(path))
        {
            var again = DatabaseMigrator.Migrate(db, NullLogger.Instance);
            Assert.False(again.Baselined);
            Assert.Empty(again.Applied);
            Assert.Single(History(db));
        }
    }

    [Fact]
    public void PartialPreMigrationSchema_RefusesToMigrate()
    {
        var path = NewDbPath();
        using (var conn = new SqliteConnection(ConnString(path)))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE \"Players\" (\"Id\" TEXT NOT NULL PRIMARY KEY);";
            cmd.ExecuteNonQuery();
        }
        using var db = Sqlite(path);
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.Migrate(db, NullLogger.Instance));
        Assert.Contains("only some", ex.Message);
        Assert.False(TableExists(path, "__EFMigrationsHistory"));
    }

    [Fact]
    public void PreMigrationTablesMissingAColumn_RefusesToBaseline()
    {
        var path = NewDbPath();
        using (var conn = new SqliteConnection(ConnString(path)))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = Fixture("ensurecreated-sqlite.sql") + "ALTER TABLE \"Runs\" DROP COLUMN \"BuildHash\";";
            cmd.ExecuteNonQuery();
        }
        using var db = Sqlite(path);
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.Migrate(db, NullLogger.Instance));
        Assert.Contains("Runs.BuildHash", ex.Message);
        Assert.False(TableExists(path, "__EFMigrationsHistory"));
    }

    /// <summary>The real app start (Program.cs) on a database the old EnsureCreated built, then a restart.</summary>
    [Fact]
    public async Task AppStart_OnEnsureCreatedDatabase_BaselinesKeepsDataAndRestartsCleanly()
    {
        var path = NewDbPath();
        CreateEnsureCreatedDatabase(path, "old-token");
        var settings = new Dictionary<string, string?> { ["SQLITE_PATH"] = path };

        using (var host = new ApiHost(settings))
        {
            var health = await host.Anon().GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            // the old player's token still works and the row can be written
            var rename = await host.As("old-token").PatchAsJsonAsync("/api/profiles/me", new { name = "NewSalt" });
            Assert.True(rename.IsSuccessStatusCode, await rename.Content.ReadAsStringAsync());
            await host.NewPlayerAsync("Fresh");

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.IsType<SqliteGameDb>(db);
            Assert.Single(History(db));
            Assert.Equal(2, await db.Players.CountAsync());
        }

        using (var host = new ApiHost(settings))
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Anon().GetAsync("/health")).StatusCode);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Single(History(db));
            Assert.Equal(new[] { "Fresh", "NewSalt" }, await db.Players.Select(p => p.Name).OrderBy(n => n).ToListAsync());
        }
    }

    /// <summary>
    /// The baseline migration must create exactly what EnsureCreated built (Postgres is the live database). The
    /// fixtures were generated by the EF Core 8 build before the move; the scripts below by EF Core 10. No database
    /// connection is needed to generate a script.
    /// </summary>
    [Theory]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void BaselineMigration_CreatesExactlyTheEnsureCreatedSchema(string provider)
    {
        using GameDb db = provider == "postgres"
            ? new PostgresGameDb(new DbContextOptionsBuilder<PostgresGameDb>().UseNpgsql("Host=localhost;Database=unused").Options)
            : new SqliteGameDb(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite("Data Source=:memory:").Options);
        var baseline = db.Database.GetMigrations().First();
        var script = db.GetService<IMigrator>().GenerateScript(fromMigration: null, toMigration: baseline);

        Assert.Equal(Statements(Fixture($"ensurecreated-{provider}.sql")), Statements(script));
    }

    /// <summary>The migrations of both providers cover the current model (forgetting a migration fails here).</summary>
    [Fact]
    public void Model_HasNoChangesMissingAMigration()
    {
        using var pg = new PostgresGameDb(new DbContextOptionsBuilder<PostgresGameDb>().UseNpgsql("Host=localhost;Database=unused").Options);
        using var sq = new SqliteGameDb(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite("Data Source=:memory:").Options);
        Assert.False(pg.Database.HasPendingModelChanges(), "Postgres: run dotnet ef migrations add (see server/MIGRATIONS.md)");
        Assert.False(sq.Database.HasPendingModelChanges(), "SQLite: run dotnet ef migrations add (see server/MIGRATIONS.md)");
    }

    /// <summary>DDL statements without comments, transactions, migration-history bookkeeping or whitespace differences.</summary>
    static List<string> Statements(string sql)
    {
        sql = Regex.Replace(sql.Replace("﻿", ""), @"^--.*$", "", RegexOptions.Multiline);
        return sql.Split(';')
            .Select(s => Regex.Replace(s, @"\s+", " ").Trim())
            .Where(s => s.Length > 0
                        && !s.Contains("__EFMigrationsHistory", StringComparison.Ordinal)
                        && s is not ("BEGIN TRANSACTION" or "START TRANSACTION" or "COMMIT"))
            .ToList();
    }
}

[DbContext(typeof(SqliteGameDb))]
[Migration(WithProbeMigration.Id)]
public sealed class ProbeMigration : Migration
{
    protected override void Up(MigrationBuilder b) =>
        b.CreateTable("MigrationProbe",
            t => new { Id = t.Column<int>(type: "INTEGER", nullable: false) },
            constraints: t => t.PrimaryKey("PK_MigrationProbe", x => x.Id));
}

/// <summary>The app's migrations plus one test-only later migration, standing in for the first real post-baseline change.</summary>
public sealed class WithProbeMigration : MigrationsAssembly
{
    public const string Id = "29991231000000_TestProbe";

    public WithProbeMigration(ICurrentDbContext currentContext, IDbContextOptions options, IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger) { }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations =>
        new SortedDictionary<string, TypeInfo>(base.Migrations.ToDictionary(p => p.Key, p => p.Value)) { [Id] = typeof(ProbeMigration).GetTypeInfo() };
}
