using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// A Logbook (M9): each rank taken adds <c>perLevelAdditive × rarity power</c> to one stat (and multiplies it by
    /// <c>perLevelMultiplicative</c>). From V3's GameDatabase logbooks; the four Logbooks whose stats were wrong in V3
    /// (register R-35 to R-38) point at their intended stats by default and a config edit restores V3's (the v3-parity preset that did is retired, D18; V5 M8z (7 Oct, review)).
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Logbook", fileName = "Logbook")]
    public sealed class LogbookConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string logbookId;
        public string displayName;
        [Tooltip("The stat each rank raises (GameEnums StatId 0–20). -1 = none (V3's Navigator's Charts pointed at a stat that does not exist, so it did nothing).")]
        public int stat;
        public float perLevelAdditive;
        public float perLevelMultiplicative = 1f;
        public int maxLevel = 10;
        [Tooltip("Offered in Saltwind (V3 V3LogbookCatalog.AllIds).")] public bool inSaltwindPool = true;
        [Tooltip("Order in V3's database (offer picks walk the Logbooks in this order).")] public int databaseOrder;

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(logbookId)) report.Error(this, nameof(logbookId), "missing id");
            if (stat != -1) report.StatId(this, nameof(stat), stat);
            if (maxLevel < 1) report.Error(this, nameof(maxLevel), "must be at least 1");
        }
    }
}
