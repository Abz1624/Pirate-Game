using System;
using System.Globalization;

namespace RoguePirates.Hackathon
{
    /// <summary>Hackathon (12 Oct): pure helpers for the wager, AI captain, daily race and rewards UI (tested in EditMode).</summary>
    public static class HackathonRules
    {
        /// <summary>The AI captain (the server's island defender has the same name).</summary>
        public const string CaptainName = "Captain Marrow";
        public const string CaptainTitle = "Terror of the Saltwind";

        /// <summary>The stakes the 1v1 menu offers, in lamports (0: no stake).</summary>
        public static readonly long[] Stakes = { 0L, 10_000_000L, 50_000_000L, 100_000_000L };

        public static string ExplorerTx(string signature) =>
            string.IsNullOrEmpty(signature) ? null : "https://explorer.solana.com/tx/" + signature + "?cluster=devnet";

        public static double LamportsToSol(long lamports) => lamports / (double)Web3Rules.LamportsPerSol;

        public static string Sol(long lamports) =>
            LamportsToSol(lamports).ToString("0.###", CultureInfo.InvariantCulture) + " SOL";

        public static string StakeLabel(long lamports) => lamports <= 0 ? "No stake" : Sol(lamports);

        /// <summary>
        /// V5 M9j: a request body (a JsonUtility object) with the run's own log JSON added under <paramref name="field"/>,
        /// so the server can check the result against it (raids: "runLog", the daily race: "log"). No log, or one that is
        /// not a JSON object, leaves the body as it is (the server then has no log to check: shadow mode).
        /// </summary>
        public static string WithRunLog(string bodyJson, string field, string runLogJson)
        {
            if (string.IsNullOrEmpty(bodyJson) || string.IsNullOrEmpty(field) || string.IsNullOrEmpty(runLogJson)) return bodyJson;
            var log = runLogJson.Trim();
            if (log.Length < 2 || log[0] != '{' || log[log.Length - 1] != '}') return bodyJson;
            var body = bodyJson.TrimEnd();
            if (body.Length < 2 || body[body.Length - 1] != '}') return bodyJson;
            var inner = body.Substring(1, body.Length - 2).Trim();
            return body.Substring(0, body.Length - 1) + (inner.Length > 0 ? "," : string.Empty) + "\"" + field + "\":" + log + "}";
        }

        /// <summary>"1:58.4" (minutes, seconds and tenths).</summary>
        public static string RaceTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            var tenths = (long)Math.Floor(seconds * 10.0 + 1e-6);
            var m = tenths / 600;
            var s = (tenths % 600) / 10;
            var t = tenths % 10;
            return m.ToString(CultureInfo.InvariantCulture) + ":" + s.ToString("00", CultureInfo.InvariantCulture) + "." + t.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Short form of a transaction signature for a label ("4D8Z...gufgsZ").</summary>
        public static string ShortSig(string sig) => string.IsNullOrEmpty(sig) ? string.Empty : Web3Rules.ShortAddress(sig, 6);

        /// <summary>The seat line of a wager ("deposited", "not deposited", "house matches").</summary>
        public static string SeatFunding(WagerSeat seat, bool house)
        {
            if (house) return "house matches";
            if (seat == null || string.IsNullOrEmpty(seat.playerId)) return "no player yet";
            return seat.deposited ? "deposited" : "not deposited";
        }

        /// <summary>The player's view of a wager's outcome (null while it is still open).</summary>
        public static string ResultLine(WagerView w, string myPlayerId)
        {
            if (w == null || !w.Exists) return null;
            var pot = Sol(w.potLamports);
            switch (w.result)
            {
                case "paid_winner":
                    return !string.IsNullOrEmpty(myPlayerId) && w.winnerPlayerId == myPlayerId
                        ? "You won the pot: " + pot + " paid to your wallet."
                        : "Your opponent won the pot (" + pot + ").";
                case "house_won": return CaptainName + " won. The house keeps your " + Sol(w.stakeLamports) + " stake.";
                case "refunded":
                    return string.IsNullOrEmpty(w.matchId)
                        ? "The lobby closed before the match: your " + Sol(w.stakeLamports) + " stake was refunded."
                        : "Draw or no result: every stake was refunded.";
                case "cancelled": return "The wager was cancelled before the match; deposits were refunded.";
            }
            if (w.status == "refunded") return "Every stake was refunded.";
            if (w.status == "cancelled") return "The wager was cancelled.";
            return null;
        }

