using System;
using System.Collections.Generic;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Lab-side progression approximations aligned with Saltwind export fields (R9).</summary>
    public static class SaltwindLabProgressionParityLogic
    {
        public static int WeaponSlotCapAtLevel(StageBalanceProfileExport export, int level)
        {
            if (export?.Progression == null) return 1;
            var cap = export.Progression.WeaponSlotCap;
            if (cap <= 0) cap = 6;
            return Math.Min(cap, 1 + Math.Max(0, level / 2));
        }

        public static bool ShouldOfferNewWeaponOnLevel(int level) => level >= 2 && level % 2 == 0;

        public static IReadOnlyList<string> ResolveAccessibleWeaponIds(
            StageBalanceProfileExport export,
            SaltwindLabUnlockPool pool)
        {
            var ids = new List<string>();
            if (export?.StarterWeaponIds != null)
                ids.AddRange(export.StarterWeaponIds);

            if (pool == SaltwindLabUnlockPool.MarksUnlocked && export?.WeaponUnlocks != null)
            {
                for (var i = 0; i < export.WeaponUnlocks.Count; i++)
                {
                    var id = export.WeaponUnlocks[i]?.ContentId;
                    if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                        ids.Add(id);
                }
            }

            if (ids.Count == 0) ids.Add("deck_cannons");
            return ids;
        }

        public static bool TryPickPolicyWeapon(
            StageBalanceProfileExport export,
            SaltwindLabBuildPolicy policy,
            SaltwindLabUnlockPool pool,
            IReadOnlyList<string> ownedWeaponIds,
            out string weaponId)
        {
            weaponId = null;
            var queue = SaltwindBalanceLabScenarioFactory.ResolveWeaponQueue(policy, pool, export);
            var accessible = ResolveAccessibleWeaponIds(export, pool);
            for (var i = 0; i < queue.Count; i++)
            {
                var candidate = queue[i];
                if (!Contains(accessible, candidate)) continue;
                if (Contains(ownedWeaponIds, candidate)) continue;
                weaponId = candidate;
                return true;
            }

            return false;
        }

        static bool Contains(IReadOnlyList<string> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id) return true;
            }

            return false;
        }
    }
}
