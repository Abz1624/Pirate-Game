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

            switch (ResolveMode(def))
            {
                case TargetingMode.ForwardArc:
                case TargetingMode.TetherTarget:
                    return TryForwardArc(enemies, origin, forward, range, def, out targetIndex, out aimDirection);
                case TargetingMode.ForwardCone:
                    return TryForwardArc(enemies, origin, forward, range, def, out targetIndex, out aimDirection);
                case TargetingMode.PortStarboard:
                    return TryPortStarboard(enemies, origin, forward, range, out targetIndex, out aimDirection);
                case TargetingMode.DensestCluster:
                case TargetingMode.OmniClusterGround:
                    return TryDensestCluster(enemies, origin, range, def, out targetIndex, out aimDirection);
                case TargetingMode.NearestElite:
                    return TryOmniNearest(enemies, origin, range, preferElite: true, preferHighestHull: def.preferHighestHull,
                        out targetIndex, out aimDirection);
                default:
                    return TryOmniNearest(enemies, origin, range, preferElite: def.preferElite,
                        preferHighestHull: def.preferHighestHull, out targetIndex, out aimDirection);
            }
        }

        static TargetingMode ResolveMode(WeaponDefinition def)
        {
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
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var leftScore = ScoreSide(enemies, origin, right, range, sideRight: false, out var leftIndex);
            var rightScore = ScoreSide(enemies, origin, right, range, sideRight: true, out var rightIndex);
            if (leftScore <= float.MinValue && rightScore <= float.MinValue) return false;

            if (rightScore >= leftScore)
            {
                targetIndex = rightIndex;
                aimDirection = right;
            }
            else
            {
                targetIndex = leftIndex;
                aimDirection = -right;
            }
            return targetIndex >= 0;
        }

        static float ScoreSide(EnemyWorld enemies, Vector3 origin, Vector3 right, float range, bool sideRight, out int bestIndex)
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
                if (delta.sqrMagnitude > range * range) continue;
                if (Vector3.Dot(delta.normalized, sideDir) < 0.35f) continue;
                var score = -delta.sqrMagnitude;
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
            if (agent.isBoss) score += 500f;
            return score;
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
            return TryDensestCluster(
                enemies,
                origin,
                range,
                new WeaponDefinition { range = range, size = 3f, preferHighestHull = preferHighestHull },
                out targetIndex,
                out aimDirection);
        }
    }
}
