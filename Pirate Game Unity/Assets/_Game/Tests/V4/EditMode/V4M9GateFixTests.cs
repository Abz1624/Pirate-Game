using System;
using System.Collections.Generic;
using System.Linq;
using HighSeas.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M9 gate (28 Sep): the fixes approved at the gate — R-31, R-40, R-43, R-49, R-84/R-102 — each on by default and off
    /// under the v3-parity preset. Targeted module tests (no runs).
    /// </summary>
    [Category("V4")]
    public sealed class V4M9GateFixTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        static ConfigSetRuntime Parity()
        {
            var runtime = Default();
            var errors = new List<string>();
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("; ", errors));
            return runtime;
        }

        static ConfigSetRuntime Set(bool parity) => parity ? Parity() : Default();

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

        // ---- R-31 ---------------------------------------------------------------------------------------------------

        sealed class Rng : IOfferRandom
        {
            readonly System.Random _r;
            public Rng(int seed) { _r = new System.Random(seed); }
            public float LootFloat() => (float)_r.NextDouble();
        }

        sealed class Ctx : IOfferContext
        {
            public bool Items = true;
            public bool IsWeaponOfferable(string weaponId) => true;
            public bool IsWeaponUnlocked(string weaponId) => true;
            public bool HasItem(string itemId) => Items;
            public bool IsEvolved(string weaponId) => false;
        }

        sealed class State : IOfferState
        {
            public List<string> Weapons = new List<string>(), Logbooks = new List<string>();
            public Dictionary<string, int> Levels = new Dictionary<string, int>(), LogbookLevels = new Dictionary<string, int>();
            public IReadOnlyList<string> OwnedWeapons => Weapons;
            public IReadOnlyList<string> OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => Levels.TryGetValue(id, out var l) ? l : 0;
            public int LogbookLevel(string id) => LogbookLevels.TryGetValue(id, out var l) ? l : 0;
        }

        static int EvolutionCards(bool parity, bool hasItems = true)
        {
            using var set = Set(parity);
            var p = set.Single<ProgressionConfig>();
            var gen = new OfferGenerator(() => set.Single<ProgressionConfig>(), () => set.Single<OfferWeightsConfig>(),
                () => OfferGenerator.InDatabaseOrder(set.All<WeaponConfig>()), () => OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()),
                () => set.All<WeaponEvolutionConfig>().ToList(), () => true);
            // Six weapons with evolutions, every one maxed; four Logbooks, every one maxed: only an evolution can be dealt.
            var state = new State();
            var evolving = set.All<WeaponEvolutionConfig>().Where(e => e.baseWeapon != null).Select(e => e.baseWeapon).Distinct().ToList();
            var others = set.All<WeaponConfig>().Where(w => w.databaseOrder >= 0 && !evolving.Contains(w)).ToList();
            foreach (var w in evolving.Concat(others).Take(p.weaponSlots)) { state.Weapons.Add(w.weaponId); state.Levels[w.weaponId] = Mathf.Max(w.maxLevel, p.maxWeaponLevel); }
            foreach (var l in OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()).Where(l => l.inSaltwindPool).Take(p.logbookSlots)) { state.Logbooks.Add(l.logbookId); state.LogbookLevels[l.logbookId] = l.maxLevel; }
            var count = 0;
            for (var seed = 1; seed <= 40; seed++)
                count += gen.Generate(new Rng(seed), new Ctx { Items = hasItems }, state, 0f, false, new OfferPlayer(100f, 100f, 1f, 6f), 30).Count(o => o.Kind == OfferKind.WeaponEvolution);
            return count;
        }

        [Test]
        public void R31_EvolutionCards_StayReachable_WithEveryWeaponMaxed()
        {
            Assert.Greater(EvolutionCards(false), 0, "the fix deals evolution cards once every weapon is maxed");
            Assert.AreEqual(0, EvolutionCards(true), "V3 (parity): the weapon-upgrade category is closed, so no evolution card");
            Assert.AreEqual(0, EvolutionCards(false, hasItems: false), "without the evolution's item there is still none");
        }

        // ---- R-43 ---------------------------------------------------------------------------------------------------

        sealed class PowerRng : IPowerupRandom { public float PowerupFloat() => 0.5f; }

        sealed class World : IPowerupWorld
        {
            public bool IsFreeSpot(Vector3 position, float margin, float clearance) => true;
            public bool IsBossFight => false;
            public bool ShipAlive => true;
            public float ShipHull => 50f;
            public float ShipMaxHull => 100f;
            public void HealShip(float amount) { }
            public void SyncShipSpeed() { }
        }

        static float SpeedAfterTailwindAndFullSail(bool parity)
        {
            using var set = Set(parity);
            var stats = new Stats();
            var module = new PowerupModule(() => set.Single<PowerupConfig>(), new PowerRng(), stats, new World(), null);
            module.ResetForRun();
            module.DevSpawn(PowerupKind.Tailwind, Vector3.zero);
            module.Tick(0.016f, Vector3.zero);
            module.DevSpawn(PowerupKind.FullSail, Vector3.zero);
            module.Tick(0.016f, Vector3.zero);
            return stats.Get((int)StatId.MoveSpeed);
        }

        [Test]
        public void R43_FullSail_StacksWithTailwind_AndPowderRushIsAttackSpeed()
        {
            using var set = Default();
            var c = set.Single<PowerupConfig>();
            var baseSpeed = new PlayerStats().Get(StatId.MoveSpeed);
            Assert.AreEqual(baseSpeed * c.tailwindSpeedMultiplier * c.fullSailSpeedMultiplier, SpeedAfterTailwindAndFullSail(false), 1e-4f, "both speed buffs");
            Assert.AreEqual(baseSpeed * c.fullSailSpeedMultiplier, SpeedAfterTailwindAndFullSail(true), 1e-4f, "V3: Full Sail replaced Tailwind");

            var stats = new Stats();
            var module = new PowerupModule(() => c, new PowerRng(), stats, new World(), null);
            module.ResetForRun();
            module.DevSpawn(PowerupKind.PowderRush, Vector3.zero);
            module.Tick(0.016f, Vector3.zero);
            Assert.AreEqual(1f + c.powderRushBonus, stats.Get((int)StatId.AttackSpeed) / new PlayerStats().Get(StatId.AttackSpeed), 1e-4f, "Powder Rush is attack speed");
            Assert.AreEqual(new PlayerStats().Get(StatId.Damage), stats.Get((int)StatId.Damage), 1e-5f, "and no damage");
        }

        // ---- R-84/R-102 ---------------------------------------------------------------------------------------------

        sealed class Run : IProgressionRun
        {
            public RunState Current = RunState.BossFight;
            public bool Exists => true;
            public bool IsRunEnded => false;
            public RunState State => Current;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) => Current = state;
            public void EnterPlaying() => Current = RunState.Playing;
        }

        sealed class Presenter : IOfferPresenter
        {
            public List<Offer> Offers;
            public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining) { Offers = offers; return true; }
        }

        static RunState StateAfterALevelUpInTheBossFight(bool resumeToBossFight, RunState? changedDuringPause = null)
        {
            using var set = Default();
            var stats = new Stats();
            var run = new Run();
            var presenter = new Presenter();
            var gen = new OfferGenerator(() => set.Single<ProgressionConfig>(), () => set.Single<OfferWeightsConfig>(),
                () => OfferGenerator.InDatabaseOrder(set.All<WeaponConfig>()), () => OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()),
                () => set.All<WeaponEvolutionConfig>().ToList(), () => true);
            var module = new ProgressionModule(() => set.Single<ProgressionConfig>(), id => set.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id),
                id => set.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id), gen, new Rng(3), new Ctx(), stats, stats, run, null)
            {
                Presenter = presenter,
                ResumeToBossFight = () => resumeToBossFight,
            };
            module.BeginRun(set.Single<ProgressionConfig>().firstLevelUpWeaponId, 6, 1f);
            module.AddXp(module.XpToNext);
            Assert.AreEqual(RunState.LevelUpPaused, run.Current, "the cards pause the run");
            if (changedDuringPause.HasValue) run.Current = changedDuringPause.Value; // e.g. the boss died while the cards were up
            module.Select(presenter.Offers[0]);
            return run.Current;
        }

        [Test]
        public void R102_ALevelUpInTheBossFight_ResumesToTheBossFight()
        {
            Assert.AreEqual(RunState.BossFight, StateAfterALevelUpInTheBossFight(true));
            Assert.AreEqual(RunState.Playing, StateAfterALevelUpInTheBossFight(false), "V3: always Playing");
            Assert.AreEqual(RunState.Playing, StateAfterALevelUpInTheBossFight(true, RunState.Playing), "the boss died during the pause: the fight is over");
            using var parity = Parity();
            Assert.IsFalse(parity.Single<StageContentConfig>().bossFightSurvivesLevelUp, "the v3-parity preset turns it off");
        }

        // ---- R-40: items ---------------------------------------------------------------------------------------------

        sealed class ItemWorld : IItemWorld
        {
            public Vector3 Position;
            public bool RunActive => true;
            public bool ShipExists => true;
            public Vector3 ShipForward => Vector3.forward;
            public Vector3 ShipPosition => Position;
            public Vector3 ShipVelocity => Vector3.zero;
            public int Gold => 0;
            public IEnumerable<WeaponConfig> Weapons => Enumerable.Empty<WeaponConfig>();
        }

        [Test]
        public void R40_ItemEffects_AreRead_OnlyWithTheFix()
        {
            foreach (var parity in new[] { false, true })
            {
                using var set = Set(parity);
                var world = new ItemWorld { Position = new Vector3(0.1f, 0f, 0.1f) };
                var items = new ItemModule(id => set.All<ItemConfig>().FirstOrDefault(i => i.itemId == id), new Stats(), world);
                items.ResetForRun();
                foreach (var id in new[] { "black_powder_keg", "powder_fuse", "sea_witchs_eye", "leviathan_tooth", "stormglass", "cursed_compass" }) items.TryAddItem(id);
                for (var i = 0; i < 30; i++) { world.Position += Vector3.forward; items.Tick(0.016f); } // 30 m without a hit charges the fuse
                var tag = parity ? "parity" : "fix";
                if (parity)
                {
                    Assert.AreEqual(1f, items.ExplosionDamageMultiplier(), tag);
                    Assert.AreEqual(1f, items.StatusDamageMultiplier(), tag);
                    Assert.AreEqual(1f, items.ControlledTargetDamageMultiplier(), tag);
                    Assert.AreEqual(0, items.ExtraLightningJumps(), tag);
                    Assert.AreEqual(1f, items.SpawnDensityMultiplier(), tag);
                    continue;
                }
                Assert.AreEqual(1.12f * 1.35f, items.ExplosionDamageMultiplier(), 1e-4f, "Black Powder Keg and the charged Powder Fuse");
                items.NotifyExplosionResolved();
                Assert.AreEqual(1.12f, items.ExplosionDamageMultiplier(), 1e-4f, "the explosion used the fuse charge");
                Assert.AreEqual(1.15f, items.StatusDamageMultiplier(), 1e-4f, "Sea Witch's Eye");
                Assert.AreEqual(1.2f, items.ControlledTargetDamageMultiplier(), 1e-4f, "Leviathan's Tooth");
                Assert.AreEqual(1, items.ExtraLightningJumps(), "Stormglass");
                Assert.AreEqual(1.18f, items.SpawnDensityMultiplier(), 1e-4f, "Cursed Compass");
            }
        }

        [Test]
        public void R40_Tarred_MarksAnEnemyControlled_UntilItWearsOff()
        {
            var statuses = new EnemyStatuses();
            Assert.IsFalse(statuses.IsControlled(3));
            var agents = new EnemyUnit[512];
            agents[3].active = true;
            statuses.ApplyStatus(3, StatusType.Tarred, 0.25f); // the harpoon's tar strength
            Assert.IsTrue(statuses.IsControlled(3));
            statuses.Tick(1f, agents, (i, d) => { }, (i, b) => { });
            Assert.IsTrue(statuses.IsControlled(3), "controlled for the harpoon's 1.2 s, not the 0.25 tar strength");
            statuses.Tick(0.3f, agents, (i, d) => { }, (i, b) => { });
            Assert.IsFalse(statuses.IsControlled(3), "then it wears off");
        }

        // ---- R-40: near misses -----------------------------------------------------------------------------------------

        sealed class Target : IHostileShotTarget
        {
            public int Hits;
            public bool Exists => true;
            public bool IsAlive => true;
            public Vector3 HullPosition => new Vector3(0f, 0.6f, 0f);
            public void TakeDamage(float amount, Vector3 source, bool isCrit) => Hits++;
        }

        static (int nearMisses, int hits) Fire(float passAt, float margin)
        {
            var shots = new HostileProjectiles(null) { NearMissMargin = margin };
            var near = 0;
            shots.NearMiss += () => near++;
            var target = new Target();
            shots.TrySpawn(new Vector3(-8f, 1.2f, passAt), Vector3.right, 14f, 5f, 3f, 0.3f);
            for (var i = 0; i < 240; i++) shots.Tick(1f / 60f, target);
            return (near, target.Hits);
        }

        [Test]
        public void R40_AShotThatPassesClose_AndMisses_IsANearMiss()
        {
            Assert.AreEqual((1, 0), Fire(2f, 2f), "passed 2 m beside the hull and splashed down: a dodge");
            Assert.AreEqual((0, 1), Fire(0f, 2f), "a hit is not a near miss");
            Assert.AreEqual((0, 0), Fire(6f, 2f), "too far away");
            Assert.AreEqual((0, 0), Fire(2f, 0f), "V3 (margin 0): no detection");
        }

        // ---- R-49: Captains ------------------------------------------------------------------------------------------

        sealed class Ship : ICaptainShip
        {
            public bool Exists => true;
            public float Speed => 0f;
            public void InitializeHull(float baseHull) { }
            public void SyncMaxHullKeepRatio() { }
            public void ApplySlow(float multiplier, float seconds) { }
            public void ApplyMoveEffect(float multiplier, float seconds) { }
        }

        sealed class CaptainRun : ICaptainRun
        {
            public int Level = 10;
            public bool Exists => true;
            public bool IsPaused => false;
            public bool IsRunEnded => false;
            public int Gold => 0;
            public int PlayerLevel => Level;
        }

        static CaptainModule Captain(ConfigSetRuntime set, string key)
        {
            var captain = new CaptainModule(new Stats(), new Ship(), new CaptainRun(), () => true);
            captain.BeginRun(set.All<CaptainConfig>().Single(c => c.captainKey == key));
            return captain;
        }

        [Test]
        public void R49_HanksArmor_AndEmberAndRoxiesMultipliers_ApplyOnlyWithTheFix()
        {
            using (var set = Default())
            {
                var hank = Captain(set, "brine");
                var p = set.All<CaptainConfig>().Single(c => c.captainKey == "brine").passive;
                Assert.AreEqual(100f / (100f + Mathf.Min(p.armorCap, p.armorPerLevel * 10)), hank.IncomingDamageMultiplier, 1e-5f, "Hank's armor at level 10");
                Assert.AreEqual(1.12f, Captain(set, "ember").PassiveBurnMultiplier, 1e-5f, "Ember's burn");
                Assert.AreEqual(1.1f, Captain(set, "roxie").LightningDamageMultiplier, 1e-5f, "Roxie's lightning");
                Assert.AreEqual(1f, Captain(set, "rook").IncomingDamageMultiplier, "no armor for Rook");
                Assert.IsTrue(set.All<CaptainConfig>().Single(c => c.captainKey == "richie").active.pickupRadiusApplied, "Cut and Run's pickup radius");
                Assert.IsFalse(set.Single<ExperimentConfig>().captainActiveOnGamepadSouth, "the gamepad active moves off South (the boost button)");
            }
            using (var set = Parity())
            {
                Assert.AreEqual(1f, Captain(set, "brine").IncomingDamageMultiplier, "V3: armor never applied");
                Assert.AreEqual(1f, Captain(set, "ember").PassiveBurnMultiplier);
                Assert.AreEqual(1f, Captain(set, "roxie").LightningDamageMultiplier);
                Assert.IsFalse(set.All<CaptainConfig>().Single(c => c.captainKey == "richie").active.pickupRadiusApplied);
                Assert.IsTrue(set.Single<ExperimentConfig>().captainActiveOnGamepadSouth);
                Assert.AreEqual(0f, set.Single<ExperimentConfig>().nearMissMargin);
            }
        }
    }
}
