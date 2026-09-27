using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>A Captain's passive (V3 V3CaptainController). All per-level values use the player level.</summary>
    [Serializable]
    public sealed class CaptainPassive
    {
        [Tooltip("+ damage per player level, up to the cap (Rook, Sparky 0.005/0.25; Ember 0.004/0.18; Roxie 0.003/0.15).")]
        public float additiveDamagePerLevel;
        public float additiveDamageCap;
        [Tooltip("+ damage = run gold / this divisor, up to the cap (Richie: 4500, 0.2). 0 = none.")] public float additiveDamageGoldDivisor;
        public float goldDamageCap;
        [Tooltip("Running Fuse (Sparky): at speed m/s or faster for seconds, this weapon deals × multiplier.")] public bool runningFuse;
        public float runningFuseSpeed = 4f;
        public float runningFuseSeconds = 2f;
        public string runningFuseWeaponId;
        public float runningFuseMultiplier = 1.15f;
        [Tooltip("Armor per player level, up to the cap (Hank). V3 sets it but never applies armor to damage (register R-49): off until decided.")]
        public float armorPerLevel;
        public float armorCap;
        public bool armorApplied;
        [Tooltip("Burn damage × (Ember) and lightning damage × (Roxie). V3 computes them, nothing reads them (R-49): off until decided.")]
        public float burnDamageMultiplier = 1f;
        public float lightningDamageMultiplier = 1f;
        public bool elementMultipliersApplied;
    }

    /// <summary>A Captain's active skill (V3 V3CaptainActiveController.TryActivate and GetDefinition).</summary>
    [Serializable]
    public sealed class CaptainActive
    {
        public string displayName;
        public float cooldownSeconds = 18f;
        [Tooltip("Every weapon fires once (All Guns!).")] public bool fireAllWeapons;
        [Tooltip("Powder kegs arm at once (Light the Fuse).")] public bool armPowderKegs;
        [Tooltip("A lightning salvo (Ride the Gale).")] public bool fireLightningSalvo;
        [Tooltip("Incoming damage × for seconds (Brace!). 0 s = none.")] public float braceSeconds;
        public float braceIncomingDamageMultiplier = 1f;
        [Tooltip("Own speed × for seconds (Brace! slows the ship). 0 s = none.")] public float slowSeconds;
        public float slowMultiplier = 1f;
        [Tooltip("Speed boost × for seconds. 0 s = none.")] public float moveSeconds;
        public float moveMultiplier = 1f;
        [Tooltip("+ damage for seconds (Cut and Run). 0 s = none.")] public float damageBuffSeconds;
        public float damageBuffAdditive;
        [Tooltip("Powder keg blast radius × for seconds. 0 s = none.")] public float kegRadiusSeconds;
        public float kegRadiusMultiplier = 1f;
        [Tooltip("Flame range × and burn damage × for seconds (Stoke the Flames). 0 s = none.")] public float flameSeconds;
        public float flameRangeMultiplier = 1f;
        public float burnDamageMultiplier = 1f;
        [Tooltip("Pickup radius × for seconds (Cut and Run's summary; V3 never applies it, R-49).")] public float pickupRadiusSeconds;
        public float pickupRadiusMultiplier = 1f;
        public bool pickupRadiusApplied;
        [Tooltip("During this window a near miss refunds cooldown seconds (Roxie; V3 never detects near misses).")] public float nearMissWindowSeconds;
        public float nearMissCooldownRefund;
    }

    /// <summary>
    /// A Captain (M9): profile, passive, active and unlock rule, from V3's V3CaptainProfile, V3CaptainCatalog,
    /// V3CaptainController, V3CaptainActiveController and the stage profile's unlock costs. The starting weapons are
    /// the adopted decision (gameplay doc §21); <c>v3StartingWeaponId</c> keeps V3's for reference.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Captain", fileName = "Captain")]
    public sealed class CaptainConfig : V4ConfigAsset
    {
        [Tooltip("Save id (rook, brine, flint, richie, ember, roxie).")] [ConfigIdentity] public string captainKey;
        [Tooltip("V3's V3CaptainId value (saved selection).")] public int legacyId;
        public string displayName;
        public string title;
        [TextArea] public string menuDescription;
        [TextArea] public string passiveSummary;
        [TextArea] public string activeSummary;
        public Color themeColor = Color.white;
        [Tooltip("Position in the menu.")] public int menuOrder;

        [Header("Ship")]
        public float baseHull = 100f;
        public float baseMoveSpeed = 6.2f;
        [Tooltip("Starting weapon (adopted decision). First Conquest still starts with Swivel Guns.")] public string startingWeaponId;
        [Tooltip("V3's starting weapon (only used outside Saltwind).")] public string v3StartingWeaponId;

        public CaptainPassive passive = new CaptainPassive();
        public CaptainActive active = new CaptainActive();

        [Header("Unlock")]
        [Tooltip("Rum to unlock (0 = free).")] public int rumCost;
        [Tooltip("Locked until this weapon is owned (Ember, Roxie). Empty = none.")] public string prerequisiteWeaponId;
        [Tooltip("V3 rule: locked unless the save owns this content id. Empty = never locked.")] public string v3OwnershipKey;
        [Tooltip("V3 rule: First Conquest also unlocks it (Sparky).")] public bool v3UnlockedByFirstConquest;
        [Tooltip("M9 playtest (Richie): unlocked once this much gold has been earned across all runs (0 = no such rule). The V4 menu enforces locks in M14; the lifetime total is counted from M9.")]
        public int unlockLifetimeGold;

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(captainKey)) report.Error(this, nameof(captainKey), "missing key");
            report.Positive(this, nameof(baseHull), baseHull);
            report.Positive(this, nameof(baseMoveSpeed), baseMoveSpeed);
            if (string.IsNullOrWhiteSpace(startingWeaponId)) report.Error(this, nameof(startingWeaponId), "missing starting weapon");
            if (active == null) report.Error(this, nameof(active), "missing active");
            else report.Positive(this, "active.cooldownSeconds", active.cooldownSeconds);
            if (rumCost < 0) report.Error(this, nameof(rumCost), "must be 0 or more");
        }
    }
}
