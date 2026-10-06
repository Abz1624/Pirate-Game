using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Difficulty
{
    /// <summary>
    /// What the adaptive levers ask the director (M11, gameplay doc §5.3). Every value is neutral (1 or 0) unless the
    /// director is in Adaptive mode and the lever is on, so Off and Observe runs are identical to a run without a director.
    /// Only ambient spawns read the levers: boss and encounter spawns, scripted beats and their times, and per-kill rewards
    /// never adapt (tested by source scan and behaviour).
    /// </summary>
    public interface IDifficultyLevers
    {
        /// <summary>True in Adaptive mode (the levers can move).</summary>
        bool Adaptive { get; }
        /// <summary>Ambient spawn rate × this (clamped band, e.g. 0.85–1.35).</summary>
        float ThreatRateMultiplier { get; }
        /// <summary>Seconds added to the run clock when checking tier unlocks (tougher tiers sooner; 0 unless Pressure &gt; 1).</summary>
        float TierLeadSeconds { get; }
        /// <summary>A spawner's pressureWeight curve at the current Pressure (1 when not adaptive or the lever is off).</summary>
        float SpawnerWeight(AnimationCurve pressureWeight);
        /// <summary>Pick-weight multiplier for an enemy with these role tags (1 when not adaptive or the lever is off).</summary>
        float CompositionWeight(IReadOnlyList<string> roleTags);
        /// <summary>True when <see cref="CompositionWeight"/> can differ from 1 (the pick then uses weights).</summary>
        bool CompositionActive { get; }
        /// <summary>Rolls the elite lever on the director's own seeded stream; false when not adaptive (no draw is made).</summary>
        bool RollElite();
        /// <summary>Playtest (3 Oct): the build's damage per second against one target (every weapon hitting), for the
        /// time-to-kill floors of bosses, minibosses and elites. 0 when unknown.</summary>
        float PlayerDps => 0f;
        /// <summary>HP multiplier for an ambient spawn made while the enemy cap blocked a spawn recently (1 otherwise).</summary>
        float CapHpMultiplier { get; }
        /// <summary>Ambient spawns' HP and damage × these (toughness lever; 1 unless Adaptive).</summary>
        float AmbientHpMultiplier { get; }
        float AmbientDamageMultiplier { get; }
        /// <summary>Minibosses and elite beats (focus phases): HP and damage × these (1 unless Adaptive). Never the boss.</summary>
        float FocusHpMultiplier { get; }
        float FocusDamageMultiplier { get; }
        /// <summary>Adaptive elites pay elite rewards.</summary>
        bool AdaptiveElitesPayEliteRewards { get; }
        /// <summary>Pressure for "Pressure at least" timeline conditions: Pressure in Adaptive mode, else 1 (as before M11).</summary>
        float ConditionPressure { get; }
        /// <summary>The config the elite lever reads (affix and multipliers).</summary>
        DirectorConfig Config { get; }
    }

    /// <summary>Pure lever formulas (tested at the band edges and for every switch).</summary>
    public static class LeverMath
    {
        /// <summary>How far Pressure is above 1 toward the band top: 0 at 1 or below, 1 at <paramref name="max"/>.</summary>
        public static float Above(float pressure, float max) => max > 1f ? Mathf.Clamp01((pressure - 1f) / (max - 1f)) : 0f;

        public static float ThreatRate(float pressure, DirectorConfig d) =>
            Mathf.Clamp(1f + d.threatRateGain * (pressure - 1f), d.threatRateMin, d.threatRateMax);

        public static float EliteChance(float pressure, float max, DirectorConfig d) => Mathf.Clamp01(d.eliteChanceAtMax * Above(pressure, max));

        public static float TierLead(float pressure, float max, DirectorConfig d) => Mathf.Max(0f, d.tierLeadSecondsAtMax) * Above(pressure, max);

        public static float CapHp(float pressure, float max, DirectorConfig d) => 1f + Mathf.Max(0f, d.capHpMultiplierAtMax - 1f) * Above(pressure, max);

        /// <summary>1 at or below Pressure 1, <paramref name="atMax"/> at the top of the band (never below 1).</summary>
        public static float Scale(float pressure, float max, float atMax) => 1f + Mathf.Max(0f, atMax - 1f) * Above(pressure, max);

        /// <summary>1 + Σ matching rules of maxBoost × share of the build in the rule's range × Above.</summary>
        public static float Composition(IReadOnlyList<string> roleTags, float closeShare, float above, DirectorConfig d)
        {
            if (roleTags == null || d.compositionRules == null || above <= 0f) return 1f;
            var weight = 1f;
            foreach (var rule in d.compositionRules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.enemyTag)) continue;
                var match = false;
                for (var i = 0; i < roleTags.Count; i++) if (roleTags[i] == rule.enemyTag) { match = true; break; }
                if (!match) continue;
                var share = rule.favouredBy == BuildRange.CloseRange ? closeShare : 1f - closeShare;
                weight += Mathf.Max(0f, rule.maxBoost) * Mathf.Clamp01(share) * above;
            }
            return weight;
        }
    }

    /// <summary>
    /// Enemy roles for the composition lever: the archetype's own tags plus roles read from its combat block — "ranged"
    /// (shoots or lays hazards from range) and "swarm" (closes in to ram, bite or blow up). No enemy ids.
    /// </summary>
    public static class EnemyRoles
    {
        public static string[] Of(string[] tags, ArchetypeCombat combat)
        {
            var roles = new List<string>();
            if (tags != null) roles.AddRange(tags);
            if (combat != null)
            {
                switch (combat.attackPattern)
                {
                    case ConfigEnums.EnemyAttackPattern.SingleShot:
                    case ConfigEnums.EnemyAttackPattern.BurstVolley:
                    case ConfigEnums.EnemyAttackPattern.SpreadVolley:
                    case ConfigEnums.EnemyAttackPattern.AreaDenial:
                        if (!roles.Contains("ranged")) roles.Add("ranged");
                        break;
                    case ConfigEnums.EnemyAttackPattern.Contact:
                    case ConfigEnums.EnemyAttackPattern.SuicideExplosion:
                    case ConfigEnums.EnemyAttackPattern.TelegraphedCharge:
                        if (!roles.Contains("swarm")) roles.Add("swarm");
                        break;
                }
            }
            return roles.ToArray();
        }
    }
}
