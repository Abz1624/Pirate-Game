using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Local prototype unlock gates shared by menu, level-up offers, and loadout.</summary>
    public static class V3ContentUnlocks
    {
        public const string FirstConquestKey = "v3_first_conquest_complete";

        public static bool HasCompletedFirstConquest =>
            PlayerPrefs.GetInt(FirstConquestKey, 0) == 1;

        public static bool IsWeaponUnlocked(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            return weaponId switch
            {
                "deck_cannons" => true,
                "broadside_battery" => true,
                "powder_kegs" => HasCompletedFirstConquest || V3CaptainSelection.Selected == V3CaptainId.Flint,
                "flying_cutlasses" => HasCompletedFirstConquest,
                _ => false
            };
        }

        public static bool IsLogbookUnlocked(string logbookId) => !string.IsNullOrEmpty(logbookId);

        public static bool IsItemUnlocked(string itemId) => !string.IsNullOrEmpty(itemId);
    }
}
