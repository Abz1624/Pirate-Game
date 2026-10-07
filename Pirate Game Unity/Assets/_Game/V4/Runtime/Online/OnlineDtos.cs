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

        /// <summary>V5 M9a (I9a.1): the run log's JSON (an object), sent as the "log" field; null sends none (as older
        /// clients). Not a JsonUtility field: <see cref="ToJson"/> splices it in as raw JSON.</summary>
        [NonSerialized] public string logJson;

        /// <summary>The request body: the summary fields, plus "log" when there is one.</summary>
        public string ToJson()
        {
            var json = UnityEngine.JsonUtility.ToJson(this);
            if (string.IsNullOrEmpty(logJson)) return json;
            var end = json.LastIndexOf('}');
            return json.Substring(0, end) + ",\"log\":" + logJson + "}";
        }
    }

    /// <summary>V5 M9a (I9a.1): a request body that is already JSON (sent as is, not through JsonUtility).</summary>
    public sealed class RawJsonBody
    {
        public readonly string Json;
        public RawJsonBody(string json) { Json = json; }
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
        /// <summary>V5 M9a (I9a.3): the run passed the server's run checks (shadow mode: unchecked runs still rank).</summary>
        public bool @checked;
    }

    [Serializable] public sealed class LeaderboardMe { public int rank; public int kills; public int runSeconds; public int level; public bool @checked; }

    [Serializable]
    public sealed class LeaderboardResponse
    {
        public string mapId; public string weekStart; public string weekEnd;
        public LeaderboardEntry[] entries; public LeaderboardMe me;

        /// <summary>The player's row (JsonUtility fills a null "me" with zeros, so rank 0 means none).</summary>
        public bool HasMe => me != null && me.rank > 0;
    }
}
