using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using RoguePirates.V4.Online;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using V = RoguePirates.Hackathon.HackathonRules.PaymentVerdict;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>
    /// Hackathon (13 Oct, review): the money path. A signed transfer's signature is kept (PlayerPrefs through a seam here)
    /// before it is sent and until the server's final answer, every retry posts that same signature (never a second
    /// payment), and the server's answers split into wait / hold (keep it) and final (forget it, say what happens to the SOL).
    /// </summary>
    public sealed class PaymentSafetyTests
    {
        string _store;
        DateTime _now;
        readonly List<string> _posted = new List<string>();

        const string Sig = "5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW";
        const string Wallet = "7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosgAsU";

        [SetUp]
        public void SetUp()
        {
            _store = null;
            _now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
            _posted.Clear();
            PendingTransfers.Read = () => _store;
            PendingTransfers.Write = json => _store = json;
            PendingTransfers.Now = () => _now;
            PaymentFlow.Delay = _ => Task.CompletedTask;
        }

        [TearDown]
        public void TearDown()
        {
            PendingTransfers.UseDefaultStorage();
            PaymentFlow.UseDefaultDelay();
            HackathonApi.UseDefaultPostDeposit();
            IslandApi.UseDefaultPosts();
        }

        static ApiResult<T> Ok<T>(T data) where T : class => new ApiResult<T> { Outcome = ApiOutcome.Ok, Status = 200, Data = data };

        static ApiResult<T> Refused<T>(long status, string code, string message = null) where T : class =>
            new ApiResult<T> { Outcome = status >= 500 ? ApiOutcome.ServerError : ApiOutcome.Rejected, Status = status, ErrorCode = code, Message = message };

        /// <summary>A fake server: answers in order (the last one repeats), recording each posted signature.</summary>
        Func<string, Task<ApiResult<T>>> Server<T>(params ApiResult<T>[] answers) where T : class
        {
            var i = 0;
            return sig =>
            {
                _posted.Add(sig);
                var a = answers[Math.Min(i, answers.Length - 1)];
                i++;
                return Task.FromResult(a);
            };
        }

        // ---------------------------------------------------------------- persistence

        [Test]
        public void Pending_IsKept_UntilForgotten_AndSurvivesAReload()
        {
            Assert.IsFalse(PendingTransfers.Has(PendingTransfers.Deposit, "lobby1"));
            PendingTransfers.Remember(PendingTransfers.Deposit, "lobby1", Sig, Wallet, 10_000_000);
            Assert.IsNotNull(_store, "written at once (the app may be killed next)");
            // A relaunch reads the same stored text.
            var saved = _store;
            PendingTransfers.Read = () => saved;
            var e = PendingTransfers.Get(PendingTransfers.Deposit, "lobby1");
            Assert.IsNotNull(e);
            Assert.AreEqual(Sig, e.signature);
            Assert.AreEqual(Wallet, e.wallet);
            Assert.AreEqual(10_000_000, e.lamports);
            Assert.IsFalse(PendingTransfers.Has(PendingTransfers.Buy, "lobby1"), "kinds are separate");
            PendingTransfers.Read = () => _store;
            PendingTransfers.Remember(PendingTransfers.Buy, "listing9", Sig + "x", "otherWallet", 5);
            Assert.AreEqual(0, PendingTransfers.All(PendingTransfers.Buy, Wallet).Count, "another wallet's pending buy is not this wallet's");
            PendingTransfers.Forget(PendingTransfers.Deposit, "lobby1");
            Assert.IsFalse(PendingTransfers.Has(PendingTransfers.Deposit, "lobby1"));
            Assert.IsTrue(PendingTransfers.Has(PendingTransfers.Buy, "listing9"));
        }

        [Test]
        public void Pending_UnreadableRecord_IsEmpty_NotAnError()
        {
            _store = "{not json";
            Assert.IsNull(PendingTransfers.Get(PendingTransfers.Deposit, "x"));
            PendingTransfers.Remember(PendingTransfers.Deposit, "x", Sig, Wallet, 1);
            Assert.IsTrue(PendingTransfers.Has(PendingTransfers.Deposit, "x"));
        }

        // ---------------------------------------------------------------- retryable vs final

        [Test]
        public void Verdicts_SplitRetryableFromFinal()
        {
            Assert.AreEqual(V.Accepted, HackathonRules.Verdict(ApiOutcome.Ok, 200, null));
            Assert.AreEqual(V.Wait, HackathonRules.Verdict(ApiOutcome.Rejected, 409, "tx_not_confirmed"));
            Assert.AreEqual(V.Wait, HackathonRules.Verdict(ApiOutcome.Offline, 0, null));
            Assert.AreEqual(V.Wait, HackathonRules.Verdict(ApiOutcome.ServerError, 502, null));
            Assert.AreEqual(V.Hold, HackathonRules.Verdict(ApiOutcome.Rejected, 400, "nft_not_received"), "only a listing waits for the escrow");
            Assert.AreEqual(V.Wait, HackathonRules.Verdict(ApiOutcome.Rejected, 400, "nft_not_received", true));
            foreach (var code in new[] { "deposit_refunded", "memo_mismatch", "amount_too_low", "signature_used", "listing_not_active", "wager_closed", "not_in_lobby", "already_listed", "some_new_refund_code" })
                Assert.AreEqual(V.Final, HackathonRules.Verdict(ApiOutcome.Rejected, 409, code), code);
            Assert.AreEqual(V.Hold, HackathonRules.Verdict(ApiOutcome.Rejected, 401, "unauthorized"), "signed out: keep it, ask again later");
            Assert.AreEqual(V.Hold, HackathonRules.Verdict(ApiOutcome.Rejected, 503, "treasury_off"));
            Assert.AreEqual(V.Hold, HackathonRules.Verdict(ApiOutcome.NotConfigured, 0, null));
            Assert.AreEqual(V.Accepted, HackathonRules.Verdict(ApiOutcome.Rejected, 409, "already_deposited"));
        }

        [Test]
        public void FinalLines_GiveTheReason_AndWhatHappensToTheSol_NoEmDashes()
        {
            var refunded = HackathonRules.FinalLine("deposit", "0.01 SOL", "deposit_refunded", "You are not seated in this lobby. Your deposit is being refunded to your wallet.");
            StringAssert.Contains("not seated", refunded);
            StringAssert.Contains("refunded", refunded);
            var memo = HackathonRules.FinalLine("payment", "0.05 SOL", "memo_mismatch", null);
            StringAssert.Contains("memo mismatch", memo);
            StringAssert.Contains("not refunded automatically", memo);
            var gone = HackathonRules.FinalLine("payment", "0.05 SOL", "listing_not_active", "The listing is sold; do not pay for it.");
            StringAssert.Contains("refunds", gone);
            var nft = HackathonRules.FinalLine("NFT transfer", null, "not_your_island", "This is not your island.");
            StringAssert.Contains("escrow", nft);
            var expired = HackathonRules.FinalLine("deposit", "0.01 SOL", "expired", null);
            StringAssert.Contains("nothing was taken", expired);
            foreach (var line in new[] { refunded, memo, gone, nft, expired,
                         HackathonRules.DepositFailureLine(true, "0.01 SOL", true, false, "x", memo),
                         HackathonRules.DepositFailureLine(false, "0.01 SOL", true, false, "x") })
            {
                Assert.IsFalse(line.Contains("—"), line);
                Assert.IsFalse(line.Contains("No SOL was taken"), "a sent transfer never says no SOL was taken: " + line);
            }
        }

        // ---------------------------------------------------------------- deposits

        static LobbyWithWager Funded(string lobbyId) => new LobbyWithWager { lobbyId = lobbyId, wager = new WagerView { lobbyId = lobbyId, stakeLamports = 10_000_000, status = "funded" } };

        [Test]
        public void Deposit_WithAPendingSignature_PostsIt_NeverPaysAgain()
        {
            PendingTransfers.Remember(PendingTransfers.Deposit, "L1", Sig, Wallet, 10_000_000);
            var server = Server(Refused<LobbyWithWager>(409, "tx_not_confirmed"), Refused<LobbyWithWager>(409, "tx_not_confirmed"), Ok(Funded("L1")));
            HackathonApi.PostDeposit = (lobby, sig) => server(sig);
            // The wallet is not connected in the test: a second payment would fail before posting. It is never asked.
            var r = HackathonApi.Deposit(new WagerView { lobbyId = "L1", stakeLamports = 10_000_000, status = "awaiting_deposits", wagerId = "w" }, null).Result;
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsNotNull(r.Wager);
            Assert.AreEqual(Sig, r.Signature);
            CollectionAssert.AreEqual(new[] { Sig, Sig, Sig }, _posted, "the same signature each time");
            Assert.IsFalse(HackathonApi.HasPendingDeposit("L1"), "accepted: forgotten");
        }

        [Test]
        public void Deposit_Refunded_IsFinal_AndForgotten()
        {
            PendingTransfers.Remember(PendingTransfers.Deposit, "L2", Sig, Wallet, 10_000_000);
            var server = Server(Refused<LobbyWithWager>(409, "deposit_refunded", "You left the lobby. Your deposit was refunded to your wallet."));
            HackathonApi.PostDeposit = (lobby, sig) => server(sig);
            var r = HackathonApi.CheckDeposit("L2", null).Result;
            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Sent);
            Assert.IsTrue(r.Refunded);
            Assert.IsFalse(r.Pending);
            StringAssert.Contains("refunded", r.Error);
            Assert.IsFalse(HackathonApi.HasPendingDeposit("L2"));
        }

        [Test]
        public void Deposit_UnknownRefusal_IsHeld_KeepsTheSignature()
        {
            PendingTransfers.Remember(PendingTransfers.Deposit, "L3", Sig, Wallet, 10_000_000);
            var server = Server(Refused<LobbyWithWager>(401, "unauthorized", "Sign in again."));
            HackathonApi.PostDeposit = (lobby, sig) => server(sig);
            var r = HackathonApi.CheckDeposit("L3", null).Result;
            Assert.IsTrue(r.Pending);
            Assert.IsTrue(r.Sent);
            StringAssert.Contains("Check deposit", r.Error);
            Assert.AreEqual(1, _posted.Count, "a hold does not hammer the server");
            Assert.IsTrue(HackathonApi.HasPendingDeposit("L3"), "kept for the next Check deposit");
            Assert.AreEqual("Sign in again.", PendingTransfers.Get(PendingTransfers.Deposit, "L3").lastError);
        }

        [Test]
        public void Deposit_StillUnconfirmed_StaysPending_ThenExpiresAsNotTaken()
        {
            PendingTransfers.Remember(PendingTransfers.Deposit, "L4", Sig, Wallet, 10_000_000);
            var server = Server(Refused<LobbyWithWager>(409, "tx_not_confirmed"));
            HackathonApi.PostDeposit = (lobby, sig) => server(sig);
            var r = HackathonApi.CheckDeposit("L4", null, null, 3).Result;
            Assert.IsTrue(r.Pending);
            Assert.AreEqual(3, _posted.Count);
            Assert.IsTrue(HackathonApi.HasPendingDeposit("L4"));
            _now = _now.AddSeconds(HackathonRules.ExpireSeconds + 10); // long past the blockhash's life
            r = HackathonApi.CheckDeposit("L4", null).Result;
            Assert.IsFalse(r.Pending);
            Assert.IsFalse(r.Sent, "devnet never had it: nothing was taken");
            StringAssert.Contains("never reached devnet", r.Error);
            Assert.IsFalse(HackathonApi.HasPendingDeposit("L4"));
        }

        // ---------------------------------------------------------------- market

        [Test]
        public void Buy_Pending_FinishPostsTheSavedSignature_AndAListingGoneShowsTheServerReason()
        {
            PendingTransfers.Remember(PendingTransfers.Buy, "LS1", Sig, Wallet, 50_000_000);
            var server = Server(Refused<Listing>(409, "listing_not_active", "The listing is cancelled; do not pay for it."));
            IslandApi.PostBuy = (id, sig) => server(sig);
            IslandApi.FetchListing = id => Task.FromResult(new Listing { listingId = id, status = "cancelled" });
            var r = IslandApi.Buy(new Listing { listingId = "LS1", priceLamports = 50_000_000 }, null).Result;
            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Final);
            StringAssert.Contains("cancelled", r.Error, "the server's reason");
            StringAssert.Contains("refund", r.Error, "what happens to the SOL");
            Assert.AreEqual(Sig, r.Signature, "the explorer link");
            CollectionAssert.AreEqual(new[] { Sig }, _posted);
            Assert.IsFalse(PendingTransfers.Has(PendingTransfers.Buy, "LS1"));
        }

        [Test]
        public void Buy_AnEarlierPostWentThrough_IsASuccess()
        {
            PendingTransfers.Remember(PendingTransfers.Buy, "LS2", Sig, Wallet, 50_000_000);
            var server = Server(Refused<Listing>(409, "listing_not_active"));
            IslandApi.PostBuy = (id, sig) => server(sig);
            IslandApi.FetchListing = id => Task.FromResult(new Listing { listingId = id, status = "sold", paymentSignature = Sig });
            var r = IslandApi.FinishBuy("LS2", null).Result;
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("sold", r.Listing.status);
        }

        [Test]
        public void List_PendingNftTransfer_FinishListingRepostsIt_WithTheAskedPrice()
        {
            PendingTransfers.Remember(PendingTransfers.List, "I1", Sig, Wallet, 70_000_000);
            long price = 0;
            var calls = 0;
            IslandApi.PostList = (island, lamports, sig) =>
            {
                price = lamports;
                _posted.Add(sig);
                calls++;
                return Task.FromResult(calls == 1 ? Refused<Listing>(400, "nft_not_received") : Ok(new Listing { listingId = "new", status = "active", escrowSignature = sig, priceLamports = lamports }));
            };
            var r = IslandApi.FinishList("I1", 0, null).Result;
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(70_000_000, price, "the price asked when the NFT was sent");
            CollectionAssert.AreEqual(new[] { Sig, Sig }, _posted, "no second NFT transfer");
            Assert.IsFalse(PendingTransfers.Has(PendingTransfers.List, "I1"));
        }

        [Test]
        public void List_AlreadyListedByAnEarlierPost_IsASuccess()
        {
            PendingTransfers.Remember(PendingTransfers.List, "I2", Sig, Wallet, 70_000_000);
            IslandApi.PostList = (island, lamports, sig) => Task.FromResult(Refused<Listing>(409, "already_listed"));
            IslandApi.FetchMyListings = () => Task.FromResult(new[] { new Listing { listingId = "L", status = "active", escrowSignature = Sig } });
            var r = IslandApi.FinishList("I2", 0, null).Result;
            Assert.IsTrue(r.Ok, r.Error);
        }

        // ---------------------------------------------------------------- signing

        [Test]
        public void TransactionId_IsTheFeePayersSignature_KnownBeforeSending()
        {
            var payer = new Account();
            var other = new Account();
            var a = new byte[64];
            var b = new byte[64];
            a[0] = 7;
            b[0] = 9;
            var sigs = new List<SignaturePubKeyPair>
            {
                new SignaturePubKeyPair { PublicKey = other.PublicKey, Signature = b },
                new SignaturePubKeyPair { PublicKey = payer.PublicKey, Signature = a },
            };
            Assert.AreEqual(Web3Rules.SignatureBase58(a), Web3Rules.TransactionId(sigs, payer.PublicKey.Key));
            Assert.IsNull(Web3Rules.TransactionId(new List<SignaturePubKeyPair> { new SignaturePubKeyPair { PublicKey = payer.PublicKey, Signature = new byte[64] } }, payer.PublicKey.Key),
                "an all-zero (unsigned) slot is not a signature");
            Assert.IsNull(Web3Rules.TransactionId(null, payer.PublicKey.Key));
        }

        [Test]
        public void SendFailures_OnlyAPreflightRejection_CountsAsNotBroadcast()
        {
            Assert.IsTrue(Web3Rules.DefinitelyNotBroadcast(-32002, null));
            Assert.IsTrue(Web3Rules.DefinitelyNotBroadcast(0, "{\"jsonrpc\":\"2.0\",\"error\":{\"code\": -32002,\"message\":\"Transaction simulation failed: Blockhash not found\"},\"id\":1}"));
            Assert.IsTrue(Web3Rules.DefinitelyNotBroadcast(0, "{\"error\":{\"code\":-32003,\"message\":\"signature verification failure\"}}"));
            Assert.IsFalse(Web3Rules.DefinitelyNotBroadcast(0, null), "no reply: maybe sent");
            Assert.IsFalse(Web3Rules.DefinitelyNotBroadcast(0, "<html>502 Bad Gateway</html>"), "a proxy error: maybe sent");
            Assert.IsFalse(Web3Rules.DefinitelyNotBroadcast(-32005, "{\"error\":{\"code\":-32005}}"), "node behind: maybe sent");
            Assert.IsFalse(Web3Rules.DefinitelyNotBroadcast(0, "{\"error\":{\"code\":-320021}}"));
        }
    }
}
