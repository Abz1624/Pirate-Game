using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// 13 Oct (owner, after the hackathon): the four items and the Logbook the Art Guide designed and deferred (D33):
    /// Devil's Match, Hammerhead Helm, Queen Anne's Fuse, Storm in a Bottle and Chaos. The data follows the guide (chances,
    /// caps, stacking, rarity), every pool knows the content, and each effect does what its card says. Also the chained
    /// mace review fixes (prewarmed maces, recycled on clear, a reel that snaps home).
    /// </summary>
    public sealed class V5DesignedItemsTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static readonly string[] NewItems = { "devils_match", "hammerhead_helm", "queen_annes_fuse", "storm_in_a_bottle" };

        static ConfigSetRuntime Config() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        // ---- Data and pools --------------------------------------------------------------------------------------------

        [Test]
        public void TheDesignedContent_FollowsTheArtGuide_AndEveryPoolKnowsIt()
        {
            using var config = Config();
            var items = config.All<ItemConfig>().ToDictionary(i => i.itemId);
            var unlocks = config.Single<UnlockConfig>();
            var icons = IconCatalog.Load();
            foreach (var id in NewItems)
            {
                Assert.IsTrue(items.ContainsKey(id), id + " is in the config set");
                var item = items[id];
                Assert.IsTrue(item.inChestPool, id + " drops from chests");
                Assert.Greater(unlocks.RumCost(id), 0, id + " is sold in the Rum shop");
                Assert.IsTrue(RoguePirates.V5.Meta.LocalMetaServices.InAPool(config, id), id + ": the shop lists it");
                Assert.IsNotNull(icons.Item(id), id + " has its icon");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.displayName), id);
                StringAssert.DoesNotContain("—", item.description, id + ": no em dash in UI text");
                Assert.IsTrue(item.effects.All(e => e.active), id + ": its effects act");
            }
            // Rarity (Art Guide 5.2): Hammerhead Helm Uncommon; the other three Epic.
            Assert.AreEqual(1, items["hammerhead_helm"].rarity);
            foreach (var id in new[] { "devils_match", "queen_annes_fuse", "storm_in_a_bottle" }) Assert.AreEqual(3, items[id].rarity, id);

            var match = items["devils_match"].effects.Single(e => e.kind == ItemEffectKind.IgniteOnHit);
            Assert.AreEqual((0.2f, 2.5f), (match.value, match.radius), "20% to ignite; a 2.5 m blast");
            var knock = items["hammerhead_helm"].effects.Single(e => e.kind == ItemEffectKind.ReceivedKnockback);
            Assert.AreEqual((0.2f, 0.8f), (knock.value, knock.cap), "20% less knockback a copy, at most 80%");
            var ram = items["hammerhead_helm"].effects.Single(e => e.kind == ItemEffectKind.RamDamage);
            Assert.AreEqual((5f, 1f), (ram.threshold, ram.seconds), "above 5 m/s, once a second per target");
            var fuse = items["queen_annes_fuse"].effects.Single(e => e.kind == ItemEffectKind.KegChainBlast);
            Assert.AreEqual((0.25f, 4f, 0.2f, 3f), (fuse.value, fuse.threshold, fuse.seconds, fuse.cap), "25% of the damage, 4 m, 0.2 s, up to 3 generations");
            var storm = items["storm_in_a_bottle"].effects.Single(e => e.kind == ItemEffectKind.LightningBurst);
            Assert.AreEqual((20f, 5, 1f, 18f, 10f), (storm.threshold, storm.count, storm.spreadSeconds, storm.cap, storm.seconds),
                "every 20 chain events, 5 strikes over 1 s within 18 m, 10 s cooldown");

            var chaos = config.All<LogbookConfig>().Single(l => l.logbookId == "chaos");
            Assert.IsTrue(chaos.inSaltwindPool && chaos.randomStat, "Chaos is in the level-up pool and rolls a stat");
            Assert.Greater(unlocks.RumCost("chaos"), 0, "Chaos is sold in the Rum shop");
            Assert.IsNotNull(icons.Logbook("chaos"), "Chaos has its icon");
            var logbooks = config.All<LogbookConfig>().ToDictionary(l => l.logbookId);
            foreach (var from in chaos.randomStatFrom)
                Assert.IsTrue(logbooks.TryGetValue(from, out var l) && !l.randomStat && l.stat >= 0, "Chaos rolls from a real stat Logbook: " + from);
        }

        [Test]
        public void TheReEnabledWeapons_AreBackInEveryPool_AndEmberStartsWithHisFlamethrower()
        {
            using var config = Config();
            var unlocks = config.Single<UnlockConfig>();
            CollectionAssert.IsEmpty(unlocks.disabledWeaponIds ?? new string[0], "nothing is switched off (owner, 13 Oct)");
            var stage = config.Single<StageContentConfig>();
            foreach (var (id, price) in new[] { ("flamethrower", 80), ("siren_bell", 150), ("squid_ink_pots", 160), ("lighthouse_lantern", 200) })
            {
                Assert.IsTrue(unlocks.starterWeaponIds.Contains(id), id + " is a starter again");
                Assert.AreEqual(price, unlocks.weaponUnlocks.Single(u => u.weaponId == id).rumCost, id + " Rum price");
                Assert.AreEqual(price, stage.sharedProfile.weaponUnlocks.Single(u => u.contentId == id).marksCost, id + " marks price");
                Assert.IsTrue(stage.sharedProfile.starterWeaponIds.Contains(id), id + " is a starter in the stage profile too");
            }
            var ember = config.All<CaptainConfig>().Single(c => c.captainKey == "ember");
            Assert.AreEqual("flamethrower", ember.startingWeaponId);
            // Every evolution item drops from chests again (Cursed Chest Key: Pirate King's Hoard is reachable).
            var items = config.All<ItemConfig>().ToDictionary(i => i.itemId);
            foreach (var evo in config.All<WeaponEvolutionConfig>().Where(e => !string.IsNullOrEmpty(e.requiredItemId)))
                Assert.IsTrue(items[evo.requiredItemId].inChestPool, evo.evolutionId + ": " + evo.requiredItemId + " drops from chests");
        }

        // ---- Item module ---------------------------------------------------------------------------------------------

        sealed class StatSink : IStatModifierSink, IStatReader
        {
            public readonly PlayerStats Store = new PlayerStats();
            readonly StatResolver _r;
            public StatSink() { _r = new StatResolver(() => Store); }
            public void Add(string source, int stat, float additive, float multiplicative = 1f) => _r.Add(source, stat, additive, multiplicative);
            public void RemoveSource(string source) => _r.RemoveSource(source);
            public void SetBase(int stat, float value) => _r.SetBase(stat, value);
            public float Get(int stat) => _r.Get(stat);
        }

        sealed class ItemWorld : IItemWorld
        {
            public bool RunActive => true;
            public bool ShipExists => true;
            public Vector3 ShipForward => Vector3.forward;
            public Vector3 ShipPosition => Vector3.zero;
            public Vector3 ShipVelocity => Vector3.zero;
            public int Gold => 0;
            public IEnumerable<WeaponConfig> Weapons => Enumerable.Empty<WeaponConfig>();
        }

        static ItemModule Items(ConfigSetRuntime config, params string[] ids)
        {
            var items = new ItemModule(id => config.All<ItemConfig>().FirstOrDefault(i => i.itemId == id), new StatSink(), new ItemWorld());
            items.ResetForRun();
            foreach (var id in ids) items.TryAddItem(id);
            return items;
        }

        [Test]
        public void WithoutTheItems_NothingChanges()
        {
            using var config = Config();
            var items = Items(config, "gunners_gloves");
            Assert.AreEqual(0f, items.IgniteChance(out _, out _, out _));
            Assert.AreEqual(1f, items.ReceivedKnockbackMultiplier());
            Assert.IsFalse(items.RamDamage(out _, out _, out _, out _));
            Assert.AreEqual(0, items.KegChainGenerations(out _, out _, out _, out _));
            var bursts = 0;
            items.LightningBurstRequested += (_, _, _, _, _, _) => bursts++;
            for (var i = 0; i < 100; i++) items.NotifyLightningChainEvent();
            Assert.AreEqual(0, bursts);
        }

        [Test]
        public void DevilsMatch_GivesItsChance_AndItsBlast()
        {
            using var config = Config();
            var items = Items(config, "devils_match");
            Assert.AreEqual(0.2f, items.IgniteChance(out var damage, out var radius, out var id), 1e-6f);
            Assert.AreEqual(("devils_match", 2.5f), (id, radius));
            Assert.Greater(damage, 0f);
            items.TryAddItem("devils_match");
            Assert.AreEqual(0.2f, items.IgniteChance(out _, out _, out _), 1e-6f, "the guide gives it no stacking");
        }

        [Test]
        public void HammerheadHelm_TakesOffAFifthOfTheKnockbackPerCopy_UpToFourFifths_AndRams()
        {
            using var config = Config();
            var items = Items(config, "hammerhead_helm");
            var expected = new[] { 0.8f, 0.6f, 0.4f, 0.2f, 0.2f, 0.2f };
            for (var copies = 1; copies <= expected.Length; copies++)
            {
                Assert.AreEqual(expected[copies - 1], items.ReceivedKnockbackMultiplier(), 1e-5f, copies + " copies");
                items.TryAddItem("hammerhead_helm");
            }
            Assert.IsTrue(items.RamDamage(out var damage, out var minSpeed, out var interval, out var id));
            Assert.AreEqual(("hammerhead_helm", 5f, 1f), (id, minSpeed, interval));
            Assert.Greater(damage, 0f);
        }

        [Test]
        public void QueenAnnesFuse_AddsAGenerationPerCopy_UpToThree()
        {
            using var config = Config();
            var items = Items(config, "queen_annes_fuse");
            for (var copies = 1; copies <= 5; copies++)
            {
                Assert.AreEqual(Mathf.Min(3, copies), items.KegChainGenerations(out var share, out var reach, out var delay, out var id), copies + " copies");
                Assert.AreEqual((0.25f, 4f, 0.2f, "queen_annes_fuse"), (share, reach, delay, id));
                items.TryAddItem("queen_annes_fuse");
            }
        }

        [Test]
        public void StormInABottle_BurstsEveryTwentyChainEvents_WithATenSecondCooldown()
        {
            using var config = Config();
            var items = Items(config, "storm_in_a_bottle");
            var bursts = new List<(string id, int strikes, float spread, float range)>();
            items.LightningBurstRequested += (id, strikes, spread, range, damage, radius) => bursts.Add((id, strikes, spread, range));
            for (var i = 0; i < 19; i++) items.NotifyLightningChainEvent();
            Assert.AreEqual(0, bursts.Count, "19 chain events: not yet");
            items.NotifyLightningChainEvent();
            Assert.AreEqual(1, bursts.Count, "the 20th");
            Assert.AreEqual(("storm_in_a_bottle", 5, 1f, 18f), bursts[0]);
            for (var i = 0; i < 40; i++) items.NotifyLightningChainEvent();
            Assert.AreEqual(1, bursts.Count, "the 10 s cooldown holds the next burst");
            for (var i = 0; i < 200; i++) items.Tick(0.05f); // 10 s
            items.NotifyLightningChainEvent();
            Assert.AreEqual(2, bursts.Count, "after the cooldown the counted events burst at once");
        }

        // ---- Weapon side ---------------------------------------------------------------------------------------------

        sealed class Enemy { public bool Active = true; public Vector3 Position; public float HpFraction = 0.5f; }

        sealed class Enemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly List<(int index, string weaponId)> Hits = new List<(int, string)>();
            public readonly List<(int index, WeaponStatus status)> Statuses = new List<(int, WeaponStatus)>();
            public int Add(Vector3 p, float hp = 0.5f) { List.Add(new Enemy { Position = p, HpFraction = hp }); return List.Count - 1; }
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => List[i].Active;
            public Vector3 Position(int i) => List[i].Position;
            public Vector3 Velocity(int i) => Vector3.zero;
            public float Radius(int i) => 0.8f;
            public float Hp(int i) => 100f;
            public float HpFraction(int i) => List[i].HpFraction;
            public bool IsElite(int i) => false;
            public bool IsBoss(int i) => false;
            public int UnitId(int i) => 1000 + i;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = -1;
                var best = float.MaxValue;
                for (var i = 0; i < List.Count; i++)
                {
                    if (!List[i].Active) continue;
                    var d = Vector3.Distance(from, List[i].Position);
                    if (d > range || d >= best) continue;
                    best = d;
                    index = i;
                }
                return index >= 0;
            }
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null) => Hits.Add((index, weaponId));
            public void ApplyStatus(int index, WeaponStatus status, float strength) => Statuses.Add((index, status));
            public int HitsOn(int index) => Hits.Count(h => h.index == index);
        }

        sealed class Ship : IShipState
        {
            public Vector3 At, Moving;
            public bool Exists => true;
            public Vector3 Position => At;
            public Vector3 Velocity => Moving;
            public Vector3 MovementForward => Moving.sqrMagnitude > 0.01f ? Moving.normalized : Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => At + local;
        }

        /// <summary>No stat changes; the designed items' effects as set.</summary>
        sealed class Mods : IWeaponModifiers
        {
            public float Ignite;
            public int Generations;
            public bool Ram;
            public int ChainEvents;
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0f;
            public float CritDamage => 1.5f;
            public float EliteDamage => 1f;
            public float GlobalDamage => 1f;
            public float CaptainAdditiveDamage => 0f;
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f;
            public float ItemCritChanceBonus => 0f;
            public float ItemAttackSpeedMultiplier => 1f;
            public float FlameRangeMultiplier => 1f;
            public float BurnDamageMultiplier => 1f;
            public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier => 1f;
            public void ExplosionResolved() { }
            public int ExtraLightningJumps => 0;
            public float IgniteChance(out float blastDamage, out float blastRadius, out string ledgerId)
            { blastDamage = 20f; blastRadius = 2.5f; ledgerId = "devils_match"; return Ignite; }
            public int KegChainGenerations(out float damageShare, out float reach, out float delay, out string ledgerId)
            { damageShare = 0.25f; reach = 4f; delay = 0.2f; ledgerId = "queen_annes_fuse"; return Generations; }
            public bool RamDamage(out float damage, out float minSpeed, out float interval, out string ledgerId)
            { damage = 25f; minSpeed = 5f; interval = 1f; ledgerId = "hammerhead_helm"; return Ram; }
            public void LightningChainEvent() => ChainEvents++;
        }

        sealed class Hooks : IWeaponCombatHooks
        {
            public void BeginDamageResolution() { }
            public bool TryRegisterProcEvent() => true;
            public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) { }
            public void RecordEmission(string weaponId) { }
            public void RecordConcurrentTargets(string weaponId, int count) { }
            public void RecordControl(string weaponId, float seconds) { }
            public void SpawnFlamePatch(Vector3 position, string weaponId) { }
            public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) { }
            public bool TryResolveShipAim(string weaponId, out Vector3 direction) { direction = Vector3.forward; return false; }
            public bool ShipVersusShip => false;
            public bool IsPvP => false;
            public bool AutoFireDisabled => false;
        }

        sealed class Random : ICombatRandom
        {
            readonly System.Random _r = new System.Random(5);
            public int Draws;
            public float CombatFloat() { Draws++; return (float)_r.NextDouble(); }
        }

        sealed class Run : IRunStatus
        {
            public bool Exists => true;
            public float ElapsedSeconds => 10f;
            public bool IsPaused => false;
            public bool IsRunEnded => false;
            public bool IsGameplayActive => true;
            public bool IsBossFight => false;
            public int PlayerLevel => 1;
            public int Kills => 0;
        }

        sealed class Loadout : IWeaponLoadout
        {
            public readonly List<(object key, string id)> Entries = new List<(object, string)>();
            public int Count => Entries.Count;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Entries[i].key, WeaponId = Entries[i].id, Level = 1, DamageBonus = 1f,
                CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        sealed class Rig : System.IDisposable
        {
            public readonly ConfigSetRuntime Config = V5DesignedItemsTests.Config();
            public readonly Enemies Enemies = new Enemies();
            public readonly Ship Ship = new Ship();
            public readonly Mods Mods = new Mods();
            public readonly Random Random = new Random();
            public readonly Loadout Loadout = new Loadout();
            public readonly WeaponCombat Combat;
            public readonly WeaponManager Manager;

            public Rig()
            {
                Combat = new WeaponCombat
                {
                    Enemies = Enemies, Damage = Enemies, Ship = Ship, Mods = Mods, Hooks = new Hooks(), Random = Random,
                    Calculator = new DamageCalculator(Mods, Random),
                    Targeting = new WeaponTargetingService(Enemies, Ship),
                    Projectiles = new PlayerProjectiles(64, Enemies, Enemies, new Hooks(), null),
                    Rules = () => Config.All<WeaponRulesConfig>().First(),
                };
                Manager = new WeaponManager(Combat, Loadout, new Run(), Config);
            }

            public void Step(float seconds)
            {
                const float dt = 1f / 60f;
                for (var t = 0f; t < seconds - 1e-4f; t += dt)
                {
                    Ship.At += Ship.Moving * dt;
                    Manager.Tick(dt);
                }
            }

            public void Dispose() => Config.Dispose();
        }

        [Test]
        public void DevilsMatch_AHitMaySetTheEnemyBurning_ButNotItsOwnBlast_AndDrawsNothingWithoutIt()
        {
            using var rig = new Rig();
            var e = rig.Enemies.Add(new Vector3(3f, 0f, 0f));
            rig.Combat.AfterWeaponHit(e, "deck_cannons");
            Assert.AreEqual(0, rig.Random.Draws, "without the item no combat draw (runs without it replay as before)");
            CollectionAssert.IsEmpty(rig.Enemies.Statuses);
            rig.Mods.Ignite = 1f;
            rig.Combat.AfterWeaponHit(e, "deck_cannons");
            CollectionAssert.AreEqual(new[] { (e, WeaponStatus.Burn) }, rig.Enemies.Statuses, "a hit sets it burning");
            rig.Combat.AfterWeaponHit(e, "devils_match");
            Assert.AreEqual(1, rig.Enemies.Statuses.Count, "its own blast never ignites");
            // Its burning-death blast: 2.5 m around the dying enemy, on the next weapon tick.
            var near = rig.Enemies.Add(new Vector3(4.5f, 0f, 0f));
            var far = rig.Enemies.Add(new Vector3(9f, 0f, 0f));
            rig.Manager.ItemBlast("devils_match", new Vector3(3f, 0f, 0f), 20f, 2.5f, 0f);
            rig.Step(1f / 60f);
            Assert.AreEqual(1, rig.Enemies.HitsOn(near));
            Assert.AreEqual(0, rig.Enemies.HitsOn(far), "outside the 2.5 m blast");
            Assert.IsTrue(rig.Enemies.Hits.All(h => h.weaponId == "devils_match"), "credited to the item");

            // Over many hits the chance is about 20%.
            rig.Mods.Ignite = 0.2f;
            rig.Enemies.Statuses.Clear();
            for (var i = 0; i < 2000; i++) rig.Combat.AfterWeaponHit(e, "deck_cannons");
            Assert.That(rig.Enemies.Statuses.Count / 2000f, Is.InRange(0.17f, 0.23f));
        }

        [Test]
        public void HammerheadHelm_RamsADamagedEnemyAhead_AboveFiveMetresASecond_OnceASecond()
        {
            using var rig = new Rig();
            rig.Mods.Ram = true;
            var hurt = rig.Enemies.Add(new Vector3(0f, 0f, 1.5f), 0.5f);
            var whole = rig.Enemies.Add(new Vector3(0.5f, 0f, 1.5f), 1f);
            var behind = rig.Enemies.Add(new Vector3(0f, 0f, -1.5f), 0.5f);
            rig.Ship.Moving = new Vector3(0f, 0f, 4f); // too slow
            rig.Manager.Tick(1f / 60f);
            CollectionAssert.IsEmpty(rig.Enemies.Hits, "at 4 m/s nothing is rammed");
            rig.Ship.Moving = new Vector3(0f, 0f, 8f);
            rig.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, rig.Enemies.HitsOn(hurt), "the damaged enemy ahead is rammed");
            Assert.AreEqual(0, rig.Enemies.HitsOn(whole), "not an enemy at full hull");
            Assert.AreEqual(0, rig.Enemies.HitsOn(behind), "not one behind the bow");
            for (var f = 0; f < 50; f++) rig.Manager.Tick(1f / 60f);
            Assert.AreEqual(1, rig.Enemies.HitsOn(hurt), "once a second per target");
            for (var f = 0; f < 15; f++) rig.Manager.Tick(1f / 60f);
            Assert.AreEqual(2, rig.Enemies.HitsOn(hurt), "and again after a second");
            Assert.IsTrue(rig.Enemies.Hits.All(h => h.weaponId == "hammerhead_helm"));
        }

        [Test]
        public void QueenAnnesFuse_ABlastSetsOffTheNextAtTheNearestEnemyItMissed_PerGeneration()
        {
            using var rig = new Rig();
            var inBlast = rig.Enemies.Add(new Vector3(-2.5f, 0f, 0f)); // inside the keg blast, 7.5 m from the second (out of its reach)
            var second = rig.Enemies.Add(new Vector3(5f, 0f, 0f));  // 2 m past a 3 m blast's edge
            var third = rig.Enemies.Add(new Vector3(10f, 0f, 0f));  // 2 m past the second blast's edge
            var away = rig.Enemies.Add(new Vector3(-30f, 0f, 0f));
            rig.Combat.QueueChainBlast(Vector3.zero, 3f, 4f, 0.2f, 2, 10f, 1f, "queen_annes_fuse", new HashSet<int> { inBlast });
            rig.Step(0.1f);
            CollectionAssert.IsEmpty(rig.Enemies.Hits, "0.2 s later");
            rig.Step(0.15f);
            Assert.AreEqual(1, rig.Enemies.HitsOn(second), "the first secondary blast lands on the nearest enemy it missed");
            Assert.AreEqual(0, rig.Enemies.HitsOn(inBlast), "not on one the keg already hit");
            rig.Step(0.25f);
            Assert.AreEqual(1, rig.Enemies.HitsOn(third), "the second generation");
            rig.Step(1f);
            Assert.AreEqual(0, rig.Enemies.HitsOn(away), "two generations, then the chain ends");
            Assert.AreEqual(2, rig.Enemies.Hits.Count);
            Assert.IsTrue(rig.Enemies.Hits.All(h => h.weaponId == "queen_annes_fuse"));
        }

        [Test]
        public void StormInABottle_StrikesEnemiesInRange_OverItsSpread_AndChainHitsAreCounted()
        {
            using var rig = new Rig();
            var a = rig.Enemies.Add(new Vector3(5f, 0f, 0f));
            var b = rig.Enemies.Add(new Vector3(-8f, 0f, 3f));
            var c = rig.Enemies.Add(new Vector3(0f, 0f, 15f));
            var far = rig.Enemies.Add(new Vector3(30f, 0f, 0f));
            rig.Manager.ItemLightningBurst("storm_in_a_bottle", 5, 1f, 18f, 30f, 2.5f);
            rig.Step(0.5f);
            var early = rig.Enemies.Hits.Count;
            Assert.That(early, Is.InRange(2, 3), "spread over the second");
            rig.Step(0.6f);
            Assert.AreEqual(5, rig.Enemies.Hits.Count, "five strikes");
            Assert.AreEqual(0, rig.Enemies.HitsOn(far), "only enemies within 18 m");
            Assert.IsTrue(new[] { a, b, c }.All(i => rig.Enemies.HitsOn(i) >= 1), "each enemy in range is struck");

            // The Lightning Mast's chain counts toward the burst.
            rig.Loadout.Entries.Add((new object(), "lightning_mast"));
            rig.Manager.SyncLoadout();
            rig.Manager.FireSlot(0);
            Assert.Greater(rig.Mods.ChainEvents, 0, "a lightning chain hit is counted");
        }

        // ---- Chaos -----------------------------------------------------------------------------------------------------

        sealed class OfferRng : IOfferRandom
        {
            readonly System.Random _r;
            public OfferRng(int seed) { _r = new System.Random(seed); }
            public float LootFloat() => (float)_r.NextDouble();
        }

        sealed class Ctx : IOfferContext
        {
            public bool IsWeaponOfferable(string weaponId) => true;
            public bool IsWeaponUnlocked(string weaponId) => true;
            public bool HasItem(string itemId) => false;
            public bool IsEvolved(string weaponId) => false;
        }

        sealed class State : IOfferState
        {
            public List<string> Weapons = new List<string> { "deck_cannons" }, Logbooks = new List<string>();
            public IReadOnlyList<string> OwnedWeapons => Weapons;
            public IReadOnlyList<string> OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => 1;
            public int LogbookLevel(string id) => 0;
        }

        static OfferGenerator Generator(ConfigSetRuntime set) => new OfferGenerator(() => set.Single<ProgressionConfig>(), () => set.Single<OfferWeightsConfig>(),
            () => OfferGenerator.InDatabaseOrder(set.All<WeaponConfig>()), () => OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()),
            () => set.All<WeaponEvolutionConfig>().ToList(), () => true);

        [Test]
        public void Chaos_TheCardRollsItsStatFromTheRunsLootStream_Deterministically_AndShowsIt()
        {
            using var config = Config();
            var chaos = config.All<LogbookConfig>().Single(l => l.logbookId == "chaos");
            var gen = Generator(config);
            // The roll: one Loot draw, the same seed gives the same Logbook, and different seeds reach several.
            var rolled = new HashSet<string>();
            for (var seed = 1; seed <= 200; seed++)
            {
                var first = gen.RollRandomStat(chaos, new OfferRng(seed));
                Assert.IsNotNull(first);
                Assert.AreEqual(first.logbookId, gen.RollRandomStat(chaos, new OfferRng(seed)).logbookId, "deterministic");
                CollectionAssert.Contains(chaos.randomStatFrom, first.logbookId);
                rolled.Add(first.logbookId);
            }
            Assert.Greater(rolled.Count, chaos.randomStatFrom.Length / 2, "the roll reaches most of its Logbooks");

            // Dealt cards: a Chaos card carries the stat it rolled, the same for the same seed.
            var cards = 0;
            for (var seed = 1; seed <= 400 && cards < 3; seed++)
            {
                var offers = gen.Generate(new OfferRng(seed), new Ctx(), new State(), 0f, false, new OfferPlayer(100f, 100f, 1f, 6f), 30);
                foreach (var o in offers.Where(o => o.Kind == OfferKind.Logbook && o.Id == "chaos"))
                {
                    cards++;
                    Assert.IsFalse(string.IsNullOrEmpty(o.RolledFrom), "the card is rolled before it is taken");
                    var again = gen.Generate(new OfferRng(seed), new Ctx(), new State(), 0f, false, new OfferPlayer(100f, 100f, 1f, 6f), 30)
                        .Single(x => x.Kind == OfferKind.Logbook && x.Id == "chaos");
                    Assert.AreEqual(o.RolledFrom, again.RolledFrom, "seed " + seed);
                }
            }
            Assert.Greater(cards, 0, "Chaos is dealt on level-ups");

            // The card names the rolled stat before it is taken.
            var card = OfferCards.ToCard(new Offer { Kind = OfferKind.Logbook, Id = "chaos", Rarity = 0, NextLevel = 1, IsNew = true, RolledFrom = "gunnery_manual" }, config);
            Assert.AreEqual("Chaos", card.title);
            var gunnery = config.All<LogbookConfig>().Single(l => l.logbookId == "gunnery_manual");
            StringAssert.StartsWith("Chaos: " + RoguePirates.V4.Foundation.UI.StatDisplay.FormatLogbookBonus((StatId)gunnery.stat, gunnery.perLevelAdditive), card.effectLines[0]);
        }

        sealed class ProgressionRun : IProgressionRun
        {
            public RunState Current = RunState.Playing;
            public bool Exists => true;
            public bool IsRunEnded => false;
            public RunState State => Current;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) => Current = state;
            public void EnterPlaying() => Current = RunState.Playing;
        }

        [Test]
        public void Chaos_ARankAddsTheRolledStat_AndARankWithoutACardRollsOnTheRunStream()
        {
            using var config = Config();
            var stats = new StatSink();
            var module = new ProgressionModule(() => config.Single<ProgressionConfig>(), id => config.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id),
                id => config.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id), Generator(config), new OfferRng(9), new Ctx(), stats, stats, new ProgressionRun(), null);
            module.BeginRun(config.Single<ProgressionConfig>().firstLevelUpWeaponId, 6, 1f);
            var gunnery = config.All<LogbookConfig>().Single(l => l.logbookId == "gunnery_manual");
            var before = stats.Get(gunnery.stat);
            module.Select(new Offer { Kind = OfferKind.Logbook, Id = "chaos", Rarity = 0, CurrentLevel = 0, NextLevel = 1, IsNew = true, RolledFrom = "gunnery_manual" });
            Assert.AreEqual(before + gunnery.perLevelAdditive, stats.Get(gunnery.stat), 1e-4f, "the card's stat, at the card's rarity");
            CollectionAssert.AreEqual(new[] { "gunnery_manual" }, module.ChaosRolls);
            Assert.IsTrue(module.RaisePick(false, "chaos"), "a PvP auto level-up raises Chaos too");
            Assert.AreEqual(2, module.ChaosRolls.Count, "and rolls its stat then");
            CollectionAssert.Contains(config.All<LogbookConfig>().Single(l => l.logbookId == "chaos").randomStatFrom, module.ChaosRolls[1]);
        }

        // ---- Chained mace review fixes -------------------------------------------------------------------------------

        [Test]
        public void ChainedMaces_ArePrewarmed_RecycledOnClear_AndSnapHomeWhenTheShipOutrunsTheReel()
        {
            using var rig = new Rig();
            var free = new ChainedMaceBehavior(rig.Combat).FreeCount;
            Assert.AreEqual(ChainedMaceBehavior.PrewarmPerSlot * ChainedMaceBehavior.PrewarmSlots, free, "4 maces for each of 6 slots, made up front");
            rig.Enemies.Add(new Vector3(0f, 0f, 6f));
            rig.Loadout.Entries.Add((new object(), "anchor_flail"));
            rig.Manager.SyncLoadout();
            Assert.IsTrue(rig.Manager.FireSlot(0));
            Assert.Greater(rig.Manager.Maces.Count, 0);
            rig.Manager.ClearInFlight(false);
            Assert.AreEqual(0, rig.Manager.Maces.Count, "cleared");

            // A boosting ship (34 m/s) outruns the 28 m/s reel: the mace snaps home instead of blocking the next throw.
            Assert.IsTrue(rig.Manager.FireSlot(0));
            var mace = rig.Manager.Maces[0];
            rig.Ship.Moving = new Vector3(0f, 0f, -34f);
            rig.Step(mace.MaxOutSeconds + ChainedMaceBehavior.MaxReturnSeconds(mace) + 0.2f);
            Assert.AreEqual(0, rig.Manager.Maces.Count, "back home within the out time and the longest reel");
        }

        [Test]
        public void ChainedMace_HitsPerFiring_WaitsForTheRoundTrip()
        {
            using var config = Config();
            var flail = config.All<WeaponConfig>().Single(w => w.weaponId == "anchor_flail");
            Assert.AreEqual(1f, WeaponStatResolver.HitsPerFiring(flail), 1e-6f, "today's cooldown is longer than the round trip");
            flail.baseCooldown = 0.05f;
            Assert.Less(WeaponStatResolver.HitsPerFiring(flail), 1f, "a throw cannot repeat before its maces are back");
            var director = config.Single<DirectorConfig>();
            Assert.Greater(director.behaviorCoverage.Length, (int)ConfigEnums.WeaponBehavior.ChainedMace, "the director weighs chained maces");
        }
    }
}
