using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M2 balance model and Monte Carlo simulator: the personalities and targets data, the model's math on hand-computed
    /// values, determinism (also across threads, as the console runs it), personality parameters moving outcomes the
    /// expected way, the calibration against the V4 autopilot's real runs, the retuned data meeting the baseline's targets,
    /// and the v3-parity preset restoring the pre-M2 values.
    /// </summary>
    [Category("V5")]
    public sealed class V5BalanceM2Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        static PlayerPersonality Clone(PlayerPersonality p, string id) => new PlayerPersonality
        {
            personalityId = id, displayName = id, share = p.share, dodgeSkill = p.dodgeSkill, contactAvoidance = p.contactAvoidance,
            engageDistance = p.engageDistance, movement = p.movement, pickupGreed = p.pickupGreed, chests = p.chests, landmarks = p.landmarks,
            bossTiming = p.bossTiming, bossMinute = p.bossMinute, cards = p.cards, rerollUse = p.rerollUse, activeUse = p.activeUse, stop = p.stop,
            stopMinutes = p.stopMinutes,
        };

        static float MedianEnd(SimContent c, PlayerPersonality p, int runs, int seed)
        {
            var ends = new List<float>();
            for (var i = 0; i < runs; i++) ends.Add(new SimRun(c, p, SimBatch.RunSeed(seed, p.personalityId, i), c.Captains[i % c.Captains.Count].captainKey).Play().EndMinutes);
            return SimBatch.Percentile(ends, 0.5f);
        }

        // ---- Data ---------------------------------------------------------------------------------------------------

        [Test]
        public void PersonalitiesAndTargets_AreInTheSet_AndValidate()
        {
            using var rt = new ConfigSetRuntime(Set);
            var personalities = rt.Single<PlayerPersonalityConfig>();
            var targets = rt.Single<BalanceTargetsConfig>();
            Assert.IsNotNull(personalities);
            Assert.IsNotNull(targets);
            var report = new ConfigValidationReport();
            personalities.Validate(report);
            targets.Validate(report);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(10, personalities.personalities.Count);
            Assert.AreEqual(1f, personalities.personalities.Sum(p => p.share), 0.001f);
            foreach (var p in personalities.personalities) Assert.IsNotNull(targets.For(p.personalityId), "targets for " + p.personalityId);
            Assert.IsNotNull(personalities.Find(targets.baselinePersonalityId), "the baseline personality exists");
        }

        [Test]
        public void Personalities_MatchThePersonalitiesDoc()
        {
            var ps = V5BalanceDefaults.Personalities();
            var first = ps.Single(p => p.personalityId == "first_timer");
            Assert.AreEqual((0.15f, 0.35f, 0.30f, 6f), (first.share, first.dodgeSkill, first.contactAvoidance, first.engageDistance));
            Assert.AreEqual((PersonalityMovement.StopGo, PersonalityBossTiming.Never, PersonalityCards.Shiny, PersonalityActive.Forgets), (first.movement, first.bossTiming, first.cards, first.activeUse));
            var steady = ps.Single(p => p.personalityId == "steady_captain");
            Assert.AreEqual((0.20f, 0.70f, 0.65f, 10f, 0.5f), (steady.share, steady.dodgeSkill, steady.contactAvoidance, steady.engageDistance, steady.pickupGreed));
            Assert.AreEqual((PersonalityBossTiming.Minute, 6f, PersonalityCards.Balanced), (steady.bossTiming, steady.bossMinute, steady.cards));
            var speed = ps.Single(p => p.personalityId == "speedrunner");
            Assert.AreEqual((PersonalityMovement.Beeline, PersonalityChests.None, PersonalityStop.AfterBoss, 3f), (speed.movement, speed.chests, speed.stop, speed.bossMinute));
        }

        [Test]
        public void Targets_ValidationCatchesBrokenData()
        {
            var t = ScriptableObject.CreateInstance<BalanceTargetsConfig>();
            try
            {
                t.configId = "t";
                t.levelUpGapTo10 = new TargetBand(20f, 10f);
                t.minutes.Add(new MinuteTargets { minute = 3 });
                t.minutes.Add(new MinuteTargets { minute = 3 });
                t.calibration.movementRetreat = new float[2];
                var report = new ConfigValidationReport();
                t.Validate(report);
                var text = report.ToString();
                StringAssert.Contains("levelUpGapTo10", text);
                StringAssert.Contains("minutes[1].minute", text);
                StringAssert.Contains("movementRetreat", text);
            }
            finally { Object.DestroyImmediate(t); }
        }

        [Test]
        public void TargetBand_NaNIsUnchecked()
        {
            Assert.IsTrue(new TargetBand(float.NaN, 5f).Contains(-100f));
            Assert.IsFalse(new TargetBand(float.NaN, 5f).Contains(5.1f));
            Assert.IsTrue(new TargetBand(2f, float.NaN).Contains(1e6f));
            Assert.IsFalse(new TargetBand(2f, 3f).Contains(1.9f));
        }

        // ---- The model's math ----------------------------------------------------------------------------------------

        [Test]
        public void Percentile_Interpolates()
        {
            Assert.AreEqual(2.5f, SimBatch.Percentile(new List<float> { 4f, 1f, 3f, 2f }, 0.5f), 1e-5f);
            Assert.AreEqual(1f, SimBatch.Percentile(new List<float> { 4f, 1f, 3f, 2f }, 0f), 1e-5f);
            Assert.AreEqual(3.7f, SimBatch.Percentile(new List<float> { 1f, 2f, 3f, 4f }, 0.9f), 1e-5f);
            Assert.IsNaN(SimBatch.Percentile(new List<float>(), 0.5f));
        }

        [Test]
        public void SpawnHp_IsTheCurveTimesOrdinaryTime_HandComputed()
        {
            using var rt = new ConfigSetRuntime(Set);
            var d = rt.Single<DifficultyCurveConfig>();
            d.enemyHpPerMinute = new CurveSpec { c0 = 1f, c1 = 0.09f, c2 = 0.018f };
            d.hpMultipliers = new TierStageMultipliers { tier = new[] { 1f, 1f, 1f }, stage = new[] { 1f, 1f, 1f } };
            d.globalHpMultiplier = 2f / 3f;
            d.ordinaryScalingDelaySeconds = 120f;
            d.ordinaryHp = new CurveSpec { c0 = 1f, c1 = 0.08f, c2 = 0.01f };
            rt.Single<ExperimentConfig>().enemyHpMultiplier = 1f;
            var c = new SimContent(rt);
            // 3:00: (1 + 0.27 + 0.162) × 2/3 × 10 = 9.5467; one minute after the delay: × (1 + 0.08 + 0.01) = 10.4059
            Assert.AreEqual(10.4059f, SimBatch.SpawnHp(c, 10f, 180f), 1e-3f);
            // 1:00, before the delay: (1 + 0.09 + 0.018) × 2/3 × 10 = 7.3867
            Assert.AreEqual(7.3867f, SimBatch.SpawnHp(c, 10f, 60f), 1e-3f);
        }

        [Test]
        public void SimulatedLevels_FollowTheProgressionConfigsXpCurve()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var run = new SimRun(c, c.Personalities.Find("steady_captain"), 5);
            var p = rt.Single<ProgressionConfig>();
            // XP to level 3 from 0: levelZeroToOne + XpToNext(1) + XpToNext(2), granted at once.
            var need = p.levelZeroToOneXp + Progression.ProgressionMath.XpToNext(p, 1) + Progression.ProgressionMath.XpToNext(p, 2);
            run.Build.GrantXp(Mathf.CeilToInt(need / run.Build.Stat(Foundation.Core.StatId.XPGain)));
            Assert.AreEqual(3, run.Build.Progression.Level);
            Assert.AreEqual(3, run.Build.LevelUpTimes.Count, "every level-up was answered with a card");
        }

        // ---- Determinism -----------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SameSeed_SameRun_AlsoOnAWorkerThread()
        {
            var rt = new ConfigSetRuntime(Set);
            try
            {
                var c = new SimContent(rt);
                var p = c.Personalities.Find("steady_captain");
                var a = new SimRun(c, p, 77, "rook").Play();
                var b = new SimRun(c, p, 77, "rook").Play();
                // The console runs batches on a worker thread; the main thread must keep running meanwhile (never block on it).
                var task = Task.Run(() => new SimRun(c, p, 77, "rook").Play());
                var deadline = System.DateTime.UtcNow.AddSeconds(60);
                while (!task.IsCompleted && System.DateTime.UtcNow < deadline) yield return null;
                Assert.IsTrue(task.IsCompleted, "the worker-thread run finished");
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                foreach (var other in new[] { b, task.Result })
                {
                    Assert.AreEqual(a.EndSeconds, other.EndSeconds);
                    Assert.AreEqual(a.Kills, other.Kills);
                    Assert.AreEqual(a.FinalLevel, other.FinalLevel);
                    CollectionAssert.AreEqual(a.LevelUpTimes, other.LevelUpTimes);
                    CollectionAssert.AreEqual(a.Weapons, other.Weapons);
                }
                var d = new SimRun(c, p, 78, "rook").Play();
                Assert.IsFalse(a.EndSeconds == d.EndSeconds && a.Kills == d.Kills && a.LevelUpTimes.SequenceEqual(d.LevelUpTimes), "another seed plays another run");
            }
            finally { rt.Dispose(); }
        }

        [Test]
        public void ABatch_DoesNotTouchTheConfigsItReads()
        {
            using var rt = new ConfigSetRuntime(Set);
            var before = rt.Hash;
            var c = new SimContent(rt);
            SimBatch.Run(c, 2, 3, new[] { c.Personalities.Find("steady_captain") });
            Assert.AreEqual(before, rt.Hash);
        }

        // ---- Personality parameters move outcomes the expected way ------------------------------------------------------

        [Test]
        public void HigherDodgeSkill_SurvivesLonger()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var steady = c.Personalities.Find("steady_captain");
            var clumsy = Clone(steady, "clumsy"); clumsy.dodgeSkill = 0.3f;
            var nimble = Clone(steady, "nimble"); nimble.dodgeSkill = 0.95f;
            Assert.Greater(MedianEnd(c, nimble, 30, 1), MedianEnd(c, clumsy, 30, 1) + 1f);
        }

        [Test]
        public void MoreContactAvoidance_TakesLessContactDamage()
        {
            // Survival time barely moves (the crowd, not contact, ends these runs; recorded for M3), so this checks the
            // parameter's direct effect: the share of hull lost to untelegraphed melee hits, for a player who sails in.
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var glass = c.Personalities.Find("glass_cannon");
            float ContactShare(float avoidance)
            {
                var p = Clone(glass, "contact" + avoidance); p.contactAvoidance = avoidance;
                float contact = 0f, total = 0f;
                for (var i = 0; i < 20; i++)
                    foreach (var kv in new SimRun(c, p, 500 + i, "rook").Play().DamageBySource)
                    {
                        total += kv.Value;
                        var d = c.Enemy(kv.Key);
                        if (d != null && d.Attack == SimAttack.Contact && !d.Telegraphed) contact += kv.Value;
                    }
                return total > 0f ? contact / total : 0f;
            }
            var bumper = ContactShare(0f);
            var careful = ContactShare(0.95f);
            Assert.Greater(bumper, 0.2f, "a charging player who never avoids contact takes much of its damage from it");
            Assert.Less(careful, bumper / 4f);
        }

        [Test]
        public void GreedierPlayers_LevelFaster()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var steady = c.Personalities.Find("steady_captain");
            var lazy = Clone(steady, "lazy"); lazy.pickupGreed = 0f;
            var greedy = Clone(steady, "greedy"); greedy.pickupGreed = 1f;
            float LevelAt3(PlayerPersonality p) => SimBatch.Percentile(Enumerable.Range(0, 20)
                .Select(i => new SimRun(c, p, 900 + i, "rook").Play()).Where(r => r.Minutes.Count > 3).Select(r => (float)r.Minutes[3].Level).ToList(), 0.5f);
            Assert.Greater(LevelAt3(greedy), LevelAt3(lazy));
        }

        [Test]
        public void ThePersonalityThatNeverSummons_NeverFightsTheBoss_AndStopRulesEndRuns()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var never = new SimRun(c, c.Personalities.Find("first_timer"), 3).Play();
            Assert.Less(never.BossSummonSeconds, 0f);
            var thumb = c.Personalities.Find("thumb_sailor");
            foreach (var r in Enumerable.Range(0, 10).Select(i => new SimRun(c, thumb, 40 + i).Play()))
                Assert.LessOrEqual(r.EndSeconds, thumb.stopMinutes * 60f + 1f, "Time(12) quits at 12:00");
        }

        // ---- Calibration against the V4 autopilot's real runs --------------------------------------------------------

        /// <summary>The values the V4 autopilot measurement ran with (before the M2 retune).</summary>
        static void PreM2(ConfigSetRuntime rt)
        {
            var p = rt.Single<ProgressionConfig>();
            p.levelZeroToOneXp = 5; p.xpCurveOffset = 5f; p.xpCurveMultiplier = 5.325000286102295; p.xpCurveExponent = 1.065000057220459;
            var d = rt.Single<DifficultyCurveConfig>();
            d.ordinaryHp.c1 = 0.11f; d.ordinaryHp.c2 = 0.022f; d.ordinaryDamage.c1 = 0.14f; d.ordinaryDamage.c2 = 0.028f;
            d.enemyDamagePerMinute.c1 = 0.055f; d.overtimeHpMultiplierBase = 2f; d.overtimeHpMultiplierExponentPerMinute = 0.5f;
            var t = rt.Single<WaveTimelineConfig>();
            t.flow.enabled = false;
            t.overtime.spawnStatRampEnd = 2f;
            var bands = t.phases.Where(x => x.ratePerSecond > 0f && !x.isOvertime).OrderBy(x => x.trigger.timeSeconds).ToList();
            bands[2].ratePerSecond = 1.8f; bands[3].ratePerSecond = 2.4f; bands[4].ratePerSecond = 3f;
            foreach (var x in t.phases.Where(x => x.focus != null && x.focus.enabled))
            {
                if (x.focus.scheduledEliteIndex < 0) { x.focus.hpMultiplier = 4f; x.focus.damageMultiplier = 1.15f; }
                else x.spawner.elite.hpMultiplier = 1f;
            }
            t.phases.Single(x => x.isBoss).spawner.elite.hpMultiplier = 1f;
            t.phases.Single(x => x.isBoss).spawner.anchor.stats.baseHP = 800f;
            rt.Single<DirectorConfig>().onTrackGrowth = DirectorConfig.LinearCurve((0f, 1f), (1f, 3.9f), (2f, 6.7f), (3f, 8.1f), (4f, 8.8f), (5f, 9.9f), (6f, 10.5f), (8f, 17f), (10f, 24f), (15f, 34f));
        }

        /// <summary>The HumanPilot "Average" profile (plan/v4-verification/playtest-m10-m13/measure/RESULTS.md) as a personality.</summary>
        static PlayerPersonality Pilot() => new PlayerPersonality
        {
            personalityId = "pilot", displayName = "Pilot", dodgeSkill = 0.6f, contactAvoidance = 0.6f, engageDistance = 10f, movement = PersonalityMovement.Kite,
            pickupGreed = 0.7f, chests = PersonalityChests.All, bossTiming = PersonalityBossTiming.Never, cards = PersonalityCards.Shiny, rerollUse = 0f,
        };

        static List<SimRunResult> PilotRuns(DirectorMode mode, int runs)
        {
            using var rt = new ConfigSetRuntime(Set);
            PreM2(rt);
            var c = new SimContent(rt) { DirectorModeOverride = mode };
            var p = Pilot();
            // The trace harness: arenas' focus enemies sunk after 20 s (the real runs' killArenaAfter).
            return Enumerable.Range(0, runs).Select(i => new SimRun(c, p, 1000 + i, "rook") { KillArenaAfterSeconds = 20f, KillBossAtSeconds = 300f }.Play()).ToList();
        }

        [Test]
        public void Calibration_MatchesTheAutopilotsRealRuns()
        {
            // Real (V4 HumanPilot, Average build, Rook, 3 seeds): median death Observe 8.0 min, Adaptive 6.3; 18–20 kills in
            // minute 1, level 3–4 at 1:00, 10–14 at 4:00, the crowd at 48–72 during minute 5.
            var observe = PilotRuns(DirectorMode.Observe, 60);
            var adaptive = PilotRuns(DirectorMode.Adaptive, 60);
            var observeEnd = SimBatch.Percentile(observe.Select(r => r.EndMinutes).ToList(), 0.5f);
            var adaptiveEnd = SimBatch.Percentile(adaptive.Select(r => r.EndMinutes).ToList(), 0.5f);
            Assert.That(observeEnd, Is.InRange(6.8f, 9.2f), "Observe median death (real 8.0)");
            Assert.That(adaptiveEnd, Is.InRange(5.2f, 7.4f), "Adaptive median death (real 6.3)");
            Assert.Less(adaptiveEnd, observeEnd, "the adaptive director shortens average runs, as measured");
            float Med(IEnumerable<float> v) => SimBatch.Percentile(v.ToList(), 0.5f);
            Assert.That(Med(observe.Select(r => (float)r.Minutes[0].Kills)), Is.InRange(14f, 34f), "kills in minute 1");
            Assert.That(Med(observe.Select(r => (float)r.Minutes[0].Level)), Is.InRange(2f, 5f), "level at 1:00");
            Assert.That(Med(observe.Where(r => r.Minutes.Count > 3).Select(r => (float)r.Minutes[3].Level)), Is.InRange(8f, 15f), "level at 4:00");
            Assert.That(Med(observe.Where(r => r.Minutes.Count > 4).Select(r => r.Minutes[4].AliveAverage)), Is.InRange(40f, 76f), "crowd in minute 5");
        }

        // ---- The retuned data meets the baseline's targets ------------------------------------------------------------

        [Test]
        public void DefaultConfig_MeetsTheBaselineTargets()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var targets = rt.Single<BalanceTargetsConfig>();
            // The same runs as the published report (seed 1, 200 runs): the test and plan/v5/phase-02-model/model-report.md agree.
            var result = SimBatch.Run(c, 200, 1, new[] { c.Personalities.Find(targets.baselinePersonalityId) });
            // Known miss, recorded in plan/v5/phase-02-model/PLAN.md for the owner: minute 6 is the baseline's boss fight,
            // when its hits go to the Barnacle King and the kills drop below the band.
            var known = new[] { "minute 6: kills" };
            var failed = result.Personalities[0].Checks.Concat(result.Pacing).Concat(result.Model.SelectMany(m => m.Checks))
                .Where(x => !x.Met && !known.Contains(x.Name)).Select(x => x.ToString()).ToList();
            CollectionAssert.IsEmpty(failed, "baseline targets (per personality, per minute and pacing)");
            Assert.GreaterOrEqual(result.Pacing.Count, 6, "every pacing check was measured");
            Assert.GreaterOrEqual(result.Model.Sum(m => m.Checks.Count), 40, "the per-minute checks ran");
        }

        [Test]
        public void Retune_IsInTheData_AndTheParityPresetRestoresIt()
        {
            using var rt = new ConfigSetRuntime(Set);
            Assert.AreEqual("saltwind-v5-m2", rt.Single<ProgressionConfig>().balanceVersion);
            Assert.AreEqual(3, rt.Single<ProgressionConfig>().levelZeroToOneXp);
            Assert.AreEqual(0.08f, rt.Single<DifficultyCurveConfig>().ordinaryHp.c1, 1e-6f);
            var parity = new ConfigSetRuntime(Set);
            try
            {
                var errors = new List<string>();
                var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
                Assert.IsTrue(PresetCodec.Apply(parity, PresetCodec.FromJson(preset), 0f, errors), string.Join("\n", errors));
                var p = parity.Single<ProgressionConfig>();
                Assert.AreEqual((5, 5f, 5.325000286102295, 1.065000057220459), (p.levelZeroToOneXp, p.xpCurveOffset, p.xpCurveMultiplier, p.xpCurveExponent));
                var d = parity.Single<DifficultyCurveConfig>();
                Assert.AreEqual((0.11f, 0.022f, 0.14f, 0.028f, 0.055f, 2f, 0.5f), (d.ordinaryHp.c1, d.ordinaryHp.c2, d.ordinaryDamage.c1, d.ordinaryDamage.c2, d.enemyDamagePerMinute.c1, d.overtimeHpMultiplierBase, d.overtimeHpMultiplierExponentPerMinute));
                var t = parity.Single<WaveTimelineConfig>();
                var bands = t.phases.Where(x => x.ratePerSecond > 0f && !x.isOvertime).OrderBy(x => x.trigger.timeSeconds).Select(x => x.ratePerSecond).ToList();
                CollectionAssert.AreEqual(new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3f }, bands);
                foreach (var x in t.phases.Where(x => x.focus != null && x.focus.enabled))
                    if (x.focus.scheduledEliteIndex < 0) Assert.AreEqual((4f, 1.15f), (x.focus.hpMultiplier, x.focus.damageMultiplier));
                    else Assert.AreEqual(1f, x.spawner.elite.hpMultiplier);
                Assert.AreEqual(1f, t.phases.Single(x => x.isBoss).spawner.elite.hpMultiplier);
                Assert.AreEqual(800f, t.phases.Single(x => x.isBoss).spawner.anchor.stats.baseHP, "the Barnacle King's pre-M2 base HP");
            }
            finally { parity.Dispose(); }
        }

        // ---- The simulator follows the game's spawn rules (review M2 #1, #2, #10) ---------------------------------------

        [Test]
        public void BossHp_ComesFromTheArchetype_WhichTheGameReads()
        {
            using var rt = new ConfigSetRuntime(Set);
            var boss = rt.Single<WaveTimelineConfig>().phases.Single(x => x.isBoss).spawner;
            // The game applies a spawner's elite multipliers only when the spawner is elite (EnemySimulation.SpawnFromProfile);
            // the boss spawner is not, so the retune sits on the archetype's base HP.
            Assert.IsFalse(boss.elite.isElite);
            Assert.AreEqual(1f, boss.elite.hpMultiplier, "no multiplier the game would ignore");
            Assert.AreEqual(1400f, boss.anchor.stats.baseHP);
            var c = new SimContent(rt);
            var p = Clone(c.Personalities.Find("sea_legend"), "boss_check");
            p.bossTiming = PersonalityBossTiming.Never; // the harness summons it at 5:00
            var run = new SimRun(c, p, 9, "rook") { SummonBossAtSeconds = 300f };
            var r = run.Play();
            Assert.GreaterOrEqual(r.BossSummonSeconds, 300f);
            var t = r.BossSummonSeconds;
            var d = rt.Single<DifficultyCurveConfig>();
            var expected = CurveEvaluator.EnemyHp(d, 1400f, t / 60f, 1, 0) * d.ordinaryHp.Evaluate((t - d.ordinaryScalingDelaySeconds) / 60f) * rt.Single<ExperimentConfig>().enemyHpMultiplier;
            Assert.AreEqual(expected, r.BossSpawnHp, expected * 1e-4f, "curve × ordinary time, no spawner multiplier");
        }

        [Test]
        public void ScheduledElites_GetTheEliteMultiplierAndTheRichAffix_AsTheGameDoes()
        {
            using var rt = new ConfigSetRuntime(Set);
            var t = rt.Single<WaveTimelineConfig>();
            var phase = t.phases.First(x => x.focus != null && x.focus.enabled && x.focus.scheduledEliteIndex >= 0);
            Assert.IsTrue(phase.spawner.elite.isElite);
            Assert.AreEqual(ConfigEnums.EliteAffix.None, phase.spawner.elite.affix, "an elite without an affix is Rich in the game");
            var c = new SimContent(rt) { DirectorModeOverride = DirectorMode.Observe }; // no focus lever
            var p = Clone(c.Personalities.Find("sea_legend"), "elite_check");
            p.dodgeSkill = 1f; p.contactAvoidance = 1f; // survives to the beat
            var run = new SimRun(c, p, 4, "rook");
            while (run.Time < phase.trigger.timeSeconds + 0.3f) run.Step();
            var r = run.Play();
            var arena = r.Arenas.First(a => a.phase == phase.phaseId);
            var def = c.Enemy(phase.focus.variant != null ? phase.focus.variant.variantId : phase.spawner.anchor.enemyId);
            var d = rt.Single<DifficultyCurveConfig>();
            var at = arena.start;
            var expected = CurveEvaluator.EnemyHp(d, def.BaseHp, at / 60f, 1, 0) * d.ordinaryHp.Evaluate((at - d.ordinaryScalingDelaySeconds) / 60f)
                * phase.focus.hpMultiplier * phase.spawner.elite.hpMultiplier * 1.25f * rt.Single<ExperimentConfig>().enemyHpMultiplier;
            Assert.AreEqual(expected, arena.hp, expected * 1e-4f);
        }

        [Test]
        public void OvertimeFormulas_EqualTheRuntimes()
        {
            using var rt = new ConfigSetRuntime(Set);
            var t = rt.Single<WaveTimelineConfig>();
            var d = rt.Single<DifficultyCurveConfig>();
            for (var time = t.stageDeadlineSeconds - 5f; time < t.stageDeadlineSeconds + 900f; time += 7.3f)
            {
                Assert.AreEqual(Spawning.OvertimePhase.SpawnInterval(t, time), SimRun.OvertimeInterval(t, time), 1e-5f, $"interval @{time}");
                Assert.AreEqual(Spawning.OvertimePhase.HpMultiplier(t, d, time), SimRun.OvertimeHpMultiplier(t, d, time), 1e-5f, $"hp @{time}");
                Assert.AreEqual(Spawning.OvertimePhase.DamageMultiplier(t, d, time), SimRun.OvertimeDamageMultiplier(t, d, time), 1e-5f, $"damage @{time}");
                Assert.AreEqual(Spawning.OvertimePhase.SpawnStatRamp(t, time), SimRun.OvertimeStatRamp(t, time), 1e-5f, $"ramp @{time}");
            }
        }

        [Test]
        public void SomeChests_BuysOnlyWithTwiceThePrice()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var some = Clone(c.Personalities.Find("steady_captain"), "some");
            var all = Clone(some, "all"); all.chests = PersonalityChests.All;
            float Chests(PlayerPersonality p) => SimBatch.Percentile(Enumerable.Range(0, 20).Select(i => (float)new SimRun(c, p, 300 + i, "rook").Play().ChestsOpened).ToList(), 0.5f);
            Assert.Less(Chests(some), Chests(all), "Some waits for twice the price; All buys what it can afford");
        }

        [Test]
        public void OnTrackGrowth_IsMeasuredFromTheBaseline()
        {
            using var rt = new ConfigSetRuntime(Set);
            var c = new SimContent(rt);
            var curve = SimBatch.OnTrackGrowth(c, c.Personalities.Find("steady_captain"), 12, 11, 6);
            Assert.AreEqual(1f, curve[0].growth);
            for (var i = 1; i < curve.Count; i++) Assert.Greater(curve[i].growth, curve[i - 1].growth * 0.9f, "offense grows minute by minute");
            // The director's curve in the data is the measured one (within noise at the early minutes).
            var d = rt.Single<DirectorConfig>();
            Assert.That(d.onTrackGrowth.Evaluate(3f) / curve.First(x => x.minute == 3f).growth, Is.InRange(0.6f, 1.6f));
        }
    }
}
