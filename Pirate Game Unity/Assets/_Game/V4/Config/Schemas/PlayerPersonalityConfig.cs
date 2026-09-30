using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    public enum PersonalityMovement
    {
        [Tooltip("Circles around the crowd.")] Kite,
        [Tooltip("Wide, drifting circles.")] Loose,
        [Tooltip("Short moves, frequent stops.")] StopGo,
        [Tooltip("Sails into the crowd.")] Charge,
        [Tooltip("Straight lines to objectives.")] Beeline
    }

    public enum PersonalityChests { All, Some, Evolution, None }

    [Flags]
    public enum PersonalityLandmarks
    {
        None = 0,
        [Tooltip("Repair Wreck.")] Wreck = 1,
        [Tooltip("Powder Cache.")] Cache = 2,
        [Tooltip("Signal Buoy.")] Buoy = 4,
        [Tooltip("Treasure Chart.")] Chart = 8
    }

    public enum PersonalityBossTiming
    {
        [Tooltip("At bossMinute:00, if the Beacon is found.")] Minute,
        [Tooltip("At the 5:00 Beacon hint.")] OnHint,
        [Tooltip("At the 7:00 Beacon marker.")] OnMarker,
        Never
    }

    public enum PersonalityCards
    {
        Random,
        [Tooltip("New weapons and the highest rarity.")] Shiny,
        [Tooltip("Spreads levels; takes hull when below half.")] Balanced,
        Damage,
        [Tooltip("Hull, armour, repair.")] Defense,
        [Tooltip("Maxes one weapon and hunts its evolution item.")] Focused,
        [Tooltip("Gold, XP and pickup-radius Logbooks first.")] Economy
    }

    public enum PersonalityActive
    {
        OnCooldown,
        [Tooltip("Saved for hull below 40% or a boss attack.")] Danger,
        [Tooltip("50% of the time it is ready.")] Sometimes,
        [Tooltip("10% of the time.")] Forgets
    }

    public enum PersonalityStop
    {
        [Tooltip("Plays until the ship sinks.")] Death,
        [Tooltip("Quits stopMinutes after beating the boss.")] AfterBoss,
        [Tooltip("Quits at stopMinutes:00.")] Time
    }

    /// <summary>
    /// V5 M2: one player personality the balance simulator plays runs as (plan/v5/PLAYER_PERSONALITIES.md). The same
    /// fields seed the AI captain profile in M16.
    /// </summary>
    [Serializable]
    public sealed class PlayerPersonality
    {
        [ConfigIdentity] public string personalityId = "steady_captain";
        public string displayName = "Steady Captain";
        [Tooltip("Share of the player base (weights the 'most players' views; all shares sum to 1).")]
        [Range(0f, 1f)] public float share = 0.2f;
        [Tooltip("Share of avoidable damage (telegraphed attacks, shots, hazards, boss attacks) avoided.")]
        [Range(0f, 1f)] public float dodgeSkill = 0.7f;
        [Tooltip("Share of melee contact hits (rammers, sharks, boarding) avoided.")]
        [Range(0f, 1f)] public float contactAvoidance = 0.65f;
        [Tooltip("Preferred distance to the nearest enemy while fighting (m).")] public float engageDistance = 10f;
        public PersonalityMovement movement = PersonalityMovement.Kite;
        [Tooltip("0 = only what is on its path; 1 = everything within 30 m, even through enemies.")]
        [Range(0f, 1f)] public float pickupGreed = 0.5f;
        public PersonalityChests chests = PersonalityChests.Some;
        public PersonalityLandmarks landmarks = PersonalityLandmarks.Wreck | PersonalityLandmarks.Cache | PersonalityLandmarks.Buoy | PersonalityLandmarks.Chart;
        public PersonalityBossTiming bossTiming = PersonalityBossTiming.Minute;
        [Tooltip("Boss timing Minute: the minute it summons the Barnacle King.")] public float bossMinute = 6f;
        public PersonalityCards cards = PersonalityCards.Balanced;
        [Tooltip("Chance it spends a refresh when no card fits its policy.")]
        [Range(0f, 1f)] public float rerollUse = 0.4f;
        public PersonalityActive activeUse = PersonalityActive.OnCooldown;
        public PersonalityStop stop = PersonalityStop.Death;
        [Tooltip("Stop AfterBoss / Time: minutes.")] public float stopMinutes;
    }

    /// <summary>V5 M2: the player personalities the balance simulator uses (plan/v5/PLAYER_PERSONALITIES.md).</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V5/Player Personalities", fileName = "PlayerPersonalities")]
    public sealed class PlayerPersonalityConfig : V4ConfigAsset
    {
        public List<PlayerPersonality> personalities = new List<PlayerPersonality>();

        public PlayerPersonality Find(string id) => personalities.Find(p => p.personalityId == id);

        public override void Validate(ConfigValidationReport report)
        {
            var ids = new HashSet<string>();
            var total = 0f;
            for (var i = 0; i < personalities.Count; i++)
            {
                var p = personalities[i];
                var at = $"personalities[{i}]";
                if (string.IsNullOrWhiteSpace(p.personalityId)) report.Error(this, at + ".personalityId", "missing id");
                else if (!ids.Add(p.personalityId)) report.Error(this, at + ".personalityId", $"duplicate id '{p.personalityId}'");
                report.Positive(this, at + ".engageDistance", p.engageDistance);
                if (p.bossTiming == PersonalityBossTiming.Minute && p.bossMinute <= 0f) report.Error(this, at + ".bossMinute", "must be above 0 for Minute timing");
                if (p.stop != PersonalityStop.Death && p.stopMinutes < 0f) report.Error(this, at + ".stopMinutes", "must not be negative");
                total += p.share;
            }
            if (personalities.Count > 0 && Mathf.Abs(total - 1f) > 0.001f) report.Error(this, "personalities", $"shares sum to {total:0.###}, not 1");
        }
    }
}
