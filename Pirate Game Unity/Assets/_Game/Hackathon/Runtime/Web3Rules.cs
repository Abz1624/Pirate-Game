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

        /// <summary>The address's page on the Solana explorer (devnet); null for no address.</summary>
        public static string ExplorerAddress(string address) =>
            string.IsNullOrEmpty(address) ? null : "https://explorer.solana.com/address/" + address + "?cluster=devnet";

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

        /// <summary>The server's default wallet name: "Captain " and four base58 characters of the address.</summary>
        /// <remarks>With the <paramref name="wallet"/> it must be that wallet's first or last four characters (so a chosen
        /// "Captain Jack" is not mistaken for the default).</remarks>
        public static bool IsDefaultName(string name, string wallet = null)
        {
            if (string.IsNullOrEmpty(name) || name.Length != 12 || !name.StartsWith("Captain ", StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(wallet) && wallet.Length >= 4)
            {
                var tail = name.Substring(8);
                return tail == wallet.Substring(0, 4) || tail == wallet.Substring(wallet.Length - 4);
            }
            for (var i = 8; i < 12; i++)
                if (!LooksLikeAddressChar(name[i])) return false;
            return true;
        }

        static bool LooksLikeAddressChar(char c) =>
            c >= '1' && c <= '9' || c >= 'A' && c <= 'H' || c >= 'J' && c <= 'N' || c >= 'P' && c <= 'Z' || c >= 'a' && c <= 'k' || c >= 'm' && c <= 'z';

        /// <summary>The server's name rules (3 to 16 letters, digits, spaces, _ or -): the trimmed name, or null with
        /// <paramref name="error"/> set. The server also refuses rude names.</summary>
        public static string CleanName(string raw, out string error)
        {
            var name = (raw ?? string.Empty).Trim();
            error = null;
            if (name.Length < 3 || name.Length > 16) { error = "Names are 3 to 16 characters."; return null; }
            foreach (var c in name)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == ' ' || c == '_' || c == '-'))
                { error = "Use letters, digits, spaces, _ or - only."; return null; }
            return name;
        }
    }
}
