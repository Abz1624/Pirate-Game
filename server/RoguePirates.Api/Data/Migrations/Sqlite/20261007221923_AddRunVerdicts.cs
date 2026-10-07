using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddRunVerdicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunVerdicts",
                columns: table => new
                {
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    ReasonCodes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RuleIds = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RulesVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientBuild = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ConfigHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunVerdicts", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_RunVerdicts_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunVerdicts_RulesVersion",
                table: "RunVerdicts",
                column: "RulesVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunVerdicts");
        }
    }
}
