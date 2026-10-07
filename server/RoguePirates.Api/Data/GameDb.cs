using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RoguePirates.Api.Data;

/// <summary>
/// The playtest database. Abstract: the app runs one of the provider subclasses below (Postgres on Railway, SQLite
/// locally and in tests), each with its own migrations folder, since migration SQL is provider specific. Services keep
/// depending on <see cref="GameDb"/>; Program.cs resolves it to the active subclass. See server/MIGRATIONS.md.
/// </summary>
public abstract class GameDb : DbContext
{
    protected GameDb(DbContextOptions options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<RunRecord> Runs => Set<RunRecord>();
    public DbSet<WeeklyBest> WeeklyBests => Set<WeeklyBest>();
    public DbSet<Lobby> Lobbies => Set<Lobby>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchReport> MatchReports => Set<MatchReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(16);
            e.Property(x => x.TokenHash).HasMaxLength(64);
        });
        b.Entity<RunRecord>(e =>
        {
            e.HasIndex(x => x.PlayerId);
            e.Property(x => x.MapId).HasMaxLength(32);
            e.Property(x => x.Captain).HasMaxLength(32);
            e.Property(x => x.BuildHash).HasMaxLength(128);
            e.Property(x => x.ClientVersion).HasMaxLength(32);
        });
        b.Entity<WeeklyBest>(e =>
        {
            e.HasIndex(x => new { x.WeekStart, x.MapId, x.PlayerId }).IsUnique();
            e.HasIndex(x => new { x.WeekStart, x.MapId, x.Kills });
            e.Property(x => x.MapId).HasMaxLength(32);
            e.Property(x => x.Captain).HasMaxLength(32);
        });
        b.Entity<Lobby>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Status, x.IsPublic });
            e.Property(x => x.Code).HasMaxLength(6);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.HostStatus).HasMaxLength(16);
            e.Property(x => x.GuestStatus).HasMaxLength(16);
        });
        b.Entity<Match>(e =>
        {
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasMaxLength(24);
        });
        b.Entity<MatchReport>(e => e.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique());

        // Every timestamp is UTC. SQLite hands DateTimes back with Kind=Unspecified and Npgsql refuses
        // non-UTC values for timestamptz, so pin the kind on the way in and out.
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v == null ? v : (v.Value.Kind == DateTimeKind.Utc ? v : (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))),
            v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
        foreach (var entity in b.Model.GetEntityTypes())
        foreach (var prop in entity.GetProperties())
        {
            if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
            else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
        }
    }
}

/// <summary>Postgres (Railway). Migrations: Data/Migrations/Postgres.</summary>
public sealed class PostgresGameDb : GameDb
{
    public PostgresGameDb(DbContextOptions<PostgresGameDb> options) : base(options) { }
}

/// <summary>SQLite (local runs and tests). Migrations: Data/Migrations/Sqlite.</summary>
public sealed class SqliteGameDb : GameDb
{
    public SqliteGameDb(DbContextOptions<SqliteGameDb> options) : base(options) { }
}
