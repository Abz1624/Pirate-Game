using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    /// <summary>
    /// V5 M9b close (determinism): the ship as spawn placement reads it: its simulated position, yaw and movement forward.
    /// The run passes <see cref="FromShip"/> (the physics body's pose), not the ship's transform (its drawn pose, which
    /// Rigidbody interpolation moves between physics steps by the frame's render phase, so two plays of one seed differed).
    /// </summary>
    public readonly struct SpawnAnchor
    {
        public readonly bool Valid;
        public readonly Vector3 position;
        public readonly float yawDegrees;
        readonly Vector3 _forward;

        public SpawnAnchor(Vector3 position, float yawDegrees, Vector3 forward)
        {
            Valid = true;
            this.position = position;
            this.yawDegrees = yawDegrees;
            forward.y = 0f;
            _forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        }

        /// <summary>The flat forward placement uses (the ship's movement forward).</summary>
        public Vector3 Forward => Valid ? _forward : Vector3.forward;

        /// <summary>The ship's simulated pose (its body), not the interpolated transform.</summary>
        public static SpawnAnchor FromShip(PlayerShipController ship) =>
            ship == null ? default : new SpawnAnchor(ship.SimPosition, ship.SimRotation.eulerAngles.y, ship.MovementForward);

        // V5 M9b close 2 (review): the implicit Transform conversion (the drawn, interpolated pose) is gone, so no caller
        // can pass the drawn pose by accident; build an anchor from the ship (FromShip) or from explicit values.
    }

    public static class SpawnPlacementService
    {
        public static bool TryPickOffScreenPosition(
            SpawnAnchor player,
            Camera camera,
            SpawnPlacementSettings settings,
            float spawnRadius,
            RunRng rng,
            out Vector3 position)
        {
            position = default;
            if (!player.Valid)
                return false;

            var minDist = settings.distanceMin;
            if (settings.requireOutsidePlayerFov && camera != null)
                minDist = Mathf.Max(minDist, GetMinSpawnDistanceBeyondView(camera, player, settings.viewportMargin) + 1.5f);
            var maxDist = Mathf.Max(minDist + 0.5f, settings.distanceMax);
            var attempts = Mathf.Max(1, settings.positionAttempts);
            var margin = settings.viewportMargin;

            for (var i = 0; i < attempts; i++)
            {
                var candidate = SampleCandidate(player, camera, settings, minDist, maxDist, rng);
                if (!IsSpawnCandidateValid(candidate, spawnRadius))
                    continue;
                if (settings.requireOutsidePlayerFov && camera != null && IsWorldPositionVisible(candidate, camera, margin))
                    continue;
                position = candidate;
                return true;
            }

            if (settings.fallbackPolicy == SpawnFallbackPolicy.Skip)
                return false;

            for (var i = 0; i < attempts; i++)
            {
                var candidate = PickAtScreenEdge(player, camera, settings, minDist, maxDist, rng);
                if (!IsSpawnCandidateValid(candidate, spawnRadius))
                    continue;
                if (settings.requireOutsidePlayerFov && camera != null && IsWorldPositionVisible(candidate, camera, margin))
                    continue;
                position = candidate;
                return true;
            }

            return false;
        }

        /// <summary>Off-screen first; optional ring around the player when encounters must not whiff (boss/cache).</summary>
        public static bool TryPickSpawnPosition(
            SpawnAnchor player,
            Camera camera,
            SpawnPlacementSettings settings,
            float spawnRadius,
            RunRng rng,
            bool allowNearPlayerFallback,
            out Vector3 position)
        {
            if (TryPickOffScreenPosition(player, camera, settings, spawnRadius, rng, out position))
                return true;

            if (!allowNearPlayerFallback || !player.Valid)
                return false;

            var minDist = settings.distanceMin;
            if (settings.requireOutsidePlayerFov && camera != null)
                minDist = Mathf.Max(minDist, GetMinSpawnDistanceBeyondView(camera, player, settings.viewportMargin) + 1.5f);
            var maxDist = Mathf.Max(minDist + 0.5f, settings.distanceMax);
            var attempts = Mathf.Max(1, settings.positionAttempts);
            for (var i = 0; i < attempts; i++)
            {
                var candidate = PickInArc(player, settings, minDist, maxDist, rng);
                if (!IsSpawnCandidateValid(candidate, spawnRadius))
                    continue;
                position = candidate;
                return true;
            }

            return false;
        }

        public static void OffsetGroupPosition(
            SpawnGroupLayout layout,
            int index,
            int count,
            float spacing,
            SpawnAnchor player,
            ref Vector3 basePosition)
        {
            if (count <= 1)
                return;

            var forward = GetPlayerForward(player);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            switch (layout)
            {
                case SpawnGroupLayout.Ring:
                {
                    var angle = index / (float)count * Mathf.PI * 2f;
                    basePosition += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spacing;
                    break;
                }
                case SpawnGroupLayout.Line:
                    basePosition += right * ((index - (count - 1) * 0.5f) * spacing);
                    break;
                case SpawnGroupLayout.Arc:
                {
                    var t = count > 1 ? index / (float)(count - 1) : 0.5f;
                    var angle = Mathf.Lerp(-50f, 50f, t) * Mathf.Deg2Rad;
                    var dir = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * forward;
                    basePosition += dir * spacing;
                    break;
                }
                case SpawnGroupLayout.FlankPair:
                    basePosition += right * (index == 0 ? -spacing : spacing);
                    break;
                case SpawnGroupLayout.Cluster:
                case SpawnGroupLayout.Scatter:
                default:
                    var jitter = new Vector3(
                        (index * 0.37f % 1f - 0.5f) * spacing,
                        0f,
                        (index * 0.61f % 1f - 0.5f) * spacing);
                    basePosition += jitter;
                    break;
            }
        }

        static Vector3 SampleCandidate(SpawnAnchor player, Camera camera, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
        {
            if (settings.requireOutsidePlayerFov && camera != null && rng.DirectorFloat() < 0.65f)
                return PickAtScreenEdge(player, camera, settings, minDist, maxDist, rng);
            return PickInArc(player, settings, minDist, maxDist, rng);
        }

        static Vector3 PickInArc(SpawnAnchor player, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
        {
            var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            var angle = rng.DirectorFloat() * Mathf.PI * 2f;
            var arc = settings.allowedArcDegrees;
            if (arc < 359f)
            {
                var half = arc * 0.5f * Mathf.Deg2Rad;
                var center = (player.yawDegrees + settings.arcYawOffset) * Mathf.Deg2Rad;
                angle = center + (rng.DirectorFloat() * 2f - 1f) * half;
            }

            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        static Vector3 PickAtScreenEdge(SpawnAnchor player, Camera camera, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
        {
            var planeY = player.position.y;
            var margin = settings.viewportMargin;
            for (var i = 0; i < 8; i++)
            {
                var edge = rng.DirectorFloat();
                var u = rng.DirectorFloat();
                float vx;
                float vy;
                if (edge < 0.25f)
                {
                    vx = u;
                    vy = -margin;
                }
                else if (edge < 0.5f)
                {
                    vx = 1f + margin;
                    vy = u;
                }
                else if (edge < 0.75f)
                {
                    vx = u;
                    vy = 1f + margin;
                }
                else
                {
                    vx = -margin;
                    vy = u;
                }

                var ray = camera.ViewportPointToRay(new Vector3(vx, vy, 0f));
                if (!TryIntersectWaterPlane(ray, planeY, out var hit))
                    continue;
                return EnforceSpawnDistance(player, hit, minDist, maxDist, planeY);
            }

            return PickInArc(player, settings, minDist, maxDist, rng);
        }

        static Vector3 EnforceSpawnDistance(SpawnAnchor player, Vector3 point, float minDist, float maxDist, float planeY)
        {
            var offset = point - player.position;
            offset.y = 0f;
            var dist = offset.magnitude;
            if (dist < 0.01f)
                return player.position + Vector3.forward * minDist;
            if (dist < minDist)
            {
                var result = player.position + offset.normalized * minDist;
                result.y = planeY;
                return result;
            }

            if (dist > maxDist)
            {
                var result = player.position + offset.normalized * maxDist;
                result.y = planeY;
                return result;
            }

            point.y = planeY;
            return point;
        }

        static bool TryIntersectWaterPlane(Ray ray, float planeY, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return false;
            var t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
                return false;
            hitPoint = ray.origin + ray.direction * t;
            hitPoint.y = planeY;
            return true;
        }

        public static bool IsFormationPositionValid(
            Vector3 position,
            float spawnRadius,
            Camera camera,
            SpawnPlacementSettings settings)
        {
            if (!IsSpawnCandidateValid(position, spawnRadius))
                return false;
            if (settings.requireOutsidePlayerFov && camera != null &&
                IsWorldPositionVisible(position, camera, settings.viewportMargin))
                return false;
            return true;
        }

        static bool IsSpawnCandidateValid(Vector3 point, float radius)
        {
            if (!PlayBounds.IsInside(point, radius))
                return false;
            if (!IslandObstacles.IsAvailable)
                return true;
            return !IslandObstacles.IsPositionBlocked(point, radius);
        }

        public static bool IsWorldPositionVisible(Vector3 worldPosition, Camera camera, float viewportMargin)
        {
            if (camera == null)
                return false;
            var viewport = camera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f)
                return false;
            return viewport.x >= -viewportMargin
                && viewport.x <= 1f + viewportMargin
                && viewport.y >= -viewportMargin
                && viewport.y <= 1f + viewportMargin;
        }

        public static float GetMinSpawnDistanceBeyondView(Camera camera, SpawnAnchor player, float viewportMargin)
        {
            if (camera == null || !player.Valid) return 10f;
            var planeY = player.position.y;
            var maxDist = 0f;
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    if (!TryIntersectWaterPlane(ray, planeY, out var hit)) continue;
                    var d = Vector3.Distance(new Vector3(player.position.x, planeY, player.position.z),
                        new Vector3(hit.x, planeY, hit.z));
                    if (d > maxDist) maxDist = d;
                }
            }

            return maxDist * (1f + viewportMargin);
        }

        static Vector3 GetPlayerForward(SpawnAnchor player) => player.Forward;
    }
}
