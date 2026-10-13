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
            try { acc = await WaitForWallet(adapter.Login(), ConnectTimeoutSeconds, true); }
            catch (WalletWaitException e) { return e.Message; }
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
            byte[] signed;
            try { signed = await WaitForWallet(Wallet.SignMessage(Encoding.UTF8.GetBytes(nonce.Data.message)), SignTimeoutSeconds); }
            catch (WalletWaitException e) { return e.Message; }
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
            _walletSendsItself = false;
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
                var sig = Web3Rules.DetachedSignature(await WaitForWallet(Wallet.SignMessage(message), SignTimeoutSeconds));
                return sig == null ? (null, "The wallet did not sign the message.") : (sig, null);
            }
            catch (WalletWaitException e) { return (null, e.Message); }
            catch (Exception e) { return (null, Readable(e)); }
        }

        /// <summary>Signs a UTF-8 text; returns the signature in base58 (the API's format).</summary>
        public async Task<(string signatureBase58, string error)> SignText(string text)
        {
            var (sig, error) = await SignMessage(Encoding.UTF8.GetBytes(text ?? string.Empty));
            return (sig != null ? Encoders.Base58.EncodeData(sig) : null, error);
        }

        /// <summary>
        /// Builds, signs (the wallet asks the player) and sends a SOL transfer on devnet, with an optional memo (the server
        /// matches deposits by it). 13 Oct (review): the wallet only signs (<see cref="WalletBase.SignTransaction"/>: the WebGL
        /// wallet adapter's signTransaction and Android MWA's sign_transactions alike); the transaction's id is its fee payer's
        /// signature, known before anything is sent. <paramref name="onSigned"/> gets it first (the caller persists it), then
        /// the game sends it. Any failure after signing is "sent, unknown" (<see cref="TransferResult.MaybeSent"/>): the SOL
        /// may have left, so the caller keeps the signature and asks the server instead of saying nothing was taken. Only a
        /// node's definite preflight rejection (simulation or signature check failed: nothing was broadcast) or a refusal
        /// before signing counts as not sent. V5 M9i: an MWA wallet without sign_transactions signs and sends itself
        /// (<see cref="WalletSignsAndSends"/>).
        /// </summary>
        public Task<TransferResult> TransferSol(string toAddress, double sol, string memo = null, Action<string> onSigned = null)
        {
            if (!WalletConnected) return Task.FromResult(TransferResult.NotSent("Connect a wallet first."));
            if (!Web3Rules.LooksLikeAddress(toAddress)) return Task.FromResult(TransferResult.NotSent("Not a Solana address."));
            var lamports = Web3Rules.SolToLamports(sol);
            if (lamports == 0) return Task.FromResult(TransferResult.NotSent("Nothing to send."));
            return SignAndSend("Transfer", "Approve the transfer in your wallet...", onSigned, async from =>
            {
                var tx = new Transaction
                {
                    RecentBlockHash = await Wallet.GetBlockHash(Commitment.Confirmed, false),
                    FeePayer = from,
                    Instructions = new List<TransactionInstruction> { SystemProgram.Transfer(from, new PublicKey(toAddress), lamports) },
                    Signatures = new List<SignaturePubKeyPair>(),
                };
                if (!string.IsNullOrEmpty(memo)) tx.Instructions.Add(MemoProgram.NewMemo(from, memo));
                return (tx, (string)null);
            });
        }

        /// <summary>The end of a transfer: its signature (once the wallet signed), the error, and whether the SOL may have
        /// left the wallet (signed and possibly broadcast: never tell the player nothing was taken).</summary>
        public readonly struct TransferResult
        {
            public readonly string Signature;
            public readonly string Error;
            public readonly bool MaybeSent;

            public TransferResult(string signature, string error, bool maybeSent)
            {
                Signature = signature;
                Error = error;
                MaybeSent = maybeSent;
            }

            public bool Ok => Error == null && !string.IsNullOrEmpty(Signature);
            public static TransferResult NotSent(string error) => new TransferResult(null, error, false);
        }

        /// <summary>Signs <paramref name="build"/>'s transaction with the wallet, hands the signature to
        /// <paramref name="onSigned"/>, then sends it (see <see cref="TransferSol"/>).</summary>
        async Task<TransferResult> SignAndSend(string what, string status, Action<string> onSigned,
            Func<PublicKey, Task<(Transaction tx, string error)>> build)
        {
            if (Busy) return TransferResult.NotSent("Busy, please wait.");
            Begin(status);
            string signature = null;
            try
            {
                var from = Wallet.Account.PublicKey;
                var (tx, buildError) = await build(from);
                if (buildError != null) return TransferResult.NotSent(Fail(buildError));
                Transaction signed = null;
                if (!_walletSendsItself)
                {
                    try { signed = await WaitForWallet(Wallet.SignTransaction(tx), SignTimeoutSeconds); }
                    catch (WalletWaitException e) { return TransferResult.NotSent(Fail(e.Message)); }
                    catch (Exception e) when (Wallet is SolanaWalletAdapter && Web3Rules.SignOnlyUnsupported(e))
                    {
                        // V5 M9i: an MWA wallet with only sign_and_send_transactions (nothing was signed).
                        Debug.Log("Web3Session: the wallet has no sign_transactions; it signs and sends from now on.");
                        _walletSendsItself = true;
                    }
                }
                if (_walletSendsItself)
                {
                    // A fresh transaction (the first one went to the wallet; a new blockhash costs nothing).
                    var (again, againError) = await build(from);
                    if (againError != null) return TransferResult.NotSent(Fail(againError));
                    return await WalletSignsAndSends(what, again, from, onSigned);
                }
                signature = Web3Rules.TransactionId(signed?.Signatures, from.Key);
                if (signature == null) return TransferResult.NotSent(Fail(what + " failed: the wallet did not sign."));
                LastTransferSignature = signature;
                // Persist first: from here on the SOL may leave the wallet whatever happens to this app.
                try { onSigned?.Invoke(signature); }
                catch (Exception e) { Debug.LogWarning("Web3Session: could not keep the signature: " + e.Message); }
                Raise("Sending the transaction to devnet...");
                var result = await Wallet.ActiveRpcClient.SendTransactionAsync(Convert.ToBase64String(signed.Serialize()), false, Commitment.Confirmed);
                if (result != null && result.WasSuccessful && !string.IsNullOrEmpty(result.Result))
                {
                    if (result.Result != signature) Debug.LogWarning($"Web3Session: the node returned {result.Result} for {signature}.");
                    Done();
                    _ = RefreshBalanceLater();
                    return new TransferResult(signature, null, true);
                }
                var reason = SendFailureReason(result);
                if (Web3Rules.DefinitelyNotBroadcast(result != null ? result.ServerErrorCode : 0, result?.RawRpcResponse))
                    return new TransferResult(signature, Fail(what + " failed: " + reason), false);
                // Unknown: the node may have taken it (a timeout, an unreadable reply).
                return new TransferResult(signature, Fail(what + " was signed and sent, but devnet's reply was unclear (" + reason + ")."), true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session." + what + ": " + e);
                if (signature == null) return TransferResult.NotSent(Fail(what + " failed: " + Readable(e)));
                return new TransferResult(signature, Fail(what + " was signed and sent, but devnet's reply was unclear (" + Readable(e) + ")."), true);
            }
        }

        /// <summary>The connected MWA wallet answered sign_transactions with "method not found": transfers go through its
        /// sign_and_send_transactions (<see cref="WalletSignsAndSends"/>) until another wallet connects.</summary>
        bool _walletSendsItself;

        /// <summary>
        /// V5 M9i: a Mobile Wallet Adapter wallet that only signs and sends (MWA 2.0's mandatory
        /// <c>sign_and_send_transactions</c>). The signature comes back only once the wallet has sent the transaction, so it
        /// is handed to <paramref name="onSigned"/> (persisted) the moment it arrives. "Sent, unknown" stays: when the wallet
        /// may have sent it without naming the signature (a dropped session, an unmapped error, the game stopped waiting),
        /// the result says the SOL may have left (<see cref="TransferResult.MaybeSent"/> with no signature) and the player is
        /// told to check the wallet before paying again; only the wallet's definite refusals count as not sent.
        /// </summary>
        async Task<TransferResult> WalletSignsAndSends(string what, Transaction tx, PublicKey from, Action<string> onSigned)
        {
            if (!(Wallet is SolanaWalletAdapter adapter)) return TransferResult.NotSent(Fail(what + " failed: this wallet cannot send transactions itself."));
            // The unsigned wire format: one zeroed signature slot for the fee payer, which the wallet fills.
            tx.Signatures = new List<SignaturePubKeyPair> { new SignaturePubKeyPair { PublicKey = from, Signature = new byte[64] } };
            Raise("Approve the transfer in your wallet (it sends it to devnet)...");
            SignAndSendTxResult result;
            Task<SignAndSendTxResult> send = null;
            try
            {
                send = adapter.SignAndSendTransactions(new[] { tx }, new SignAndSendTransactionsOptions { Commitment = "confirmed" });
                result = await WaitForWallet(send, SignTimeoutSeconds, false, true);
            }
            catch (WalletWaitException e)
            {
                // The wallet may still answer: a signature it gives later is kept too, so a retry checks instead of paying.
                _ = KeepLateSignature(send, onSigned);
                return new TransferResult(null, Fail(what + ": " + e.Message), true);
            }
            catch (OperationInFlightException e) { return TransferResult.NotSent(Fail(what + " failed: " + e.Message)); }
            catch (Exception e)
            {
                Debug.LogWarning("Web3Session." + what + " (wallet sends): " + e);
                return new TransferResult(null, Fail(MaybeSentLine(what, Readable(e))), true);
            }
            var outcome = Web3Rules.ReadSignAndSend(result, out var signature, out var reason);
            switch (outcome)
            {
                case Web3Rules.WalletSend.Sent:
                    LastTransferSignature = signature;
                    try { onSigned?.Invoke(signature); }
                    catch (Exception e) { Debug.LogWarning("Web3Session: could not keep the signature: " + e.Message); }
                    Done();
                    _ = RefreshBalanceLater();
                    return new TransferResult(signature, null, true);
                case Web3Rules.WalletSend.NotSent:
                    return TransferResult.NotSent(Fail(reason == "Cancelled in the wallet." ? reason + " Nothing was sent." : what + " failed: " + reason + "."));
                default:
                    return new TransferResult(null, Fail(MaybeSentLine(what, reason)), true);
            }
        }

        /// <summary>A sign-and-send the game stopped waiting for: its signature, if the wallet still sends it, is persisted.</summary>
        async Task KeepLateSignature(Task<SignAndSendTxResult> send, Action<string> onSigned)
        {
            if (send == null) return;
            try
            {
                if (Web3Rules.ReadSignAndSend(await send, out var signature, out _) != Web3Rules.WalletSend.Sent) return;
                LastTransferSignature = signature;
                onSigned?.Invoke(signature);
                Debug.Log("Web3Session: the wallet sent it after all; its signature is kept (" + signature + ").");
            }
            catch (Exception e) { Debug.LogWarning("Web3Session: a late wallet answer could not be read: " + e.Message); }
        }

        static string MaybeSentLine(string what, string reason) =>
            what + " may have been sent by your wallet, but it did not say (" + reason + "). Check your wallet's activity before trying again.";

        static string SendFailureReason(Solana.Unity.Rpc.Core.Http.RequestResult<string> result)
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
            return reason;
        }

        /// <summary>
        /// Hackathon U-3: sends one NFT (an SPL token of the classic token program, amount 1) from the wallet to
        /// <paramref name="toAddress"/>'s associated token account, creating that account if needed (ATA "create
        /// idempotent"). Returns the transaction signature.
        /// </summary>
        public Task<TransferResult> TransferNft(string mintAddress, string toAddress, Action<string> onSigned = null)
        {
            if (!WalletConnected) return Task.FromResult(TransferResult.NotSent("Connect a wallet first."));
            if (!Web3Rules.LooksLikeAddress(toAddress) || !Web3Rules.LooksLikeAddress(mintAddress)) return Task.FromResult(TransferResult.NotSent("Not a Solana address."));
            return SignAndSend("NFT transfer", "Approve the NFT transfer in your wallet...", onSigned, async from =>
            {
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
                if (!held) return (null, "Your wallet does not hold this island's NFT.");
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
                return (tx, (string)null);
            });
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

        // ---------------------------------------------------------------- waiting for the wallet

        /// <summary>How long the game waits for the wallet (seconds): choosing and approving a connection, signing.</summary>
        public const float ConnectTimeoutSeconds = 180f, SignTimeoutSeconds = 180f;

        sealed class WalletWaitException : Exception
        {
            public WalletWaitException(string message) : base(message) { }
        }

        bool _cancelRequested;
        bool _waiting;

        /// <summary>The session waits for the wallet (a connection, a signature): <see cref="CancelWait"/> stops waiting.</summary>
        public bool CanCancel => Busy && _waiting;

        /// <summary>Stops waiting for the wallet (the player closed it, or it never answered). Safe: a transfer is sent by the
        /// game only after the wallet returns its signature, so a signature the wallet gives later is never sent. The one
        /// exception is a wallet that sends itself (V5 M9i, <see cref="WalletSignsAndSends"/>): the player is told it may
        /// still send.</summary>
        public void CancelWait()
        {
            if (_waiting) _cancelRequested = true;
        }

        /// <summary>Waits for <paramref name="task"/>, at most <paramref name="seconds"/>, until <see cref="CancelWait"/>, or (the
        /// WebGL wallet chooser) until its close button hides the chooser before a wallet was picked. Throws
        /// <see cref="WalletWaitException"/> with the player-facing reason; the abandoned task is left to the SDK. V5 M9i:
        /// <paramref name="walletSends"/> (a wallet that sends the transaction itself): stopping the wait cannot stop the
        /// wallet, so the reason says it may still send it.</summary>
        async Task<T> WaitForWallet<T>(Task<T> task, float seconds, bool watchPicker = false, bool walletSends = false)
        {
            _waiting = true;
            _cancelRequested = false;
            Raise(Status);
            var until = Time.realtimeSinceStartup + seconds;
            var picker = watchPicker ? new PickerWatch() : null;
            try
            {
                while (!task.IsCompleted)
                {
                    if (_cancelRequested)
                        throw new WalletWaitException(walletSends ? "Stopped waiting. Your wallet may still send it: check its activity before trying again." : "Cancelled. Nothing was sent.");
                    if (Time.realtimeSinceStartup > until)
                        throw new WalletWaitException(walletSends ? "The wallet did not answer in time and may still send it: check its activity before trying again." : "The wallet did not answer in time. Nothing was sent; try again.");
                    if (picker != null && picker.Closed()) throw new WalletWaitException("The wallet choice was closed.");
                    await Awaitable.NextFrameAsync();
                }
                return await task;
            }
            finally
            {
                _waiting = false;
                _cancelRequested = false;
            }
        }

        /// <summary>The SDK's WebGL wallet chooser (SolanaWalletAdapterWebGL.WalletAdapterUI): its close button only hides it,
        /// and the SDK's login then waits for a choice forever. Seen open, then hidden without a pick: closed.</summary>
        sealed class PickerWatch
        {
            bool _seen, _picked;

            public bool Closed()
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var ui = SolanaWalletAdapterWebGL.WalletAdapterUI;
                if (ui == null) return false;
                if (ui.activeSelf)
                {
                    if (!_seen)
                    {
                        _seen = true;
                        var screen = ui.transform.childCount > 0 ? ui.transform.GetChild(0).GetComponent<WalletAdapterScreen>() : null;
                        if (screen != null)
                        {
                            var picked = screen.OnSelectedAction;
                            screen.OnSelectedAction = name => { _picked = true; picked?.Invoke(name); };
                        }
                    }
                    return false;
                }
                return _seen && !_picked;
#else
                return _seen && _picked; // only the WebGL player has the chooser
#endif
            }
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
