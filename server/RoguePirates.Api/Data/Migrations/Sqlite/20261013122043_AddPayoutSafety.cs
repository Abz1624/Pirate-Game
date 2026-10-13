using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddPayoutSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastValidBlockHeight",
                table: "Payouts",
                type: "INTEGER",
                nullable: true);

            // 13 Oct hotfix (hand-written; the same SQL in the Postgres migration), see AddPayoutSafetySql
            migrationBuilder.Sql(AddPayoutSafetySql.LegacyPending);
            migrationBuilder.Sql(AddPayoutSafetySql.MarkDuplicates);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Reason_RefId_Live",
                table: "Payouts",
                columns: new[] { "Reason", "RefId" },
                unique: true,
                filter: "\"RefId\" IS NOT NULL AND \"Status\" <> 'failed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_Reason_RefId_Live",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "LastValidBlockHeight",
                table: "Payouts");
        }
    }
}
