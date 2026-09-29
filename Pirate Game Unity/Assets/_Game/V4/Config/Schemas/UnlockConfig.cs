using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class WeaponUnlock
    {
        public string weaponId;
        [Tooltip("Rum to unlock.")] public int rumCost;
    }

    /// <summary>How a run picks its starting weapon.</summary>
    public enum StartingWeaponRule
    {
        [Tooltip("V3: Saltwind always starts with the First Conquest weapon (Swivel Guns).")] V3AlwaysFirstConquestWeapon = 0,
        [Tooltip("Adopted decision: the Captain's starting weapon; First Conquest still starts with the First Conquest weapon.")] CaptainWeapon = 1,
    }

    /// <summary>Which rule locks Captains in the menu (M14.13).</summary>
    public enum CaptainLockRule
    {
        [Tooltip("V3: Hank needs the save to own \"brine\", Sparky \"flint\" or a First Conquest; the rest are open.")] V3Ownership = 0,
        [Tooltip("Adopted decision (M9 gate): each Captain config's Rum cost, prerequisite weapon and lifetime-gold rule.")] CaptainConfig = 1,
    }

    /// <summary>
    /// Content unlocks (M9): the starter weapons, Rum unlock costs, the First Conquest weapon and how a run picks its
    /// starting weapon. From V3's stage profile (starterWeaponIds, weaponUnlocks) and ContentUnlocks.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Unlocks", fileName = "Unlocks")]
    public sealed class UnlockConfig : V4ConfigAsset
    {
        [Tooltip("Always unlocked.")] public string[] starterWeaponIds = Array.Empty<string>();
        public WeaponUnlock[] weaponUnlocks = Array.Empty<WeaponUnlock>();
        [Tooltip("The weapon of a First Conquest run (no conquest recorded yet).")] public string firstConquestWeaponId;
        public StartingWeaponRule startingWeaponRule = StartingWeaponRule.CaptainWeapon;
        [Tooltip("M14.13: the menu's Captain lock rule (the v3-parity preset sets V3Ownership).")] [AppliesFrom(14)]
        public CaptainLockRule captainLockRule = CaptainLockRule.CaptainConfig;

        public override void Validate(ConfigValidationReport report)
        {
            if (starterWeaponIds == null || starterWeaponIds.Length == 0) report.Error(this, nameof(starterWeaponIds), "needs at least one starter weapon");
            if (string.IsNullOrWhiteSpace(firstConquestWeaponId)) report.Error(this, nameof(firstConquestWeaponId), "missing");
            if (weaponUnlocks != null)
                for (var i = 0; i < weaponUnlocks.Length; i++)
                    if (weaponUnlocks[i] != null && weaponUnlocks[i].rumCost < 0) report.Error(this, $"weaponUnlocks[{i}].rumCost", "must be 0 or more");
        }
    }
}