        /// <summary>The payout signature to link: the winner's payout, else the player's refund.</summary>
        public static string PayoutSignature(WagerView w)
        {
            if (w == null) return null;
            if (!string.IsNullOrEmpty(w.payoutSignature)) return w.payoutSignature;
            if (w.payouts == null) return null;
            foreach (var p in w.payouts)
                if (p != null && !string.IsNullOrEmpty(p.signature)) return p.signature;
            return null;
        }

        /// <summary>A payout that is still on its way (pending or not yet confirmed).</summary>
        public static bool PayoutPending(WagerView w)
        {
            if (w?.payouts == null) return false;
            foreach (var p in w.payouts)
                if (p != null && (p.status == "pending" || p.status == "unconfirmed" || string.IsNullOrEmpty(p.signature))) return true;
            return false;
        }

        /// <summary>The race goal reached: the kills and the target (a target of 0 counts as 100).</summary>
        public static bool RaceFinished(int kills, int target) => kills >= (target > 0 ? target : 100);

        /// <summary>The hub message after a bundled deposit did not go through. Once a transfer was sent it never says that
        /// no SOL was taken: a refused transfer is being refunded; an unconfirmed one stays in the lobby (Check deposit); one
        /// the server refused for good says what happens to the SOL (<paramref name="finalLine"/>).</summary>
        public static string DepositFailureLine(bool host, string stake, bool sent, bool refunded, string reason, string finalLine = null)
        {
            var who = host ? "Match cancelled" : "Left the match";
            reason = string.IsNullOrEmpty(reason) ? null : reason.Trim().TrimEnd('.');
            if (refunded)
                return $"{who}: the {stake} deposit could not be used" + (reason != null ? $" ({reason})" : string.Empty) + ". Your refund is on the way to your wallet.";
            if (sent && !string.IsNullOrEmpty(finalLine)) return $"{who}: {finalLine}";
            if (sent)
                return $"Your {stake} transfer was sent but is not confirmed yet" + (reason != null ? $" ({reason})" : string.Empty) + ". Stay here and press Check deposit; you will not pay twice.";
            return $"{who}: the {stake} deposit was not made" + (reason != null ? $" ({reason})" : string.Empty) + ". No SOL was taken.";
        }

        // ---------------------------------------------------------------- payments: the server's answer to a signature

        /// <summary>
        /// 13 Oct (review): how a payment's server answer is handled.
        /// <list type="bullet">
        /// <item>Accepted: done, the signature is forgotten;</item>
        /// <item>Wait: not seen on devnet yet or the server is unreachable: ask again in a moment (same signature);</item>
        /// <item>Hold: an answer that may change later (signed out, wagers off, an unknown refusal): keep the signature and
        /// let the player ask again (Check deposit, Finish purchase, Finish listing); never pay again;</item>
        /// <item>Final: the server decided for good (refunded, refused, the listing or seat is gone): forget the signature
        /// and say what happens to the SOL.</item>
        /// </list>
        /// </summary>
        public enum PaymentVerdict { Accepted, Wait, Hold, Final }

        /// <summary>The refusals that are final for a signed payment (the same signature can never succeed).</summary>
        public static readonly string[] FinalCodes =
        {
            "deposit_refunded", "purchase_refunded", "payment_refunded", "refunded", "memo_mismatch", "amount_too_low",
            "signature_used", "invalid_signature", "wager_closed", "not_in_lobby", "already_started", "no_wager",
            "listing_not_active", "listing_not_found", "own_listing", "already_listed", "not_your_island", "nft_not_minted",
            "invalid_price", "invalid_purpose", "purpose_reserved", "reserved_purpose", "wrong_purpose", "expired",
            // 13 Oct server hotfix (66ba84ed): a recorded payment re-checked and refused, a failed or foreign transaction,
            // no stakes on bot matches.
            "payment_used", "wrong_sender", "wrong_recipient", "tx_failed", "bot_stakes_off", "raid_stakes_off",
            "lobby_not_found", "not_your_listing", "nft_not_in_escrow",
        };

