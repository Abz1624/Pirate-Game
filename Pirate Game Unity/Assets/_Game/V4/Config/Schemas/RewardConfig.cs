using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>What earns a Rum row credit (each maps to V4 combat events; no enemy or POI ids).</summary>
    public enum RumSource
    {
        /// <summary>A whole pre-deadline minute with at least <see cref="RumRow.minKills"/> kills (closed minutes only).</summary>
        QualifyingMinute = 0,
        /// <summary>An overtime minute with at least minKills kills, credited when the next overtime minute begins (V3 rule).</summary>
        OvertimeMinute = 1,
        /// <summary>A special landmark used (LandmarkUsed event; chests and the Beacon never publish it). Once per landmark.</summary>
        SpecialLandmark = 2,
        /// <summary>A scheduled elite (a timeline focus phase with a scheduled-elite index) sunk. Once per elite.</summary>
        ScheduledElite = 3,
        /// <summary>The Barnacle King defeated (BossState 2).</summary>
        BossVictory = 4,
        /// <summary>A chest opened (ChestOpened event).</summary>
        ChestOpened = 5,
        /// <summary>Run gold left at death, banked as Rum on a flattening curve: maxRum × (1 − ½^(gold / goldForHalf)), so
        /// hoarded or overtime-farmed gold can never pay more than maxRum (playtest round 2; Narrative Bible: the remaining
        /// run Gold is banked when the run ends).</summary>
        RunGoldBanked = 6,
    }

    /// <summary>One Rum rule: a source, how much each credit pays, and its caps.</summary>
    [Serializable]
    public sealed class RumRow
    {
        [Tooltip("Stable id (console, tests).")] public string rowId = "row";
        [Tooltip("Shown on the results screen.")] public string label = "Row";
        public bool enabled = true;
        public RumSource source;
        [Tooltip("Rum per credit.")] public int rum = 1;
        [Tooltip("Minute sources: kills needed in the minute.")] public int minKills = 10;
        [Tooltip("At most this many credits per run (0 = no limit). Not used by the gold row (use maxRum there).")] public int maxCredits;
        [Tooltip("At most this much Rum from the row per run (0 = no limit); credits stop once it is reached.")] public int maxRum;
        [Tooltip("RunGoldBanked: leftover gold that pays half of maxRum; each further goldForHalf pays half of what is left.")] public float goldForHalf = 1500f;
    }

    /// <summary>
    /// Rum (M12, gameplay doc §11a): the meta currency earned in a run, paid once per run id at death. Every source is a
    /// data row listening to V4 events; the defaults are V3's Marks rules (57 on the approved fixture). The save field
    /// stays <c>marks</c>; players only ever read "Rum".
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Rewards", fileName = "Rewards")]
    public sealed class RewardConfig : V4ConfigAsset
    {
        [Tooltip("The currency's player-facing name.")] public string currencyName = "Rum";
        public List<RumRow> rows = new List<RumRow>
        {
            new RumRow { rowId = "minutes", label = "Active minutes", source = RumSource.QualifyingMinute, rum = 2, minKills = 10 },
            new RumRow { rowId = "landmarks", label = "Special discoveries", source = RumSource.SpecialLandmark, rum = 3 },
            new RumRow { rowId = "elites", label = "Scheduled elite", source = RumSource.ScheduledElite, rum = 5, maxCredits = 2 },
            new RumRow { rowId = "boss", label = "Boss victory", source = RumSource.BossVictory, rum = 25, maxCredits = 1 },
            new RumRow { rowId = "overtime", label = "Overtime", source = RumSource.OvertimeMinute, rum = 3, minKills = 10, maxRum = 15 },
            new RumRow { rowId = "chests", label = "Chests opened", source = RumSource.ChestOpened, rum = 1, enabled = false },
            new RumRow { rowId = "gold", label = "Gold banked", source = RumSource.RunGoldBanked, rum = 0, maxRum = 60, goldForHalf = 1500f },
        };

        public RumRow Row(string rowId)
        {
            foreach (var r in rows) if (r != null && r.rowId == rowId) return r;
            return null;
        }

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(currencyName)) report.Error(this, nameof(currencyName), "missing currency name");
            var ids = new HashSet<string>();
            foreach (var r in rows)
            {
                if (r == null) { report.Error(this, nameof(rows), "null row"); continue; }
                if (string.IsNullOrWhiteSpace(r.rowId) || !ids.Add(r.rowId)) report.Error(this, nameof(rows), $"missing or duplicate row id '{r.rowId}'");
                if (r.rum < 0 || r.minKills < 0 || r.maxCredits < 0 || r.maxRum < 0) report.Error(this, nameof(rows), $"row '{r.rowId}': negative value");
                if (r.source == RumSource.RunGoldBanked && (r.goldForHalf <= 0f || r.maxRum <= 0))
                    report.Error(this, nameof(rows), $"row '{r.rowId}': the gold curve needs maxRum and goldForHalf above 0");
            }
        }
    }
}
