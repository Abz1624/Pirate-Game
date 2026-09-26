using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>One explicit view pool: a prefab under a key, how many to create up front, the cap and what happens at it.</summary>
    [Serializable]
    public sealed class PoolCatalogEntry
    {
        public string key = "pool";
        public GameObject prefab;
        [Tooltip("Instances created before the run starts.")] public int prewarm = 8;
        [Tooltip("Most instances alive at once (0 = no cap).")] public int cap = 32;
        public ConfigEnums.PoolOverflowPolicy overflow = ConfigEnums.PoolOverflowPolicy.Grow;
        [Tooltip("Registered only in the V4ModuleLab scene (test and stress pools).")] public bool moduleLabOnly;
    }

    /// <summary>
    /// The V4 view pool catalog (M4): explicit entries plus entries derived at run start from the enemies the wave
    /// timeline can spawn (one per visual model). Weapon projectile, muzzle and impact keys are added in M5, when
    /// weapons get V4 visuals. Nothing in the run renders through the pool before M5 (weapons) and M8 (enemy views),
    /// so derived enemy entries are listed but only prewarmed when <see cref="prewarmEnemyViews"/> is on.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Pool Catalog", fileName = "PoolCatalog")]
    public sealed class PoolCatalogConfig : V4ConfigAsset
    {
        [Tooltip("The run's pools are built once at run start: edits apply on restart.")]
        [AppliesFrom(0)] public List<PoolCatalogEntry> entries = new List<PoolCatalogEntry>();

        [Header("Derived enemy view entries")]
        [Tooltip("Add one entry per enemy model the wave timeline can spawn.")] [AppliesFrom(0)] public bool deriveEnemyViews = true;
        [Tooltip("Create the derived enemy views up front. Off until V4 enemy views use the pool (M8): nothing would use them.")] [AppliesFrom(8)] public bool prewarmEnemyViews;
        [Tooltip("Prewarm per enemy model when the archetype's poolPrewarm is 0.")] [AppliesFrom(8)] public int enemyViewPrewarm = 12;
        [Tooltip("Prewarm for boss and miniboss models when the archetype's poolPrewarm is 0.")] [AppliesFrom(8)] public int bossViewPrewarm = 1;
        [Tooltip("Cap per enemy model (0 = no cap).")] [AppliesFrom(8)] public int enemyViewCap = 0;
        [AppliesFrom(8)] public ConfigEnums.PoolOverflowPolicy enemyViewOverflow = ConfigEnums.PoolOverflowPolicy.Grow;

        [Header("Derived projectile view entries (M5)")]
        [Tooltip("Add one entry per projectile model the weapons fire (and the default model).")] [AppliesFrom(0)] public bool deriveProjectileViews = true;
        [AppliesFrom(0)] public int projectileViewPrewarm = 24;
        [Tooltip("Cap per projectile model (0 = no cap).")] [AppliesFrom(0)] public int projectileViewCap = 0;
        [AppliesFrom(0)] public ConfigEnums.PoolOverflowPolicy projectileViewOverflow = ConfigEnums.PoolOverflowPolicy.Grow;

        public override void Validate(ConfigValidationReport report)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var at = $"entries[{i}]";
                if (e == null) { report.Error(this, at, "empty entry"); continue; }
                if (string.IsNullOrWhiteSpace(e.key)) report.Error(this, at + ".key", "missing key");
                else if (!keys.Add(e.key)) report.Error(this, at + ".key", $"duplicate key '{e.key}'");
                if (e.prefab == null) report.Error(this, at + ".prefab", "missing prefab");
                if (e.prewarm < 0) report.Error(this, at + ".prewarm", "negative prewarm");
                if (e.cap < 0) report.Error(this, at + ".cap", "negative cap");
                if (e.cap > 0 && e.prewarm > e.cap) report.Error(this, at + ".prewarm", $"prewarm {e.prewarm} above cap {e.cap}");
            }
            if (enemyViewPrewarm < 0) report.Error(this, nameof(enemyViewPrewarm), "negative prewarm");
            if (bossViewPrewarm < 0) report.Error(this, nameof(bossViewPrewarm), "negative prewarm");
            if (enemyViewCap < 0) report.Error(this, nameof(enemyViewCap), "negative cap");
        }
    }
}
