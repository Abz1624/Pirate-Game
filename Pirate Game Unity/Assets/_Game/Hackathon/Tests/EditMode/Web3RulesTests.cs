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
    }
}
