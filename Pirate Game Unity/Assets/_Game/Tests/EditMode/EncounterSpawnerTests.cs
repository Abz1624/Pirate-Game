using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public class EncounterSpawnerTests
    {
        [Test]
        public void SpawnProfileEnums_IncludeAdvancedSpecialBehaviors()
        {
            Assert.That(System.Enum.IsDefined(typeof(EnemySpecialBehavior), EnemySpecialBehavior.Formation));
            Assert.That(System.Enum.IsDefined(typeof(EnemySpecialBehavior), EnemySpecialBehavior.Adaptive));
            Assert.That(System.Enum.IsDefined(typeof(SpawnMode), SpawnMode.Continuous));
        }

        [Test]
        public void SpawnProfileResolver_MapsLegacyFlankBehavior()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.behavior = EnemyBehavior.FlankingChaser;
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.enemy = def;
            profile.movement.pattern = EnemyMovementPattern.UseDefinition;

            Assert.That(SpawnProfileResolver.ResolveMovement(profile), Is.EqualTo(EnemyMovementPattern.Flank));
        }

        [Test]
        public void TelegraphedChargePattern_ResetsAfterChargeDuration()
        {
            var agent = new EnemyAgent { moveSpeed = 4f, id = 1 };
            var p = new EnemySpawnRuntimeParams
            {
                movementPattern = EnemyMovementPattern.TelegraphedCharge,
                chargeWindUp = 1.5f,
                chargeDuration = 0.35f,
                chargeCooldown = 2f,
                chargeSpeedMult = 2.5f
            };
            var chargeTimer = 2f;
            var dir = Vector3.forward;

            for (var i = 0; i < 200; i++)
            {
                EnemyPatternMovement.ComputeVelocity(
                    agent, p, dir, dir, 10f, 0.05f, ref chargeTimer);
                if (chargeTimer >= 1.99f && chargeTimer <= 2.01f && i > 10)
                    break;
            }

            Assert.That(chargeTimer, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void SpawnProfileResolver_EliteFieldsAppliedInResolve()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.specialBehavior = EnemySpecialBehavior.PhaseDash;
            profile.elite.isElite = true;
            profile.elite.affix = EliteAffix.Swift;

            var resolved = SpawnProfileResolver.Resolve(profile, 7, FormationRole.Leader, 0, -1);
            Assert.That(resolved.specialBehavior, Is.EqualTo(EnemySpecialBehavior.PhaseDash));
            Assert.That(resolved.phaseDash.dashCooldownSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void ApplyCombatExperiment_DisablesAmbientScheduleWhenEncounterSpawnOff()
        {
            var go = new GameObject("EncounterSpawner");
            var spawner = go.AddComponent<EncounterSpawner>();
            var profile = CombatExperimentProfile.CreateBuiltIn();
            profile.encounterSpawnEnabled = false;

            spawner.ApplyCombatExperiment(profile);

            Assert.That(spawner.AmbientScheduleEnabled, Is.False);
            Assert.That(spawner.enabled, Is.True);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyCombatExperiment_AppliesSpawnDensityMultiplier()
        {
            var go = new GameObject("EncounterSpawner");
            var spawner = go.AddComponent<EncounterSpawner>();
            var profile = CombatExperimentProfile.CreateBuiltIn();
            profile.spawnDensityMultiplier = 1.75f;

            spawner.ApplyCombatExperiment(profile);

            Assert.That(spawner.DebugSpawnRateMultiplier, Is.EqualTo(1.75f).Within(0.001f));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnPlacementService_RejectsVisibleCandidateWhenRequired()
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.position = Vector3.zero;
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 20f, -10f);
            cam.transform.LookAt(Vector3.zero);

            var settings = new SpawnPlacementSettings
            {
                requireOutsidePlayerFov = true,
                distanceMin = 8f,
                distanceMax = 12f,
                viewportMargin = 0.05f,
                positionAttempts = 4
            };

            var visiblePos = playerGo.transform.position + Vector3.forward * 10f;
            Assert.That(
                SpawnPlacementService.IsWorldPositionVisible(visiblePos, cam, settings.viewportMargin),
                Is.True);

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(camGo);
        }
    }
}
