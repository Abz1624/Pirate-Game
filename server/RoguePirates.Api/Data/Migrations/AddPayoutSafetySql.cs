namespace RoguePirates.Api.Data.Migrations;

/// <summary>
/// Data fixes run by the AddPayoutSafety migrations (13 Oct hotfix), before the unique live-payout index is created.
/// Plain SQL that runs unchanged on Postgres and SQLite. Never change it once deployed (it is part of a migration).
/// </summary>
public static class AddPayoutSafetySql
{
    /// <summary>Payouts left pending without a signature by the old code may have been sent: unconfirmed, checked by hand.</summary>
    public const string LegacyPending =
        "UPDATE \"Payouts\" SET \"Status\" = 'unconfirmed', " +
        "\"Error\" = 'From before the 13 Oct hotfix: check on the explorer by hand before any retry.' " +
        "WHERE \"Status\" = 'pending' AND \"Signature\" IS NULL;";

    /// <summary>Duplicate live payouts of one obligation: all but the oldest get RefId "&lt;refId&gt;#dup&lt;id&gt;".</summary>
    public const string MarkDuplicates =
        "UPDATE \"Payouts\" SET \"RefId\" = substr(\"RefId\", 1, 40) || '#dup' || CAST(\"Id\" AS TEXT) " +
        "WHERE \"RefId\" IS NOT NULL AND \"Status\" <> 'failed' AND EXISTS (SELECT 1 FROM \"Payouts\" AS o " +
        "WHERE o.\"Reason\" = \"Payouts\".\"Reason\" AND o.\"RefId\" = \"Payouts\".\"RefId\" AND o.\"Status\" <> 'failed' " +
        "AND o.\"Id\" < \"Payouts\".\"Id\");";
}
