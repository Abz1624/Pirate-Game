using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddHackathonIslands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefenderPlayerId",
                table: "Raids",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Seconds",
                table: "Raids",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "MarketListings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MintError",
                table: "Islands",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NftStatus",
                table: "Islands",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ShieldUntil",
                table: "Islands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "Islands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Raids_DefenderPlayerId",
                table: "Raids",
                column: "DefenderPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Islands_Slot",
                table: "Islands",
                column: "Slot",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Raids_DefenderPlayerId",
                table: "Raids");

            migrationBuilder.DropIndex(
                name: "IX_Islands_Slot",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "DefenderPlayerId",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "Seconds",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "MintError",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "NftStatus",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "ShieldUntil",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "Islands");
        }
    }
}
