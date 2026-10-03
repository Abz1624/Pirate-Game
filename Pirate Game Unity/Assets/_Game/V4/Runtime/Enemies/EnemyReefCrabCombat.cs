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
        // Playtest (2 Oct 2026): the crab keeps its distance and lobs one big, medium-paced cannonball at a time (dodgeable),
        // instead of chasing the ship; the claw slam stays for a ship that sails into it.
        const float SlamTriggerDistance = 4.5f;
        // Playtest (3 Oct, owner: "my weapons were not attacking the crab miniboss"): it kept its centre 10 to 14 m away, but
        // weapons pick targets by centre distance and most reach 6 to 13 m (orbiting blades 6, lantern 9, flamethrower 9.5,
        // swivel guns and broadside 12), so most of a build never fired at it. It now holds its hull 3 to 5.5 m off
        // (centre about 6.5 to 9 m at its drawn size), still at range for its cannonball but inside the weapons' reach.
        const float KeepEdgeMin = 3f, KeepEdgeMax = 5.5f;
        const float ShotInterval = 2.5f;
        const float ShotWindup = 0.6f;
        const float ShotSpeed = 8f;
        const float ShotLife = 2.8f;
        const float ShotHitRadius = 1.0f;
        const float ShotVisualRadius = 0.9f;
        const float ShotDamageMultiplier = 1.2f;
        const float ShotMaxRange = 20f;

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
                    {
                        state.phase = EnemyCombatPhase.Approach;
                        state.phaseTimer = ShotInterval * 0.5f; // the cannon is ready again soon after the slam
                    }
                    return Vector3.zero;
                case EnemyCombatPhase.Execute: // aiming the cannon: almost still, then one big shot
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        FireCannonball(ref agent, playerPos, dir, dist, playerDamage);
                        state.phase = EnemyCombatPhase.Approach;
                        state.phaseTimer = ShotInterval;
                    }
                    return tangent * (def.moveSpeed * 0.1f);
                default:
                    if (dist < SlamTriggerDistance)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = SlamWindup;
                        return Vector3.zero;
                    }

                    if (dist > ShotMaxRange && state.phaseTimer < ShotWindup) state.phaseTimer = ShotWindup; // the first shot comes a moment after it is in range
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f && dist <= ShotMaxRange)
                    {
                        state.phase = EnemyCombatPhase.Execute;
                        state.phaseTimer = ShotWindup;
                        return Vector3.zero;
                    }

                    var keepMin = agent.radius + KeepEdgeMin;
                    var keepMax = agent.radius + KeepEdgeMax;
                    if (dist > keepMax) return dir * (def.moveSpeed * (dist > keepMax + 6f ? 0.9f : 0.65f));
                    if (dist < keepMin) return (-dir * 0.8f + tangent * 0.5f) * (def.moveSpeed * 0.6f); // back off
                    return tangent * (def.moveSpeed * 0.55f);
            }
        }

        void FireCannonball(ref EnemyUnit agent, Vector3 playerPos, Vector3 dir, float dist, DamageReceiver playerDamage)
        {
            var shots = Shots;
            if (shots == null || dist > ShotMaxRange) return;
            // Aimed where the ship will be when the ball arrives (playtest, owner: enemies intercept, not chase).
            var ship = playerDamage != null ? playerDamage.GetComponent<PlayerShipController>() : null;
            var playerVelocity = Sim != null ? Sim.PlayerVelocityForEnemies : ship != null ? ship.Velocity : Vector3.zero; // playtest (3 Oct): not the island bounce
            var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
            var led = EnemySimulation.AimLed(muzzle, playerPos, playerVelocity, ShotSpeed, dir);
            shots.TrySpawn(muzzle, led, ShotSpeed, agent.damage * ShotDamageMultiplier, ShotLife, ShotHitRadius, ShotVisualRadius,
                EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, ShotSpeed));
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
