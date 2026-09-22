using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    /// <summary>Captures referenced weapon stats for reproducible profile content hashing (R9).</summary>
    public static class WeaponStatExportLogic
    {
        public static List<StageWeaponStatSnapshot> SnapshotReferencedWeapons(
            StageBalanceProfile profile,
            GameDatabase database = null)
        {
            var list = new List<StageWeaponStatSnapshot>();
            if (profile == null) return list;

            database ??= GameDatabaseLoader.Load();
            if (!GameDatabaseLoader.IsValid(database)) return list;

            var ids = new HashSet<string>();
            CollectIds(profile.starterWeaponIds, ids);
            if (profile.weaponUnlocks != null)
            {
                for (var i = 0; i < profile.weaponUnlocks.Length; i++)
                {
                    var id = profile.weaponUnlocks[i]?.contentId;
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }

            if (ids.Count == 0) ids.Add("deck_cannons");

            var sorted = new List<string>(ids);
            sorted.Sort(string.CompareOrdinal);
            for (var i = 0; i < sorted.Count; i++)
                TryAddSnapshot(list, database, sorted[i]);

            return list;
        }

        static void CollectIds(string[] source, HashSet<string> ids)
        {
            if (source == null) return;
            for (var i = 0; i < source.Length; i++)
            {
                if (!string.IsNullOrEmpty(source[i])) ids.Add(source[i]);
            }
        }

        static void TryAddSnapshot(List<StageWeaponStatSnapshot> list, GameDatabase database, string weaponId)
        {
            var def = database.GetWeapon(weaponId);
            if (def == null) return;

            var damageAxis = 0.08f;
            if (def.TryGetAxisConfig(UpgradeAxis.Damage, out var axis))
                damageAxis = axis.baseIncrement;

            list.Add(new StageWeaponStatSnapshot
            {
                WeaponId = def.id,
                BaseDamage = def.baseDamage,
                BaseCooldown = def.baseCooldown,
                Range = def.range,
                ProjectileSpeed = def.projectileSpeed,
                MaxLevel = def.maxLevel,
                DamageUpgradePerLevel = damageAxis
            });
        }
    }
}
