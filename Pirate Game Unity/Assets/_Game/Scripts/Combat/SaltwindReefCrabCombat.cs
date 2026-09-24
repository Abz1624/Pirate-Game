using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Reef Crab miniboss — sidestep guard, claw slam, bubble fan (Saltwind redesign).</summary>
    public static class SaltwindReefCrabCombat
    {
        const float SlamRadius = 4f;
        const float SlamWindup = 1.2f;
        const float SlamRecovery = 2f;
        const float GuardReduction = 0.3f;

        public static Vector3 TickMovement(
            int agentIndex,
            ref EnemyAgent agent,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            var state = SaltwindCombatAgentStateStore.GetOrCreate(agent.id, def.id);
            var toPlayer = playerPos - agent.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;
            var tangent = Vector3.Cross(Vector3.up, dir);

            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        if (HorizontalDistance(agent.position, playerPos) <= SlamRadius && playerDamage != null)
                            playerDamage.TakeDamage(agent.damage * 1.35f, agent.position, false);
                        state.phase = EnemyCombatPhase.Recover;
                        state.phaseTimer = SlamRecovery;
                    }
                    return tangent * (def.moveSpeed * 0.25f);
                case EnemyCombatPhase.Recover:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                        state.phase = EnemyCombatPhase.Approach;
                    return Vector3.zero;
                default:
                    if (dist < 5.5f)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = SlamWindup;
                        return Vector3.zero;
                    }

                    if (dist > 9f)
                        return dir * (def.moveSpeed * 0.65f);
                    return tangent * (def.moveSpeed * 0.55f);
            }
        }

        public static float GetDirectHitMultiplier(Vector3 agentPos, Vector3 attackOrigin, bool guardActive)
        {
            if (!guardActive) return 1f;
            var toAgent = agentPos - attackOrigin;
            toAgent.y = 0f;
            if (toAgent.sqrMagnitude < 0.01f) return 1f - GuardReduction;
            return 1f - GuardReduction;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
