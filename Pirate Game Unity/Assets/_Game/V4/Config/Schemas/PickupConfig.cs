using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Pickups (M9): kill drops, collection radii, barrel bursts and the flying pickups, from V3's PickupWorld,
    /// DemoPacingSettings and DemoJuiceSettings. Views stay V3's pickup view pool (M14).
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Pickups", fileName = "Pickups")]
    public sealed class PickupConfig : V4ConfigAsset
    {
        [Header("Kill drops")]
        [Tooltip("Chance a kill also drops a health barrel.")] public float healthDropChance = 0.12f;
        [Tooltip("Hull the health barrel restores.")] public int healthDropValue = 25;
        [Tooltip("Several drops spread on a circle this wide (m), with angle and radius jitter.")] public float dropSpreadRadius = 1.6f;
        public float dropAngleJitterDegrees = 15f;
        public float dropRadiusJitter = 0.15f;
        [Tooltip("With every pickup slot taken, a drop merges into a same-type pickup within this distance (else it is lost).")] public float mergeRadius = 2f;

        [Header("Collection")]
        [Tooltip("XP crystals home in within this distance (or the ship's pickup radius, if larger).")] public float xpAutoPickupRadius = 4.25f;
        [Tooltip("Coins home in within this distance.")] public float goldAutoPickupRadius = 26f;
        [Tooltip("Barrels open when the ship comes this close.")] public float barrelTouchRadius = 5f;
        [Tooltip("Homing speed from min (far) to max (close).")] public float flySpeedMin = 10f;
        public float flySpeedMax = 28f;
        [Tooltip("A homing pickup pays this close to the ship.")] public float collectDistance = 0.75f;
        [Tooltip("Bobbing on the water: amplitude (m) and speed.")] public float bobAmplitude = 0.12f;
        public float bobSpeed = 3.5f;

        [Header("Barrel bursts")]
        [Tooltip("An opened barrel bursts into this many flying pieces (random between min and max, × effects density, at least burstMinimum).")]
        public int burstCountMin = 6;
        public int burstCountMax = 10;
        public int burstMinimum = 3;
        [Tooltip("At most this many flying pieces appear per frame (the rest wait).")] public int maxFlyingSpawnsPerFrame = 18;

        public override void Validate(ConfigValidationReport report)
        {
            if (healthDropChance < 0f || healthDropChance > 1f) report.Error(this, nameof(healthDropChance), "must be 0-1");
            report.Positive(this, nameof(barrelTouchRadius), barrelTouchRadius);
            report.Positive(this, nameof(collectDistance), collectDistance);
            if (burstCountMin < 1 || burstCountMax < burstCountMin) report.Error(this, nameof(burstCountMin), "needs 1 ≤ min ≤ max");
            if (maxFlyingSpawnsPerFrame < 1) report.Error(this, nameof(maxFlyingSpawnsPerFrame), "must be at least 1");
        }
    }
}
