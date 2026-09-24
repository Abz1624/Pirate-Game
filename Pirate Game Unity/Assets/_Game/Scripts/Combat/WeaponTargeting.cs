using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class WeaponTargeting
    {
        public static bool UsesDirectionalGeometry(WeaponDefinition def)
        {
            if (def == null) return false;
            if (!CombatExperimentContext.Active.directionalWeaponTargetingEnabled) return false;
            return def.targeting switch
            {
                TargetingMode.ForwardArc or TargetingMode.ForwardCone or TargetingMode.AftDeploy
                    or TargetingMode.PortStarboard or TargetingMode.TetherTarget => true,
                TargetingMode.PlayerTrail when def.pattern == WeaponPattern.MineDrop => true,
                TargetingMode.TrailZone => true,
                _ => def.targetArcDegrees > 0f && def.targeting == TargetingMode.Nearest
            };
        }

        public static bool TryResolveTarget(
            EnemyWorld enemies,
            PlayerShipController player,
            WeaponDefinition def,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = player != null ? player.MovementForward : Vector3.forward;
            if (enemies == null || player == null || def == null) return false;

            var origin = player.Position;
            var forward = player.MovementForward;
            var range = def.range;

            bool resolved;
            switch (ResolveMode(def))
            {
                case TargetingMode.ForwardArc:
                case TargetingMode.TetherTarget:
                    resolved = def != null && def.preferHighestHull
                        ? TryForwardArcHighestHull(enemies, origin, forward, range, def, out targetIndex, out aimDirection)
                        : TryForwardArc(enemies, origin, forward, range, def, out targetIndex, out aimDirection);
                    break;
                case TargetingMode.ForwardCone:
                    resolved = TryForwardArc(enemies, origin, forward, range, def, out targetIndex, out aimDirection);
                    break;
                case TargetingMode.PortStarboard:
                    resolved = TryPortStarboard(enemies, origin, forward, range, out targetIndex, out aimDirection);
                    break;
                case TargetingMode.DensestCluster:
                case TargetingMode.OmniClusterGround:
                    resolved = TryDensestCluster(enemies, origin, range, def, out targetIndex, out aimDirection);
                    break;
                case TargetingMode.NearestElite:
                    resolved = TryOmniNearest(enemies, origin, range, preferElite: true, preferHighestHull: def.preferHighestHull,
                        out targetIndex, out aimDirection);
                    break;
                default:
                    if (def.id == "swivel_guns")
                        resolved = TrySwivelDefense(enemies, origin, range, out targetIndex, out aimDirection);
                    else
                        resolved = TryOmniNearest(enemies, origin, range, preferElite: def.preferElite,
                            preferHighestHull: def.preferHighestHull, out targetIndex, out aimDirection);
                    break;
            }

            if (!resolved || targetIndex < 0) return false;
            ApplyLeadAim(enemies, origin, def, targetIndex, ref aimDirection);
            return true;
        }

        static TargetingMode ResolveMode(WeaponDefinition def)
        {
            if (def.targeting == TargetingMode.PortStarboard)
                return TargetingMode.PortStarboard;
            if (!CombatExperimentContext.Active.directionalWeaponTargetingEnabled)
            {
                if (def.targeting == TargetingMode.DensestCluster) return TargetingMode.DensestCluster;
                if (def.targeting == TargetingMode.NearestElite) return TargetingMode.NearestElite;
                return TargetingMode.Nearest;
            }
            return def.targeting;
        }

        public static bool IsWithinForwardArc(Vector3 origin, Vector3 forward, Vector3 targetPos, float arcDegrees)
        {
            forward.y = 0f;
            var toTarget = targetPos - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(forward, toTarget) <= arcDegrees * 0.5f;
        }

        public static bool HasEnemyInForwardArc(
            EnemyWorld enemies,
            Vector3 origin,
            Vector3 forward,
            float range,
            float arcDegrees)
        {
            if (enemies == null) return false;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (IsWithinForwardArc(origin, forward, agents[i].position, arcDegrees)) return true;
            }
            return false;
        }

        public static float ResolveArcDegrees(WeaponDefinition def)
        {
            if (def == null) return 120f;
            if (def.targetArcDegrees > 0f) return def.targetArcDegrees;
            if (def.fanSpreadDegrees > 0f) return def.fanSpreadDegrees;
            return 120f;
        }

        static bool TryForwardArcHighestHull(
            EnemyWorld enemies,
            Vector3 origin,
            Vector3 forward,
            float range,
            WeaponDefinition def,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            var arc = def.targetArcDegrees > 0f ? def.targetArcDegrees : 120f;
            var bestHp = float.MinValue;
            var bestDist = float.MaxValue;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist > range) continue;
                if (!IsWithinForwardArc(origin, forward, agents[i].position, arc)) continue;
                if (agents[i].hp > bestHp || (Mathf.Approximately(agents[i].hp, bestHp) && dist < bestDist))
                {
                    bestHp = agents[i].hp;
                    bestDist = dist;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0) return false;
            aimDirection = (agents[targetIndex].position - origin).normalized;
            return true;
        }

        static bool TryForwardArc(
            EnemyWorld enemies,
            Vector3 origin,
            Vector3 forward,
            float range,
            WeaponDefinition def,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            var arc = def.targetArcDegrees > 0f ? def.targetArcDegrees : 120f;
            var bestScore = float.MinValue;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (!IsWithinForwardArc(origin, forward, agents[i].position, arc)) continue;

                var score = ScoreTarget(agents[i], def, delta.magnitude);
                if (score <= bestScore) continue;
                bestScore = score;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (agents[targetIndex].position - origin).normalized;
            return true;
        }

        static bool TryPortStarboard(
            EnemyWorld enemies,
            Vector3 origin,
            Vector3 forward,
            float range,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var leftScore = ScoreSide(enemies, origin, forward, right, range, sideRight: false, out var leftIndex);
            var rightScore = ScoreSide(enemies, origin, forward, right, range, sideRight: true, out var rightIndex);
            if (leftScore <= float.MinValue && rightScore <= float.MinValue)
                return false;

            var useRight = rightScore >= leftScore;
            targetIndex = useRight ? rightIndex : leftIndex;
            if (targetIndex < 0) return false;

            var agents = enemies.Agents;
            var toTarget = agents[targetIndex].position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return false;
            aimDirection = toTarget.normalized;
            return true;
        }

        /// <summary>True when a world point lies in the port or starboard firing arcs (not bow/stern).</summary>
        public static bool IsInBroadsideArc(Vector3 forward, Vector3 origin, Vector3 targetPos)
        {
            forward.y = 0f;
            var delta = targetPos - origin;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f) return false;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var dir = delta.normalized;
            var forwardDot = Vector3.Dot(dir, forward);
            if (Mathf.Abs(forwardDot) > BroadsideMaxForwardDot) return false;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var sideDot = Mathf.Abs(Vector3.Dot(dir, right));
            return sideDot >= BroadsideMinSideDot;
        }

        const float BroadsideMaxForwardDot = 0.5f;
        const float BroadsideMinSideDot = 0.55f;

        static float ScoreSide(
            EnemyWorld enemies,
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float range,
            bool sideRight,
            out int bestIndex)
        {
            bestIndex = -1;
            var bestScore = float.MinValue;
            var sideDir = sideRight ? right : -right;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                var distSq = delta.sqrMagnitude;
                if (distSq > range * range || distSq < 0.01f) continue;

                var dir = delta.normalized;
                var forwardDot = Vector3.Dot(dir, forward);
                if (Mathf.Abs(forwardDot) > BroadsideMaxForwardDot) continue;

                var sideDot = Vector3.Dot(dir, sideDir);
                if (sideDot < BroadsideMinSideDot) continue;

                var score = sideDot * 2f - Mathf.Abs(forwardDot) - distSq * 0.02f;
                if (agents[i].isElite) score += 0.5f;
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            return bestScore;
        }

        static bool TryDensestCluster(
            EnemyWorld enemies,
            Vector3 origin,
            float range,
            WeaponDefinition def,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestCount = -1;
            var bestDist = float.MaxValue;
            var clusterRadius = Mathf.Max(2.5f, def.size * 2f);
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;

                var count = 0;
                for (var j = 0; j < agents.Length; j++)
                {
                    if (!agents[j].active) continue;
                    if (Vector3.Distance(agents[i].position, agents[j].position) <= clusterRadius)
                        count++;
                }

                if (count < bestCount) continue;
                if (count == bestCount && delta.sqrMagnitude >= bestDist) continue;
                bestCount = count;
                bestDist = delta.sqrMagnitude;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (agents[targetIndex].position - origin).normalized;
            return true;
        }

        static bool TrySwivelDefense(
            EnemyWorld enemies,
            Vector3 origin,
            float range,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestScore = float.MinValue;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist > range) continue;
                var score = -dist;
                if (dist <= 6f) score += 100f;
                if (score <= bestScore) continue;
                bestScore = score;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (agents[targetIndex].position - origin).normalized;
            return true;
        }

        static bool TryOmniNearest(
            EnemyWorld enemies,
            Vector3 origin,
            float range,
            bool preferElite,
            bool preferHighestHull,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestScore = float.MinValue;
            var agents = enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                var score = ScoreTarget(agents[i], preferElite, preferHighestHull, delta.magnitude);
                if (score <= bestScore) continue;
                bestScore = score;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (agents[targetIndex].position - origin).normalized;
            return true;
        }

        static float ScoreTarget(EnemyAgent agent, WeaponDefinition def, float distance) =>
            ScoreTarget(agent, def.preferElite, def.preferHighestHull, distance);

        static float ScoreTarget(EnemyAgent agent, bool preferElite, bool preferHighestHull, float distance)
        {
            var score = -distance;
            if (preferElite && agent.isElite) score += 1000f;
            if (preferHighestHull) score += agent.hp;
            return score;
        }

        static void ApplyLeadAim(EnemyWorld enemies, Vector3 origin, WeaponDefinition def, int targetIndex, ref Vector3 aimDirection)
        {
            if (enemies == null || targetIndex < 0 || targetIndex >= enemies.Agents.Length) return;
            var agent = enemies.Agents[targetIndex];
            if (!agent.active) return;
            var speed = ResolveLeadSpeed(def);
            aimDirection = ComputeLeadDirection(origin, agent.position, agent.velocity, speed);
        }

        static float ResolveLeadSpeed(WeaponDefinition def) =>
            def != null && def.projectileSpeed > 0.1f ? def.projectileSpeed : 14f;

        public static Vector3 ComputeLeadDirection(
            Vector3 origin,
            Vector3 targetPos,
            Vector3 targetVelocity,
            float projectileSpeed)
        {
            targetVelocity.y = 0f;
            var toTarget = targetPos - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            if (projectileSpeed < 0.1f || targetVelocity.sqrMagnitude < 0.04f)
                return toTarget.normalized;

            var a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            var b = 2f * Vector3.Dot(targetVelocity, toTarget);
            var c = toTarget.sqrMagnitude;
            float timeToImpact;
            if (Mathf.Abs(a) < 0.0001f)
            {
                timeToImpact = c / Mathf.Max(0.001f, -b);
            }
            else
            {
                var discriminant = b * b - 4f * a * c;
                if (discriminant < 0f)
                    return toTarget.normalized;
                var sqrt = Mathf.Sqrt(discriminant);
                var t1 = (-b - sqrt) / (2f * a);
                var t2 = (-b + sqrt) / (2f * a);
                timeToImpact = t1 > 0.02f ? t1 : (t2 > 0.02f ? t2 : toTarget.magnitude / projectileSpeed);
            }

            timeToImpact = Mathf.Clamp(timeToImpact, 0f, 2.5f);
            var predicted = targetPos + targetVelocity * timeToImpact;
            var aim = predicted - origin;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : toTarget.normalized;
        }

        public static Vector3 PredictClusterGround(EnemyWorld enemies, PlayerShipController player, float range)
        {
            if (enemies == null || player == null) return player != null ? player.Position : Vector3.zero;
            if (TryDensestCluster(enemies, player.Position, range, preferHighestHull: false, out var index, out _))
                return enemies.Agents[index].position;

            var velocity = player.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.5f)
                return player.Position + velocity.normalized * Mathf.Min(range * 0.65f, 8f);
            return player.Position + player.MovementForward * 6f;
        }

        static bool TryDensestCluster(
            EnemyWorld enemies,
            Vector3 origin,
            float range,
            bool preferHighestHull,
            out int targetIndex,
            out Vector3 aimDirection)
        {
            var probe = ScriptableObject.CreateInstance<WeaponDefinition>();
            probe.range = range;
            probe.size = 3f;
            probe.preferHighestHull = preferHighestHull;
            return TryDensestCluster(enemies, origin, range, probe, out targetIndex, out aimDirection);
        }
    }
}
