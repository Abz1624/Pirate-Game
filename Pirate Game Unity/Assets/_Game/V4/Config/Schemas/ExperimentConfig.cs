using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Mirror of the V3 CombatExperimentProfile (the built-in D_FULL_COMBAT_V2 defaults; no asset exists in V3).
    /// The V4 Spawn Controller applies the density and enemy multipliers from M3.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Experiment Config", fileName = "ExperimentConfig")]
    public sealed class ExperimentConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string legacyProfileId = "D_FULL_COMBAT_V2";
        [AppliesFrom(9)] public int weaponSlotCap = 6;
        [AppliesFrom(9)] public int startingWeaponCount = 1;
        [AppliesFrom(9)] public bool openingWeaponDraftEnabled;
        [AppliesFrom(5)] public bool directionalWeaponTargetingEnabled = true;
        [Tooltip("Off: no ambient spawns (beats, overtime and encounters still run).")] public bool encounterSpawnEnabled = true;
        [AppliesFrom(9)] public bool captainActivesEnabled;
        [Tooltip("R-49 (on = V3): gamepad South (also the boost button) uses the Captain's active; off: the right shoulder does. Kept on for now (M9 playtest: no gamepad to test with).")]
        [AppliesFrom(9)] public bool captainActiveOnGamepadSouth;
        [Tooltip("R-40 fix (0 = V3, off): a hostile shot passing within this many metres of the ship's hit circle without hitting is a near-miss dodge (Ghost Lantern, Roxie's refund).")]
        [AppliesFrom(9)] public float nearMissMargin = 2f;
        [Tooltip("M9 playtest (off = V3): an active whose only effect is firing the weapons (All Guns!) keeps its charge when no enemy is in range, and a toast says so.")]
        [AppliesFrom(9)] public bool captainActiveHoldsWithoutTarget = true;
        [AppliesFrom(9)] public bool weaponEvolutionsEnabled = true;
        public float spawnDensityMultiplier = 1f, enemyHpMultiplier = 1f, enemyDamageMultiplier = 1f;
        [AppliesFrom(14, "V3 never reads it either; the player juice presenter in M14")] [Range(0.25f, 1.5f)] public float playerVfxIntensityMultiplier = 1f;
        [AppliesFrom(12)] public bool telemetryEnabled = true;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(spawnDensityMultiplier), spawnDensityMultiplier);
            report.Positive(this, nameof(enemyHpMultiplier), enemyHpMultiplier);
            report.Positive(this, nameof(enemyDamageMultiplier), enemyDamageMultiplier);
        }
    }
}
