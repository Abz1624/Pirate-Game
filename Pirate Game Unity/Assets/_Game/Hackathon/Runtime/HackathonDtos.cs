using System;

namespace RoguePirates.Hackathon
{
    // Hackathon (12 Oct): the API's wager, daily race and weekly reward JSON (server/README.md, "Hackathon: ..."), as
    // JsonUtility reads it. JsonUtility fills a null object with defaults, so "is it there" checks read an id or a count.

    [Serializable] public sealed class WagerStakeRequest { public long stakeLamports; }
    [Serializable] public sealed class WagerDepositRequest { public string signature; }

    [Serializable] public sealed class WagerSeat { public string playerId; public bool house; public bool deposited; public string signature; }

    [Serializable]
    public sealed class PayoutView
    {
        public string payoutId; public string wallet; public long lamports; public double sol; public string reason; public string refId;
        public string status; public string signature; public string explorerUrl; public string createdAt; public string sentAt;
    }

    [Serializable]
    public sealed class WagerView
    {
        public string wagerId; public string kind; public string lobbyId; public string matchId;
        public long stakeLamports; public double stakeSol; public long potLamports; public bool vsHouse;
        public string status; public bool funded; public string result; public string memo; public string treasury; public string cluster;
        public WagerSeat host; public WagerSeat guest;
        public string winnerPlayerId; public string payoutSignature; public PayoutView[] payouts;
        public string createdAt; public string settledAt;

        public bool Exists => !string.IsNullOrEmpty(wagerId) && stakeLamports > 0;
        public bool Open => status == "awaiting_deposits" || status == "funded";
        public bool Closed => status == "settled" || status == "refunded" || status == "cancelled";
    }

    /// <summary>The lobby JSON of the wager endpoints (only the wager is read here; the lobby goes to V4's session).</summary>
    [Serializable] public sealed class LobbyWithWager { public string lobbyId; public string status; public WagerView wager; }

    // ---- Daily race ----

    [Serializable]
    public sealed class DailyRace
    {
        public long raceId; public string date; public int seed; public string mapId; public int targetKills; public string goal;
        public string opensAt; public string closesAt; public string serverNow;
    }

    [Serializable] public sealed class RaceMine { public int rank; public double timeSeconds; public long timeMs; public int attempts; public string verdict; public string submittedAt; }

    [Serializable]
    public sealed class DailyRaceResponse
    {
        public DailyRace race; public int entries; public RaceMine me;
        public bool HasMe => me != null && me.rank > 0;
    }

    [Serializable]
    public sealed class RaceEntryRequest
    {
        public string date; public int seed; public double timeSeconds; public int kills; public int level; public string clientVersion;
    }

    [Serializable]
    public sealed class RaceEntryResponse
    {
        public bool accepted; public bool improved; public DailyRace race; public string verdict; public bool @checked; public RaceMine best;
    }

    [Serializable]
    public sealed class RaceBoardEntry
    {
        public int rank; public string playerId; public string name; public string tag; public string wallet;
        public double timeSeconds; public long timeMs; public int attempts; public string verdict; public bool @checked; public string submittedAt;
    }

    [Serializable]
    public sealed class RaceBoardResponse
    {
        public DailyRace race; public int total; public RaceBoardEntry[] entries; public RaceMine me;
        public bool HasMe => me != null && me.rank > 0;
    }

    // ---- Weekly rewards ----

    [Serializable] public sealed class RewardTableRow { public int rank; public long lamports; public double sol; }

    [Serializable]
    public sealed class RewardWinner
    {
        public int rank; public string playerId; public string name; public string tag; public string wallet; public string walletShort;
        public int kills; public int runSeconds; public bool @checked; public long lamports; public double sol; public PayoutView payout;
    }

    [Serializable]
    public sealed class WeeklyRewardsResponse
    {
        public string weekStart; public string weekEnd; public string mapId; public bool dryRun; public bool alreadyRecorded; public bool checkedOnly;
        public RewardTableRow[] table; public RewardWinner[] winners;
    }
}
