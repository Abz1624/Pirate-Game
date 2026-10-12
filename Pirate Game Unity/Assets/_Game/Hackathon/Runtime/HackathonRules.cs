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
        /// no SOL was taken: a refused transfer is being refunded; an unconfirmed one stays in the lobby (Check deposit).</summary>
        public static string DepositFailureLine(bool host, string stake, bool sent, bool refunded, string reason)
        {
            var who = host ? "Match cancelled" : "Left the match";
            reason = string.IsNullOrEmpty(reason) ? null : reason.Trim().TrimEnd('.');
            if (refunded)
                return $"{who}: the {stake} deposit could not be used" + (reason != null ? $" ({reason})" : string.Empty) + ". Your refund is on the way to your wallet.";
            if (sent)
                return $"Your {stake} transfer was sent but is not confirmed yet" + (reason != null ? $" ({reason})" : string.Empty) + ". Stay here and press Check deposit; you will not pay twice.";
            return $"{who}: the {stake} deposit was not made" + (reason != null ? $" ({reason})" : string.Empty) + ". No SOL was taken.";
        }
    }
}
