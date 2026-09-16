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
            const int barrels = 5;
            const int looseClusters = 10;
            const int crystalsPerCluster = 3;

            for (var b = 0; b < barrels; b++)
            {
                var angle = (b / (float)barrels) * Mathf.PI * 2f + rng.LootFloat() * 0.35f;
                var radius = 5f + rng.LootFloat() * 4f;
                var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var value = 10 + rng.DirectorInt(0, 6);
                PickupWorld.Instance.Spawn(PickupType.XpBarrel, value, pos);
            }

            for (var c = 0; c < looseClusters; c++)
            {
                var angle = rng.LootFloat() * Mathf.PI * 2f;
                var radius = 3f + rng.LootFloat() * 7f;
                var clusterCenter = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                for (var k = 0; k < crystalsPerCluster; k++)
                {
                    var offsetAngle = rng.LootFloat() * Mathf.PI * 2f;
                    var offsetRadius = 0.15f + rng.LootFloat() * 0.45f;
                    var offset = new Vector3(Mathf.Cos(offsetAngle) * offsetRadius, 0f, Mathf.Sin(offsetAngle) * offsetRadius);
                    PickupWorld.Instance.Spawn(PickupType.Infamy, 1, clusterCenter + offset);
                }
            }
        }
    }
}
