using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddSessionExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "PlayerSessions",
                type: "TEXT",
                nullable: true);

            // V5 M9h (hand-written; the same SQL in both providers' migration): linked guest tokens stop working
            migrationBuilder.Sql(AddSessionExpirySql.RotateLinkedGuestTokens);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_ExpiresAt",
                table: "PlayerSessions",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerSessions_ExpiresAt",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "PlayerSessions");
        }
    }
}
