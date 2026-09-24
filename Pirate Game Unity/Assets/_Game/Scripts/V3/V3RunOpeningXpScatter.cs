using HighSeas.Core;
using HighSeas.Pickups;
using UnityEngine;

namespace HighSeas.V3
{
    public static class V3RunOpeningXpScatter
    {
        public static void ScatterNearPlayer(Vector3 center, int runSeed)
        {
            if (PickupWorld.Instance == null) return;

            var rng = new RunRng(runSeed ^ 0x58F0);
            const int crystals = 12;
            for (var c = 0; c < crystals; c++)
            {
                var angle = rng.LootFloat() * Mathf.PI * 2f;
                var radius = 4f + rng.LootFloat() * 6f;
                var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var value = c < 4 ? 4 + rng.DirectorInt(0, 3) : 2 + rng.DirectorInt(0, 2);
                PickupWorld.Instance.Spawn(PickupType.Infamy, value, pos);
            }

            PickupWorld.Instance.AttractAllInfamy();
        }
    }
}
