using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddMintSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IslandClaimedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MintLastValidBlockHeight",
                table: "Islands",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MintSentAt",
                table: "Islands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MintedAt",
                table: "Islands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingMint",
                table: "Islands",
                type: "character varying(44)",
                maxLength: 44,
                nullable: true);

            // V5 M9h (hand-written; the same SQL in both providers' migration): owners and sellers of islands have used their
            // one free claim; islands already minted get their mint times for the report
            migrationBuilder.Sql(AddMintSafetySql.MarkClaimed);
            migrationBuilder.Sql(AddMintSafetySql.MintedTimes);

            migrationBuilder.CreateIndex(
                name: "IX_Islands_MintSentAt",
                table: "Islands",
                column: "MintSentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Islands_MintSentAt",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "IslandClaimedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MintLastValidBlockHeight",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "MintSentAt",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "MintedAt",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "PendingMint",
                table: "Islands");
        }
    }
}
