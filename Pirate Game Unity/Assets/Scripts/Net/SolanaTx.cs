using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PirateGame.Net
{
    public static class SolanaTx
    {
        public static IRpcClient Client(string rpcUrl) => ClientFactory.GetClient(rpcUrl);

        public static async Task<string> SendAsync(
            IRpcClient rpc,
            Account signer,
            TransactionInstruction instruction,
            uint? computeUnitLimit = null)
        {
            var blockhash = await FetchBlockhashAsync(rpc);
            var builder = new TransactionBuilder()
                .SetRecentBlockHash(blockhash)
                .SetFeePayer(signer.PublicKey);
            if (computeUnitLimit.HasValue)
                builder.AddInstruction(ComputeBudgetProgram.SetComputeUnitLimit(computeUnitLimit.Value));
            builder.AddInstruction(instruction);

            var tx = builder.Build(signer);
            var result = await rpc.SendTransactionAsync(tx, false, Commitment.Confirmed);
            if (!result.WasSuccessful)
                throw new InvalidOperationException($"{result.Reason} {result.RawRpcResponse}");

            await ConfirmSignatureAsync(rpc, result.Result);
            return result.Result;
        }

        /// <summary>
        /// SendTransaction only submits; poll until confirmed/finalized (or err).
        /// On local validators, status polling can be flaky — callers that create
        /// accounts should also WaitForProgramAccountAsync.
        /// </summary>
        public static async Task ConfirmSignatureAsync(
            IRpcClient rpc,
            string signature,
            int timeoutMs = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var statuses = await rpc.GetSignatureStatusesAsync(
                    new List<string> { signature },
                    searchTransactionHistory: true);
                if (!statuses.WasSuccessful || statuses.Result?.Value == null || statuses.Result.Value.Count == 0)
                {
                    await Task.Delay(250);
                    continue;
                }

                var info = statuses.Result.Value[0];
                if (info == null)
                {
                    await Task.Delay(250);
                    continue;
                }

                if (info.Error != null)
                    throw new InvalidOperationException($"tx {signature} failed on-chain: {info.Error}");

                var conf = info.ConfirmationStatus?.ToString() ?? "";
                if (conf.IndexOf("confirmed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    conf.IndexOf("finalized", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                await Task.Delay(250);
            }

            Debug.LogWarning($"[Solana] tx {signature} confirm poll timed out — continuing");
        }

        public static async Task WaitForProgramAccountAsync(
            IRpcClient rpc,
            PublicKey pubkey,
            PublicKey ownerProgram,
            int timeoutMs = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var result = await rpc.GetAccountInfoAsync(pubkey, Commitment.Confirmed);
                var value = result.WasSuccessful ? result.Result?.Value : null;
                if (value != null && value.Owner == ownerProgram.Key)
                    return;
                await Task.Delay(250);
            }

            throw new TimeoutException(
                $"Account {pubkey} not owned by {ownerProgram} within {timeoutMs}ms");
        }

        public static async Task<byte[]> GetAccountDataAsync(IRpcClient rpc, PublicKey pubkey)
        {
            var result = await rpc.GetAccountInfoAsync(pubkey, Commitment.Confirmed);
            if (!result.WasSuccessful || result.Result?.Value?.Data == null || result.Result.Value.Data.Count == 0)
                return null;
            return Convert.FromBase64String(result.Result.Value.Data[0]);
        }

        public static async Task<bool> AccountExistsAsync(IRpcClient rpc, PublicKey pubkey)
        {
            var result = await rpc.GetAccountInfoAsync(pubkey, Commitment.Confirmed);
            return result.WasSuccessful && result.Result?.Value != null;
        }

        public static async Task AirdropAsync(IRpcClient rpc, PublicKey pubkey, ulong lamports = 1_000_000_000)
        {
            try
            {
                await rpc.RequestAirdropAsync(pubkey, lamports);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Solana] airdrop skipped: {ex.Message}");
            }
        }

        public static Account LoadPayer(BlockchainConfig config)
        {
            if (Solana.Unity.SDK.Web3.Account != null)
                return Solana.Unity.SDK.Web3.Account;

            var path = CoopParrelSync.ResolvePayerPath(config);
            if (string.IsNullOrEmpty(path))
                path = ResolveKeypairPath(config);
            return LoadPayerFromPath(path);
        }

        public static Account LoadSecondaryPayer(BlockchainConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.localClientPayerKeypairPath))
            {
                throw new FileNotFoundException(
                    "No secondary payer keypair. Set BlockchainConfig.localClientPayerKeypairPath (player2.json).");
            }

            var path = Environment.ExpandEnvironmentVariables(config.localClientPayerKeypairPath);
            return LoadPayerFromPath(path);
        }

        public static Account LoadPayerFromPath(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path ?? "");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    "No Solana payer keypair. Set BlockchainConfig.localPayerKeypairPath, " +
                    "or place id.json at ~/.config/solana/id.json, or login a Solana.Unity wallet.");
            }

            var raw = File.ReadAllText(path);
            var bytes = ParseKeypairJson(raw);
            if (bytes.Length < 64)
                throw new InvalidDataException($"Keypair at {path} is not 64 bytes");
            var pubkey = new byte[32];
            Buffer.BlockCopy(bytes, 32, pubkey, 0, 32);
            var account = new Account(bytes, pubkey);
            Debug.Log($"[Solana] Loaded payer {account.PublicKey} from {path}");
            return account;
        }

        public static Account LoadOrCreateSession(BlockchainConfig config = null, string prefsKey = null)
        {
            prefsKey ??= CoopParrelSync.SessionPrefsKey(config);
            if (PlayerPrefs.HasKey(prefsKey + "_sec") && PlayerPrefs.HasKey(prefsKey + "_pub"))
            {
                var sec = Convert.FromBase64String(PlayerPrefs.GetString(prefsKey + "_sec"));
                var pub = Convert.FromBase64String(PlayerPrefs.GetString(prefsKey + "_pub"));
                return new Account(sec, pub);
            }

            var generated = new Account();
            PlayerPrefs.SetString(prefsKey + "_sec", Convert.ToBase64String(generated.PrivateKey.KeyBytes));
            PlayerPrefs.SetString(prefsKey + "_pub", Convert.ToBase64String(generated.PublicKey.KeyBytes));
            PlayerPrefs.Save();
            Debug.Log($"[SessionKey] Created {generated.PublicKey}");
            return generated;
        }

        static async Task<string> FetchBlockhashAsync(IRpcClient rpc)
        {
            // Agave 2.x removed getRecentBlockhash — prefer getLatestBlockhash.
            try
            {
                var latest = await rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
                if (latest.WasSuccessful && latest.Result?.Value != null)
                    return latest.Result.Value.Blockhash;
                Debug.LogWarning($"[Solana] getLatestBlockhash: {latest.Reason}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Solana] getLatestBlockhash threw: {ex.Message}");
            }

            var recent = await rpc.GetRecentBlockHashAsync();
            if (recent.WasSuccessful && recent.Result?.Value != null)
                return recent.Result.Value.Blockhash;
            throw new InvalidOperationException($"blockhash failed: {recent.Reason}");
        }

        static string ResolveKeypairPath(BlockchainConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.localPayerKeypairPath))
                return Environment.ExpandEnvironmentVariables(config.localPayerKeypairPath);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var native = Path.Combine(home, ".config", "solana", "id.json");
            if (File.Exists(native)) return native;

            var user = Environment.UserName;
            foreach (var distro in new[] { "Ubuntu", "Ubuntu-26.04", "Ubuntu-24.04", "Ubuntu-22.04" })
            {
                // Windows Unity → WSL Solana CLI keypair
                var wsl = $@"\\wsl$\{distro}\home\{user}\.config\solana\id.json";
                if (File.Exists(wsl)) return wsl;
                // Common case: Windows username differs from Linux login
                wsl = $@"\\wsl$\{distro}\home\shara\.config\solana\id.json";
                if (File.Exists(wsl)) return wsl;
            }

            return native;
        }

        static byte[] ParseKeypairJson(string json)
        {
            json = json.Trim();
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start < 0 || end <= start)
                throw new InvalidDataException("Expected Solana keypair JSON array");
            var inner = json.Substring(start + 1, end - start - 1);
            var parts = inner.Split(',');
            var bytes = new byte[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                bytes[i] = byte.Parse(parts[i].Trim());
            return bytes;
        }
    }
}
