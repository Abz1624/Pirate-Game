using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Co-op / PvP bots stay in a tight radius around the human player ship.</summary>
    public static class V3MultiShipBotBounds
    {
        public const float MaxDistanceFromPlayer = 17f;
        public const float HardDistanceFromPlayer = 19f;

        public static Vector3 ClampNearPlayer(Vector3 worldPosition, Vector3 playerPosition)
        {
            if (!V3RunSession.IsMultiShip)
                return worldPosition;

            var delta = worldPosition - playerPosition;
            delta.y = 0f;
            var distSq = delta.sqrMagnitude;
            var max = MaxDistanceFromPlayer;
            if (distSq <= max * max)
                return worldPosition;

            if (distSq < 0.0001f)
                return playerPosition;

            return playerPosition + delta.normalized * max;
        }

        public static bool IsBeyondHardLeash(Vector3 shipPosition, Vector3 playerPosition)
        {
            if (!V3RunSession.IsMultiShip)
                return false;

            var delta = shipPosition - playerPosition;
            delta.y = 0f;
            return delta.sqrMagnitude > HardDistanceFromPlayer * HardDistanceFromPlayer;
        }

        public static Vector3 PullOntoLeash(Vector3 shipPosition, Vector3 playerPosition, float hullRadius)
        {
            if (!V3RunSession.IsMultiShip)
                return shipPosition;

            var delta = shipPosition - playerPosition;
            delta.y = 0f;
            var dist = delta.magnitude;
            if (dist <= HardDistanceFromPlayer - 0.5f)
                return PlayBounds.Clamp(shipPosition, hullRadius + 1.5f);

            var pulled = dist < 0.01f
                ? playerPosition
                : playerPosition + delta / dist * (HardDistanceFromPlayer - 1f);
            return PlayBounds.Clamp(pulled, hullRadius + 1.5f);
        }

        public static Vector3 ClampShipPosition(Vector3 position, float hullRadius) =>
            PlayBounds.Clamp(position, hullRadius + 1.5f);
    }
}
