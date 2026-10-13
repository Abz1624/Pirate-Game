using System.Text;
using NUnit.Framework;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Bip39;
using Solana.Unity.Wallet.Utilities;

namespace RoguePirates.Hackathon.Tests
{
    /// <summary>Hackathon (12 Oct): the wallet helpers and the sign-in signature format the API checks.</summary>
    public sealed class Web3RulesTests
    {
        [Test]
        public void ShortAddress_KeepsEnds()
        {
            Assert.AreEqual("7xKX...9fQe", Web3Rules.ShortAddress("7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe"));
            Assert.AreEqual("abc", Web3Rules.ShortAddress("abc"));
            Assert.AreEqual(string.Empty, Web3Rules.ShortAddress(null));
        }

        [Test]
        public void ExplorerAddress_PointsAtDevnet()
        {
            Assert.AreEqual("https://explorer.solana.com/address/7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe?cluster=devnet",
                Web3Rules.ExplorerAddress("7xKXtg2CW87d97TXJSDpbD5jBkheTqA83TZRuJosg9fQe"));
            Assert.IsNull(Web3Rules.ExplorerAddress(null));
            Assert.IsNull(Web3Rules.ExplorerAddress(string.Empty));
        }

        [Test]
        public void SolToLamports_Rounds()
        {
            Assert.AreEqual(1_000_000UL, Web3Rules.SolToLamports(0.001));
            Assert.AreEqual(1_000_000_000UL, Web3Rules.SolToLamports(1));
            Assert.AreEqual(0UL, Web3Rules.SolToLamports(-1));
            Assert.AreEqual(0UL, Web3Rules.SolToLamports(double.NaN));
        }

        [Test]
        public void AddressAndEmailShapes()
        {
            Assert.IsTrue(Web3Rules.LooksLikeAddress("11111111111111111111111111111111"));
            Assert.IsFalse(Web3Rules.LooksLikeAddress("0OIl1111111111111111111111111111"));
            Assert.IsFalse(Web3Rules.LooksLikeAddress("short"));
            Assert.IsTrue(Web3Rules.LooksLikeEmail("a@b.co"));
            Assert.IsFalse(Web3Rules.LooksLikeEmail("a@b"));
            Assert.IsFalse(Web3Rules.LooksLikeEmail("a b@c.de"));
        }

        [Test]
        public void SignedMessage_DetachesTrailingSignature_AndVerifies()
        {
            var account = new Wallet(new Mnemonic(WordList.English, WordCount.Twelve)).Account;
            var message = Encoding.UTF8.GetBytes("roguepirates.fun wants you to sign in with your Solana account:\nNonce: abcdef12");
            var sig = account.Sign(message);
            Assert.AreEqual(64, sig.Length);
            Assert.AreSame(sig, Web3Rules.DetachedSignature(sig));
            // MWA wallets may return message || signature.
            var envelope = new byte[message.Length + 64];
            message.CopyTo(envelope, 0);
            sig.CopyTo(envelope, message.Length);
            var detached = Web3Rules.DetachedSignature(envelope);
            CollectionAssert.AreEqual(sig, detached);
            Assert.IsTrue(account.PublicKey.Verify(message, detached));
            CollectionAssert.AreEqual(sig, Encoders.Base58.DecodeData(Encoders.Base58.EncodeData(sig)));
            Assert.IsNull(Web3Rules.DetachedSignature(new byte[10]));
        }

        // ---- V5 M9i: MWA wallets that only sign and send ----------------------------------------------------------------

        [Test]
        public void SignOnlyUnsupported_IsMethodNotFound_FromTheWallet_EvenWrapped()
        {
            var notFound = new Solana.Unity.SDK.MwaRpcException(Solana.Unity.SDK.MwaErrorCodes.MethodNotFound, "sign_transactions");
            Assert.IsTrue(Web3Rules.SignOnlyUnsupported(notFound));
            Assert.IsTrue(Web3Rules.SignOnlyUnsupported(new System.AggregateException(notFound)));
            Assert.IsTrue(Web3Rules.SignOnlyUnsupported(new System.Exception("outer", notFound)));
            Assert.IsFalse(Web3Rules.SignOnlyUnsupported(new Solana.Unity.SDK.MwaRpcException(Solana.Unity.SDK.MwaErrorCodes.NotSigned, "declined")),
                "a declined signature is not a missing method");
            Assert.IsFalse(Web3Rules.SignOnlyUnsupported(new System.Exception("timeout")));
            Assert.IsFalse(Web3Rules.SignOnlyUnsupported(null));
        }

        [Test]
        public void ReadSignAndSend_SentOnlyWithASignature_RefusalsNotSent_RestUnknown()
        {
            var raw = new byte[64];
            for (var i = 0; i < raw.Length; i++) raw[i] = (byte)(i + 1);
            var expected = Encoders.Base58.EncodeData(raw);

            Assert.AreEqual(Web3Rules.WalletSend.Sent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.Success { Signatures = new[] { raw } }, out var sig, out _));
            Assert.AreEqual(expected, sig, "the wallet's signature is the transaction id the caller persists");
            Assert.AreEqual(Web3Rules.WalletSend.Unknown, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.Success { Signatures = new byte[0][] }, out sig, out _),
                "a success without a signature may still have sent it");
            Assert.IsNull(sig);

            Assert.AreEqual(Web3Rules.WalletSend.Sent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.NotSubmitted { PartialSignatures = new[] { raw } }, out sig, out _),
                "a partial submit naming this transaction landed it");
            Assert.AreEqual(expected, sig);
            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.NotSubmitted { PartialSignatures = new byte[][] { null } }, out sig, out _));
            Assert.IsNull(sig);

            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.UserDeclined(), out _, out var reason));
            StringAssert.Contains("Cancelled", reason);
            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.NotSupported(), out _, out _));
            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.Unauthorized(), out _, out _));
            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.InvalidPayloads { Valid = new[] { false } }, out _, out _));
            Assert.AreEqual(Web3Rules.WalletSend.NotSent, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.TooManyPayloads(), out _, out _));

            Assert.AreEqual(Web3Rules.WalletSend.Unknown, Web3Rules.ReadSignAndSend(new SignAndSendTxResult.Failed { Code = 0, Message = "session closed" }, out sig, out reason),
                "a dropped session may have sent it: never say nothing was taken");
            Assert.IsNull(sig);
            StringAssert.Contains("session closed", reason);
            Assert.AreEqual(Web3Rules.WalletSend.Unknown, Web3Rules.ReadSignAndSend(null, out _, out _));
        }
    }
}
