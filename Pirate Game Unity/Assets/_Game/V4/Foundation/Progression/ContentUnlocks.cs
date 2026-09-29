using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Progression
{
    /// <summary>Local prototype unlock gates shared by menu, level-up offers, and loadout.</summary>
    public static class ContentUnlocks
    {
        public const string FirstConquestKey = "v3_first_conquest_complete";

        static bool BypassProgressionGates => PlaytestFlags.BypassProgressionGates;

        public static bool HasCompletedFirstConquest =>
            BypassProgressionGates || PlayerPrefs.GetInt(FirstConquestKey, 0) == 1;

        public static bool IsWeaponUnlocked(string weaponId)
        {
            if (!WeaponFeatureFlags.IsWeaponEnabled(weaponId))
                return false;
            if (BypassProgressionGates) return !string.IsNullOrEmpty(weaponId);
            if (string.IsNullOrEmpty(weaponId)) return false;

            if (StageBalanceProfileContext.IsActive)
            {
                var export = StageBalanceProfileContext.Export;
                if (SaltwindStageProgressionLogic.IsWeaponInOfferPool(export, weaponId))
                    return true;
                return AppRoot.Instance?.Save?.HasEffectiveContentOwned(weaponId) ?? false;
            }

            // M16: V3's hard-coded unlock table (outside a stage-profile run) is gone; V4 asks only inside runs, where the
            // stage profile is always active. Outside one, only the save's ownership counts.
            return AppRoot.Instance?.Save?.HasEffectiveContentOwned(weaponId) ?? false;
        }

    }
}
