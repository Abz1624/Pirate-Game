using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
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
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ReasonCodes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RuleIds = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: false),
                    RulesVersion = table.Column<int>(type: "integer", nullable: false),
                    ClientBuild = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConfigHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
