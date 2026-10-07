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

        static PickupModule Make(ConfigSetRuntime config, Rewards rewards, Views views, int seed, float vfxDensity = 1f)
        {
            var module = new PickupModule(() => config.Single<PickupConfig>(), 64, rewards, views, () => false, () => vfxDensity);
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
            config.Single<PickupConfig>().xpPickupMultiplier = 1f; // the barrel's own value (the XP pace has its own test)
            var rewards = new Rewards();
            var views = new Views();
            var pickups = Make(config, rewards, views, 101);
            var kill = new Vector3(10f, 0f, 10f);
            // V5 M8z (7 Oct, D20): a barrel pays exactly its value, whatever the number of pieces it bursts into.
            const int xp = 3, gold = 2;
            pickups.SpawnFromEnemy(kill, xp, gold);
            Assert.AreEqual(2, pickups.ActiveCount, "an XP barrel and a gold barrel");
            Assert.AreEqual(2, views.Live);

            var far = new Vector3(-200f, 0f, -200f);
            for (var i = 0; i < 120; i++) pickups.Tick(Dt, far, 1f);
            Assert.AreEqual(2, pickups.ActiveCount, "nothing is collected with the ship far away");
            Assert.AreEqual(0, rewards.Xp + rewards.Gold);

            for (var i = 0; i < 600 && (pickups.ActiveCount > 0 || rewards.Xp == 0 || rewards.Gold == 0); i++) pickups.Tick(Dt, kill, 1f);
            Assert.AreEqual(0, pickups.ActiveCount, "both barrels were collected");
            Assert.AreEqual(0, views.Live, "their views went back");
            Assert.AreEqual(xp, rewards.Xp, "the XP value, once");
            Assert.AreEqual(gold, rewards.Gold, "the gold value, once");

            for (var i = 0; i < 120; i++) pickups.Tick(Dt, kill, 1f);
            Assert.AreEqual(xp, rewards.Xp, "no second payout");
            Assert.AreEqual(gold, rewards.Gold);
        }

        // V5 M8z (7 Oct, D20, owner): the same XP per kill at every graphics quality and with Reduced Effects. The effects
        // density (Low 0.45, Mid 0.75, High 1, each halved by Reduced Effects) sets only how many pieces a barrel bursts
        // into; the pieces split the barrel's value exactly (a piece may carry 0), for XP, gold and health alike.
        [Test]
        public void ABarrel_PaysExactlyItsValue_AtEveryEffectsDensity()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            config.Single<PickupConfig>().xpPickupMultiplier = 1f; // the barrel's own value (the XP pace has its own test)
            foreach (var density in new[] { 0.225f, 0.375f, 0.45f, 0.5f, 0.75f, 1f })
                for (var value = 1; value <= 13; value++)
                {
                    var rewards = new Rewards();
                    var views = new Views();
                    var pickups = Make(config, rewards, views, 300 + value, density);
                    var kill = new Vector3(10f, 0f, 10f);
                    pickups.Spawn(PickupType.XpBarrel, value, kill);
                    pickups.Spawn(PickupType.GoldBarrel, value, kill + Vector3.right);
                    pickups.Spawn(PickupType.Powerup, value, kill + Vector3.left);
                    for (var i = 0; i < 900 && pickups.ActiveCount > 0; i++) pickups.Tick(Dt, kill, 1f);
                    for (var i = 0; i < 120; i++) pickups.Tick(Dt, kill, 1f);
                    Assert.AreEqual(0, pickups.ActiveCount, $"density {density}, value {value}: every piece collected");
                    Assert.AreEqual(value, rewards.Xp, $"density {density}: an XP barrel worth {value} pays exactly {value}");
                    Assert.AreEqual(value, rewards.Gold, $"density {density}: a gold barrel worth {value} pays exactly {value}");
                    Assert.AreEqual(value, rewards.Heal, $"density {density}: a health barrel worth {value} heals exactly {value}");
                }
        }

        [Test]
        public void XpPickupMultiplier_PacesTheXp_WithoutLosingFractions()
        {
            // V5 demo baseline (30 Sep 2026): XP from pickups ×0.5, the fractions carried from orb to orb.
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            // V5 M8z (7 Oct, D20): 0.8 with the exact barrel payout and the retuned XP table; V5 M8z (8 Oct, close): 0.4
            // since the owner halved the XP drops (7 Oct).
            Assert.AreEqual(0.4f, config.Single<PickupConfig>().xpPickupMultiplier, "the playtest pace in the data (the owner halved the XP drops, 7 Oct)");
            int Collect(float rate, int barrels)
            {
                config.Single<PickupConfig>().xpPickupMultiplier = rate;
                var rewards = new Rewards();
                var pickups = Make(config, rewards, new Views(), 101);
                var kill = new Vector3(10f, 0f, 10f);
                for (var b = 0; b < barrels; b++)
                {
                    pickups.SpawnFromEnemy(kill, 7, 0);
                    for (var i = 0; i < 600; i++) pickups.Tick(Dt, kill, 1f); // collected, burst into orbs, granted
                }
                return rewards.Xp;
            }
            var full = Collect(1f, 3);
            Assert.Greater(full, 0);
            Assert.AreEqual(Mathf.FloorToInt(full * 0.5f), Collect(0.5f, 3), "half of the same orbs, no fraction lost");
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
