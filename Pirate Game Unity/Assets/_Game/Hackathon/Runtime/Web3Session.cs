using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RoguePirates.V4.Online;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct, owner: "web3 mode only; the wallet address is the primary profile key"). The wallet and its
    /// API session, on devnet:
    /// <list type="bullet">
    /// <item>connect: WebGL the browser wallets (Phantom, Solflare) through the SDK's wallet adapter; Android Mobile Wallet
    /// Adapter; the Editor (and development builds) the SDK's in-game test wallet;</item>
    /// <item>sign in with Solana: nonce from the API, the wallet signs the message, the API verifies and returns the same
    /// session shape as a guest profile. It is stored in V4's <see cref="ProfileStore"/> as the active profile, so the
    /// leaderboard, 1v1 lobbies and run upload use the wallet player unchanged;</item>
    /// <item>SOL balance, optional email, SOL transfers (wagers, purchases) and message signing.</item>
    /// </list>
    /// Every public call returns a player-facing error (or null) instead of throwing.
    /// </summary>
    public sealed class Web3Session
    {
        public enum WalletKind { None, WalletAdapter, TestWallet }

        public const string WalletProfilesKey = "rp.hackathon.walletProfiles";
        const string TestWalletPassword = "rogue-pirates-devnet-test"; // the Editor test wallet only (devnet, PlayerPrefs)

        static Web3Session _instance;
        public static Web3Session Instance => _instance ?? (_instance = new Web3Session());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [Serializable] sealed class WalletProfile { public string wallet; public string localId; public string email; }
        [Serializable] sealed class Saved { public List<WalletProfile> entries = new List<WalletProfile>(); }

        Saved _saved;

        /// <summary>The wallet, profile, balance or busy state changed (the UI refreshes).</summary>
        public event Action Changed;

        public WalletBase Wallet { get; private set; }
        public WalletKind Kind { get; private set; }
        public bool Busy { get; private set; }
        /// <summary>What the session is doing ("Waiting for the wallet...").</summary>
        public string Status { get; private set; }
        public string LastError { get; private set; }
        public double? Balance { get; private set; }
        public string LastTransferSignature { get; private set; }

        /// <summary>The connected wallet's address, else the address of the signed-in wallet profile (a session kept from
        /// an earlier launch: online features work, signing needs a reconnect).</summary>
        public string Address
        {
            get
            {
                var key = Wallet?.Account?.PublicKey;
                if (key != null) return key.Key;
                return ActiveEntry()?.wallet;
            }
        }

        public bool WalletConnected => Wallet?.Account != null;

        /// <summary>The active V4 profile is a wallet profile with an API session.</summary>
        public bool SignedIn
        {
            get
            {
                var e = ActiveEntry();
                var p = OnlineService.Active;
                return e != null && p != null && p.Registered && (!WalletConnected || e.wallet == Wallet.Account.PublicKey.Key);
            }
        }

        public LocalProfile Profile => SignedIn ? OnlineService.Active : null;
        public string Email => ActiveEntry()?.email;
        public string Token => Profile?.token;
        public static bool TestWalletAvailable => Application.isEditor || (Debug.isDebugBuild && HackathonSettings.Load().testWalletInDevelopmentBuilds);

        // ---------------------------------------------------------------- connect / sign in

        /// <summary>Connects a wallet (the platform's, or the test wallet) and signs in with it. Null on success.</summary>
        public async Task<string> Connect(bool testWallet = false)
        {
            if (Busy) return "Busy, please wait.";
            Begin(testWallet ? "Opening the test wallet..." : "Waiting for the wallet...");
            try
            {
                var error = await ConnectWallet(testWallet);
                if (error != null) return Fail(error);
                var signInError = await SignInNow();
                if (signInError != null) return Fail(signInError);
                _ = RefreshBalance();
                return Done();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session.Connect: " + e);
                return Fail(Readable(e));
            }
        }

        /// <summary>Signs in again with the connected wallet (a new nonce and signature).</summary>
        public async Task<string> SignIn()
        {
            if (Busy) return "Busy, please wait.";
            if (!WalletConnected) return "Connect a wallet first.";
            Begin("Signing in...");
            try
            {
                var error = await SignInNow();
                return error != null ? Fail(error) : Done();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session.SignIn: " + e);
                return Fail(Readable(e));
            }
        }

        async Task<string> ConnectWallet(bool testWallet)
        {
            var s = HackathonSettings.Load();
            var rpc = string.IsNullOrWhiteSpace(s.customRpc) ? null : s.customRpc.Trim();
            var ws = string.IsNullOrWhiteSpace(s.customStreamingRpc) ? null : s.customStreamingRpc.Trim();
            DropWallet();
            if (testWallet)
            {
                if (!TestWalletAvailable) return "The test wallet is only in the Editor and development builds.";
                EnsureHost();
                var w = new InGameWallet(RpcCluster.DevNet, rpc, ws);
                // Editor: RP_TEST_WALLET_SECRET (a base58 secret key or a mnemonic, devnet only, never in the repo) loads a
                // funded wallet instead of the generated one.
                var secret = Application.isEditor ? Environment.GetEnvironmentVariable("RP_TEST_WALLET_SECRET") : null;
                Account account = null;
                if (!string.IsNullOrWhiteSpace(secret)) account = await w.CreateAccount(secret.Trim(), TestWalletPassword);
                if (account == null) account = await w.Login(TestWalletPassword);
                if (account == null) account = await w.CreateAccount(null, TestWalletPassword);
                if (account == null) return "Could not open the test wallet.";
                Wallet = w;
                Kind = WalletKind.TestWallet;
                return null;
            }
#if UNITY_EDITOR
            return "Browser and phone wallets work in WebGL and Android builds. In the Editor use the test wallet.";
#else
            if (Application.platform != RuntimePlatform.WebGLPlayer && Application.platform != RuntimePlatform.Android)
                return "Wallets are supported in the WebGL and Android builds.";
            EnsureHost();
            var options = new SolanaWalletAdapterOptions
            {
                solanaMobileWalletAdapterOptions = new SolanaMobileWalletAdapterOptions
                {
                    name = s.appName, identityUri = s.appUri, iconUri = s.appIconPath, keepConnectionAlive = true,
                },
                solanaWalletAdapterWebGLOptions = new SolanaWalletAdapterWebGLOptions
                {
                    walletAdapterUIPrefab = s.walletAdapterUIPrefab, walletAdapterButtonPrefab = s.walletAdapterButtonPrefab,
                },
                phantomWalletOptions = new PhantomWalletOptions(),
            };
            var adapter = new SolanaWalletAdapter(options, RpcCluster.DevNet, rpc, ws);
            Web3Host.Instance.RaiseWalletChooser();
            Account acc;
            try { acc = await adapter.Login(); }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session wallet login: " + e.Message);
                return Application.platform == RuntimePlatform.WebGLPlayer
                    ? "The wallet did not connect (" + e.Message + "). Install Phantom or Solflare, set it to Devnet, and try again."
                    : "The wallet did not connect (" + e.Message + "). Install a Solana wallet app (Phantom, Solflare) and try again.";
            }
            if (acc == null) return "The wallet did not connect (cancelled?).";
            Wallet = adapter;
            Kind = WalletKind.WalletAdapter;
            return null;
#endif
        }

        async Task<string> SignInNow()
        {
            var services = OnlineService.Client;
            if (!services.Configured) return "No game server configured (add ?api=https://... to the page URL).";
            var address = Wallet.Account.PublicKey.Key;
            Raise("Asking the server for a sign-in message...");
            var nonce = await services.Post<WalletNonceResponse>("/api/auth/wallet/nonce", new WalletNonceRequest { wallet = address });
            if (!nonce.Ok || nonce.Data == null || string.IsNullOrEmpty(nonce.Data.message)) return ApiError("sign-in message", nonce);

            Raise("Approve the sign-in message in your wallet...");
            var signed = await Wallet.SignMessage(Encoding.UTF8.GetBytes(nonce.Data.message));
            var sig = Web3Rules.DetachedSignature(signed);
            if (sig == null) return "The wallet did not sign the message.";

            Raise("Checking the signature...");
            var verify = await services.Post<WalletVerifyResponse>("/api/auth/wallet/verify", new WalletVerifyRequest
            {
                wallet = address, signature = Encoders.Base58.EncodeData(sig), message = nonce.Data.message, name = string.Empty,
            });
            if (!verify.Ok || verify.Data == null || string.IsNullOrEmpty(verify.Data.token)) return ApiError("sign-in", verify);
            StoreProfile(address, verify.Data);
            // A first sign-in (or a profile still on the default "Captain xxxx") asks for a captain name once.
            NamePromptDue = verify.Data.isNew || Web3Rules.IsDefaultName(verify.Data.name, address);
            // Health check and a /me read through V4's online service (rating, and the chip goes Online).
            await OnlineService.EnsureConnected();
            await OnlineService.SyncActive();
            return null;
        }

        /// <summary>The wallet's session becomes the active V4 profile (one local profile per wallet on this device).</summary>
        void StoreProfile(string address, WalletVerifyResponse r)
        {
            var store = OnlineService.Profiles;
            var entry = Load().entries.Find(e => e.wallet == address);
            LocalProfile p = entry != null ? store.Find(entry.localId) : null;
            if (p == null)
            {
                p = store.Add(r.name);
                if (entry == null) { entry = new WalletProfile { wallet = address }; _saved.entries.Add(entry); }
                entry.localId = p.localId;
            }
            entry.email = r.email;
            SaveEntries();
            store.SetActive(p.localId);
            store.Apply(p.localId, r.playerId, r.token, r.name, r.tag);
            store.Rename(p.localId, r.name, true); // not dirty: the server's name stands
        }

        /// <summary>Disconnects the wallet and forgets its session on this device (the server keeps the profile).</summary>
        public void Disconnect()
        {
            var entry = ActiveEntry();
            if (entry != null)
            {
                OnlineService.Forget(entry.localId);
                Load().entries.Remove(entry);
                SaveEntries();
            }
            DropWallet();
            Balance = null;
            LastError = null;
            Status = null;
            Raise(null);
        }

        void DropWallet()
        {
            var w = Wallet;
            Wallet = null;
            Kind = WalletKind.None;
            if (w == null) return;
            try { w.Logout(); }
            catch (Exception e) { Debug.LogWarning("Web3Session.Logout: " + e.Message); }
        }

        // ---------------------------------------------------------------- balance, email

        /// <summary>The address's devnet SOL balance (works for a kept session without a connected wallet).</summary>
        public async Task<double?> RefreshBalance()
        {
            var address = Address;
            if (string.IsNullOrEmpty(address)) return null;
            try
            {
                var w = Wallet ?? ReadOnlyWallet();
                Balance = await w.GetBalance(new PublicKey(address), Commitment.Confirmed);
                Raise(Status);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session.RefreshBalance: " + e.Message);
            }
            return Balance;
        }

        WalletBase _readOnly;
        WalletBase ReadOnlyWallet()
        {
            if (_readOnly != null) return _readOnly;
            var s = HackathonSettings.Load();
            _readOnly = new InGameWallet(RpcCluster.DevNet, string.IsNullOrWhiteSpace(s.customRpc) ? null : s.customRpc.Trim(),
                string.IsNullOrWhiteSpace(s.customStreamingRpc) ? null : s.customStreamingRpc.Trim());
            return _readOnly;
        }

        /// <summary>True after a sign-in that should ask for a captain name (a new wallet, or the default name). The menu shows
        /// the prompt once and clears it (<see cref="DismissNamePrompt"/>), whether the player picks a name or skips.</summary>
        public bool NamePromptDue { get; private set; }

        public void DismissNamePrompt() => NamePromptDue = false;

        /// <summary>Renames the signed-in profile (PATCH /api/profiles/me, the server's name rules). Null on success.</summary>
        public async Task<string> SetName(string rawName)
        {
            var name = Web3Rules.CleanName(rawName, out var invalid);
            if (name == null) return invalid;
            var p = Profile;
            if (p == null) return "Sign in with a wallet first.";
            var r = await OnlineService.Client.Patch<ProfileNameResponse>("/api/profiles/me", new ProfileNameRequest { name = name }, p.token);
            if (!r.Ok)
            {
                if (r.ErrorCode == "name_not_allowed") return "That name is not allowed. Please pick a different one.";
                if (r.ErrorCode == "invalid_name") return "Names are 3 to 16 characters: letters, digits, spaces, _ or -.";
                return ApiError("name", r);
            }
            var saved = r.Data != null && !string.IsNullOrEmpty(r.Data.name) ? r.Data.name : name;
            OnlineService.Profiles.Rename(p.localId, saved, true); // not dirty: the server has it
            NamePromptDue = false;
            Raise(Status);
            return null;
        }

        /// <summary>Sets (or clears, with an empty value) the profile's contact email. Null on success.</summary>
        public async Task<string> SetEmail(string email)
        {
            email = (email ?? string.Empty).Trim();
            if (email.Length > 0 && !Web3Rules.LooksLikeEmail(email)) return "That does not look like an email address.";
            var p = Profile;
            if (p == null) return "Sign in with a wallet first.";
            var r = await OnlineService.Client.Send<EmailResponse>("PUT", "/api/profile/email", new EmailRequest { email = email }, p.token, 0);
            if (!r.Ok) return ApiError("email", r);
            var entry = ActiveEntry();
            if (entry != null) { entry.email = r.Data != null ? r.Data.email : email; SaveEntries(); }
            Raise(Status);
            return null;
        }

        // ---------------------------------------------------------------- signing and transfers

        /// <summary>Signs arbitrary bytes with the connected wallet; returns the 64-byte ed25519 signature.</summary>
        public async Task<(byte[] signature, string error)> SignMessage(byte[] message)
        {
            if (!WalletConnected) return (null, "Connect a wallet first.");
            try
            {
                var sig = Web3Rules.DetachedSignature(await Wallet.SignMessage(message));
                return sig == null ? (null, "The wallet did not sign the message.") : (sig, null);
            }
            catch (Exception e) { return (null, Readable(e)); }
        }

        /// <summary>Signs a UTF-8 text; returns the signature in base58 (the API's format).</summary>
        public async Task<(string signatureBase58, string error)> SignText(string text)
        {
            var (sig, error) = await SignMessage(Encoding.UTF8.GetBytes(text ?? string.Empty));
            return (sig != null ? Encoders.Base58.EncodeData(sig) : null, error);
        }

        /// <summary>Builds, signs (the wallet asks the player) and sends a SOL transfer on devnet, with an optional memo (the
        /// server matches deposits by it). Returns the transaction signature.</summary>
        public async Task<(string signature, string error)> TransferSol(string toAddress, double sol, string memo = null)
        {
            if (!WalletConnected) return (null, "Connect a wallet first.");
            if (!Web3Rules.LooksLikeAddress(toAddress)) return (null, "Not a Solana address.");
            var lamports = Web3Rules.SolToLamports(sol);
            if (lamports == 0) return (null, "Nothing to send.");
            if (Busy) return (null, "Busy, please wait.");
            Begin("Approve the transfer in your wallet...");
            try
            {
                var from = Wallet.Account.PublicKey;
                var tx = new Transaction
                {
                    RecentBlockHash = await Wallet.GetBlockHash(Commitment.Confirmed, false),
                    FeePayer = from,
                    Instructions = new List<TransactionInstruction> { SystemProgram.Transfer(from, new PublicKey(toAddress), lamports) },
                    Signatures = new List<SignaturePubKeyPair>(),
                };
                if (!string.IsNullOrEmpty(memo)) tx.Instructions.Add(MemoProgram.NewMemo(from, memo));
                var result = await Wallet.SignAndSendTransaction(tx, false, Commitment.Confirmed);
                if (result == null || !result.WasSuccessful || string.IsNullOrEmpty(result.Result))
                {
                    var reason = result == null ? "no reply" : (result.Reason ?? "refused");
                    // The SDK cannot parse newer RPC error bodies ("Unable to parse json."): read the node's message.
                    var raw = result?.RawRpcResponse;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(raw, @"""message""\s*:\s*""([^""]+)""");
                        if (m.Success) reason = m.Groups[1].Value;
                    }
                    if (reason.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("0x1", StringComparison.Ordinal) >= 0 ||
                        reason.IndexOf("no record of a prior credit", StringComparison.OrdinalIgnoreCase) >= 0)
                        reason = "not enough devnet SOL (get some at faucet.solana.com)";
                    return (null, Fail("Transfer failed: " + reason));
                }
                LastTransferSignature = result.Result;
                Done();
                _ = RefreshBalanceLater();
                return (result.Result, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session.TransferSol: " + e);
                return (null, Fail("Transfer failed: " + Readable(e)));
            }
        }

        /// <summary>
        /// Hackathon U-3: sends one NFT (an SPL token of the classic token program, amount 1) from the wallet to
        /// <paramref name="toAddress"/>'s associated token account, creating that account if needed (ATA "create
        /// idempotent"). Returns the transaction signature.
        /// </summary>
        public async Task<(string signature, string error)> TransferNft(string mintAddress, string toAddress)
        {
            if (!WalletConnected) return (null, "Connect a wallet first.");
            if (!Web3Rules.LooksLikeAddress(toAddress) || !Web3Rules.LooksLikeAddress(mintAddress)) return (null, "Not a Solana address.");
            if (Busy) return (null, "Busy, please wait.");
            Begin("Approve the NFT transfer in your wallet...");
            try
            {
                var from = Wallet.Account.PublicKey;
                var mint = new PublicKey(mintAddress);
                var to = new PublicKey(toAddress);
                // The NFT sits in the wallet's associated account (the server mints and moves it there); else any account
                // of the wallet holding it.
                var source = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(from, mint);
                var accounts = await Wallet.ActiveRpcClient.GetTokenAccountsByOwnerAsync(from.Key, mintAddress, null, Commitment.Confirmed);
                var held = false;
                if (accounts?.Result?.Value != null)
                {
                    foreach (var a in accounts.Result.Value)
                    {
                        var amount = a?.Account?.Data?.Parsed?.Info?.TokenAmount?.Amount;
                        if (amount == null || amount == "0") continue;
                        held = true;
                        if (a.PublicKey != source.Key) source = new PublicKey(a.PublicKey);
                        if (a.PublicKey == AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(from, mint).Key) break;
                    }
                }
                if (!held) return (null, Fail("Your wallet does not hold this island's NFT."));
                var destination = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(to, mint);
                var tx = new Transaction
                {
                    RecentBlockHash = await Wallet.GetBlockHash(Commitment.Confirmed, false),
                    FeePayer = from,
                    Instructions = new List<TransactionInstruction>
                    {
                        new TransactionInstruction
                        {
                            ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
                            Keys = new List<AccountMeta>
                            {
                                AccountMeta.Writable(from, true),
                                AccountMeta.Writable(destination, false),
                                AccountMeta.ReadOnly(to, false),
                                AccountMeta.ReadOnly(mint, false),
                                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                            },
                            Data = new byte[] { 1 }, // CreateIdempotent
                        },
                        TokenProgram.Transfer(source, destination, 1, from),
                    },
                    Signatures = new List<SignaturePubKeyPair>(),
                };
                var result = await Wallet.SignAndSendTransaction(tx, false, Commitment.Confirmed);
                if (result == null || !result.WasSuccessful || string.IsNullOrEmpty(result.Result))
                {
                    var reason = result == null ? "no reply" : (result.Reason ?? "refused");
                    var raw = result?.RawRpcResponse;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(raw, @"""message""\s*:\s*""([^""]+)""");
                        if (m.Success) reason = m.Groups[1].Value;
                    }
                    if (reason.IndexOf("no record of a prior credit", StringComparison.OrdinalIgnoreCase) >= 0)
                        reason = "not enough devnet SOL for the fee (get some at faucet.solana.com)";
                    return (null, Fail("NFT transfer failed: " + reason));
                }
                LastTransferSignature = result.Result;
                Done();
                _ = RefreshBalanceLater();
                return (result.Result, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session.TransferNft: " + e);
                return (null, Fail("NFT transfer failed: " + Readable(e)));
            }
        }

        async Task RefreshBalanceLater()
        {
            await Awaitable.WaitForSecondsAsync(2f);
            await RefreshBalance();
        }

        /// <summary>Devnet airdrop to the connected wallet (often rate limited; faucet.solana.com is the fallback).</summary>
        public async Task<string> RequestAirdrop(double sol = 1)
        {
            if (!WalletConnected) return "Connect a wallet first.";
            try
            {
                var r = await Wallet.RequestAirdrop(Web3Rules.SolToLamports(sol), Commitment.Confirmed);
                if (r == null || !r.WasSuccessful) return "Airdrop refused (" + (r?.Reason ?? "no reply") + "). Use faucet.solana.com.";
                _ = RefreshBalanceLater();
                return null;
            }
            catch (Exception e) { return "Airdrop failed: " + Readable(e); }
        }

        // ---------------------------------------------------------------- state

        WalletProfile ActiveEntry()
        {
            var p = OnlineService.Active;
            if (p == null) return null;
            return Load().entries.Find(e => e.localId == p.localId);
        }

        Saved Load()
        {
            if (_saved != null) return _saved;
            var json = PlayerPrefs.GetString(WalletProfilesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { _saved = JsonUtility.FromJson<Saved>(json); }
                catch (Exception e) { Debug.LogWarning("Web3Session: unreadable wallet profiles: " + e.Message); }
            }
            if (_saved == null) _saved = new Saved();
            if (_saved.entries == null) _saved.entries = new List<WalletProfile>();
            // Drop entries whose V4 profile is gone.
            _saved.entries.RemoveAll(e => e == null || OnlineService.Profiles.Find(e.localId) == null);
            return _saved;
        }

        void SaveEntries()
        {
            PlayerPrefs.SetString(WalletProfilesKey, JsonUtility.ToJson(Load()));
            PlayerPrefs.Save();
        }

        void Begin(string status)
        {
            Busy = true;
            LastError = null;
            Raise(status);
        }

        string Done()
        {
            Busy = false;
            Raise(null);
            return null;
        }

        string Fail(string error)
        {
            Busy = false;
            LastError = error;
            Raise(null);
            return error;
        }

        void Raise(string status)
        {
            Status = status;
            try { Changed?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void EnsureHost() => Web3Host.Ensure();

        static string ApiError<T>(string what, ApiResult<T> r) where T : class
        {
            switch (r.Outcome)
            {
                case ApiOutcome.NotConfigured: return "No game server configured.";
                case ApiOutcome.Offline: return $"The game server is not reachable ({OnlineService.Client.BaseUrl}). " + (r.Message ?? string.Empty);
                case ApiOutcome.Rejected:
                    if (r.Status == 404) return $"The server has no wallet {what} yet (404 at {OnlineService.Client.BaseUrl}).";
                    return r.Message ?? $"The server refused the {what} ({r.Status}).";
                default: return $"Server error during {what} ({r.Status}). " + (r.Message ?? string.Empty);
            }
        }

        static string Readable(Exception e)
        {
            var m = e is AggregateException a && a.InnerException != null ? a.InnerException.Message : e.Message;
            if (string.IsNullOrEmpty(m)) return "Unknown error.";
            if (m.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("reject", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cancelled in the wallet.";
            return m;
        }
    }
}
