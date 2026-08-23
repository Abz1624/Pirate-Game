using UnityEngine;

namespace HighSeas.World
{
    /// <summary>
    /// Capsule-based movement resolution against island colliders on the Island layer (ground mesh or fallback capsule).
    /// </summary>
    public static class IslandObstacles
    {
        public const string LayerName = "Island";
        const float Skin = 0.05f;
        const int MaxResolveIterations = 5;
        const int MaxDepenetrateIterations = 6;

        static int? _cachedLayer;
        static readonly Collider[] OverlapBuffer = new Collider[12];

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

        public static Vector3 ApplyAvoidanceSteering(
            Vector3 position,
            Vector3 velocity,
            Vector3 preferredDirection,
            float agentRadius,
            float probeDistance = 5f)
        {
            if (Mask == 0)
                return velocity;

            var flatVelocity = velocity;
            flatVelocity.y = 0f;
            if (flatVelocity.sqrMagnitude < 0.01f)
                return velocity;

            var direction = flatVelocity.normalized;
            var speed = flatVelocity.magnitude;
            var height = GetCollisionHeight(agentRadius);
            var castRadius = GetCastRadius(agentRadius);
            GetCapsulePoints(position, agentRadius, height, out var pointA, out var pointB);

            if (!Physics.CapsuleCast(
                    pointA,
                    pointB,
                    castRadius,
                    direction,
                    out var hit,
                    probeDistance,
                    Mask,
                    QueryTriggerInteraction.Ignore))
            {
                return velocity;
            }

            var tangent = Vector3.Cross(Vector3.up, hit.normal);
            if (tangent.sqrMagnitude < 0.0001f)
                return velocity;

            tangent.Normalize();
            var tangentAlt = -tangent;
            var preferred = preferredDirection;
            preferred.y = 0f;
            if (preferred.sqrMagnitude > 0.01f)
            {
                preferred.Normalize();
                tangent = Vector3.Dot(tangent, preferred) >= Vector3.Dot(tangentAlt, preferred)
                    ? tangent
                    : tangentAlt;
            }
            else if (Vector3.Dot(tangent, direction) < 0f)
            {
                tangent = tangentAlt;
            }

            var steerStrength = 1f - Mathf.Clamp01(hit.distance / probeDistance);
            var steered = Vector3.Lerp(direction, tangent, steerStrength * 0.85f).normalized * speed;
            steered.y = 0f;
            return PreservePreferredDirection(steered, preferredDirection, speed);
        }

        public static Vector3 ResolveCapsuleMovement(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity,
            Vector3 preferredDirection = default)
        {
            var mask = Mask;
            if (mask == 0 || delta.sqrMagnitude < 1e-8f)
                return position + delta;

            var maxStep = Mathf.Max(radius * 0.35f, 0.2f);
            var stepCount = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / maxStep));
            var stepDelta = delta / stepCount;
            var result = position;

            for (var step = 0; step < stepCount; step++)
                result = ResolveCapsuleStep(result, stepDelta, radius, height, ref velocity, mask);

            result = Depenetrate(result, radius, height);
            velocity = PreservePreferredDirection(velocity, preferredDirection, velocity.magnitude);
            return result;
        }

        static Vector3 ResolveCapsuleStep(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity,
            int mask)
        {
            var result = position;
            var remaining = delta;
            var castRadius = GetCastRadius(radius);

            for (var iteration = 0; iteration < MaxResolveIterations; iteration++)
            {
                var distance = remaining.magnitude;
                if (distance < 1e-6f)
                    break;

                var direction = remaining / distance;
                GetCapsulePoints(result, radius, height, out var pointA, out var pointB);

                if (!Physics.CapsuleCast(
                        pointA,
                        pointB,
                        castRadius,
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

                var slide = Vector3.ProjectOnPlane(remaining - direction * Mathf.Max(hit.distance, 0f), hit.normal);
                slide.y = 0f;
                remaining = slide;

                var flatVelocity = velocity;
                flatVelocity.y = 0f;
                var inwardSpeed = Vector3.Dot(flatVelocity, hit.normal);
                if (inwardSpeed < 0f)
                {
                    velocity -= hit.normal * inwardSpeed;
                    velocity.y = 0f;
                }
            }

            return result;
        }

        public static Vector3 Depenetrate(Vector3 position, float radius, float height)
        {
            if (Mask == 0)
                return position;

            var result = position;
            for (var attempt = 0; attempt < MaxDepenetrateIterations; attempt++)
            {
                GetCapsulePoints(result, radius, height, out var pointA, out var pointB);
                var overlapCount = Physics.OverlapCapsuleNonAlloc(
                    pointA,
                    pointB,
                    GetCastRadius(radius),
                    OverlapBuffer,
                    Mask,
                    QueryTriggerInteraction.Ignore);

                if (overlapCount == 0)
                    break;

                var push = Vector3.zero;
                for (var i = 0; i < overlapCount; i++)
                {
                    var collider = OverlapBuffer[i];
                    if (collider == null) continue;

                    var closest = collider.ClosestPoint(result);
                    var away = result - closest;
                    away.y = 0f;
                    var dist = away.magnitude;
                    if (dist < 0.001f)
                    {
                        away = result - collider.bounds.center;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.001f)
                            away = Vector3.forward;
                    }

                    var penetration = radius - dist;
                    if (penetration > 0f)
                        push += away.normalized * (penetration + Skin);
                }

                if (push.sqrMagnitude < 1e-8f)
                    break;

                result += push;
                result.y = position.y;
            }

            return result;
        }

        static Vector3 PreservePreferredDirection(Vector3 velocity, Vector3 preferredDirection, float speed)
        {
            var preferred = preferredDirection;
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 0.01f)
                return velocity;

            preferred.Normalize();
            var flatVelocity = velocity;
            flatVelocity.y = 0f;
            var forwardAmount = Vector3.Dot(flatVelocity, preferred);
            if (forwardAmount >= 0f)
                return velocity;

            var slide = Vector3.ProjectOnPlane(flatVelocity, preferred);
            if (slide.sqrMagnitude < 0.01f)
            {
                slide = Vector3.Cross(Vector3.up, preferred);
                if (Vector3.Dot(slide, flatVelocity) < 0f)
                    slide = -slide;
            }

            slide = slide.normalized * Mathf.Max(speed * 0.35f, flatVelocity.magnitude);
            slide.y = 0f;
            return slide;
        }

        static float GetCastRadius(float radius) => Mathf.Max(0.15f, radius * 0.92f);

        static float GetCollisionHeight(float radius) => Mathf.Max(radius * 2.5f, 1.25f);

        static void GetCapsulePoints(Vector3 position, float radius, float height, out Vector3 pointA, out Vector3 pointB)
        {
            var halfHeight = Mathf.Max(radius, height * 0.5f - radius);
            pointA = position + Vector3.up * halfHeight;
            pointB = position - Vector3.up * halfHeight;
        }
    }
}
