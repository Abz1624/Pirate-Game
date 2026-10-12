using System;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.World;
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
        // V5 M3e: profiler markers for the simulation's sections.
        static readonly Unity.Profiling.ProfilerMarker BehaviorMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Behavior"),
            MoveMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Move"),
            ViewMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.View"),
            CombatMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Combat"),
            HazardsMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Hazards"),
            OverlapMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Overlaps"),
            HashMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Hash"),
            ContactMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.Contact"),
            SeparationMarker = new Unity.Profiling.ProfilerMarker("V4.Sim.HullSeparation");

        public void Tick(float dt, Vector3 playerPos, DamageReceiver playerDamage)
        {
            _tickClock += dt; // V5 M9b close (review): the bounce hold's clock
            var playerVelocity = ResolvePlayerVelocity(playerDamage);
            TickLeash(dt, playerPos, playerVelocity);
            var frameContactRadius = GetPlayerContactRadius(playerDamage); // V5 M3e: once a frame (was once per enemy)
            using (HashMarker.Auto())
            {
                _hash.Clear();
                for (var i = 0; i < _agents.Length; i++)
                {
                    if (_agents[i].active) _hash.Insert(i, _agents[i].position);
                }
            }

            var damageSource = _services.DamageSource; // Playtest (6 Oct, owner): the session recorder's damage by source
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                ref var e = ref _agents[i];
                if (e.remote) continue; // Playtest (3 Oct): the 1v1 opponent moves by the network (SetRemoteState)
                var def = _definitions[e.definitionIndex];
                if (damageSource != null) damageSource.Current = def.id;
                Vector3 desiredVelocity;
                using (BehaviorMarker.Auto())
                {
                    _services.SpecialBehaviors.TickPreMovement(this, i, ref e, def, dt, playerPos, playerDamage);
                    desiredVelocity = TickBehavior(i, ref e, def, dt, playerPos, playerVelocity, playerDamage);
                    if (e.spawnProfile != null)
                    {
                        _services.SpecialBehaviors.TickPostMovement(this, ref e, ref e.patternParams, dt, playerPos);
                        desiredVelocity = e.velocity;
                    }
                }

                using (MoveMarker.Auto())
                {
                    MoveAgent(ref e, dt, desiredVelocity, def, playerPos);
                    e.position.y = def.visualHeightOffset;
                    if (_settings.collideWithIslands && IslandObstacles.IsAvailable)
                    {
                        var collisionRadius = GetCollisionRadiusForAgent(e);
                        var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                        e.position = IslandObstacles.Depenetrate(e.position, collisionRadius, height);
                    }
                }
                using (ViewMarker.Auto()) UpdateView(ref e, def, dt, playerPos);

                var dist = HorizontalDistance(e.position, playerPos);
                using (CombatMarker.Auto())
                {
                    if (_params.CombatEnabled && _params.UsesCombatBehavior(def.id))
                        _services.Behaviors.TickCombat(this, i, ref e, def, dt, playerPos, playerDamage);
                    else
                        TickRangedAttack(ref e, def, dt, playerPos, playerVelocity, dist, playerDamage);
                }
                using var contact = ContactMarker.Auto();
                var playerRadius = frameContactRadius;
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
                        _services.HitPlayer(playerDamage, e.damage, e.position);
                        e.contactCooldown = GameConstants.EnemyContactCooldown;
                        TryApplyCreatureContactKnockback(ref e, def, playerPos);
                        PushCreatureOffHull(ref e, playerPos, playerRadius, def);
                    }
                }
                if (e.contactCooldown > 0f) e.contactCooldown -= dt;
            }

            if (damageSource != null) damageSource.Current = "hazard";
            if (_params.CombatEnabled)
                using (HazardsMarker.Auto()) _services.Hazards.Tick(dt);
            if (damageSource != null) damageSource.Current = null;

            using (SeparationMarker.Auto()) ResolvePlayerHullSeparation(playerPos, playerDamage);
            using (OverlapMarker.Auto()) ResolveAgentOverlaps();
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

        // V5 M3e: the player's ship for its damage receiver, looked up once per receiver (twice a frame before); per
        // simulation, so nothing outlives the run.
        DamageReceiver _shipOwner;
        RoguePirates.V4.Foundation.Player.PlayerShipController _ship;

        RoguePirates.V4.Foundation.Player.PlayerShipController ShipOf(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return null;
            if (!ReferenceEquals(playerDamage, _shipOwner) || _ship == null)
            {
                _shipOwner = playerDamage;
                _ship = playerDamage.GetComponent<RoguePirates.V4.Foundation.Player.PlayerShipController>();
            }
            return _ship;
        }

        /// <summary>
        /// Playtest (3 Oct, owner: "when the player collided with an island, the enemies also reacted"): the ship's velocity
        /// includes the island bounce's push, so every chaser's intercept point, the cannon leads and the leash heading
        /// flipped with it. While a bounce lasts, enemies keep the velocity from just before the impact. Every enemy
        /// behaviour reads this (it is set once a frame in <see cref="Tick"/>).
        /// </summary>
        public Vector3 PlayerVelocityForEnemies { get; private set; }

        int _seenIslandImpacts = -1;
        float _bounceUntil = float.NegativeInfinity;
        /// <summary>V5 M9b close (review): the simulation's own clock (the sum of its steps) for the bounce hold; was the
        /// frame clock, which runs on through pauses and differs between frame rates and the match server.</summary>
        float _tickClock;

        Vector3 ResolvePlayerVelocity(DamageReceiver playerDamage)
        {
            PlayerVelocityForEnemies = ReadPlayerVelocity(playerDamage);
            return PlayerVelocityForEnemies;
        }

        Vector3 ReadPlayerVelocity(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return Vector3.zero;
            var ship = ShipOf(playerDamage);
            if (ship == null) return Vector3.zero;
            var impacts = ship.IslandImpactCount;
            if (impacts != _seenIslandImpacts)
            {
                if (_seenIslandImpacts >= 0 && impacts > _seenIslandImpacts)
                    _bounceUntil = _tickClock + Mathf.Max(0.3f, ship.IslandImpact.BounceSeconds) + 0.25f;
                _seenIslandImpacts = impacts;
            }
            if (_tickClock < _bounceUntil) return PlayerVelocityForEnemies; // the velocity from before the bounce
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

        float GetPlayerContactRadius(DamageReceiver playerDamage)
        {
            if (playerDamage == null) return 1.15f;
            var ship = ShipOf(playerDamage);
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

        /// <summary>V5 M3e: the active slots in slot order, so the overlap passes walk pairs of live enemies only (the same
        /// pairs in the same order as walking every slot, so the result is unchanged).</summary>
        int[] _activeSlots = new int[64];

        /// <summary>Hackathon (12 Oct): who gives way in an overlap: 2 boss, 1 miniboss, 0 everything else.</summary>
        int OverlapWeight(in EnemyUnit e)
        {
            if (e.isBoss) return 2;
            var def = _definitions != null && e.definitionIndex >= 0 && e.definitionIndex < _definitions.Length ? _definitions[e.definitionIndex] : null;
            return def == null ? 0 : def.isBoss ? 2 : def.isMiniboss ? 1 : 0;
        }

        void ResolveAgentOverlaps()
        {
            var count = 0;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active || _agents[i].remote) continue;
                if (count == _activeSlots.Length) System.Array.Resize(ref _activeSlots, _activeSlots.Length * 2);
                _activeSlots[count++] = i;
            }
            for (var pass = 0; pass < 2; pass++)
            {
                for (var a = 0; a < count; a++)
                {
                    ref var self = ref _agents[_activeSlots[a]];

                    for (var b = a + 1; b < count; b++)
                    {
                        ref var other = ref _agents[_activeSlots[b]];

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

                        // Hackathon (12 Oct, owner on the Seeker: "the Barnacle King randomly teleported"): a heavier enemy
                        // (boss over miniboss over the rest) is never pushed by a lighter one; the lighter takes the whole
                        // separation. Splitting it let a pack (or a miniboss) spawned on the boss shove it ~10 m in one frame.
                        var selfWeight = OverlapWeight(self);
                        var otherWeight = OverlapWeight(other);
                        if (selfWeight != otherWeight)
                        {
                            if (selfWeight < otherWeight) self.position += ClipIslandPush(self.position, push * 2f);
                            else other.position += ClipIslandPush(other.position, -push * 2f);
                            continue;
                        }
                        // Neither is shoved onto an island; what one cannot take, the other does.
                        var selfPush = ClipIslandPush(self.position, push);
                        var otherPush = ClipIslandPush(other.position, -push - (push - selfPush));
                        self.position += selfPush;
                        other.position += otherPush;
                    }
                }
            }
        }

        // ---- V5 M4: weapon slows and knockbacks ----------------------------------------------------------------------

        /// <summary>Seconds a weapon knockback takes to push its distance.</summary>
        public const float PushSeconds = 0.15f;
        /// <summary>The slow and knockback rules (the weapon rules config; the design's values when there is none).</summary>
        public System.Func<RoguePirates.V4.Config.WeaponRulesConfig> WeaponRules;

        /// <summary>
        /// Slows the enemy by <paramref name="fraction"/> of its speed for <paramref name="seconds"/>. The strongest slow
        /// holds: a weaker one does nothing while it lasts; an equal one extends it; a stronger one replaces it. Regular
        /// enemies are slowed at most <c>maxSlow</c>; bosses and minibosses take <c>bossSlowShare</c>, at most
        /// <c>bossMaxSlow</c> (V5 M8z, 7 Oct, D21, Q24: the Reef Crab like the boss, by the definition's flags). A committed
        /// charge dash and the boss's forced movement are not slowed (they keep their telegraphed reach; D21, Q25).
        /// </summary>
        public void ApplySlow(int index, float fraction, float seconds)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active || _agents[index].remote || fraction <= 0f || seconds <= 0f) return;
            ref var e = ref _agents[index];
            var def = _definitions[e.definitionIndex];
            var rules = WeaponRules?.Invoke();
            var big = IsBossLike(e, def);
            fraction = big
                ? Mathf.Min(rules != null ? rules.bossMaxSlow : 0.2f, fraction * (rules != null ? rules.bossSlowShare : 0.5f))
                : Mathf.Min(rules != null ? rules.maxSlow : 0.6f, fraction);
            if (fraction <= 0f) return;
            if (e.slowTimer > 0f && e.slowFraction > fraction + 1e-4f) return;
            if (e.slowTimer > 0f && Mathf.Abs(e.slowFraction - fraction) <= 1e-4f) e.slowTimer = Mathf.Max(e.slowTimer, seconds);
            else { e.slowFraction = fraction; e.slowTimer = seconds; }
        }

        /// <summary>V5 M8z (7 Oct, D21, Q24): bosses and minibosses share the slow and knockback rules (definition flags, no ids).</summary>
        static bool IsBossLike(in EnemyUnit e, EnemyDefinition def) => e.isBoss || (def != null && (def.isBoss || def.isMiniboss));

        /// <summary>The enemy's speed multiplier from slows (1 = none).</summary>
        public float SpeedMultiplier(int index) =>
            index >= 0 && index < _agents.Length && _agents[index].slowTimer > 0f ? 1f - _agents[index].slowFraction : 1f;

        /// <summary>Pushes the enemy <paramref name="meters"/> (at most <c>maxKnockbackMeters</c>) away from <paramref name="from"/>
        /// over <c>knockbackSeconds</c> (islands still stop it). Bosses and minibosses are not moved (V5 M8z, D21, Q24);
        /// elites go <c>eliteKnockbackShare</c> of it (combat design 2.3).</summary>
        public void Knockback(int index, Vector3 from, float meters)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active || _agents[index].remote || meters <= 0f) return;
            ref var e = ref _agents[index];
            var def = _definitions[e.definitionIndex];
            if (IsBossLike(e, def)) return;
            var rules = WeaponRules?.Invoke();
            meters = Mathf.Min(meters, rules != null ? rules.maxKnockbackMeters : 3f);
            if (e.isElite) meters *= rules != null ? rules.eliteKnockbackShare : 0.5f;
            if (meters <= 0f) return;
            var seconds = rules != null ? rules.knockbackSeconds : 0.15f;
            var away = e.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = e.velocity.sqrMagnitude > 1e-4f ? -e.velocity : Vector3.forward;
            away.y = 0f;
            e.pushVelocity = away.normalized * (meters / seconds);
            e.pushTimer = seconds;
        }

        /// <summary>V5 M4b: drags the enemy up to <paramref name="meters"/> toward <paramref name="toward"/> (never past it),
        /// with the knockback's rules: bosses and minibosses not moved, elites half, at most <c>maxKnockbackMeters</c>.</summary>
        public void Pull(int index, Vector3 toward, float meters)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active || _agents[index].remote || meters <= 0f) return;
            ref var e = ref _agents[index];
            var def = _definitions[e.definitionIndex];
            if (IsBossLike(e, def)) return;
            var rules = WeaponRules?.Invoke();
            meters = Mathf.Min(meters, rules != null ? rules.maxKnockbackMeters : 3f);
            if (e.isElite) meters *= rules != null ? rules.eliteKnockbackShare : 0.5f;
            var to = toward - e.position;
            to.y = 0f;
            meters = Mathf.Min(meters, to.magnitude - 0.3f);
            if (meters <= 0f) return;
            var seconds = rules != null ? rules.knockbackSeconds : 0.15f;
            e.pushVelocity = to.normalized * (meters / seconds);
            e.pushTimer = seconds;
        }

        /// <summary>This tick's slow multiplier and knockback displacement; counts both down.</summary>
        static float WeaponMotion(ref EnemyUnit e, float dt, out Vector3 push)
        {
            push = Vector3.zero;
            var multiplier = 1f;
            if (e.slowTimer > 0f)
            {
                multiplier = 1f - e.slowFraction;
                e.slowTimer -= dt;
                if (e.slowTimer <= 0f) { e.slowTimer = 0f; e.slowFraction = 0f; }
            }
            if (e.pushTimer > 0f)
            {
                push = e.pushVelocity * Mathf.Min(dt, e.pushTimer);
                e.pushTimer = Mathf.Max(0f, e.pushTimer - dt);
            }
            return multiplier;
        }

        void MoveAgent(ref EnemyUnit e, float dt, Vector3 desiredVelocity, EnemyDefinition def, Vector3 playerPos)
        {
            var speed = WeaponMotion(ref e, dt, out var push);
            if (IsCommittedDash(e) || (e.isBoss && _bossUseForcedVelocity)) speed = 1f; // telegraphed moves keep their reach
            var delta = e.velocity * (dt * speed) + push;
            if (!_settings.collideWithIslands || !IslandObstacles.IsAvailable)
            {
                e.position += delta;
                return;
            }

            var collisionRadius = GetCollisionRadiusForAgent(e);
            var preferredDirection = desiredVelocity;
            preferredDirection.y = 0f;

            var steerBias = e.id;
            if (!e.isBoss && e.knockbackTimer <= 0f && !IsCommittedDash(e))
            {
                RouteAroundIslands(ref e, dt, ref preferredDirection, collisionRadius, playerPos);
                // The contact slide turns the same way as the route (its tangent convention: +1 = Cross(up, normal)).
                if (e.islandAvoidSide != 0)
                    steerBias = e.islandAvoidSide > 0 ? 0 : 1;
            }

            e.velocity = IslandObstacles.ApplyAvoidanceSteering(
                e.position,
                e.velocity,
                preferredDirection,
                collisionRadius,
                _settings.islandAvoidanceProbeDistance,
                steerBias);

            delta = e.velocity * (dt * speed) + push;
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

        const float IslandCheckInterval = 0.2f;
        const float IslandRouteLookahead = 18f;
        const float IslandRouteShortLookahead = 10f;

        /// <summary>A committed dash (charge, lunge, bomb run) keeps its line; the charge code handles islands itself.</summary>
        bool IsCommittedDash(in EnemyUnit e) =>
            _params.CombatEnabled && _states.TryGet(e.id, out var state) && state.phase == EnemyCombatPhase.Execute;

        /// <summary>
        /// Island pathing: while an island blocks the way to the goal, steer along its shore on the side chosen when it was
        /// first seen (the side the goal lies on), held until the way is clear, and hug harder the nearer the agent is.
        /// Without this, agents pushed straight at the player pile up on the near shore. Physics checks are throttled.
        /// </summary>
        void RouteAroundIslands(ref EnemyUnit e, float dt, ref Vector3 preferredDirection, float collisionRadius, Vector3 playerPos)
        {
            var speed = preferredDirection.magnitude;
            if (speed < 0.05f)
                return;
            var goalDir = preferredDirection / speed;

            e.islandCheckTimer -= dt;
            if (e.islandCheckTimer <= 0f)
            {
                e.islandCheckTimer = IslandCheckInterval;
                UpdateIslandRoute(ref e, goalDir, collisionRadius, playerPos);
            }

            if (e.islandAvoidSide == 0)
                return;

            var radial = e.position - e.islandCenter;
            radial.y = 0f;
            var d = radial.magnitude;
            if (d < 0.01f)
                return;
            radial /= d;

            // The goal now lies away from the island: the way is open.
            if (Vector3.Dot(goalDir, radial) > 0.2f)
            {
                e.islandAvoidSide = 0;
                return;
            }

            var tangent = Vector3.Cross(Vector3.up, radial) * e.islandAvoidSide;
            var hold = Mathf.Max(1f, e.islandRadius);
            var radialCorrection = Mathf.Clamp((hold - d) / hold * 2f, -0.5f, 0.8f);
            var around = (tangent + radial * radialCorrection).normalized;
            if (Vector3.Dot(goalDir, around) < -0.95f)
                around = tangent;

            var nearness = Mathf.Clamp01(1f - (d - hold) / 8f);
            var routed = Vector3.Slerp(goalDir, around, Mathf.Lerp(0.35f, 1f, nearness));
            routed.y = 0f;
            if (routed.sqrMagnitude < 0.0001f)
                return;
            routed.Normalize();

            // Turn the whole velocity (separation included) by the same amount the goal was turned.
            e.velocity = Quaternion.FromToRotation(goalDir, routed) * e.velocity;
            e.velocity.y = 0f;
            preferredDirection = routed * speed;
        }

        /// <summary>One throttled look toward the goal (the player when heading at it, else a short look ahead).</summary>
        void UpdateIslandRoute(ref EnemyUnit e, Vector3 goalDir, float collisionRadius, Vector3 playerPos)
        {
            var toPlayer = playerPos - e.position;
            toPlayer.y = 0f;
            var distToPlayer = toPlayer.magnitude;
            var lookahead = IslandRouteShortLookahead;
            if (distToPlayer > 0.01f && Vector3.Dot(goalDir, toPlayer / distToPlayer) > 0.7f)
                lookahead = Mathf.Min(IslandRouteLookahead, distToPlayer);
            if (lookahead < 1.5f)
            {
                e.islandAvoidSide = 0;
                return;
            }

            // A thin probe, so an agent already touching the shore still sees it (casts ignore colliders they start inside).
            var origin = new Vector3(e.position.x, Mathf.Max(e.position.y, 0f) + 0.5f, e.position.z);
            var probeRadius = Mathf.Max(0.2f, collisionRadius * 0.5f);
            if (!Physics.SphereCast(origin, probeRadius, goalDir, out var hit, lookahead, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
            {
                e.islandAvoidSide = 0;
                return;
            }

            if (!IslandSpawnExclusionRegistry.TryGetNearestZone(hit.point, 3f, out var center, out var radius))
            {
                var bounds = hit.collider != null ? hit.collider.bounds : new Bounds(hit.point, Vector3.one);
                center = bounds.center;
                radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + 2f;
            }

            center.y = 0f;
            var sameIsland = e.islandAvoidSide != 0 && (center - e.islandCenter).sqrMagnitude < 1f;
            e.islandCenter = center;
            e.islandRadius = radius;
            if (sameIsland)
                return;

            // The side is chosen once: the way round on which the goal lies (sign of to-centre x goal). A goal dead
            // behind the centre keeps the way the agent is already drifting.
            var toCenter = center - e.position;
            toCenter.y = 0f;
            var side = toCenter.x * goalDir.z - toCenter.z * goalDir.x;
            if (Mathf.Abs(side) < 0.1f * toCenter.magnitude)
                side = toCenter.x * e.velocity.z - toCenter.z * e.velocity.x;
            e.islandAvoidSide = side < 0f ? -1 : 1;
        }

        /// <summary>
        /// Removes the part of a crowding push that points into a nearby island (an island spawn-exclusion zone), so
        /// separation and overlap resolution never shove an agent onto rock.
        /// </summary>
        static Vector3 ClipIslandPush(Vector3 position, Vector3 push)
        {
            if (IslandSpawnExclusionRegistry.Count == 0 || push.sqrMagnitude < 1e-8f)
                return push;
            if (!IslandSpawnExclusionRegistry.TryGetNearestZone(position, 2f, out var center, out _))
                return push;

            var outward = position - center;
            outward.y = 0f;
            var d = outward.magnitude;
            if (d < 0.01f)
                return push;
            outward /= d;
            var inward = -Vector3.Dot(push, outward);
            return inward > 0f ? push + outward * inward : push;
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
                && _params.TryGetCombatProfile(def.id, out var combatProfile)
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
                && _params.TryGetCombatProfile(def.id, out var saltwindProfile)
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
            var chaseDir = GetPursuitDirection(e.id, e.position, e.moveSpeed, playerPos, playerVelocity);

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
                        playerVelocity,
                        RunSeconds);
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

        const float PursuitMaxLead = 3f; // playtest (3 Oct, owner: "intercept the player's path"): was 1.2 s; now only a cap (InterceptLead)
        const float SurroundRange = 12f;
        const float SurroundMaxAngle = 75f;

        /// <summary>
        /// Chasers cut the player off: they aim where the player will be when they arrive (lead capped at
        /// <see cref="PursuitMaxLead"/> s) and, inside <paramref name="surroundRange"/>, at a slot beside that point picked
        /// by unit id (golden-ratio spread of bearings), so a pack closes from several sides instead of trailing in one line.
        /// </summary>
        // Playtest (3 Oct, owner): on the big map, enemies left far behind (they cannot catch a sailing ship) filled the cap
        // out of sight, so the player saw only a few. Ordinary enemies further than LeashDistance are moved back to just
        // outside the view, ahead of the ship (bosses, minibosses and elites stay where they are).
        // V5 (owner, 10 Oct, D28): a leashed enemy is put back just outside the run camera's view along its new bearing
        // (EnemyLeashShape: the view from RunConfig's camera data, swept from the camera's follow lag behind the ship to the
        // online client's lead ahead of it, and the margin; was 31 m ahead that day, a 26 m constant before, both
        // inside the view's top corners and sides), and an enemy that could be on screen is never moved.
        // V5 M9b close (review): the leash distance (45 m) and the hysteresis (10 m: an enemy is leashed only this far beyond
        // where the leash would put it back along its own bearing, so one just put back, sailing in, is not moved again) are
        // RunConfig's (were constants); a view with no footprint on the sea falls back to the pre-D28 26 m ahead.
        const float LeashCheckSeconds = 0.5f;
        /// <summary>V5 (owner, 10 Oct): the run's config, read at each leash check (null: RunConfig's defaults).</summary>
        public System.Func<RoguePirates.V4.Config.RunConfig> LeashConfig;
        /// <summary>Leash relocations so far, and attempts refused because the play area's edge pulled the spot into view.</summary>
        public int LeashRelocations { get; private set; }
        public int LeashRefusedInView { get; private set; }
        readonly EnemyLeashShape _leashShape = new EnemyLeashShape();
        float _leashTimer;
        const int LeashStreamSalt = 0x1EA5;
        // V5 M9a (I9a.2): the leash's own stream, seeded from the run seed at the run start (was a fixed System.Random(0x1EA5)
        // for every run, so where a leashed enemy reappears now differs from before and from run to run, by design).
        RoguePirates.V4.Foundation.Core.RunStream _leashRandom = new RoguePirates.V4.Foundation.Core.RunStream(LeashStreamSalt);

        /// <summary>V5 M9a (I9a.2): seeds the run's own streams of the enemy world (the leash) from the run seed.</summary>
        public void SeedRun(int runSeed)
        {
            _leashRandom = new RoguePirates.V4.Foundation.Core.RunStream(runSeed ^ LeashStreamSalt);
            _leashTimer = 0f;
        }

        void TickLeash(float dt, Vector3 playerPos, Vector3 playerVelocity)
        {
            _leashTimer -= dt;
            if (_leashTimer > 0f) return;
            _leashTimer = LeashCheckSeconds;
            playerVelocity.y = 0f;
            var heading = playerVelocity.sqrMagnitude > 1f ? playerVelocity.normalized : Vector3.forward;
            var config = LeashConfig != null ? LeashConfig() : null;
            var view = _leashShape.Set(config, playerVelocity);
            var margin = config != null ? config.enemyLeashViewMargin : RoguePirates.V4.Config.RunConfig.EnemyLeashViewMarginDefault;
            var leashDistance = config != null ? config.enemyLeashDistance : RoguePirates.V4.Config.RunConfig.EnemyLeashDistanceDefault;
            var hysteresis = config != null ? config.enemyLeashHysteresis : RoguePirates.V4.Config.RunConfig.EnemyLeashHysteresisDefault;
            for (var i = 0; i < _agents.Length; i++)
            {
                ref var e = ref _agents[i];
                if (!e.active || e.isBoss || e.isElite || e.remote) continue;
                var def = _definitions[e.definitionIndex];
                if (def == null || def.isMiniboss) continue;
                var distance = HorizontalDistance(e.position, playerPos);
                if (distance < leashDistance) continue;
                var clearance = margin + e.radius;
                var from = new Vector2(e.position.x - playerPos.x, e.position.z - playerPos.z);
                // Never moved while it could be on screen (it would vanish), nor just outside where it would be put back.
                if (view.Valid && distance < view.PlacementDistance(from, clearance) + hysteresis) continue;
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    var angle = ((float)_leashRandom.NextDouble() * 2f - 1f) * 75f;
                    // V5 M9b close (review): no view footprint (a misconfigured camera): the pre-D28 placement, 26 m ahead
                    // (was the margin's few metres from the ship, then refused as in view).
                    var p = playerPos + (view.Valid
                        ? view.Placement(heading, angle, clearance)
                        : Quaternion.Euler(0f, angle, 0f) * heading * RoguePirates.V4.Config.RunConfig.EnemyLeashFallbackReturnDistance);
                    p = PlayBounds.Clamp(p, e.radius + 1f);
                    p.y = def.visualHeightOffset;
                    // The play area's edge can pull the spot back into view: another bearing then.
                    if (view.Valid && view.DistanceOutside(new Vector2(p.x - playerPos.x, p.z - playerPos.z)) < clearance - 0.01f) { LeashRefusedInView++; continue; }
                    if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(p, e.radius + 0.5f)) continue;
                    e.position = p;
                    LeashRelocations++;
                    if (e.view != null)
                    {
                        e.view.position = p;
                        ClearTrails(e.view); // playtest (3 Oct, owner: "the enemy trails were behaving weirdly")
                    }
                    break;
                }
            }
        }

        static readonly System.Collections.Generic.List<TrailRenderer> TrailBuffer = new System.Collections.Generic.List<TrailRenderer>();
        static readonly System.Collections.Generic.List<ParticleSystem> ParticleBuffer = new System.Collections.Generic.List<ParticleSystem>();

        /// <summary>Playtest (3 Oct, owner): a leashed enemy jumps tens of metres in one frame; its wake trails (world
        /// space) would draw a long streak across the sea from where it was, so their points and particles are dropped.</summary>
        static void ClearTrails(Transform view)
        {
            view.GetComponentsInChildren(true, TrailBuffer);
            for (var i = 0; i < TrailBuffer.Count; i++) if (TrailBuffer[i] != null) TrailBuffer[i].Clear();
            view.GetComponentsInChildren(true, ParticleBuffer);
            for (var i = 0; i < ParticleBuffer.Count; i++) if (ParticleBuffer[i] != null) ParticleBuffer[i].Clear(false);
            TrailBuffer.Clear();
            ParticleBuffer.Clear();
        }

        /// <summary>
        /// Playtest (3 Oct, owner: the dinghy "syncs with the player on every turn"): the lead was the full travel time
        /// (up to 3 s) even when the chaser could never get there, so packs aimed far ahead of a faster ship and swung with
        /// every turn it made. Now the lead is the real intercept time when there is one (a ship sailing away gets a short
        /// one), varied per unit (65 to 100 %) so a pack does not turn as one, and the point it aims at is never further
        /// ahead of the ship than 60 % of the chaser's distance, so close in it heads for the ship itself.
        /// </summary>
        static float InterceptLead(int agentId, Vector3 toPlayer, float dist, float agentSpeed, Vector3 playerVelocity)
        {
            var playerSpeed = playerVelocity.magnitude;
            // |toPlayer + v t| = s t  ->  (v.v - s^2) t^2 + 2 (toPlayer.v) t + toPlayer.toPlayer = 0
            var a = playerSpeed * playerSpeed - agentSpeed * agentSpeed;
            var b = 2f * Vector3.Dot(toPlayer, playerVelocity);
            var c = dist * dist;
            var lead = -1f;
            if (Mathf.Abs(a) < 0.001f) { if (b < 0f) lead = -c / b; }
            else
            {
                var disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    var root = Mathf.Sqrt(disc);
                    var t1 = (-b - root) / (2f * a);
                    var t2 = (-b + root) / (2f * a);
                    lead = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
                }
            }
            if (!(lead > 0f)) lead = dist / (agentSpeed + playerSpeed); // cannot catch it: a short lead, then chase
            lead *= 0.65f + 0.35f * Mathf.Repeat(agentId * 0.381966f, 1f);
            lead = Mathf.Min(lead, PursuitMaxLead);
            return Mathf.Min(lead, dist * 0.6f / Mathf.Max(0.01f, playerSpeed));
        }

        internal static Vector3 GetPursuitDirection(
            int agentId,
            Vector3 agentPos,
            float agentSpeed,
            Vector3 playerPos,
            Vector3 playerVelocity,
            float surroundRange = SurroundRange)
        {
            var toPlayer = playerPos - agentPos;
            toPlayer.y = 0f;
            var dist = toPlayer.magnitude;
            if (dist < 0.01f)
                return Vector3.forward;

            playerVelocity.y = 0f;
            var aim = playerPos;
            if (playerVelocity.sqrMagnitude > 0.12f)
                aim = playerPos + playerVelocity * InterceptLead(agentId, toPlayer, dist, Mathf.Max(1f, agentSpeed), playerVelocity);

            if (surroundRange > 0f && dist < surroundRange && dist > 2.5f)
            {
                var fromAim = agentPos - aim;
                fromAim.y = 0f;
                if (fromAim.sqrMagnitude > 0.01f)
                {
                    var slot = Mathf.Repeat(agentId * 0.618034f, 1f) * 2f - 1f;
                    var fade = Mathf.Clamp01((surroundRange - dist) / 4f);
                    var ringRadius = Mathf.Clamp((dist - 2.5f) * 0.6f, 0f, 6f) * fade;
                    aim += Quaternion.Euler(0f, slot * SurroundMaxAngle, 0f) * (fromAim.normalized * ringRadius);
                }
            }

            var toAim = aim - agentPos;
            toAim.y = 0f;
            return toAim.sqrMagnitude > 0.01f ? toAim.normalized : toPlayer / dist;
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

        void TickRangedAttack(
            ref EnemyUnit e,
            EnemyDefinition def,
            float dt,
            Vector3 playerPos,
            Vector3 playerVelocity,
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

                GetHostileShotProfile(def, rules, out var speed, out var life, out var hitRadius, out var visualRadius);
                dir = Spread(AimLed(e.position, playerPos, playerVelocity, speed, dir), e);
                var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
                if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius,
                        LedShotDistance(muzzle, playerPos, playerVelocity, speed)))
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

            GetHostileShotProfile(def, rules, out var speed, out var life, out var hitRadius, out var visualRadius);
            dir = Spread(AimLed(e.position, playerPos, playerVelocity, speed, dir), e);
            var muzzle = e.position + dir * Mathf.Max(e.radius + 1.1f, 1.6f) + Vector3.up * 0.55f;
            var shotDistance = LedShotDistance(muzzle, playerPos, playerVelocity, speed);
            if (!hostiles.TrySpawn(muzzle, dir, speed, e.damage, life, hitRadius, visualRadius, shotDistance))
                return;

            e.attackCooldown = GetAttackRecovery(def, rules);

            if (rules.rangedFlankShots)
            {
                var flank = Vector3.Cross(Vector3.up, dir);
                hostiles.TrySpawn(muzzle, (dir + flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius, shotDistance);
                hostiles.TrySpawn(muzzle, (dir - flank * 0.55f).normalized, speed, e.damage * 0.85f, life, hitRadius, visualRadius, shotDistance);
            }
            }
        }



        /// <summary>Flat unit direction toward where a shot at <paramref name="shotSpeed"/> meets the player (lead-capped); <paramref name="fallback"/> if degenerate.</summary>
        internal static Vector3 AimLed(Vector3 from, Vector3 playerPos, Vector3 playerVelocity, float shotSpeed, Vector3 fallback)
        {
            var aim = HostileProjectiles.PredictAimPoint(from, playerPos, playerVelocity, shotSpeed) - from;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.01f) return aim.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        /// <summary>
        /// Playtest (3 Oct, owner, round 7: "firing something too accurate"): an ordinary shooter's aim wanders up to
        /// <see cref="ShotSpreadDegrees"/> either side of the led line, so holding a course is not a certain hit. Elites,
        /// minibosses and the boss keep the exact line (their shots are slow and telegraphed). Deterministic: from the
        /// unit's id and position, no random draw.
        /// </summary>
        internal const float ShotSpreadDegrees = 8f;

        internal static Vector3 Spread(Vector3 dir, in EnemyUnit agent)
        {
            if (agent.isElite || agent.isBoss) return dir;
            var h = Mathf.Sin(agent.id * 12.9898f + agent.position.x * 78.233f + agent.position.z * 37.719f) * 43758.5453f;
            var u = h - Mathf.Floor(h); // 0..1
            return Quaternion.Euler(0f, (u * 2f - 1f) * ShotSpreadDegrees, 0f) * dir;
        }

        /// <summary>Horizontal distance from the muzzle to the led aim point, so the shot's arc lands there.</summary>
        internal static float LedShotDistance(Vector3 muzzle, Vector3 playerPos, Vector3 playerVelocity, float shotSpeed) =>
            HorizontalDistance(muzzle, HostileProjectiles.PredictAimPoint(muzzle, playerPos, playerVelocity, shotSpeed));

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
            push = ClipIslandPush(self.position, push / count);

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
            if (e.remote)
            {
                // Playtest (3 Oct, owner): the 1v1 opponent's hull is its own client's; the hit goes to the server instead.
                RemoteDamaged?.Invoke(index, damage, weaponId);
                if (showHitFeedback && e.view != null && !(SuppressHitHighlight != null && SuppressHitHighlight(weaponId)))
                    _feedback?.DamageHighlight(e.view);
                HitPublished?.Invoke(damage, isCrit, e.position, weaponId);
                if (showHitFeedback && damage >= 0.5f) _feedback?.DamageNumber(e.position, damage, isCrit);
                return;
            }
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
            HitPublished?.Invoke(damage, isCrit, e.position, weaponId); // M14: the Hit event, at the moment V3 raised TriggerBus.Hit
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
