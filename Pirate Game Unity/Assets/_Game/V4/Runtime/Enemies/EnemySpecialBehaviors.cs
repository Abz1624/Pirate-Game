using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Port of EnemySpecialBehaviorSystem (formation, summon, tether, aura, phase dash, ambush, adaptive), per run. No V3 data uses these.</summary>
    public sealed partial class EnemySpecialBehaviors
    {
        readonly EnemyCombatServices _services;
        EnemySimulation Sim => _services.Simulation;
        HazardTelegraphs Hazards => _services.Hazards;
        EncounterBudget Budget => _services.Budget;
        IEnemyEffects Effects => _services.Effects;
        HostileProjectiles Shots => _services.Shots;
        bool CombatEnabled => _services.CombatEnabled;
        CombatAgentState State(int unitId, EnemyDefinition def) => _services.State(unitId, def);

        public EnemySpecialBehaviors(EnemyCombatServices services) { _services = services; }

        /// <summary>Runs before pattern movement (adaptive overrides, ambush reveal, non-movement modules).</summary>
        public void TickPreMovement(
            EnemySimulation world,
            int agentIndex,
            ref EnemyUnit agent,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            ref var p = ref agent.patternParams;
            if (p.specialBehavior == EnemySpecialBehavior.None)
                return;

            switch (p.specialBehavior)
            {
                case EnemySpecialBehavior.Summon:
                    TickSummon(world, agentIndex, ref agent, ref p, dt);
                    break;
                case EnemySpecialBehavior.TetherBeam:
                    TickTether(ref agent, ref p, dt, playerPos, playerDamage);
                    break;
                case EnemySpecialBehavior.SupportAura:
                    TickSupportAura(world, agentIndex, ref agent, ref p, dt);
                    break;
                case EnemySpecialBehavior.TerrainAmbush:
                    TickTerrainAmbush(ref agent, ref p, dt, playerPos);
                    break;
                case EnemySpecialBehavior.Adaptive:
                    TickAdaptive(ref p, dt);
                    break;
            }
        }

        /// <summary>Runs after pattern movement so formation slots and phase dash override base velocity.</summary>
        public void TickPostMovement(
            EnemySimulation world,
            ref EnemyUnit agent,
            ref EnemySpawnRuntimeParams p,
            float dt,
            Vector3 playerPos)
        {
            if (p.specialBehavior == EnemySpecialBehavior.None)
                return;

            switch (p.specialBehavior)
            {
                case EnemySpecialBehavior.Formation:
                    TickFormation(world, ref agent, ref p, playerPos);
                    break;
                case EnemySpecialBehavior.PhaseDash:
                    TickPhaseDash(ref agent, ref p, dt, playerPos);
                    break;
            }
        }

        void TickFormation(EnemySimulation world, ref EnemyUnit agent, ref EnemySpawnRuntimeParams p, Vector3 playerPos)
        {
            if (p.formationRole != FormationRole.Follower || p.formationLeaderAgentId <= 0)
                return;

            if (!world.TryGetAgentByUnitId(p.formationLeaderAgentId, out var leaderIndex))
            {
                if (p.formation.breakOnLeaderDeath)
                    p.formationRole = FormationRole.Auto;
                return;
            }

            ref var leader = ref world.Agents[leaderIndex];
            var offset = FormationOffset(p.formationSlot, p.formation);
            var target = leader.position + offset;
            var toTarget = target - agent.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                agent.velocity = toTarget.normalized * agent.moveSpeed * 0.9f;

            if (p.formation.breakOnPlayerProximity)
            {
                var dist = Vector3.Distance(agent.position, playerPos);
                if (dist <= p.formation.breakProximityRadius)
                    p.formationRole = FormationRole.Auto;
            }
        }

        Vector3 FormationOffset(int slot, FormationBehaviorSettings settings)
        {
            var spacing = settings.slotSpacing;
            return settings.shape switch
            {
                FormationShape.V => new Vector3((slot - 1) * spacing * 0.6f, 0f, -slot * spacing * 0.4f),
                FormationShape.Wedge => new Vector3((slot - 1) * spacing * 0.5f, 0f, -slot * spacing * 0.35f),
                FormationShape.Box => new Vector3((slot % 2 == 0 ? -1f : 1f) * spacing, 0f, (slot / 2) * spacing * 0.5f),
                _ => new Vector3((slot - 1) * spacing, 0f, 0f)
            };
        }

        void TickSummon(EnemySimulation world, int agentIndex, ref EnemyUnit agent, ref EnemySpawnRuntimeParams p, float dt)
        {
            if (p.summon.minionProfile == null)
                return;

            p.summonTimer -= dt;
            if (p.summonTimer > 0f)
                return;
            p.summonTimer = p.summon.spawnIntervalSeconds;

            if (p.aliveMinionCount >= p.summon.maxAliveMinions)
                return;

            // V3 queued the minions on EncounterSpawner (switched off since M7, so they never spawn); no V3 data summons.
            if (!_services.TrySummon(p.summon.minionProfile, p.summon.minionsPerWave))
                return;
            p.aliveMinionCount += p.summon.minionsPerWave;
        }

        void TickTether(
            ref EnemyUnit agent,
            ref EnemySpawnRuntimeParams p,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            var dist = HorizontalDistance(agent.position, playerPos);
            if (dist > p.tether.breakRange)
            {
                p.tetherActiveTimer = 0f;
                return;
            }

            if (dist > p.tether.maxRange)
                return;

            if (p.tether.breakOnIslandBlock && IslandObstacles.IsAvailable)
            {
                var origin = agent.position + Vector3.up * 0.5f;
                var target = playerPos + Vector3.up * 0.5f;
                if (Physics.Linecast(origin, target, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
                {
                    p.tetherActiveTimer = 0f;
                    return;
                }
            }

            p.tetherActiveTimer += dt;
            if (p.tetherActiveTimer < p.tether.rampUpSeconds)
                return;

            p.tetherTickTimer -= dt;
            if (p.tetherTickTimer > 0f)
                return;
            p.tetherTickTimer = p.tether.tickIntervalSeconds;
            _services.HitPlayer(playerDamage, p.tether.tickDamage, agent.position);
        }

        void TickSupportAura(EnemySimulation world, int agentIndex, ref EnemyUnit agent, ref EnemySpawnRuntimeParams p, float dt)
        {
            if (p.supportAura.disableWhenDamaged && agent.hp < agent.maxHp * 0.98f)
                return;

            p.auraPulseTimer -= dt;
            if (p.auraPulseTimer > 0f)
                return;
            p.auraPulseTimer = p.supportAura.pulseIntervalSeconds;

            var agents = world.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active || i == agentIndex)
                    continue;
                var delta = agents[i].position - agent.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > p.supportAura.radius * p.supportAura.radius)
                    continue;

                ref var ally = ref agents[i];
                var heal = p.supportAura.healPerSecond * p.supportAura.pulseIntervalSeconds;
                heal += ally.maxHp * p.supportAura.healPercentMaxHpPerSecond * p.supportAura.pulseIntervalSeconds;
                if (heal > 0f)
                    ally.hp = Mathf.Min(ally.maxHp, ally.hp + heal);
                ally.moveSpeed *= p.supportAura.allySpeedMultiplier;
            }
        }

        void TickPhaseDash(ref EnemyUnit agent, ref EnemySpawnRuntimeParams p, float dt, Vector3 playerPos)
        {
            if (p.dashPauseTimer > 0f)
            {
                p.dashPauseTimer -= dt;
                agent.velocity = Vector3.zero;
                return;
            }

            if (p.dashActiveTimer > 0f)
            {
                p.dashActiveTimer -= dt;
                var dashDir = ResolveDashDir(agent.position, playerPos, p.phaseDash.axis, agent.id);
                agent.velocity = dashDir * p.phaseDash.dashSpeed;
                if (p.dashActiveTimer <= 0f)
                {
                    p.dashPauseTimer = p.phaseDash.postDashPauseSeconds;
                    p.dashConsecutiveCount++;
                    if (p.dashConsecutiveCount >= p.phaseDash.maxConsecutiveDashes)
                    {
                        p.dashConsecutiveCount = 0;
                        p.dashCooldownTimer = p.phaseDash.dashCooldownSeconds;
                    }
                }

                return;
            }

            if (p.dashWindUpTimer > 0f)
            {
                p.dashWindUpTimer -= dt;
                agent.velocity = Vector3.zero;
                if (p.dashWindUpTimer <= 0f)
                    p.dashActiveTimer = p.phaseDash.dashDurationSeconds;
                return;
            }

            p.dashCooldownTimer -= dt;
            if (p.dashCooldownTimer > 0f)
                return;

            p.dashWindUpTimer = p.phaseDash.windUpSeconds;
        }

        Vector3 ResolveDashDir(Vector3 from, Vector3 playerPos, PhaseDashAxis axis, int agentId)
        {
            var toPlayer = playerPos - from;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
                return Vector3.forward;
            toPlayer.Normalize();
            return axis switch
            {
                PhaseDashAxis.Flank => Vector3.Cross(Vector3.up, toPlayer) * (agentId % 2 == 0 ? 1f : -1f),
                PhaseDashAxis.AwayThenReengage => -toPlayer,
                _ => toPlayer
            };
        }

        void TickTerrainAmbush(ref EnemyUnit agent, ref EnemySpawnRuntimeParams p, float dt, Vector3 playerPos)
        {
            if (!p.ambushHidden)
                return;

            agent.velocity = Vector3.zero;
            var dist = HorizontalDistance(agent.position, playerPos);
            var trigger = p.terrainAmbush.trigger;
            if (trigger == TerrainAmbushTrigger.WhenPlayerEntersRadius && dist <= p.terrainAmbush.playerTriggerRadius)
                RevealAmbush(ref agent, ref p);
            else if (trigger == TerrainAmbushTrigger.WhenOffScreenThenReveal)
            {
                var cam = Camera.main;
                if (cam != null && SpawnPlacementService.IsWorldPositionVisible(agent.position, cam, 0.05f))
                    RevealAmbush(ref agent, ref p);
            }
            else if (trigger == TerrainAmbushTrigger.OnSpawn)
                RevealAmbush(ref agent, ref p);
        }

        void RevealAmbush(ref EnemyUnit agent, ref EnemySpawnRuntimeParams p)
        {
            p.ambushHidden = false;
            agent.moveSpeed *= p.terrainAmbush.revealBurstSpeedMultiplier;
        }

        void TickAdaptive(ref EnemySpawnRuntimeParams p, float dt)
        {
            p.adaptationCooldownTimer -= dt;
            var signals = _services.AdaptiveSignals; // V3: RunCombatSignals.Instance, never created (R-12)
            if (signals == null || p.adaptationCooldownTimer > 0f)
                return;

            if (!signals.IsSignalActive(AdaptiveSignal.PlayerKiteTime, p.adaptive))
                return;

            if (p.adaptationStacks >= p.adaptive.maxAdaptationStacks)
                return;

            p.adaptationStacks++;
            p.adaptiveSpeedMult = p.adaptive.kiteResponseSpeedMultiplier;
            p.adaptiveMovementOverride = p.adaptive.kiteResponseMovement;
            p.hasAdaptiveMovementOverride = true;
            p.adaptationCooldownTimer = p.adaptive.adaptationCooldownSeconds;
        }

        float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
