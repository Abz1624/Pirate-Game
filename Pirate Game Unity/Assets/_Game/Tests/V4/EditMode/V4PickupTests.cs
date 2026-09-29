using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M15: direct tests of the V4 pickup module (its V3 parity test went with V3): a kill drops its barrels, they wait
    /// on the water until the ship comes close, then pay out once; the drop scatter comes from the run's own seeded
    /// stream, so the same seed places the same barrels.
    /// </summary>
    [Category("V4")]
    public sealed class V4PickupTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const float Dt = 1f / 60f;

        sealed class Rewards : IPickupRewards
        {
            public int Xp, Gold, Heal;
            public bool RunEnded => false;
            public float XpGain => 1f;
            public void GrantXp(int amount) => Xp += amount;
            public void GrantGold(int amount) => Gold += amount;
            public void HealShip(int amount) => Heal += amount;
            public bool RollHealthDrop(float chance) => false;
            public void PlaySound(string id) { }
        }

        sealed class Views : IPickupViews
        {
            readonly Dictionary<object, Vector3> _at = new Dictionary<object, Vector3>();
            public int Live => _at.Count;
            public object Acquire(PickupType type, Vector3 position, int value) { var v = new object(); _at[v] = position; return v; }
            public Vector3 AlignBarrel(object view) => _at[view];
            public void BeginHoming(object view, PickupType type) { }
            public void Update(object view, PickupType type, Vector3 position, bool homing, float dt) => _at[view] = position;
            public void Release(object view, PickupType type, int value) => _at.Remove(view);
        }

        static PickupModule Make(ConfigSetRuntime config, Rewards rewards, Views views, int seed)
        {
            var module = new PickupModule(() => config.Single<PickupConfig>(), 64, rewards, views, () => false, () => 1f);
            module.Seed(seed);
            return module;
        }

        static List<Vector3> Positions(PickupModule m)
        {
            var list = new List<Vector3>();
            for (var i = 0; i < m.Capacity; i++)
                if (m.TryGet(i, out _, out _, out var p, out _)) list.Add(p);
            return list;
        }

        [Test]
        public void AKillsBarrels_WaitForTheShip_ThenPayOutOnce()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var rewards = new Rewards();
            var views = new Views();
            var pickups = Make(config, rewards, views, 101);
            var kill = new Vector3(10f, 0f, 10f);
            pickups.SpawnFromEnemy(kill, 7, 3);
            Assert.AreEqual(2, pickups.ActiveCount, "an XP barrel and a gold barrel");
            Assert.AreEqual(2, views.Live);

            var far = new Vector3(-200f, 0f, -200f);
            for (var i = 0; i < 120; i++) pickups.Tick(Dt, far, 1f);
            Assert.AreEqual(2, pickups.ActiveCount, "nothing is collected with the ship far away");
            Assert.AreEqual(0, rewards.Xp + rewards.Gold);

            for (var i = 0; i < 600 && (pickups.ActiveCount > 0 || rewards.Xp == 0 || rewards.Gold == 0); i++) pickups.Tick(Dt, kill, 1f);
            Assert.AreEqual(0, pickups.ActiveCount, "both barrels were collected");
            Assert.AreEqual(0, views.Live, "their views went back");
            Assert.AreEqual(7, rewards.Xp, "the XP value, once");
            Assert.AreEqual(3, rewards.Gold, "the gold value, once");

            for (var i = 0; i < 120; i++) pickups.Tick(Dt, kill, 1f);
            Assert.AreEqual(7, rewards.Xp, "no second payout");
            Assert.AreEqual(3, rewards.Gold);
        }

        [Test]
        public void TheSameSeed_ScattersTheSameBarrels()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var a = Make(config, new Rewards(), new Views(), 202);
            var b = Make(config, new Rewards(), new Views(), 202);
            for (var k = 0; k < 5; k++)
            {
                a.SpawnFromEnemy(new Vector3(k * 7f, 0f, 3f), 4, 2);
                b.SpawnFromEnemy(new Vector3(k * 7f, 0f, 3f), 4, 2);
            }
            CollectionAssert.AreEqual(Positions(a), Positions(b), "same seed, same scatter");
            Assert.AreEqual(10, Positions(a).Count);
            // The scatter really is random: two barrels of one kill do not land on the kill point.
            Assert.AreNotEqual(Positions(a)[0], Positions(a)[1]);
        }
    }
}
