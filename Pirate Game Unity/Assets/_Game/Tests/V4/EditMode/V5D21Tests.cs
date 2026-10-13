using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, D21): the owner's answers to the open questions, one targeted test each, on the real configs:
    /// the Knockback card only where knockback works (Q17), the Harpoon's tar as a real slow (Q23), the Squid Ink Pots
    /// that replaced the Ramming Prow (Q19: auto-fires, the back of the pack, its slow, milestones and cards), one Luck
    /// scale (Q27), elite damage on the boss (Q32) and the 1024 texture cap on every web build (Q34). The enemy world's
    /// side (the crab like the boss, Q24; dashes, Q25) is in V4EnemyTickM8Tests.
    /// </summary>
    public sealed class V5D21Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string InkPots = "squid_ink_pots";
        const float Dt = 1f / 60f;

        sealed class Enemy { public bool Active = true, Boss, Elite; public Vector3 Position, Velocity; }

        sealed class Enemies : IEnemyQuery, IEnemyDamageSink
        {
            public readonly List<Enemy> List = new List<Enemy>();
            public readonly List<(int index, float amount)> Hits = new List<(int, float)>();
            public readonly List<(int index, float fraction, float seconds)> Slows = new List<(int, float, float)>();
            public int Add(Vector3 p, bool boss = false) { List.Add(new Enemy { Position = p, Boss = boss }); return List.Count - 1; }
            public bool Exists => true;
            public int Capacity => List.Count;
            public bool IsActive(int i) => List[i].Active;
            public Vector3 Position(int i) => List[i].Position;
            public Vector3 Velocity(int i) => List[i].Velocity;
            public float Radius(int i) => 0.8f;
            public float Hp(int i) => 1e6f;
            public bool IsElite(int i) => List[i].Elite;
            public bool IsBoss(int i) => List[i].Boss;
            public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
            {
                index = -1;
                var best = float.MaxValue;
                for (var i = 0; i < List.Count; i++)
                {
                    var d = Vector3.Distance(from, List[i].Position);
                    if (!List[i].Active || d > range || d >= best) continue;
                    best = d;
                    index = i;
                }
                return index >= 0;
            }
            public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null) => Hits.Add((index, amount));
            public void ApplyStatus(int index, WeaponStatus status, float strength) { }
            public void ApplySlow(int index, float fraction, float seconds) => Slows.Add((index, fraction, seconds));
        }

        sealed class Ship : IShipState
        {
            public bool Exists => true;
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 MovementForward => Vector3.forward;
            public Vector3 TransformPoint(Vector3 local) => local;
        }

        sealed class Mods : IWeaponModifiers
        {
            public float Elite = 1f;
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0f;
            public float CritDamage => 1.5f;
            public float EliteDamage => Elite;
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

        sealed class NoCrit : ICombatRandom { public float CombatFloat() => 0.999f; }

        // V5 M8z (8 Oct, close): the Luck test's real rolls. Every loot float is one value (a grid point), loot ints cycle
        // (a chest retrying an ineligible item picks another of the same rarity).
        const int Grid = 4000;

        sealed class FixedRoll : RoguePirates.V4.Progression.IOfferRandom, IStageRandom
        {
            readonly float _value;
            int _next;
            public FixedRoll(float value) => _value = value;
            public float LootFloat() => _value;
            public int LootInt(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : minInclusive + _next++ % (maxExclusive - minInclusive);
            public int MapInt(int minInclusive, int maxExclusive) => minInclusive;
        }

        /// <summary>The share of grid rolls in (0, 1) for which <paramref name="hit"/> holds.</summary>
        static float GridShare(System.Func<float, bool> hit)
        {
            var hits = 0;
            for (var k = 0; k < Grid; k++) if (hit((k + 0.5f) / Grid)) hits++;
            return hits / (float)Grid;
        }

        sealed class LuckRun : IStageRun
        {
            public float Luck { get; set; }
            public bool Exists => true;
            public bool IsRunEnded => false;
            public bool IsPaused => false;
            public RoguePirates.V4.Foundation.Core.PauseReason PauseReason => RoguePirates.V4.Foundation.Core.PauseReason.None;
            public RoguePirates.V4.Foundation.Core.RunState State => RoguePirates.V4.Foundation.Core.RunState.Playing;
            public float ElapsedSeconds => 300f;
            public int Gold => 1000000;
            public int RunSeed => 1;
            public float MaxHull => 100f;
            public void SetState(RoguePirates.V4.Foundation.Core.RunState state) { }
            public void SetPause(RoguePirates.V4.Foundation.Core.PauseReason reason) { }
            public void AddGold(int amount) { }
            public bool TrySpendGold(int amount) => true;
            public void SecureObjective() { }
            public void CompleteRun(bool victory) { }
            public void AddStatModifier(int stat, float add, float multiply) { }
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
            public readonly object Key = new object();
            public string Id, Evolution;
            public int Level = 1;
            public float Duration = 1f, Slow;
            public int Count => 1;
            public LoadoutEntry Get(int i) => new LoadoutEntry { Key = Key, WeaponId = Id, EvolutionId = Evolution, Level = Level, DamageBonus = 1f,
                CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = Duration, KnockbackMult = 1f, SlowBonus = Slow };
            public void Mirror(int i, in LoadoutMirror state) { }
        }

        sealed class Rig : System.IDisposable
        {
            public readonly ConfigSetRuntime Config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            public readonly Enemies Enemies = new Enemies();
            public readonly Loadout Loadout = new Loadout();
            public readonly Mods Mods = new Mods();
            public readonly WeaponCombat Combat;
            public readonly WeaponManager Manager;

            public Rig(string weaponId)
            {
                Loadout.Id = weaponId;
                var ship = new Ship();
                var random = new NoCrit();
                Combat = new WeaponCombat
                {
                    Enemies = Enemies, Damage = Enemies, Ship = ship, Mods = Mods, Hooks = new Hooks(), Random = random,
                    Calculator = new DamageCalculator(Mods, random),
                    Targeting = new WeaponTargetingService(Enemies, ship),
                    Projectiles = new PlayerProjectiles(64, Enemies, Enemies, new Hooks(), null),
                    Rules = () => Config.All<WeaponRulesConfig>().First(),
                };
                Manager = new WeaponManager(Combat, Loadout, new Run(), Config);
            }

            public WeaponConfig Weapon(string id) => Config.All<WeaponConfig>().Single(w => w.weaponId == id);

            public void Play(float seconds)
            {
                for (var t = 0f; t < seconds; t += Dt)
                {
                    Manager.Tick(Dt);
                    Combat.Projectiles.Tick(Dt);
                    foreach (var e in Enemies.List) e.Position += e.Velocity * Dt;
                }
            }

            public void Dispose() => Config.Dispose();
        }

        // ---- Q17: the Knockback card only where knockback works -------------------------------------------------------

        [Test]
        public void TheKnockbackCard_IsOnlyOffered_ForWeaponsThatKnockBack()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var withCard = new List<string>();
            foreach (var w in config.All<WeaponConfig>())
            {
                if (!(w.upgradeAxes ?? new WeaponUpgradeAxis[0]).Any(a => a.axis == ConfigEnums.UpgradeAxis.Knockback)) continue;
                withCard.Add(w.weaponId);
                // The behaviours that read KnockbackMult: an Orbit's or an Aura's knockbackMeters.
                var knocks = (w.behavior == ConfigEnums.WeaponBehavior.Orbit && w.orbit.knockbackMeters > 0f)
                             || (w.behavior == ConfigEnums.WeaponBehavior.Aura && w.aura.knockbackMeters > 0f)
                             || (w.behavior == ConfigEnums.WeaponBehavior.ChainedMace && w.mace.knockbackMeters > 0f); // Hackathon (12 Oct)
                Assert.IsTrue(knocks, $"{w.weaponId} offers the Knockback card but does not knock back");
            }
            Debug.Log("[V5 D21 Q17] Knockback card on: " + string.Join(", ", withCard));
            CollectionAssert.IsNotEmpty(withCard, "the weapons that knock back keep it (the Anchor Flail)");
        }

        // ---- Q23: the Harpoon's tar is a real slow --------------------------------------------------------------------

        [Test]
        public void TheHarpoonsTar_SlowsWhatItHits_ForTheTarsSeconds()
        {
            using var rig = new Rig("harpoon_gun");
            var harpoon = rig.Weapon("harpoon_gun");
            Assert.Greater(harpoon.projectile.tarSlowSeconds, 0f, "the tar slows (D21)");
            var target = rig.Enemies.Add(new Vector3(0f, 0f, 6f));
            rig.Play(1.5f);
            Assert.IsTrue(rig.Enemies.Hits.Any(h => h.index == target), "the harpoon hit");
            Assert.IsNotEmpty(rig.Enemies.Slows, "and its tar slowed");
            Assert.IsTrue(rig.Enemies.Slows.All(s => s.index == target && Mathf.Approximately(s.fraction, harpoon.projectile.tarSlow)
                && Mathf.Approximately(s.seconds, harpoon.projectile.tarSlowSeconds)), "by the tar's strength for its seconds (the enemy world halves it on bosses)");
        }

        // ---- Q19: the Squid Ink Pots -----------------------------------------------------------------------------------

        /// <summary>A line of enemies straight ahead, 8 to 20 m out (3 m apart), coming at the ship.</summary>
        static void Line(Rig rig, float speed = 0f)
        {
            foreach (var z in new[] { 8f, 11f, 14f, 17f, 20f })
            {
                var i = rig.Enemies.Add(new Vector3(0f, 0f, z));
                rig.Enemies.List[i].Velocity = Vector3.back * speed;
            }
        }

        [Test]
        public void TheInkPots_FireOnTheirOwn_AtTheBackOfThePack_AndSlowOnlyThere()
        {
            using var rig = new Rig(InkPots);
            var w = rig.Weapon(InkPots);
            Assert.IsTrue(w.predictedArea.rearTargeting && w.predictedArea.slowFraction > 0f, "a rear-targeting slowing weapon");
            Line(rig);
            rig.Manager.Tick(Dt); // no input: the weapon's own tick fires it
            Assert.AreEqual(1, rig.Combat.Strikes.Count, "one pot lobbed with no input");
            var landing = rig.Combat.Strikes[0].Position;
            Assert.AreEqual(20f, landing.z, 0.01f, "on the farthest enemy of the pack (20 m), not the nearest (8 m)");
            rig.Play(w.predictedArea.impactDelaySeconds + 1.2f);
            var slowed = rig.Enemies.Slows.Select(s => s.index).Distinct().OrderBy(i => i).ToList();
            CollectionAssert.AreEqual(new[] { 3, 4 }, slowed, "only the back of the pack (17 and 20 m) is slowed; 8, 11 and 14 m are not");
            Assert.IsTrue(rig.Enemies.Slows.All(s => Mathf.Approximately(s.fraction, w.predictedArea.slowFraction) && Mathf.Approximately(s.seconds, w.predictedArea.slowSeconds)),
                "the configured slow and its seconds");
        }

        [Test]
        public void TheInkPots_LeadAMovingPack_AndTheSlickKeepsSlowing_ForItsSeconds()
        {
            using var rig = new Rig(InkPots);
            var w = rig.Weapon(InkPots);
            var p = w.predictedArea;
            Line(rig, 4f);
            rig.Manager.Tick(Dt);
            Assert.AreEqual(20f - 4f * p.impactDelaySeconds, rig.Combat.Strikes[0].Position.z, 0.1f, "the pot lands where the rear enemy will be");
            rig.Manager.Slots[0].CooldownTimer = 100f; // one pot
            rig.Play(p.impactDelaySeconds + 0.05f);
            Assert.AreEqual(1, rig.Combat.Whirlpools.Count, "the pot left a slick");
            var ticks = rig.Combat.Whirlpools[0].Ticks;
            Assert.AreEqual(WeaponStatResolver.WhirlpoolTicks(p), ticks);
            foreach (var e in rig.Enemies.List) e.Velocity = Vector3.zero; // hold the back of the pack in the slick
            rig.Enemies.Slows.Clear();
            rig.Play(p.whirlpoolSeconds + 0.2f);
            Assert.AreEqual(0, rig.Combat.Whirlpools.Count, "the slick is gone after its seconds");
            var rear = rig.Enemies.List.Count - 1;
            Assert.AreEqual(ticks, rig.Enemies.Slows.Count(s => s.index == rear), "every tick slows what is inside it");
        }

        [Test]
        public void TheInkPots_Level1Dps_IsTheEqualRule_AndPullsNothing()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var w = config.All<WeaponConfig>().Single(x => x.weaponId == InkPots);
            Assert.AreEqual(14.78f, V5StarterDpsTests.LevelOneSingleTargetDps(w), 14.78f * 0.05f, "D20: the same level-1 single-target DPS");
            Assert.AreEqual(0f, w.predictedArea.pullMetersPerSecond, "it slows, it does not drag");
            var axes = w.upgradeAxes.Select(a => a.axis).ToList();
            CollectionAssert.AreEquivalent(new[] { ConfigEnums.UpgradeAxis.Duration, ConfigEnums.UpgradeAxis.Slow, ConfigEnums.UpgradeAxis.Size, ConfigEnums.UpgradeAxis.Cooldown }, axes,
                "duration, slow strength, area, cooldown");
            CollectionAssert.AreEqual(new[] { 4, 8, 12 }, w.milestones.Select(m => m.level), "rank 4/8/12 milestones (Q20)");
            Assert.IsTrue(config.Single<UnlockConfig>().starterWeaponIds.Contains(InkPots), "a free starter (D18)");
            Assert.IsFalse(string.IsNullOrWhiteSpace(w.displayName), "its name shows on the card");
        }

        [Test]
        public void TheInkPots_Milestones_AndCards_ChangeItInPlay()
        {
            int Pots(int level)
            {
                using var rig = new Rig(InkPots);
                rig.Loadout.Level = level;
                foreach (var z in new[] { 8f, 20f }) rig.Enemies.Add(new Vector3(0f, 0f, z)); // two packs, far apart
                rig.Manager.Tick(Dt);
                return rig.Combat.Strikes.Count;
            }
            Assert.AreEqual(1, Pots(3));
            Assert.AreEqual(2, Pots(4), "rank 4: a second pot, on the next pack back");

            float Radius(int level)
            {
                using var rig = new Rig(InkPots);
                rig.Loadout.Level = level;
                rig.Enemies.Add(new Vector3(0f, 0f, 12f));
                rig.Manager.Tick(Dt);
                return rig.Combat.Strikes[0].Radius;
            }
            Assert.AreEqual(Radius(7) * 1.25f, Radius(8), 1e-3f, "rank 8: 25% wider");

            (float slow, float seconds, int ticks) Slick(int level, float slowCards, float durationCards)
            {
                using var rig = new Rig(InkPots);
                rig.Loadout.Level = level;
                rig.Loadout.Slow = slowCards;
                rig.Loadout.Duration = durationCards;
                rig.Enemies.Add(new Vector3(0f, 0f, 12f));
                rig.Manager.Tick(Dt);
                rig.Manager.Slots[0].CooldownTimer = 100f;
                rig.Play(rig.Weapon(InkPots).predictedArea.impactDelaySeconds + 0.05f);
                var s = rig.Combat.Whirlpools[0];
                return (s.SlowFraction, s.SlowSeconds, s.Ticks);
            }
            var plain = Slick(1, 0f, 1f);
            Assert.AreEqual(plain.slow + 0.08f, Slick(1, 0.08f, 1f).slow, 1e-4f, "Slow cards add to the slow");
            Assert.AreEqual(plain.slow + 0.1f, Slick(12, 0f, 1f).slow, 1e-4f, "rank 12: +10% slow");
            var longer = Slick(1, 0f, 1.5f);
            Assert.AreEqual(plain.seconds * 1.5f, longer.seconds, 1e-4f, "Duration cards lengthen the slow");
            Assert.AreEqual(Mathf.FloorToInt(plain.ticks * 1.5f), longer.ticks, "and the slick");
        }

        [Test]
        public void TheInkPots_Evolution_IsBlackTide_WithTheCuttlefishBone()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var evo = config.All<WeaponEvolutionConfig>().Single(e => e.baseWeapon != null && e.baseWeapon.weaponId == InkPots);
            Assert.AreEqual(ConfigEnums.EvolutionEffect.Empowered, evo.effect);
            // D21 follow-up (9 Oct, owner): the Prow's Iron Figurehead became the ink-themed Cuttlefish Bone (same stats).
            Assert.AreEqual("cuttlefish_bone", evo.requiredItemId, "the Ink Pots' own evolution item");
            var item = config.All<ItemConfig>().Single(i => i.itemId == "cuttlefish_bone");
            Assert.IsTrue(item.inChestPool, "the Cuttlefish Bone drops from chests");
            Assert.IsFalse(config.All<ItemConfig>().Any(i => i.itemId == "iron_figurehead"), "the Iron Figurehead is gone");
            Assert.Greater(evo.empower.extraSlow, 0f, "the evolution slows harder");
            Assert.IsFalse(config.All<WeaponConfig>().Any(w => w.weaponId == "ramming_prow"), "the Ramming Prow is gone");
            Assert.IsFalse(config.All<WeaponEvolutionConfig>().Any(e => e.evolutionId == "ironclad_ram"), "and its evolution");
            var unlocks = config.Single<UnlockConfig>();
            Assert.IsFalse(unlocks.starterWeaponIds.Contains("ramming_prow") || unlocks.weaponUnlocks.Any(u => u.weaponId == "ramming_prow"), "and its unlock entries");
        }

        [Test]
        public void TheWeaponCards_ShowTheSlow_AndTheMilestone()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var w = config.All<WeaponConfig>().Single(x => x.weaponId == InkPots);
            var lines = RoguePirates.V4.Presentation.Hud.CardStatLines.NewWeapon(w);
            Assert.IsTrue(lines.Any(l => l.StartsWith("Slow ")), "the new-weapon card shows its slow: " + string.Join(" | ", lines));
        }

        // ---- Q32: Admiral (elite damage) on the boss ----------------------------------------------------------------

        [Test]
        public void EliteDamage_AppliesToTheBoss_ThroughTheWeapons()
        {
            float Damage(bool boss)
            {
                using var rig = new Rig("deck_cannons");
                rig.Mods.Elite = 2f; // Admiral's elite damage
                rig.Enemies.Add(new Vector3(0f, 0f, 5f), boss);
                rig.Play(0.6f);
                Assert.IsNotEmpty(rig.Enemies.Hits);
                return rig.Enemies.Hits[0].amount;
            }
            Assert.AreEqual(Damage(false) * 2f, Damage(true), 1e-3f, "the boss takes the elite damage bonus (Q32)");
        }

        // ---- Q27: one Luck scale ----------------------------------------------------------------------------------------

        [Test]
        public void Luck_IsOneFraction_ForCardsAndChests()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var doubloon = config.All<ItemConfig>().Single(i => i.itemId == "lucky_doubloon");
            var luck = doubloon.statModifiers.Single(m => m.stat == (int)RoguePirates.V4.Foundation.Core.StatId.Luck).additive;
            Assert.AreEqual(0.03f, luck, 1e-6f, "the data: +3% Luck as a fraction");
            Assert.AreEqual(1.03f, ChestEconomy.LuckBoost(luck), 1e-6f, "chests: +3% to the Uncommon-and-better weights (was +0.03%)");
            // V5 M8z (8 Oct, close): the real rolls, not the formula. Level-up cards (OfferGenerator.RollRarity): weights ×
            // (1 + Luck)^(luckExponent × tier), Legendary clamped, on the same fraction.
            var p = config.Single<ProgressionConfig>();
            var offers = new RoguePirates.V4.Progression.OfferGenerator(() => p, () => config.Single<OfferWeightsConfig>(),
                () => RoguePirates.V4.Progression.OfferGenerator.InDatabaseOrder(config.All<WeaponConfig>()),
                () => RoguePirates.V4.Progression.OfferGenerator.InDatabaseOrder(config.All<LogbookConfig>()),
                () => config.All<WeaponEvolutionConfig>().ToList(), () => true);
            float CardShare(float l) => GridShare(r => offers.RollRarity(new FixedRoll(r), l) >= 1);
            float ExpectedCardShare(float l)
            {
                var w = new float[5];
                var before = 0f;
                for (var i = 0; i < 5; i++) { w[i] = p.rarityBaseWeights[i] * Mathf.Pow(1f + l, p.luckExponent * i); before += w[i]; }
                if (w[4] / before > p.legendaryMaxShare) w[4] = before * p.legendaryMaxShare;
                var total = w.Sum();
                return (total - w[0]) / total;
            }
            Assert.AreEqual(ExpectedCardShare(luck), CardShare(luck), 2f / Grid, "cards: Uncommon-or-better at +3% Luck, as a fraction");
            Assert.Greater(CardShare(luck), CardShare(0f) + 10f / Grid, "cards: +3% Luck raises the share");
            Assert.Greater(Mathf.Abs(CardShare(luck) - ExpectedCardShare(luck / 100f)), 10f / Grid, "cards: not the old points scale");

            // Chests (ChestEconomy.RollSalvageItem through PlanChest): Uncommon and better × (1 + Luck), Rare+ × the depth boost.
            var stage = config.Single<StageContentConfig>();
            var allWeapons = config.All<WeaponConfig>().Select(x => x.weaponId).ToArray();
            var rarityOf = config.All<ItemConfig>().ToDictionary(i => i.itemId, i => i.rarity);
            Assert.IsTrue(config.All<ItemConfig>().Any(i => i.inChestPool && i.rarity == 1), "the chest pool has Uncommon items (no fallback to Common)");
            float ChestShare(float l) => GridShare(r =>
            {
                var plan = new ChestEconomy(() => config, new LuckRun { Luck = l }, new FixedRoll(r), () => null, () => null).PlanChest(allWeapons);
                Assert.IsTrue(plan.Valid, plan.Error);
                return rarityOf[plan.ItemId] >= 1;
            });
            float ExpectedChestShare(float l)
            {
                var weights = stage.chestRarityWeights;
                var depth = 1f + stage.chestRareBoostPerStageDepth * Mathf.Max(0, stage.chestStageDepth - 1);
                float common = weights[0], better = 0f;
                for (var i = 1; i < weights.Length; i++) better += weights[i] * (i >= 2 ? depth : 1f) * (1f + l);
                return better / (common + better);
            }
            Assert.AreEqual(ExpectedChestShare(luck), ChestShare(luck), 2f / Grid, "chests: Uncommon-or-better at +3% Luck, as a fraction");
            Assert.Greater(ChestShare(luck), ChestShare(0f) + 10f / Grid, "chests: +3% Luck raises the share");
            Assert.Greater(Mathf.Abs(ChestShare(luck) - ExpectedChestShare(luck / 100f)), 10f / Grid, "chests: not the old points scale (V3 read luck / 100)");
            // Every Luck source in the data is on the fraction scale (no point values left).
            foreach (var item in config.All<ItemConfig>())
                foreach (var m in item.statModifiers ?? new ItemStatModifier[0])
                    if (m.stat == (int)RoguePirates.V4.Foundation.Core.StatId.Luck) Assert.Less(m.additive, 0.5f, item.itemId + " Luck is a fraction");
            foreach (var l in config.All<LogbookConfig>())
                if (l.stat == (int)RoguePirates.V4.Foundation.Core.StatId.Luck) Assert.Less(l.perLevelAdditive, 0.5f, l.logbookId + " Luck is a fraction");
            Assert.AreEqual("+5% Luck", RoguePirates.V4.Foundation.UI.StatDisplay.FormatLogbookBonus(RoguePirates.V4.Foundation.Core.StatId.Luck, 0.05f), "shown as a percentage");
        }

        // ---- Q34: the 1024 texture cap on every web build -------------------------------------------------------------

        [Test]
        public void EveryWebBuild_CapsTexturesAt1024()
        {
            Assert.AreEqual(1024, RoguePirates.V4.Editor.V5WebGLProfiles.TextureCapFor(RoguePirates.V4.Editor.V5WebGLProfiles.Profile.Mobile));
            Assert.AreEqual(1024, RoguePirates.V4.Editor.V5WebGLProfiles.TextureCapFor(RoguePirates.V4.Editor.V5WebGLProfiles.Profile.Desktop), "desktop too (D21, Q34)");
        }
    }
}
