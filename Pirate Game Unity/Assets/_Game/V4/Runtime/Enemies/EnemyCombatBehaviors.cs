using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Port of SaltwindCombatBehaviorSystem (movement and attacks per combat archetype), per run.</summary>
    public sealed partial class EnemyCombatBehaviors
    {
        readonly EnemyCombatServices _services;
        EnemySimulation Sim => _services.Simulation;
        HazardTelegraphs Hazards => _services.Hazards;
        EncounterBudget Budget => _services.Budget;
        IEnemyEffects Effects => _services.Effects;
        HostileProjectiles Shots => _services.Shots;
        bool CombatEnabled => _services.CombatEnabled;
        CombatAgentState State(int unitId, EnemyDefinition def) => _services.State(unitId, def);
        EnemySpecialistCombat Specialists => _services.Specialists;

        public EnemyCombatBehaviors(EnemyCombatServices services) { _services = services; }

        public static float GetSpeedCapMult(EnemyCombatProfile profile)
        {
            if (profile == null) return 1.5f;
            return profile.archetype switch
            {
                SaltwindCombatArchetype.Swordfish => 2.6f,
                SaltwindCombatArchetype.Hammerhead or SaltwindCombatArchetype.EliteHammerhead => 2.8f,
                SaltwindCombatArchetype.RammerRaft => 2.4f,
                _ => 1.55f
            };
        }

        public bool TryTickMovement(
            EnemySimulation world,
            int agentIndex,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
            DamageReceiver playerDamage,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (profile == null || def == null) return false;

            var state = State(agent.id, def);
            var toPlayer = playerPos - agent.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;

            switch (profile.archetype)
            {
                case SaltwindCombatArchetype.DeckhandDinghy:
                    velocity = TickDeckhand(ref agent, state, dir, dist, dt, playerPos, playerVelocity);
                    return true;
                case SaltwindCombatArchetype.Shark:
                case SaltwindCombatArchetype.DrownedPursuer:
                    velocity = TickShark(ref agent, state, dir, dist, playerPos, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.Swordfish:
                    velocity = TickSwordfish(ref agent, state, profile, dir, dist, dt, playerPos, playerVelocity);
                    return true;
                case SaltwindCombatArchetype.CutthroatSkiff:
                    velocity = TickCutthroatSkiff(ref agent, state, dir, dist, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.Hammerhead:
                case SaltwindCombatArchetype.EliteHammerhead:
                case SaltwindCombatArchetype.RammerRaft:
                case SaltwindCombatArchetype.RamRaider:
                    if (Sim.RulesFor(agent.definitionIndex).eliteLatchesSecondChargeForAll && agent.isElite) // V3 quirk (R-100): latches the shared profile
                        profile.eliteHammerheadSecondCharge = true;
                    velocity = TickTelegraphedCharge(ref agent, state, profile, dir, dist, dt, playerPos, playerVelocity);
                    return true;
                case SaltwindCombatArchetype.Cannonboat:
                case SaltwindCombatArchetype.NeedleSloop:
                case SaltwindCombatArchetype.BombKetch:
                case SaltwindCombatArchetype.DrownedHarrier:
                    velocity = TickMaintainRange(ref agent, state, profile, dir, dist, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.MantaRay:
                    velocity = TickMantaOrbit(ref agent, state, profile, dir, dist, dt);
                    return true;
                case SaltwindCombatArchetype.BombBoat:
                    velocity = TickBombBoat(ref agent, state, dir, dist, dt, playerPos, playerVelocity);
                    return true;
                case SaltwindCombatArchetype.PowderBarge:
                    velocity = dir * (agent.moveSpeed * 0.55f);
                    return true;
                case SaltwindCombatArchetype.RepairTender:
                    velocity = TickRepairTenderApproach(world, agentIndex, ref agent, dir, dist, dt);
                    return true;
                case SaltwindCombatArchetype.BoardingBarge:
                case SaltwindCombatArchetype.ShieldFrigate:
                case SaltwindCombatArchetype.GunnerCog:
                    velocity = TickMaintainRange(ref agent, state, profile, dir, dist, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.NetCutter:
                    velocity = TickNetCutterRoute(ref agent, state, dir, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.TreasureRaider:
                    velocity = TickTreasureRaider(ref agent, state, dir, dist, playerPos, dt);
                    return true;
                case SaltwindCombatArchetype.KrakenSpawn:
                case SaltwindCombatArchetype.EliteKrakenSpawn:
                    velocity = TickKrakenAnchor(ref agent, state, dir, dist, dt);
                    return true;
                default:
                    velocity = EnemyPatternMovement.ComputeVelocity(
                        agent, agent.patternParams, dir, dir, dist, dt, ref agent.chargeTimer, playerVelocity, Sim.RunSeconds);
                    return true;
            }
        }

        public void TickCombat(
            EnemySimulation world,
            int agentIndex,
            ref EnemyUnit agent,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            if (!_services.SpawnParams.TryGetCombatProfile(def.id, out var profile)) return;
            if (profile.archetype == SaltwindCombatArchetype.ReefCrabMiniboss) return;

            var state = State(agent.id, def);
            var dist = HorizontalDistance(agent.position, playerPos);
            var playerVelocity = world != null ? world.PlayerVelocityForEnemies : ResolvePlayerVelocity(playerDamage); // playtest (3 Oct): not the island bounce

            if (agent.attackCooldown > 0f)
                agent.attackCooldown -= dt;

            if (profile.archetype is SaltwindCombatArchetype.BoardingBarge
                or SaltwindCombatArchetype.NetCutter
                or SaltwindCombatArchetype.PowderBarge
                or SaltwindCombatArchetype.RepairTender
                or SaltwindCombatArchetype.GunnerCog
                or SaltwindCombatArchetype.ShieldFrigate
                or SaltwindCombatArchetype.BombKetch
                or SaltwindCombatArchetype.NeedleSloop)
            {
                Specialists.Tick(
                    world, agentIndex, ref agent, def, profile, state, dt, playerPos, playerVelocity, dist, playerDamage);
                return;
            }

            switch (profile.archetype)
            {
                case SaltwindCombatArchetype.Cannonboat:
                    TrySingleProjectileAttack(world, ref agent, def, profile, state, playerPos, playerVelocity, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.MantaRay:
                    TrySpreadVolley(world, ref agent, def, profile, state, playerPos, playerVelocity, dist, dt, playerDamage);
                    break;
                case SaltwindCombatArchetype.BombBoat:
                    TickBombBoatDetonation(world, ref agent, def, state, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.KrakenSpawn:
                case SaltwindCombatArchetype.EliteKrakenSpawn:
                    TickKrakenMarks(world, ref agent, def, profile, state, playerPos, dt, playerDamage);
                    break;
                case SaltwindCombatArchetype.DrownedHarrier:
                    TryBurstCrossingShot(world, ref agent, def, profile, state, playerPos, playerVelocity, dist, dt, playerDamage);
                    break;
            }
        }

        Vector3 ResolvePlayerVelocity(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return Vector3.zero;
            var ship = playerDamage.GetComponent<PlayerShipController>();
            return ship != null ? ship.Velocity : Vector3.zero;
        }

        float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        Vector3 TickDeckhand(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            // Playtest (3 Oct, owner: the dinghy "is not doing anything to hurt the player; it runs away"): it backed off as
            // soon as it came within 2.2 m of its own radius, before its hull could touch the ship, so it never rammed.
            // Now it rams: it backs off only after its contact hit landed (the contact cooldown has just started), and
            // peels away to one side for a moment before coming round again.
            if (state.phase == EnemyCombatPhase.Approach && agent.contactCooldown > GameConstants.EnemyContactCooldown * 0.5f)
            {
                state.phase = EnemyCombatPhase.Recover;
                state.phaseTimer = 0.6f;
            }
            state.contactDisabled = state.phase == EnemyCombatPhase.Recover;

            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                var side = Vector3.Cross(Vector3.up, dir) * ((agent.id & 1) == 0 ? 1f : -1f);
                return (side * 0.85f - dir * 0.5f).normalized * (agent.moveSpeed * 0.9f);
            }

            // Cut the player off and close from a ring slot (replaces the fixed per-id side bias).
            var pursuit = EnemySimulation.GetPursuitDirection(agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity);
            var wobble = Vector3.Cross(Vector3.up, pursuit) * Mathf.Sin(agent.id * 0.7f + Sim.RunSeconds * 2.2f) /* V5 M9a: run time, not Time.time */ * 0.18f;
            var approach = (pursuit + wobble).normalized;
            var speedMult = dist > 10f ? 1.02f : dist > 5f ? 1.12f : 1.22f;
            return approach * (agent.moveSpeed * speedMult);
        }

        Vector3 TickShark(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dt)
        {
            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= dt; // V5 M9b close (review): the simulation's step (was the frame's delta time)
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                return -dir * (agent.moveSpeed * 0.85f);
            }

            // Cut the player off (intercept point) and, when close, come in from a ring slot so a pack surrounds.
            dir = EnemySimulation.GetPursuitDirection(agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity);

            var speed = dist > 16f ? agent.moveSpeed * 0.82f
                : dist > 7f ? agent.moveSpeed * 1.05f
                : agent.moveSpeed * 1.28f;
            return dir * speed;
        }

        Vector3 SteerSwordfishAim(
            Vector3 from,
            Vector3 playerPos,
            Vector3 playerVelocity,
            Vector3 currentAim,
            float maxTurnDegPerSec,
            float dt,
            float velocityLeadSeconds)
        {
            playerVelocity.y = 0f;
            var target = playerPos + playerVelocity * velocityLeadSeconds;
            var desired = target - from;
            desired.y = 0f;
            if (desired.sqrMagnitude < 0.01f)
                return currentAim.sqrMagnitude > 0.01f ? currentAim.normalized : Vector3.forward;
            desired.Normalize();
            if (currentAim.sqrMagnitude < 0.01f)
                return desired;
            var maxRad = maxTurnDegPerSec * Mathf.Deg2Rad * dt;
            return Vector3.RotateTowards(currentAim.normalized, desired, maxRad, 0f);
        }

        const float SwordfishInstantLungeRadiusDefault = 5.5f;

        float ResolveSwordfishInstantLungeRadius(EnemyCombatProfile profile)
        {
            if (profile == null || profile.preferredRange <= 0f)
                return SwordfishInstantLungeRadiusDefault;
            return Mathf.Clamp(profile.preferredRange * 0.55f, 4.5f, 7f);
        }

        float ResolveSwordfishDashDuration(EnemyCombatProfile profile) =>
            profile != null && profile.chargeDuration > 0f ? profile.chargeDuration : 0.65f;

        void BeginSwordfishExecute(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 playerPos,
            Vector3 playerVelocity,
            Vector3 fallbackDir)
        {
            state.phase = EnemyCombatPhase.Execute;
            state.phaseTimer = ResolveSwordfishDashDuration(profile);
            state.contactDisabled = false;
            agent.chargeTimer = state.phaseTimer;
            state.committedDirection = SteerSwordfishAim(
                agent.position, playerPos, playerVelocity, fallbackDir, 720f, 0.016f, 0.1f);
        }

        bool TryTriggerSwordfishInstantLunge(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            float dist,
            Vector3 playerPos,
            Vector3 playerVelocity,
            Vector3 fallbackDir)
        {
            if (dist > ResolveSwordfishInstantLungeRadius(profile)) return false;
            if (state.phase is EnemyCombatPhase.Execute or EnemyCombatPhase.Recover) return false;
            BeginSwordfishExecute(ref agent, state, profile, playerPos, playerVelocity, fallbackDir);
            return true;
        }

        Vector3 TickSwordfish(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            const float hoverRange = 10f;
            const float dashSpeed = 12f;
            const float telegraphSeconds = 0.85f;

            if (TryTriggerSwordfishInstantLunge(
                    ref agent, state, profile, dist, playerPos, playerVelocity, dir))
                return state.committedDirection * dashSpeed;

            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    state.committedDirection = SteerSwordfishAim(
                        agent.position, playerPos, playerVelocity, state.committedDirection, 48f, dt, 0.12f);
                    if (state.phaseTimer <= 0f)
                        BeginSwordfishExecute(ref agent, state, profile, playerPos, playerVelocity, state.committedDirection);

                    var creep = state.committedDirection * (agent.moveSpeed * 0.42f);
                    var strafe = Vector3.Cross(Vector3.up, state.committedDirection) * (agent.moveSpeed * 0.22f * state.orbitSign);
                    return creep + strafe;
                case EnemyCombatPhase.Execute:
                    state.phaseTimer -= dt;
                    state.contactDisabled = false;
                    agent.chargeTimer = Mathf.Max(0f, state.phaseTimer);
                    state.committedDirection = SteerSwordfishAim(
                        agent.position, playerPos, playerVelocity, state.committedDirection, 26f, dt, 0.08f);
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Recover;
                        state.phaseTimer = 1.2f;
                        agent.chargeTimer = 0f;
                    }
                    return state.committedDirection * dashSpeed;
                case EnemyCombatPhase.Recover:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    if (state.phaseTimer <= 0f)
                        state.phase = EnemyCombatPhase.Approach;
                    return -state.committedDirection * (agent.moveSpeed * 0.85f);
                default:
                    state.contactDisabled = false;
                    if (dist > hoverRange + 1f)
                    {
                        state.committedDirection = SteerSwordfishAim(
                            agent.position, playerPos, playerVelocity, dir, 90f, dt, 0.1f);
                        return state.committedDirection * (agent.moveSpeed * 1.1f);
                    }

                    if (dist <= hoverRange + 1.5f)
                    {
                        if (state.phase == EnemyCombatPhase.Approach)
                        {
                            state.phase = EnemyCombatPhase.Telegraph;
                            state.phaseTimer = telegraphSeconds;
                            state.committedDirection = SteerSwordfishAim(
                                agent.position, playerPos, playerVelocity, dir, 180f, dt, 0.12f);
                        }

                        state.committedDirection = SteerSwordfishAim(
                            agent.position, playerPos, playerVelocity, state.committedDirection, 55f, dt, 0.1f);
                        var tangent = Vector3.Cross(Vector3.up, state.committedDirection) * state.orbitSign;
                        return (state.committedDirection * 0.35f + tangent * 0.65f).normalized * (agent.moveSpeed * 0.55f);
                    }

                    return dir * (agent.moveSpeed * 0.85f);
            }
        }

        Vector3 TickCutthroatSkiff(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            Vector3 playerVelocity,
            float dt)
        {
            playerVelocity.y = 0f;
            var rear = playerVelocity.sqrMagnitude > 0.2f
                ? -playerVelocity.normalized
                : -dir;

            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Execute;
                        state.phaseTimer = 0.55f;
                    }
                    return state.committedDirection * (agent.moveSpeed * 0.25f);
                case EnemyCombatPhase.Execute:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Reposition;
                        state.phaseTimer = 1.4f;
                    }
                    return state.committedDirection * (agent.moveSpeed * 1.35f);
                case EnemyCombatPhase.Reposition:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                        state.phase = EnemyCombatPhase.Approach;
                    return (rear + Vector3.Cross(Vector3.up, rear) * state.orbitSign).normalized * (agent.moveSpeed * 1.1f);
                default:
                {
                    var flankTarget = playerVelocity.sqrMagnitude > 0.25f
                        ? playerVelocity.normalized * -7f + Vector3.Cross(Vector3.up, playerVelocity.normalized) * state.orbitSign * 5f
                        : rear * 6f + Vector3.Cross(Vector3.up, dir) * state.orbitSign * 5f;
                    var toFlank = flankTarget;
                    toFlank.y = 0f;
                    if (toFlank.sqrMagnitude > 0.01f && dist > 12f)
                        return toFlank.normalized * (agent.moveSpeed * 1f);

                    if (dist < 13f && state.phase == EnemyCombatPhase.Approach)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = 0.8f;
                        var cross = Vector3.Cross(Vector3.up, playerVelocity.sqrMagnitude > 0.2f ? playerVelocity.normalized : dir);
                        state.committedDirection = (cross * state.orbitSign).normalized;
                    }

                    return dir * (agent.moveSpeed * 0.8f);
                }
            }
        }

        Vector3 TickTelegraphedCharge(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    // Playtest (3 Oct, owner, round 7: attacks must be readable): the charge lane shows on the water for the
                    // whole windup; the aim follows the ship until the last ChargeAimLockSeconds, then the charge runs
                    // exactly along the lane that was shown (it used to re-aim at the last instant).
                    if (state.phaseTimer > ChargeAimLockSeconds || state.committedDirection.sqrMagnitude < 0.01f)
                        state.committedDirection = PredictChargeDirection(agent.position, playerPos, playerVelocity, profile);
                    if (state.phaseTimer > 0f)
                        Hazards.WarnLine(agent.position, state.committedDirection, GetChargeLength(agent, profile), agent.radius + 0.8f, state.phaseTimer, agent.id);
                    if (state.phaseTimer <= 0f)
                    {
                        if (ChargeLaneBlocked(agent, state.committedDirection, GetChargeLength(agent, profile)))
                        {
                            // An island is in the way: go back to approaching (island pathing takes it round) instead
                            // of ramming the shore and sitting out a cooldown.
                            state.phase = EnemyCombatPhase.Approach;
                            state.contactDisabled = false;
                            return Vector3.zero;
                        }

                        state.phase = EnemyCombatPhase.Execute;
                        state.phaseTimer = profile.chargeDuration;
                        state.attackId++;
                        Budget.TryReserveCharge(state.attackId);
                    }

                    return Vector3.zero;
                case EnemyCombatPhase.Execute:
                    state.phaseTimer -= dt;
                    var vel = state.committedDirection * GetChargeSpeed(agent, profile);
                    if (IslandObstacles.IsAvailable)
                    {
                        var next = agent.position + vel * dt;
                        var radius = agent.radius + 0.35f;
                        var height = IslandObstacles.GetCollisionHeight(radius);
                        var corrected = IslandObstacles.Depenetrate(next, radius, height);
                        if ((corrected - next).sqrMagnitude > 0.04f)
                        {
                            // Hit an island: a short recovery that already turns back toward the player (not a freeze).
                            state.phase = EnemyCombatPhase.Recover;
                            state.phaseTimer = Mathf.Min(profile.chargeCooldown, IslandBumpRecoverSeconds);
                            state.contactDisabled = true;
                            state.committedDirection = EnemySimulation.GetPursuitDirection(
                                agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity, 0f);
                            return GetRecoverDrift(agent, state, profile, playerPos, playerVelocity);
                        }
                    }

                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Recover;
                        state.phaseTimer = profile.chargeCooldown;
                        state.chargePassCount++;
                        if ((profile.eliteHammerheadSecondCharge || (agent.isElite && Sim.RulesFor(agent.definitionIndex).eliteChargesTwice)) && state.chargePassCount == 1)
                        {
                            state.phase = EnemyCombatPhase.Telegraph;
                            state.phaseTimer = profile.chargeWindUp;
                        }
                        else if (profile.archetype == SaltwindCombatArchetype.RamRaider && state.chargePassCount == 1)
                        {
                            state.phase = EnemyCombatPhase.Reposition;
                            state.phaseTimer = 1.1f;
                        }
                    }

                    return vel;
                case EnemyCombatPhase.Reposition:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = profile.chargeWindUp;
                    }
                    return -state.committedDirection * (agent.moveSpeed * 0.45f);
                case EnemyCombatPhase.Recover:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Approach;
                        state.chargePassCount = 0;
                        state.contactDisabled = false;
                    }
                    return GetRecoverDrift(agent, state, profile, playerPos, playerVelocity);
                default:
                {
                    // Engage from about the charge's own length, so the charge reaches the player (22 m left it idling).
                    var chargeLength = GetChargeLength(agent, profile);
                    if (dist < chargeLength)
                    {
                        var lane = PredictChargeDirection(agent.position, playerPos, playerVelocity, profile);
                        if (!ChargeLaneBlocked(agent, lane, chargeLength))
                        {
                            state.phase = EnemyCombatPhase.Telegraph;
                            state.phaseTimer = Mathf.Max(profile.chargeWindUp, EncounterBudget.MinChargeWarning);
                            state.committedDirection = lane;
                        }
                    }

                    var approachMult = profile.archetype == SaltwindCombatArchetype.Hammerhead ? 1.05f
                        : profile.archetype == SaltwindCombatArchetype.RammerRaft ? 0.55f
                        : 0.85f;
                    var approachDir = EnemySimulation.GetPursuitDirection(agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity);
                    return approachDir * (agent.moveSpeed * approachMult);
                }
            }
        }

        const float IslandBumpRecoverSeconds = 0.6f;
        /// <summary>Playtest (3 Oct, owner, round 7): the charge lane stops following the ship this long before the charge.</summary>
        const float ChargeAimLockSeconds = 0.35f;
        const float RecoverDriftSpeedMult = 0.6f;
        const float MinChargeEngage = 8f;
        const float MaxChargeEngage = 18f;

        static float GetChargeSpeed(in EnemyUnit agent, EnemyCombatProfile profile) =>
            profile.archetype is SaltwindCombatArchetype.Hammerhead or SaltwindCombatArchetype.EliteHammerhead
                ? Mathf.Max(agent.moveSpeed * profile.chargeSpeedMult, 11f)
                : agent.moveSpeed * profile.chargeSpeedMult;

        /// <summary>How far one charge travels (speed x duration, a little short so it ends on the player), clamped.</summary>
        static float GetChargeLength(in EnemyUnit agent, EnemyCombatProfile profile) =>
            Mathf.Clamp(GetChargeSpeed(agent, profile) * profile.chargeDuration * 0.95f, MinChargeEngage, MaxChargeEngage);

        /// <summary>After a charge the charger curves back toward the player at reduced speed instead of sitting still.</summary>
        static Vector3 GetRecoverDrift(in EnemyUnit agent, CombatAgentState state, EnemyCombatProfile profile, Vector3 playerPos, Vector3 playerVelocity)
        {
            var toward = EnemySimulation.GetPursuitDirection(agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity, 0f);
            var heading = toward;
            if (state.committedDirection.sqrMagnitude > 0.01f)
            {
                var progress = 1f - Mathf.Clamp01(state.phaseTimer / Mathf.Max(0.1f, profile.chargeCooldown));
                var from = state.committedDirection.normalized;
                heading = Vector3.Dot(from, toward) < -0.95f
                    ? Vector3.Cross(Vector3.up, from) * state.orbitSign
                    : Vector3.Slerp(from, toward, 0.3f + 0.7f * progress);
                heading.y = 0f;
                heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : toward;
            }

            return heading * (agent.moveSpeed * RecoverDriftSpeedMult);
        }

        /// <summary>Would a charge along <paramref name="direction"/> hit an island within <paramref name="length"/>?</summary>
        static bool ChargeLaneBlocked(in EnemyUnit agent, Vector3 direction, float length)
        {
            if (!IslandObstacles.IsAvailable || direction.sqrMagnitude < 0.01f)
                return false;
            var origin = new Vector3(agent.position.x, Mathf.Max(agent.position.y, 0f) + 0.5f, agent.position.z);
            return Physics.SphereCast(origin, Mathf.Max(0.2f, agent.radius * 0.6f), direction.normalized, out _, length,
                IslandObstacles.Mask, QueryTriggerInteraction.Ignore);
        }

        Vector3 PredictChargeDirection(Vector3 from, Vector3 playerPos, Vector3 playerVelocity, EnemyCombatProfile profile)
        {
            playerVelocity.y = 0f;
            var lead = profile.archetype == SaltwindCombatArchetype.RamRaider ? 1.15f : 0.55f;
            var predicted = playerPos + playerVelocity * lead;
            var to = predicted - from;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                to = playerPos - from;
            to.y = 0f;
            return to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
        }

        Vector3 TickMaintainRange(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            Vector3 playerVelocity,
            float dt)
        {
            var preferred = profile.preferredRange > 0f ? profile.preferredRange : 12f;
            var min = preferred * 0.75f;
            var max = preferred * 1.15f;
            var tangent = Vector3.Cross(Vector3.up, dir) * state.orbitSign;

            if (profile.archetype == SaltwindCombatArchetype.NeedleSloop && dist < 8f)
            {
                state.phase = EnemyCombatPhase.Flee;
                state.phaseTimer = 1.2f;
            }

            if (state.phase == EnemyCombatPhase.Flee)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                var escape = (-dir + tangent).normalized;
                return escape * (agent.moveSpeed * 1.25f);
            }

            if (dist > max)
                return dir * (agent.moveSpeed * 0.7f);
            if (dist < min)
                return (-dir + tangent * 0.35f).normalized * (agent.moveSpeed * 0.65f);
            if (agent.attackCooldown > 0f)
                return tangent * (agent.moveSpeed * 0.45f);
            return tangent * (agent.moveSpeed * 0.35f);
        }

        Vector3 TickMantaOrbit(
            ref EnemyUnit agent,
            CombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            float dt)
        {
            var radius = profile.preferredRange > 0f ? profile.preferredRange : 10f;
            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= dt;
                state.orbitSign *= -1f;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
            }

            var tangent = Vector3.Cross(Vector3.up, dir) * state.orbitSign;
            if (dist > radius + 3f)
                return (dir * 0.4f + tangent * 0.6f).normalized * (agent.moveSpeed * 0.85f);
            if (dist < radius - 2f)
                return (-dir * 0.25f + tangent * 0.75f).normalized * (agent.moveSpeed * 0.75f);
            return tangent * (agent.moveSpeed * 0.9f);
        }

        Vector3 TickBombBoat(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            if (state.phase == EnemyCombatPhase.Execute)
            {
                state.phaseTimer -= dt;
                return state.committedDirection * 14f;
            }

            if (state.phase == EnemyCombatPhase.Telegraph)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                {
                    state.phase = EnemyCombatPhase.Execute;
                    state.phaseTimer = 2.5f;
                }
                return Vector3.zero;
            }

            if (dist <= 5f)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 0.35f;
                playerVelocity.y = 0f;
                state.committedPosition = playerPos + playerVelocity * 0.25f;
                state.committedDirection = (state.committedPosition - agent.position).normalized;
                if (state.committedDirection.sqrMagnitude < 0.01f)
                    state.committedDirection = dir;
                return Vector3.zero;
            }

            return EnemySimulation.GetPursuitDirection(agent.id, agent.position, agent.moveSpeed, playerPos, playerVelocity, 0f)
                * (agent.moveSpeed * 1.5f);
        }

        Vector3 TickRepairTenderApproach(
            EnemySimulation world,
            int selfIndex,
            ref EnemyUnit agent,
            Vector3 dir,
            float dist,
            float dt)
        {
            var agents = world.Agents;
            var bestIndex = -1;
            var bestMissing = 0f;
            for (var i = 0; i < agents.Length; i++)
            {
                if (i == selfIndex || !agents[i].active) continue;
                var ally = agents[i];
                if (ally.maxHp <= 0f) continue;
                var allyDef = world.Definitions[ally.definitionIndex];
                if (allyDef == null || allyDef.isBoss || allyDef.isMiniboss) continue;
                if (Sim.RulesFor(ally.definitionIndex).excludedFromRepair) continue;
                var missing = ally.maxHp - ally.hp;
                if (missing < ally.maxHp * 0.04f) continue;
                if (Vector3.Distance(ally.position, agent.position) > 20f) continue;
                if (missing <= bestMissing) continue;
                bestMissing = missing;
                bestIndex = i;
            }

            if (bestIndex >= 0)
            {
                var toAlly = agents[bestIndex].position - agent.position;
                toAlly.y = 0f;
                if (toAlly.sqrMagnitude > 9f)
                    return toAlly.normalized * (agent.moveSpeed * 0.75f);
                return (-dir * 0.35f + toAlly.normalized * 0.25f).normalized * (agent.moveSpeed * 0.4f);
            }

            if (dist < 14f)
                return (-dir).normalized * (agent.moveSpeed * 0.55f);
            return dir * (agent.moveSpeed * 0.35f);
        }

        Vector3 TickNetCutterRoute(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            Vector3 playerVelocity,
            float dt)
        {
            playerVelocity.y = 0f;
            var ahead = playerVelocity.sqrMagnitude > 0.35f
                ? playerVelocity.normalized
                : dir;
            var side = Vector3.Cross(Vector3.up, ahead) * state.orbitSign;
            return (ahead * 0.65f + side * 0.35f).normalized * (agent.moveSpeed * 0.95f);
        }

        Vector3 TickTreasureRaider(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            Vector3 playerPos,
            float dt)
        {
            if (state.phase == EnemyCombatPhase.Flee)
            {
                state.phaseTimer -= dt; // V5 M9b close (review): the simulation's step (was the frame's delta time)
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                var away = (agent.position - playerPos).normalized;
                return away * (agent.moveSpeed * 1.45f);
            }

            if (dist < 10f)
            {
                state.phase = EnemyCombatPhase.Flee;
                state.phaseTimer = 1.5f;
            }

            var escape = PlayBounds.Center == Vector3.zero
                ? -dir
                : (PlayBounds.Center - agent.position).normalized;
            return escape * (agent.moveSpeed * 1.1f);
        }

        Vector3 TickKrakenAnchor(
            ref EnemyUnit agent,
            CombatAgentState state,
            Vector3 dir,
            float dist,
            float dt)
        {
            if (state.phase == EnemyCombatPhase.Reposition)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                return dir * (agent.moveSpeed * 0.45f);
            }

            if (dist > 9f)
                return dir * (agent.moveSpeed * 0.5f);
            if (state.phase == EnemyCombatPhase.Approach)
            {
                state.phase = EnemyCombatPhase.Reposition;
                state.phaseTimer = 2f;
            }

            return Vector3.zero;
        }

        void TrySingleProjectileAttack(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            CombatAgentState state,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;
            var preferred = profile.attackRange > 0f ? profile.attackRange : profile.preferredRange;
            if (dist < preferred * 0.5f || dist > preferred * 1.55f) return;
            if (!Budget.TryReserveRanged(state.attackId)) return;

            var hostiles = Shots;
            if (hostiles == null) return;

            var dir = playerPos - agent.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir = EnemySimulation.Spread(EnemySimulation.AimLed(agent.position, playerPos, playerVelocity, profile.projectileSpeed, dir), agent);
            var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
            var shotDistance = EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, profile.projectileSpeed);
            if (!hostiles.TrySpawn(muzzle, dir, profile.projectileSpeed, agent.damage, 1.5f, 0.45f, 0.35f, shotDistance))
                return;

            agent.attackCooldown = profile.attackCooldown;
            state.attackId++;
        }

        void TrySpreadVolley(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            CombatAgentState state,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            float dt,
            DamageReceiver playerDamage)
        {
            if (state.phase == EnemyCombatPhase.Telegraph)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                {
                    FireFan(agent, def, profile, playerPos, playerVelocity);
                    state.phase = EnemyCombatPhase.Recover;
                    state.phaseTimer = 1.6f;
                    agent.attackCooldown = profile.attackCooldown;
                }
                return;
            }

            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                return;
            }

            if (agent.attackCooldown > 0f) return;
            var preferred = profile.preferredRange;
            if (dist < preferred - 3f || dist > preferred + 5f) return;

            state.phase = EnemyCombatPhase.Telegraph;
            state.phaseTimer = profile.attackWindUp;
        }

        void FireFan(in EnemyUnit agent, EnemyDefinition def, EnemyCombatProfile profile, Vector3 playerPos, Vector3 playerVelocity)
        {
            var hostiles = Shots;
            if (hostiles == null) return;
            var dir = playerPos - agent.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir = EnemySimulation.Spread(EnemySimulation.AimLed(agent.position, playerPos, playerVelocity, profile.projectileSpeed, dir), agent);
            var shotDistance = EnemySimulation.LedShotDistance(
                agent.position + dir * (agent.radius + 1.1f), playerPos, playerVelocity, profile.projectileSpeed);
            var count = Mathf.Max(1, profile.projectileCount);
            var spread = profile.spreadDegrees;
            var half = spread * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var angle = count <= 1 ? 0f : -half + spread / (count - 1) * i;
                var shotDir = Quaternion.Euler(0f, angle, 0f) * dir;
                var muzzle = agent.position + shotDir * (agent.radius + 1.1f) + Vector3.up * 0.55f;
                hostiles.TrySpawn(muzzle, shotDir, profile.projectileSpeed, agent.damage * 0.9f, 1.6f, 0.42f, 0.32f, shotDistance);
            }
        }

        void TickBombBoatDetonation(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyDefinition def,
            CombatAgentState state,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            const float blastRadius = 3f;
            // Playtest (3 Oct, owner, round 7): its blast circle rides with it from its windup until it goes off.
            if ((state.phase == EnemyCombatPhase.Execute || state.phase == EnemyCombatPhase.Telegraph) && state.phaseTimer > 0f)
                Hazards.Warn(agent.position, blastRadius, state.phaseTimer, agent.id);
            if (state.phase == EnemyCombatPhase.Execute)
            {
                if (dist <= blastRadius + 1.5f)
                    ExplodeBombBoat(world, ref agent, playerPos, playerDamage, blastRadius);
                else if (state.phaseTimer <= 0f)
                    ExplodeBombBoat(world, ref agent, playerPos, playerDamage, blastRadius);
                return;
            }

            if (state.phase != EnemyCombatPhase.Telegraph) return;
            if (state.phaseTimer > 0f) return;
            state.phase = EnemyCombatPhase.Execute;
            state.phaseTimer = 2f;
        }

        void ExplodeBombBoat(
            EnemySimulation world,
            ref EnemyUnit agent,
            Vector3 playerPos,
            DamageReceiver playerDamage,
            float blastRadius)
        {
            Hazards.ClearWarnings(agent.id);
            Effects.PlayExplosion(agent.position, blastRadius);
            if (HorizontalDistance(agent.position, playerPos) <= blastRadius && playerDamage != null)
                _services.HitPlayer(playerDamage, agent.damage * 1.4f, agent.position);
            world.DespawnByUnitId(agent.id);
        }

        void TickKrakenMarks(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            CombatAgentState state,
            Vector3 playerPos,
            float dt,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;

            if (!Budget.TryReserveAreaDenial(state.attackId)) return;
            agent.attackCooldown = profile.attackCooldown;
            state.attackId++;
            Hazards.SpawnCircle(playerPos, 1.5f, profile.attackWindUp, agent.damage * 0.75f);
            if (profile.eliteKrakenSequentialTraps)
            {
                var offset = Vector3.Cross(Vector3.up, (playerPos - agent.position).normalized) * 2.5f;
                Hazards.SpawnCircle(playerPos + offset, 1.5f, profile.attackWindUp + 0.35f, agent.damage * 0.75f);
                Hazards.SpawnCircle(playerPos - offset, 1.5f, profile.attackWindUp + 0.7f, agent.damage * 0.75f);
            }
            else
            {
                var side = Vector3.Cross(Vector3.up, (playerPos - agent.position).normalized) * 2f;
                Hazards.SpawnCircle(playerPos + side, 1.5f, profile.attackWindUp, agent.damage * 0.75f);
            }
        }

        void TryBurstCrossingShot(
            EnemySimulation world,
            ref EnemyUnit agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            CombatAgentState state,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
            float dt,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;
            if (dist < profile.preferredRange - 3f || dist > profile.preferredRange + 5f) return;

            if (state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = profile.attackWindUp;
                state.committedPosition = playerPos + playerVelocity * 0.8f;
                return;
            }

            state.phaseTimer -= dt;
            if (state.phaseTimer > 0f) return;

            var hostiles = Shots;
            if (hostiles != null)
            {
                // Lead the shot from where the player is now (the telegraph-time guess is the fallback). A profile without
                // a shot speed (the harrier's data has 0) uses the runtime default of 9 m/s, else its shots only drop.
                var shotSpeed = profile.projectileSpeed > 0f ? profile.projectileSpeed : 9f;
                var dir = EnemySimulation.Spread(EnemySimulation.AimLed(
                    agent.position, playerPos, playerVelocity, shotSpeed, state.committedPosition - agent.position), agent);
                var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                var shotDistance = EnemySimulation.LedShotDistance(muzzle, playerPos, playerVelocity, shotSpeed);
                for (var i = 0; i < 2; i++)
                    hostiles.TrySpawn(muzzle, dir, shotSpeed, agent.damage, 1.4f, 0.4f, 0.32f, shotDistance);
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }
    }
}
