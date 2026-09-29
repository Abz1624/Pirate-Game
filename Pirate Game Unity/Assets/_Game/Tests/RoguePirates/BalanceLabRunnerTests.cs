using NUnit.Framework;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.Tests
{
    public class BalanceLabRunnerTests
    {
        [Test]
        public void SameScenarioAndSeeds_ProducesSameSummary()
        {
            var scenario = new BalanceLabScenario { Seed = 41, Runs = 4, DurationSeconds = 30f };
            var first = BalanceLabRunner.Run(scenario);
            var second = BalanceLabRunner.Run(scenario);

            Assert.That(second.WinRate, Is.EqualTo(first.WinRate));
            Assert.That(second.AverageKills, Is.EqualTo(first.AverageKills));
            Assert.That(second.AverageLevel, Is.EqualTo(first.AverageLevel));
            Assert.That(second.Samples[0].Kills, Is.EqualTo(first.Samples[0].Kills));
        }

        [Test]
        public void Scenario_NormalizesInvalidInputsWithoutMutatingCaller()
        {
            var scenario = new BalanceLabScenario { Runs = 0, WeaponSlotCap = 0, WeaponCount = 99, DurationSeconds = 0f };
            var result = BalanceLabRunner.Run(scenario);

            Assert.That(result.Runs, Is.EqualTo(1));
            Assert.That(scenario.Runs, Is.EqualTo(0), "The lab runner must not alter the caller's preset.");
        }

        [Test]
        public void MoreActiveWeapons_IncreasesShortRunKills()
        {
            var oneWeapon = new BalanceLabScenario { Seed = 8, Runs = 3, DurationSeconds = 45f, WeaponCount = 1 };
            var sixWeapons = oneWeapon.Clone();
            sixWeapons.WeaponCount = 6;

            Assert.That(BalanceLabRunner.Run(sixWeapons).AverageKills,
                Is.GreaterThanOrEqualTo(BalanceLabRunner.Run(oneWeapon).AverageKills));
        }

        [Test]
        public void KitingAutoplayer_AutoTargetsAndProducesCombatProgress()
        {
            var scenario = new BalanceLabScenario
            {
                Runs = 2,
                DurationSeconds = 30f,
                InputPolicy = SimInputPolicy.Kite,
                WeaponCount = 3
            };

            var result = BalanceLabRunner.Run(scenario);
            Assert.That(result.AverageKills, Is.GreaterThan(0f));
            Assert.That(result.AverageLevel, Is.GreaterThan(1f));
        }

        [Test]
        public void CiGate_ReportsTargetBandResultDeterministically()
        {
            var scenario = new BalanceLabScenario
            {
                Seed = 7, Runs = 3, DurationSeconds = 20f,
                TargetAverageSurvivalMin = 0f, TargetAverageSurvivalMax = 1f,
                TargetMerchantAffordabilityMin = 0f, TargetExpertBossWinMin = 0f
            };
            Assert.That(BalanceLabCiGate.MeetsTargets(scenario, out var report), Is.True);
            Assert.That(report, Does.Contain("Target bands"));
        }

        [Test]
        public void Clone_DoesNotShareEditableLoadoutData()
        {
            var original = new BalanceLabScenario();
            original.WeaponLoadout.Add(new BalanceWeaponLoadoutEntry { Id = "original" });
            var copy = original.Clone();
            copy.WeaponLoadout[0].Id = "changed";
            Assert.That(original.WeaponLoadout[0].Id, Is.EqualTo("original"));
        }
    }
}
