using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Stormshot Cannon: the weapon fires arc lanes, and the first enemy hit emits a short pellet burst.</summary>
    [Serializable]
    public sealed class LaneBurstBlock
    {
        public int pellets = 5;
        public float spreadDegrees = 55f, rayLength = 6f, hitRadius = 0.75f, damageMultiplier = 0.35f, procCoefficient = 0.7f, sizeBase = 0.22f, lifeSeconds = 2f;
    }

    /// <summary>King's Broadside: a delayed follow-up volley to the other side.</summary>
    [Serializable]
    public sealed class FollowUpBlock
    {
        public float delaySeconds = 0.12f;
        public int minPellets = 2;
        public float angleStepDegrees = 6f, damageMultiplier = 0.45f, sizeBase = 0.2f, lifeSeconds = 2.2f, hitRadius = 0.75f;
    }

    /// <summary>Devil's Wake: a second keg beside each drop, and blasts arm nearby kegs.</summary>
    [Serializable]
    public sealed class WakeKegsBlock
    {
        public float sideOffset = 1.6f, chainRadius = 4.2f, chainArmSeconds = 0.08f;
    }

    /// <summary>Bladed Tide: the orbit radius pulses.</summary>
    [Serializable]
    public sealed class OrbitPulseBlock
    {
        [Tooltip("Radius × (minimum + amplitude × (0.5 + 0.5 sin(time × frequency))).")] public float minimum = 0.72f, amplitude = 0.38f, frequency = 1.75f;
    }

    /// <summary>Hellfire Nozzle: a wider cone; direct flame kills leave burning patches.</summary>
    [Serializable]
    public sealed class WideConeBlock
    {
        public float arcMultiplier = 1.2f;
        public bool patchesOnKill = true;
    }

    /// <summary>Tempest Mast: a jump strike on the nearest other enemy, then a delayed strike on the densest cluster.</summary>
    [Serializable]
    public sealed class JumpStrikeBlock
    {
        public float jumpRange = 9f, jumpRadiusFactor = 0.75f, jumpDamageMultiplier = 0.65f, jumpProcMultiplier = 0.8f;
        public float delayedSeconds = 0.85f, delayedRadiusFactor = 0.8f, delayedDamageMultiplier = 0.55f, delayedProcMultiplier = 0.5f;
    }

    /// <summary>
    /// A weapon evolution (M5, from V3's WeaponEvolutionCatalog). Which weapons are evolved still comes from the legacy
    /// level system until M9; this asset says what the evolution does to its weapon's behavior, with its numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Weapon Evolution", fileName = "WeaponEvolution")]
    public sealed class WeaponEvolutionConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string evolutionId;
        public string displayName, description;
        public WeaponConfig baseWeapon;
        [Tooltip("Item that unlocks it (an item config from M9).")] public string requiredItemId;
        public ConfigEnums.EvolutionEffect effect;
        public LaneBurstBlock laneBurst = new LaneBurstBlock();
        public FollowUpBlock followUp = new FollowUpBlock();
        public WakeKegsBlock wakeKegs = new WakeKegsBlock();
        public OrbitPulseBlock orbitPulse = new OrbitPulseBlock();
        public WideConeBlock wideCone = new WideConeBlock();
        public JumpStrikeBlock jumpStrike = new JumpStrikeBlock();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(evolutionId)) report.Error(this, nameof(evolutionId), "missing evolution id");
            if (baseWeapon == null) report.Error(this, nameof(baseWeapon), "missing base weapon");
            if (effect == ConfigEnums.EvolutionEffect.None) report.Error(this, nameof(effect), "no effect");
        }
    }
}
