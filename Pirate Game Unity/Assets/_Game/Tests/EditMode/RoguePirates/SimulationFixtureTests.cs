using NUnit.Framework;
using RoguePirates.Simulation;
using RoguePirates.Simulation.Balance;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Tests
{
    public class SimulationFixtureTests
    {
        [Test]
        public void SameSeed_ProducesStableStateHash()
        {
            var a = BalanceSimulationRunner.Run(12345, 900);
            var b = BalanceSimulationRunner.Run(12345, 900);
            Assert.That(b.StateHash, Is.EqualTo(a.StateHash));
        }

        [Test]
        public void RenderFps30_60_120_YieldsIdenticalOutcomes()
        {
            var sim30 = new CombatSimulator(12345);
            var sim60 = new CombatSimulator(12345);
            var sim120 = new CombatSimulator(12345);

            sim30.StepRenderFrames(30, 10f);
            sim60.StepRenderFrames(60, 10f);
            sim120.StepRenderFrames(120, 10f);

            Assert.That(sim60.ComputeStateHash(), Is.EqualTo(sim30.ComputeStateHash()));
            Assert.That(sim120.ComputeStateHash(), Is.EqualTo(sim30.ComputeStateHash()));
        }

        [Test]
        public void PauseSet_ClearingOneReason_DoesNotClearOthers()
        {
            var pause = new PauseReasonSet();
            pause.Add(SimPauseReason.Manual);
            pause.Add(SimPauseReason.Choice);
            pause.Remove(SimPauseReason.Manual);
            Assert.That(pause.Has(SimPauseReason.Choice), Is.True);
            Assert.That(pause.BlocksSimulation, Is.True);
        }

        [Test]
        public void LastRum_ConsumesOnceOnSameTickDoubleLethal()
        {
            var lastRum = new LastRumSystem();
            lastRum.BeginTick();
            var hull = SimFixed.FromInt(0);
            Assert.That(lastRum.TryPreventLethal(ref hull), Is.True);
            Assert.That(lastRum.TryPreventLethal(ref hull), Is.False);
            Assert.That(hull.ToFloat(), Is.GreaterThan(0f));
        }

        [Test]
        public void BossPlayerSameTick_SecuresObjectiveBeforeRunEnd()
        {
            var sim = new CombatSimulator(99999);
            for (var i = 0; i < 600; i++)
                sim.Step(SimConstants.TickDurationSeconds);
            sim.ApplySameTickLethalFixture();
            Assert.That(sim.ObjectiveSecured, Is.True);
        }

        [Test]
        public void SimRng_LegendaryRateCapped()
        {
            var rng = SimRng.CreateStream(12345, 19349663);
            var legendary = 0;
            const int rolls = 10000;
            for (var i = 0; i < rolls; i++)
            {
                if (rng.RollRarity(2f) == SimRarity.Legendary) legendary++;
            }
            Assert.That(legendary / (float)rolls, Is.LessThanOrEqualTo(0.05f));
        }

        [Test]
        public void SimMath_DamageMatchesGoldenVector()
        {
            var dmg = SimMath.ComputeDamage(10f, 1.2f, 0.08f, 1f, false, 1.5f, false, 1f, 1f, 25f);
            Assert.That(dmg.ToFloat(), Is.EqualTo(10.368f).Within(0.01f));
        }
    }
}
