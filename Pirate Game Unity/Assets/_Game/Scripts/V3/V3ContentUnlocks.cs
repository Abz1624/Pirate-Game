using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Local prototype unlock gates shared by menu, level-up offers, and loadout.</summary>
    public static class V3ContentUnlocks
    {
        public const string FirstConquestKey = "v3_first_conquest_complete";

        static bool BypassProgressionGates =>
            V3PlaytestFlags.UnlockAllContent
            && (!StageBalanceProfileContext.IsActive || V3PlaytestFlags.SaltwindDebugUnlockAll);

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
                return AppRoot.Instance?.Save?.HasMarksOwned(weaponId) ?? false;
            }

            if (CombatExperimentContext.Active.weaponSlotCap >= 6 && IsExperimentalPlaytestWeapon(weaponId))
                return HasCompletedFirstConquest || weaponId is "long_nine" or "swivel_guns";

            return weaponId switch
            {
                "deck_cannons" => true,
                "broadside_battery" => true,
                "powder_kegs" => HasCompletedFirstConquest || V3CaptainSelection.Selected == V3CaptainId.Flint,
                "flying_cutlasses" => HasCompletedFirstConquest,
                "lightning_mast" => HasCompletedFirstConquest,
                "flamethrower" => HasCompletedFirstConquest,
                "burning_oil" => HasCompletedFirstConquest,
                _ => false
            };
        }

        static bool IsExperimentalPlaytestWeapon(string weaponId) =>
            weaponId is "long_nine" or "swivel_guns" or "harpoon_gun" or "mortar";

        public static bool IsLogbookUnlocked(string logbookId) =>
            BypassProgressionGates || !string.IsNullOrEmpty(logbookId);

        public static bool IsItemUnlocked(string itemId)
        {
            if (BypassProgressionGates) return !string.IsNullOrEmpty(itemId);
            if (string.IsNullOrEmpty(itemId)) return false;
            if (StageBalanceProfileContext.IsActive)
                return SaltwindStageProgressionLogic.IsRunItemOfferEligible(
                    StageBalanceProfileContext.Export,
                    itemId);
            return true;
        }
    }
}
