using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Sqlite
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
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogVerdict",
                table: "Raids",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaceCheck",
                table: "DailyRaceEntries",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ServerSeconds",
                table: "DailyRaceEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyRaceStarts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyRaceId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Starts = table.Column<int>(type: "INTEGER", nullable: false)
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
