namespace RoguePirates.Api.Data.Migrations;

/// <summary>
/// Data fixes run by the AddMintSafety migrations (V5 M9h). Plain SQL that runs unchanged on Postgres and SQLite. Never
/// change it once deployed (it is part of a migration).
/// </summary>
public static class AddMintSafetySql
{
    /// <summary>One free island claim per wallet ever: profiles that own an island or sold one count as having claimed
    /// (claims and purchases were not told apart before; a buyer who never claimed is caught too, the safe side).</summary>
    public const string MarkClaimed =
        "UPDATE \"Players\" SET \"IslandClaimedAt\" = \"CreatedAt\" WHERE \"Id\" IN (SELECT \"OwnerPlayerId\" FROM \"Islands\") " +
        "OR \"Id\" IN (SELECT \"SellerPlayerId\" FROM \"MarketListings\" WHERE \"Status\" = 'sold');";

    /// <summary>Islands minted before M9h: the claim time stands in for when the mint was sent and confirmed (a claim
    /// minted right away), so the report lists them.</summary>
    public const string MintedTimes =
        "UPDATE \"Islands\" SET \"MintSentAt\" = \"CreatedAt\", \"MintedAt\" = \"CreatedAt\" WHERE \"NftStatus\" = 'minted';";
}
