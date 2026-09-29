using System;
using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// The run's enemy combat profiles (M14; was V3's static EnemyCombatProfileRegistry, which LegacyCombatProfileOverride
    /// filled from config and restored afterwards). One profile per registered archetype/variant combat block, copied per
    /// run so the V3 elite-charge latch (R-100) changes this run's copy only; config edits recopy the edited block (as the
    /// override rewrote the registry profile). The profile type is the shared data type the combat code reads (M15 moves it).
    /// </summary>
    public sealed class EnemyCombatProfiles : IDisposable
    {
        readonly ConfigSetRuntime _config;
        readonly Dictionary<string, EnemyCombatProfile> _byId = new Dictionary<string, EnemyCombatProfile>();

        public EnemyCombatProfiles(ConfigSetRuntime config)
        {
            _config = config;
            if (_config == null) return;
            foreach (var a in _config.All<EnemyArchetypeConfig>()) Apply(a.enemyId, a.combat);
            foreach (var v in _config.All<EnemyVariantConfig>()) Apply(v.variantId, v.combat);
            _config.ConfigChanged += OnConfigChanged;
        }

        public int Count => _byId.Count;

        public bool TryGet(string enemyId, out EnemyCombatProfile profile)
        {
            profile = null;
            return !string.IsNullOrEmpty(enemyId) && _byId.TryGetValue(enemyId, out profile);
        }

        void Apply(string id, ArchetypeCombat block)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (block == null || !block.registered) { _byId.Remove(id); return; }
            if (!_byId.TryGetValue(id, out var profile)) _byId[id] = profile = new EnemyCombatProfile { enemyId = id };
            FieldMirror.CopyFields(block, profile);
        }

        void OnConfigChanged(ConfigChange change)
        {
            switch (_config.Get<V4ConfigAsset>(change.configId))
            {
                case EnemyArchetypeConfig a: Apply(a.enemyId, a.combat); break;
                case EnemyVariantConfig v: Apply(v.variantId, v.combat); break;
            }
        }

        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            _byId.Clear();
        }
    }

    /// <summary>
    /// Spawn movement/attack parameters (M14; port of V3's SpawnProfileResolver and EnemyCombatProfileRegistry
    /// .ResolveRuntimeParams, was LegacyEnemySpawnParams). Same rules and float steps; V3's enemy-id rules are archetype
    /// flags (<see cref="ArchetypeWorldRules.alwaysRanged"/>, <see cref="ArchetypeWorldRules.defaultAttackSingleShot"/>).
    /// </summary>
    public sealed class EnemySpawnParamResolver : IEnemySpawnParams
    {
        readonly EnemyCombatProfiles _profiles;
        readonly Dictionary<string, ArchetypeWorldRules> _rules = new Dictionary<string, ArchetypeWorldRules>();
        static readonly SpawnMovementOverrides DefaultMovement = new SpawnMovementOverrides();
        static readonly SpawnAttackOverrides DefaultAttack = new SpawnAttackOverrides();

        public EnemySpawnParamResolver(EnemyCombatProfiles profiles, ConfigSetRuntime config)
        {
            _profiles = profiles;
            if (config == null) return;
            // V3 matched the exact id (a variant never inherits its base's rules).
            foreach (var a in config.All<EnemyArchetypeConfig>()) if (!string.IsNullOrEmpty(a.enemyId) && a.world != null) _rules[a.enemyId] = a.world;
            foreach (var v in config.All<EnemyVariantConfig>()) if (!string.IsNullOrEmpty(v.variantId) && v.world != null) _rules[v.variantId] = v.world;
        }

        /// <summary>The Saltwind combat behaviour (V3: on whenever the stage profile was active, so in every V4 run and lab).</summary>
        public bool CombatEnabled { get; set; } = true;

        public bool HasCombatProfile(EnemyDefinition def) => def != null && _profiles != null && _profiles.TryGet(def.id, out _);
        public bool TryGetCombatProfile(string enemyId, out EnemyCombatProfile profile)
        {
            profile = null;
            return _profiles != null && _profiles.TryGet(enemyId, out profile);
        }
        public bool UsesCombatBehavior(string enemyId) => CombatEnabled && _profiles != null && _profiles.TryGet(enemyId, out _);

        public EnemySpawnRuntimeParams ResolveForDefinition(EnemyDefinition def) => Resolve(null, def, 0, FormationRole.Auto, 0, -1);

        public EnemySpawnRuntimeParams Resolve(SpawnProfile profile, EnemyDefinition spawnedEnemy, int profileInstanceId, FormationRole role, int slot, int leaderId)
        {
            var identityEnemy = spawnedEnemy ?? profile?.enemy;
            EnemySpawnRuntimeParams p;
            if (CombatEnabled && identityEnemy != null)
                p = ResolveRuntimeParams(identityEnemy, profile);
            else
            {
                // V3 quirk kept: BuildFromProfileAnchor pointed the shared profile at the identity enemy.
                if (profile != null && identityEnemy != null) profile.enemy = identityEnemy;
                p = BuildFromBlocks(profile != null ? profile.movement : DefaultMovement, profile != null ? profile.attack : DefaultAttack, profile != null ? profile.enemy : identityEnemy);
            }

            p.profileInstanceId = profileInstanceId;
            p.formationRole = role;
            p.formationSlot = slot;
            p.formationLeaderAgentId = leaderId;
            if (profile != null)
            {
                p.specialBehavior = profile.specialBehavior;
                p.formation = profile.formation;
                p.summon = profile.summon;
                p.tether = profile.tether;
                p.supportAura = profile.supportAura;
                p.phaseDash = profile.phaseDash;
                p.terrainAmbush = profile.terrainAmbush;
                p.adaptive = profile.adaptive;
                p.adaptiveSpeedMult = 1f;
                p.ambushHidden = profile.specialBehavior == EnemySpecialBehavior.TerrainAmbush && profile.terrainAmbush.trigger != TerrainAmbushTrigger.OnSpawn;
                p.dashCooldownTimer = profile.phaseDash.dashCooldownSeconds * 0.5f;
                p.summonTimer = profile.summon.spawnIntervalSeconds;
            }
            return p;
        }

        /// <summary>V3 EnemyCombatProfileRegistry.ResolveRuntimeParams: the combat profile, else the definition's legacy mapping;
        /// then the encounter overrides.</summary>
        EnemySpawnRuntimeParams ResolveRuntimeParams(EnemyDefinition spawnedDef, SpawnProfile encounterProfile)
        {
            if (spawnedDef == null) return default;
            var p = TryGetCombatProfile(spawnedDef.id, out var combat)
                ? FromProfile(combat, spawnedDef)
                : BuildFromBlocks(DefaultMovement, DefaultAttack, spawnedDef); // V3 ResolveLegacyFromDefinition (a fresh profile)
            if (encounterProfile != null) ApplyEncounterOverrides(ref p, encounterProfile, spawnedDef);
            return p;
        }

        static EnemySpawnRuntimeParams FromProfile(EnemyCombatProfile combat, EnemyDefinition def) => new EnemySpawnRuntimeParams
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

        /// <summary>V3 SpawnProfileResolver.ApplyEncounterOverrides (only when the profile's enemy is the spawned one).</summary>
        static void ApplyEncounterOverrides(ref EnemySpawnRuntimeParams p, SpawnProfile profile, EnemyDefinition spawnedEnemy)
        {
            if (profile == null) return;
            if (profile.enemy != null && spawnedEnemy != null && profile.enemy.id != spawnedEnemy.id) return;
            if (profile.movement.pattern != EnemyMovementPattern.UseDefinition) p.movementPattern = profile.movement.pattern;
            if (profile.attack.pattern != EnemyAttackPattern.UseDefinition) p.attackPattern = profile.attack.pattern;
            if (profile.movement.preferredRange > 0f) p.preferredRange = profile.movement.preferredRange;
            if (profile.movement.chargeWindUpSeconds > 0f) p.chargeWindUp = profile.movement.chargeWindUpSeconds;
            if (profile.movement.chargeDurationSeconds > 0f) p.chargeDuration = profile.movement.chargeDurationSeconds;
            if (profile.movement.chargeCooldownSeconds > 0f) p.chargeCooldown = profile.movement.chargeCooldownSeconds;
            if (profile.movement.chargeSpeedMultiplier > 0f) p.chargeSpeedMult = profile.movement.chargeSpeedMultiplier;
            if (profile.attack.attackRange > 0f) p.attackRange = profile.attack.attackRange;
            if (profile.attack.windUpSeconds > 0f) p.attackWindUp = profile.attack.windUpSeconds;
            if (profile.attack.cooldownSeconds > 0f) p.attackCooldown = profile.attack.cooldownSeconds;
        }

        /// <summary>V3 SpawnProfileResolver.BuildFromProfileAnchor over a profile's movement/attack blocks and enemy.</summary>
        EnemySpawnRuntimeParams BuildFromBlocks(SpawnMovementOverrides m, SpawnAttackOverrides a, EnemyDefinition enemy) => new EnemySpawnRuntimeParams
        {
            movementPattern = ResolveMovement(m, enemy),
            attackPattern = ResolveAttack(a, enemy),
            preferredRange = m.preferredRange,
            interceptLead = m.interceptLead,
            orbitRadius = m.orbitRadius,
            orbitDirection = m.orbitDirection,
            flankAngleRad = m.flankAngleDegrees * Mathf.Deg2Rad,
            chargeWindUp = m.chargeWindUpSeconds,
            chargeDuration = m.chargeDurationSeconds,
            chargeCooldown = m.chargeCooldownSeconds,
            chargeSpeedMult = m.chargeSpeedMultiplier,
            separationStrength = m.separationStrength,
            contactDamageEnabled = a.contactDamageEnabled,
            attackRange = a.attackRange,
            attackWindUp = a.windUpSeconds,
            attackCooldown = a.cooldownSeconds,
            projectileCount = Mathf.Max(1, a.projectileCount),
            spreadRad = a.spreadDegrees * Mathf.Deg2Rad,
            projectileSpeed = a.projectileSpeed,
            projectileLifetime = a.projectileLifetime,
            burstShots = Mathf.Max(1, a.burstShots),
            burstGap = a.burstGapSeconds,
            areaRadius = a.areaRadius,
            areaDuration = a.areaDurationSeconds,
            adaptiveSpeedMult = 1f
        };

        static EnemyMovementPattern ResolveMovement(SpawnMovementOverrides m, EnemyDefinition enemy) =>
            m.pattern != EnemyMovementPattern.UseDefinition ? m.pattern : MapLegacyBehavior(enemy != null ? enemy.behavior : EnemyBehavior.DirectChaser);

        EnemyAttackPattern ResolveAttack(SpawnAttackOverrides a, EnemyDefinition enemy)
        {
            if (a.pattern != EnemyAttackPattern.UseDefinition) return a.pattern;
            var behavior = enemy != null ? enemy.behavior : EnemyBehavior.DirectChaser;
            if (enemy != null && Rules(enemy).defaultAttackSingleShot) return EnemyAttackPattern.SingleShot;
            if (!SupportsRangedAttacks(enemy)) return EnemyAttackPattern.Contact;
            if (behavior == EnemyBehavior.SuicideApproach) return EnemyAttackPattern.SuicideExplosion;
            if (behavior == EnemyBehavior.MaintainRange || behavior == EnemyBehavior.FormationRanged) return EnemyAttackPattern.SingleShot;
            return EnemyAttackPattern.Contact;
        }

        /// <summary>V3 EnemyRangedAttackRules.SupportsRangedAttacks (its id list is the archetypes' alwaysRanged flag).</summary>
        bool SupportsRangedAttacks(EnemyDefinition def)
        {
            if (def == null) return false;
            if (Rules(def).alwaysRanged) return true;
            return def.category == EnemyCategory.Ship
                && (def.behavior == EnemyBehavior.MaintainRange || def.behavior == EnemyBehavior.FormationRanged || def.behavior == EnemyBehavior.StationaryTurret);
        }

        static readonly ArchetypeWorldRules NoRules = new ArchetypeWorldRules();
        ArchetypeWorldRules Rules(EnemyDefinition def) => def != null && _rules.TryGetValue(def.id, out var r) ? r : NoRules;

        static EnemyMovementPattern MapLegacyBehavior(EnemyBehavior behavior) => behavior switch
        {
            EnemyBehavior.FlankingChaser => EnemyMovementPattern.Flank,
            EnemyBehavior.TelegraphedCharge => EnemyMovementPattern.TelegraphedCharge,
            EnemyBehavior.MaintainRange => EnemyMovementPattern.MaintainRange,
            EnemyBehavior.FormationRanged => EnemyMovementPattern.MaintainRange,
            EnemyBehavior.SuicideApproach => EnemyMovementPattern.DirectChase,
            EnemyBehavior.StrafeBurst => EnemyMovementPattern.Strafe,
            EnemyBehavior.FastSwarm => EnemyMovementPattern.Swarm,
            EnemyBehavior.DiveLunge => EnemyMovementPattern.DiveLunge,
            EnemyBehavior.DriftPulse => EnemyMovementPattern.Drift,
            EnemyBehavior.StationaryTurret => EnemyMovementPattern.Stationary,
            EnemyBehavior.TankChaser => EnemyMovementPattern.DirectChase,
            _ => EnemyMovementPattern.DirectChase
        };
    }
}
