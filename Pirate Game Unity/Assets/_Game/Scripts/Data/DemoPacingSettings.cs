using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "DemoPacingSettings", menuName = "HighSeas/Demo Pacing Settings")]
    public sealed class DemoPacingSettings : ScriptableObject
    {
        [Header("Level Ups")]
        [Tooltip("Multiplier on XP required for levels 1 through Early Level Cap.")]
        [Range(0.3f, 1.5f)] public float earlyLevelXpMultiplier = 0.95f;
        [Min(1)] public int earlyLevelCap = 6;
        [Range(0.5f, 2f)] public float xpRequirementMultiplier = 1.28f;
        [Range(0.5f, 3f)] public float xpDropMultiplier = 1.05f;

        [Header("Weapon Growth")]
        [Range(1f, 2f)] public float upgradePowerMultiplier = 1.2f;
        [Range(1f, 2f)] public float startingDamageMultiplier = 1.1f;

        [Header("POI Timing")]
        [Min(0f)] public float bossBeaconMinSeconds = 180f;
        [Min(0.1f)] public float bossTelegraphSeconds = 0.7f;
        [Range(0f, 1f)] public float firstChestDiscount = 0.5f;

        [Header("Spawn Pressure")]
        [Range(0.1f, 2f)] public float postScriptedThreatMultiplier = 0.75f;
        [Tooltip("Seconds into the run before the first reef crab miniboss spawns.")]
        [Min(0f)] public float firstMinibossSeconds = 90f;

        [Header("Pickups")]
        [Tooltip("XP crystals auto-collect within this range.")]
        [Min(0.5f)] public float xpAutoPickupRadius = 4.25f;
        [Tooltip("Loose gold coins auto-collect within this range.")]
        [Min(0.5f)] public float goldAutoPickupRadius = 26f;
        [Tooltip("Health barrels and gold barrels require sailing this close to collect.")]
        [Min(0.3f)] public float barrelTouchRadius = 5f;
        [Range(0f, 1f)] public float healthDropChance = 0.12f;
        [Tooltip("Visual flyers spawned when a barrel breaks (value is split across them).")]
        [Min(3)] public int barrelBurstVisualMin = 6;
        [Min(3)] public int barrelBurstVisualMax = 10;
        [Tooltip("Caps flyer spawns per frame so multiple barrel collects do not hitch.")]
        [Min(4)] public int maxFlyingPickupSpawnsPerFrame = 18;
    }
}
