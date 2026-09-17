using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Multi-ship bots stay inside storm-safe playable water with the human fleet.</summary>
    public static class V3MultiShipBotBounds
    {
        public static float BotPlayMargin(float hullRadius) =>
            hullRadius + PlayBounds.StormEdgeMargin;

        public static float MaxDistanceFromPlayer(Vector3 playerPosition, float hullRadius = 1.15f)
        {
            if (!V3RunSession.IsMultiShip)
                return float.MaxValue;

            var margin = BotPlayMargin(hullRadius);
            var local = playerPosition - PlayBounds.Center;
            var reachX = PlayBounds.HalfExtentX - margin - Mathf.Abs(local.x);
            var reachZ = PlayBounds.HalfExtentZ - margin - Mathf.Abs(local.z);
            reachX = Mathf.Max(0f, reachX);
            reachZ = Mathf.Max(0f, reachZ);
            var fromPlayer = Mathf.Sqrt(reachX * reachX + reachZ * reachZ);

            var mapDiagonal = Mathf.Sqrt(
                PlayBounds.HalfExtentX * PlayBounds.HalfExtentX +
                PlayBounds.HalfExtentZ * PlayBounds.HalfExtentZ);

            return Mathf.Max(fromPlayer, mapDiagonal * 0.98f);
        }

        public static float HardDistanceFromPlayer(Vector3 playerPosition, float hullRadius = 1.15f) =>
            MaxDistanceFromPlayer(playerPosition, hullRadius) + 4f;

        public static Vector3 ClampNearPlayer(Vector3 worldPosition, Vector3 playerPosition, float hullRadius = 1.15f)
        {
            if (!V3RunSession.IsMultiShip)
                return worldPosition;

            worldPosition = ClampShipPosition(worldPosition, hullRadius);

            var delta = worldPosition - playerPosition;
            delta.y = 0f;
            var max = MaxDistanceFromPlayer(playerPosition, hullRadius);
            if (delta.sqrMagnitude <= max * max)
                return worldPosition;

            if (delta.sqrMagnitude < 0.0001f)
                return playerPosition;

            return playerPosition + delta.normalized * max;
        }

        public static bool IsBeyondHardLeash(Vector3 shipPosition, Vector3 playerPosition, float hullRadius = 1.15f)
        {
            if (!V3RunSession.IsMultiShip)
                return false;

            if (IsOutsidePlayableArea(shipPosition, hullRadius))
                return true;

            var delta = shipPosition - playerPosition;
            delta.y = 0f;
            var hard = HardDistanceFromPlayer(playerPosition, hullRadius);
            return delta.sqrMagnitude > hard * hard;
        }

        public static bool IsOutsidePlayableArea(Vector3 shipPosition, float hullRadius = 1.15f) =>
            !PlayBounds.IsInsideStormSafeZone(shipPosition) ||
            !PlayBounds.IsInside(shipPosition, BotPlayMargin(hullRadius));

        public static Vector3 PullOntoLeash(Vector3 shipPosition, Vector3 playerPosition, float hullRadius) =>
            ClampShipPosition(shipPosition, hullRadius);

        public static Vector3 ClampShipPosition(Vector3 position, float hullRadius) =>
            PlayBounds.Clamp(position, BotPlayMargin(hullRadius));

        public static float DistanceToStormEdge(Vector3 position)
        {
            var local = position - PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - PlayBounds.StormEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PlayBounds.StormEdgeMargin;
            var overX = Mathf.Max(0f, Mathf.Abs(local.x) - limitX);
            var overZ = Mathf.Max(0f, Mathf.Abs(local.z) - limitZ);
            return Mathf.Sqrt(overX * overX + overZ * overZ);
        }

        /// <summary>Meters inside the storm-safe rectangle (negative if outside).</summary>
        public static float ClearanceFromStormEdge(Vector3 position)
        {
            var local = position - PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - PlayBounds.StormEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PlayBounds.StormEdgeMargin;
            var clearX = limitX - Mathf.Abs(local.x);
            var clearZ = limitZ - Mathf.Abs(local.z);
            return Mathf.Min(clearX, clearZ);
        }
    }
}
