using System;

namespace RoguePirates.Hackathon
{
    // Hackathon (12 Oct, U-3): the API's island, raid and market JSON (server/README.md, "Hackathon: islands, RUM, raids,
    // island NFT market"), as JsonUtility reads it. JsonUtility fills a null object with defaults, so "is it there" checks
    // read an id.

    [Serializable] public sealed class IslandOwner { public string playerId; public string name; public string wallet; public string walletShort; }

    [Serializable]
    public sealed class IslandNft
    {
        public string status; public string mint; public string mintSignature; public string error;
        public string explorerUrl; public string metadataUrl; public string imageUrl;
        public bool Minted => status == "minted" && !string.IsNullOrEmpty(mint);
    }

    [Serializable]
    public sealed class IslandRum { public long exposed; public long vault; public long total; public long cap; public long ratePerHour; public int vaultPercent; public string lastAccrualAt; }

    [Serializable] public sealed class IslandBuilding { public string kind; public int level; public int maxLevel; public long nextCost; }

    [Serializable]
    public sealed class IslandDefence { public double hullMultiplier; public int extraWaves; public int weaponLevel; public int difficulty; public int cannonTower; public int walls; }

    /// <summary>The island's defence record (raids it held / finished raids against it); successPercent is 0 with no raids
    /// (JsonUtility reads the server's null as 0), so read raidsAgainst first.</summary>
    [Serializable]
    public sealed class DefenceRecord { public int raidsAgainst; public int defended; public int fallen; public int successPercent; public string label; }

    [Serializable]
    public sealed class IslandView
    {
        public string islandId; public string name; public int slot; public string slotName; public string region;
        public int x; public int y; public int level;
        public IslandOwner owner; public bool isMine; public IslandNft nft; public IslandRum rum;
        public IslandBuilding[] buildings; public IslandDefence defence;
        public string shieldUntil; public bool shielded; public bool listed; public string listingId; public long priceLamports;
        public bool raidInProgress; public bool raidable; public string createdAt; public DefenceRecord defenceRecord;

        public IslandBuilding Building(string kind)
        {
            if (buildings == null) return null;
            foreach (var b in buildings) if (b != null && b.kind == kind) return b;
            return null;
        }
    }

    [Serializable] public sealed class IslandSlotView { public int slot; public string name; public string region; public int x; public int y; public string islandId; }

    [Serializable]
    public sealed class IslandWorld { public int mapWidth; public int mapHeight; public IslandSlotView[] slots; public int freeSlots; public IslandView[] islands; }

    [Serializable] public sealed class MyIslands { public long rum; public IslandView[] islands; }

    [Serializable] public sealed class ClaimRequest { public string name; }
    [Serializable] public sealed class BuildRequest { public string kind; }
    [Serializable] public sealed class CollectResponse { public long collected; public long fromStore; public long fromVault; public long rum; public IslandView island; }
    [Serializable] public sealed class BuildResponse { public string kind; public int level; public long cost; public long rum; public IslandView island; }

    // ---- Raids ----

    [Serializable]
    public sealed class DefenderConfig
    {
        public string captainId; public string captainName; public int seed; public double hullMultiplier; public int extraWaves;
        public int weaponLevel; public int cannonTower; public int walls; public int difficulty; public int timeLimitSeconds;
    }

    [Serializable] public sealed class RaidLoot { public long exposedRum; public long ifWon; public long ifPartial; public int lootPercent; }

    [Serializable]
    public sealed class RaidStart
    {
        public string raidId; public string islandId; public string islandName; public string startedAt; public string expiresAt;
        public long feePaid; public long rum; public DefenderConfig defender; public RaidLoot loot; public RaidDefence defence;
        /// <summary>A practice raid on your own island (POST /api/raids/practice): nothing is charged, stored or reported.</summary>
        public bool practice;
    }

    [Serializable] public sealed class PracticeRequest { public string islandId; }


    [Serializable] public sealed class RaidResultRequest { public bool won; public bool partial; public int seconds; }

    [Serializable]
    public sealed class RaidResult
    {
        public string raidId; public string result; public long rumLooted; public long rum; public int seconds;
        public string defenderShieldUntil; public int guardianMinute; public int minuteReached; public bool guardianSunk;
    }

    [Serializable] public sealed class RaidParty { public string playerId; public string name; public string wallet; }

    [Serializable]
    public sealed class RaidRow
    {
        public string raidId; public string islandId; public string islandName; public RaidParty attacker; public RaidParty defender;
        public string result; public long rumLooted; public int seconds; public string startedAt; public string finishedAt;
    }

    [Serializable] public sealed class MyRaids { public RaidRow[] attacks; public RaidRow[] defences; }

    // ---- Market ----

    [Serializable]
    public sealed class ListingIsland { public string islandId; public string name; public int slot; public string slotName; public string region; public int level; public long ratePerHour; public DefenceRecord defenceRecord; }

    [Serializable]
    public sealed class Listing
    {
        public string listingId; public string status; public long priceLamports; public double priceSol; public string nftMint; public string memo; public string treasury;
        public ListingIsland island; public IslandOwner seller; public IslandOwner buyer;
        public string escrowSignature; public string paymentSignature; public string transferSignature; public string payoutSignature;
        public string transferExplorerUrl; public string payoutExplorerUrl; public string error; public string createdAt; public string closedAt;
    }

    [Serializable] public sealed class MarketView { public string treasury; public string cluster; public Listing[] listings; }
    [Serializable] public sealed class ListRequest { public string islandId; public long priceLamports; public string signature; }
    [Serializable] public sealed class SignatureRequest { public string signature; }
    [Serializable] public sealed class TreasuryView { public string treasury; public string address; public string cluster; }
}
