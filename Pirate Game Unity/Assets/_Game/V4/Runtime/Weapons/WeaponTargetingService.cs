using RoguePirates.V4.Config;
using UnityEngine;
using Mode = RoguePirates.V4.Config.ConfigEnums.TargetingMode;

namespace RoguePirates.V4.Weapons
{
    /// <summary>
    /// Target selection, port of V3 WeaponTargeting with the same scoring, tie-breaks and iteration order (traces compare
    /// V4 with V3 shot for shot). Reads the weapon config instead of WeaponDefinition; the Swivel Guns id check is the
    /// projectile block's closeDefenseTargeting flag. Allocates nothing (V3 created a WeaponDefinition per cluster probe).
    /// </summary>
    public sealed class WeaponTargetingService
    {
        const float BroadsideMaxForwardDot = 0.5f;
        const float BroadsideMinSideDot = 0.55f;
        const float ClusterProbeSize = 3f;

        readonly IEnemyQuery _enemies;
        readonly IShipState _ship;

        /// <summary>ExperimentConfig.directionalWeaponTargetingEnabled (read per call, it is live-editable).</summary>
        public System.Func<bool> DirectionalTargeting = () => true;

        public WeaponTargetingService(IEnemyQuery enemies, IShipState ship)
        {
            _enemies = enemies;
            _ship = ship;
        }

        public bool TryResolveTarget(WeaponConfig def, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = _ship != null && _ship.Exists ? _ship.MovementForward : Vector3.forward;
            if (_enemies == null || !_enemies.Exists || _ship == null || !_ship.Exists || def == null) return false;

            var origin = _ship.Position;
            var forward = _ship.MovementForward;
            var range = def.range;

            bool resolved;
            switch (ResolveMode(def))
            {
                case Mode.ForwardArc:
                case Mode.TetherTarget:
                    resolved = def.preferHighestHull
                        ? TryForwardArcHighestHull(origin, forward, range, def, out targetIndex, out aimDirection)
                        : TryForwardArc(origin, forward, range, def, out targetIndex, out aimDirection);
                    break;
                case Mode.ForwardCone:
                    resolved = TryForwardArc(origin, forward, range, def, out targetIndex, out aimDirection);
                    break;
                case Mode.PortStarboard:
                    resolved = TryPortStarboard(origin, forward, range, out targetIndex, out aimDirection);
                    break;
                case Mode.DensestCluster:
                case Mode.OmniClusterGround:
                    resolved = TryDensestCluster(origin, range, def.size, out targetIndex, out aimDirection);
                    break;
                case Mode.NearestElite:
                    resolved = TryOmniNearest(origin, range, true, def.preferHighestHull, out targetIndex, out aimDirection);
                    break;
                default:
                    resolved = def.projectile.closeDefenseTargeting
                        ? TryCloseDefense(origin, range, out targetIndex, out aimDirection)
                        : TryOmniNearest(origin, range, def.preferElite, def.preferHighestHull, out targetIndex, out aimDirection);
                    break;
            }

            if (!resolved || targetIndex < 0) return false;
            ApplyLeadAim(origin, def, targetIndex, ref aimDirection);
            return true;
        }

        Mode ResolveMode(WeaponConfig def)
        {
            if (def.targeting == Mode.PortStarboard) return Mode.PortStarboard;
            if (!DirectionalTargeting())
            {
                if (def.targeting == Mode.DensestCluster) return Mode.DensestCluster;
                if (def.targeting == Mode.NearestElite) return Mode.NearestElite;
                return Mode.Nearest;
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

        public bool HasEnemyInForwardArc(Vector3 origin, Vector3 forward, float range, float arcDegrees)
        {
            if (_enemies == null || !_enemies.Exists) return false;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var position = _enemies.Position(i);
                var delta = position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (IsWithinForwardArc(origin, forward, position, arcDegrees)) return true;
            }
            return false;
        }

        public static float ResolveArcDegrees(WeaponConfig def)
        {
            if (def == null) return 120f;
            if (def.targetArcDegrees > 0f) return def.targetArcDegrees;
            if (def.fanSpreadDegrees > 0f) return def.fanSpreadDegrees;
            return 120f;
        }

        bool TryForwardArcHighestHull(Vector3 origin, Vector3 forward, float range, WeaponConfig def, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            var arc = def.targetArcDegrees > 0f ? def.targetArcDegrees : 120f;
            var bestHp = float.MinValue;
            var bestDist = float.MaxValue;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var position = _enemies.Position(i);
                var delta = position - origin;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist > range) continue;
                if (!IsWithinForwardArc(origin, forward, position, arc)) continue;
                var hp = _enemies.Hp(i);
                if (hp > bestHp || (Mathf.Approximately(hp, bestHp) && dist < bestDist))
                {
                    bestHp = hp;
                    bestDist = dist;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0) return false;
            aimDirection = (_enemies.Position(targetIndex) - origin).normalized;
            return true;
        }

