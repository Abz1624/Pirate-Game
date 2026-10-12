using System;
using System.Threading.Tasks;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): the API calls of the 1v1 wagers, the daily race and the weekly rewards (server/README.md,
    /// "Hackathon: ..."), through V4's API client and the active (wallet) profile. Every call returns a player-facing error
    /// (or null) instead of throwing.
    /// </summary>
    public static class HackathonApi
    {
        static ApiClient Client => OnlineService.Client;
        static string Token => OnlineService.Active != null ? OnlineService.Active.token : null;

        // ---------------------------------------------------------------- wagers

        public static async Task<(WagerView wager, string error)> GetWager(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId)) return (null, "No lobby.");
            var r = await Client.Get<WagerView>("/api/wagers/" + lobbyId);
            if (r.Ok) return (r.Data, null);
            if (r.Status == 404) return (null, null); // no stake on this lobby
            return (null, Error("wager", r));
        }

        /// <summary>Host only, before any deposit: sets the lobby's stake (0 removes it).</summary>
        public static async Task<(WagerView wager, string error)> SetStake(string lobbyId, long lamports)
        {
            var r = await Client.Post<LobbyWithWager>("/api/wagers/" + lobbyId, new WagerStakeRequest { stakeLamports = lamports }, Token);
            if (!r.Ok) return (null, Error("stake", r));
            return (r.Data?.wager, null);
        }

        /// <summary>
        /// Pays the stake from the connected wallet to the treasury with the wager's memo, then has the server verify it.
        /// A transfer the server cannot see yet (409 tx_not_confirmed) is retried for about 30 s; the signature is kept so
        /// a later retry does not pay twice.
        /// </summary>
        public static async Task<(WagerView wager, string error)> Deposit(WagerView w, Action<string> progress)
        {
            if (w == null || !w.Exists) return (null, "This lobby has no stake.");
            var session = Web3Session.Instance;
            if (!session.WalletConnected) return (null, "Connect your wallet (Wallet button, top of the menu) to deposit.");
            if (string.IsNullOrEmpty(w.treasury)) return (null, "The server has no treasury address for this wager.");

            var signature = PendingDeposit(w.lobbyId);
            if (string.IsNullOrEmpty(signature))
            {
                progress?.Invoke("Approve the " + HackathonRules.Sol(w.stakeLamports) + " transfer in your wallet...");
                var (sig, error) = await session.TransferSol(w.treasury, HackathonRules.LamportsToSol(w.stakeLamports), string.IsNullOrEmpty(w.memo) ? "wager:" + w.lobbyId : w.memo);
                if (error != null) return (null, error);
                signature = sig;
                RememberDeposit(w.lobbyId, signature);
            }

            for (var attempt = 0; attempt < 12; attempt++)
            {
                progress?.Invoke(attempt == 0 ? "Sent. The server is checking the deposit on devnet..." : "Waiting for devnet to confirm the deposit...");
                var r = await Client.Post<LobbyWithWager>("/api/wagers/" + w.lobbyId + "/deposit", new WagerDepositRequest { signature = signature }, Token);
                if (r.Ok) { RememberDeposit(w.lobbyId, null); return (r.Data?.wager, null); }
                if (r.ErrorCode == "tx_not_confirmed" || r.Outcome == ApiOutcome.Offline)
                {
                    await Awaitable.WaitForSecondsAsync(2.5f);
                    continue;
                }
                if (r.ErrorCode == "already_deposited")
                {
                    RememberDeposit(w.lobbyId, null);
                    var (again, _) = await GetWager(w.lobbyId);
                    return (again, null);
                }
                // A transfer the server refuses for good (wrong memo, too little) is not retried with the same signature.
                if (r.ErrorCode != null && r.ErrorCode != "tx_not_confirmed") RememberDeposit(w.lobbyId, null);
                return (null, Error("deposit", r));
            }
            return (null, "Devnet has not confirmed the transfer yet. Press Check deposit in a few seconds (you will not pay twice).");
        }

        static string _pendingLobby, _pendingSig;

        /// <summary>A deposit transfer for <paramref name="lobbyId"/> was sent but the server has not confirmed it yet (leaving
        /// the lobby then could strand the SOL, so the menu keeps the lobby and offers "Check deposit").</summary>
        public static bool HasPendingDeposit(string lobbyId) => !string.IsNullOrEmpty(PendingDeposit(lobbyId));
        static string PendingDeposit(string lobbyId) => _pendingLobby == lobbyId ? _pendingSig : null;
        static void RememberDeposit(string lobbyId, string sig)
        {
            _pendingLobby = sig != null ? lobbyId : null;
            _pendingSig = sig;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pendingLobby = _pendingSig = null; }

        // ---------------------------------------------------------------- daily race

        public static async Task<(DailyRaceResponse data, string error)> Race()
        {
            await OnlineService.EnsureConnected();
            var r = await Client.Get<DailyRaceResponse>("/api/race/daily", Token);
            return r.Ok && r.Data?.race != null ? (r.Data, null) : (null, Error("daily race", r));
        }

        public static async Task<(RaceBoardResponse data, string error)> RaceBoard(string date, int limit = 50)
        {
            var path = "/api/race/daily/board?limit=" + limit + (string.IsNullOrEmpty(date) ? string.Empty : "&date=" + Uri.EscapeDataString(date));
            var r = await Client.Get<RaceBoardResponse>(path, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("race board", r));
        }

        public static async Task<(RaceEntryResponse data, string error)> SubmitRace(RaceEntryRequest entry)
        {
            if (Token == null) return (null, "Sign in (Wallet button) to post race times.");
            var r = await Client.Post<RaceEntryResponse>("/api/race/daily/entry", entry, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("race entry", r));
        }

        // ---------------------------------------------------------------- weekly rewards

        public static async Task<(WeeklyRewardsResponse data, string error)> WeeklyRewards(string weekStart = null)
        {
            var path = "/api/rewards/weekly" + (string.IsNullOrEmpty(weekStart) ? string.Empty : "?weekStart=" + Uri.EscapeDataString(weekStart));
            var r = await Client.Get<WeeklyRewardsResponse>(path);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("rewards", r));
        }

        // ---------------------------------------------------------------- errors

        public static string Error<T>(string what, ApiResult<T> r) where T : class
        {
            switch (r.Outcome)
            {
                case ApiOutcome.NotConfigured: return "No game server configured.";
                case ApiOutcome.Offline: return "The game server is not reachable. Check the connection and try again.";
            }
            switch (r.ErrorCode)
            {
                case "wallet_required": return "Sign in with a wallet first (Wallet button, top of the menu).";
                case "treasury_off": return "Wagers are switched off on the server right now.";
                case "invalid_stake": return "That stake is not allowed (0.001 to 0.5 SOL).";
                case "wager_locked": return "A deposit was made: the stake cannot change now.";
                case "memo_mismatch": return "That transfer was not for this wager (memo mismatch).";
                case "amount_too_low": return "The transfer was smaller than the stake.";
                case "already_started": return "The match already started.";
                case "wager_closed": return "This wager is closed.";
                case "not_in_lobby": return "You are not seated in this lobby.";
                case "seed_mismatch": return "This run was not on today's race course.";
                case "target_not_reached": return "The race needs 100 kills.";
                case "implausible_time": return "The server did not accept that race time.";
                case "race_closed": return "Today's race is closed.";
                case "race_not_open": return "The race is not open yet.";
            }
            if (!string.IsNullOrEmpty(r.Message)) return r.Message;
            return $"The server refused the {what} ({r.Status}).";
        }
    }
}