        /// <summary>The verdict for a server answer. <paramref name="nftWait"/>: a listing, where the escrow may not see the
        /// NFT yet (nft_not_received) right after the transfer.</summary>
        public static PaymentVerdict Verdict(RoguePirates.V4.Online.ApiOutcome outcome, long status, string code, bool nftWait = false)
        {
            switch (outcome)
            {
                case RoguePirates.V4.Online.ApiOutcome.Ok: return PaymentVerdict.Accepted;
                case RoguePirates.V4.Online.ApiOutcome.Offline:
                case RoguePirates.V4.Online.ApiOutcome.ServerError: return PaymentVerdict.Wait;
                case RoguePirates.V4.Online.ApiOutcome.NotConfigured: return PaymentVerdict.Hold;
            }
            if (code == "already_deposited") return PaymentVerdict.Accepted;
            if (code == "tx_not_confirmed" || (nftWait && code == "nft_not_received")) return PaymentVerdict.Wait;
            if (!string.IsNullOrEmpty(code))
            {
                if (Array.IndexOf(FinalCodes, code) >= 0 || code.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0) return PaymentVerdict.Final;
            }
            return PaymentVerdict.Hold;
        }

        /// <summary>A transfer that devnet still has not seen this long after signing can never land (its blockhash expired
        /// after about 90 s): it was not taken.</summary>
        public const double ExpireSeconds = 300;

        public static bool Expired(string code, double ageSeconds) => code == "tx_not_confirmed" && ageSeconds > ExpireSeconds;

        /// <summary>
        /// The line for a signed payment the server refused for good: what, the server's reason, and what happens to the
        /// SOL (or the NFT). <paramref name="what"/>: "deposit", "payment", "NFT transfer".
        /// </summary>
        public static string FinalLine(string what, string amount, string code, string serverMessage)
        {
            var reason = string.IsNullOrEmpty(serverMessage) ? FriendlyCode(code) : serverMessage.Trim().TrimEnd('.');
            var head = string.IsNullOrEmpty(amount) ? $"The {what} was not accepted" : $"The {amount} {what} was not accepted";
            if (code == "expired")
                return $"The {what} never reached devnet (it expired), so nothing was taken. You can try again.";
            var line = head + (string.IsNullOrEmpty(reason) ? "." : $": {reason}.");
            var refundSaid = (serverMessage ?? string.Empty).IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0;
            if (refundSaid) return line;
            if (what == "NFT transfer")
                return line + " The NFT is in the market escrow: ask the organisers to return it (keep the transaction link).";
            if (code != null && code.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0)
                return line + " Your SOL is refunded to your wallet.";
            switch (code)
            {
                case "tx_failed":
                    return line + " The transaction failed on devnet, so no SOL moved (only the network fee).";
                case "payment_used":
                    return line + " That payment was already used or refunded: check the transaction link.";
                case "listing_not_active":
                case "listing_not_found":
                    return line + " The server refunds a payment for a listing that is no longer for sale to your wallet.";
                case "memo_mismatch":
                case "amount_too_low":
                case "signature_used":
                case "invalid_signature":
                    return line + " If SOL left your wallet it is in the game's escrow and is not refunded automatically: keep the transaction link and ask the organisers.";
            }
            return line + " If SOL left your wallet the server refunds it; check the transaction link.";
        }

        static string FriendlyCode(string code)
        {
            switch (code)
            {
                case "memo_mismatch": return "the transfer was not for this (memo mismatch)";
                case "amount_too_low": return "the transfer was smaller than the price";
                case "signature_used": return "that transaction was already used";
                case "listing_not_active": return "the listing is no longer for sale";
                case "wager_closed": return "the wager is closed";
                case "not_in_lobby": return "you are no longer seated in this lobby";
                case "already_started": return "the match already started";
                case "tx_failed": return "the transaction failed on devnet";
                case "wrong_sender": return "the transfer was not from your signed-in wallet";
                case "payment_used": return "the payment was already used or refunded";
                default: return string.IsNullOrEmpty(code) ? null : code.Replace('_', ' ');
            }
        }
    }
}
