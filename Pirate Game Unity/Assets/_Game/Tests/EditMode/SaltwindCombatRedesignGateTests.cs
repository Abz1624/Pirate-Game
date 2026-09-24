using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public sealed class SaltwindCombatRedesignGateTests
    {
        [SetUp]
        public void SetUp() =>
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());

        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            SaltwindCombatAgentStateStore.ClearAll();
            SaltwindHazardTelegraph.ClearAll();
            SaltwindCombatEncounterBudget.ResetForRun();
        }

        [Test]
        public void SpawnResolver_UsesSpawnedDefinition_NotAnchorProfile()
        {
            var shark = ScriptableObject.CreateInstance<EnemyDefinition>();
            shark.id = "shark";
            shark.behavior = EnemyBehavior.DirectChaser;

            var cannon = ScriptableObject.CreateInstance<EnemyDefinition>();
            cannon.id = "cannonboat";
            cannon.behavior = EnemyBehavior.MaintainRange;
            cannon.preferredRange = 13f;

            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.enemy = shark;
            profile.movement.pattern = EnemyMovementPattern.DirectChase;
            profile.attack.pattern = EnemyAttackPattern.Contact;

            var resolved = SpawnProfileResolver.Resolve(profile, cannon, 1, FormationRole.Auto, 0, -1);
            Assert.AreEqual(EnemyMovementPattern.MaintainRange, resolved.movementPattern);
            Assert.AreEqual(EnemyAttackPattern.SingleShot, resolved.attackPattern);
            Assert.Greater(resolved.preferredRange, 10f);
        }

        [Test]
        public void ManualAndScheduledResolve_ParityForSwordfish()
        {
            var swordfish = ScriptableObject.CreateInstance<EnemyDefinition>();
            swordfish.id = "swordfish";
            swordfish.behavior = EnemyBehavior.FastSwarm;

            var manual = SpawnProfileResolver.ResolveForDefinition(swordfish);
            var scheduled = SpawnProfileResolver.Resolve(null, swordfish, 2, FormationRole.Auto, 0, -1);
            Assert.AreEqual(manual.movementPattern, scheduled.movementPattern);
            Assert.AreEqual(manual.chargeWindUp, scheduled.chargeWindUp);
        }

        [Test]
        public void SwivelGuns_RegisteredWithOmniNearestTargeting()
        {
            var db = GameDatabaseLoader.Load();
            CombatWeaponCatalog.ApplyToDatabase(db, CombatExperimentProfile.CreateBuiltIn());
            var swivel = db.GetWeapon("swivel_guns");
            Assert.IsNotNull(swivel);
            Assert.AreEqual(TargetingMode.Nearest, swivel.targeting);
            Assert.IsFalse(swivel.preferElite);
        }

        [Test]
        public void CombatBalanceVersion_IsIncremented()
        {
            Assert.AreEqual(3, SaltwindCombatFeature.CombatBalanceVersion);
        }
    }
}
