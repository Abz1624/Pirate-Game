using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Pickups;
using HighSeas.V3;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>M9 step 9.7: the V4 pickup module against V3's PickupWorld, frame by frame on the same random streams.</summary>
    [Category("V4")]
    public sealed class V4PickupM9Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            StageBalanceProfileContext.ResetForTests();
        }

        GameObject Host(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        static T Awake<T>(GameObject host) where T : MonoBehaviour
        {
            var c = host.AddComponent<T>();
            typeof(T).GetMethod("Awake", Private)?.Invoke(c, null);
            return c;
        }

        sealed class Rewards : IPickupRewards
        {
            public RunManager Run;
            public RunRng Director;
            public int Xp, Gold;
            public float Hull, MaxHull;
            public bool RunEnded => Run.IsRunEnded;
            public float XpGain => Run.Stats.XPGain;
            public void GrantXp(int amount) => Xp += amount;
            public void GrantGold(int amount) => Gold += SaltwindStageEconomyLogic.ApplyGoldGrant(amount, StageBalanceProfileContext.Export, Run.Stats.GoldGain);
            public void HealShip(int amount) => Hull += Mathf.Min(amount, MaxHull - Hull);
            public bool RollHealthDrop(float chance) => Director.DirectorFloat() < chance;
            public void PlaySound(string id) { }
        }

        /// <summary>V3 and V4 draw from the same global UnityEngine.Random: each side keeps its own state.</summary>
        sealed class Streams
        {
            Random.State _v3, _v4;
            public Streams(int seed)
            {
                Random.InitState(seed);
                _v3 = _v4 = Random.state;
            }
            public void V3(System.Action a) { Random.state = _v3; a(); _v3 = Random.state; }
            public void V4(System.Action a) { Random.state = _v4; a(); _v4 = Random.state; }
        }

        [Test]
        public void Pickups_EqualV3sPickupWorld_OverKillsBurstsAndMagneticTides([Values(false, true)] bool tideOpensBarrels)
        {
            LogAssertIgnore();
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile);
            using var set = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var config = set.Single<PickupConfig>();

            var rm = Awake<RunManager>(Host("run"));
            rm.BeginRun(33);
            rm.Stats.AddModifier(new StatModifier(StatId.XPGain, 0.37f)); // non-integer XP gain, so the rounding is compared too
            var shipHost = Host("ship");
            var damage = shipHost.AddComponent<HighSeas.Combat.DamageReceiver>();
            damage.Initialize(5000f);
            typeof(HighSeas.Combat.DamageReceiver).GetField("_currentHP", Private).SetValue(damage, 10f);
            var ship = shipHost.AddComponent<HighSeas.Player.PlayerShipController>();
            typeof(HighSeas.Player.PlayerShipController).GetField("_damage", Private).SetValue(ship, damage);
            var v3 = Awake<PickupWorld>(Host("v3-pickups"));
            var pool = typeof(PickupWorld).GetField("_pool", Private);
            var pending = typeof(PickupWorld).GetField("_pendingFlyingPickups", Private);
            var cap = ((PickupAgent[])pool.GetValue(v3)).Length;

            var rewards = new Rewards { Run = rm, Director = new RunRng(33), Hull = 10f, MaxHull = 5000f };
            var juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var module = new PickupModule(() => config, cap, rewards, new LegacyPickupViews(Host("v4-pickups").transform, juice),
                () => tideOpensBarrels, () => HighSeas.Platform.QualityProfile.Current.vfxDensity);

            var streams = new Streams(77);
            var scenario = new System.Random(8);
            var player = Vector3.zero;
            var target = Vector3.zero;
            var v3Xp = 0;
            var gold0 = rm.Gold;
            var peak = 0;
            var directPays = 0;
            for (var frame = 0; frame < 12000; frame++)
            {
                const float dt = 1f / 60f;
                // Long stretches away from the drops fill the pool (merges), then the ship sails through them.
                if (frame % 900 == 0) target = new Vector3(scenario.Next(-60, 60), 0f, scenario.Next(-60, 60));
                player = Vector3.MoveTowards(player, target, (frame / 900 % 3 == 0 ? 2f : 14f) * dt);
                for (var k = scenario.Next(0, 3); k > 0; k--)
                {
                    var at = new Vector3(scenario.Next(-60, 60), 0f, scenario.Next(-60, 60));
                    int xp = scenario.Next(0, 12), g = scenario.Next(0, 4) == 0 ? 0 : scenario.Next(1, 30);
                    streams.V3(() => v3.SpawnFromEnemy(at, xp, g, false));
                    streams.V4(() => module.SpawnFromEnemy(at, xp, g));
                }
                if (scenario.Next(0, 200) == 0)
                {
                    var at = player + new Vector3(scenario.Next(-30, 30), 0f, scenario.Next(-30, 30));
                    var type = scenario.Next(0, 2) == 0 ? PickupType.Infamy : PickupType.Gold;
                    var value = scenario.Next(1, 9);
                    streams.V3(() => v3.Spawn(type, value, at));
                    streams.V4(() => module.Spawn(type, value, at));
                }
                if (scenario.Next(0, 1500) == 0)
                {
                    streams.V3(() => { v3.AttractLooseRewards(); if (tideOpensBarrels) OpenAllBarrels(v3); });
                    streams.V4(module.AttractLooseRewards);
                }
                if (scenario.Next(0, 1500) == 0)
                {
                    streams.V3(v3.AttractAllInfamy);
                    streams.V4(module.AttractAllXp);
                }

                // V3's XP goes to LevelSystem (absent here): count it from the pickups that paid this frame.
                var before = (PickupAgent[])((PickupAgent[])pool.GetValue(v3)).Clone();
                var queued = new List<(PickupType type, int value)>();
                foreach (var p in (IEnumerable)pending.GetValue(v3))
                    queued.Add(((PickupType)p.GetType().GetField("type").GetValue(p), (int)p.GetType().GetField("value").GetValue(p)));
                streams.V3(() => v3.Tick(dt, player, rm.Stats.PickupRadius));
                streams.V4(() => module.Tick(dt, player, rm.Stats.PickupRadius));
                var after = (PickupAgent[])pool.GetValue(v3);
                var spawned = 0;
                for (var i = 0; i < cap; i++)
                {
                    if (!before[i].active && after[i].active) spawned++;
                    if (before[i].active && !after[i].active && (before[i].type == PickupType.FlyingXp || before[i].type == PickupType.Infamy) && before[i].homingToPlayer)
                        v3Xp += Grant(before[i].value, rm.Stats.XPGain);
                }
                var processed = Mathf.Min(config.maxFlyingSpawnsPerFrame, queued.Count);
                for (var q = spawned; q < processed; q++)
                {
                    directPays++;
                    if (queued[q].type == PickupType.FlyingXp) v3Xp += Grant(queued[q].value, rm.Stats.XPGain);
                }

                var tag = $"frame {frame}";
                var active = 0;
                for (var i = 0; i < cap; i++)
                {
                    var live = module.TryGet(i, out var type, out var value, out var position, out var homing);
                    Assert.AreEqual(after[i].active, live, $"{tag} slot {i} active");
                    if (!live) continue;
                    active++;
                    Assert.AreEqual(after[i].type, type, $"{tag} slot {i} type");
                    Assert.AreEqual(after[i].value, value, $"{tag} slot {i} value");
                    Assert.AreEqual(after[i].position, position, $"{tag} slot {i} position");
                    Assert.AreEqual(after[i].homingToPlayer, homing, $"{tag} slot {i} homing");
                }
                Assert.AreEqual(active, module.ActiveCount, tag + " count");
                Assert.AreEqual(v3Xp, rewards.Xp, tag + " XP");
                Assert.AreEqual(rm.Gold - gold0, rewards.Gold, tag + " gold");
                Assert.AreEqual(damage.CurrentHP, rewards.Hull, tag + " hull");
                peak = Mathf.Max(peak, active);
            }
            Assert.AreEqual(cap, peak, "the pool filled (merges and direct pays reached)");
            Assert.Greater(directPays, 0, "some flying pieces paid directly (pool full)");
            Assert.Greater(rewards.Xp, 0);
            Assert.Greater(rewards.Gold, 0);
            Assert.Greater(rewards.Hull, 10f, "health barrels healed");
            Assert.AreEqual(rm.Rng.DirectorFloat(), rewards.Director.DirectorFloat(), "same Director draws");
        }

        /// <summary>The R-103 fix as the M8 adapter applied it to V3: every XP, gold and health barrel opened (V3's Collect).</summary>
        static void OpenAllBarrels(PickupWorld pickups)
        {
            var pool = (PickupAgent[])typeof(PickupWorld).GetField("_pool", Private).GetValue(pickups);
            var collect = typeof(PickupWorld).GetMethod("Collect", Private);
            for (var i = 0; i < pool.Length; i++)
                if (pool[i].active && (pool[i].type == PickupType.XpBarrel || pool[i].type == PickupType.GoldBarrel || pool[i].type == PickupType.Powerup))
                    collect.Invoke(pickups, new object[] { i });
        }

        static int Grant(int value, float xpGain) => Mathf.Max(1, Mathf.RoundToInt(value * xpGain));

        static void LogAssertIgnore() => UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // V3 view placeholders log in edit mode
    }
}
