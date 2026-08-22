using UnityEngine;

namespace HighSeas.World
{
    /// <summary>
    /// Capsule-based movement resolution against island geometry on the Island layer.
    /// </summary>
    public static class IslandObstacles
    {
        public const string LayerName = "Island";
        const float Skin = 0.02f;

        static int? _cachedLayer;

        public static int Layer
        {
            get
            {
                if (!_cachedLayer.HasValue)
                    _cachedLayer = LayerMask.NameToLayer(LayerName);
                return _cachedLayer.Value;
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

        static void GetCapsulePoints(Vector3 position, float radius, float height, out Vector3 pointA, out Vector3 pointB)
        {
            var halfHeight = Mathf.Max(radius, height * 0.5f - radius);
            pointA = position + Vector3.up * halfHeight;
            pointB = position - Vector3.up * halfHeight;
        }
    }
}
