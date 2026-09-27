using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Category weights for one loadout size (V3 LevelOfferGenerator.GetFillStateWeights).</summary>
    [Serializable]
    public sealed class OfferWeightRow
    {
        [Tooltip("This row applies while the ship carries at most this many weapons (the last row applies above it).")] public int maxWeapons;
        public float weaponUpgrade, newWeapon, logbookUpgrade, newLogbook, runItem;
    }

    /// <summary>
    /// Level-up card categories by how many weapons the ship carries (M9). Each card rolls a category from the row for
    /// the current loadout (categories that can't be offered drop out), then an offer in it.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Offer Weights", fileName = "OfferWeights")]
    public sealed class OfferWeightsConfig : V4ConfigAsset
    {
        [Tooltip("Rows by weapon count, ascending (V3's Saltwind table).")]
        public OfferWeightRow[] rows =
        {
            new OfferWeightRow { maxWeapons = 2, weaponUpgrade = 20f, newWeapon = 40f, logbookUpgrade = 15f, newLogbook = 25f },
            new OfferWeightRow { maxWeapons = 3, weaponUpgrade = 30f, newWeapon = 30f, logbookUpgrade = 20f, newLogbook = 20f },
            new OfferWeightRow { maxWeapons = 4, weaponUpgrade = 30f, newWeapon = 30f, logbookUpgrade = 20f, newLogbook = 20f },
            new OfferWeightRow { maxWeapons = 5, weaponUpgrade = 40f, newWeapon = 16f, logbookUpgrade = 18f, newLogbook = 12f },
            new OfferWeightRow { maxWeapons = 6, weaponUpgrade = 55f, newWeapon = 0f, logbookUpgrade = 45f, newLogbook = 0f },
        };

        /// <summary>The row for a loadout of <paramref name="weapons"/> (the last row above the table).</summary>
        public OfferWeightRow RowFor(int weapons)
        {
            if (rows == null || rows.Length == 0) return null;
            foreach (var row in rows) if (weapons <= row.maxWeapons) return row;
            return rows[rows.Length - 1];
        }

        public override void Validate(ConfigValidationReport report)
        {
            if (rows == null || rows.Length == 0) { report.Error(this, nameof(rows), "needs at least one row"); return; }
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                if (r == null) { report.Error(this, $"rows[{i}]", "empty row"); continue; }
                if (i > 0 && rows[i - 1] != null && r.maxWeapons <= rows[i - 1].maxWeapons) report.Error(this, $"rows[{i}].maxWeapons", "rows must ascend");
                report.WeightsNotZero(this, $"rows[{i}]", new[] { r.weaponUpgrade, r.newWeapon, r.logbookUpgrade, r.newLogbook, r.runItem });
                foreach (var w in new[] { r.weaponUpgrade, r.newWeapon, r.logbookUpgrade, r.newLogbook, r.runItem }) report.NotNegative(this, $"rows[{i}]", w);
            }
        }
    }
}
