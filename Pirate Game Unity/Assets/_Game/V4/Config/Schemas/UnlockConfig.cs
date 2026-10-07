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

    /// <summary>V5 M5: a Logbook or item bought with Rum (not in the run pools until owned).</summary>
    [Serializable]
    public sealed class ContentUnlock
    {
        public string contentId;
        [Tooltip("Rum to unlock.")] public int rumCost;
    }

    /// <summary>V5 M8z (8 Oct, close): a removed or renamed content id and the id that replaced it (saves and drafts holding
    /// the old id resolve to the new one).</summary>
    [Serializable]
    public sealed class ContentIdAlias
    {
        public string fromId;
        public string toId;
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
        // V5 M8z (7 Oct): the weapon switch-off list, moved from code (WeaponFeatureFlags) to config.
        [Tooltip("V5 M8z: weapons switched off in voyages and 1v1s (level-up cards, chests, the draft, the shop, starting weapons). Applied when the menu or a run loads the config.")]
        [AppliesFrom(0, "read when the menu or a run starts")] public string[] disabledWeaponIds = Array.Empty<string>();
        public WeaponUnlock[] weaponUnlocks = Array.Empty<WeaponUnlock>();
        [Tooltip("V5 M5: Logbooks bought with Rum; the others are always in the level-up pool.")]
        public ContentUnlock[] logbookUnlocks = Array.Empty<ContentUnlock>();
        [Tooltip("V5 M5: items bought with Rum; the others are always in the chest pool.")]
        public ContentUnlock[] itemUnlocks = Array.Empty<ContentUnlock>();
        [Tooltip("V5 M8z (8 Oct, close): removed or renamed content ids (weapons, evolutions, Logbooks, items) and their replacements. A save or a saved 1v1 draft holding an old id resolves it to the new one when it loads (the Ramming Prow bought in a save is the Squid Ink Pots).")]
        [AppliesFrom(0, "read when the menu or a run starts")] public ContentIdAlias[] contentIdAliases = Array.Empty<ContentIdAlias>();

        /// <summary>V5 M8z (8 Oct, close): the aliases as old id → new id pairs (for the Foundation's registry).</summary>
        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> AliasPairs()
        {
            foreach (var a in contentIdAliases ?? Array.Empty<ContentIdAlias>())
                if (a != null) yield return new System.Collections.Generic.KeyValuePair<string, string>(a.fromId, a.toId);
        }

        /// <summary>A Logbook's or item's Rum price (0 = free).</summary>
        public int RumCost(string contentId)
        {
            foreach (var u in logbookUnlocks) if (u != null && u.contentId == contentId) return u.rumCost;
            foreach (var u in itemUnlocks) if (u != null && u.contentId == contentId) return u.rumCost;
            return 0;
        }
        [Tooltip("The weapon of a First Conquest run (no conquest recorded yet).")] public string firstConquestWeaponId;
        public StartingWeaponRule startingWeaponRule = StartingWeaponRule.CaptainWeapon;
        [Tooltip("M14.13: the menu's Captain lock rule (V3Ownership is V3's).")] [AppliesFrom(14)]
        public CaptainLockRule captainLockRule = CaptainLockRule.CaptainConfig;

        public override void Validate(ConfigValidationReport report)
        {
            if (starterWeaponIds == null || starterWeaponIds.Length == 0) report.Error(this, nameof(starterWeaponIds), "needs at least one starter weapon");
            if (string.IsNullOrWhiteSpace(firstConquestWeaponId)) report.Error(this, nameof(firstConquestWeaponId), "missing");
            if (weaponUnlocks != null)
                for (var i = 0; i < weaponUnlocks.Length; i++)
                    if (weaponUnlocks[i] != null && weaponUnlocks[i].rumCost < 0) report.Error(this, $"weaponUnlocks[{i}].rumCost", "must be 0 or more");
            // V5 M8z (8 Oct, close): an alias names both ids, never itself, once per old id, and an old id is not a live one.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < (contentIdAliases?.Length ?? 0); i++)
            {
                var a = contentIdAliases[i];
                if (a == null || string.IsNullOrWhiteSpace(a.fromId) || string.IsNullOrWhiteSpace(a.toId)) { report.Error(this, $"contentIdAliases[{i}]", "needs both ids"); continue; }
                if (a.fromId == a.toId) report.Error(this, $"contentIdAliases[{i}]", "maps an id to itself");
                if (!seen.Add(a.fromId)) report.Error(this, $"contentIdAliases[{i}]", $"{a.fromId} is aliased twice");
                if (Array.IndexOf(starterWeaponIds ?? Array.Empty<string>(), a.fromId) >= 0 || Array.Exists(weaponUnlocks ?? Array.Empty<WeaponUnlock>(), u => u != null && u.weaponId == a.fromId))
                    report.Error(this, $"contentIdAliases[{i}]", $"{a.fromId} is still a live weapon");
            }
        }
    }
}
