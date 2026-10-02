using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>
    /// Movement resolution against island mesh colliders on the Island layer (convex ground + non-convex foliage).
    /// </summary>
    public static class IslandObstacles
    {
        public const string LayerName = "Island";
        const float Skin = 0.05f;
        const int MaxResolveIterations = 5;
        const int MaxDepenetrateIterations = 8;

        static int? _cachedLayer;
        static readonly Collider[] OverlapBuffer = new Collider[16];
        static readonly Collider[] OverlapScratch = new Collider[16];

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

        public static bool IsPositionBlocked(Vector3 position, float radius, float height = 0f)
        {
            if (Mask == 0)
                return false;

            if (height <= 0f)
                height = GetCollisionHeight(radius);

            GetCapsulePoints(position, radius, height, out var pointA, out var pointB);
            var castRadius = GetCastRadius(radius);
            if (Physics.CheckCapsule(pointA, pointB, castRadius, Mask, QueryTriggerInteraction.Ignore))
                return true;

            return Physics.CheckSphere(
                GetSphereProbeCenter(position, height),
                castRadius,
                Mask,
                QueryTriggerInteraction.Ignore);
        }

        public static Vector3 ApplyAvoidanceSteering(
            Vector3 position,
            Vector3 velocity,
            Vector3 preferredDirection,
            float agentRadius,
            float probeDistance = 5f,
            int steerBias = 0)
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

            if (!TryMovementCast(pointA, pointB, castRadius, direction, probeDistance, Mask, out var hit))
                return velocity;

            var left = Quaternion.Euler(0f, -40f, 0f) * direction;
            var right = Quaternion.Euler(0f, 40f, 0f) * direction;
            var leftClear = !TryMovementCast(pointA, pointB, castRadius, left, probeDistance * 0.9f, Mask, out _);
            var rightClear = !TryMovementCast(pointA, pointB, castRadius, right, probeDistance * 0.9f, Mask, out _);

            var normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f)
                return velocity;
            normal.Normalize();

            var tangent = Vector3.Cross(Vector3.up, normal).normalized;
            var tangentAlt = -tangent;

            var preferred = preferredDirection;
            preferred.y = 0f;
            if (preferred.sqrMagnitude > 0.01f)
            {
                preferred.Normalize();
                var dotA = Vector3.Dot(tangent, preferred);
                var dotB = Vector3.Dot(tangentAlt, preferred);
                var useAlt = dotB > dotA;
                if (Mathf.Abs(dotA - dotB) < 0.12f)
                    useAlt = (steerBias & 1) != 0;
                tangent = useAlt ? tangentAlt : tangent;

                if (leftClear && !rightClear)
                    tangent = Vector3.Dot(tangent, Vector3.Cross(Vector3.up, preferred)) < 0f ? tangentAlt : tangent;
                else if (rightClear && !leftClear)
                    tangent = Vector3.Dot(tangent, Vector3.Cross(Vector3.up, preferred)) > 0f ? tangentAlt : tangent;
            }
            else if (Vector3.Dot(tangent, direction) < 0f)
            {
                tangent = tangentAlt;
            }

            if (TryMovementCast(pointA, pointB, castRadius, tangent, probeDistance * 0.75f, Mask, out _)
                && !TryMovementCast(pointA, pointB, castRadius, tangentAlt, probeDistance * 0.75f, Mask, out _))
            {
                tangent = tangentAlt;
            }

            var steerStrength = 1f - Mathf.Clamp01(hit.distance / probeDistance);
            var steered = Vector3.Slerp(direction, tangent, steerStrength * 0.75f).normalized * speed;
            steered.y = 0f;
            return steered;
        }

        /// <summary>V5: the hardest island hit of one move: the shore's flat outward normal and the speed taken out along
        /// it (zero when the move touched nothing).</summary>
        public struct ShoreHit
        {
            public Vector3 Normal;
            public float InwardSpeed;
        }

        public static Vector3 ResolveCapsuleMovement(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity,
            Vector3 preferredDirection = default) =>
            ResolveCapsuleMovement(position, delta, radius, height, ref velocity, out _, preferredDirection);

        /// <summary>As above, also reporting the hardest shore hit of the move (V5 island impact).</summary>
        public static Vector3 ResolveCapsuleMovement(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity,
            out ShoreHit shoreHit,
            Vector3 preferredDirection = default)
        {
            shoreHit = default;
            var mask = Mask;
            if (mask == 0 || delta.sqrMagnitude < 1e-8f)
                return position + delta;

            var maxStep = Mathf.Max(radius * 0.3f, 0.15f);
            var stepCount = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / maxStep));
            var stepDelta = delta / stepCount;
            var result = position;

            for (var step = 0; step < stepCount; step++)
                result = ResolveCapsuleStep(result, stepDelta, radius, height, ref velocity, mask, ref shoreHit);

            if (IsOverlapping(result, radius, height))
                result = Depenetrate(result, radius, height);

            velocity = PreservePreferredDirection(velocity, preferredDirection, velocity.magnitude);
            return result;
        }

        static bool IsOverlapping(Vector3 position, float radius, float height)
        {
            if (Mask == 0) return false;
            return CollectBlockingColliders(position, radius, height, OverlapBuffer) > 0;
        }

        static Vector3 ResolveCapsuleStep(
            Vector3 position,
            Vector3 delta,
            float radius,
            float height,
            ref Vector3 velocity,
            int mask,
            ref ShoreHit shoreHit)
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

                if (!TryMovementCast(pointA, pointB, castRadius, direction, distance + Skin, mask, out var hit))
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
                    if (-inwardSpeed > shoreHit.InwardSpeed)
                    {
                        var n = hit.normal;
                        n.y = 0f;
                        if (n.sqrMagnitude > 1e-6f)
                        {
                            shoreHit.InwardSpeed = -inwardSpeed;
                            shoreHit.Normal = n.normalized;
                        }
                    }
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
                var overlapCount = CollectBlockingColliders(result, radius, height, OverlapBuffer);
                if (overlapCount == 0)
                    break;

                var push = Vector3.zero;
                for (var i = 0; i < overlapCount; i++)
                {
                    var collider = OverlapBuffer[i];
                    if (collider == null) continue;
                    push += GetDepenetrationVector(collider, result, radius);
                }

                if (push.sqrMagnitude < 1e-8f)
                    break;

                result += push;
                result.y = position.y;
            }

            return result;
        }

        static Vector3 GetDepenetrationVector(Collider collider, Vector3 position, float radius)
        {
            // Playtest (3 Oct): ClosestPoint only works on primitives and convex meshes; the islands' foliage colliders are
            // concave (by design), so those use their bounds' nearest point (the console error and a wrong push otherwise).
            var concave = collider is MeshCollider mesh && !mesh.convex;
            var closest = concave ? collider.bounds.ClosestPoint(position) : collider.ClosestPoint(position);
            var away = position - closest;
            away.y = 0f;
            var dist = away.magnitude;
            if (dist < 0.001f)
            {
                away = position - collider.bounds.center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f)
                    away = Vector3.forward;
            }

            var penetration = radius - dist;
            return penetration > 0f ? away.normalized * (penetration + Skin) : Vector3.zero;
        }

        static bool TryMovementCast(
            Vector3 pointA,
            Vector3 pointB,
            float castRadius,
            Vector3 direction,
            float distance,
            int mask,
            out RaycastHit hit)
        {
            if (Physics.CapsuleCast(
                    pointA,
                    pointB,
                    castRadius,
                    direction,
                    out hit,
                    distance,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            var sphereCenter = (pointA + pointB) * 0.5f;
            return Physics.SphereCast(
                sphereCenter,
                castRadius,
                direction,
                out hit,
                distance,
                mask,
                QueryTriggerInteraction.Ignore);
        }

        static int CollectBlockingColliders(Vector3 position, float radius, float height, Collider[] buffer)
        {
            GetCapsulePoints(position, radius, height, out var pointA, out var pointB);
            var castRadius = GetCastRadius(radius);
            var count = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                castRadius,
                buffer,
                Mask,
                QueryTriggerInteraction.Ignore);

            var sphereCount = Physics.OverlapSphereNonAlloc(
                GetSphereProbeCenter(position, height),
                castRadius,
                OverlapScratch,
                Mask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < sphereCount; i++)
            {
                if (count >= buffer.Length)
                    break;
                if (ContainsCollider(buffer, count, OverlapScratch[i]))
                    continue;
                buffer[count++] = OverlapScratch[i];
            }

            return count;
        }

        static bool ContainsCollider(Collider[] buffer, int count, Collider candidate)
        {
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] == candidate)
                    return true;
            }

            return false;
        }

        static Vector3 GetSphereProbeCenter(Vector3 position, float height) =>
            position + Vector3.up * Mathf.Max(0.75f, height * 0.25f);

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
            if (forwardAmount >= -0.2f)
                return velocity;

            var slide = Vector3.ProjectOnPlane(flatVelocity, preferred);
            if (slide.sqrMagnitude < 0.01f)
            {
                slide = Vector3.Cross(Vector3.up, preferred);
                if (Vector3.Dot(slide, flatVelocity) < 0f)
                    slide = -slide;
            }

            slide = slide.normalized * Mathf.Max(speed * 0.85f, flatVelocity.magnitude);
            slide.y = 0f;
            return slide;
        }

        static float GetCastRadius(float radius) => Mathf.Max(0.15f, radius * 0.92f);

        public static float GetCollisionHeight(float radius) => Mathf.Max(radius * 2.5f, 1.25f);

        static void GetCapsulePoints(Vector3 position, float radius, float height, out Vector3 pointA, out Vector3 pointB)
        {
            var halfHeight = Mathf.Max(radius, height * 0.5f - radius);
            pointA = position + Vector3.up * halfHeight;
            pointB = position - Vector3.up * halfHeight;
        }
    }
}
