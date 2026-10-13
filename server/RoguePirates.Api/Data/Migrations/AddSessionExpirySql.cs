namespace RoguePirates.Api.Data.Migrations;

/// <summary>
/// Data fix run by the AddSessionExpiry migrations (V5 M9h). Plain SQL that runs unchanged on Postgres and SQLite. Never
/// change it once deployed (it is part of a migration).
/// </summary>
public static class AddSessionExpirySql
{
    /// <summary>A guest profile that linked a wallet before M9h kept its guest token working; wallet profiles are reached
    /// through sessions only, so their own token becomes a value no bearer token hashes to (hashes are 64 hex digits).</summary>
    public const string RotateLinkedGuestTokens =
        "UPDATE \"Players\" SET \"TokenHash\" = 'rotated:' || CAST(\"Id\" AS TEXT) WHERE \"WalletAddress\" IS NOT NULL;";
}
