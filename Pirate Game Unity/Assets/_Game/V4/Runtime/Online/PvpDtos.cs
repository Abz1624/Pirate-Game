using System;

namespace RoguePirates.V4.Online
{
    // Playtest (3 Oct, owner): the 1v1 lobby and match JSON (server/README.md, "1v1 lobbies" and "1v1 match protocol"),
    // as JsonUtility reads and writes it. JsonUtility fills a missing or null object with defaults, so "is it there" checks
    // read an id (an empty matchId = no match). The per-player dictionaries of match_end are read by PvpJson.

    [Serializable] public sealed class PvpDraft { public string[] weapons; public string[] logbooks; }

    [Serializable]
    public sealed class LobbySeat
    {
        public string seat; public string playerId; public string name; public string tag; public int rating; public bool bot;
        public string status; public bool ready; public bool hasDraft; public PvpDraft draft;
        public string Display => string.IsNullOrEmpty(tag) ? name : name + tag;
    }

    [Serializable] public sealed class ArenaInfo { public float centerX; public float centerZ; public float radius; }
    [Serializable] public sealed class SpawnInfo { public int seat; public string playerId; public float x; public float z; public float yaw; }

    [Serializable]
    public sealed class StormInfo
    {
        public long shrinkStartMs; public long shrinkEndMs; public float startRadius; public float endRadius;
        public float damagePerSecond; public float damageGrowthPerSecond;
    }

    [Serializable]
    public sealed class LobbyMatch
    {
        public string matchId; public int seed; public string matchServer; public string hostMode; public string hostPlayerId;
        public long serverNow; public long countdownMs; public long startsAt; public long startedAt; public long endsAt; public long durationMs;
        public ArenaInfo arena; public SpawnInfo[] spawns; public StormInfo storm; public int botSeat;
    }

    [Serializable]
    public sealed class LobbyState
    {
        public string t; public string lobbyId; public string code; public string inviteUrl; public bool @public; public string status;
        public string hostPlayerId; public string guestPlayerId; public string createdAt; public string updatedAt; public string expiresAt;
        public LobbySeat[] seats; public LobbyMatch match;

        public bool HasMatch => match != null && !string.IsNullOrEmpty(match.matchId);
        public LobbySeat Seat(string playerId)
        {
            if (seats == null || string.IsNullOrEmpty(playerId)) return null;
            foreach (var s in seats) if (s != null && s.playerId == playerId) return s;
            return null;
        }
        public LobbySeat Host => seats != null && seats.Length > 0 ? seats[0] : null;
        public LobbySeat Guest => seats != null && seats.Length > 1 ? seats[1] : null;
    }

    [Serializable] public sealed class PublicHost { public string playerId; public string name; public string tag; public int rating; }
    [Serializable] public sealed class PublicLobby { public string lobbyId; public string code; public string inviteUrl; public string createdAt; public string expiresAt; public PublicHost host; }
    [Serializable] public sealed class PublicLobbiesResponse { public PublicLobby[] lobbies; }

    [Serializable] public sealed class CreateLobbyRequest { public bool @public; }
    [Serializable] public sealed class JoinLobbyRequest { public string code; }
    [Serializable] public sealed class LobbyStatusRequest { public string status; }
    [Serializable] public sealed class LobbyStatusDraftRequest { public string status; public PvpDraft draft; }
    [Serializable] public sealed class RematchResponse { public string status; public string by; public string waitingFor; public string lobbyId; public string code; }

    // ---- The match protocol (relay) ----

    /// <summary>Any message: its type (and the bot flag the host's bot messages carry).</summary>
    [Serializable] public sealed class PvpMsg { public string t; public bool asBot; }

    [Serializable]
    public sealed class BarrelRules
    {
        public long xpEveryMs; public long healthEveryMs; public long firstXpMs; public long firstHealthMs; public long ttlMs;
        public float claimRadius; public float healthHealFraction;
    }

    [Serializable]
    public sealed class MatchPlayer
    {
        public int seat; public string playerId; public string name; public string tag; public int rating; public bool bot; public PvpDraft draft;
        public string Display => string.IsNullOrEmpty(tag) ? name : name + tag;
    }

    [Serializable] public sealed class BotInfo { public int seat; public string playerId; public string simulatedBy; }
    [Serializable] public sealed class LiveInfo { public bool ended; public BarrelMsg[] barrels; }

    [Serializable]
    public sealed class MatchStartMsg
    {
        public string t; public string matchId; public string lobbyId; public int seed; public int you; public string youPlayerId;
        public long serverNow; public long countdownMs; public long startsAt; public long endsAt; public long durationMs;
        public ArenaInfo arena; public SpawnInfo[] spawns; public StormInfo storm; public BarrelRules barrels;
        public MatchPlayer[] players; public BotInfo bot; public LiveInfo live;

        public bool HasBot => bot != null && !string.IsNullOrEmpty(bot.playerId);
        public MatchPlayer Player(int seat)
        {
            if (players == null) return null;
            foreach (var p in players) if (p != null && p.seat == seat) return p;
            return null;
        }
        public SpawnInfo Spawn(int seat)
        {
            if (spawns == null) return null;
            foreach (var s in spawns) if (s != null && s.seat == seat) return s;
            return null;
        }
    }

    [Serializable]
    public sealed class StateMsg
    {
        public string t = "state"; public string from; public long st; public float x; public float z; public float yaw; public float vx; public float vz;
        public float hp; public float maxHp; public int level; public bool alive = true; public bool boosting; public bool asBot;
    }

    [Serializable] public sealed class HitMsg { public string t = "hit"; public float dmg; public string weaponId; public string from; public string to; public long st; public bool asBot; }
    // Playtest (3 Oct, owner, round 7): cosmetic weapon effects and the loadout, relayed verbatim to the opponent ("fx").
    // k: f fired, s side salvo, b beam (x/z: offset from the ship; dx/dz: direction), e explosion, l lightning,
    // a area impact, m mortar reticle (x/z: arena point), p weapon pulse (around the ship); r: radius or range.
    [Serializable] public sealed class PvpFx { public string k; public string w; public float x; public float z; public float dx; public float dz; public float r; }
    [Serializable] public sealed class FxMsg { public string t = "fx"; public string from; public PvpFx[] e; public string[] lo; public int[] lv; }
    [Serializable] public sealed class PickupMsg { public string t = "pickup"; public int id; public bool asBot; }
    [Serializable] public sealed class PingMsg { public string t = "ping"; public long cs; }
    [Serializable] public sealed class PongMsg { public string t; public long cs; public long st; }

    [Serializable]
    public sealed class BarrelMsg
    {
        public string t; public int id; public string kind; public float x; public float z; public long st; public long expiresAt; public string by;
        public bool IsXp => kind == "xp";
    }

    [Serializable] public sealed class PresenceMsg { public string t; public string playerId; public bool connected; public long forfeitAt; }
    [Serializable] public sealed class RejectMsg { public string t; public string of; public string reason; public int id; }
    [Serializable] public sealed class RematchMsg { public string t; public string status; public string by; public string lobbyId; public string code; }

    [Serializable]
    public sealed class EndStats
    {
        public float damageDealt; public float damageTaken; public int level; public int barrelsTaken; public float finalHp; public float maxHp; public bool bot;
    }

    [Serializable] public sealed class RatingChange { public int before; public int after; public int change; }

    [Serializable]
    public sealed class MatchEndMsg
    {
        public string t; public string matchId; public string winnerPlayerId; public string reason; public bool rated; public int durationSeconds; public long rematchUntil;
    }
}
