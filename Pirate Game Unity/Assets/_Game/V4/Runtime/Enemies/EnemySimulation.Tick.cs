using System;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.World;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// The enemy world's frame (port of EnemyWorld.Tick and everything it calls: behaviours, movement, island and hull
    /// collision, separation, views, ranged attacks, contact damage, hazards) and the hit path. Same order, same
    /// arithmetic; enemy-id branches read the archetype's world rules.
    /// </summary>
    public sealed partial class EnemySimulation
    {
        public void Tick(float dt, Vector3 playerPos, DamageReceiver playerDamage)
        {
            var playerVelocity = ResolvePlayerVelocity(playerDamage);
            _hash.Clear();
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active) _hash.Insert(i, _agents[i].position);
            }

            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                ref var e = ref _agents[i];
                var def = _definitions[e.definitionIndex];
                _services.SpecialBehaviors.TickPreMovement(this, i, ref e, def, dt, playerPos, playerDamage);
                var desiredVelocity = TickBehavior(i, ref e, def, dt, playerPos, playerVelocity, playerDamage);
                if (e.spawnProfile != null)
                {
                    _services.SpecialBehaviors.TickPostMovement(this, ref e, ref e.patternParams, dt, playerPos);
                    desiredVelocity = e.velocity;
                }

                MoveAgent(ref e, dt, desiredVelocity, def);
                e.position.y = def.visualHeightOffset;
                if (_settings.collideWithIslands && IslandObstacles.IsAvailable)
                {
                    var collisionRadius = GetCollisionRadiusForAgent(e);
                    var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                    e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
                }
                UpdateView(ref e, def, dt, playerPos);

                var dist = HorizontalDistance(e.position, playerPos);
                if (_params.CombatEnabled && EnemyCombatProfileRegistry.UsesSaltwindBehavior(def.id))
                    _services.Behaviors.TickCombat(this, i, ref e, def, dt, playerPos, playerDamage);
                else
                    TickRangedAttack(ref e, def, dt, playerPos, dist, playerDamage);
                var playerRadius = GetPlayerContactRadius(playerDamage);
                var contactRange = e.radius + playerRadius;
                if (def.category == EnemyCategory.Creature && !def.isBoss && !def.isMiniboss)
                    contactRange = GetMinimumCreatureHullDistance(playerRadius, e.radius);

                if (dist < contactRange && e.contactCooldown <= 0f)
                {
                    var saltwindContactBlocked = _params.CombatEnabled
                        && _states.TryGet(e.id, out var combatState)
                        && combatState.contactDisabled;
                    var contactEnabled = !saltwindContactBlocked
                        && (e.spawnProfile == null || e.patternParams.contactDamageEnabled);
                    if (contactEnabled || def.isMiniboss)
                    {
                        playerDamage?.TakeDamage(e.damage, e.position, false);
                        e.contactCooldown = GameConstants.EnemyContactCooldown;
                        TryApplyCreatureContactKnockback(ref e, def, playerPos);
                        PushCreatureOffHull(ref e, playerPos, playerRadius, def);
                    }
                }
                if (e.contactCooldown > 0f) e.contactCooldown -= dt;
            }

            if (_params.CombatEnabled)
                _services.Hazards.Tick(dt);

            ResolvePlayerHullSeparation(playerPos, playerDamage);
            ResolveAgentOverlaps();
        }

        void ResolvePlayerHullSeparation(Vector3 playerPos, DamageReceiver playerDamage)
        {
            if (playerDamage == null) return;

            var playerRadius = GetPlayerContactRadius(playerDamage);
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                ref var agent = ref _agents[i];
                var def = _definitions[agent.definitionIndex];
                if (def.category != EnemyCategory.Creature || def.isBoss || def.isMiniboss)
                    continue;

                var padding = GameConstants.CreatureHullSeparationPadding;
                var required = playerRadius + agent.radius + padding;
                var offset = agent.position - playerPos;
                offset.y = 0f;
                var dist = offset.magnitude;
                if (dist >= required)
                    continue;

                if (dist < 0.001f)
                    offset = new Vector3(agent.id % 2 == 0 ? 1f : -1f, 0f, 0.35f);

                agent.position = playerPos + offset.normalized * required;
                var toPlayer = playerPos - agent.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    var approach = Vector3.Dot(agent.velocity, toPlayer.normalized);
                    if (approach > 0f)
                        agent.velocity -= toPlayer.normalized * approach;
                }

                if (agent.view != null)
                    agent.view.position = agent.position;
            }
        }

        static float GetMinimumCreatureHullDistance(float playerRadius, float enemyRadius) =>
            playerRadius + enemyRadius + GameConstants.CreatureHullSeparationPadding;

        static Vector3 ResolvePlayerVelocity(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return Vector3.zero;
            var ship = playerDamage.GetComponent<HighSeas.Player.PlayerShipController>();
            if (ship == null) return Vector3.zero;
            var velocity = ship.Velocity;
            velocity.y = 0f;
            return velocity;
        }

        static void TryApplyCreatureContactKnockback(ref EnemyUnit agent, EnemyDefinition def, Vector3 playerPos)
        {
            if (def.category != EnemyCategory.Creature || def.isBoss || def.isMiniboss)
                return;

            var away = agent.position - playerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = new Vector3(agent.id % 2 == 0 ? 1f : -1f, 0f, 0.6f);
            agent.knockbackDir = away.normalized;
            agent.knockbackTimer = GameConstants.CreatureKnockbackSeconds;
        }

        static void PushCreatureOffHull(ref EnemyUnit agent, Vector3 playerPos, float playerRadius, EnemyDefinition def)
        {
            if (def.isMiniboss || def.isBoss) return;
            var required = GetMinimumCreatureHullDistance(playerRadius, agent.radius);
            var offset = agent.position - playerPos;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.0001f)
                offset = agent.knockbackDir.sqrMagnitude > 0.01f ? agent.knockbackDir : Vector3.forward;
            agent.position = playerPos + offset.normalized * required;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float GetPlayerContactRadius(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return 1.15f;
            var ship = playerDamage.GetComponent<HighSeas.Player.PlayerShipController>();
            return ship != null ? ship.IslandCollisionRadius : 1.15f;
        }

        float GetCollisionRadius(EnemyDefinition def)
        {
            var padding = def.category == EnemyCategory.Ship ? _settings.shipCollisionPadding : _settings.islandCollisionPadding;
            return def.radius + padding;
        }




        public void ResolveSpawnOverlapsOnly(int passes = 3)
        {
            for (var pass = 0; pass < passes; pass++)
                ResolveAgentOverlaps();
        }

        /// <summary>Push agents apart and off islands after promo / debug spawns (no simulation tick).</summary>
        public void ResolveSpawnLayout(int overlapPasses = 4)
        {
            for (var pass = 0; pass < overlapPasses; pass++)
            {
                ResolveAgentOverlaps();
                if (!_settings.collideWithIslands || !IslandObstacles.IsAvailable)
                    continue;

                for (var i = 0; i < _agents.Length; i++)
                {
                    if (!_agents[i].active) continue;
                    ref var e = ref _agents[i];
                    var collisionRadius = GetCollisionRadiusForAgent(e);
                    var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                    e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
                    if (e.view != null)
                        e.view.position = e.position;
                }
            }
        }

        void ResolveAgentOverlaps()
        {
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < _agents.Length; i++)
                {
                    if (!_agents[i].active) continue;
                    ref var self = ref _agents[i];

                    for (var j = i + 1; j < _agents.Length; j++)
                    {
                        if (!_agents[j].active) continue;
                        ref var other = ref _agents[j];

                        var delta = self.position - other.position;
                        delta.y = 0f;
                        var minDist = self.radius + other.radius + _settings.enemySeparationPadding;
                        var dist = delta.magnitude;
                        if (dist >= minDist)
                            continue;

                        Vector3 push;
                        if (dist > 0.001f)
                            push = delta.normalized * ((minDist - dist) * 0.5f);
                        else
                            push = new Vector3((self.id % 2 == 0 ? 1f : -1f) * minDist * 0.5f, 0f, 0f);

                        self.position += push;
                        other.position -= push;
                    }
                }
            }
        }

        void MoveAgent(ref EnemyUnit e, float dt, Vector3 desiredVelocity, EnemyDefinition def)
        {
            var delta = e.velocity * dt;
            if (!_settings.collideWithIslands || !IslandObstacles.IsAvailable)
            {
                e.position += delta;
                return;
            }

            var collisionRadius = GetCollisionRadiusForAgent(e);
            var preferredDirection = desiredVelocity;
            preferredDirection.y = 0f;

            e.velocity = IslandObstacles.ApplyAvoidanceSteering(
                e.position,
                e.velocity,
                preferredDirection,
                collisionRadius,
                _settings.islandAvoidanceProbeDistance,
                e.id);

            delta = e.velocity * dt;
            var height = IslandObstacles.GetCollisionHeight(collisionRadius);
            e.position = IslandObstacles.ResolveCapsuleMovement(
                e.position,
                delta,
                collisionRadius,
                height,
                ref e.velocity,
                preferredDirection);

            e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
        }

        void UpdateView(ref EnemyUnit e, EnemyDefinition def, float dt, Vector3 playerPos)
        {
            if (e.view == null) return;

            e.view.position = e.position;

            if (RulesFor(e.definitionIndex).swordfishSpringDash)
                _views.TickSpring(e.view, ref e.swordfishSpringVisual, e.chargeTimer > 0f);

            Vector3 faceDirection;
            if (e.velocity.sqrMagnitude >= 0.01f)
                faceDirection = e.velocity;
            else if (def.behavior == EnemyBehavior.TelegraphedCharge)
            {
                faceDirection = playerPos - e.position;
                faceDirection.y = 0f;
                if (faceDirection.sqrMagnitude < 0.01f)
                    return;
            }
            else
            {
                return;
            }

            var facing = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
            if (Mathf.Abs(def.visualYawOffset) > 0.01f)
                facing *= Quaternion.Euler(0f, def.visualYawOffset, 0f);

            e.view.rotation = Quaternion.Slerp(e.view.rotation, facing, 1f - Mathf.Exp(-12f * dt));
            _views.TickWake(e.view, def.category, e.velocity);
        }

        Vector3 TickBehavior(int agentIndex, ref EnemyUnit e, EnemyDefinition def, float dt, Vector3 playerPos, Vector3 playerVelocity, DamageReceiver playerDamage = null)
        {
            if (e.isBoss && agentIndex == _bossControlSlot)
            {
                if (_bossPauseMovement)
                {
                    e.velocity = Vector3.zero;
                    return Vector3.zero;
                }

                if (_bossUseForcedVelocity)
                {
                    e.velocity = _bossForcedVelocity;
                    return _bossForcedVelocity;
                }
            }

            if (e.knockbackTimer > 0f)
            {
                e.knockbackTimer -= dt;
                var fade = Mathf.Clamp01(e.knockbackTimer / GameConstants.CreatureKnockbackSeconds);
                return e.knockbackDir * (e.moveSpeed * (1.6f + 0.9f * fade));
            }

            var toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            var dir = dist > 0.01f ? toPlayer / dist : Vector3.forward;

            if (_params.CombatEnabled
                && EnemyCombatProfileRegistry.TryGet(def.id, out var combatProfile)
                && combatProfile.archetype == SaltwindCombatArchetype.ReefCrabMiniboss)
            {
                var crabVel = _services.ReefCrab.TickMovement(
                    agentIndex, ref e, def, dt, playerPos, playerDamage);
                crabVel = ApplySeparation(agentIndex, crabVel, ref e, crabVel);
                e.velocity = Vector3.ClampMagnitude(crabVel, e.moveSpeed * 1.1f);
                return crabVel;
            }

            // (V3 ReefCrabMinibossBehavior and the shark rush only ran with the Saltwind combat feature off; V4 runs always
            // have it on, so they are not ported.)

            if (_params.CombatEnabled
                && EnemyCombatProfileRegistry.TryGet(def.id, out var saltwindProfile)
                && saltwindProfile.archetype != SaltwindCombatArchetype.ReefCrabMiniboss
                && _services.Behaviors.TryTickMovement(this, agentIndex, ref e, def, saltwindProfile, dt, playerPos, playerVelocity, playerDamage,
                    out var saltwindVel))
            {
                var saltwindIntent = saltwindVel;
                saltwindVel = ApplySeparation(agentIndex, saltwindVel, ref e, saltwindIntent);
                e.velocity = Vector3.ClampMagnitude(saltwindVel, e.moveSpeed * EnemyCombatBehaviors.GetSpeedCapMult(saltwindProfile));
                return saltwindIntent;
            }


            var usesCreatureIntercept = def.category == EnemyCategory.Creature && !def.isBoss && !def.isMiniboss;
            var chaseDir = usesCreatureIntercept
                ? GetCreatureInterceptDirection(e, playerPos, playerVelocity, dist, dir)
                : GetEncircleChaseDirection(e.id, e.position, playerPos, dist, dir, playerVelocity);

            if (e.spawnProfile != null)
            {
                Vector3 patternVel;
                if (def.behavior == EnemyBehavior.DiveLunge)
                {
                    patternVel = TickSwordfishBehavior(ref e, chaseDir, dir, dist, dt, playerPos, playerVelocity);
                }
                else
                {
                    patternVel = EnemyPatternMovement.ComputeVelocity(
                        e,
                        e.patternParams,
                        chaseDir,
                        dir,
                        dist,
                        dt,
                        ref e.chargeTimer,
                        playerVelocity);
                }

                var patternIntent = patternVel;
                patternVel = ApplySeparation(agentIndex, patternVel, ref e, patternIntent);
                e.velocity = Vector3.ClampMagnitude(patternVel, e.moveSpeed * 1.5f);
                return patternIntent;
            }

            if (RulesFor(e.definitionIndex).swordfishSpringDash)
            {
                var swordfishVel = TickSwordfishBehavior(ref e, chaseDir, dir, dist, dt, playerPos, playerVelocity);
                var swordfishIntent = swordfishVel;
                swordfishVel = ApplySeparation(agentIndex, swordfishVel, ref e, swordfishIntent);
                var swordfishCap = e.moveSpeed * 1.5f;
                if (swordfishVel.sqrMagnitude > e.moveSpeed * e.moveSpeed * 2.1f)
                    swordfishCap = e.moveSpeed * 2.6f;
                e.velocity = Vector3.ClampMagnitude(swordfishVel, swordfishCap);
                return swordfishIntent;
            }

            Vector3 behaviorVelocity;

            switch (def.behavior)
            {
                case EnemyBehavior.DirectChaser:
                case EnemyBehavior.TankChaser:
                case EnemyBehavior.DiveLunge:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    break;
                case EnemyBehavior.FlankingChaser:
                {
                    var aimDir = usesCreatureIntercept ? chaseDir : dir;
                    var flankSign = (e.id & 1) == 0 ? 1f : -1f;
                    var flank = Vector3.Cross(Vector3.up, aimDir) * flankSign;
                    behaviorVelocity = (aimDir + flank * 0.35f).normalized * e.moveSpeed;
                    break;
                }
                case EnemyBehavior.TelegraphedCharge:
                    e.chargeTimer -= dt;
                    if (def.isMiniboss)
                    {
                        const float miniWindUp = 0.45f;
                        const float miniChargeDuration = 0.42f;
                        const float miniCooldown = 0.95f;
                        if (e.chargeTimer <= -miniChargeDuration)
                            e.chargeTimer = miniCooldown;
                        if (e.chargeTimer <= 0f)
                            behaviorVelocity = dir * (e.moveSpeed * 3.5f);
                        else if (e.chargeTimer <= miniWindUp)
                            behaviorVelocity = Vector3.zero;
                        else
                            behaviorVelocity = dir * (e.moveSpeed * 0.95f);
                    }
                    else if (e.chargeTimer <= 0f)
                        behaviorVelocity = dir * (e.moveSpeed * 2.5f);
                    else if (e.chargeTimer > 1.5f)
                        behaviorVelocity = dir * (e.moveSpeed * 0.2f);
                    else
                        behaviorVelocity = Vector3.zero;
                    if (!def.isMiniboss && e.chargeTimer <= -0.35f)
                        e.chargeTimer = 2f;
                    break;
                case EnemyBehavior.MaintainRange:
                case EnemyBehavior.FormationRanged:
                {
                    var preferred = def.preferredRange > 0f ? def.preferredRange : 10f;
                    var playerStatic = playerVelocity.sqrMagnitude < 0.12f;
                    if (playerStatic)
                    {
                        if (dist > Mathf.Max(2.5f, preferred * 0.35f))
                            behaviorVelocity = dir * (e.moveSpeed * 0.85f);
                        else
                            behaviorVelocity = Vector3.zero;
                        break;
                    }

                    if (def.category == EnemyCategory.Ship && e.attackCooldown > 0f)
                    {
                        behaviorVelocity = Vector3.zero;
                        break;
                    }

                    if (def.isMiniboss && dist > preferred * 0.35f)
                        behaviorVelocity = dir * e.moveSpeed;
                    else if (dist > preferred + 3f)
                        behaviorVelocity = dir * (e.moveSpeed * 0.65f);
                    else if (dist < preferred - 2f)
                    {
                        if (def.isMiniboss)
                            behaviorVelocity = dir * (e.moveSpeed * 0.85f);
                        else
                        {
                            var strafeSign = (e.id & 1) == 0 ? 1f : -1f;
                            var tangent = Vector3.Cross(Vector3.up, dir) * strafeSign;
                            behaviorVelocity = tangent * (e.moveSpeed * 0.35f);
                        }
                    }
                    else if (def.isMiniboss)
                        behaviorVelocity = dir * (e.moveSpeed * 0.55f);
                    else
                        behaviorVelocity = Vector3.zero;
                    break;
                }
                case EnemyBehavior.SuicideApproach:
                    behaviorVelocity = chaseDir * (e.moveSpeed * 1.4f);
                    break;
                case EnemyBehavior.StrafeBurst:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    behaviorVelocity = tangent * e.moveSpeed + dir * (e.moveSpeed * 0.25f);
                    e.attackCooldown -= dt;
                    break;
                }
                case EnemyBehavior.FastSwarm:
                    behaviorVelocity = chaseDir * (e.moveSpeed * 1.2f);
                    break;
                case EnemyBehavior.DriftPulse:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    e.attackCooldown -= dt;
                    break;
                case EnemyBehavior.StationaryTurret:
                    behaviorVelocity = Vector3.zero;
                    break;
                default:
                    behaviorVelocity = chaseDir * e.moveSpeed;
                    break;
            }

            var intent = behaviorVelocity;
            behaviorVelocity = ApplySeparation(agentIndex, behaviorVelocity, ref e, intent);
            var maxSpeed = e.moveSpeed * 1.5f;
            if (e.knockbackTimer > 0f)
                maxSpeed = e.moveSpeed * 2.5f;
            else if (RulesFor(e.definitionIndex).swordfishSpringDash && behaviorVelocity.sqrMagnitude > e.moveSpeed * e.moveSpeed * 2.1f)
                maxSpeed = e.moveSpeed * 2.6f;
            e.velocity = Vector3.ClampMagnitude(behaviorVelocity, maxSpeed);
            return intent;
        }

        static Vector3 GetCreatureInterceptDirection(
            in EnemyUnit agent,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float distToPlayer,
            Vector3 directDir)
        {
            playerVelocity.y = 0f;
            var playerSpeed = playerVelocity.magnitude;
            var leadTime = playerSpeed > 0.35f
                ? Mathf.Clamp(distToPlayer / Mathf.Max(1f, agent.moveSpeed + playerSpeed * 0.65f), 0.12f, 2.2f)
                : 0f;

            var toEnemy = agent.position - playerPos;
            toEnemy.y = 0f;
            if (playerSpeed > 0.35f && toEnemy.sqrMagnitude > 0.01f)
            {
                var forward = playerVelocity / playerSpeed;
                var alignment = Vector3.Dot(toEnemy.normalized, forward);
                if (alignment > -0.35f)
                    leadTime *= Mathf.Lerp(1.08f, 1.42f, Mathf.Clamp01(alignment));
            }

            var predicted = playerPos + playerVelocity * leadTime;
            var toPredicted = predicted - agent.position;
            toPredicted.y = 0f;
            if (toPredicted.sqrMagnitude < 0.01f)
                return GetEncircleChaseDirection(agent.id, agent.position, playerPos, distToPlayer, directDir, playerVelocity);

            var interceptDir = toPredicted.normalized;
            var encircleDir = GetEncircleChaseDirection(agent.id, agent.position, playerPos, distToPlayer, directDir, playerVelocity);
            var interceptWeight = distToPlayer > 16f ? 0.38f : distToPlayer > 8f ? 0.65f : 0.9f;
            if (playerSpeed > 0.75f && toEnemy.sqrMagnitude > 0.01f)
            {
                var alignment = Vector3.Dot(toEnemy.normalized, playerVelocity.normalized);
                if (alignment > -0.25f)
                    interceptWeight = Mathf.Min(0.96f, interceptWeight + 0.12f);
            }

            return Vector3.Slerp(encircleDir, interceptDir, interceptWeight).normalized;
        }

        /// <summary>Straight rush — slow approach, faster commit when close (no orbit or charge windup).</summary>
        static Vector3 TickSharkBehavior(
            ref EnemyUnit agent,
            Vector3 directDir,
            float dist,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            directDir.y = 0f;
            if (directDir.sqrMagnitude < 0.01f)
                directDir = Vector3.forward;
            directDir.Normalize();

            var playerSpeed = playerVelocity.magnitude;
            if (playerSpeed > 0.5f && dist > 9f)
            {
                var leadTime = Mathf.Clamp(dist / Mathf.Max(playerSpeed, 0.5f), 0.15f, 0.55f);
                var predicted = playerPos + playerVelocity * leadTime;
                var toLead = predicted - agent.position;
                toLead.y = 0f;
                if (toLead.sqrMagnitude > 0.04f)
                    directDir = toLead.normalized;
            }

            if (dist > 16f)
                return directDir * (agent.moveSpeed * 0.82f);
            if (dist > 7f)
                return directDir * (agent.moveSpeed * 1.05f);
            return directDir * (agent.moveSpeed * 1.28f);
        }

        static Vector3 TickSwordfishBehavior(
            ref EnemyUnit agent,
            Vector3 cruiseDir,
            Vector3 directDir,
            float dist,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity)
        {
            const float springRange = 11f;
            const float instantSpringRange = 5.5f;
            const float springDuration = 0.34f;
            const float retreatDuration = 0.55f;
            const float springRetriggerCooldown = 2f;
            const float springSpeedMult = 3.1f;
            const float cruiseMult = 1.12f;

            directDir.y = 0f;
            if (directDir.sqrMagnitude < 0.01f)
                directDir = Vector3.forward;
            directDir.Normalize();

            if (agent.chargeTimer > 0f)
            {
                agent.chargeTimer -= dt;
                if (agent.chargeTimer <= 0f)
                    agent.chargeTimer = -retreatDuration;
                return directDir * (agent.moveSpeed * springSpeedMult);
            }

            if (agent.chargeTimer < 0f && agent.chargeTimer > -retreatDuration)
            {
                agent.chargeTimer += dt;
                if (agent.chargeTimer >= 0f)
                    agent.chargeTimer = -springRetriggerCooldown;
                return -directDir * (agent.moveSpeed * 0.72f);
            }

            if (agent.chargeTimer < 0f)
            {
                agent.chargeTimer += dt;
                if (agent.chargeTimer > 0f)
                    agent.chargeTimer = 0f;
                var tangent = Vector3.Cross(Vector3.up, directDir) * ((agent.id & 1) == 0 ? 1f : -1f);
                return (tangent * 0.62f + directDir * 0.18f).normalized * (agent.moveSpeed * cruiseMult);
            }

            if (dist <= instantSpringRange && SwordfishLegacyReadyToSpring(agent, retreatDuration))
            {
                agent.chargeTimer = springDuration;
                return directDir * (agent.moveSpeed * springSpeedMult);
            }

            if (dist <= springRange)
            {
                agent.chargeTimer = springDuration;
                return directDir * (agent.moveSpeed * springSpeedMult);
            }

            var orbit = Vector3.Cross(Vector3.up, directDir) * ((agent.id & 1) == 0 ? 1f : -1f);
            return (orbit * 0.42f + directDir * 0.58f).normalized * (agent.moveSpeed * cruiseMult);
        }

        static bool SwordfishLegacyReadyToSpring(in EnemyUnit agent, float retreatDuration)
        {
            if (agent.chargeTimer > 0f) return false;
            if (agent.chargeTimer < 0f && agent.chargeTimer > -retreatDuration) return false;
            return true;
        }

        /// <summary>Spread melee swarms around the player instead of stacking in a rear conga line.</summary>
        static Vector3 GetEncircleChaseDirection(
            int agentId,
            Vector3 agentPos,
            Vector3 playerPos,
            float distToPlayer,
            Vector3 directDir,
            Vector3 playerVelocity)
        {
            if (distToPlayer < 0.01f)
                return Vector3.forward;

            playerVelocity.y = 0f;
            if (playerVelocity.sqrMagnitude < 0.12f)
                return directDir;

            var ringRadius = Mathf.Clamp(distToPlayer * 0.42f, 3.5f, 9f);
            var slotAngle = agentId * 2.39996323f;
            var slotOffset = new Vector3(Mathf.Cos(slotAngle), 0f, Mathf.Sin(slotAngle)) * ringRadius;
            var slotTarget = playerPos + slotOffset;
            var toSlot = slotTarget - agentPos;
            toSlot.y = 0f;

            if (toSlot.sqrMagnitude < 0.01f)
                return directDir;

            var encircleDir = toSlot.normalized;
            var encircleWeight = distToPlayer > 14f ? 0.72f : distToPlayer > 7f ? 0.55f : 0.28f;
            if (playerVelocity.sqrMagnitude < 0.75f)
                encircleWeight *= 0.35f;
            return Vector3.Slerp(directDir, encircleDir, encircleWeight).normalized;
        }

        void TickRangedAttack(
            ref EnemyUnit e,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            float dist,
            DamageReceiver playerDamage)
        {
            var rules = RulesFor(e.definitionIndex);
            if (!SupportsRangedAttacks(def, rules))
                return;

            if (rules.noProfilelessRangedAttack)
                return;

            if (e.spawnProfile != null)
            {
                var attackPattern = e.patternParams.attackPattern;
                if (attackPattern == EnemyAttackPattern.Contact || attackPattern == EnemyAttackPattern.None)
                    return;

                e.attackCooldown -= dt;
                if (e.attackCooldown > 0f) return;

                var preferred = e.patternParams.attackRange > 0f
                    ? e.patternParams.attackRange
                    : def.preferredRange > 0f ? def.preferredRange : 12f;
                var minRange = preferred * 0.7f;
                var maxRange = preferred * 1.25f;
                if (dist < minRange || dist > maxRange)
                    return;

                var dir = playerPos - e.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) return;

                if (IslandObstacles.IsAvailable)
                {
                    var origin = e.position + Vector3.up * 0.5f;
                    var target = playerPos + Vector3.up * 0.5f;
                    if (Physics.Linecast(origin, target, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
                        return;
                }

                var hostiles = _services.Shots;
                if (hostiles == null) return;

                dir.Normalize();
                var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
                GetHostileShotProfile(def, rules, out var speed, out var life, out var hitRadius, out var visualRadius);
                if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius))
                    return;

                e.attackCooldown = e.patternParams.attackCooldown > 0f
                    ? e.patternParams.attackCooldown
                    : GetAttackRecovery(def, rules);
                return;
            }
            else
            {
            if (def.category != EnemyCategory.Ship &&
                def.behavior != EnemyBehavior.MaintainRange &&
                def.behavior != EnemyBehavior.FormationRanged)
                return;

            e.attackCooldown -= dt;
            if (e.attackCooldown > 0f) return;

            var preferred = def.preferredRange > 0f ? def.preferredRange : 12f;
            var minRange = rules.rangedMinRangeFactor.enabled ? preferred * rules.rangedMinRangeFactor.value : preferred * 0.7f;
            var maxRange = rules.rangedMaxRangeFactor.enabled ? preferred * rules.rangedMaxRangeFactor.value : preferred * 1.25f;
            if (dist < minRange || dist > maxRange)
                return;

            var dir = playerPos - e.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            if (IslandObstacles.IsAvailable)
            {
                var origin = e.position + Vector3.up * 0.5f;
                var target = playerPos + Vector3.up * 0.5f;
                if (Physics.Linecast(origin, target, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
                    return;
            }

            var hostiles = _services.Shots;
            if (hostiles == null) return;

            dir.Normalize();
            var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
            GetHostileShotProfile(def, rules, out var speed, out var life, out var hitRadius, out var visualRadius);
            if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius))
                return;

            e.attackCooldown = GetAttackRecovery(def, rules);

            if (rules.rangedFlankShots)
            {
                var flank = Vector3.Cross(Vector3.up, dir);
                hostiles.TrySpawn(muzzle, (dir + flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius);
                hostiles.TrySpawn(muzzle, (dir - flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius);
            }
            }
        }



        /// <summary>V3 EnemyRangedAttackRules.SupportsRangedAttacks; its enemy-id list is <c>world.alwaysRanged</c>.</summary>
        static bool SupportsRangedAttacks(EnemyDefinition def, ArchetypeWorldRules rules)
        {
            if (def == null) return false;
            if (rules.alwaysRanged) return true;
            return def.category == EnemyCategory.Ship &&
                   (def.behavior == EnemyBehavior.MaintainRange ||
                    def.behavior == EnemyBehavior.FormationRanged ||
                    def.behavior == EnemyBehavior.StationaryTurret);
        }

        static float GetAttackRecovery(EnemyDefinition def, ArchetypeWorldRules rules)
        {
            if (rules.rangedRecovery.enabled)
                return rules.rangedRecovery.value;

            var recovery = def.attackCooldown > 0f ? def.attackCooldown : 2.4f;
            if (def.category == EnemyCategory.Ship)
                recovery = Mathf.Max(recovery, 2.5f);
            return recovery;
        }

        static void GetHostileShotProfile(
            EnemyDefinition def,
            ArchetypeWorldRules rules,
            out float speed,
            out float life,
            out float hitRadius,
            out float visualRadius)
        {
            if (rules.ownShotProfile)
            {
                speed = rules.shotSpeed;
                life = rules.shotLife;
                hitRadius = rules.shotHitRadius;
                visualRadius = rules.shotVisualRadius;
                return;
            }

            if (def.category == EnemyCategory.Ship)
            {
                speed = 9f;
                life = 1.35f;
                hitRadius = 0.22f;
                visualRadius = 0.13f;
                return;
            }

            speed = 9f;
            life = 1.3f;
            hitRadius = 0.22f;
            visualRadius = 0.13f;
        }

        Vector3 ApplySeparation(int agentIndex, Vector3 velocity, ref EnemyUnit self, Vector3 preferredDirection)
        {
            var nearby = _hash.Query(self.position, self.radius + 1.5f, 8);
            var push = Vector3.zero;
            var count = 0;
            for (var i = 0; i < nearby.Count; i++)
            {
                var index = nearby[i];
                if (index == agentIndex || !_agents[index].active) continue;
                var delta = self.position - _agents[index].position;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist < 0.01f || dist > self.radius + _agents[index].radius + 1.5f) continue;
                push += delta.normalized * (1f - dist / (self.radius + _agents[index].radius + 1.5f));
                count++;
            }

            if (count == 0) return velocity;
            push /= count;

            var separated = velocity + push * 3f;
            if (preferredDirection.sqrMagnitude < 0.01f)
                return separated;

            preferredDirection.y = 0f;
            preferredDirection.Normalize();
            var forwardAmount = Vector3.Dot(separated, preferredDirection);
            if (forwardAmount >= 0f)
                return separated;

            var slide = Vector3.ProjectOnPlane(separated, preferredDirection);
            return slide.sqrMagnitude > 0.01f ? slide : velocity;
        }

        public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
        {
            index = FindNearest(from, range, preferElite);
            if (index >= 0 || !preferElite) return index >= 0;
            index = FindNearest(from, range, false);
            return index >= 0;
        }

        int FindNearest(Vector3 from, float range, bool eliteOnly)
        {
            _hash.Clear();
            for (var i = 0; i < _agents.Length; i++)
                if (_agents[i].active) _hash.Insert(i, _agents[i].position);

            var best = -1;
            var bestDist = range * range;
            var candidates = _hash.Query(from, range, 64);
            for (var qi = 0; qi < candidates.Count; qi++)
            {
                var i = candidates[qi];
                if (!_agents[i].active) continue;
                if (eliteOnly && !_agents[i].isElite) continue;
                var d = (_agents[i].position - from).sqrMagnitude;
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        public void SetBurnVisual(int index, bool burning) =>
            _views.SetBurning(index >= 0 && index < _agents.Length ? _agents[index].view : null, burning);

        public void HealAgent(int index, float amount)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;
            var e = _agents[index];
            e.hp = Mathf.Min(e.maxHp, e.hp + Mathf.Max(0f, amount));
            _agents[index] = e;
            _views.SetHealthFill(e.healthBar, e.hp / Mathf.Max(1f, e.maxHp));
        }

        public void ApplyDamage(int index, float damage, bool isCrit, bool showHitFeedback = true, string weaponId = null)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;
            ref var e = ref _agents[index];
            if (!string.IsNullOrEmpty(weaponId))
                e.lastHitWeaponId = weaponId;
            e.hp -= damage;

            if (e.healthBar != null)
                _views.SetHealthFill(e.healthBar, e.maxHp > 0f ? e.hp / e.maxHp : 0f);

            if (showHitFeedback && e.view != null && !(SuppressHitHighlight != null && SuppressHitHighlight(weaponId)))
                _feedback?.DamageHighlight(e.view);

            if (showHitFeedback && damage > 0.25f)
            {
                if (_target != null && _target.Exists)
                {
                    var away = e.position - _target.Position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.01f)
                    {
                        e.knockbackDir = away.normalized;
                        e.knockbackTimer = Mathf.Clamp(damage * 0.018f, 0.06f, 0.28f);
                    }
                }

                _feedback?.HitShakeAndSound(damage);
            }

            _feedback?.RaiseHit(damage, isCrit, e.position, weaponId);
            if (showHitFeedback && damage >= 0.5f)
                _feedback?.DamageNumber(e.position, damage, isCrit);

            if (e.hp <= 0f)
            {
                var def = _definitions[e.definitionIndex];
                _kills?.OnKilled(e, def);
                Despawn(index);
            }
        }

    }
}
