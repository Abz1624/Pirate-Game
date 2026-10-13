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
        [Tooltip("Hackathon (13 Oct, owner): the Chaos Logbook. Each rank's card rolls (on the run's Loot stream) one of the Logbooks in randomStatFrom and shows it; taking it adds that Logbook's per-rank stat. stat and perLevelAdditive are not used.")]
        public bool randomStat;
        [Tooltip("The Logbooks a random-stat rank can roll (their stat and per-rank value).")] public string[] randomStatFrom = System.Array.Empty<string>();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(logbookId)) report.Error(this, nameof(logbookId), "missing id");
            if (stat != -1) report.StatId(this, nameof(stat), stat);
            if (maxLevel < 1) report.Error(this, nameof(maxLevel), "must be at least 1");
            if (randomStat && (randomStatFrom == null || randomStatFrom.Length == 0)) report.Error(this, nameof(randomStatFrom), "a random-stat Logbook needs Logbooks to roll from");
        }
    }
}
