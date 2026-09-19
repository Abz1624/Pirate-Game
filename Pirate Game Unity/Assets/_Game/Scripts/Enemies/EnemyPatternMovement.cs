using UnityEngine;

namespace HighSeas.Enemies
{
    public static class EnemyPatternMovement
    {
        public static Vector3 ComputeVelocity(
            in EnemyAgent agent,
            in EnemySpawnRuntimeParams p,
            Vector3 chaseDir,
            Vector3 dir,
            float dist,
            float dt,
            ref float chargeTimer)
        {
            if (p.ambushHidden)
                return Vector3.zero;

            var speed = agent.moveSpeed * p.adaptiveSpeedMult;
            var movement = p.hasAdaptiveMovementOverride ? p.adaptiveMovementOverride : p.movementPattern;

            switch (movement)
            {
                case EnemyMovementPattern.DirectChase:
                case EnemyMovementPattern.DiveLunge:
                    return chaseDir * speed;
                case EnemyMovementPattern.Intercept:
                    return chaseDir * speed;
                case EnemyMovementPattern.Flank:
                {
                    var flankSign = (agent.id & 1) == 0 ? 1f : -1f;
                    var flank = Vector3.Cross(Vector3.up, dir) * flankSign;
                    return (dir + flank * Mathf.Tan(p.flankAngleRad)).normalized * speed;
                }
                case EnemyMovementPattern.Orbit:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir) * p.orbitDirection;
                    return (tangent + dir * 0.15f).normalized * speed;
                }
                case EnemyMovementPattern.MaintainRange:
                {
                    var preferred = p.preferredRange > 0f ? p.preferredRange : 10f;
                    if (dist > preferred + 3f)
                        return dir * (speed * 0.65f);
                    if (dist < preferred - 2f)
                    {
                        var strafeSign = (agent.id & 1) == 0 ? 1f : -1f;
                        var tangent = Vector3.Cross(Vector3.up, dir) * strafeSign;
                        return tangent * (speed * 0.35f);
                    }

                    return Vector3.zero;
                }
                case EnemyMovementPattern.Strafe:
                {
                    var tangent = Vector3.Cross(Vector3.up, dir);
                    return (tangent + dir * 0.25f).normalized * speed;
                }
                case EnemyMovementPattern.TelegraphedCharge:
                    chargeTimer -= dt;
                    if (chargeTimer <= 0f)
                        return dir * (speed * p.chargeSpeedMult);
                    if (chargeTimer > p.chargeWindUp)
                        return dir * (speed * 0.2f);
                    return Vector3.zero;
                case EnemyMovementPattern.Swarm:
                    return chaseDir * (speed * 1.2f);
                case EnemyMovementPattern.Drift:
                    return chaseDir * speed;
                case EnemyMovementPattern.Stationary:
                    return Vector3.zero;
                default:
                    return chaseDir * speed;
            }
        }
    }
}
