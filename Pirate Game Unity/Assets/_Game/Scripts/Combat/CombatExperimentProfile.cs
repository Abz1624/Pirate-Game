using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>
    /// Data-driven A/B/C/D combat experiment profile. Profiles change configuration only — no gameplay forks.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatExperimentProfile", menuName = "HighSeas/Combat Experiment Profile")]
    public sealed class CombatExperimentProfile : ScriptableObject
    {
        public string profileId = "D_FULL_COMBAT_V2";
        public int weaponSlotCap = 6;
        public int startingWeaponCount = 1;
        public bool openingWeaponDraftEnabled = true;
        public bool directionalWeaponTargetingEnabled = true;
        public bool directorV2Enabled = true;
        public bool captainActivesEnabled;
        public bool weaponEvolutionsEnabled;
        [Min(0.1f)] public float spawnDensityMultiplier = 1f;
        [Min(0.1f)] public float enemyHpMultiplier = 1f;
        [Min(0.1f)] public float enemyDamageMultiplier = 1f;
        [Range(0.25f, 1.5f)] public float playerVfxIntensityMultiplier = 1f;
        public bool telemetryEnabled = true;

        public static CombatExperimentProfile CreateBuiltIn(string id)
        {
            var profile = CreateInstance<CombatExperimentProfile>();
            profile.profileId = id;
            switch (id)
            {
                case "A_CURRENT":
                    profile.weaponSlotCap = 2;
                    profile.openingWeaponDraftEnabled = false;
                    profile.directionalWeaponTargetingEnabled = false;
                    profile.directorV2Enabled = false;
                    profile.spawnDensityMultiplier = 1f;
                    profile.telemetryEnabled = true;
                    break;
                case "B_SIX_ONLY":
                    profile.weaponSlotCap = 6;
                    profile.openingWeaponDraftEnabled = true;
                    profile.directionalWeaponTargetingEnabled = false;
                    profile.directorV2Enabled = false;
                    profile.spawnDensityMultiplier = 1f;
                    break;
                case "C_SIX_DIRECTIONAL":
                    profile.weaponSlotCap = 6;
                    profile.openingWeaponDraftEnabled = true;
                    profile.directionalWeaponTargetingEnabled = true;
                    profile.directorV2Enabled = false;
                    profile.spawnDensityMultiplier = 1f;
                    break;
                default:
                    profile.profileId = "D_FULL_COMBAT_V2";
                    profile.weaponSlotCap = 6;
                    profile.openingWeaponDraftEnabled = true;
                    profile.directionalWeaponTargetingEnabled = true;
                    profile.directorV2Enabled = true;
                    profile.spawnDensityMultiplier = 1f;
                    profile.weaponEvolutionsEnabled = true;
                    break;
            }
            return profile;
        }
    }

    public static class CombatExperimentContext
    {
        const string PrefKey = "combat_experiment_profile";
        const string AimVisualsPrefKey = "combat_aim_visuals_enabled";
        const string CaptainActivesPrefKey = "combat_captain_actives_enabled";
        const string DefaultProfileId = "D_FULL_COMBAT_V2";

        public static readonly string[] ProfileIds =
        {
            "A_CURRENT",
            "B_SIX_ONLY",
            "C_SIX_DIRECTIONAL",
            "D_FULL_COMBAT_V2"
        };

        static CombatExperimentProfile _active;

        public static string SelectedProfileId =>
            PlayerPrefs.GetString(PrefKey, DefaultProfileId);

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

        public static void SetActive(CombatExperimentProfile profile)
        {
            _active = profile;
            if (profile != null && !string.IsNullOrEmpty(profile.profileId))
                PlayerPrefs.SetString(PrefKey, profile.profileId);
        }

        public static void SetActiveProfileId(string profileId)
        {
            SetActive(CreateBuiltIn(profileId));
        }

        public static CombatExperimentProfile CreateBuiltIn(string id) =>
            CombatExperimentProfile.CreateBuiltIn(id);

        public static string GetDisplayName(string profileId) => profileId switch
        {
            "A_CURRENT" => "A · Baseline (2 slots)",
            "B_SIX_ONLY" => "B · Six Arsenal",
            "C_SIX_DIRECTIONAL" => "C · Directional",
            _ => "D · Full Combat V2"
        };

        public static string GetDescription(string profileId) => profileId switch
        {
            "A_CURRENT" => "Legacy 2-weapon baseline for A/B comparison.",
            "B_SIX_ONLY" => "Six weapon slots without heading-sensitive targeting.",
            "C_SIX_DIRECTIONAL" => "Six slots plus aim arcs and heading matter.",
            _ => "Six slots, directional weapons, and Director V2 spawn pressure."
        };

        public static void ResetForTests() => _active = null;

        static CombatExperimentProfile ResolveActiveProfile()
        {
            var id = PlayerPrefs.GetString(PrefKey, DefaultProfileId);
            var asset = Resources.Load<CombatExperimentProfile>($"CombatExperiment/{id}");
            if (asset != null) return asset;
            return CombatExperimentProfile.CreateBuiltIn(id);
        }
    }
}
