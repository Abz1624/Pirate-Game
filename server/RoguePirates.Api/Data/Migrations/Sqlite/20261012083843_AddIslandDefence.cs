using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddIslandDefence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuardianMinute",
                table: "Raids",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GuardianSunk",
                table: "Raids",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinuteReached",
                table: "Raids",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenceJson",
                table: "Islands",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DefenceUpdatedAt",
                table: "Islands",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianMinute",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "GuardianSunk",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "MinuteReached",
                table: "Raids");

            migrationBuilder.DropColumn(
                name: "DefenceJson",
                table: "Islands");

            migrationBuilder.DropColumn(
                name: "DefenceUpdatedAt",
                table: "Islands");
        }
    }
}
