using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Reef Crab miniboss — sidestep guard, claw slam, bubble fan (Saltwind redesign).</summary>
    /// <summary>Port of SaltwindReefCrabCombat (the Reef Crab miniboss), per run.</summary>
    public sealed partial class EnemyReefCrabCombat
    {
        readonly EnemyCombatServices _services;
        EnemySimulation Sim => _services.Simulation;
        HazardTelegraphs Hazards => _services.Hazards;
        EncounterBudget Budget => _services.Budget;
        IEnemyEffects Effects => _services.Effects;
        HostileProjectiles Shots => _services.Shots;
        bool CombatEnabled => _services.CombatEnabled;
        CombatAgentState State(int unitId, EnemyDefinition def) => _services.State(unitId, def);

        public EnemyReefCrabCombat(EnemyCombatServices services) { _services = services; }

        const float SlamRadius = 4f;
        const float SlamWindup = 1.2f;
        const float SlamRecovery = 2f;
        const float GuardReduction = 0.3f;

        public Vector3 TickMovement(
            int agentIndex,
            ref EnemyUnit agent,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            var state = State(agent.id, def);
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
                            _services.HitPlayer(playerDamage, agent.damage * 1.35f, agent.position);
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

        public float GetDirectHitMultiplier(Vector3 agentPos, Vector3 attackOrigin, bool guardActive)
        {
            if (!guardActive) return 1f;
            var toAgent = agentPos - attackOrigin;
            toAgent.y = 0f;
            if (toAgent.sqrMagnitude < 0.01f) return 1f - GuardReduction;
            return 1f - GuardReduction;
        }

        float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
