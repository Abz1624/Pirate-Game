using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddRunLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunLogs",
                columns: table => new
                {
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    SeedSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConfigHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BalanceVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BuildId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientRunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Bytes = table.Column<int>(type: "integer", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLogs", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_RunLogs_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunLogs");
        }
    }
}
