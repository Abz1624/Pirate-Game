using UnityEngine;

namespace HighSeas.Combat
{
    public static class WeaponBeamTargeting
    {
        public static bool IsInsideBeam(
            Vector3 origin,
            Vector3 forward,
            float range,
            float halfWidth,
            Vector3 targetPosition,
            float targetRadius)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();

            var delta = targetPosition - origin;
            delta.y = 0f;
            var along = Vector3.Dot(delta, forward);
            if (along < 0f || along > range) return false;

            var lateral = delta - forward * along;
            var limit = halfWidth + Mathf.Max(0f, targetRadius);
            return lateral.sqrMagnitude <= limit * limit;
        }
    }
}
