using System.Threading.Tasks;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): the AI captain and the wager state across scenes. The server's practice bot is shown as
    /// <see cref="HackathonRules.CaptainName"/> (lobby seats and the match's players, through V4's display hook
    /// <see cref="PvpSession.LobbyReceived"/>: names only, ids and rules unchanged). The last staked lobby's wager is
    /// kept here so the run scene and the menu can show its result and payout after the match.
    /// </summary>
    public static class HackathonPvp
    {
        /// <summary>The lobby of the last wager seen (kept after the lobby is left, for the result).</summary>
        public static string WagerLobbyId { get; private set; }
        public static WagerView Wager { get; private set; }
        /// <summary>The wager changed (a poll or a deposit).</summary>
        public static event System.Action<WagerView> WagerChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            WagerLobbyId = null;
            Wager = null;
            WagerChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            PvpSession.LobbyReceived = NameLobby;
            PvpSession.MatchStartReceived = NameMatch;
        }

        static void NameLobby(LobbyState state)
        {
            if (state?.seats == null) return;
            foreach (var s in state.seats)
                if (s != null && s.bot) { s.name = HackathonRules.CaptainName; s.tag = string.Empty; }
        }

        static void NameMatch(MatchStartMsg start)
        {
            if (start?.players == null) return;
            var botId = start.HasBot ? start.bot.playerId : null;
            foreach (var p in start.players)
                if (p != null && (p.bot || (botId != null && p.playerId == botId))) { p.name = HackathonRules.CaptainName; p.tag = string.Empty; }
        }

        /// <summary>The current match is against the AI captain.</summary>
        public static bool VsCaptain(MatchStartMsg match) => match != null && match.HasBot;

        /// <summary>Records a wager read elsewhere (a stake set, a deposit) as the current one.</summary>
        public static void Adopt(string lobbyId, WagerView w)
        {
            if (string.IsNullOrEmpty(lobbyId) || w == null || !w.Exists) return;
            WagerLobbyId = lobbyId;
            Wager = w;
            WagerChanged?.Invoke(w);
        }

        /// <summary>Reads the wager of <paramref name="lobbyId"/> (null: no stake there). Returns the error, or null.</summary>
        public static async Task<string> Refresh(string lobbyId)
        {
            var (w, error) = await HackathonApi.GetWager(lobbyId);
            if (error != null) return error;
            if (w != null && w.Exists) { WagerLobbyId = lobbyId; Wager = w; WagerChanged?.Invoke(w); }
            else if (WagerLobbyId == lobbyId) { Wager = null; WagerChanged?.Invoke(null); }
            return null;
        }

        /// <summary>The wager of the lobby the player is in (or was in last), when it has one.</summary>
        public static WagerView For(string lobbyId) => lobbyId != null && lobbyId == WagerLobbyId ? Wager : null;
    }
}
