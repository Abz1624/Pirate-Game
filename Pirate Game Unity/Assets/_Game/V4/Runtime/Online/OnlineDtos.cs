using System;

namespace RoguePirates.V4.Online
{
    // Playtest (3 Oct, owner): the playtest API's JSON (server/README.md), as JsonUtility reads and writes it (field names
    // are the API's camelCase names). Lobby and match shapes come with the PvP task.

    [Serializable] public sealed class ApiError { public string error; public string message; }
    [Serializable] public sealed class HealthResponse { public bool ok; public string db; }

    [Serializable] public sealed class NameRequest { public string name; }

    [Serializable]
    public sealed class CreateProfileResponse { public string playerId; public string token; public string name; public string tag; }

    [Serializable]
    public sealed class ProfileResponse
    {
        public string playerId; public string name; public string tag;
        public int rating; public int wins; public int losses; public string createdAt;
    }

    [Serializable]
    public sealed class ProfileStatsResponse
    {
        public string playerId; public int bestKillsAllTime; public int bestKillsThisWeek; public int runsPlayed;
        public string weekStart; public string weekEnd;
    }

    [Serializable]
    public sealed class RunSubmitRequest
    {
        public int kills; public int runSeconds; public int level;
        public string captain; public string mapId; public string buildHash; public string clientVersion;
    }

    [Serializable] public sealed class RunBest { public int kills; public int runSeconds; public int level; public string captain; }

    [Serializable]
    public sealed class RunSubmitResponse
    {
        public bool accepted; public long runId; public string weekStart; public string weekEnd;
        public bool isNewWeeklyBest; public RunBest weeklyBest;
    }

    [Serializable]
    public sealed class LeaderboardEntry
    {
        public int rank; public string playerId; public string name; public string tag;
        public int kills; public int runSeconds; public int level; public string captain;
    }

    [Serializable] public sealed class LeaderboardMe { public int rank; public int kills; public int runSeconds; public int level; }

    [Serializable]
    public sealed class LeaderboardResponse
    {
        public string mapId; public string weekStart; public string weekEnd;
        public LeaderboardEntry[] entries; public LeaderboardMe me;

        /// <summary>The player's row (JsonUtility fills a null "me" with zeros, so rank 0 means none).</summary>
        public bool HasMe => me != null && me.rank > 0;
    }
}
