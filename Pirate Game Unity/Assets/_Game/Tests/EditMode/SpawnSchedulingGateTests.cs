using NUnit.Framework;

namespace HighSeas.Tests
{
    public sealed class SpawnModifierStackTests
    {
        [Test]
        public void Modifiers_ComposeMultiplicatively_AndExpire()
        {
            var stack = new Enemies.SpawnModifierStack();
            stack.Reset(1f);
            stack.SetTimedModifier("swarm", 1.25f, 0f, 30f);
            stack.SetTimedModifier("boss", 0.5f, 0f, 20f);
            Assert.AreEqual(0.625f, stack.GetRateMultiplier(10f), 0.001f);
            Assert.AreEqual(1.25f, stack.GetRateMultiplier(25f), 0.001f);
            Assert.AreEqual(1f, stack.GetRateMultiplier(35f), 0.001f);
        }

        [Test]
        public void RemoveModifier_StopsApplying()
        {
            var stack = new Enemies.SpawnModifierStack();
            stack.SetTimedModifier("pulse", 2f, 0f, 60f);
            stack.Remove("pulse");
            Assert.AreEqual(1f, stack.GetRateMultiplier(5f), 0.001f);
        }
    }

    public sealed class AmbientSpawnBudgetTests
    {
        [Test]
        public void BlockedAccumulation_Capped_AndDrainsOneAtATime()
        {
            var budget = new Enemies.AmbientSpawnBudget { MaxCarry = 3f };
            for (var i = 0; i < 100; i++)
                budget.AccumulateWhileBlocked(1f, 1f, blocked: true);
            Assert.AreEqual(3f, budget.Accumulated, 0.001f);
            Assert.AreEqual(1, budget.TryConsumeOne());
            Assert.AreEqual(1, budget.TryConsumeOne());
            Assert.AreEqual(1, budget.TryConsumeOne());
            Assert.AreEqual(0, budget.TryConsumeOne());
            Assert.AreEqual(0f, budget.Accumulated, 0.001f);
        }

        [Test]
        public void Unblocked_DoesNotAccumulate()
        {
            var budget = new Enemies.AmbientSpawnBudget();
            budget.AccumulateWhileBlocked(5f, 2f, blocked: false);
            Assert.AreEqual(0f, budget.Accumulated);
        }
    }
}
