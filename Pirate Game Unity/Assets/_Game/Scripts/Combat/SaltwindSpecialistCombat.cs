using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Support/control specialists and bomb ketch mortar resolution.</summary>
    public static class SaltwindSpecialistCombat
    {
        public static void Tick(
            EnemyWorld world,
            int agentIndex,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
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
                    TickGunnerCog(ref agent, profile, state, dt, playerPos, dist);
                    break;
                case SaltwindCombatArchetype.ShieldFrigate:
                    TickShieldFrigate(ref agent, profile, state, dt, playerPos, dist);
                    break;
                case SaltwindCombatArchetype.BombKetch:
                    TickBombKetch(world, ref agent, profile, state, dt, playerPos, playerVelocity, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.NeedleSloop:
                    TickNeedleSloop(ref agent, profile, state, dt, playerPos, dist);
                    break;
            }
        }

        static void TickBoardingBarge(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
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
                state.phaseTimer = profile.attackWindUp;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            if (dist <= 10f && playerDamage != null)
            {
                var ship = playerDamage.GetComponent<PlayerShipController>();
                ship?.ApplySlow(0.75f, 2f);
                playerDamage.TakeDamage(agent.damage * 0.35f, agent.position, false);
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        static void TickNetCutter(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            SaltwindHazardTelegraph.SpawnNetSegment(state.committedPosition, 6f, 3f, 0.2f, 1f);
            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = 4f;
        }

        static void TickPowderBarge(ref EnemyAgent agent, SaltwindCombatAgentState state, float dt, Vector3 playerVelocity)
        {
            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;
            if (state.chargePassCount >= 3) return;

            SaltwindHazardTelegraph.SpawnMine(agent.position, 1.2f, 6f, agent.damage * 0.85f);
            state.chargePassCount++;
            state.phaseTimer = 3f;
        }

        static void TickRepairTender(EnemyWorld world, int selfIndex, ref EnemyAgent agent, SaltwindCombatAgentState state, float dt)
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
                if (def.id == "repair_tender") continue;
                if (EnemyCombatProfileRegistry.TryGet(def.id, out var p)
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

        static void TickGunnerCog(
            ref EnemyAgent agent,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            float dt,
            Vector3 playerPos,
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

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles != null)
            {
                for (var shot = 0; shot < 3; shot++)
                {
                    var muzzle = agent.position + state.committedDirection * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, state.committedDirection, profile.projectileSpeed, agent.damage * 0.9f, 1.5f, 0.42f, 0.32f);
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        static void TickShieldFrigate(
            ref EnemyAgent agent,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            float dt,
            Vector3 playerPos,
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

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles != null)
            {
                var dir = playerPos - agent.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, dir, profile.projectileSpeed, agent.damage, 1.4f, 0.45f, 0.35f);
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        static void TickBombKetch(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
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

            SaltwindHazardTelegraph.SpawnCircle(state.committedPosition, 3f, 1.2f, agent.damage * 1.1f);
            SaltwindCombatEffects.PlayExplosion(state.committedPosition, 3f);
            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }

        static void TickNeedleSloop(
            ref EnemyAgent agent,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            float dt,
            Vector3 playerPos,
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

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles != null)
            {
                var dir = playerPos - agent.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    var muzzle = agent.position + dir * (agent.radius + 1.3f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, dir, 16f, agent.damage, 1.2f, 0.35f, 0.28f);
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }
    }
}
