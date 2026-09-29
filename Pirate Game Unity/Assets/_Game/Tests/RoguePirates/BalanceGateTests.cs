using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.AccountClient.Raids;
using RoguePirates.Content;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.Tests
{
    public class BalanceGateTests
    {
        [Test]
        public void ContentManifest_Validates()
        {
            var manifest = ContentExporter.ExportBaseline();
            var result = ContentValidator.Validate(manifest);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        }

        [Test]
        public void BatchSimulation_NoRunawayKillsOnShortRuns()
        {
            var results = BalanceSimulationRunner.RunBatch(new[] { 1, 2, 3, 42, 12345 }, 300);
            foreach (var r in results)
                Assert.That(r.Kills, Is.LessThan(500));
        }

        [Test]
        public void MandatoryPickGate_ThresholdEnforced()
        {
            var picks = new Dictionary<string, float>
            {
                ["deck_cannons"] = 0.50f,
                ["broadside"] = 0.30f
            };
            Assert.That(BalanceSimulationRunner.HasMandatoryPickDominance(picks), Is.False);

            picks["deck_cannons"] = 0.70f;
            Assert.That(BalanceSimulationRunner.HasMandatoryPickDominance(picks), Is.True);
        }

        [Test]
        public void SoakSimulation_CompletesWithoutException()
        {
            var results = SoakSimulationRunner.RunSoak(1000, 5, 600);
            Assert.That(results.Count, Is.EqualTo(5));
        }

        [Test]
        public void RaidSettlement_IsIdempotent()
        {
            var svc = new RaidSettlementService();
            var quote = svc.CreateQuote("target", 100);
            Assert.That(svc.Settle(quote, false), Is.EqualTo(RaidSettlementResult.Success));
            Assert.That(svc.Settle(quote, false), Is.EqualTo(RaidSettlementResult.AlreadySettled));
        }
    }
}
