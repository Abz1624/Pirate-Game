using HighSeas.Combat;
using NUnit.Framework;

namespace HighSeas.Tests
{
    public class SpawnDirectorTests
    {
        [Test]
        public void SpawnPackRegistry_ReturnsNonEmptyPacks()
        {
            Assert.That(SpawnPackRegistry.GetPack(0f).Count, Is.GreaterThan(0));
            Assert.That(SpawnPackRegistry.GetPack(8f).Count, Is.GreaterThan(0));
        }

        [Test]
        public void SpawnPackRegistry_LateMixIncludesSpecialists()
        {
            var pack = SpawnPackRegistry.GetPack(8.5f);
            var hasSpecialist = false;
            for (var i = 0; i < pack.Count; i++)
            {
                if (pack[i].EnemyId == "repair_tender" || pack[i].EnemyId == "powder_barge")
                    hasSpecialist = true;
            }
            Assert.That(hasSpecialist, Is.True);
        }
    }
}
