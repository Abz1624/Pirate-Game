using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Single combat configuration: Full Combat V2 (Director V2 spawns, six slots, directional weapons).</summary>
    [CreateAssetMenu(fileName = "CombatExperimentProfile", menuName = "HighSeas/Combat Experiment Profile")]
    public sealed class CombatExperimentProfile : ScriptableObject
    {
        public const string FullCombatV2Id = "D_FULL_COMBAT_V2";

        public string profileId = FullCombatV2Id;
        public int weaponSlotCap = 6;
        public int startingWeaponCount = 1;
        public bool openingWeaponDraftEnabled = true;
        public bool directionalWeaponTargetingEnabled = true;
        public bool directorV2Enabled = true;
        public bool captainActivesEnabled;
        public bool weaponEvolutionsEnabled = true;
        [Min(0.1f)] public float spawnDensityMultiplier = 1f;
        [Min(0.1f)] public float enemyHpMultiplier = 1f;
        [Min(0.1f)] public float enemyDamageMultiplier = 1f;
        [Range(0.25f, 1.5f)] public float playerVfxIntensityMultiplier = 1f;
        public bool telemetryEnabled = true;

        public static CombatExperimentProfile CreateBuiltIn(string id = null)
        {
            var profile = CreateInstance<CombatExperimentProfile>();
            profile.profileId = FullCombatV2Id;
            profile.weaponSlotCap = 6;
            profile.openingWeaponDraftEnabled = true;
            profile.directionalWeaponTargetingEnabled = true;
            profile.directorV2Enabled = true;
            profile.spawnDensityMultiplier = 1f;
            profile.weaponEvolutionsEnabled = true;
            profile.telemetryEnabled = true;
            return profile;
        }
    }

    public static class CombatExperimentContext
    {
        const string AimVisualsPrefKey = "combat_aim_visuals_enabled";
        const string CaptainActivesPrefKey = "combat_captain_actives_enabled";

        public static readonly string[] ProfileIds = { CombatExperimentProfile.FullCombatV2Id };

        static CombatExperimentProfile _active;

        public static string SelectedProfileId => CombatExperimentProfile.FullCombatV2Id;

        public static bool ShowAimVisuals
        {
            get => PlayerPrefs.GetInt(AimVisualsPrefKey, 1) == 1;
            set => PlayerPrefs.SetInt(AimVisualsPrefKey, value ? 1 : 0);
        }

        public static bool CaptainActivesEnabled
        {
            get => PlayerPrefs.GetInt(CaptainActivesPrefKey, 0) == 1;
            set => PlayerPrefs.SetInt(CaptainActivesPrefKey, value ? 1 : 0);
        }

        public static CombatExperimentProfile Active =>
            _active ??= ResolveActiveProfile();

        public static void SetActive(CombatExperimentProfile profile) =>
            _active = profile ?? CreateBuiltIn();

        public static void SetActiveProfileId(string profileId) =>
            SetActive(CreateBuiltIn(profileId));

        public static CombatExperimentProfile CreateBuiltIn(string id = null) =>
            CombatExperimentProfile.CreateBuiltIn(id);

        public static string GetDisplayName(string profileId) => "Full Combat V2";

        public static string GetDescription(string profileId) =>
            "Six weapon slots, directional weapons, and Director V2 spawn pressure.";

        public static void ResetForTests() => _active = null;

        static CombatExperimentProfile ResolveActiveProfile()
        {
            var asset = Resources.Load<CombatExperimentProfile>(
                $"CombatExperiment/{CombatExperimentProfile.FullCombatV2Id}");
            return asset != null ? asset : CreateBuiltIn();
        }
    }
}
