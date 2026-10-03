using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Support/control specialists and bomb ketch mortar resolution.</summary>
    /// <summary>Port of SaltwindSpecialistCombat (Gunner Cog, Shield Frigate, Net Cutter, Powder Barge, Repair Tender...), per run.</summary>
    public sealed partial class EnemySpecialistCombat
    {
        readonly EnemyCombatServices _services;
        EnemySimulation Sim => _services.Simulation;
        HazardTelegraphs Hazards => _services.Hazards;
        EncounterBudget Budget => _services.Budget;
        IEnemyEffects Effects => _services.Effects;
        HostileProjectiles Shots => _services.Shots;
        bool CombatEnabled => _services.CombatEnabled;
        CombatAgentState State(int unitId, EnemyDefinition def) => _services.State(unitId, def);

        public EnemySpecialistCombat(EnemyCombatServices services) { _services = services; }

        public void Tick(
            EnemySimulation world,
            int agentIndex,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            DamageReceiver playerDamage)
        {
            switch (profile.archetype)
            {
                case SaltwindCombatArchetype.BoardingBarge:
                    TickBoardingBarge(world, ref agent, profile, state, dt, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.NetCutter:
                    TickNetCutter(ref agent, state, dt, playerPos, playerVelocity, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.PowderBarge:
                    TickPowderBarge(ref agent, state, dt, playerVelocity);
                    break;
                case SaltwindCombatArchetype.RepairTender:
                    TickRepairTender(world, agentIndex, ref agent, state, dt);
                    break;
                case SaltwindCombatArchetype.GunnerCog:
                    TickGunnerCog(ref agent, profile, state, dt, playerPos, playerVelocity, dist);
                    break;
                case SaltwindCombatArchetype.ShieldFrigate:
                    TickShieldFrigate(ref agent, profile, state, dt, playerPos, playerVelocity, dist);
                    break;
                case SaltwindCombatArchetype.BombKetch:
                    TickBombKetch(world, ref agent, profile, state, dt, playerPos, playerVelocity, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.NeedleSloop:
                    TickNeedleSloop(ref agent, profile, state, dt, playerPos, playerVelocity, dist);
                    break;
            }
        }

        void TickBoardingBarge(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (dist > 14f) return;
            if (agent.attackCooldown > 0f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = Mathf.Max(profile.attackWindUp, EncounterBudget.MinChargeWarning);
                return;
            }

            state.phaseTimer -= dt;
            // Playtest (3 Oct, owner, round 7): the grapples' 10 m reach shows round the barge during the windup.
            if (state.phaseTimer > 0f) { Hazards.Warn(agent.position, BoardingReach, state.phaseTimer, agent.id); return; }

            if (dist <= BoardingReach && playerDamage != null)
            {
                var ship = playerDamage.GetComponent<PlayerShipController>();
                ship?.ApplySlow(0.75f, 2f);
                _services.HitPlayer(playerDamage, agent.damage * 0.35f, agent.position);
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        const float BoardingReach = 10f;

        void TickNetCutter(
            ref EnemyUnit agent,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;

            playerVelocity.y = 0f;
            var lead = playerVelocity.sqrMagnitude > 0.2f ? Mathf.Clamp(playerVelocity.magnitude * 0.65f, 4f, 10f) : 6f;
            var ahead = playerVelocity.sqrMagnitude > 0.2f
                ? playerPos + playerVelocity.normalized * lead
                : playerPos + (agent.position - playerPos).normalized * 6f;
            ahead.y = 0f;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 0.85f;
                state.committedPosition = ahead;
                Hazards.Warn(ahead, 3f, 0.85f); // Playtest (3 Oct, owner, round 7): where the net falls
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            Hazards.SpawnNetSegment(state.committedPosition, 6f, 3f, 0.2f, 1f);
            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = 4f;
        }

        void TickPowderBarge(ref EnemyUnit agent, CombatAgentState state, float dt, Vector3 playerVelocity)
        {
            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;
            if (state.chargePassCount >= 3) return;

            Hazards.SpawnMine(agent.position, 1.2f, 6f, agent.damage * 0.85f);
            state.chargePassCount++;
            state.phaseTimer = 3f;
        }

        void TickRepairTender(EnemySimulation world, int selfIndex, ref EnemyUnit agent, CombatAgentState state, float dt)
        {
            if (agent.attackCooldown > 0f) return;

            var agents = world.Agents;
            var bestIndex = -1;
            var bestMissing = 0f;
            for (var i = 0; i < agents.Length; i++)
            {
                if (i == selfIndex || !agents[i].active) continue;
                var ally = agents[i];
                if (ally.maxHp <= 0f) continue;
                var def = world.Definitions[ally.definitionIndex];
                if (def == null || def.isBoss || def.isMiniboss) continue;
                if (Sim.RulesFor(ally.definitionIndex).excludedFromRepair) continue;
                if (_services.SpawnParams.TryGetCombatProfile(def.id, out var p)
                    && p.archetype == SaltwindCombatArchetype.RepairTender)
                    continue;
                var missing = ally.maxHp - ally.hp;
                if (missing < ally.maxHp * 0.05f) continue;
                if (Vector3.Distance(ally.position, agent.position) > 8f) continue;
                if (missing <= bestMissing) continue;
                bestMissing = missing;
                bestIndex = i;
            }

            if (bestIndex < 0) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 1f;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            world.HealAgent(bestIndex, agents[bestIndex].maxHp * 0.03f);
            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = 4f;
        }

        const float GunnerVolleySpreadDegrees = 6f;
        const float NeedleShotSpeed = 16f;

        void TickGunnerCog(
            ref EnemyUnit agent,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist)
        {
            if (dist < 5f || dist > 22f) return;
            if (agent.attackCooldown > 0f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                if (state.attackSequence == 0)
                {
                    state.attackSequence = 1;
                    agent.attackCooldown = (agent.id % 4) * 0.4f;
                    return;
                }

                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 0.6f;
                var dir = playerPos - agent.position;
                dir.y = 0f;
                state.committedDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            var hostiles = Shots;
            if (hostiles != null)
            {
                // Lead the volley at firing time and fan it (-6, 0, +6 degrees) instead of stacking three shots on one line.
                var aim = EnemySimulation.Spread(EnemySimulation.AimLed(
                    agent.position, playerPos, playerVelocity, profile.projectileSpeed, state.committedDirection), agent);
                var muzzle = agent.position + aim * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                var shotDistance = EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, profile.projectileSpeed);
                for (var shot = 0; shot < 3; shot++)
                {
                    var shotDir = Quaternion.Euler(0f, (shot - 1) * GunnerVolleySpreadDegrees, 0f) * aim;
                    hostiles.TrySpawn(muzzle, shotDir, profile.projectileSpeed, agent.damage * 0.9f, 1.5f, 0.42f, 0.32f, shotDistance);
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        void TickShieldFrigate(
            ref EnemyUnit agent,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist)
        {
            if (dist < 8f || dist > 16f) return;
            if (agent.attackCooldown > 0f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = profile.attackWindUp;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            var hostiles = Shots;
            if (hostiles != null)
            {
                var dir = playerPos - agent.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir = EnemySimulation.Spread(EnemySimulation.AimLed(agent.position, playerPos, playerVelocity, profile.projectileSpeed, dir), agent);
                    var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, dir, profile.projectileSpeed, agent.damage, 1.4f, 0.45f, 0.35f,
                        EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, profile.projectileSpeed));
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        void TickBombKetch(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;
            if (dist < profile.preferredRange - 5f || dist > profile.preferredRange + 6f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                playerVelocity.y = 0f;
                state.committedPosition = playerPos + playerVelocity * Mathf.Clamp(playerVelocity.magnitude * 0.5f, 0f, 4f);
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 0.5f;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            // Playtest (3 Oct, owner, round 7: "I could see the powder keg explosion smoke on the player ship but not what
            // caused it"): the blast went off where the bomb was aimed the moment it was thrown, 1.2 s before its damage.
            // Now only the red circle shows there, and the blast plays when it lands (HazardTelegraphs).
            Hazards.SpawnCircle(state.committedPosition, 3f, 1.2f, agent.damage * 1.1f);
            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        void TickNeedleSloop(
            ref EnemyUnit agent,
            EnemyCombatProfile profile,
            CombatAgentState state,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist)
        {
            if (dist < 12f || dist > 24f) return;
            if (agent.attackCooldown > 0f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 1f;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            var hostiles = Shots;
            if (hostiles != null)
            {
                var dir = playerPos - agent.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir = EnemySimulation.Spread(EnemySimulation.AimLed(agent.position, playerPos, playerVelocity, NeedleShotSpeed, dir), agent);
                    var muzzle = agent.position + dir * (agent.radius + 1.3f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, dir, NeedleShotSpeed, agent.damage, 1.2f, 0.35f, 0.28f,
                        EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, NeedleShotSpeed));
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }
    }
}
