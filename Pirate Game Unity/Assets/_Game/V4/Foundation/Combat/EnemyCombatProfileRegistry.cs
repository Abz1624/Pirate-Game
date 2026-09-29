using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Combat
{
    public static class EnemyCombatProfileRegistry
    {
        static readonly Dictionary<string, EnemyCombatProfile> Profiles = Build();

        public static bool TryGet(string enemyId, out EnemyCombatProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(enemyId)) return false;
            return Profiles.TryGetValue(enemyId, out profile);
        }

        public static bool UsesSaltwindBehavior(string enemyId) =>
            SaltwindCombatFeature.IsEnabled && TryGet(enemyId, out _);

        public static EnemySpawnRuntimeParams ResolveRuntimeParams(EnemyDefinition spawnedDef, SpawnProfile encounterProfile)
        {
            if (spawnedDef == null)
                return default;

            EnemySpawnRuntimeParams p;
            if (TryGet(spawnedDef.id, out var combat))
                p = FromProfile(combat, spawnedDef);
            else
                p = SpawnProfileResolver.ResolveLegacyFromDefinition(spawnedDef);

            if (encounterProfile != null)
                SpawnProfileResolver.ApplyEncounterOverrides(ref p, encounterProfile, spawnedDef);

            return p;
        }

        static EnemySpawnRuntimeParams FromProfile(EnemyCombatProfile combat, EnemyDefinition def)
        {
            var p = new EnemySpawnRuntimeParams
            {
                movementPattern = combat.movementPattern,
                attackPattern = combat.attackPattern,
                preferredRange = combat.preferredRange > 0f ? combat.preferredRange : def.preferredRange,
                attackRange = combat.attackRange > 0f ? combat.attackRange : 12f,
                attackWindUp = combat.attackWindUp,
                attackCooldown = combat.attackCooldown,
                projectileCount = Mathf.Max(1, combat.projectileCount),
                spreadRad = combat.spreadDegrees * Mathf.Deg2Rad,
                projectileSpeed = combat.projectileSpeed > 0f ? combat.projectileSpeed : 9f,
                projectileLifetime = 1.6f,
                contactDamageEnabled = combat.contactDamageEnabled,
                chargeWindUp = combat.chargeWindUp,
                chargeDuration = combat.chargeDuration,
                chargeCooldown = combat.chargeCooldown,
                chargeSpeedMult = combat.chargeSpeedMult > 0f ? combat.chargeSpeedMult : 2.5f,
                separationStrength = 1f
            };
            return p;
        }

        static Dictionary<string, EnemyCombatProfile> Build()
        {
            var map = new Dictionary<string, EnemyCombatProfile>();

            void Add(
                string id,
                SaltwindCombatArchetype archetype,
                EnemyMovementPattern move,
                EnemyAttackPattern attack,
                float preferredRange = 8f,
                float attackRange = 12f,
                float windUp = 0.8f,
                float cooldown = 2f,
                int projectiles = 1,
                float spread = 0f,
                float chargeWindUp = 1f,
                float chargeDuration = 0.65f,
                float chargeCooldown = 2f,
                float chargeSpeed = 2.5f,
                bool contact = true)
            {
                map[id] = new EnemyCombatProfile
                {
                    enemyId = id,
                    archetype = archetype,
                    movementPattern = move,
                    attackPattern = attack,
                    preferredRange = preferredRange,
                    attackRange = attackRange,
                    attackWindUp = windUp,
                    attackCooldown = cooldown,
                    projectileCount = projectiles,
                    spreadDegrees = spread,
                    projectileSpeed = 9f,
                    chargeWindUp = chargeWindUp,
                    chargeDuration = chargeDuration,
                    chargeCooldown = chargeCooldown,
                    chargeSpeedMult = chargeSpeed,
                    contactDamageEnabled = contact
                };
            }

            Add("deckhand_dinghy", SaltwindCombatArchetype.DeckhandDinghy, EnemyMovementPattern.Swarm, EnemyAttackPattern.Contact, 6f);
            Add("shark", SaltwindCombatArchetype.Shark, EnemyMovementPattern.DirectChase, EnemyAttackPattern.Contact);
            Add("swordfish", SaltwindCombatArchetype.Swordfish, EnemyMovementPattern.DiveLunge, EnemyAttackPattern.Contact, 10f,
                chargeWindUp: 1f, chargeDuration: 0.65f, chargeCooldown: 1.2f, chargeSpeed: 3f);
            Add("cutthroat_skiff", SaltwindCombatArchetype.CutthroatSkiff, EnemyMovementPattern.Flank, EnemyAttackPattern.Contact, 9f);
            Add("hammerhead", SaltwindCombatArchetype.Hammerhead, EnemyMovementPattern.TelegraphedCharge, EnemyAttackPattern.Contact,
                chargeWindUp: 1.2f, chargeDuration: 1f, chargeCooldown: 2f, chargeSpeed: 2.5f);
            Add("cannonboat", SaltwindCombatArchetype.Cannonboat, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.SingleShot,
                14f, 14f, 0.8f, 2f);
            Add("manta_ray", SaltwindCombatArchetype.MantaRay, EnemyMovementPattern.Strafe, EnemyAttackPattern.SpreadVolley,
                10f, 10f, 0.9f, 2.2f, 3, 18f);

            Add("needle_sloop", SaltwindCombatArchetype.NeedleSloop, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.SingleShot,
                18f, 20f, 1f, 2.5f, 1, 0f, contact: false);
            Add("rammer_raft", SaltwindCombatArchetype.RammerRaft, EnemyMovementPattern.TelegraphedCharge, EnemyAttackPattern.Contact,
                chargeWindUp: 1.4f, chargeDuration: 1.1f, chargeCooldown: 2.2f, chargeSpeed: 2.8f);
            Add("ram_raider", SaltwindCombatArchetype.RamRaider, EnemyMovementPattern.TelegraphedCharge, EnemyAttackPattern.Contact,
                chargeWindUp: 1f, chargeDuration: 0.75f, chargeCooldown: 2f, chargeSpeed: 3f);
            Add("bomb_boat", SaltwindCombatArchetype.BombBoat, EnemyMovementPattern.DirectChase, EnemyAttackPattern.SuicideExplosion,
                4f, 3f, 1.2f, 99f, contact: false);
            Add("bomb_ketch", SaltwindCombatArchetype.BombKetch, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.AreaDenial,
                16f, 16f, 0.5f, 4f, 1, 0f, contact: false);
            Add("powder_barge", SaltwindCombatArchetype.PowderBarge, EnemyMovementPattern.DirectChase, EnemyAttackPattern.AreaDenial,
                12f, contact: false);
            Add("boarding_barge", SaltwindCombatArchetype.BoardingBarge, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.SingleShot,
                7.5f, 9f, 1f, 3f, contact: false);
            Add("net_cutter", SaltwindCombatArchetype.NetCutter, EnemyMovementPattern.Flank, EnemyAttackPattern.AreaDenial,
                10f, 6f, 1.2f, 4f, contact: false);
            Add("repair_tender", SaltwindCombatArchetype.RepairTender, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.None,
                8f, 8f, 1f, 4f, contact: false);
            Add("shield_frigate", SaltwindCombatArchetype.ShieldFrigate, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.BurstVolley,
                10f, 12f, 0.8f, 5f, contact: false);
            Add("gunner_cog", SaltwindCombatArchetype.GunnerCog, EnemyMovementPattern.MaintainRange, EnemyAttackPattern.BurstVolley,
                12f, 14f, 0.6f, 3f, 3, 0f);
            Add("treasure_raider", SaltwindCombatArchetype.TreasureRaider, EnemyMovementPattern.Swarm, EnemyAttackPattern.Contact, 14f);
            Add("kraken_spawn", SaltwindCombatArchetype.KrakenSpawn, EnemyMovementPattern.DirectChase, EnemyAttackPattern.AreaDenial,
                8f, 8f, 1.2f, 6f, 2, 0f, contact: false);

            map["reef_crab_miniboss"] = new EnemyCombatProfile
            {
                enemyId = "reef_crab_miniboss",
                archetype = SaltwindCombatArchetype.ReefCrabMiniboss,
                movementPattern = EnemyMovementPattern.MaintainRange,
                attackPattern = EnemyAttackPattern.Contact,
                preferredRange = 6f,
                contactDamageEnabled = true
            };

            map["drowned_pursuer"] = new EnemyCombatProfile
            {
                enemyId = "drowned_pursuer",
                archetype = SaltwindCombatArchetype.DrownedPursuer,
                movementPattern = EnemyMovementPattern.DirectChase,
                attackPattern = EnemyAttackPattern.Contact,
                preferredRange = 8f
            };

            map["drowned_harrier"] = new EnemyCombatProfile
            {
                enemyId = "drowned_harrier",
                archetype = SaltwindCombatArchetype.DrownedHarrier,
                movementPattern = EnemyMovementPattern.MaintainRange,
                attackPattern = EnemyAttackPattern.BurstVolley,
                preferredRange = 16f,
                attackRange = 18f,
                attackWindUp = 1f,
                attackCooldown = 2.5f,
                projectileCount = 2,
                contactDamageEnabled = false
            };

            var eliteHammer = map["hammerhead"];
            map["elite_hammerhead"] = new EnemyCombatProfile
            {
                enemyId = "hammerhead",
                archetype = SaltwindCombatArchetype.EliteHammerhead,
                movementPattern = eliteHammer.movementPattern,
                attackPattern = eliteHammer.attackPattern,
                preferredRange = eliteHammer.preferredRange,
                chargeWindUp = eliteHammer.chargeWindUp,
                chargeDuration = eliteHammer.chargeDuration,
                chargeCooldown = 3f,
                chargeSpeedMult = eliteHammer.chargeSpeedMult,
                eliteHammerheadSecondCharge = true
            };

            var kraken = map["kraken_spawn"];
            map["elite_kraken_spawn"] = new EnemyCombatProfile
            {
                enemyId = "kraken_spawn",
                archetype = SaltwindCombatArchetype.EliteKrakenSpawn,
                attackPattern = EnemyAttackPattern.AreaDenial,
                movementPattern = kraken.movementPattern,
                preferredRange = kraken.preferredRange,
                attackWindUp = kraken.attackWindUp,
                attackCooldown = kraken.attackCooldown,
                projectileCount = 3,
                eliteKrakenSequentialTraps = true
            };

            return map;
        }
    }
}
