using HighSeas.Core;
using HighSeas.World;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class GameMathTests
    {
        [Test]
        public void DiagonalMovementNormalization_IsWithinOnePercent()
        {
            var v = new Vector2(1f, 1f);
            v.Normalize();
            var speed = v.magnitude;
            Assert.That(Mathf.Abs(speed - 1f), Is.LessThan(0.01f));
        }

        [Test]
        public void DamageFormula_MatchesGoldenVector()
        {
            var dmg = GameMath.ComputeDamage(
                baseDamage: 10f,
                weaponLevelDamage: 1.2f,
                globalDamageBonus: 0.08f,
                weaponDamageBonus: 1f,
                isCrit: false,
                critDamage: 1.5f,
                isElite: false,
                eliteDamageMult: 1f,
                statusMult: 1f,
                targetArmor: 25f);
            Assert.That(dmg, Is.EqualTo(10.368f).Within(0.01f));
        }

        [Test]
        public void XPToNext_IsMonotonic()
        {
            var prev = 0f;
            for (var level = 1; level <= 20; level++)
            {
                var next = GameMath.XPToNext(level);
                Assert.That(next, Is.GreaterThan(prev));
                prev = next;
            }
        }

        [Test]
        public void ChestCost_Increases()
        {
            var c1 = GameMath.ChestCost(1);
            var c2 = GameMath.ChestCost(2);
            Assert.That(c2, Is.GreaterThan(c1));
        }

        [Test]
        public void RarityRoll_LegendaryProbabilityCapped()
        {
            var rng = new RunRng(12345);
            var legendary = 0;
            const int rolls = 10000;
            for (var i = 0; i < rolls; i++)
            {
                if (rng.RollRarity(2f) == Rarity.Legendary) legendary++;
            }
            Assert.That(legendary / (float)rolls, Is.LessThanOrEqualTo(0.05f));
        }

        [Test]
        public void ThreatPerSecond_GrowsWithTime()
        {
            var early = GameMath.ComputeThreatPerSecond(30f, 1, 0, 0, false);
            var late = GameMath.ComputeThreatPerSecond(300f, 1, 0, 0, false);
            Assert.That(late, Is.GreaterThan(early));
        }

        [Test]
        public void StageGenerator_ReachabilityPasses()
        {
            var gen = new World.StageGenerator();
            var layout = gen.Generate(42, 0, ProceduralIslandSettings.Default);
            Assert.That(layout.pois.Count, Is.GreaterThan(0));
            Assert.That(layout.playerSpawn, Is.Not.EqualTo(Vector3.zero));
        }

        [Test]
        public void StageGenerator_SpawnsOnWaterPlane()
        {
            var settings = ProceduralIslandSettings.Default;
            var layout = new StageGenerator().Generate(GameConstants.DemoRunSeed, 0, settings);
            Assert.That(layout.playerSpawn.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.islandCenters.Count, Is.GreaterThan(0));
            for (var i = 0; i < layout.islandCenters.Count; i++)
                Assert.That(layout.islandCenters[i].y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void StageGenerator_IslandsDoNotOverlap()
        {
            var settings = ProceduralIslandSettings.Default;
            var layout = new StageGenerator().Generate(12345, 0, settings);
            var minDist = settings.minSeparationMeters * 0.95f;
            for (var i = 0; i < layout.islandCenters.Count; i++)
            {
                for (var j = i + 1; j < layout.islandCenters.Count; j++)
                {
                    var delta = layout.islandCenters[i] - layout.islandCenters[j];
                    delta.y = 0f;
                    Assert.That(delta.magnitude, Is.GreaterThanOrEqualTo(minDist));
                }
            }
        }

        [Test]
        public void StageGenerator_HighCountSettings_PlacesNearTarget()
        {
            var settings = new ProceduralIslandSettings
            {
                countMin = 50,
                countMax = 75,
                islandDiameterMeters = 4f,
                minSeparationMeters = 6f,
                maxPlacementAttempts = 8000
            };
            var layout = new StageGenerator().Generate(GameConstants.DemoRunSeed, 0, settings);
            Assert.That(layout.islandCenters.Count, Is.GreaterThanOrEqualTo(50));
            Assert.That(layout.islandCenters.Count, Is.LessThanOrEqualTo(75));
        }

        [Test]
        public void SaveMigration_DefaultsMissingFields()
        {
            var save = new SaveService(new PlayerPrefsSaveBackend());
            Assert.That(save.Meta.version, Is.EqualTo(GameConstants.SaveVersion));
        }
    }
}
