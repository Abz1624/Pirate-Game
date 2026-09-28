using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Events;
using UnityEngine;
using static RoguePirates.V4.Tests.V4DifficultyM10Tests;

namespace RoguePirates.V4.Tests
{
    /// <summary>M11: the adaptive levers, the never-adapt list, replay determinism and the run score record.</summary>
    [Category("V4")]
    public sealed class V4AdaptiveM11Tests
    {
        const float Dt = 1f / 60f;

        static void Run(DifficultyDirector director, CombatEvents events, ref int t, int seconds)
        {
            for (var end = t + 60 * seconds; t < end; t++) { director.Tick(new SimTick(t, Dt, t * Dt)); events.EndTick(); }
        }

        [Test]
        public void LeverMath_StaysInsideItsClamps_AtTheBandEdges()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var max = set.Single<DifficultyCurveConfig>().pressureMax;
            Assert.AreEqual(1f, LeverMath.ThreatRate(1f, d), 1e-6f);
            Assert.AreEqual(1.35f, LeverMath.ThreatRate(1.35f, d), 1e-6f, "+35 % at the top");
            Assert.AreEqual(0.85f, LeverMath.ThreatRate(0.85f, d), 1e-6f, "−15 % at the bottom");
            var gain = d.threatRateGain;
            d.threatRateGain = 5f;
            try
            {
                Assert.AreEqual(d.threatRateMax, LeverMath.ThreatRate(1.35f, d), 1e-6f, "a big gain still clamps");
                Assert.AreEqual(d.threatRateMin, LeverMath.ThreatRate(0.85f, d), 1e-6f);
            }
            finally { d.threatRateGain = gain; }

            Assert.AreEqual(0f, LeverMath.EliteChance(1f, max, d));
            Assert.AreEqual(0f, LeverMath.EliteChance(0.85f, max, d), "weak builds get no fewer elites than the design");
            Assert.AreEqual(d.eliteChanceAtMax, LeverMath.EliteChance(max, max, d), 1e-6f);
            Assert.AreEqual(0f, LeverMath.TierLead(0.9f, max, d), "tiers never unlock later");
            Assert.AreEqual(60f, LeverMath.TierLead(max, max, d), 1e-4f, "about a minute earlier at the top");
            Assert.AreEqual(1f, LeverMath.CapHp(1f, max, d));
            Assert.AreEqual(d.capHpMultiplierAtMax, LeverMath.CapHp(max, max, d), 1e-6f);
            Assert.AreEqual(1f, LeverMath.CapHp(0.85f, max, d), "never lowers HP");
        }

        [Test]
        public void Levers_AreNeutral_UnlessAdaptive_AndEachSwitchTurnsItsLeverOff()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var (director, _, _) = Rig(set);
            IDifficultyLevers levers = director;
            director.ForcedPressure = 1.35f;
            Assert.IsTrue(levers.Adaptive);
            Assert.AreEqual(1.35f, levers.ThreatRateMultiplier, 1e-5f);
            Assert.AreEqual(60f, levers.TierLeadSeconds, 1e-3f);
            Assert.AreEqual(d.capHpMultiplierAtMax, levers.CapHpMultiplier, 1e-5f);
            Assert.AreEqual(1.35f, levers.ConditionPressure, 1e-5f);
            Assert.IsTrue(levers.CompositionActive);

            foreach (var mode in new[] { DirectorMode.Observe, DirectorMode.Off })
            {
                d.mode = mode;
                try
                {
                    Assert.IsFalse(levers.Adaptive, mode.ToString());
                    Assert.AreEqual(1f, levers.ThreatRateMultiplier, mode.ToString());
                    Assert.AreEqual(0f, levers.TierLeadSeconds);
                    Assert.AreEqual(1f, levers.CapHpMultiplier);
                    Assert.AreEqual(1f, levers.ConditionPressure, "conditions see 1, as before M11");
                    Assert.IsFalse(levers.CompositionActive);
                    Assert.AreEqual(1f, levers.SpawnerWeight(AnimationCurve.Linear(0.85f, 0f, 1.35f, 3f)));
                    Assert.AreEqual(1f, levers.CompositionWeight(new[] { "ranged" }));
                    for (var i = 0; i < 200; i++) Assert.IsFalse(levers.RollElite());
                }
                finally { d.mode = DirectorMode.Adaptive; }
            }

