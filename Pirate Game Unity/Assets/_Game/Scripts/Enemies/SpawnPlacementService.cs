using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Enemies
{
    public static class SpawnPlacementService
    {
        public static bool TryPickOffScreenPosition(
            Transform player,
            Camera camera,
            SpawnPlacementSettings settings,
            float spawnRadius,
            RunRng rng,
            out Vector3 position)
        {
            position = default;
            if (player == null)
                return false;

            var minDist = settings.distanceMin;
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

        public static void OffsetGroupPosition(
            SpawnGroupLayout layout,
            int index,
            int count,
            float spacing,
            Transform player,
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

        static Vector3 SampleCandidate(Transform player, Camera camera, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
        {
            if (settings.requireOutsidePlayerFov && camera != null && rng.DirectorFloat() < 0.65f)
                return PickAtScreenEdge(player, camera, settings, minDist, maxDist, rng);
            return PickInArc(player, settings, minDist, maxDist, rng);
        }

        static Vector3 PickInArc(Transform player, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
        {
            var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            var angle = rng.DirectorFloat() * Mathf.PI * 2f;
            var arc = settings.allowedArcDegrees;
            if (arc < 359f)
            {
                var half = arc * 0.5f * Mathf.Deg2Rad;
                var center = (player.eulerAngles.y + settings.arcYawOffset) * Mathf.Deg2Rad;
                angle = center + (rng.DirectorFloat() * 2f - 1f) * half;
            }

            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        static Vector3 PickAtScreenEdge(Transform player, Camera camera, SpawnPlacementSettings settings, float minDist, float maxDist, RunRng rng)
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

        static Vector3 EnforceSpawnDistance(Transform player, Vector3 point, float minDist, float maxDist, float planeY)
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

        static Vector3 GetPlayerForward(Transform player)
        {
            if (player == null)
                return Vector3.forward;
            var ship = player.GetComponent<PlayerShipController>();
            if (ship != null)
            {
                var movement = ship.MovementForward;
                movement.y = 0f;
                if (movement.sqrMagnitude > 0.01f)
                    return movement.normalized;
            }

            var forward = player.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        }
    }
}
