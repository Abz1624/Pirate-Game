using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class SaltwindCombatBehaviorSystem
    {
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

        public static bool TryTickMovement(
            EnemyWorld world,
            int agentIndex,
            ref EnemyAgent agent,
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

            var state = SaltwindCombatAgentStateStore.GetOrCreate(agent.id, def.id);
            var toPlayer = playerPos - agent.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;

            switch (profile.archetype)
            {
                case SaltwindCombatArchetype.DeckhandDinghy:
                    velocity = TickDeckhand(ref agent, state, dir, dist, dt);
                    return true;
                case SaltwindCombatArchetype.Shark:
                case SaltwindCombatArchetype.DrownedPursuer:
                    velocity = TickShark(ref agent, state, dir, dist, playerPos, playerVelocity);
                    return true;
                case SaltwindCombatArchetype.Swordfish:
                    velocity = TickSwordfish(ref agent, state, profile, dir, dist, dt, playerPos);
                    return true;
                case SaltwindCombatArchetype.CutthroatSkiff:
                    velocity = TickCutthroatSkiff(ref agent, state, dir, dist, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.Hammerhead:
                case SaltwindCombatArchetype.EliteHammerhead:
                case SaltwindCombatArchetype.RammerRaft:
                case SaltwindCombatArchetype.RamRaider:
                    velocity = TickTelegraphedCharge(ref agent, state, profile, dir, dist, dt);
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
                    velocity = TickBombBoat(ref agent, state, dir, dist, dt);
                    return true;
                case SaltwindCombatArchetype.PowderBarge:
                    velocity = dir * (agent.moveSpeed * 0.55f);
                    return true;
                case SaltwindCombatArchetype.BoardingBarge:
                case SaltwindCombatArchetype.RepairTender:
                case SaltwindCombatArchetype.ShieldFrigate:
                case SaltwindCombatArchetype.GunnerCog:
                    velocity = TickMaintainRange(ref agent, state, profile, dir, dist, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.NetCutter:
                    velocity = TickNetCutterRoute(ref agent, state, dir, playerVelocity, dt);
                    return true;
                case SaltwindCombatArchetype.TreasureRaider:
                    velocity = TickTreasureRaider(ref agent, state, dir, dist, playerPos);
                    return true;
                case SaltwindCombatArchetype.KrakenSpawn:
                case SaltwindCombatArchetype.EliteKrakenSpawn:
                    velocity = TickKrakenAnchor(ref agent, state, dir, dist, dt);
                    return true;
                default:
                    velocity = EnemyPatternMovement.ComputeVelocity(
                        agent, agent.patternParams, dir, dir, dist, dt, ref agent.chargeTimer, playerVelocity);
                    return true;
            }
        }

        public static void TickCombat(
            EnemyWorld world,
            int agentIndex,
            ref EnemyAgent agent,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            DamageReceiver playerDamage)
        {
            if (!EnemyCombatProfileRegistry.TryGet(def.id, out var profile)) return;
            if (profile.archetype == SaltwindCombatArchetype.ReefCrabMiniboss) return;

            var state = SaltwindCombatAgentStateStore.GetOrCreate(agent.id, def.id);
            var dist = HorizontalDistance(agent.position, playerPos);

            switch (profile.archetype)
            {
                case SaltwindCombatArchetype.Cannonboat:
                case SaltwindCombatArchetype.NeedleSloop:
                    TrySingleProjectileAttack(world, ref agent, def, profile, state, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.MantaRay:
                    TrySpreadVolley(world, ref agent, def, profile, state, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.BombBoat:
                    TickBombBoatDetonation(world, ref agent, def, state, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.BombKetch:
                    TickBombKetchMortar(world, ref agent, def, profile, state, playerPos, dist, playerDamage);
                    break;
                case SaltwindCombatArchetype.KrakenSpawn:
                case SaltwindCombatArchetype.EliteKrakenSpawn:
                    TickKrakenMarks(world, ref agent, def, profile, state, playerPos, dt, playerDamage);
                    break;
                case SaltwindCombatArchetype.DrownedHarrier:
                    TryBurstCrossingShot(world, ref agent, def, profile, state, playerPos, playerVelocity: ResolvePlayerVelocity(playerDamage), dist, playerDamage);
                    break;
            }
        }

        static Vector3 ResolvePlayerVelocity(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return Vector3.zero;
            var ship = playerDamage.GetComponent<PlayerShipController>();
            return ship != null ? ship.Velocity : Vector3.zero;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static Vector3 TickDeckhand(ref EnemyAgent agent, SaltwindCombatAgentState state, Vector3 dir, float dist, float dt)
        {
            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                return -dir * (agent.moveSpeed * 0.65f);
            }

            if (dist < agent.radius + 2.2f && state.phase == EnemyCombatPhase.Approach)
            {
                state.phase = EnemyCombatPhase.Recover;
                state.phaseTimer = 0.8f;
                return -dir * (agent.moveSpeed * 0.9f);
            }

            var wobble = Vector3.Cross(Vector3.up, dir) * Mathf.Sin(agent.id * 0.7f + Time.time * 2.2f) * 0.22f;
            return (dir + wobble).normalized * (agent.moveSpeed * 1.05f);
        }

        static Vector3 TickShark(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
            Vector3 dir,
            float dist,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= Time.deltaTime;
                if (state.phaseTimer <= 0f)
                    state.phase = EnemyCombatPhase.Approach;
                return -dir * (agent.moveSpeed * 0.85f);
            }

            playerVelocity.y = 0f;
            if (playerVelocity.sqrMagnitude > 0.5f && dist > 9f)
            {
                var leadTime = Mathf.Clamp(dist / Mathf.Max(playerVelocity.magnitude, 0.5f), 0.15f, 0.55f);
                var predicted = playerPos + playerVelocity * leadTime;
                var toLead = predicted - agent.position;
                toLead.y = 0f;
                if (toLead.sqrMagnitude > 0.04f)
                    dir = toLead.normalized;
            }

            var speed = dist > 16f ? agent.moveSpeed * 0.82f
                : dist > 7f ? agent.moveSpeed * 1.05f
                : agent.moveSpeed * 1.28f;
            return dir * speed;
        }

        static Vector3 TickSwordfish(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            float dt,
            Vector3 playerPos)
        {
            const float hoverRange = 10f;
            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Execute;
                        state.phaseTimer = profile.chargeDuration;
                        state.committedDirection = state.committedDirection.sqrMagnitude > 0.01f
                            ? state.committedDirection
                            : dir;
                    }
                    return Vector3.zero;
                case EnemyCombatPhase.Execute:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Recover;
                        state.phaseTimer = 1.2f;
                    }
                    return state.committedDirection * (agent.moveSpeed * profile.chargeSpeedMult);
                case EnemyCombatPhase.Recover:
                    state.phaseTimer -= dt;
                    if (state.phaseTimer <= 0f)
                        state.phase = EnemyCombatPhase.Approach;
                    return -state.committedDirection * (agent.moveSpeed * 0.7f);
                default:
                    if (dist > hoverRange + 2f)
                        return dir * (agent.moveSpeed * 0.95f);
                    if (dist <= hoverRange + 0.5f && state.phase == EnemyCombatPhase.Approach)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = profile.chargeWindUp;
                        state.committedDirection = dir;
                        var tangent = Vector3.Cross(Vector3.up, dir) * state.orbitSign;
                        return tangent * (agent.moveSpeed * 0.35f);
                    }

                    var orbit = Vector3.Cross(Vector3.up, dir) * state.orbitSign;
                    return orbit * (agent.moveSpeed * 0.55f);
            }
        }

        static Vector3 TickCutthroatSkiff(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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
                    if (dist > 14f)
                    {
                        var flankTarget = agent.position + rear * 8f + Vector3.Cross(Vector3.up, rear) * state.orbitSign * 6f;
                        var toFlank = flankTarget - agent.position;
                        toFlank.y = 0f;
                        if (toFlank.sqrMagnitude > 0.01f)
                            return toFlank.normalized * (agent.moveSpeed * 0.9f);
                    }

                    if (dist < 11f && state.phase == EnemyCombatPhase.Approach)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = 0.8f;
                        state.committedDirection = Vector3.Cross(Vector3.up, dir) * state.orbitSign;
                        if (state.committedDirection.sqrMagnitude < 0.01f)
                            state.committedDirection = dir;
                        state.committedDirection.Normalize();
                    }

                    return dir * (agent.moveSpeed * 0.75f);
            }
        }

        static Vector3 TickTelegraphedCharge(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
            EnemyCombatProfile profile,
            Vector3 dir,
            float dist,
            float dt)
        {
            switch (state.phase)
            {
                case EnemyCombatPhase.Telegraph:
                    state.phaseTimer -= dt;
                    state.contactDisabled = true;
                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Execute;
                        state.phaseTimer = profile.chargeDuration;
                        state.committedDirection = dir;
                        state.attackId++;
                        SaltwindCombatEncounterBudget.TryReserveCharge(state.attackId);
                    }
                    return Vector3.zero;
                case EnemyCombatPhase.Execute:
                    state.phaseTimer -= dt;
                    var speed = agent.moveSpeed * profile.chargeSpeedMult;
                    var vel = state.committedDirection * speed;
                    if (IslandObstacles.IsAvailable)
                    {
                        var next = agent.position + vel * dt;
                        var radius = agent.radius + 0.35f;
                        var height = IslandObstacles.GetCollisionHeight(radius);
                        var corrected = IslandObstacles.Depenetrate(next, radius, height);
                        if ((corrected - next).sqrMagnitude > 0.04f)
                        {
                            state.phase = EnemyCombatPhase.Recover;
                            state.phaseTimer = profile.chargeCooldown;
                            state.contactDisabled = true;
                            return Vector3.zero;
                        }
                    }

                    if (state.phaseTimer <= 0f)
                    {
                        state.phase = EnemyCombatPhase.Recover;
                        state.phaseTimer = profile.chargeCooldown;
                        state.chargePassCount++;
                        if (profile.eliteHammerheadSecondCharge && state.chargePassCount == 1)
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
                    return Vector3.zero;
                default:
                    if (dist < profile.preferredRange + 4f)
                    {
                        state.phase = EnemyCombatPhase.Telegraph;
                        state.phaseTimer = Mathf.Max(profile.chargeWindUp, SaltwindCombatEncounterBudget.MinChargeWarning);
                        state.committedDirection = dir;
                    }

                    return dir * (agent.moveSpeed * (profile.archetype == SaltwindCombatArchetype.RammerRaft ? 0.55f : 0.75f));
            }
        }

        static Vector3 TickMaintainRange(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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

        static Vector3 TickMantaOrbit(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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

        static Vector3 TickBombBoat(ref EnemyAgent agent, SaltwindCombatAgentState state, Vector3 dir, float dist, float dt)
        {
            if (state.phase == EnemyCombatPhase.Telegraph)
            {
                state.phaseTimer -= dt;
                if (state.phaseTimer <= 0f)
                    state.phaseTimer = 0f;
                return Vector3.zero;
            }

            if (dist <= 4f && state.phase != EnemyCombatPhase.Telegraph)
            {
                state.phase = EnemyCombatPhase.Telegraph;
                state.phaseTimer = 1.2f;
                return Vector3.zero;
            }

            return dir * (agent.moveSpeed * 1.35f);
        }

        static Vector3 TickNetCutterRoute(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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

        static Vector3 TickTreasureRaider(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
            Vector3 dir,
            float dist,
            Vector3 playerPos)
        {
            if (state.phase == EnemyCombatPhase.Flee)
            {
                state.phaseTimer -= Time.deltaTime;
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

        static Vector3 TickKrakenAnchor(
            ref EnemyAgent agent,
            SaltwindCombatAgentState state,
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

        static void TrySingleProjectileAttack(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;
            var preferred = profile.attackRange > 0f ? profile.attackRange : profile.preferredRange;
            if (dist < preferred * 0.7f || dist > preferred * 1.25f) return;
            if (!SaltwindCombatEncounterBudget.TryReserveRanged(state.attackId)) return;

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles == null) return;

            var dir = playerPos - agent.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
            if (!hostiles.TrySpawn(muzzle, dir, profile.projectileSpeed, agent.damage, 1.5f, 0.45f, 0.35f))
                return;

            agent.attackCooldown = profile.attackCooldown;
            state.attackId++;
        }

        static void TrySpreadVolley(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (state.phase == EnemyCombatPhase.Telegraph)
            {
                state.phaseTimer -= Time.deltaTime;
                if (state.phaseTimer <= 0f)
                {
                    FireFan(agent, def, profile, playerPos);
                    state.phase = EnemyCombatPhase.Recover;
                    state.phaseTimer = 1.6f;
                    agent.attackCooldown = profile.attackCooldown;
                }
                return;
            }

            if (state.phase == EnemyCombatPhase.Recover)
            {
                state.phaseTimer -= Time.deltaTime;
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

        static void FireFan(in EnemyAgent agent, EnemyDefinition def, EnemyCombatProfile profile, Vector3 playerPos)
        {
            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles == null) return;
            var dir = playerPos - agent.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            var count = Mathf.Max(1, profile.projectileCount);
            var spread = profile.spreadDegrees;
            var half = spread * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var angle = count <= 1 ? 0f : -half + spread / (count - 1) * i;
                var shotDir = Quaternion.Euler(0f, angle, 0f) * dir;
                var muzzle = agent.position + shotDir * (agent.radius + 1.1f) + Vector3.up * 0.55f;
                hostiles.TrySpawn(muzzle, shotDir, profile.projectileSpeed, agent.damage * 0.9f, 1.6f, 0.42f, 0.32f);
            }
        }

        static void TickBombBoatDetonation(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (state.phase != EnemyCombatPhase.Telegraph) return;
            if (state.phaseTimer > 0f) return;
            var blastRadius = 3f;
            if (HorizontalDistance(agent.position, playerPos) <= blastRadius && playerDamage != null)
                playerDamage.TakeDamage(agent.damage * 1.4f, agent.position, false);
            world.DespawnByUnitId(agent.id);
        }

        static void TickBombKetchMortar(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f) return;
            if (dist < profile.preferredRange - 4f || dist > profile.preferredRange + 4f) return;
            if (!SaltwindCombatEncounterBudget.TryReserveAreaDenial(state.attackId)) return;

            state.committedPosition = playerPos + ResolvePlayerVelocity(playerDamage) * 0.65f;
            state.phase = EnemyCombatPhase.Telegraph;
            state.phaseTimer = 1.2f;
            agent.attackCooldown = profile.attackCooldown;
            state.attackId++;
        }

        static void TickKrakenMarks(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            float dt,
            DamageReceiver playerDamage)
        {
            if (agent.attackCooldown > 0f)
            {
                agent.attackCooldown -= dt;
                return;
            }

            if (!SaltwindCombatEncounterBudget.TryReserveAreaDenial(state.attackId)) return;
            agent.attackCooldown = profile.attackCooldown;
            state.attackId++;
            SaltwindHazardTelegraph.SpawnCircle(playerPos, 1.5f, profile.attackWindUp, agent.damage * 0.75f);
            if (profile.eliteKrakenSequentialTraps)
            {
                var offset = Vector3.Cross(Vector3.up, (playerPos - agent.position).normalized) * 2.5f;
                SaltwindHazardTelegraph.SpawnCircle(playerPos + offset, 1.5f, profile.attackWindUp + 0.35f, agent.damage * 0.75f);
                SaltwindHazardTelegraph.SpawnCircle(playerPos - offset, 1.5f, profile.attackWindUp + 0.7f, agent.damage * 0.75f);
            }
            else
            {
                var side = Vector3.Cross(Vector3.up, (playerPos - agent.position).normalized) * 2f;
                SaltwindHazardTelegraph.SpawnCircle(playerPos + side, 1.5f, profile.attackWindUp, agent.damage * 0.75f);
            }
        }

        static void TryBurstCrossingShot(
            EnemyWorld world,
            ref EnemyAgent agent,
            EnemyDefinition def,
            EnemyCombatProfile profile,
            SaltwindCombatAgentState state,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float dist,
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

            state.phaseTimer -= Time.deltaTime;
            if (state.phaseTimer > 0f) return;

            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles != null)
            {
                var dir = (state.committedPosition - agent.position).normalized;
                for (var i = 0; i < 2; i++)
                {
                    var muzzle = agent.position + dir * (agent.radius + 1.2f) + Vector3.up * 0.55f;
                    hostiles.TrySpawn(muzzle, dir, profile.projectileSpeed, agent.damage, 1.4f, 0.4f, 0.32f);
                }
            }

            state.phase = EnemyCombatPhase.Approach;
            agent.attackCooldown = profile.attackCooldown;
        }
    }
}
