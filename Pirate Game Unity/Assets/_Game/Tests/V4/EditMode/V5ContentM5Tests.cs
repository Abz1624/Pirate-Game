using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M5: the new Logbooks, items and powerups. Content is reachable and priced; each new item effect does what its
    /// card says (Jolly Roger, Barnacle Crown, Leviathan Heart, Captain's Last Rum, Shark Tooth Necklace, Skeleton Key,
    /// Smuggler's Ledger, Black Spot); the hull's death hook restores the ship once.
    /// </summary>
    public sealed class V5ContentM5Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        sealed class Stats : IStatModifierSink, IStatReader
        {
            public readonly PlayerStats Store = new PlayerStats();
            readonly StatResolver _r;
            public Stats() { _r = new StatResolver(() => Store); }
            public void Add(string source, int stat, float additive, float multiplicative = 1f) => _r.Add(source, stat, additive, multiplicative);
            public void RemoveSource(string source) => _r.RemoveSource(source);
            public void SetBase(int stat, float value) => _r.SetBase(stat, value);
            public float Get(int stat) => _r.Get(stat);
        }

        sealed class World : IItemWorld
        {
            public int Near;
            public bool RunActive => true;
            public bool ShipExists => true;
            public Vector3 ShipForward => Vector3.forward;
            public Vector3 ShipPosition => Vector3.zero;
            public Vector3 ShipVelocity => Vector3.zero;
            public int Gold => 0;
            public IEnumerable<WeaponConfig> Weapons => Enumerable.Empty<WeaponConfig>();
            public int EnemiesNear(float radius) => Near;
        }

        static (ItemModule items, World world, ConfigSetRuntime config) Rig(params string[] itemIds)
        {
            var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var world = new World();
            var items = new ItemModule(id => config.All<ItemConfig>().FirstOrDefault(i => i.itemId == id), new Stats(), world);
            items.ResetForRun();
            foreach (var id in itemIds) items.TryAddItem(id);
            return (items, world, config);
        }

        static readonly string[] Logbooks = { "fortune", "tidecaller", "admiral", "plunderer" }; // Ironclad waits for armor (question 30)
        // D34: the Commons are bought too (in every pool they broke the calibrated baselines); Reef Carapace and Cursed Coin
        // are out of the pools until armor and gold gain apply in runs (questions 30-31).
        static readonly string[] Commons = { "spare_planks", "powder_monkey", "shark_tooth_necklace", "manta_sail", "crows_nest" };
        static readonly string[] Bought =
        {
            "skeleton_key", "smugglers_ledger", "black_spot", "dead_mans_compass", "barnacle_crown", "jolly_roger",
            "leviathan_heart", "captains_last_rum",
        };

        [Test]
        public void TheNewContent_IsInThePools_AndPriced()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var unlocks = config.Single<UnlockConfig>();
            var logbooks = config.All<LogbookConfig>().ToDictionary(l => l.logbookId);
            foreach (var id in Logbooks)
            {
                Assert.IsTrue(logbooks.ContainsKey(id) && logbooks[id].inSaltwindPool, id);
                Assert.Greater(unlocks.RumCost(id), 0, id + " is bought with Rum");
            }
            var items = config.All<ItemConfig>().ToDictionary(i => i.itemId);
            var icons = RoguePirates.V4.Presentation.Hud.IconCatalog.Load();
            foreach (var id in Commons.Concat(Bought))
            {
                Assert.IsTrue(items.ContainsKey(id) && items[id].inChestPool, id);
                Assert.IsNotNull(icons.Item(id), id + " has an icon");
                StringAssert.DoesNotContain("—", items[id].description, id + ": no em dash in UI text");
            }
            foreach (var id in Commons.Concat(Bought)) Assert.Greater(unlocks.RumCost(id), 0, id + " is bought with Rum (D34)");
            foreach (var id in new[] { "reef_carapace", "cursed_coin" }) Assert.IsFalse(items[id].inChestPool, id + " waits for its stat to apply in runs");
            Assert.IsFalse(logbooks["ironclad"].inSaltwindPool, "Ironclad waits for armor");
            Assert.AreEqual(0.03f, logbooks["fortune"].perLevelAdditive, 1e-6f, "Fortune on the level-up Luck scale (Lucky Doubloon's +0.03)");
        }

        [Test]
        public void JollyRoger_StacksPerTenKills_AndAHitTakesHalf()
        {
            var (items, _, config) = Rig("jolly_roger");
            using (config)
            {
                for (var i = 0; i < 35; i++) items.NotifyKill();
                Assert.AreEqual(3, items.StreakStacks);
                Assert.AreEqual(1.03f, items.GlobalDamageMultiplier(), 1e-4f);
                items.NotifyPlayerTookDamage();
                Assert.AreEqual(1, items.StreakStacks, "half of 3, rounded up, is lost");
            }
        }

        [Test]
        public void BarnacleCrown_StacksWhileSurrounded_AndFades()
        {
            var (items, world, config) = Rig("barnacle_crown");
            using (config)
            {
                world.Near = 5;
                for (var i = 0; i < 60; i++) items.Tick(0.05f); // 3 s
                Assert.AreEqual(3f, items.SurroundStacks, 0.06f);
                Assert.AreEqual(1.06f, items.GlobalDamageMultiplier(), 1e-3f, "+2% a stack");
                world.Near = 1;
                for (var i = 0; i < 40; i++) items.Tick(0.05f); // 2 s
                Assert.AreEqual(0f, items.SurroundStacks, 1e-4f, "it fades two stacks a second");
            }
        }

        [Test]
        public void LeviathanHeart_PulsesEvery12Seconds()
        {
            var (items, _, config) = Rig("leviathan_heart");
            using (config)
            {
                var pulses = new List<(string id, float damage, float radius, float telegraph)>();
                items.PulseRequested += (id, d, r, t) => pulses.Add((id, d, r, t));
                for (var i = 0; i < 25 * 20; i++) items.Tick(0.05f); // 25 s
                Assert.AreEqual(2, pulses.Count);
                Assert.AreEqual(("leviathan_heart", 45f, 5f, 0.6f), pulses[0]);
                items.TryAddItem("leviathan_heart");
                for (var i = 0; i < 12 * 20 + 1; i++) items.Tick(0.05f);
                Assert.AreEqual(45f * 1.2f, pulses.Last().damage, 1e-3f, "a second copy: ×1.2");
            }
        }

        [Test]
        public void CaptainsLastRum_SavesTheShipOnce_WithAFrenzy()
        {
            var (items, _, config) = Rig("captains_last_rum");
            using (config)
            {
                var go = new GameObject("hull");
                try
                {
                    var hull = go.AddComponent<DamageReceiver>();
                    hull.Initialize(100f);
                    hull.PreventDeath = items.TryCheatDeath;
                    var died = 0;
                    hull.Died += () => died++;
                    hull.TakeImpact(150f, Vector3.zero);
                    Assert.AreEqual(0, died, "the Last Rum saved it");
                    Assert.AreEqual(25f, hull.CurrentHP, 1e-3f, "back at 25% hull");
                    Assert.AreEqual(1.3f, items.GlobalDamageMultiplier(), 1e-4f, "and +30% damage for 8 s");
                    for (var i = 0; i < 9 * 20; i++) items.Tick(0.05f);
                    Assert.AreEqual(1f, items.GlobalDamageMultiplier(), 1e-4f, "the frenzy ends");
                    hull.TakeImpact(150f, Vector3.zero);
                    Assert.AreEqual(0, died, "a knock during the 2 s of safety does nothing");
                    Assert.IsFalse(items.TryCheatDeath(out _, out _), "once a run: the next fatal hit sinks the ship");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void ChestAndRewardItems_ReportTheirMultipliers()
        {
            var (items, _, config) = Rig("shark_tooth_necklace", "skeleton_key", "smugglers_ledger", "black_spot");
            using (config)
            {
                Assert.AreEqual(1.12f, items.HealthyTargetDamageMultiplier(out var threshold), 1e-4f);
                Assert.AreEqual(0.9f, threshold, 1e-4f);
                Assert.AreEqual(0.06f, items.FreeChestChance(), 1e-4f);
                Assert.AreEqual(1.2f, items.ChestPriceMultiplier(), 1e-4f);
                Assert.AreEqual(1.1f, items.ChestRarityBoost(), 1e-4f);
                Assert.AreEqual(1.1f, items.EliteRewardMultiplier(), 1e-4f);
                items.TryAddItem("skeleton_key");
                items.TryAddItem("smugglers_ledger");
                Assert.AreEqual(1f - 0.94f * 0.94f, items.FreeChestChance(), 1e-4f, "chance stacks per copy");
                Assert.AreEqual(1.2f, items.ChestPriceMultiplier(), 1e-4f, "the price rise does not stack");
                Assert.AreEqual(1.2f, items.ChestRarityBoost(), 1e-4f);
            }
            var (none, _, c2) = Rig();
            using (c2)
            {
                Assert.AreEqual(1f, none.HealthyTargetDamageMultiplier(out _), "without the items nothing changes");
                Assert.AreEqual(0f, none.FreeChestChance());
                Assert.AreEqual(1f, none.ChestPriceMultiplier());
                Assert.AreEqual(1f, none.EliteRewardMultiplier());
                Assert.IsFalse(none.TryCheatDeath(out _, out _));
            }
        }
    }
}
