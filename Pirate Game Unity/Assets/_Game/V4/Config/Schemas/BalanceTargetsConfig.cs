using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>A value that should fall inside [min, max]; a bound of NaN is not checked.</summary>
    [Serializable]
    public struct TargetBand
    {
        public float min, max;
        public TargetBand(float min, float max) { this.min = min; this.max = max; }
        public bool Contains(float value) => (float.IsNaN(min) || value >= min) && (float.IsNaN(max) || value <= max);
        public override string ToString() => $"{(float.IsNaN(min) ? "–" : min.ToString("0.##"))}–{(float.IsNaN(max) ? "–" : max.ToString("0.##"))}";
    }

    /// <summary>What a personality's runs should look like (medians over the simulator's runs).</summary>
    [Serializable]
    public sealed class PersonalityTargets
    {
        public string personalityId;
        [Tooltip("Median minute the ship sinks.")] public TargetBand deathMinute = new TargetBand(float.NaN, float.NaN);
        [Tooltip("Share of runs that beat the Barnacle King.")] public TargetBand bossWinRate = new TargetBand(float.NaN, float.NaN);
        [Tooltip("Median player level at 10:00 (runs that reach it).")] public TargetBand levelAt10 = new TargetBand(float.NaN, float.NaN);
    }

    /// <summary>What one minute of a baseline run should look like (the model compares the simulator's medians with it).</summary>
    [Serializable]
    public sealed class MinuteTargets
    {
        [Tooltip("The minute (0 = 0:00–1:00).")] public int minute;
        [Tooltip("Enemies alive as a share of the enemy cap.")] public TargetBand aliveShare = new TargetBand(float.NaN, float.NaN);
        public TargetBand killsPerMinute = new TargetBand(float.NaN, float.NaN);
        [Tooltip("Player level at the end of the minute.")] public TargetBand level = new TargetBand(float.NaN, float.NaN);
        [Tooltip("Hull lost in the minute per 100 hull (before healing): 0.5 = 50 hull points.")] public TargetBand damageTakenShare = new TargetBand(float.NaN, float.NaN);
        public TargetBand goldPerMinute = new TargetBand(float.NaN, float.NaN);
    }

    /// <summary>
    /// The balance simulator's model constants: how the abstract run (1-D distances, shares and hit chances) maps onto the
    /// real game. Calibrated in V5 M2 against the V4 human-like autopilot's real runs (plan/v4-verification/
    /// playtest-m10-m13/measure: Average build, Rook, Observe and Adaptive): per-minute spawns, kills, alive, level,
    /// offense and Pressure, and the median death (Observe 8.0, Adaptive 6.3 min). Change them only with a new
    /// calibration (the calibration test pins them).
    /// </summary>
    [Serializable]
    public sealed class SimCalibration
    {
        [Tooltip("Distance from the ship where ambient enemies appear (m, just beyond the view).")] public float spawnDistance = 24f;
        [Tooltip("Share of a weapon's nominal hits (volley × coverage) that land: shots miss, travel, and fire into empty arcs.")] public float hitEfficiency = 0.22f;
        [Tooltip("Distance at which a melee enemy touches the ship (m).")] public float contactDistance = 2.2f;
        [Tooltip("Inside the engage distance the ship moves away at its speed × this per movement style (Kite, Loose, StopGo, Charge, Beeline; negative = sails in).")]
        public float[] movementRetreat = { 0.85f, 0.7f, 0.4f, -0.3f, 0.6f };
        [Tooltip("Enemies always close at least this share of their speed (a kiting ship circles, it cannot flee forever).")] public float minClosingShare = 0.15f;
        [Tooltip("Outside the engage distance enemies close at their speed × this (the ship sails its own way).")] public float farClosingShare = 0.4f;
        [Tooltip("At most this many melee enemies touch the ship at once.")] public int maxContacts = 5;
        [Tooltip("An ordinary melee enemy in reach lands this share of its attacks on a player who never avoids contact (kiting ships are rarely touched).")] public float contactHitShare = 0.015f;
        [Tooltip("An elite or miniboss in reach lands this share of its rams and bites (before contact avoidance).")] public float eliteContactShare = 0.5f;
        [Tooltip("Share of ranged shots that would hit a ship that never dodges.")] public float rangedAccuracy = 0.006f;
        [Tooltip("Share of bomb-boat blasts that catch a ship that never dodges once the boat reaches it: the main source of big hits.")] public float hazardAccuracy = 0.08f;
        [Tooltip("Share of area-denial attacks (marked zones, kraken slams) that catch a ship that never dodges.")] public float areaAccuracy = 0.02f;
        [Tooltip("Ranged enemies shoot from this share of their preferred range (the kiting ship keeps passing closer).")] public float rangedHoldShare = 0.8f;
        [Tooltip("Share of a volley's shots that can hit at once (spread volleys rarely all land).")] public float volleyHitShare = 0.5f;
        [Tooltip("Share of the Barnacle King's attacks that would land on a ship that never dodges.")] public float bossAccuracy = 0.2f;
        [Tooltip("Seconds per Barnacle King attack cycle (telegraph + execute + recovery).")] public float bossAttackCycleSeconds = 3.9f;
        [Tooltip("XP picked up at pickup greed 0 (the rest is left behind); greed 1 picks up everything.")] public float xpBaseShare = 0.75f;
        [Tooltip("Health barrels picked up at greed 0.")] public float barrelBaseShare = 0.3f;
        [Tooltip("Minutes over which landmarks and chests are found (first, last).")] public Vector2 exploreMinutes = new Vector2(1.5f, 8f);
        [Tooltip("Share of the map's chests a player finds at pickup greed 0 (greed 1 finds all).")] public float chestsFoundBaseShare = 0.5f;
        [Tooltip("Seconds to find the Beacon after the 5:00 hint (OnHint players).")] public float hintSearchSeconds = 45f;
        [Tooltip("Seconds to sail to the Beacon once it is marked or charted.")] public float beaconTravelSeconds = 25f;
    }

    /// <summary>
    /// V5 M2: the balance targets the model and the Monte Carlo simulator check the configs against. Seeded from the
    /// owner's targets (30 Sep 2026) and plan/v5/research/BALANCE_RESEARCH.md.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V5/Balance Targets", fileName = "BalanceTargets")]
    public sealed class BalanceTargetsConfig : V4ConfigAsset
    {
        [Tooltip("V5 M4: the simulator's regular player owns the starters and every weapon, Logbook and item priced at most this much Rum (today's weapon ladder tops out at 100); dearer content is checked by its own tests.")]
        [Min(0)] public int regularPlayerRum = 100;

        [Header("Per personality (simulator)")]
        public List<PersonalityTargets> personalities = new List<PersonalityTargets>();

        [Header("Per minute (the baseline personality)")]
        public List<MinuteTargets> minutes = new List<MinuteTargets>();

        [Header("Pacing (the baseline personality)")]
        [Tooltip("Seconds between level-ups in minute 1.")] public TargetBand levelUpGapMinute1 = new TargetBand(4f, 12f);
        [Tooltip("Average seconds between level-ups over 0:00–10:00.")] public TargetBand levelUpGapTo10 = new TargetBand(10f, 20f);

        [Header("Time to kill for the baseline build (model)")]
        public TargetBand fodderTtkSeconds = new TargetBand(float.NaN, 1f);
        [Tooltip("The scheduled elite beats.")] public TargetBand eliteTtkSeconds = new TargetBand(4f, 8f);
        [Tooltip("Minibosses (the Reef Crab).")] public TargetBand minibossTtkSeconds = new TargetBand(15f, 30f);
        public TargetBand bossTtkSeconds = new TargetBand(45f, 90f);

        [Header("Simulator")]
        [Tooltip("The personality every pacing and TTK target is measured on.")] public string baselinePersonalityId = "steady_captain";
        [Tooltip("Runs per personality in a standard batch.")] public int runsPerPersonality = 200;
        [Tooltip("Simulation step (s).")] public float stepSeconds = 0.25f;
        [Tooltip("A run stops here if the ship is still afloat (minutes).")] public float maxMinutes = 30f;

        [Header("Simulator calibration")]
        public SimCalibration calibration = new SimCalibration();

        public PersonalityTargets For(string id) => personalities.Find(p => p.personalityId == id);

        public override void Validate(ConfigValidationReport report)
        {
            if (runsPerPersonality < 1) report.Error(this, nameof(runsPerPersonality), "must be at least 1");
            report.Positive(this, nameof(stepSeconds), stepSeconds);
            report.Positive(this, nameof(maxMinutes), maxMinutes);
            if (string.IsNullOrWhiteSpace(baselinePersonalityId)) report.Error(this, nameof(baselinePersonalityId), "missing");
            var ids = new HashSet<string>();
            for (var i = 0; i < personalities.Count; i++)
                if (!ids.Add(personalities[i].personalityId ?? string.Empty)) report.Error(this, $"personalities[{i}]", "duplicate personality");
            var minutesSeen = new HashSet<int>();
            for (var i = 0; i < minutes.Count; i++)
                if (!minutesSeen.Add(minutes[i].minute) || minutes[i].minute < 0) report.Error(this, $"minutes[{i}].minute", "duplicate or negative minute");
            foreach (var (name, band) in new[] { ("levelUpGapMinute1", levelUpGapMinute1), ("levelUpGapTo10", levelUpGapTo10), ("fodderTtkSeconds", fodderTtkSeconds), ("eliteTtkSeconds", eliteTtkSeconds), ("bossTtkSeconds", bossTtkSeconds) })
                if (!float.IsNaN(band.min) && !float.IsNaN(band.max) && band.max < band.min) report.Error(this, name, "max below min");
            foreach (var (name, band) in new[] { ("minibossTtkSeconds", minibossTtkSeconds) })
                if (!float.IsNaN(band.min) && !float.IsNaN(band.max) && band.max < band.min) report.Error(this, name, "max below min");
            for (var i = 0; i < minutes.Count; i++)
                foreach (var (name, band) in new[] { ("aliveShare", minutes[i].aliveShare), ("killsPerMinute", minutes[i].killsPerMinute), ("level", minutes[i].level), ("damageTakenShare", minutes[i].damageTakenShare), ("goldPerMinute", minutes[i].goldPerMinute) })
                    if (!float.IsNaN(band.min) && !float.IsNaN(band.max) && band.max < band.min) report.Error(this, $"minutes[{i}].{name}", "max below min");
            for (var i = 0; i < personalities.Count; i++)
                foreach (var (name, band) in new[] { ("deathMinute", personalities[i].deathMinute), ("bossWinRate", personalities[i].bossWinRate), ("levelAt10", personalities[i].levelAt10) })
                    if (!float.IsNaN(band.min) && !float.IsNaN(band.max) && band.max < band.min) report.Error(this, $"personalities[{i}].{name}", "max below min");
            var c = calibration;
            if (c == null) { report.Error(this, nameof(calibration), "missing"); return; }
            if (c.movementRetreat == null || c.movementRetreat.Length != 5) report.Error(this, "calibration.movementRetreat", "needs one value per movement style (5)");
            if (c.maxContacts < 1) report.Error(this, "calibration.maxContacts", "must be at least 1");
            report.Positive(this, "calibration.spawnDistance", c.spawnDistance);
            report.Positive(this, "calibration.bossAttackCycleSeconds", c.bossAttackCycleSeconds);
            if (c.exploreMinutes.y < c.exploreMinutes.x) report.Error(this, "calibration.exploreMinutes", "last before first");
        }
    }
}
