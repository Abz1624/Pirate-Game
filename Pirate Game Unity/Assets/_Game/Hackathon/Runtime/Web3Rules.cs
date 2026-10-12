using System;
using System.Globalization;

namespace RoguePirates.Hackathon
{
    /// <summary>Hackathon (12 Oct): small pure helpers for the wallet UI and transfers (tested in EditMode).</summary>
    public static class Web3Rules
    {
        public const ulong LamportsPerSol = 1_000_000_000UL;

        /// <summary>"7xKX...9fQe" (the address itself when short).</summary>
        public static string ShortAddress(string address, int keep = 4)
        {
            if (string.IsNullOrEmpty(address)) return string.Empty;
            return address.Length <= keep * 2 + 3 ? address : address.Substring(0, keep) + "..." + address.Substring(address.Length - keep);
        }

        /// <summary>SOL to lamports, rounded to the nearest lamport; negative or NaN is 0.</summary>
        public static ulong SolToLamports(double sol)
        {
            if (double.IsNaN(sol) || sol <= 0) return 0;
            return (ulong)Math.Round(sol * LamportsPerSol);
        }

        public static string FormatSol(double? sol) =>
            sol.HasValue ? sol.Value.ToString("0.####", CultureInfo.InvariantCulture) + " SOL" : "- SOL";

        /// <summary>The ed25519 signature from a wallet's signed message: the trailing 64 bytes (some Mobile Wallet Adapter
        /// wallets return message || signature, browser wallets the bare signature).</summary>
        public static byte[] DetachedSignature(byte[] signed)
        {
            if (signed == null || signed.Length < 64) return null;
            if (signed.Length == 64) return signed;
            var sig = new byte[64];
            Array.Copy(signed, signed.Length - 64, sig, 0, 64);
            return sig;
        }

        /// <summary>A cheap shape check for a base58 Solana address (32-44 base58 characters).</summary>
        public static bool LooksLikeAddress(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 32 || s.Length > 44) return false;
            foreach (var c in s)
                if (!(c >= '1' && c <= '9' || c >= 'A' && c <= 'H' || c >= 'J' && c <= 'N' || c >= 'P' && c <= 'Z' || c >= 'a' && c <= 'k' || c >= 'm' && c <= 'z')) return false;
            return true;
        }

        public static bool LooksLikeEmail(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 254) return false;
            var at = s.IndexOf('@');
            return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith(".") && s.IndexOf(' ') < 0;
        }
    }
}