        bool TryForwardArc(Vector3 origin, Vector3 forward, float range, WeaponConfig def, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            var arc = def.targetArcDegrees > 0f ? def.targetArcDegrees : 120f;
            var bestScore = float.MinValue;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var position = _enemies.Position(i);
                var delta = position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (!IsWithinForwardArc(origin, forward, position, arc)) continue;

                var score = ScoreTarget(i, def.preferElite, def.preferHighestHull, delta.magnitude);
                if (score <= bestScore) continue;
                bestScore = score;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (_enemies.Position(targetIndex) - origin).normalized;
            return true;
        }

        bool TryPortStarboard(Vector3 origin, Vector3 forward, float range, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var leftScore = ScoreSide(origin, forward, right, range, false, out var leftIndex);
            var rightScore = ScoreSide(origin, forward, right, range, true, out var rightIndex);
            if (leftScore <= float.MinValue && rightScore <= float.MinValue)
                return false;

            var useRight = rightScore >= leftScore;
            targetIndex = useRight ? rightIndex : leftIndex;
            if (targetIndex < 0) return false;

            var toTarget = _enemies.Position(targetIndex) - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return false;
            aimDirection = toTarget.normalized;
            return true;
        }

        float ScoreSide(Vector3 origin, Vector3 forward, Vector3 right, float range, bool sideRight, out int bestIndex)
        {
            bestIndex = -1;
            var bestScore = float.MinValue;
            var sideDir = sideRight ? right : -right;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var delta = _enemies.Position(i) - origin;
                delta.y = 0f;
                var distSq = delta.sqrMagnitude;
                if (distSq > range * range || distSq < 0.01f) continue;

                var dir = delta.normalized;
                var forwardDot = Vector3.Dot(dir, forward);
                if (Mathf.Abs(forwardDot) > BroadsideMaxForwardDot) continue;

                var sideDot = Vector3.Dot(dir, sideDir);
                if (sideDot < BroadsideMinSideDot) continue;

                var score = sideDot * 2f - Mathf.Abs(forwardDot) - distSq * 0.02f;
                if (_enemies.IsElite(i)) score += 0.5f;
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            return bestScore;
        }

        bool TryDensestCluster(Vector3 origin, float range, float size, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestCount = -1;
            var bestDist = float.MaxValue;
            var clusterRadius = Mathf.Max(2.5f, size * 2f);
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var position = _enemies.Position(i);
                var delta = position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;

                var count = 0;
                for (var j = 0; j < _enemies.Capacity; j++)
                {
                    if (!_enemies.IsActive(j)) continue;
                    if (Vector3.Distance(position, _enemies.Position(j)) <= clusterRadius)
                        count++;
                }

                if (count < bestCount) continue;
                if (count == bestCount && delta.sqrMagnitude >= bestDist) continue;
                bestCount = count;
                bestDist = delta.sqrMagnitude;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (_enemies.Position(targetIndex) - origin).normalized;
            return true;
        }

        bool TryCloseDefense(Vector3 origin, float range, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestScore = float.MinValue;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var delta = _enemies.Position(i) - origin;
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
            aimDirection = (_enemies.Position(targetIndex) - origin).normalized;
            return true;
        }

        bool TryOmniNearest(Vector3 origin, float range, bool preferElite, bool preferHighestHull, out int targetIndex, out Vector3 aimDirection)
        {
            targetIndex = -1;
            aimDirection = Vector3.forward;
            var bestScore = float.MinValue;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.IsActive(i)) continue;
                var delta = _enemies.Position(i) - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                var score = ScoreTarget(i, preferElite, preferHighestHull, delta.magnitude);
                if (score <= bestScore) continue;
                bestScore = score;
                targetIndex = i;
            }

            if (targetIndex < 0) return false;
            aimDirection = (_enemies.Position(targetIndex) - origin).normalized;
            return true;
        }

        float ScoreTarget(int index, bool preferElite, bool preferHighestHull, float distance)
        {
            var score = -distance;
            if (preferElite && _enemies.IsElite(index)) score += 1000f;
            if (preferHighestHull) score += _enemies.Hp(index);
            return score;
        }

        void ApplyLeadAim(Vector3 origin, WeaponConfig def, int targetIndex, ref Vector3 aimDirection)
        {
            if (targetIndex < 0 || targetIndex >= _enemies.Capacity || !_enemies.IsActive(targetIndex)) return;
            var speed = def.projectileSpeed > 0.1f ? def.projectileSpeed : 14f;
            aimDirection = ComputeLeadDirection(origin, _enemies.Position(targetIndex), _enemies.Velocity(targetIndex), speed);
        }

        public static Vector3 ComputeLeadDirection(Vector3 origin, Vector3 targetPos, Vector3 targetVelocity, float projectileSpeed)
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

        /// <summary>The densest enemy cluster in range (a 3 m probe), else a point ahead of the ship.</summary>
        public Vector3 PredictClusterGround(float range)
        {
            if (_enemies == null || !_enemies.Exists || _ship == null || !_ship.Exists) return _ship != null && _ship.Exists ? _ship.Position : Vector3.zero;
            if (TryDensestCluster(_ship.Position, range, ClusterProbeSize, out var index, out _))
                return _enemies.Position(index);

            var velocity = _ship.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.5f)
                return _ship.Position + velocity.normalized * Mathf.Min(range * 0.65f, 8f);
            return _ship.Position + _ship.MovementForward * 6f;
        }
    }
}
