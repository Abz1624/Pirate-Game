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

        /// <summary>
        /// 13 Oct (review): a signed transaction's id, known before it is sent: the fee payer's signature (the first one;
        /// Solana's transaction id is always the first signature, and the fee payer signs first), base58. Null when the
        /// wallet returned no signature.
        /// </summary>
        public static string TransactionId(System.Collections.Generic.IList<Solana.Unity.Rpc.Models.SignaturePubKeyPair> signatures, string feePayer)
        {
            if (signatures == null || signatures.Count == 0) return null;
            byte[] sig = null;
            foreach (var s in signatures)
                if (s?.PublicKey != null && s.PublicKey.Key == feePayer) { sig = s.Signature; break; }
            if (sig == null) sig = signatures[0]?.Signature;
            return SignatureBase58(sig);
        }

        /// <summary>A 64-byte signature in base58; null for a missing or all-zero (unsigned) one.</summary>
        public static string SignatureBase58(byte[] sig)
        {
            if (sig == null || sig.Length != 64) return null;
            var any = false;
            foreach (var b in sig) if (b != 0) { any = true; break; }
            return any ? Solana.Unity.Wallet.Utilities.Encoders.Base58.EncodeData(sig) : null;
        }

        /// <summary>
        /// A send that the node refused in its preflight (JSON-RPC -32002 "Transaction simulation failed", including
        /// "Blockhash not found" and insufficient funds; -32003 signature verification failed): the node did not broadcast
        /// it, so it can never land. Anything else (a timeout, no reply, an unreadable body, another code) may have been
        /// broadcast: the transfer is "sent, unknown".
        /// </summary>
        public static bool DefinitelyNotBroadcast(int serverErrorCode, string rawResponse)
        {
            if (serverErrorCode == -32002 || serverErrorCode == -32003) return true;
            if (string.IsNullOrEmpty(rawResponse)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(rawResponse, @"""code""\s*:\s*-3200[23]\b");
        }

        // ---------------------------------------------------------------- wallets that sign and send themselves

        /// <summary>
        /// V5 M9i: a Mobile Wallet Adapter wallet without <c>sign_transactions</c> (optional, and deprecated, in MWA 2.0; only
        /// <c>sign_and_send_transactions</c> is mandatory) answers the game's sign request with JSON-RPC -32601 "method not
        /// found". Nothing was signed, so the game can ask the wallet to sign and send instead.
        /// </summary>
        public static bool SignOnlyUnsupported(Exception e)
        {
            for (var x = e; x != null; x = x.InnerException)
                if (x is Solana.Unity.SDK.MwaRpcException m && m.Code == Solana.Unity.SDK.MwaErrorCodes.MethodNotFound) return true;
            return false;
        }

        /// <summary>What a wallet's own sign-and-send did with the SOL.</summary>
        public enum WalletSend
        {
            /// <summary>Signed and sent: the signature is known (persist it at once).</summary>
            Sent,
            /// <summary>Nothing left the wallet (declined, refused, never signed or never submitted).</summary>
            NotSent,
            /// <summary>The wallet may have sent it but gave no signature (a dropped session, an unmapped error).</summary>
            Unknown,
        }

        /// <summary>
        /// V5 M9i: reads the SDK's typed result of <c>sign_and_send_transactions</c> for one transaction. A success or a partial
        /// submit that names this transaction's signature is sent; the wallet's definite refusals (declined, unsupported,
        /// unauthorized, invalid or too many payloads, signed but not submitted) are not; anything else is unknown, since the
        /// wallet may have broadcast it.
        /// </summary>
        public static WalletSend ReadSignAndSend(SignAndSendTxResult result, out string signature, out string reason)
        {
            signature = null;
            reason = null;
            switch (result)
            {
                case SignAndSendTxResult.Success s:
                    signature = SignatureBase58(s.Signatures != null && s.Signatures.Length > 0 ? s.Signatures[0] : null);
                    if (signature != null) return WalletSend.Sent;
                    reason = "the wallet returned no signature";
                    return WalletSend.Unknown;
                case SignAndSendTxResult.NotSubmitted ns:
                    signature = SignatureBase58(ns.PartialSignatures != null && ns.PartialSignatures.Length > 0 ? ns.PartialSignatures[0] : null);
                    if (signature != null) return WalletSend.Sent;
                    reason = "the wallet signed it but could not send it";
                    return WalletSend.NotSent;
                case SignAndSendTxResult.UserDeclined _:
                    reason = "Cancelled in the wallet.";
                    return WalletSend.NotSent;
                case SignAndSendTxResult.NotSupported _:
                    reason = "this wallet cannot sign transactions for the game";
                    return WalletSend.NotSent;
                case SignAndSendTxResult.Unauthorized _:
                    reason = "the wallet session expired; reconnect your wallet";
                    return WalletSend.NotSent;
                case SignAndSendTxResult.InvalidPayloads _:
                case SignAndSendTxResult.TooManyPayloads _:
                    reason = "the wallet refused the transaction";
                    return WalletSend.NotSent;
                case SignAndSendTxResult.Failed f:
                    reason = string.IsNullOrEmpty(f.Message) ? "the wallet gave no answer" : f.Message;
                    return WalletSend.Unknown;
                default:
                    reason = "the wallet gave no answer";
                    return WalletSend.Unknown;
            }
        }
    }
}
