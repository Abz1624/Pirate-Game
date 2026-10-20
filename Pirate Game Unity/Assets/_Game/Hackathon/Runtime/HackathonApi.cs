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

        /// <summary>The end of a deposit attempt.</summary>
        public sealed class DepositResult
        {
            public WagerView Wager;
            public string Error;
            /// <summary>A transfer exists (from now or an earlier try): never say that no SOL was taken.</summary>
            public bool Sent;
            /// <summary>The server refused the seat and refunds the transfer.</summary>
            public bool Refunded;
            /// <summary>The server's final refusal, worded with what happens to the SOL (null otherwise).</summary>
            public string FinalLine;
            /// <summary>The transfer's signature (the explorer link), when there is one.</summary>
            public string Signature;
            /// <summary>The signature is still pending (Check deposit asks again; it is re-posted when the menu opens).</summary>
            public bool Pending;
            public bool Ok => Error == null;
        }

        /// <summary>The seam for the deposit post (tests): lobby id and signature to the server's answer.</summary>
        public static Func<string, string, Task<ApiResult<LobbyWithWager>>> PostDeposit = DefaultPostDeposit;

        static async Task<ApiResult<LobbyWithWager>> DefaultPostDeposit(string lobbyId, string signature) =>
            await Client.Post<LobbyWithWager>("/api/wagers/" + lobbyId + "/deposit", new WagerDepositRequest { signature = signature }, Token);

        public static void UseDefaultPostDeposit() => PostDeposit = DefaultPostDeposit;

        /// <summary>
        /// Pays the stake from the connected wallet to the treasury with the wager's memo, then has the server verify it.
        /// 13 Oct (review): the signature is kept (<see cref="PendingTransfers"/>, PlayerPrefs) the moment the wallet signs,
        /// before it is sent, and until the server answers for good: a retry, a reopened menu or a relaunch after the phone
        /// killed the game posts that signature again and never pays twice. tx_not_confirmed is retried for about 30 s.
        /// </summary>
        public static async Task<DepositResult> Deposit(WagerView w, Action<string> progress)
        {
            if (w == null || !w.Exists) return new DepositResult { Error = "This lobby has no stake." };
            var lobbyId = w.lobbyId;
            if (!PendingTransfers.Has(PendingTransfers.Deposit, lobbyId))
            {
                var session = Web3Session.Instance;
                if (!session.WalletConnected) return new DepositResult { Error = "Connect your wallet (Wallet button, top of the menu) to deposit." };
                if (string.IsNullOrEmpty(w.treasury)) return new DepositResult { Error = "The server has no treasury address for this wager." };
                // The wallet may be finishing something else (a balance read, a sign-in): wait briefly instead of failing.
                for (var i = 0; i < 40 && session.Busy; i++) await Awaitable.WaitForSecondsAsync(0.25f);
                if (session.Busy) return new DepositResult { Error = "The wallet is still busy with another request. Press Pay deposit to try again." };
                progress?.Invoke("Approve the " + HackathonRules.Sol(w.stakeLamports) + " transfer in your wallet...");
                var wallet = session.Address;
                var t = await session.TransferSol(w.treasury, HackathonRules.LamportsToSol(w.stakeLamports),
                    string.IsNullOrEmpty(w.memo) ? "wager:" + lobbyId : w.memo,
                    sig => PendingTransfers.Remember(PendingTransfers.Deposit, lobbyId, sig, wallet, w.stakeLamports));
                if (!t.Ok && !t.MaybeSent)
                {
                    // Refused before signing, or rejected by the node's preflight: nothing was broadcast.
                    PendingTransfers.Forget(PendingTransfers.Deposit, lobbyId);
                    return new DepositResult { Error = t.Error ?? "The transfer was not made." };
                }
                // V5 M9i: a wallet that sends itself may have sent it without naming the signature: nothing to check.
                if (!t.Ok && string.IsNullOrEmpty(t.Signature)) return new DepositResult { Error = t.Error, Sent = true };
                if (!t.Ok) progress?.Invoke("The transfer was signed; checking whether devnet has it...");
            }
            return await CheckDeposit(lobbyId, progress, HackathonRules.Sol(w.stakeLamports));
        }

        /// <summary>Posts this lobby's pending deposit signature and reads the server's answer (no wallet, no payment).</summary>
        public static async Task<DepositResult> CheckDeposit(string lobbyId, Action<string> progress, string stake = null, int attempts = 12)
        {
            var pending = PendingTransfers.Get(PendingTransfers.Deposit, lobbyId);
            if (pending == null) return new DepositResult { Error = "No deposit is waiting for this lobby." };
            stake ??= HackathonRules.Sol(pending.lamports);
            var a = await PaymentFlow.Post(PendingTransfers.Deposit, lobbyId, sig => PostDeposit(lobbyId, sig),
                attempt => progress?.Invoke(attempt == 0 ? "Sent. The server is checking the deposit on devnet..." : "Waiting for devnet to confirm the deposit..."),
                false, attempts);
            var result = new DepositResult { Sent = true, Signature = a.Signature };
            switch (a.Verdict)
            {
                case HackathonRules.PaymentVerdict.Accepted:
                    result.Wager = a.Data?.wager;
                    if (result.Wager == null) (result.Wager, _) = await GetWager(lobbyId);
                    return result;
                case HackathonRules.PaymentVerdict.Final:
                    result.Sent = a.Code != "expired";
                    result.Refunded = a.Code != null && a.Code.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0;
                    result.FinalLine = HackathonRules.FinalLine("deposit", stake, a.Code, a.Message);
                    result.Error = result.Refunded && !string.IsNullOrEmpty(a.Message) ? a.Message : result.FinalLine;
                    return result;
                case HackathonRules.PaymentVerdict.Hold:
                    result.Pending = true;
                    result.Error = (string.IsNullOrEmpty(a.Message) ? "The server did not take the deposit yet" : a.Message.TrimEnd('.')) +
                                   ". Your transfer is kept: press Check deposit to ask again (you will not pay twice).";
                    return result;
                default:
                    result.Pending = true;
                    result.Error = "Devnet has not confirmed the transfer yet. Press Check deposit in a few seconds (you will not pay twice).";
                    return result;
            }
        }

        /// <summary>A deposit transfer for <paramref name="lobbyId"/> was signed but the server has not answered for good yet
        /// (the menu keeps the lobby and offers "Check deposit"; leaving first posts it).</summary>
        public static bool HasPendingDeposit(string lobbyId) => PendingTransfers.Has(PendingTransfers.Deposit, lobbyId);

        /// <summary>
        /// Posts every pending deposit of the signed-in wallet (the menu opening, a relaunch): the server takes it, or
        /// refunds it when the seat is gone. Returns one line per final answer (for the hub message), or null.
        /// </summary>
        public static async Task<string> ResumePendingDeposits(string exceptLobbyId = null)
        {
            var wallet = Web3Session.Instance.Address;
            if (string.IsNullOrEmpty(wallet) || Token == null) return null;
            string lines = null;
            foreach (var e in PendingTransfers.All(PendingTransfers.Deposit, wallet))
            {
                if (e.key == exceptLobbyId) continue;
                var r = await CheckDeposit(e.key, null, null, 1);
                if (r.Pending) continue;
                var line = r.Ok ? "An earlier deposit of " + HackathonRules.Sol(e.lamports) + " was confirmed." : r.Error;
                lines = lines == null ? line : lines + "\n" + line;
            }
            return lines;
        }

        /// <summary>
        /// Leaves the current 1v1 lobby (voyages, raids and Leave call it). A deposit still pending for the lobby is posted
        /// first and its answer awaited, so the server can refund it if the seat goes; one devnet has not confirmed yet stays
        /// kept and is posted again when the menu opens. Returns the deposit's line to show, or null.
        /// </summary>
        public static async Task<string> LeaveLobbySafely(bool leave = true)
        {
            if (!PvpSession.Exists) return null;
            var pvp = PvpSession.Instance;
            var lobbyId = pvp.Lobby?.lobbyId;
            string line = null;
            if (lobbyId != null && HasPendingDeposit(lobbyId))
            {
                var r = await CheckDeposit(lobbyId, null, null, 6);
                line = r.Pending
                    ? "Your deposit is not confirmed yet; it is kept and checked again when you come back to the menu (the server refunds it if your seat is gone)."
                    : r.Ok ? "Your deposit was confirmed; leaving before the match refunds it to your wallet." : r.Error;
            }
            if (leave && pvp.Lobby != null) await pvp.Leave();
            return line;
        }

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

        /// <summary>V5 M9j: the server's start of a race attempt (the entry's time is checked against the time since it).
        /// Best effort: a failure only leaves the entry unchecked (shadow mode).</summary>
        public static async Task<string> StartRace(string date)
        {
            if (Token == null) return "not signed in";
            var body = string.IsNullOrEmpty(date) ? "{}" : "{\"date\":\"" + date + "\"}";
            var r = await Client.Post<RaceStartResponse>("/api/race/daily/start", new RawJsonBody(body), Token);
            return r.Ok ? null : Error("race start", r);
        }

        /// <param name="logJson">V5 M9j: the race voyage's closed run log (checked by the server in shadow mode); null: none.</param>
        public static async Task<(RaceEntryResponse data, string error)> SubmitRace(RaceEntryRequest entry, string logJson = null)
        {
            if (Token == null) return (null, "Sign in (Wallet button) to post race times.");
            var r = await Client.Post<RaceEntryResponse>("/api/race/daily/entry",
                new RawJsonBody(HackathonRules.WithRunLog(JsonUtility.ToJson(entry), "log", logJson)), Token);
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
                case "deposit_refunded": return string.IsNullOrEmpty(r.Message) ? "The deposit is being refunded to your wallet." : r.Message;
                case "bot_stakes_off": return "Matches against " + HackathonRules.CaptainName + " are free play: no stake.";
                case "purpose_reserved": return "That kind of payment goes through its own screen (1v1 deposit, market).";
                case "payment_used": return "That payment was already used or refunded.";
                case "wrong_sender": return "The transfer was not from your signed-in wallet.";
                case "tx_failed": return "The transaction failed on devnet.";
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
