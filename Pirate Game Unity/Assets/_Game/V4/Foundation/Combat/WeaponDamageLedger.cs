using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;

namespace RoguePirates.V4.Foundation.Combat
{
    public sealed class WeaponContributionStats
    {
        public float totalDamage;
        public int kills;
        public float bossDamage;
        public float eliteDamage;
        public int hits;
        public int critHits;
        public int controlApplications;
        public float controlSeconds;
        public int procEventsGenerated;
        public int maxConcurrentTargetsHit;
        public int emissions;
    }

    public static class WeaponDamageLedger
    {
        static readonly Dictionary<string, WeaponContributionStats> _stats = new();

        public static void Reset()
        {
            _stats.Clear();
        }

        public static void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            var stats = GetOrCreate(weaponId);
            stats.totalDamage += damage;
            stats.hits++;
            if (isCrit) stats.critHits++;
            if (isElite) stats.eliteDamage += damage;
            if (isBoss) stats.bossDamage += damage;
        }

        public static void RecordKill(string weaponId) => GetOrCreate(weaponId).kills++;
        public static void RecordEmission(string weaponId) => GetOrCreate(weaponId).emissions++;
        public static void RecordControl(string weaponId, float seconds = 0f)
        {
            var stats = GetOrCreate(weaponId);
            stats.controlApplications++;
            stats.controlSeconds += seconds;
        }

        public static void RecordConcurrentTargets(string weaponId, int count)
        {
            var stats = GetOrCreate(weaponId);
            if (count > stats.maxConcurrentTargetsHit)
                stats.maxConcurrentTargetsHit = count;
        }

        public static IReadOnlyDictionary<string, WeaponContributionStats> Snapshot() =>
            new Dictionary<string, WeaponContributionStats>(_stats);

        static WeaponContributionStats GetOrCreate(string weaponId)
        {
            if (!_stats.TryGetValue(weaponId, out var stats))
            {
                stats = new WeaponContributionStats();
                _stats[weaponId] = stats;
            }
            return stats;
        }
    }
}
