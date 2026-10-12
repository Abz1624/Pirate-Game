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
    public DbSet<RunLog> RunLogs => Set<RunLog>();
    public DbSet<RunVerdict> RunVerdicts => Set<RunVerdict>();
    public DbSet<WeeklyBest> WeeklyBests => Set<WeeklyBest>();
    public DbSet<Lobby> Lobbies => Set<Lobby>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchReport> MatchReports => Set<MatchReport>();
    // Hackathon (AddHackathon)
    public DbSet<WalletNonce> WalletNonces => Set<WalletNonce>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<SolanaDeposit> SolanaDeposits => Set<SolanaDeposit>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<Wager> Wagers => Set<Wager>();
    public DbSet<DailyRace> DailyRaces => Set<DailyRace>();
    public DbSet<DailyRaceEntry> DailyRaceEntries => Set<DailyRaceEntry>();
    public DbSet<WeeklyReward> WeeklyRewards => Set<WeeklyReward>();
    public DbSet<Island> Islands => Set<Island>();
    public DbSet<IslandBuilding> IslandBuildings => Set<IslandBuilding>();
    public DbSet<Raid> Raids => Set<Raid>();
    public DbSet<MarketListing> MarketListings => Set<MarketListing>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(16);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.WalletAddress).IsUnique();
            e.Property(x => x.WalletAddress).HasMaxLength(44);
            e.Property(x => x.Email).HasMaxLength(254);
        });
        b.Entity<RunRecord>(e =>
        {
            e.HasIndex(x => x.PlayerId);
            e.Property(x => x.MapId).HasMaxLength(32);
            e.Property(x => x.Captain).HasMaxLength(32);
            e.Property(x => x.BuildHash).HasMaxLength(128);
            e.Property(x => x.ClientVersion).HasMaxLength(32);
        });
        b.Entity<RunLog>(e =>
        {
            e.HasKey(x => x.RunId);
            e.Property(x => x.RunId).ValueGeneratedNever();
            e.HasOne<RunRecord>().WithOne().HasForeignKey<RunLog>(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.SeedSource).HasMaxLength(16);
            e.Property(x => x.ConfigHash).HasMaxLength(128);
            e.Property(x => x.BalanceVersion).HasMaxLength(64);
            e.Property(x => x.BuildId).HasMaxLength(64);
            e.Property(x => x.ClientRunId).HasMaxLength(64);
            e.Property(x => x.RejectCode).HasMaxLength(32);
            e.Property(x => x.RejectReason).HasMaxLength(256);
        });
        b.Entity<RunVerdict>(e =>
        {
            e.HasKey(x => x.RunId);
            e.Property(x => x.RunId).ValueGeneratedNever();
            e.HasOne<RunRecord>().WithOne().HasForeignKey<RunVerdict>(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RulesVersion);
            e.Property(x => x.Status).HasMaxLength(24);
            e.Property(x => x.ReasonCodes).HasMaxLength(512);
            e.Property(x => x.RuleIds).HasMaxLength(512);
            e.Property(x => x.ClientBuild).HasMaxLength(64);
            e.Property(x => x.ConfigHash).HasMaxLength(128);
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

        // ---- Hackathon (AddHackathon) ----
        const int Sig = 88, Key = 44;
        b.Entity<WalletNonce>(e =>
        {
            e.HasIndex(x => x.Nonce).IsUnique();
            e.HasIndex(x => x.Wallet);
            e.Property(x => x.Nonce).HasMaxLength(32);
            e.Property(x => x.Wallet).HasMaxLength(Key);
            e.Property(x => x.Message).HasMaxLength(1024);
        });
        b.Entity<PlayerSession>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.PlayerId);
            e.Property(x => x.TokenHash).HasMaxLength(64);
        });
        b.Entity<SolanaDeposit>(e =>
        {
            e.HasIndex(x => x.Signature).IsUnique();
            e.HasIndex(x => x.PlayerId);
            e.HasIndex(x => new { x.Purpose, x.RefId });
            e.Property(x => x.Signature).HasMaxLength(Sig);
            e.Property(x => x.Wallet).HasMaxLength(Key);
            e.Property(x => x.Purpose).HasMaxLength(32);
            e.Property(x => x.RefId).HasMaxLength(64);
            e.Property(x => x.Memo).HasMaxLength(256);
            e.Property(x => x.Status).HasMaxLength(16);
        });
        b.Entity<Payout>(e =>
        {
            e.HasIndex(x => x.PlayerId);
            e.HasIndex(x => new { x.Reason, x.RefId });
            e.Property(x => x.Wallet).HasMaxLength(Key);
            e.Property(x => x.Reason).HasMaxLength(32);
            e.Property(x => x.RefId).HasMaxLength(64);
            e.Property(x => x.Signature).HasMaxLength(Sig);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Error).HasMaxLength(512);
        });
        b.Entity<Wager>(e =>
        {
            e.HasIndex(x => x.LobbyId);
            e.HasIndex(x => x.MatchId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.HostDepositSig).HasMaxLength(Sig);
            e.Property(x => x.GuestDepositSig).HasMaxLength(Sig);
            e.Property(x => x.PayoutSig).HasMaxLength(Sig);
            e.Property(x => x.Status).HasMaxLength(24);
        });
        b.Entity<DailyRace>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
            e.Property(x => x.MapId).HasMaxLength(32);
        });
        b.Entity<DailyRaceEntry>(e =>
        {
            e.HasIndex(x => new { x.DailyRaceId, x.PlayerId }).IsUnique();
            e.HasIndex(x => new { x.DailyRaceId, x.TimeMs });
            e.Property(x => x.Verdict).HasMaxLength(24);
        });
        b.Entity<WeeklyReward>(e =>
        {
            e.HasIndex(x => new { x.WeekStart, x.MapId, x.Rank }).IsUnique();
            e.HasIndex(x => x.PlayerId);
            e.Property(x => x.MapId).HasMaxLength(32);
        });
        b.Entity<Island>(e =>
        {
            e.HasIndex(x => x.OwnerPlayerId);
            e.HasIndex(x => x.NftMint).IsUnique();
            e.Property(x => x.Name).HasMaxLength(32);
            e.Property(x => x.NftMint).HasMaxLength(Key);
            e.Property(x => x.MintSig).HasMaxLength(Sig);
        });
        b.Entity<IslandBuilding>(e =>
        {
            e.HasIndex(x => new { x.IslandId, x.Kind }).IsUnique();
            e.Property(x => x.Kind).HasMaxLength(24);
        });
        b.Entity<Raid>(e =>
        {
            e.HasIndex(x => x.AttackerPlayerId);
            e.HasIndex(x => x.IslandId);
            e.Property(x => x.Result).HasMaxLength(16);
        });
        b.Entity<MarketListing>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.NftMint);
            e.HasIndex(x => x.SellerPlayerId);
            e.Property(x => x.NftMint).HasMaxLength(Key);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.EscrowSig).HasMaxLength(Sig);
            e.Property(x => x.PaymentSig).HasMaxLength(Sig);
            e.Property(x => x.TransferSig).HasMaxLength(Sig);
            e.Property(x => x.PayoutSig).HasMaxLength(Sig);
        });
        // ---- Hackathon islands, raids, market (AddHackathonIslands) ----
        b.Entity<Island>(e =>
        {
            e.HasIndex(x => x.Slot).IsUnique();
            e.Property(x => x.NftStatus).HasMaxLength(16);
            e.Property(x => x.MintError).HasMaxLength(256);
        });
        b.Entity<Raid>(e => e.HasIndex(x => x.DefenderPlayerId));
        b.Entity<MarketListing>(e => e.Property(x => x.Error).HasMaxLength(256));

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
