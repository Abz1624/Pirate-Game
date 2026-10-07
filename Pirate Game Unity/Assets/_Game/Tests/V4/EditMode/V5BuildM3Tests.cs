using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Weapons;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3 build balance: evolutions reachable (the card from weapon level 6 with its item since the demo-baseline retune, 8 in M3; rarer cards granting more
    /// levels, the pity rule, the chest's evolution-item chance), weapon parity per role, Logbook and item bands, Captains
    /// within ±10%, the item flow, and the v3-parity preset restoring every M3 value.
    /// V5 M8z (I8z.7, owner 7 Oct 2026: one simulator): the checks that ran the in-engine simulator and its weapon and build
    /// models (weapon parity per role, the new weapons' parity, Logbook and item bands, Captains within ±10% and items by
    /// 10:00, focused builds evolving) were retired with it; the game-rule checks here drive the game's own modules.
    /// </summary>
    [Category("V5")]
    public sealed class V5BuildM3Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        sealed class Rng : IOfferRandom, IStageRandom
        {
            readonly System.Random _r;
            public Rng(int seed) => _r = new System.Random(seed);
            public float LootFloat() => (float)_r.NextDouble();
            public int LootInt(int min, int max) => _r.Next(min, max);
            public int MapInt(int min, int max) => _r.Next(min, max);
        }

        sealed class Context : IOfferContext
        {
            public readonly HashSet<string> Items = new HashSet<string>(), Evolved = new HashSet<string>();
            public bool IsWeaponOfferable(string weaponId) => true;
            public bool IsWeaponUnlocked(string weaponId) => true;
            public bool HasItem(string itemId) => Items.Contains(itemId);
            public bool IsEvolved(string weaponId) => Evolved.Contains(weaponId);
        }

        sealed class State : IOfferState
        {
            public readonly List<string> Weapons = new List<string>(), Logbooks = new List<string>();
            public readonly Dictionary<string, int> Levels = new Dictionary<string, int>();
            public IReadOnlyList<string> OwnedWeapons => Weapons;
            public IReadOnlyList<string> OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => Levels.TryGetValue(id, out var l) ? l : 0;
            public int LogbookLevel(string id) => 0;
        }

        static OfferGenerator Generator(ConfigSetRuntime rt) => new OfferGenerator(() => rt.Single<ProgressionConfig>(), () => rt.Single<OfferWeightsConfig>(),
            () => OfferGenerator.InDatabaseOrder(rt.All<WeaponConfig>()), () => OfferGenerator.InDatabaseOrder(rt.All<LogbookConfig>()),
            () => rt.All<WeaponEvolutionConfig>().ToList(), () => true);

        static (State state, Context ctx, WeaponEvolutionConfig evo) OneEvolvingWeapon(ConfigSetRuntime rt, int level)
        {
            var evo = rt.All<WeaponEvolutionConfig>().First(e => e.baseWeapon != null && !string.IsNullOrEmpty(e.requiredItemId));
            var state = new State();
            // Six weapons so no new-weapon cards are dealt; the evolving one at the given level.
            foreach (var w in rt.All<WeaponConfig>().Where(w => w.databaseOrder >= 0).Take(rt.Single<ProgressionConfig>().weaponSlots))
            {
                state.Weapons.Add(w.weaponId);
                state.Levels[w.weaponId] = 1;
            }
            if (!state.Weapons.Contains(evo.baseWeapon.weaponId)) state.Weapons[0] = evo.baseWeapon.weaponId;
            state.Levels[evo.baseWeapon.weaponId] = level;
            var ctx = new Context();
            ctx.Items.Add(evo.requiredItemId);
            return (state, ctx, evo);
        }

        static bool DealsEvolution(ConfigSetRuntime rt, int level)
        {
            var (state, ctx, evo) = OneEvolvingWeapon(rt, level);
            var g = Generator(rt);
            for (var i = 0; i < 40; i++)
                if (g.Generate(new Rng(i), ctx, state, 0f, false, new OfferPlayer(100f, 100f, 1f, 6f), 20).Any(o => o.Kind == OfferKind.WeaponEvolution && o.EvolutionId == evo.evolutionId))
                    return true;
            return false;
        }

        // ---- Evolutions reachable (G3.1) ---------------------------------------------------------------------------

        [Test]
        public void EvolutionCard_ComesFromWeaponLevel6_WithItsItem()
        {
            // M3 set level 8; the demo-baseline retune (XP ×0.5) moved it to 6 and M3c (fewer level-ups) to 5, so focused
            // builds still evolve in most runs.
            using var rt = new ConfigSetRuntime(Set);
            var evo = rt.Single<ProgressionConfig>().evolutionUnlockLevel;
            Assert.AreEqual(5, evo);
            Assert.IsFalse(DealsEvolution(rt, evo - 1), "not below the evolution level");
            Assert.IsTrue(DealsEvolution(rt, evo), "from the evolution level");
            using var parity = V4DifficultyM10Tests.Parity();
            Assert.IsFalse(DealsEvolution(parity, 8), "V3 (the parity preset): only at the max level");
            Assert.IsTrue(DealsEvolution(parity, parity.Single<ProgressionConfig>().maxWeaponLevel));
        }

        /// <summary>
        /// The game's own ProgressionModule behind small ports (V5 M8z: was the retired in-engine simulator's SimBuild),
        /// starting with the first Captain's weapon.
        /// </summary>
        sealed class TestBuild : IOfferRandom, IOfferContext, IProgressionRun, IProgressionShip, IOfferPresenter
        {
            readonly System.Random _rng = new System.Random(1);
            readonly PlayerStats _stats = new PlayerStats();
            readonly ConfigSetRuntime _rt;
            float _hull;
            public ProgressionModule Progression { get; }

            public TestBuild(ConfigSetRuntime rt)
            {
                _rt = rt;
                var stats = new StatResolver(() => _stats);
                Progression = new ProgressionModule(() => rt.Single<ProgressionConfig>(), id => Weapon(rt, id), id => rt.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id),
                    Generator(rt), this, this, stats, stats, this, this)
                {
                    Presenter = this,
                    ResumeToBossFight = () => false,
                };
                _hull = _stats.MaxHull;
                var p = rt.Single<ProgressionConfig>();
                var captain = rt.All<CaptainConfig>().OrderBy(c => c.captainKey, StringComparer.Ordinal).First();
                Progression.BeginRun(captain.startingWeaponId, p.weaponSlots, p.newWeaponDamageBonus);
            }

            public int WeaponLevel(string id) => Progression.WeaponLevel(id);

            public float LootFloat() => (float)_rng.NextDouble();
            public bool IsWeaponOfferable(string weaponId) => true;
            public bool IsWeaponUnlocked(string weaponId) => true;
            public bool HasItem(string itemId) => false;
            public bool IsEvolved(string weaponId) => Progression.EvolutionOf(weaponId) != null;
            public bool Exists => true;
            public bool IsRunEnded => false;
            public RunState State => RunState.Playing;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) { }
            public void EnterPlaying() { }
            public bool IsAlive => true;
            public float Hull => _hull;
            public float MaxHull => _stats.MaxHull;
            public void Heal(float amount) => _hull = Mathf.Min(MaxHull, _hull + Mathf.Max(0f, amount));
            public void SyncSpeed() { }
            public void SyncMaxHull(float healFlat) { _hull = Mathf.Min(_hull, MaxHull); Heal(healFlat); }
            public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining) => true;
            public void Hide() { }
        }

        static WeaponConfig Weapon(ConfigSetRuntime rt, string id) => rt.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id);

        static TestBuild Build(ConfigSetRuntime rt) => new TestBuild(rt);

        [Test]
        public void TakingARareCard_ThroughTheProgressionModule_GrantsItsLevels()
        {
            using var rt = new ConfigSetRuntime(Set);
            var b = Build(rt);
            var id = b.Progression.OwnedWeapons[0];
            var w = Weapon(rt, id);
            var card = Generator(rt).WeaponUpgradeCard(w, b.WeaponLevel(id), 2, new Rng(4));
            b.Progression.Select(card);
            var rare = 1 + rt.Single<ProgressionConfig>().levelsPerRarity[2];
            Assert.AreEqual(rare, b.WeaponLevel(id), "Rare: its levels through Select");
            Assert.AreEqual(rare, b.Progression.Loadout.Weapons[b.Progression.Loadout.IndexOf(id)].Level, "the loadout agrees");
        }

        [Test]
        public void EvolvedAtTheUnlockLevel_TheWeaponLevelsOnTo12_ThroughCards()
        {
            using var rt = new ConfigSetRuntime(Set);
            var b = Build(rt);
            var evo = rt.All<WeaponEvolutionConfig>().First(e => e.baseWeapon != null);
            var id = evo.baseWeapon.weaponId;
            b.Progression.DevSetWeapons(new[] { id });
            var index = b.Progression.Loadout.IndexOf(id);
            while (b.WeaponLevel(id) < rt.Single<ProgressionConfig>().evolutionUnlockLevel) b.Progression.DevRankUp(index);
            b.Progression.DevEvolve(id, evo.evolutionId);
            var g = Generator(rt);
            var rng = new Rng(7);
            for (var i = 0; i < 12 && b.WeaponLevel(id) < Weapon(rt, id).maxLevel; i++)
                b.Progression.Select(g.WeaponUpgradeCard(Weapon(rt, id), b.WeaponLevel(id), 0, rng));
            Assert.AreEqual(Weapon(rt, id).maxLevel, b.WeaponLevel(id), "upgrade cards take the evolved weapon to its max");
            Assert.AreEqual(evo.evolutionId, b.Progression.EvolutionOf(id), "and it stays evolved");
        }

        [Test]
        public void RarerCards_GrantMoreLevels_CappedAtTheMax()
        {
            using var rt = new ConfigSetRuntime(Set);
            var g = Generator(rt);
            var w = rt.All<WeaponConfig>().First(x => x.databaseOrder >= 0);
            var levels = rt.Single<ProgressionConfig>().levelsPerRarity; // M3: 1/1/2/2/3; M3c (fewer level-ups): 2/2/3/3/4
            Assert.Less(levels[0], levels[4], "rarer cards grant more levels");
            for (var rarity = 0; rarity < 5; rarity++)
            {
                var card = g.WeaponUpgradeCard(w, 3, rarity, new Rng(rarity));
                Assert.AreEqual(3 + levels[rarity], card.NextLevel, $"rarity {rarity}");
            }
            Assert.AreEqual(w.maxLevel, g.WeaponUpgradeCard(w, w.maxLevel - 1, 4, new Rng(9)).NextLevel, "a Legendary card never passes the max level");

            var loadout = new ProgressionLoadout(id => id == w.weaponId ? w : null, _ => null);
            loadout.EnsureSlotCapacity(1);
            loadout.Equip(w.weaponId);
            var legendary = g.WeaponUpgradeCard(w, 1, 4, new Rng(3));
            loadout.ApplyUpgradePackage(0, legendary.Lines, legendary.NextLevel - legendary.CurrentLevel);
            Assert.AreEqual(Mathf.Min(w.maxLevel, 1 + levels[4]), loadout.Weapons[0].Level);
            using var parity = V4DifficultyM10Tests.Parity();
            Assert.AreEqual(4, Generator(parity).WeaponUpgradeCard(w, 3, 4, new Rng(1)).NextLevel, "V3: one level whatever the rarity");
        }

        [Test]
        public void PityRule_RaisesTheNextWeaponUpgrade_AfterSixDealsWithoutARare()
        {
            using var rt = new ConfigSetRuntime(Set);
            var p = rt.Single<ProgressionConfig>();
            Assert.AreEqual((6, 2), (p.rarePityLevelUps, p.pityMinRarity));
            p.rarityBaseWeights = new[] { 1f, 0.0001f, 0.0001f, 0.0001f, 0.0001f }; // nothing but Common without the pity rule
            var g = Generator(rt);
            var (state, ctx, _) = OneEvolvingWeapon(rt, 2);
            ctx.Items.Clear();
            var player = new OfferPlayer(100f, 100f, 1f, 6f);
            var rng = new Rng(5);
            for (var i = 0; i < 6; i++)
            {
                var deal = g.Generate(rng, ctx, state, 0f, false, player, 20);
                Assert.IsFalse(deal.Any(o => o.Rarity >= 2), $"deal {i}: no rare yet");
            }
            Assert.AreEqual(6, g.DealsSinceRare);
            List<Offer> next = null;
            for (var i = 0; i < 20; i++)
            {
                next = g.Generate(rng, ctx, state, 0f, false, player, 20);
                if (next.Any(o => o.Kind == OfferKind.WeaponUpgrade)) break;
            }
            Assert.AreEqual(1, next.Count(o => o.Kind == OfferKind.WeaponUpgrade && o.Rarity >= 2), "the pity rule raises one weapon upgrade in the deal");
            Assert.AreEqual(0, g.DealsSinceRare, "and the count starts again");
            g.ResetForRun();
            Assert.AreEqual(0, g.DealsSinceRare);
        }

        [Test]
        public void PityCountsDeals_ARefreshIsADeal()
        {
            using var rt = new ConfigSetRuntime(Set);
            rt.Single<ProgressionConfig>().rarityBaseWeights = new[] { 1f, 0.0001f, 0.0001f, 0.0001f, 0.0001f };
            var b = Build(rt);
            var before = 0;
            b.Progression.GenerateOffers(); // a level-up's deal
            var g = typeof(ProgressionModule).GetField("_offers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(b.Progression) as OfferGenerator;
            before = g.DealsSinceRare;
            b.Progression.GenerateOffers(); // what a refresh does: deal again
            Assert.AreEqual(before + 1, g.DealsSinceRare, "each set of cards shown counts (documented on the config field)");
        }

        // ---- The chest's evolution-item chance (I3.2) --------------------------------------------------------------

        sealed class Run : IStageRun
        {
            public bool Exists => true;
            public bool IsRunEnded => false;
            public bool IsPaused => false;
            public PauseReason PauseReason => PauseReason.None;
            public RunState State => RunState.Playing;
            public float ElapsedSeconds => 300f;
            public int Gold { get; set; } = 10000;
            public int RunSeed => 1;
            public float MaxHull => 100f;
            public float Luck => 0f;
            public void SetState(RunState state) { }
            public void SetPause(PauseReason reason) { }
            public void AddGold(int amount) => Gold += amount;
            public bool TrySpendGold(int amount) { Gold -= amount; return true; }
            public void SecureObjective() { }
            public void CompleteRun(bool victory) { }
            public void AddStatModifier(int stat, float add, float multiply) { }
        }

        sealed class Weapons : IWeaponManager
        {
            public readonly List<WeaponSlot> List = new List<WeaponSlot>();
            public IReadOnlyList<WeaponSlot> Slots => List;
            public PlayerProjectiles Projectiles => null;
            public void FireAllOnce() { }
            public void ArmAllKegs() { }
            public void FireLightningSalvo() { }
            public bool FireSlot(int index) => false;
            public void ClearInFlight() { }
            public IReadOnlyList<OrbitBehavior.HitSphere> OrbitHits => Array.Empty<OrbitBehavior.HitSphere>();
        }

        static float EvolutionItemShare(ConfigSetRuntime rt, int weaponLevel, bool owned, bool evolved)
        {
            var evo = rt.All<WeaponEvolutionConfig>().First(e => e.baseWeapon != null && rt.All<ItemConfig>().Any(i => i.itemId == e.requiredItemId && i.inChestPool));
            var weapons = new Weapons();
            weapons.List.Add(new WeaponSlot { Config = evo.baseWeapon, Level = weaponLevel, Evolution = evolved ? evo : null });
            var hits = 0;
            const int chests = 400;
            for (var i = 0; i < chests; i++)
            {
                var economy = new ChestEconomy(() => rt, new Run(), new Rng(i), () => null, () => weapons) { HasItem = id => owned && id == evo.requiredItemId };
                if (economy.PlanChest(new[] { evo.baseWeapon.weaponId }).ItemId == evo.requiredItemId) hits++;
            }
            return hits / (float)chests;
        }

        [Test]
        public void Chests_OftenGiveAReadyWeaponsEvolutionItem()
        {
            using var rt = new ConfigSetRuntime(Set);
            var stage = rt.Single<StageContentConfig>();
            var evo = rt.Single<ProgressionConfig>().evolutionUnlockLevel;
            Assert.AreEqual((0.4f, 0), (stage.evolutionItemChance, stage.evolutionItemMinWeaponLevel), "0 = from the evolution card's level");
            Assert.That(EvolutionItemShare(rt, evo, false, false), Is.GreaterThan(0.35f), "about 40% (plus the ordinary roll's own chance)");
            Assert.Less(EvolutionItemShare(rt, evo - 1, false, false), 0.15f, "below the evolution level only the ordinary roll");
            Assert.Less(EvolutionItemShare(rt, evo, true, false), 0.15f, "not when the item is already carried");
            Assert.Less(EvolutionItemShare(rt, evo, false, true), 0.15f, "not when the weapon has evolved");
            using var parity = V4DifficultyM10Tests.Parity();
            Assert.Less(EvolutionItemShare(parity, 8, false, false), 0.15f, "V3: off");
        }

        sealed class CountingRng : IStageRandom
        {
            readonly System.Random _r = new System.Random(3);
            public int Draws;
            /// <summary>Fixed values (0.5, the lowest index): the draw count then does not depend on what was drawn before.</summary>
            public bool Constant;
            public float LootFloat() { Draws++; return Constant ? 0.5f : (float)_r.NextDouble(); }
            public int LootInt(int min, int max) { Draws++; return Constant ? min : _r.Next(min, max); }
            public int MapInt(int min, int max) { Draws++; return Constant ? min : _r.Next(min, max); }
        }

        [Test]
        public void EvolutionItemChance_Off_MakesNoExtraLootDraws()
        {
            using var rt = new ConfigSetRuntime(Set);
            var evo = rt.All<WeaponEvolutionConfig>().First(e => e.baseWeapon != null);
            var weapons = new Weapons();
            weapons.List.Add(new WeaponSlot { Config = evo.baseWeapon, Level = 12 });
            int Draws(float chance)
            {
                rt.Single<StageContentConfig>().evolutionItemChance = chance;
                var rng = new CountingRng { Constant = true };
                var economy = new ChestEconomy(() => rt, new Run(), rng, () => null, () => weapons) { HasItem = _ => false };
                economy.PlanChest(new[] { evo.baseWeapon.weaponId });
                return rng.Draws;
            }
            var off = Draws(0f);
            Assert.AreEqual(off + 1, Draws(0.0001f), "on: one draw for the chance (the ordinary roll follows when it misses)");
            rt.Single<StageContentConfig>().evolutionItemChance = 0f;
            var rngOff = new CountingRng { Constant = true };
            new ChestEconomy(() => rt, new Run(), rngOff, () => null, () => weapons) { HasItem = null }.PlanChest(new[] { evo.baseWeapon.weaponId });
            Assert.AreEqual(off, rngOff.Draws, "off (V3): the same draws as without the feature");
        }

        [Test]
        public void EvolutionItemLevel_FollowsTheEvolutionCardsLevel()
        {
            using var rt = new ConfigSetRuntime(Set);
            var stage = rt.Single<StageContentConfig>();
            var p = rt.Single<ProgressionConfig>();
            Assert.AreEqual(0, stage.evolutionItemMinWeaponLevel, "0 = follow the card");
            Assert.AreEqual(p.evolutionUnlockLevel, ChestEconomy.EvolutionItemMinLevel(stage, p));
            p.evolutionUnlockLevel = 10;
            Assert.AreEqual(10, ChestEconomy.EvolutionItemMinLevel(stage, p), "the two cannot disagree");
        }

        // ---- Parity and bands (G3.2, G3.3, G3.4, I3.2) ---------------------------------------------------------------

        [Test]
        public void ParityPreset_RestoresEveryM3Value()
        {
            using var parity = V4DifficultyM10Tests.Parity();
            var p = parity.Single<ProgressionConfig>();
            Assert.AreEqual(0, p.evolutionUnlockLevel);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1 }, p.levelsPerRarity);
            Assert.AreEqual(0, p.rarePityLevelUps);
            var s = parity.Single<StageContentConfig>();
            Assert.AreEqual((25, 1.22f, 0f), (s.chestPriceIncrementBase, s.chestPriceGrowth, s.evolutionItemChance));
            Assert.AreEqual(90f, parity.All<CaptainConfig>().Single(c => c.captainKey == "flint").baseHull);
            var v3Damage = new Dictionary<string, float>
            {
                ["broadside_battery"] = 8, ["deck_cannons"] = 11, ["flamethrower"] = 8, ["flying_cutlasses"] = 7, ["harpoon_gun"] = 16,
                ["lightning_mast"] = 14, ["long_nine"] = 28, ["mortar"] = 22, ["powder_kegs"] = 14, ["swivel_guns"] = 6,
            };
            using var current = new ConfigSetRuntime(Set);
            foreach (var w in parity.All<WeaponConfig>().Where(w => v3Damage.ContainsKey(w.weaponId)))
            {
                Assert.AreEqual(v3Damage[w.weaponId], w.baseDamage, 1e-4f, w.weaponId);
                StringAssert.StartsWith("saltwind-v5-", current.All<WeaponConfig>().Single(x => x.weaponId == w.weaponId).balanceVersion, "retuned in M3 (the Lightning Mast again in the demo-baseline retune)");
            }
            Assert.AreEqual(0.08f, parity.All<LogbookConfig>().Single(l => l.logbookId == "gunnery_manual").perLevelAdditive, 1e-5f);
            Assert.AreEqual(10f, parity.All<LogbookConfig>().Single(l => l.logbookId == "carpenters_notes").perLevelAdditive, 1e-5f);
            Assert.AreEqual(1.08f, parity.All<ItemConfig>().Single(i => i.itemId == "dragons_breath_nozzle").statModifiers[0].multiplicative, 1e-5f);
            // Every weapon's upgrade increments and caps (V3's values).
            var v3Axes = new Dictionary<string, (float inc, float cap)[]>
            {
                ["broadside_battery"] = new[] { (0.07f, 1.8f), (0.05f, 0.6f), (1f, 5f), (0.06f, 1.5f) },
                ["deck_cannons"] = new[] { (0.08f, 2f), (0.05f, 0.6f), (1f, 4f), (0.06f, 1.5f), (0.05f, 1.5f) },
                ["flamethrower"] = new[] { (0.08f, 2f), (0.1f, 2f), (0.08f, 2f) },
                ["flying_cutlasses"] = new[] { (0.07f, 1.8f), (0.05f, 0.6f), (1f, 4f), (0.07f, 1.8f) },
                ["harpoon_gun"] = new[] { (0.08f, 2f), (0.05f, 0.6f), (1f, 4f), (0.06f, 1.5f), (0.05f, 1.5f) },
                ["lightning_mast"] = new[] { (0.08f, 2f), (0.05f, 0.6f), (1f, 4f), (0.04f, 1f) },
                ["long_nine"] = new[] { (0.08f, 2f), (0.05f, 0.6f), (1f, 4f), (0.06f, 1.5f), (0.05f, 1.5f) },
                ["mortar"] = new[] { (0.08f, 2f), (0.05f, 0.6f) },
                ["powder_kegs"] = new[] { (0.1f, 2.5f), (0.04f, 0.5f), (0.08f, 2f), (0.06f, 1.5f) },
                ["swivel_guns"] = new[] { (0.08f, 2f), (0.05f, 0.6f), (1f, 4f), (0.06f, 1.5f), (0.05f, 1.5f) },
            };
            foreach (var w in parity.All<WeaponConfig>().Where(w => v3Axes.ContainsKey(w.weaponId)))
                for (var i = 0; i < v3Axes[w.weaponId].Length; i++)
                {
                    Assert.AreEqual(v3Axes[w.weaponId][i].inc, w.upgradeAxes[i].baseIncrement, 1e-5f, $"{w.weaponId} axis {i} increment");
                    Assert.AreEqual(v3Axes[w.weaponId][i].cap, w.upgradeAxes[i].maxAccumulated, 1e-5f, $"{w.weaponId} axis {i} cap");
                }
        }
    }
}
