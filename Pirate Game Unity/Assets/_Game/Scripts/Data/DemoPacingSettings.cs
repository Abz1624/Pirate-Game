using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "DemoPacingSettings", menuName = "HighSeas/Demo Pacing Settings")]
    public sealed class DemoPacingSettings : ScriptableObject
    {
        [Header("Level Ups")]
        [Tooltip("Multiplier on XP required for levels 1 through Early Level Cap.")]
        [Range(0.3f, 1f)] public float earlyLevelXpMultiplier = 0.62f;
        [Min(1)] public int earlyLevelCap = 6;
        [Range(1f, 3f)] public float xpDropMultiplier = 1.35f;

        [Header("Weapon Growth")]
        [Range(1f, 2f)] public float upgradePowerMultiplier = 1.2f;
        [Range(1f, 2f)] public float startingDamageMultiplier = 1.1f;

        [Header("POI Timing")]
        [Min(0f)] public float bossBeaconMinSeconds = 180f;
        [Min(0.1f)] public float bossTelegraphSeconds = 0.7f;
        [Range(0f, 1f)] public float firstChestDiscount = 0.5f;

        [Header("Spawn Pressure")]
        [Range(0.1f, 2f)] public float postScriptedThreatMultiplier = 0.75f;
    }
}
