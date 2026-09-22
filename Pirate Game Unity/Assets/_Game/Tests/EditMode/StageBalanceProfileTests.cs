using HighSeas.Balance;
using HighSeas.V3.MultiShip;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Tests
{
    public sealed class StageBalanceProfileTests
    {
        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            V3RunSession.Reset();
        }

        [Test]
        public void Export_RoundTrip_PreservesGameplayValues()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var export = profile.ToExport();
            var json = StageBalanceProfileExportCodec.ToCanonicalJson(export);
            var roundTrip = StageBalanceProfileExportCodec.WithContentHash(
                StageBalanceProfileExportCodec.FromCanonicalJson(json));

            Assert.AreEqual(export.ProfileId, roundTrip.ProfileId);
            Assert.AreEqual(export.Timing.StageDeadlineSeconds, roundTrip.Timing.StageDeadlineSeconds);
            Assert.AreEqual(export.Spawn.MaxActiveEnemies, roundTrip.Spawn.MaxActiveEnemies);
            Assert.AreEqual(export.Economy.ChestBasePrice, roundTrip.Economy.ChestBasePrice);
            Assert.AreEqual(export.StarterWeaponIds.Count, roundTrip.StarterWeaponIds.Count);
            CollectionAssert.AreEqual(export.Spawn.AmbientRatePerSecond, roundTrip.Spawn.AmbientRatePerSecond);
            Assert.Greater(export.WeaponStatSnapshots.Count, 0);
            Assert.AreEqual(export.WeaponStatSnapshots.Count, roundTrip.WeaponStatSnapshots.Count);
            Assert.AreEqual(export.BossStatSnapshot.BaseHull, roundTrip.BossStatSnapshot.BaseHull, 0.01f);
        }

        [Test]
        public void Export_Hash_IsStable_AndChangesWhenGameplayChanges()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var first = profile.ToExport();
            var second = profile.ToExport();
            Assert.AreEqual(first.ContentHash, second.ContentHash);

            profile.balanceVersion = 2;
            var changed = profile.ToExport();
            Assert.AreNotEqual(first.ContentHash, changed.ContentHash);
        }

        [Test]
        public void RuntimeState_IsNotSharedAcrossRuns()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            StageBalanceProfileContext.BeginRun(profile);
            StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
            Assert.AreEqual(31, StageBalanceProfileContext.Runtime.CurrentChestPrice);

            StageBalanceProfileContext.BeginRun(profile);
            Assert.AreEqual(20, StageBalanceProfileContext.Runtime.CurrentChestPrice);
        }

        [Test]
        public void FormulaFixtures_MatchApprovedDefaults()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(20, StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, 0));
            Assert.AreEqual(31, StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, 1));
            Assert.AreEqual(38, StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, 2));
            Assert.AreEqual(46, StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, 3));
            Assert.AreEqual(56, StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, 4));
            Assert.AreEqual(7, StageBalanceProfileExportCodec.XpToNextLevel(export, 0));
            Assert.AreEqual(0.35f, StageBalanceProfileExportCodec.OvertimeSpawnInterval(export, 600f), 0.001f);
            Assert.AreEqual(1.4f, StageBalanceProfileExportCodec.OvertimeSpawnInterval(export, 0f), 0.001f);
        }

        [Test]
        public void Resolver_RejectsMissingHostInSinglePlayer()
        {
            V3RunSession.Configure(V3RunSessionKind.SinglePlayer);
            var resolved = StageBalanceProfileResolver.TryResolveForCurrentScene(out _, out var error);
            Assert.IsFalse(resolved);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void Resolver_SkipsLocalMultiplayer()
        {
            V3RunSession.Configure(V3RunSessionKind.CoOp);
            var resolved = StageBalanceProfileResolver.TryResolveForCurrentScene(out var profile, out _);
            Assert.IsFalse(resolved);
            Assert.IsNull(profile);
        }
    }
}
