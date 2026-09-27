using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Floor powerups (same names and order as V3's V3FloorPowerupKind).</summary>
    public enum PowerupKind { RepairSupplies = 0, BattleSignal = 1, Tailwind = 2, SalvageChime = 3, FullSail = 4, PowderRush = 5, MagneticTide = 6, DeadCalm = 7 }

    [Serializable]
    public sealed class PowerupPoolEntry
    {
        public PowerupKind kind;
        [Tooltip("Relative chance among the pool (the roll walks the entries in order).")] public float weight = 25f;
    }

    /// <summary>
    /// Floor powerups (M9): the drop roll on kills, the pool and each powerup's effect, from V3's V3FloorPowerupWorld,
    /// V3PowerupCatalog and the stage profile's rewards block.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Powerups", fileName = "Powerups")]
    public sealed class PowerupConfig : V4ConfigAsset
    {
        [Header("Drops")]
        [Tooltip("Chance per non-boss kill.")] public float dropChancePerKill = 0.01f;
        [Tooltip("Seconds after a drop before the next roll can succeed.")] public float minIntervalSeconds = 20f;
        [Tooltip("Elites always drop one (skipping the chance and the interval; the cap, a free slot and a spot still apply).")] public bool elitesAlwaysDrop = true;
        [Tooltip("At most this many per stage (also the number of powerups on the water at once).")] public int maxPerStage = 6;
        [Tooltip("Seconds a powerup stays on the water.")] public float lifetimeSeconds = 30f;
        [Tooltip("The ship collects it this close (m).")] public float collectRadius = 1.5f;
        [Tooltip("Spot search around the kill: rings × points, ring spacing (m).")] public int searchRings = 4;
        public int searchPointsPerRing = 8;
        public float searchRingSpacing = 1.5f;
        [Tooltip("The spot keeps this far inside the play bounds (m) and clear of islands by the clearance.")] public float boundsMargin = 1f;
        public float islandClearance = 0.45f;
        [Tooltip("The pool (V3 Saltwind: Repair Supplies, Powder Rush, Magnetic Tide, Dead Calm, 25 each).")]
        public PowerupPoolEntry[] pool =
        {
            new PowerupPoolEntry { kind = PowerupKind.RepairSupplies, weight = 25f },
            new PowerupPoolEntry { kind = PowerupKind.PowderRush, weight = 25f },
            new PowerupPoolEntry { kind = PowerupKind.MagneticTide, weight = 25f },
            new PowerupPoolEntry { kind = PowerupKind.DeadCalm, weight = 25f },
        };

        [Header("Effects")]
        [Tooltip("Repair Supplies heals this fraction of max hull.")] public float repairMaxHullFraction = 0.2f;
        [Tooltip("Battle Signal: damage × for seconds.")] public float battleSignalDamageMultiplier = 1.25f;
        public float battleSignalSeconds = 8f;
        [Tooltip("Tailwind: move speed × for seconds.")] public float tailwindSpeedMultiplier = 1.2f;
        public float tailwindSeconds = 6f;
        [Tooltip("Full Sail: move speed × for seconds (shares Tailwind's buff).")] public float fullSailSpeedMultiplier = 1.45f;
        public float fullSailSeconds = 8f;
        [Tooltip("Powder Rush: the bonus for seconds.")] public float powderRushBonus = 0.3f;
        public float powderRushSeconds = 8f;
        [Tooltip("V3 (register R-43): Powder Rush gives damage × (1 + bonus) in Battle Signal's slot despite its attack-speed name. Off: attack speed × (1 + bonus) in its own slot.")]
        public bool powderRushIsDamageInBattleSignalSlot = true;
        [Tooltip("Dead Calm: enemy spawn rate × (1 − slow) for seconds (not during the boss fight).")] public float deadCalmSlowFraction = 0.3f;
        public float deadCalmSeconds = 5f;

        public override void Validate(ConfigValidationReport report)
        {
            if (dropChancePerKill < 0f || dropChancePerKill > 1f) report.Error(this, nameof(dropChancePerKill), "must be 0-1");
            if (maxPerStage < 1) report.Error(this, nameof(maxPerStage), "must be at least 1");
            report.Positive(this, nameof(lifetimeSeconds), lifetimeSeconds);
            report.Positive(this, nameof(collectRadius), collectRadius);
            if (pool == null || pool.Length == 0) report.Error(this, nameof(pool), "needs at least one powerup");
            else report.WeightsNotZero(this, nameof(pool), Array.ConvertAll(pool, p => p != null ? p.weight : 0f));
            if (deadCalmSlowFraction < 0f || deadCalmSlowFraction > 0.9f) report.Error(this, nameof(deadCalmSlowFraction), "must be 0-0.9");
        }
    }
}
