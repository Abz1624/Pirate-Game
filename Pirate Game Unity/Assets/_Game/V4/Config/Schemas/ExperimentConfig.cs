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
        [AppliesFrom(3)] public bool encounterSpawnEnabled = true;
        [AppliesFrom(9)] public bool captainActivesEnabled;
        [AppliesFrom(9)] public bool weaponEvolutionsEnabled = true;
        [AppliesFrom(3)] public float spawnDensityMultiplier = 1f, enemyHpMultiplier = 1f, enemyDamageMultiplier = 1f;
        [AppliesFrom(6)] [Range(0.25f, 1.5f)] public float playerVfxIntensityMultiplier = 1f;
        [AppliesFrom(3)] public bool telemetryEnabled = true;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(spawnDensityMultiplier), spawnDensityMultiplier);
            report.Positive(this, nameof(enemyHpMultiplier), enemyHpMultiplier);
            report.Positive(this, nameof(enemyDamageMultiplier), enemyDamageMultiplier);
        }
    }
}
