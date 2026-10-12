using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddHackathon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Players",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Rum",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "WalletAddress",
                table: "Players",
                type: "character varying(44)",
                maxLength: 44,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyRaceEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyRaceId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeMs = table.Column<int>(type: "integer", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: true),
                    Verdict = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRaceEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyRaces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    MapId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IslandBuildings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IslandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IslandBuildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Islands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NftMint = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    MintSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    RumStored = table.Column<long>(type: "bigint", nullable: false),
                    RumVault = table.Column<long>(type: "bigint", nullable: false),
                    LastAccrualAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Islands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IslandId = table.Column<Guid>(type: "uuid", nullable: true),
                    NftMint = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    SellerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLamports = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BuyerPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EscrowSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    PaymentSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    TransferSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    PayoutSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketListings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Wallet = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Lamports = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Signature = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Raids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IslandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RumLooted = table.Column<long>(type: "bigint", nullable: false),
                    WagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raids", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolanaDeposits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Signature = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Wallet = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Lamports = table.Column<long>(type: "bigint", nullable: false),
                    Memo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Slot = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolanaDeposits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wagers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LobbyId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    StakeLamports = table.Column<long>(type: "bigint", nullable: false),
                    HostPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    VsHouse = table.Column<bool>(type: "boolean", nullable: false),
                    HostDepositSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    GuestDepositSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    WinnerPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayoutId = table.Column<long>(type: "bigint", nullable: true),
                    PayoutSig = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wagers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletNonces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nonce = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Wallet = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletNonces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyRewards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WeekStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MapId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lamports = table.Column<long>(type: "bigint", nullable: false),
                    PayoutId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyRewards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_WalletAddress",
                table: "Players",
                column: "WalletAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyRaceEntries_DailyRaceId_PlayerId",
                table: "DailyRaceEntries",
                columns: new[] { "DailyRaceId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyRaceEntries_DailyRaceId_TimeMs",
                table: "DailyRaceEntries",
                columns: new[] { "DailyRaceId", "TimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRaces_Date",
                table: "DailyRaces",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IslandBuildings_IslandId_Kind",
                table: "IslandBuildings",
                columns: new[] { "IslandId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Islands_NftMint",
                table: "Islands",
                column: "NftMint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Islands_OwnerPlayerId",
                table: "Islands",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_NftMint",
                table: "MarketListings",
                column: "NftMint");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_SellerPlayerId",
                table: "MarketListings",
                column: "SellerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_Status",
                table: "MarketListings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_PlayerId",
                table: "Payouts",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Reason_RefId",
                table: "Payouts",
                columns: new[] { "Reason", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_PlayerId",
                table: "PlayerSessions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_TokenHash",
                table: "PlayerSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Raids_AttackerPlayerId",
                table: "Raids",
                column: "AttackerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Raids_IslandId",
                table: "Raids",
                column: "IslandId");

            migrationBuilder.CreateIndex(
                name: "IX_SolanaDeposits_PlayerId",
                table: "SolanaDeposits",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SolanaDeposits_Purpose_RefId",
                table: "SolanaDeposits",
                columns: new[] { "Purpose", "RefId" });

            migrationBuilder.CreateIndex(
                name: "IX_SolanaDeposits_Signature",
                table: "SolanaDeposits",
                column: "Signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wagers_LobbyId",
                table: "Wagers",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_Wagers_MatchId",
                table: "Wagers",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Wagers_Status",
                table: "Wagers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletNonces_Nonce",
                table: "WalletNonces",
                column: "Nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletNonces_Wallet",
                table: "WalletNonces",
                column: "Wallet");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRewards_PlayerId",
                table: "WeeklyRewards",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRewards_WeekStart_MapId_Rank",
                table: "WeeklyRewards",
                columns: new[] { "WeekStart", "MapId", "Rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRaceEntries");

            migrationBuilder.DropTable(
                name: "DailyRaces");

            migrationBuilder.DropTable(
                name: "IslandBuildings");

            migrationBuilder.DropTable(
                name: "Islands");

            migrationBuilder.DropTable(
                name: "MarketListings");

            migrationBuilder.DropTable(
                name: "Payouts");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "Raids");

            migrationBuilder.DropTable(
                name: "SolanaDeposits");

            migrationBuilder.DropTable(
                name: "Wagers");

            migrationBuilder.DropTable(
                name: "WalletNonces");

            migrationBuilder.DropTable(
                name: "WeeklyRewards");

            migrationBuilder.DropIndex(
                name: "IX_Players_WalletAddress",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Rum",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WalletAddress",
                table: "Players");
        }
    }
}
