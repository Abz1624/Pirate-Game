using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class V3MultiShipSpawnUtil
    {
        public static Vector3 ResolveSafeSpawn(Vector3 desired, float hullRadius = 1.15f)
        {
            desired.y = 0f;
            if (!IslandObstacles.IsAvailable)
                return V3MultiShipBotBounds.ClampShipPosition(desired, hullRadius);

            var height = IslandObstacles.GetCollisionHeight(hullRadius);
            if (!IslandObstacles.IsPositionBlocked(desired, hullRadius))
                return V3MultiShipBotBounds.ClampShipPosition(desired, hullRadius);

            for (var ring = 1; ring <= 14; ring++)
            {
                for (var i = 0; i < 10; i++)
                {
                    var angle = i / 10f * Mathf.PI * 2f;
                    var candidate = desired + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring * 3f;
                    if (!IslandObstacles.IsPositionBlocked(candidate, hullRadius))
                        return V3MultiShipBotBounds.ClampShipPosition(candidate, hullRadius);
                }
            }

            desired = IslandObstacles.Depenetrate(desired, hullRadius, height);
            return V3MultiShipBotBounds.ClampShipPosition(desired, hullRadius);
        }
    }
}
