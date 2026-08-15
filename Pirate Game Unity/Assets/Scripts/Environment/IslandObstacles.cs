using UnityEngine;

namespace PirateGame.Environment
{
    /// <summary>
    /// Shared layer mask and capsule-based movement resolution against island geometry.
    /// </summary>
    public static class IslandObstacles
    {
        public const string LayerName = "Island";
        const float Skin = 0.02f;

        static int? cachedLayer;

        public static int Layer
        {
            get
            {
                if (!cachedLayer.HasValue)
                    cachedLayer = LayerMask.NameToLayer(LayerName);
                return cachedLayer.Value;
            }
        }

        public static int Mask => Layer >= 0 ? 1 << Layer : 0;

        public static bool IsAvailable => Layer >= 0;

        public static Vector3 ResolveCapsuleMovement(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity)
        {
            var mask = Mask;
            if (mask == 0 || delta.sqrMagnitude < 1e-8f)
                return position + delta;

            var result = position;
            var remaining = delta;

            for (var iteration = 0; iteration < 2; iteration++)
            {
                var distance = remaining.magnitude;
                if (distance < 1e-6f)
                    break;

                var direction = remaining / distance;
                GetCapsulePoints(result, radius, height, out var pointA, out var pointB);

                if (!Physics.CapsuleCast(
                        pointA,
                        pointB,
                        radius,
                        direction,
                        out var hit,
                        distance + Skin,
                        mask,
                        QueryTriggerInteraction.Ignore))
                {
                    result += remaining;
                    break;
                }

                var moveDistance = Mathf.Max(0f, hit.distance - Skin);
                result += direction * moveDistance;

                var inwardSpeed = Vector3.Dot(velocity, hit.normal);
                if (inwardSpeed < 0f)
                    velocity -= hit.normal * inwardSpeed;

                remaining = Vector3.ProjectOnPlane(remaining - direction * hit.distance, hit.normal);
            }

            return result;
        }

        public static Vector3 SteerAroundObstacles(
            Vector3 origin,
            Vector3 desiredDirection,
            float probeRadius,
            float lookAhead,
            float steerWeight)
        {
            var mask = Mask;
            if (mask == 0 || desiredDirection.sqrMagnitude < 1e-8f)
                return desiredDirection.normalized;

            desiredDirection = desiredDirection.normalized;
            var steer = Vector3.zero;
            var probeOrigin = origin + Vector3.up * 0.5f;

            AccumulateAvoidance(probeOrigin, desiredDirection, probeRadius, lookAhead, mask, ref steer);
            AccumulateAvoidance(probeOrigin, Quaternion.Euler(0f, 35f, 0f) * desiredDirection, probeRadius, lookAhead * 0.85f, mask, ref steer);
            AccumulateAvoidance(probeOrigin, Quaternion.Euler(0f, -35f, 0f) * desiredDirection, probeRadius, lookAhead * 0.85f, mask, ref steer);

            if (steer.sqrMagnitude < 1e-6f)
                return desiredDirection;

            return (desiredDirection + steer * steerWeight).normalized;
        }

        static void AccumulateAvoidance(
            Vector3 origin,
            Vector3 direction,
            float probeRadius,
            float lookAhead,
            int mask,
            ref Vector3 steer)
        {
            if (direction.sqrMagnitude < 1e-8f || lookAhead <= 0f)
                return;

            direction = direction.normalized;
            if (!Physics.SphereCast(origin, probeRadius, direction, out var hit, lookAhead, mask, QueryTriggerInteraction.Ignore))
                return;

            var avoid = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (avoid.sqrMagnitude < 1e-6f)
                return;

            avoid.Normalize();
            var weight = 1f - Mathf.Clamp01(hit.distance / lookAhead);
            steer += avoid * weight;
        }

        static void GetCapsulePoints(Vector3 position, float radius, float height, out Vector3 pointA, out Vector3 pointB)
        {
            var halfHeight = Mathf.Max(radius, height * 0.5f - radius);
            pointA = position + Vector3.up * halfHeight;
            pointB = position - Vector3.up * halfHeight;
        }
    }
}
