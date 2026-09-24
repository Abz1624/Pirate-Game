using HighSeas.Balance;
using HighSeas.Core;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    public sealed class SaltwindAmbientCompositionGateTests
    {
        [Test]
        public void AmbientBandProfileIds_ParseToBandIndex()
        {
            Assert.IsTrue(SaltwindAmbientCompositionLogic.TryGetAmbientBandIndex("saltwind_ambient_band_0", out var band0));
            Assert.AreEqual(0, band0);
            Assert.IsTrue(SaltwindAmbientCompositionLogic.TryGetAmbientBandIndex("saltwind_ambient_band_4", out var band4));
            Assert.AreEqual(4, band4);
            Assert.IsFalse(SaltwindAmbientCompositionLogic.TryGetAmbientBandIndex("saltwind_boss", out _));
        }

        [Test]
        public void OrdinaryShare_MatchesHandoffBands()
        {
            Assert.AreEqual(1f, SaltwindAmbientCompositionLogic.GetOrdinaryShare(0), 0.001f);
            Assert.AreEqual(0.8f, SaltwindAmbientCompositionLogic.GetOrdinaryShare(1), 0.001f);
            Assert.AreEqual(0.7f, SaltwindAmbientCompositionLogic.GetOrdinaryShare(2), 0.001f);
            Assert.AreEqual(0.65f, SaltwindAmbientCompositionLogic.GetOrdinaryShare(3), 0.001f);
            Assert.AreEqual(0.6f, SaltwindAmbientCompositionLogic.GetOrdinaryShare(4), 0.001f);
        }

        [Test]
        public void Band0_PicksOnlySoldierFodder()
        {
            LogAssert.ignoreFailingMessages = true;
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var rng = new RunRng(12345);
            for (var i = 0; i < 64; i++)
            {
                Assert.IsTrue(SaltwindAmbientCompositionLogic.TryPickEnemyId(
                    0, rng, _ => 0, export, out var id, elapsedSeconds: 45f, playerLevel: 1));
                Assert.That(id, Is.EqualTo("swordfish").Or.EqualTo("deckhand_dinghy"));
            }

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void KrakenAreaDenial_RespectsCapAndFallsBack()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            export.Spawn.MaxSimultaneousAreaDenial = 2;
            var rng = new RunRng(999);
            var picks = 0;
            for (var i = 0; i < 48; i++)
            {
                Assert.IsTrue(SaltwindAmbientCompositionLogic.TryPickEnemyId(
                    4, rng, id => id == "kraken_spawn" ? 2 : 0, export, out var picked));
                if (picked == "kraken_spawn")
                    picks++;
            }

            Assert.AreEqual(0, picks);
        }

        [Test]
        public void SpawnTelemetry_Checkpoints_FireOncePerRun()
        {
            StageBalanceProfileContext.ResetForTests();
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());

            SaltwindSpawnTelemetryLogic.ResetForNewRun();
            SaltwindSpawnTelemetryLogic.MaybeRecordCheckpoint(119f, 10, 0.5f, 1f);
            SaltwindSpawnTelemetryLogic.MaybeRecordCheckpoint(120f, 12, 0.5f, 1f);
            SaltwindSpawnTelemetryLogic.MaybeRecordCheckpoint(121f, 13, 0.5f, 1f);

            StageBalanceProfileContext.ResetForTests();
        }
    }
}
