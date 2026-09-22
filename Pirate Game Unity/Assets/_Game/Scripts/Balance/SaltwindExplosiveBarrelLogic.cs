using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.Balance
{
    public static class SaltwindExplosiveBarrelLogic
    {
        public static bool CanArm(in bool consumed) => !consumed;

        public static bool ShouldDetonate(float fuseRemaining) => fuseRemaining <= 0f;

        public static HashSet<int> ApplyBlastDamage(
            IReadOnlyList<Vector3> actorPositions,
            Vector3 center,
            float radius,
            HashSet<int> alreadyDamagedActorIds)
        {
            var hit = new HashSet<int>();
            var radiusSq = radius * radius;
            for (var i = 0; i < actorPositions.Count; i++)
            {
                if (alreadyDamagedActorIds.Contains(i)) continue;
                var delta = actorPositions[i] - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSq) continue;
                hit.Add(i);
            }

            return hit;
        }
    }
}