            void Off(System.Action<bool> set_, System.Action check) { set_(false); try { check(); } finally { set_(true); } }
            Off(v => d.threatRateLever = v, () => Assert.AreEqual(1f, levers.ThreatRateMultiplier));
            Off(v => d.tierTimingLever = v, () => Assert.AreEqual(0f, levers.TierLeadSeconds));
            Off(v => d.capHpLever = v, () => Assert.AreEqual(1f, levers.CapHpMultiplier));
            Off(v => d.compositionLever = v, () => { Assert.IsFalse(levers.CompositionActive); Assert.AreEqual(1f, levers.SpawnerWeight(AnimationCurve.Constant(0f, 2f, 2f))); });
            Off(v => d.eliteLever = v, () => { for (var i = 0; i < 200; i++) Assert.IsFalse(levers.RollElite()); });
            Off(v => d.pressureConditionTriggers = v, () => Assert.AreEqual(1f, levers.ConditionPressure));
        }

        [Test]
        public void EliteLever_UsesItsOwnSeededStream_AtTheConfiguredRate()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            List<bool> Rolls(int seed, float pressure)
            {
                var (director, _, _) = Rig(set, seed);
                director.ForcedPressure = pressure;
                return Enumerable.Range(0, 20000).Select(_ => director.RollElite()).ToList();
            }
            var a = Rolls(101, 1.35f);
            CollectionAssert.AreEqual(a, Rolls(101, 1.35f), "same seed, same rolls");
            CollectionAssert.AreNotEqual(a, Rolls(202, 1.35f), "the stream is seed-derived");
            var rate = a.Count(x => x) / 20000f;
            Assert.AreEqual(d.eliteChanceAtMax, rate, 0.006f, "≈ the configured chance at the top of the band");
            Assert.IsFalse(Rolls(101, 1f).Any(x => x), "no elites at Pressure 1");
        }

        [Test]
        public void Composition_CloseRangeBuilds_DrawRangedShips_LongRangeBuilds_DrawSwarms()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var shooter = EnemyRoles.Of(null, new ArchetypeCombat { attackPattern = ConfigEnums.EnemyAttackPattern.SingleShot });
            var rammer = EnemyRoles.Of(null, new ArchetypeCombat { attackPattern = ConfigEnums.EnemyAttackPattern.Contact });
            CollectionAssert.Contains(shooter, "ranged");
            CollectionAssert.Contains(rammer, "swarm");
            Assert.AreEqual(1.6f, LeverMath.Composition(shooter, 1f, 1f, d), 1e-5f, "all close range: ranged ships +60 %");
            Assert.AreEqual(1f, LeverMath.Composition(rammer, 1f, 1f, d), 1e-5f);
            Assert.AreEqual(1.6f, LeverMath.Composition(rammer, 0f, 1f, d), 1e-5f, "all long range: swarms +60 %");
            Assert.AreEqual(1f, LeverMath.Composition(shooter, 1f, 0f, d), "no bias at Pressure 1");

            // Every archetype gets a role from its combat block (no enemy ids): the default timeline has both roles.
            var roles = set.All<EnemyArchetypeConfig>().Where(x => !x.isBoss).Select(x => EnemyRoles.Of(x.tags, x.combat)).ToList();
            Assert.IsTrue(roles.Any(r => r.Contains("ranged")) && roles.Any(r => r.Contains("swarm")));

            // The director measures the close-range share of the build.
            var (director, events, inputs) = Rig(set);
            var t = 0;
            Run(director, events, ref t, 3);
            var closeWeapons = set.All<WeaponConfig>().Where(w => d.closeRangeBehaviors.Contains(w.behavior)).ToList();
            Assume.That(closeWeapons.Count, Is.GreaterThan(0));
            inputs.Slots.Clear();
            inputs.Slots.Add(new Weapons.WeaponSlot { Config = closeWeapons[0] });
            Run(director, events, ref t, 2);
            Assert.AreEqual(1f, director.CloseRangeShare, 1e-5f);
        }

        [Test]
        public void Adaptive_Replay_GivesTheSamePressure_AndTheSameElites()
        {
            using var set = Default();
            (List<float>, List<bool>) Play()
            {
                var (director, events, inputs) = Rig(set, 303);
                var pressures = new List<float>();
                var elites = new List<bool>();
                for (var t = 0; t < 60 * 150; t++)
                {
                    if (t == 60 * 20) foreach (var w in WithWeapons(set, 6).Weapons.Skip(1)) inputs.Slots.Add(w);
                    if (t % 20 == 0) events.Publish(new CombatEvent(CombatEventKind.Spawned, t, t * Dt));
                    if (t % 15 == 0) events.Publish(new CombatEvent(CombatEventKind.Killed, t, t * Dt));
                    if (t % 90 == 0) events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, t, t * Dt, z: 3f));
                    director.Tick(new SimTick(t, Dt, t * Dt));
                    events.EndTick();
                    if (t % 60 == 0) { pressures.Add(director.Pressure); elites.Add(director.RollElite()); }
                }
                return (pressures, elites);
            }
            var (p1, e1) = Play();
            var (p2, e2) = Play();
            CollectionAssert.AreEqual(p1, p2);
            CollectionAssert.AreEqual(e1, e2);
            Assert.Greater(p1.Last(), 1.2f, "a six-weapon build pushes Pressure up");
        }

        /// <summary>Boss HP, scripted beats and their times, encounters, overtime and per-kill rewards never read the levers.</summary>
        [Test]
        public void NeverAdapt_BossBeatsEncountersAndRewards_DoNotReadTheLevers()
        {
            var root = Path.Combine(Application.dataPath, "_Game/V4/Runtime");
            string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
            foreach (var file in new[] { "Spawning/ScriptedPhases.cs", "Spawning/EncounterExecutor.cs", "Spawning/OvertimeAndStorm.cs" })
                Assert.IsFalse(Read(file).Contains("Levers"), file + " (beats, encounters, boss, overtime) must not adapt");
            foreach (var dir in new[] { "Stage", "Progression", "Enemies", "Weapons" })
                foreach (var f in Directory.GetFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
                    Assert.IsFalse(File.ReadAllText(f).Contains("Levers") || File.ReadAllText(f).Contains("IDifficultyLevers"), f + " (rewards, boss HP, kills) must not adapt");

            // Inside the executor the levers exist only for ambient spawns.
            var executor = Read("Spawning/SpawnExecutor.cs");
            StringAssert.Contains("var levers = ambient && _s.Levers != null && _s.Levers.Adaptive ? _s.Levers : null;", executor);
            // Time-triggered phases never move: the trigger code reads the levers only for the Pressure condition.
            var triggers = Read("Spawning/TimelineTriggers.cs");
            var leverLines = triggers.Split('\n').Where(l => l.Contains("_s.Levers")).ToList();
            Assert.AreEqual(1, leverLines.Count);
            StringAssert.Contains("PressureAtLeast", leverLines[0]);
        }

        [Test]
        public void RunRecord_StoresPressureBesideKills_AndRankedNeedsAClean_AdaptiveRun()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var (director, events, inputs) = Rig(set, 101);
            var t = 0;
            Run(director, events, ref t, 5);
            foreach (var w in WithWeapons(set, 6).Weapons.Skip(1)) inputs.Slots.Add(w);
            Run(director, events, ref t, 60);
            var record = director.CreateRecord(101, 321, 65f, "hash", 0);
            Assert.AreEqual(321, record.kills);
            Assert.Greater(record.peakPressure, record.averagePressure);
            Assert.Greater(record.averagePressure, 1f);
            Assert.LessOrEqual(record.peakPressure, 1.35f);
            Assert.AreEqual(d.version, record.directorVersion);
            Assert.AreEqual("Adaptive", record.directorMode);
            Assert.AreEqual("hash", record.configHash);
            Assert.IsTrue(record.ranked, "adaptive, ranked version, no edits, never forced");

            Assert.IsFalse(director.CreateRecord(101, 1, 1f, "hash", 2).ranked, "live tuning edits unrank");
            d.mode = DirectorMode.Observe;
            try { Assert.IsFalse(director.CreateRecord(101, 1, 1f, "hash", 0).ranked, "only Adaptive runs are ranked"); }
            finally { d.mode = DirectorMode.Adaptive; }
            var version = d.rankedVersion;
            d.rankedVersion = "director-2";
            try { Assert.IsFalse(director.CreateRecord(101, 1, 1f, "hash", 0).ranked, "ranked runs need the published version"); }
            finally { d.rankedVersion = version; }
            director.ForcedPressure = 1.2f;
            director.ForcedPressure = null;
            Assert.IsFalse(director.CreateRecord(101, 1, 1f, "hash", 0).ranked, "a run that was ever forced is not ranked");
        }

        [Test]
        public void RunRecordStore_KeepsTheLastFifty_InItsOwnKey()
        {
            var key = PlayerPrefsRunRecordStore.Key;
            var had = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            try
            {
                PlayerPrefs.DeleteKey(key);
                var store = new PlayerPrefsRunRecordStore();
                for (var i = 0; i < 55; i++) store.Add(new RunScoreRecord { kills = i, averagePressure = 1.1f, peakPressure = 1.3f });
                var records = new PlayerPrefsRunRecordStore().Records;
                Assert.AreEqual(PlayerPrefsRunRecordStore.MaxRecords, records.Count);
                Assert.AreEqual(5, records[0].kills);
                Assert.AreEqual(54, records.Last().kills);
                Assert.AreEqual(1.3f, records.Last().peakPressure);
                PlayerPrefs.SetString(key, "{not json");
                Assert.AreEqual(0, new PlayerPrefsRunRecordStore().Records.Count, "a corrupt key reads as empty");
            }
            finally
            {
                if (had != null) PlayerPrefs.SetString(key, had); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void ReviewFixes_EnumOrder_NaN_ConditionsNeverLater_AndConsoleUnranks()
        {
            // The elite affix is cast between the config enum and the game enum: same names in the same order.
            CollectionAssert.AreEqual(System.Enum.GetNames(typeof(HighSeas.Core.EliteAffix)), System.Enum.GetNames(typeof(ConfigEnums.EliteAffix)));
            using var set = Default();
            var (director, _, _) = Rig(set);
            IDifficultyLevers levers = director;
            director.ForcedPressure = float.NaN;
            Assert.IsNull(director.ForcedPressure, "NaN is rejected (it would make ambient spawns fire every tick)");
            director.ForcedPressure = float.PositiveInfinity;
            Assert.IsNull(director.ForcedPressure);
            director.ForcedPressure = 0.85f;
            Assert.AreEqual(1f, levers.ConditionPressure, "a weak build never delays a Pressure condition");
            director.ForcedPressure = null;
            Assert.IsTrue(director.CreateRecord(1, 1, 1f, "h", 0).ranked == false, "(forced earlier)");
            var (clean, _, _) = Rig(set);
            Assert.IsTrue(clean.CreateRecord(1, 1, 1f, "h", 0).ranked);
            clean.MarkUnranked();
            Assert.IsFalse(clean.CreateRecord(1, 1, 1f, "h", 0).ranked, "opening the dev console unranks the run");
            clean.BeginRun(new RunStart(2, false));
            Assert.IsFalse(clean.Unranked, "a new run starts clean");
        }

        [Test]
        public void ForcedPressure_IsClampedToTheBand_AndClears()
        {
            using var set = Default();
            var (director, _, _) = Rig(set);
            director.ForcedPressure = 5f;
            Assert.AreEqual(1.35f, director.Pressure, 1e-6f);
            director.ForcedPressure = 0f;
            Assert.AreEqual(0.85f, director.Pressure, 1e-6f);
            director.ForcedPressure = null;
            Assert.AreEqual(1f, director.Pressure, 1e-6f);
        }
    }
}
