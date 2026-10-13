using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoguePirates.Api.Data.Migrations.Postgres
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
                type: "bigint",
                nullable: true);

            // 13 Oct hotfix (hand-written; the same SQL in the Sqlite migration): payouts from before the hotfix left pending
            // without a signature may have been sent (the old code saved the signature only after confirming): they become
            // unconfirmed for the owner to check by hand; the reconciler never touches them.
            migrationBuilder.Sql(AddPayoutSafetySql.LegacyPending);
            // Existing duplicate live payouts for one obligation (a past double payment) keep their rows but get a marked
            // RefId, so the unique index can be created; the oldest keeps the obligation's key.
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
