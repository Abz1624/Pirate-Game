using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Prototype / developer toggles. Normal solo Saltwind uses gated progression unless developer preset is on.</summary>
    public static class V3PlaytestFlags
    {
        const string DeveloperPresetPrefKey = "highseas_v3_developer_preset";

        /// <summary>Legacy bypass for non-Saltwind experiment menus. Off for normal Saltwind verification.</summary>
        public const bool UnlockAllContent = false;

        /// <summary>When a stage balance profile is active, only developer preset bypasses Marks gates.</summary>
        public const bool SaltwindDebugUnlockAll = false;

        /// <summary>Editor/tests may force developer preset without PlayerPrefs.</summary>
        public static bool ForceDeveloperPresetForTests { get; set; }

        public static bool DeveloperPresetActive =>
            ForceDeveloperPresetForTests || PlayerPrefs.GetInt(DeveloperPresetPrefKey, 0) == 1;

        public static bool EnableRunPlaytestHotkeys => DeveloperPresetActive;
        public static bool EnableSpawnDirectorDebugUi => DeveloperPresetActive;
        public static bool EnableEnemyIdentityLabels => DeveloperPresetActive;

        /// <summary>Skip weapon cooldown firing only in non-Saltwind isolated behavior tests.</summary>
        public const bool DisableShipAutoFire = false;

        public static bool ShouldDisableShipAutoFire =>
            DisableShipAutoFire && !Balance.StageBalanceProfileContext.IsActive;

        public static bool BypassProgressionGates =>
            DeveloperPresetActive
            || (UnlockAllContent && !Balance.StageBalanceProfileContext.IsActive);

        public static void SetDeveloperPresetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(DeveloperPresetPrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetForTests()
        {
            ForceDeveloperPresetForTests = false;
        }
    }
}
