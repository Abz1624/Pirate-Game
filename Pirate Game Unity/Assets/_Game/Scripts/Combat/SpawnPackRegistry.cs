using System.Collections.Generic;

namespace HighSeas.Combat
{
    public readonly struct SpawnPackEntry
    {
        public readonly string EnemyId;
        public readonly int Count;

        public SpawnPackEntry(string enemyId, int count)
        {
            EnemyId = enemyId;
            Count = count;
        }
    }

    /// <summary>Named Director V2 packs (doc §7.6).</summary>
    public static class SpawnPackRegistry
    {
        public static IReadOnlyList<SpawnPackEntry> GetPack(float elapsedMinutes)
        {
            if (elapsedMinutes >= 8f)
                return LateMix;
            if (elapsedMinutes >= 6f)
                return ZonePressure;
            if (elapsedMinutes >= 4f)
                return AnchorAndFire;
            if (elapsedMinutes >= 2f)
                return Gunline;
            return Swarm;
        }

        static readonly SpawnPackEntry[] Swarm =
        {
            new("shark", 12)
        };

        static readonly SpawnPackEntry[] Gunline =
        {
            new("shark", 4),
            new("cannonboat", 1),
            new("manta_ray", 1),
            new("needle_sloop", 1)
        };

        static readonly SpawnPackEntry[] AnchorAndFire =
        {
            new("reef_crab_miniboss", 1),
            new("cannonboat", 1),
            new("shark", 4),
            new("net_cutter", 1)
        };

        static readonly SpawnPackEntry[] ZonePressure =
        {
            new("kraken_spawn", 1),
            new("manta_ray", 2),
            new("shark", 4),
            new("bomb_ketch", 1)
        };

        static readonly SpawnPackEntry[] LateMix =
        {
            new("hammerhead", 1),
            new("kraken_spawn", 1),
            new("cannonboat", 1),
            new("shark", 4),
            new("repair_tender", 1),
            new("powder_barge", 1)
        };
    }
}
