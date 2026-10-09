using HighSeas.Combat;

using UnityEngine;



namespace HighSeas.V3

{

    /// <summary>Local prototype unlock gates shared by menu, level-up offers, and loadout.</summary>

    public static class V3ContentUnlocks

    {

        public const string FirstConquestKey = "v3_first_conquest_complete";



        public static bool HasCompletedFirstConquest =>

            V3PlaytestFlags.UnlockAllContent || PlayerPrefs.GetInt(FirstConquestKey, 0) == 1;



        public static bool IsWeaponUnlocked(string weaponId)

        {

            if (!WeaponFeatureFlags.IsWeaponEnabled(weaponId))

                return false;

            if (V3PlaytestFlags.UnlockAllContent) return !string.IsNullOrEmpty(weaponId);

            if (string.IsNullOrEmpty(weaponId)) return false;

            if (CombatExperimentContext.Active.weaponSlotCap >= 6 && IsExperimentalPlaytestWeapon(weaponId))

                return HasCompletedFirstConquest || weaponId is "long_nine" or "swivel_guns" or "tidal_aura" or "monkey_buccaneer";

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

            weaponId is "long_nine" or "swivel_guns" or "harpoon_gun" or "mortar" or "tidal_aura" or "monkey_buccaneer";



        public static bool IsLogbookUnlocked(string logbookId) =>

            V3PlaytestFlags.UnlockAllContent || !string.IsNullOrEmpty(logbookId);



        public static bool IsItemUnlocked(string itemId) =>

            V3PlaytestFlags.UnlockAllContent || !string.IsNullOrEmpty(itemId);

    }

}


