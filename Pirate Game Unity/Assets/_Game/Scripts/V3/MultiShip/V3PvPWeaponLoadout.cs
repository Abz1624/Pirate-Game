using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class V3PvPWeaponLoadout
    {
        public const int WeaponCount = 6;
        public const string DuelWeaponId = "broadside_battery";
        public const int DuelWeaponLevel = 3;

        public static bool IsExcludedFromPvP(string weaponId) =>
            weaponId == "mortar";

        public static string[] GetDuelLoadout(GameDatabase database)
        {
            if (database == null || database.GetWeapon(DuelWeaponId) == null)
                return System.Array.Empty<string>();
            return new[] { DuelWeaponId };
        }

        public static string[] RollRandomWeapons(GameDatabase database, RunRng rng)
        {
            if (database == null || rng == null)
                return System.Array.Empty<string>();

            var pool = new List<string>();
            foreach (var id in V3WeaponPlaytestCatalog.All)
            {
                if (IsExcludedFromPvP(id)) continue;
                if (!string.IsNullOrEmpty(id) && database.GetWeapon(id) != null)
                    pool.Add(id);
            }

            var count = Mathf.Min(WeaponCount, pool.Count);
            var result = new string[count];
            for (var i = 0; i < count; i++)
            {
                var pick = rng.LootInt(0, pool.Count);
                result[i] = pool[pick];
                pool.RemoveAt(pick);
            }

            return result;
        }

        public static string[] GetAllPlaytestWeapons(GameDatabase database)
        {
            if (database == null)
                return System.Array.Empty<string>();

            var result = new List<string>();
            foreach (var id in V3WeaponPlaytestCatalog.All)
            {
                if (IsExcludedFromPvP(id)) continue;
                if (!string.IsNullOrEmpty(id) && database.GetWeapon(id) != null)
                    result.Add(id);
            }

            return result.ToArray();
        }
    }
}
