using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddServerResultChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogReason",
                table: "Raids",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogVerdict",
                table: "Raids",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaceCheck",
                table: "DailyRaceEntries",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ServerSeconds",
                table: "DailyRaceEntries",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyRaceStarts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyRaceId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Starts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRaceStarts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRaceStarts_DailyRaceId_PlayerId",
                table: "DailyRaceStarts",
                columns: new[] { "DailyRaceId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRaceStarts");

            migrationBuilder.DropColumn(
                name: "LogReason",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "LogVerdict",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "RaceCheck",
                table: "DailyRaceEntries");

            migrationBuilder.DropColumn(
                name: "ServerSeconds",
                table: "DailyRaceEntries");
        }
    }
}
