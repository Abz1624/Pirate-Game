using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Events;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M10: the difficulty curve, the player power index and the director (observe).</summary>
    [Category("V4")]
    public sealed class V4DifficultyM10Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        internal static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        internal static ConfigSetRuntime Parity()
        {
            var runtime = Default();
            var errors = new List<string>();
            var preset = File.ReadAllText(Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("; ", errors));
            return runtime;
        }

        [Test]
        public void CurveDefaults_EqualV3sFormulas_BitForBit_AtAnyTime()
        {
            foreach (var parity in new[] { false, true })
            {
                using var set = parity ? Parity() : Default();
                var c = set.Single<DifficultyCurveConfig>();
                for (var step = 0; step <= 12000; step++) // 0–20 minutes every 0.1 s
                {
                    var minute = step * 0.1f / 60f;
                    foreach (var tier in new[] { 0, 1, 2, 3 })
                    foreach (var stage in new[] { -1, 0, 1, 2, 3 })
                    {
                        Assert.AreEqual(GameMath.EnemyHP(37f, minute, tier, stage), CurveEvaluator.EnemyHp(c, 37f, minute, tier, stage), $"HP at {minute} min, tier {tier}, stage {stage}");
                        Assert.AreEqual(GameMath.EnemyDamage(9f, minute, tier, stage), CurveEvaluator.EnemyDamage(c, 9f, minute, tier, stage), $"damage at {minute} min");
                    }
                }
            }
        }

        [Test]
        public void KeyframeCurves_AreEvaluated()
        {
            using var set = Default();
            var c = set.Single<DifficultyCurveConfig>();
            var old = c.enemyHpPerMinute;
            c.enemyHpPerMinute = new CurveSpec { mode = CurveMode.Keyframes, keyframes = AnimationCurve.Linear(0f, 1f, 10f, 3f) };
            try { Assert.AreEqual(37f * 2f * c.globalHpMultiplier, CurveEvaluator.EnemyHp(c, 37f, 5f, 1, 0), 1e-3f); }
            finally { c.enemyHpPerMinute = old; }
        }

        // ---- Director shaping ------------------------------------------------------------------------------------------

        [Test]
        public void Director_Clamps_SlopeLimits_AndSmooths()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var c = set.Single<DifficultyCurveConfig>();
            var strong = new DirectorSample { OffenseNorm = 10f, DefenseNorm = 3f, Performance = 2f };
            Assert.AreEqual(c.pressureMax, DirectorMath.Target(strong, d, c.pressureMin, c.pressureMax), "clamped at the top");
            var weak = new DirectorSample { OffenseNorm = 0f, DefenseNorm = 0f, Performance = 0f };
            Assert.AreEqual(c.pressureMin, DirectorMath.Target(weak, d, c.pressureMin, c.pressureMax), "clamped at the bottom");
            Assert.AreEqual(1f, DirectorMath.Target(new DirectorSample { OffenseNorm = 1f, DefenseNorm = 1f, Performance = 1f }, d, c.pressureMin, c.pressureMax), 1e-6f, "even = 1");

            var p = 1f;
            for (var s = 0; s < 600; s++)
            {
                var next = DirectorMath.Step(p, c.pressureMax, 1f, d, c.pressureMin, c.pressureMax);
                Assert.LessOrEqual(next - p, d.maxChangePerSecond + 1e-6f, "never faster than the slope limit");
                Assert.GreaterOrEqual(next, p, "moves toward the target");
                p = next;
            }
            Assert.AreEqual(c.pressureMax, p, 1e-3f, "reaches the target in time");
            Assert.LessOrEqual(DirectorMath.Step(1f, 1.3f, 1f, d, c.pressureMin, c.pressureMax) - 1f, d.maxChangePerSecond + 1e-6f);
            // Smoothing: a small gap is closed by the exponential, not the slope limit.
            var small = DirectorMath.Step(1f, 1.01f, 1f, d, c.pressureMin, c.pressureMax);
            Assert.AreEqual(1f + 0.01f * (1f - Mathf.Exp(-1f / d.smoothingSeconds)), small, 1e-6f);
        }

        [Test]
        public void Director_Replay_GivesTheSamePressureSeries()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var c = set.Single<DifficultyCurveConfig>();
            List<float> Series()
            {
                var rng = new System.Random(7);
                var p = 1f;
                var list = new List<float>();
                for (var s = 0; s < 900; s++)
                {
                    var sample = new DirectorSample { OffenseNorm = (float)rng.NextDouble() * 2f, DefenseNorm = (float)rng.NextDouble() * 1.5f, Performance = (float)rng.NextDouble() * 2f };
                    p = DirectorMath.Step(p, DirectorMath.Target(sample, d, c.pressureMin, c.pressureMax), 1f, d, c.pressureMin, c.pressureMax);
                    list.Add(p);
                }
                return list;
            }
            CollectionAssert.AreEqual(Series(), Series());
        }

        [Test]
        public void Performance_RewardsKillingFaster_AndPunishesHullLoss()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            Assert.AreEqual(1f, DirectorMath.Performance(9, 10, 0f, 30f, d), 1e-5f, "90% of spawns killed is even");
            Assert.AreEqual(1f, DirectorMath.Performance(3, 0, 0f, 30f, d), 1e-5f, "nothing spawned (arena, boss fight): even, not a lead");
            Assert.Greater(DirectorMath.Performance(20, 10, 0f, 30f, d), 1f);
            Assert.Less(DirectorMath.Performance(9, 10, 0.3f, 30f, d), 1f, "hull lost pulls it down");
        }

        // ---- Power index -----------------------------------------------------------------------------------------------

        internal sealed class Mods : IWeaponModifiers
        {
            public float AttackSpeed => 1f; public float ProjectileSpeed => 1f; public float Size => 1f; public float Duration => 1f;
            public float Quantity => 0f; public float CritChance { get; set; } public float CritDamage { get; set; } = 1.5f; public float EliteDamage => 1f;
            public float GlobalDamage { get; set; } = 1f; public float CaptainAdditiveDamage => 0f;
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f; public float ItemCritChanceBonus => 0f; public float ItemAttackSpeedMultiplier => 1f;
            public float FlameRangeMultiplier => 1f; public float BurnDamageMultiplier => 1f; public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier => 1f; public void ExplosionResolved() { } public int ExtraLightningJumps => 0;
        }

        internal sealed class Inputs : IPowerInputs
        {
            public List<WeaponSlot> Slots = new List<WeaponSlot>();
            public Mods M = new Mods();
            public float HullValue = 100f, MaxHullValue = 100f, Incoming = 1f;
            public IReadOnlyList<WeaponSlot> Weapons => Slots;
            public IWeaponModifiers Modifiers => M;
            public float Hull => HullValue;
            public float MaxHull => MaxHullValue;
            public float IncomingDamageMultiplier => Incoming;
        }

        internal static Inputs WithWeapons(ConfigSetRuntime set, int count)
        {
            var inputs = new Inputs();
            foreach (var w in set.All<WeaponConfig>().Where(w => w.databaseOrder >= 0).OrderBy(w => w.databaseOrder).Take(count))
                inputs.Slots.Add(new WeaponSlot { Config = w });
            return inputs;
        }

        [Test]
        public void PowerIndex_GrowsWithTheBuild_AndDefenseFollowsTheHull()
        {
            using var set = Default();
            var d = set.Single<DirectorConfig>();
            var one = PlayerPowerIndex.Offense(WithWeapons(set, 1), d);
            var six = PlayerPowerIndex.Offense(WithWeapons(set, 6), d);
            Assert.Greater(one, 0f);
            Assert.Greater(six, one * 3f, "six weapons are far stronger than one");
            var stronger = WithWeapons(set, 1);
            stronger.M.GlobalDamage = 2f;
            Assert.AreEqual(one * 2f, PlayerPowerIndex.Offense(stronger, d), one * 1e-4f, "damage stat doubles offense");

            var i = new Inputs { HullValue = 50f };
            Assert.AreEqual(0.5f, PlayerPowerIndex.Defense(i), 1e-5f);
            i.Incoming = 0.5f;
            Assert.AreEqual(1f, PlayerPowerIndex.Defense(i), 1e-5f, "halved incoming damage doubles defense");
        }

        // ---- The module ------------------------------------------------------------------------------------------------

        [Test]
        public void Director_SamplesOncePerInterval_PublishesPower_AndStaysInTheBand()
        {
            using var set = Default();
            var clock = new RunClock();
            var events = new CombatEvents();
            var context = new RunContext(clock, events, set);
            var inputs = WithWeapons(set, 1);
            context.Register<IPowerInputs>(inputs);
            var director = new DifficultyDirector();
            director.Initialize(context);
            director.BeginRun(new RunStart(1, false));
            var power = 0;
            events.TickPublished += batch => power += batch.Count(e => e.Kind == CombatEventKind.PowerChanged);
            const float dt = 1f / 60f;
            for (var t = 0; t < 60 * 120; t++)
            {
                if (t == 60 * 30) foreach (var w in WithWeapons(set, 6).Slots.Skip(1)) inputs.Slots.Add(w); // the build grows at 0:30
                director.Tick(new SimTick(t, dt, t * dt));
                events.EndTick();
            }
            var c = set.Single<DifficultyCurveConfig>();
            Assert.AreEqual(120, power, 1, "one sample a second");
            Assert.Greater(director.Pressure, 1f, "a build that outgrows the curve raises Pressure");
            Assert.LessOrEqual(director.Pressure, c.pressureMax);
            Assert.GreaterOrEqual(director.Pressure, c.pressureMin);
            Assert.AreEqual(Mathf.Min(120, 180), director.History.Count);
        }

        internal static (DifficultyDirector director, CombatEvents events, Inputs inputs) Rig(ConfigSetRuntime set, int seed = 1)
        {
            var events = new CombatEvents();
            var context = new RunContext(new RunClock(), events, set);
            var inputs = WithWeapons(set, 1);
            context.Register<IPowerInputs>(inputs);
            var director = new DifficultyDirector();
            director.Initialize(context);
            director.BeginRun(new RunStart(seed, false));
            return (director, events, inputs);
        }

        [Test]
        public void Performance_ComesFromTheEventStream()
        {
            using var set = Default();
            var (director, events, _) = Rig(set);
            const float dt = 1f / 60f;
            for (var t = 0; t < 60 * 20; t++)
            {
                if (t % 30 == 0) events.Publish(new CombatEvent(CombatEventKind.Spawned, t, t * dt));
                if (t % 30 == 0) { events.Publish(new CombatEvent(CombatEventKind.Killed, t, t * dt)); events.Publish(new CombatEvent(CombatEventKind.Killed, t, t * dt)); }
                director.Tick(new SimTick(t, dt, t * dt));
                events.EndTick();
            }
            Assert.Greater(director.Performance, 1.5f, "killing twice as fast as they arrive");
            for (var t = 60 * 20; t < 60 * 25; t++)
            {
                if (t % 10 == 0) events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, t, t * dt, z: 10f));
                director.Tick(new SimTick(t, dt, t * dt));
                events.EndTick();
            }
            Assert.Less(director.Performance, 1f, "losing hull fast pulls it down");
        }

        [Test]
        public void OffMode_KeepsPressureAtOne_AndForgetsWhatHappenedMeanwhile()
        {
            using var set = Default();
            var (director, events, inputs) = Rig(set);
            const float dt = 1f / 60f;
            var t = 0;
            void Run(int seconds) { for (var end = t + 60 * seconds; t < end; t++) { director.Tick(new SimTick(t, dt, t * dt)); events.EndTick(); } }
            Run(5); // the baseline is the one-weapon build
            foreach (var w in WithWeapons(set, 6).Slots.Skip(1)) inputs.Slots.Add(w);
            Run(40);
            Assert.Greater(director.Pressure, 1f);
            var d = set.Single<DirectorConfig>();
            d.mode = DirectorMode.Off;
            try
            {
                for (var i = 0; i < 50; i++) events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, t, t * dt, z: 50f));
                Run(5);
                Assert.AreEqual(1f, director.Pressure, "Off: Pressure is 1");
                d.mode = DirectorMode.Observe;
                Run(2);
                Assert.GreaterOrEqual(director.Performance, 0.99f, "hull lost while Off does not land in the window");
            }
            finally { d.mode = DirectorMode.Observe; }
        }

        /// <summary>Observe mode cannot change a run: nothing in the runtime reads Pressure outside the difficulty module (M11 adds the levers).</summary>
        [Test]
        public void ObserveMode_NothingReadsPressure()
        {
            var root = Path.Combine(Application.dataPath, "_Game/V4/Runtime");
            bool Reads(string text) => text.Contains("IPressureView") || text.Contains("DifficultyDirector") || text.Contains("PowerChanged");
            var readers = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/Difficulty/") && Reads(File.ReadAllText(f)))
                .Select(f => f.Substring(root.Length + 1)).ToList();
            CollectionAssert.AreEquivalent(new[] { Path.Combine("Core", "V4Game.cs"), Path.Combine("Events", "CombatEvents.cs") }, readers,
                "only the composition root holds the director, and only the event enum names PowerChanged");
            var presentation = Path.Combine(Application.dataPath, "_Game/V4/Presentation");
            Assert.IsFalse(Directory.GetFiles(presentation, "*.cs", SearchOption.AllDirectories).Any(f => Reads(File.ReadAllText(f))), "no presenter reads Pressure");
            // M11: the default is Adaptive (the levers read Pressure through IDifficultyLevers, never IPressureView); the
            // v3-parity preset keeps Observe, which the trace sets prove changes nothing.
            using var set = Default();
            Assert.AreEqual(DirectorMode.Adaptive, set.Single<DirectorConfig>().mode, "M11 ships Adaptive");
            using var parity = Parity();
            Assert.AreEqual(DirectorMode.Observe, parity.Single<DirectorConfig>().mode, "the v3-parity preset observes only");
        }
    }
}
