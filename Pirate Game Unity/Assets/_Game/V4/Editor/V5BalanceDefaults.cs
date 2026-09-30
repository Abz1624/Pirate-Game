using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M2: writes the player personalities (plan/v5/PLAYER_PERSONALITIES.md) and the balance targets the model and the
    /// Monte Carlo simulator check against, and adds both to the Saltwind config set.
    /// </summary>
    public static class V5BalanceDefaults
    {
        const string Folder = "Assets/_Game/V4/Data/Balance";
        const string PersonalitiesPath = Folder + "/PlayerPersonalities.asset";
        const string TargetsPath = Folder + "/BalanceTargets.asset";
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        const PersonalityLandmarks All = PersonalityLandmarks.Wreck | PersonalityLandmarks.Cache | PersonalityLandmarks.Buoy | PersonalityLandmarks.Chart;

        static PlayerPersonality P(string id, string name, float share, float dodge, float contact, float engage, PersonalityMovement move, float greed,
            PersonalityChests chests, PersonalityLandmarks landmarks, PersonalityBossTiming boss, float bossMinute, PersonalityCards cards, float reroll,
            PersonalityActive active, PersonalityStop stop, float stopMinutes) =>
            new PlayerPersonality
            {
                personalityId = id, displayName = name, share = share, dodgeSkill = dodge, contactAvoidance = contact, engageDistance = engage,
                movement = move, pickupGreed = greed, chests = chests, landmarks = landmarks, bossTiming = boss, bossMinute = bossMinute,
                cards = cards, rerollUse = reroll, activeUse = active, stop = stop, stopMinutes = stopMinutes,
            };

        /// <summary>The ten personalities of plan/v5/PLAYER_PERSONALITIES.md (shares sum to 100%).</summary>
        public static List<PlayerPersonality> Personalities() => new List<PlayerPersonality>
        {
            P("first_timer", "First-Timer", 0.15f, 0.35f, 0.30f, 6f, PersonalityMovement.StopGo, 0.2f, PersonalityChests.Some, PersonalityLandmarks.Wreck,
                PersonalityBossTiming.Never, 0f, PersonalityCards.Shiny, 0f, PersonalityActive.Forgets, PersonalityStop.Death, 0f),
            P("thumb_sailor", "Thumb Sailor", 0.15f, 0.45f, 0.40f, 8f, PersonalityMovement.StopGo, 0.3f, PersonalityChests.Some, PersonalityLandmarks.Wreck | PersonalityLandmarks.Chart,
                PersonalityBossTiming.OnMarker, 0f, PersonalityCards.Random, 0.1f, PersonalityActive.Sometimes, PersonalityStop.Time, 12f),
            P("casual_cruiser", "Casual Cruiser", 0.20f, 0.55f, 0.50f, 9f, PersonalityMovement.Loose, 0.4f, PersonalityChests.Some,
                PersonalityLandmarks.Wreck | PersonalityLandmarks.Buoy | PersonalityLandmarks.Chart, PersonalityBossTiming.OnMarker, 0f, PersonalityCards.Balanced, 0.2f,
                PersonalityActive.Sometimes, PersonalityStop.Death, 0f),
            P("steady_captain", "Steady Captain", 0.20f, 0.70f, 0.65f, 10f, PersonalityMovement.Kite, 0.5f, PersonalityChests.Some, All,
                PersonalityBossTiming.Minute, 6f, PersonalityCards.Balanced, 0.4f, PersonalityActive.OnCooldown, PersonalityStop.Death, 0f),
            P("glass_cannon", "Glass Cannon", 0.07f, 0.65f, 0.45f, 5f, PersonalityMovement.Charge, 0.4f, PersonalityChests.All, PersonalityLandmarks.Cache,
                PersonalityBossTiming.Minute, 4f, PersonalityCards.Damage, 0.5f, PersonalityActive.OnCooldown, PersonalityStop.Death, 0f),
            P("turtle", "Turtle", 0.06f, 0.60f, 0.70f, 14f, PersonalityMovement.Loose, 0.3f, PersonalityChests.Some, PersonalityLandmarks.Wreck | PersonalityLandmarks.Buoy,
                PersonalityBossTiming.OnMarker, 0f, PersonalityCards.Defense, 0.3f, PersonalityActive.Danger, PersonalityStop.Death, 0f),
            P("treasure_hound", "Treasure Hound", 0.06f, 0.55f, 0.50f, 9f, PersonalityMovement.Loose, 0.9f, PersonalityChests.All, All,
                PersonalityBossTiming.OnMarker, 0f, PersonalityCards.Economy, 0.3f, PersonalityActive.OnCooldown, PersonalityStop.Death, 0f),
            P("build_planner", "Build Planner", 0.05f, 0.75f, 0.70f, 10f, PersonalityMovement.Kite, 0.5f, PersonalityChests.Evolution, PersonalityLandmarks.Cache | PersonalityLandmarks.Chart,
                PersonalityBossTiming.Minute, 5f, PersonalityCards.Focused, 0.9f, PersonalityActive.OnCooldown, PersonalityStop.Death, 0f),
            P("sea_legend", "Sea Legend", 0.04f, 0.90f, 0.90f, 11f, PersonalityMovement.Kite, 0.6f, PersonalityChests.Evolution, All,
                PersonalityBossTiming.Minute, 4f, PersonalityCards.Focused, 1.0f, PersonalityActive.Danger, PersonalityStop.Death, 0f),
            P("speedrunner", "Speedrunner", 0.02f, 0.80f, 0.75f, 12f, PersonalityMovement.Beeline, 0.1f, PersonalityChests.None, PersonalityLandmarks.Chart,
                PersonalityBossTiming.Minute, 3f, PersonalityCards.Damage, 0.6f, PersonalityActive.OnCooldown, PersonalityStop.AfterBoss, 0f),
        };

        static PersonalityTargets PT(string id, float deathMin, float deathMax, float bossMin, float bossMax, float levelMin = float.NaN, float levelMax = float.NaN) =>
            new PersonalityTargets { personalityId = id, deathMinute = new TargetBand(deathMin, deathMax), bossWinRate = new TargetBand(bossMin, bossMax), levelAt10 = new TargetBand(levelMin, levelMax) };

        static MinuteTargets MT(int minute, float aliveMin, float aliveMax, float killsMin, float killsMax, float levelMin, float levelMax, float damageMax) =>
            new MinuteTargets
            {
                minute = minute, aliveShare = new TargetBand(aliveMin, aliveMax), killsPerMinute = new TargetBand(killsMin, killsMax),
                level = new TargetBand(levelMin, levelMax), damageTakenShare = new TargetBand(float.NaN, damageMax), goldPerMinute = new TargetBand(float.NaN, float.NaN),
            };

        /// <summary>
        /// The M2 targets (proposed 30 Sep 2026 from plan/v5/research/BALANCE_RESEARCH.md and the personalities' intent; the
        /// owner confirms them at the M2 playtest). End minute = the median minute a run ends (sunk, or quit by its stop rule).
        /// </summary>
        public static void FillTargets(BalanceTargetsConfig t)
        {
            t.personalities = new List<PersonalityTargets>
            {
                PT("first_timer", 5.5f, 9f, 0f, 0.15f),
                PT("thumb_sailor", 7f, 12f, 0.1f, 0.6f),
                PT("casual_cruiser", 8f, 12f, 0.3f, 0.7f),
                PT("steady_captain", 11f, 15f, 0.6f, 0.85f, 21f, 35f), // level at 10:00 was 30–46 before the demo-baseline retune (XP ×0.5)
                // Revised after the first calibrated run (were 0.45–0.85; 10–14.5; 9–13.5 and 0.3–0.75): the Turtle (dodge 0.6)
                // and the Treasure Hound (0.55) dodge less than the baseline (0.7), so they sink before it; the Glass Cannon
                // summons the boss at 4:00 with a damage build, and beating it early is what it plays for.
                PT("glass_cannon", 8f, 13f, 0.6f, 1f),
                PT("turtle", 8f, 13f, 0.3f, 0.7f),
                PT("treasure_hound", 7.5f, 12f, 0.2f, 0.6f),
                PT("build_planner", 13f, 19f, 0.8f, 1f),
                PT("sea_legend", 17f, float.NaN, 0.95f, 1f),
                PT("speedrunner", 3f, 8f, 0.5f, 1f),
            };
            // Alive share of the enemy cap follows the M1 floors (a jagged crowd); kills and levels an on-track run should
            // reach; damage taken per minute per 100 hull stays survivable early.
            t.minutes = new List<MinuteTargets>
            {
                MT(0, 0.03f, 0.30f, 8f, 45f, 3f, 9f, 0.35f),
                // Revised after the first model run (kills were 18–70): the M1 flow's minute-1 floor keeps ~15 enemies up, and
                // an on-track build sinks them as they come.
                MT(1, 0.10f, 0.45f, 18f, 90f, 6f, 13f, 0.45f),
                MT(2, 0.15f, 0.55f, 25f, 90f, 9f, 17f, 0.6f),
                MT(3, 0.12f, 0.55f, 25f, 100f, 12f, 20f, 0.6f),
                MT(4, 0.20f, 0.65f, 35f, 120f, 14f, 23f, 0.7f),
                // Minute 5 holds the 5:30 horde (alive was ≤ 0.65); minute 6 is the baseline's boss fight, when its hits go to
                // the Barnacle King (kills were ≥ 45).
                MT(5, 0.18f, 0.70f, 35f, 130f, 16f, 26f, 0.8f),
                // Levels from 6:00 follow the demo-baseline retune (XP ×0.5, 30 Sep 2026); before: 18–29, 21–32, 23–35,
                // 25–38, 28–42, 30–45, 32–48.
                MT(6, 0.25f, 0.75f, 30f, 140f, 15f, 26f, 0.9f),
                MT(7, 0.30f, 0.85f, 50f, 160f, 17f, 29f, 1f),
                MT(8, 0.22f, 0.80f, 45f, 160f, 18f, 31f, 1f),
                MT(9, 0.35f, 0.90f, 55f, 180f, 20f, 33f, 1.1f),
                // Overtime spawns one enemy every 1.4 s at first, down to 0.35 s (kills were ≥ 50).
                MT(10, 0.25f, 1.0f, 30f, 200f, 21f, 35f, float.NaN),
                MT(11, 0.25f, 1.0f, 30f, 220f, 22f, 37f, float.NaN),
                MT(12, 0.25f, 1.0f, 30f, 240f, 23f, 39f, float.NaN),
            };
            // The demo-baseline retune (XP ×0.5), ±20% around the chosen 15 s and 25 s (owner, 1 Oct): were 4–12 and 13–20.
            t.levelUpGapMinute1 = new TargetBand(12f, 18f);
            t.levelUpGapTo10 = new TargetBand(20f, 30f);
            t.fodderTtkSeconds = new TargetBand(float.NaN, 1f);
            t.eliteTtkSeconds = new TargetBand(4f, 8f);
            t.minibossTtkSeconds = new TargetBand(15f, 30f);
            t.bossTtkSeconds = new TargetBand(45f, 90f);
            t.baselinePersonalityId = "steady_captain";
            t.runsPerPersonality = 200;
            t.stepSeconds = 0.25f;
            t.maxMinutes = 30f;
            t.calibration = new SimCalibration(); // the calibrated constants are the schema defaults (M2 calibration)
        }

        const string BalanceVersion = "saltwind-v5-m2";
        const float BossBaseHp = 1400f;

        /// <summary>
        /// The director's on-track offense growth measured by the simulator after the retune (SimBatch.OnTrackGrowth, the
        /// Steady Captain in Observe mode, 60 runs, seed 11, after the review fixes; 7–8 min smoothed, 15:00 extrapolated), smoothed: × the run's starting offense at each minute.
        /// </summary>
        static readonly (float minute, float growth)[] OnTrackGrowth =
        {
            (0f, 1f), (1f, 5.4f), (2f, 8.3f), (3f, 11f), (4f, 14.7f), (5f, 17.7f), (6f, 23.3f), (7f, 29f), (8f, 35f), (9f, 40.8f), (10f, 48.8f), (12f, 57.4f), (15f, 80f),
        };

        /// <summary>
        /// V5 M2 G2.1: the retune the balance model and simulator asked for (plan/v5/phase-02-model/PLAN.md). Phases are
        /// found by their role (ambient bands by start time, the miniboss arena, the scheduled elite beats, the boss), not
        /// by id. Every value changed here has its pre-M2 value in the v3-parity preset.
        /// </summary>
        [MenuItem("RoguePirates/V5/Write M2 retune")]
        public static string ApplyRetune()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found: " + SetPath;
            var progression = set.All<ProgressionConfig>().FirstOrDefault();
            var curve = set.All<DifficultyCurveConfig>().FirstOrDefault();
            var timeline = set.All<WaveTimelineConfig>().FirstOrDefault();
            var director = set.All<DirectorConfig>().FirstOrDefault();
            if (progression == null || curve == null || timeline == null || director == null) return "missing configs";

            // XP: a level-up every ~8 s in minute 1, every ~17 s on average to 10:00 (was 20 s and ~35 s).
            Undo.RecordObject(progression, "V5 M2 retune");
            progression.levelZeroToOneXp = 3;
            progression.xpCurveOffset = 2f;
            progression.xpCurveMultiplier = 0.7;
            progression.xpCurveExponent = 1.5;
            progression.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(progression);

            // Enemies: gentler growth after 2:00 (HP and damage), per-minute damage, and a slower overtime ramp.
            Undo.RecordObject(curve, "V5 M2 retune");
            curve.ordinaryHp.c1 = 0.08f; curve.ordinaryHp.c2 = 0.01f;
            curve.ordinaryDamage.c1 = 0.06f; curve.ordinaryDamage.c2 = 0.01f;
            curve.enemyDamagePerMinute.c1 = 0.025f;
            curve.overtimeHpMultiplierBase = 1.25f;
            curve.overtimeHpMultiplierExponentPerMinute = 0.25f;
            curve.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(curve);

            Undo.RecordObject(timeline, "V5 M2 retune");
            // The last three ambient bands spawn a little slower so an on-track build keeps up (the crowd still fills).
            var bands = timeline.phases.Where(p => p.ratePerSecond > 0f && !p.isOvertime && p.trigger.kind == PhaseTriggerKind.Time).OrderBy(p => p.trigger.timeSeconds).ToList();
            var rates = new[] { 1.3f, 1.5f, 2.0f };
            for (var i = 0; i < rates.Length && bands.Count - rates.Length + i >= 0; i++) bands[bands.Count - rates.Length + i].ratePerSecond = rates[i];
            timeline.overtime.spawnStatRampEnd = 1.5f;
            // The M1 flow around the 5:30 horde: a slightly lower floor in minute 5 and a smaller line horde.
            if (timeline.flow != null)
            {
                foreach (var w in timeline.flow.waves) if (Mathf.Approximately(w.startSeconds, 300f)) w.minAliveShare = 0.26f;
                foreach (var h in timeline.flow.hordes) if (Mathf.Approximately(h.timeSeconds, 330f)) h.count = 14;
            }
            // The miniboss arena (a focus phase that is not a scheduled elite): a 15–30 s fight instead of ~50 s.
            foreach (var p in timeline.phases.Where(p => p.focus != null && p.focus.enabled && p.focus.scheduledEliteIndex < 0))
            {
                p.focus.hpMultiplier = 3.8f;
                p.focus.damageMultiplier = 0.8f;
            }
            timeline.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(timeline);
            // Scheduled elites: 4–8 s to sink instead of ~1 s; the Barnacle King a 45–90 s fight.
            foreach (var p in timeline.phases.Where(p => p.focus != null && p.focus.enabled && p.focus.scheduledEliteIndex >= 0 && p.spawner != null))
                SetSpawnerHp(p.spawner, 6f);
            // The boss spawner is not elite, so the game ignores its elite HP multiplier (review M2 #1): the Barnacle King's
            // extra HP goes on its archetype's base HP, which every spawn reads (was 800).
            foreach (var p in timeline.phases.Where(p => p.isBoss && p.spawner != null))
            {
                SetSpawnerHp(p.spawner, 1f);
                var boss = p.spawner.anchor;
                if (boss == null) continue;
                Undo.RecordObject(boss, "V5 M2 retune");
                boss.stats.baseHP = BossBaseHp;
                boss.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(boss);
            }

            Undo.RecordObject(director, "V5 M2 retune");
            var keys = OnTrackGrowth.Select(k => new Keyframe(k.minute, k.growth)).ToArray();
            director.onTrackGrowth = new AnimationCurve(keys);
            for (var i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(director.onTrackGrowth, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(director.onTrackGrowth, i, AnimationUtility.TangentMode.Linear);
            }
            director.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
            return $"ok: {bands.Count} bands, balance version {BalanceVersion}";
        }

        static void SetSpawnerHp(EnemySpawnerConfig spawner, float hp)
        {
            Undo.RecordObject(spawner, "V5 M2 retune");
            spawner.elite.hpMultiplier = hp;
            spawner.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(spawner);
        }

        [MenuItem("RoguePirates/V5/Write M2 personalities and balance targets")]
        public static string Apply()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/V4/Data", "Balance");
            var personalities = AssetDatabase.LoadAssetAtPath<PlayerPersonalityConfig>(PersonalitiesPath);
            if (personalities == null)
            {
                personalities = ScriptableObject.CreateInstance<PlayerPersonalityConfig>();
                AssetDatabase.CreateAsset(personalities, PersonalitiesPath);
            }
            personalities.configId = "personalities.saltwind";
            personalities.personalities = Personalities();
            EditorUtility.SetDirty(personalities);

            var targets = AssetDatabase.LoadAssetAtPath<BalanceTargetsConfig>(TargetsPath);
            if (targets == null)
            {
                targets = ScriptableObject.CreateInstance<BalanceTargetsConfig>();
                AssetDatabase.CreateAsset(targets, TargetsPath);
            }
            targets.configId = "balance_targets.saltwind";
            FillTargets(targets);
            EditorUtility.SetDirty(targets);

            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found: " + SetPath;
            if (!set.configs.Contains(personalities)) set.configs.Add(personalities);
            if (!set.configs.Contains(targets)) set.configs.Add(targets);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return $"ok: {personalities.personalities.Count} personalities, {targets.personalities.Count} personality targets, {targets.minutes.Count} minute targets";
        }
    }
}
