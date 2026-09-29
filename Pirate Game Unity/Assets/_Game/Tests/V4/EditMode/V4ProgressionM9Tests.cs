using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.Weapons;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using RoguePirates.V4.Composition;

namespace RoguePirates.V4.Tests
{
    /// <summary>M9 step 9.6: the V4 offer generator deals V3's level-up cards (lockstep on the same Loot stream).</summary>
    [Category("V4")]
    public sealed class V4ProgressionM9Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            StageBalanceProfileContext.ResetForTests();
            WeaponEvolutionRuntime.Reset();
        }

        sealed class Loot : IOfferRandom
        {
            readonly RunRng _rng;
            public Loot(RunRng rng) { _rng = rng; }
            public float LootFloat() => _rng.LootFloat();
        }

        sealed class Ctx : IOfferContext
        {
            public Func<string, bool> Items = _ => false;
            // The run's rule (V3 IsWeaponOfferEligible: the unlock counts while V3RunContext is active, as in every run).
            public bool IsWeaponOfferable(string weaponId) => RoguePirates.V4.Composition.BuildModule.IsWeaponOfferEligible(weaponId);
            public bool IsWeaponUnlocked(string weaponId) => V3ContentUnlocks.IsWeaponUnlocked(weaponId);
            public bool HasItem(string itemId) => Items(itemId);
            public bool IsEvolved(string weaponId) => WeaponEvolutionRuntime.IsEvolved(weaponId);
        }

        sealed class State : IOfferState
        {
            public List<string> Weapons = new List<string>(), Logbooks = new List<string>();
            public Dictionary<string, int> WeaponLevels = new Dictionary<string, int>(), LogbookLevels = new Dictionary<string, int>();
            public IReadOnlyList<string> OwnedWeapons => Weapons;
            public IReadOnlyList<string> OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => WeaponLevels.TryGetValue(id, out var l) ? l : 0;
            public int LogbookLevel(string id) => LogbookLevels.TryGetValue(id, out var l) ? l : 0;
        }

        GameDatabase RunDatabase()
        {
            var db = V3ConfigMigrator.LoadCleanDatabase();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            return clone;
        }

        static OfferGenerator Generator(ConfigSetRuntime set) => new OfferGenerator(
            () => set.Single<ProgressionConfig>(),
            () => set.Single<OfferWeightsConfig>(),
            () => OfferGenerator.InDatabaseOrder(set.All<WeaponConfig>()),
            () => OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()),
            () => set.All<WeaponEvolutionConfig>().ToList(),
            () => CombatExperimentContext.Active.weaponEvolutionsEnabled);

        /// <summary>The config set under the v3-parity preset (V3's Logbook stats and the other V3 values).</summary>
        static ConfigSetRuntime Parity()
        {
            var runtime = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var errors = new List<string>();
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("; ", errors));
            return runtime;
        }

        static void AssertSame(List<LevelOffer> v3, List<Offer> v4, string tag)
        {
            Assert.AreEqual(v3.Count, v4.Count, tag + " card count");
            for (var i = 0; i < v3.Count; i++)
            {
                var a = v3[i];
                var b = v4[i];
                var t = $"{tag} card {i} ({a.kind} {a.id})";
                Assert.AreEqual((int)a.kind, (int)b.Kind, t + " kind");
                Assert.AreEqual(a.id, b.Id, t + " id");
                Assert.AreEqual(a.evolutionId, b.EvolutionId, t + " evolution");
                Assert.AreEqual((int)a.rarity, b.Rarity, t + " rarity");
                Assert.AreEqual(a.currentLevel, b.CurrentLevel, t + " current level");
                Assert.AreEqual(a.nextLevel, b.NextLevel, t + " next level");
                var la = a.upgradeLines ?? Array.Empty<WeaponUpgradeLine>();
                var lb = b.Lines ?? Array.Empty<UpgradeLine>();
                Assert.AreEqual(la.Length, lb.Length, t + " lines");
                for (var k = 0; k < la.Length; k++)
                {
                    Assert.AreEqual((int)la[k].axis, (int)lb[k].Axis, t + $" line {k} axis");
                    Assert.AreEqual(la[k].value, lb[k].Value, t + $" line {k} value");
                }
            }
        }

        [Test]
        public void Offers_EqualV3s_OnTheSameLootStream_ForManyBuilds()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile); // Saltwind: fill-state weights, the Saltwind first level-up and Logbook pool
            var settings = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            using var set = Parity();
            var gen = Generator(set);
            var db = RunDatabase();
            var weaponIds = db.weapons.Where(w => w != null).Select(w => w.id).ToArray();
            var logbookIds = db.logbooks.Where(l => l != null).Select(l => l.id).ToArray();
            var p = set.Single<ProgressionConfig>();
            var scenario = new System.Random(4242);
            var kinds = new HashSet<LevelOfferKind>();
            var inventory = Inventory();
            var items = (List<string>)typeof(V3RunItemInventory).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            // M9 trace finding: in runs (V3RunContext active) V3 offers only unlocked weapons. Half the builds run that way,
            // with weapons locked (not starters, not owned), so the unlock rule is compared too.
            V3RunContext.IsActive = true;
            var locked = weaponIds.Count(id => !V3ContentUnlocks.IsWeaponUnlocked(id));
            V3RunContext.IsActive = false;
            Assert.Greater(locked, 0, "some weapons are locked in the test save (else the run rule is not exercised)");
            var lockedNewWeaponSeen = false;

            try
            {
            for (var seed = 1; seed <= 600; seed++)
            {
                V3RunContext.IsActive = seed % 2 == 0;
                var state = new State();
                var weaponCount = scenario.Next(1, 7);
                foreach (var id in weaponIds.OrderBy(_ => scenario.Next()).Take(weaponCount))
                {
                    state.Weapons.Add(id);
                    state.WeaponLevels[id] = scenario.Next(0, 5) == 0 ? 12 : scenario.Next(1, 12);
                }
                var logbookCount = scenario.Next(0, 5);
                foreach (var id in logbookIds.OrderBy(_ => scenario.Next()).Take(logbookCount))
                {
                    state.Logbooks.Add(id);
                    state.LogbookLevels[id] = scenario.Next(0, 4) == 0 ? db.GetLogbook(id).maxLevel : scenario.Next(1, 8);
                }
                if (seed % 25 == 0)
                {
                    // Everything capped: only fallback cards (or Continue) are left.
                    state = new State();
                    foreach (var id in weaponIds.Take(p.weaponSlots)) { state.Weapons.Add(id); state.WeaponLevels[id] = db.GetWeapon(id).maxLevel; }
                    foreach (var id in logbookIds.Take(p.logbookSlots)) { state.Logbooks.Add(id); state.LogbookLevels[id] = db.GetLogbook(id).maxLevel; }
                }
                var isFirst = seed % 9 == 0 && seed % 25 != 0;
                var luck = (float)scenario.NextDouble() * 0.6f;
                var level = scenario.Next(1, 25);
                var hull = scenario.Next(0, 3) == 0 ? 100f : (float)scenario.NextDouble() * 100f;
                var stats = new PlayerStats();
                if (scenario.Next(0, 6) == 0) stats.AddModifier(new StatModifier(StatId.Damage, 10f)); // damage fallback capped
                var itemsOwned = scenario.Next(0, 2) == 0 && seed % 25 != 0;

                // V3's evolution check reads the item inventory and the evolved set.
                WeaponEvolutionRuntime.Reset();
                var v3Items = itemsOwned ? new HashSet<string>(set.All<WeaponEvolutionConfig>().Select(e => e.requiredItemId)) : new HashSet<string>();
                if (scenario.Next(0, 5) == 0 && state.Weapons.Count > 0)
                {
                    var evo = set.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.baseWeapon != null && e.baseWeapon.weaponId == state.Weapons[0]);
                    if (evo != null) WeaponEvolutionRuntime.Apply(state.Weapons[0], evo.evolutionId);
                }
                inventory.ClearForNewRun();
                items.AddRange(v3Items);

                var v3Rng = new RunRng(seed);
                var v4Rng = new RunRng(seed);
                var v3 = LevelOfferGenerator.Generate(db, settings, pacing, v3Rng, luck, state.Weapons, state.WeaponLevels, state.Logbooks, state.LogbookLevels,
                    p.weaponSlots, p.logbookSlots, isFirst, stats, hull, 100f, level, useFillStateWeighting: true);
                var v4 = gen.Generate(new Loot(v4Rng), new Ctx { Items = v3Items.Contains }, state, luck, isFirst,
                    new OfferPlayer(hull, 100f, stats.GlobalDamage, stats.MoveSpeed), level);
                AssertSame(v3, v4, $"seed {seed}");
                Assert.AreEqual(v3Rng.LootFloat(), v4Rng.LootFloat(), $"seed {seed}: same number of Loot draws");
                foreach (var o in v3)
                {
                    kinds.Add(o.kind);
                    if (!V3RunContext.IsActive && o.kind == LevelOfferKind.NewWeapon && !V3ContentUnlocks.IsWeaponUnlocked(o.id)) lockedNewWeaponSeen = true;
                }
            }
            }
            finally { V3RunContext.IsActive = false; }
            Assert.IsTrue(lockedNewWeaponSeen, "outside runs a locked weapon is offered (so the in-run builds really filter)");
            // The scenarios reach every Saltwind card kind.
            foreach (var k in new[] { LevelOfferKind.WeaponUpgrade, LevelOfferKind.NewWeapon, LevelOfferKind.Logbook, LevelOfferKind.WeaponEvolution, LevelOfferKind.FallbackHeal })
                CollectionAssert.Contains(kinds, k, k + " covered");
        }

        /// <summary>V3's evolution check asks V3RunItemInventory.Instance.HasItem: a live inventory (items set per scenario).</summary>
        V3RunItemInventory Inventory()
        {
            var inv = V3RunItemInventory.Instance != null ? V3RunItemInventory.Instance : V3RunItemInventory.Ensure();
            _created.Add(inv.gameObject);
            return inv;
        }

        // ---- The module (V3 LevelSystem) ------------------------------------------------------------------------------

        sealed class Run : IProgressionRun
        {
            public bool Exists => true;
            public bool IsRunEnded => false;
            public RunState State => RunState.Playing;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) { }
            public void EnterPlaying() { }
        }

        static T Awake<T>(GameObject host) where T : MonoBehaviour
        {
            var c = host.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);
            return c;
        }

        [Test]
        public void Module_EqualsV3sLevelSystem_OverXpGrantsAndCardChoices()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile);
            var settings = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            using var set = Parity();
            var db = RunDatabase();
            var experiment = CombatExperimentContext.Active;
            var inventory = Inventory();
            var starter = "swivel_guns";
            var choose = BindingFlags.Instance | BindingFlags.NonPublic;

            for (var seed = 1; seed <= 12; seed++)
            {
                // V3: RunManager, the switched-off weapon record, LevelSystem set up as V3RunBootstrap / RunBootstrapSequence do.
                var host = new GameObject("v3-run");
                _created.Add(host);
                var rm = Awake<RunManager>(host);
                rm.BeginRun(seed);
                inventory.ClearForNewRun();
                WeaponEvolutionRuntime.Reset();
                var recordHost = new GameObject("record");
                _created.Add(recordHost);
                var record = Awake<WeaponController>(recordHost);
                record.ApplyCombatExperiment(experiment);
                record.EnsureWeaponSlotCapacity(profile.weaponSlotCap);
                record.Initialize(db, starter);
                var levelHost = new GameObject("levels");
                _created.Add(levelHost);
                var levels = levelHost.AddComponent<LevelSystem>();
                typeof(LevelSystem).GetField("levelUpSettings", choose).SetValue(levels, settings);
                typeof(LevelSystem).GetField("demoPacing", choose).SetValue(levels, pacing);
                typeof(LevelSystem).GetMethod("Awake", choose).Invoke(levels, null);
                levels.ApplyStageBalanceProgression();
                levels.ConfigureV3();
                levels.ApplyCombatExperiment(experiment);
                levels.PrepareForNewRun(db, starter);
                levels.SyncWeaponControllerFromOwnedLoadout(record);

                // V4: the module on its own Loot stream and stats.
                var v4Stats = new PlayerStats();
                var resolver = new StatResolver(() => v4Stats);
                var v4Rng = new RunRng(seed);
                var module = new ProgressionModule(
                    () => set.Single<ProgressionConfig>(),
                    id => set.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id),
                    id => set.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id),
                    Generator(set), new Loot(v4Rng), new Ctx(), resolver, resolver, new Run(), null);
                module.BeginRun(starter, experiment.weaponSlotCap, pacing.startingDamageMultiplier);

                var scenario = new System.Random(seed * 31);
                for (var step = 0; step < 40; step++)
                {
                    var xp = scenario.Next(1, 30);
                    levels.AddXP(xp);
                    module.AddXp(xp);
                    var tag = $"seed {seed} step {step}";
                    while (levels.PendingLevelUps > 0)
                    {
                        var v3Offers = (List<LevelOffer>)typeof(LevelSystem).GetMethod("GenerateOffers", choose).Invoke(levels, null);
                        var v4Offers = module.GenerateOffers();
                        AssertSame(v3Offers, v4Offers, tag);
                        var action = scenario.Next(0, 10);
                        if (action == 0 && levels.RefreshesRemaining > 0)
                        {
                            typeof(LevelSystem).GetMethod("OnLevelUpRefresh", choose).Invoke(levels, null);
                            module.Refresh();
                            continue;
                        }
                        if (action == 1)
                        {
                            typeof(LevelSystem).GetMethod("OnLevelUpSkipped", choose).Invoke(levels, null);
                            module.Skip();
                            continue;
                        }
                        var pick = scenario.Next(0, v3Offers.Count);
                        typeof(LevelSystem).GetMethod("OnOfferSelected", choose).Invoke(levels, new object[] { v3Offers[pick] });
                        module.Select(v4Offers[pick]);
                    }

                    Assert.AreEqual(levels.Level, module.Level, tag + " level");
                    Assert.AreEqual(levels.CurrentXP, module.CurrentXp, tag + " xp");
                    Assert.AreEqual(levels.XPToNext, (float)module.XpToNext, tag + " xp to next");
                    Assert.AreEqual(levels.PendingLevelUps, module.PendingLevelUps, tag + " pending");
                    Assert.AreEqual(levels.RefreshesRemaining, module.RefreshesRemaining, tag + " refreshes");
                    CollectionAssert.AreEqual(levels.OwnedWeapons, module.OwnedWeapons, tag + " weapons");
                    CollectionAssert.AreEqual(levels.OwnedLogbooks, module.OwnedLogbooks, tag + " logbooks");
                    foreach (var id in levels.OwnedWeapons) Assert.AreEqual(levels.GetWeaponLevel(id), module.WeaponLevel(id), tag + " level of " + id);
                    foreach (var id in levels.OwnedLogbooks) Assert.AreEqual(levels.GetLogbookLevel(id), module.LogbookLevel(id), tag + " rank of " + id);
                    Assert.AreEqual(record.Weapons.Count, module.Loadout.Count, tag + " loadout size");
                    for (var i = 0; i < record.Weapons.Count; i++)
                    {
                        var a = record.Weapons[i];
                        var b = module.Loadout.Get(i);
                        var t = $"{tag} slot {i} ({a.def.id})";
                        Assert.AreEqual(a.def.id, b.WeaponId, t);
                        Assert.AreEqual(a.level, b.Level, t + " level");
                        Assert.AreEqual(a.damageBonus, b.DamageBonus, t + " damage");
                        Assert.AreEqual(a.quantityBonus, b.QuantityBonus, t + " quantity");
                        Assert.AreEqual(a.cooldownMult, b.CooldownMult, t + " cooldown");
                        Assert.AreEqual(a.speedMult, b.SpeedMult, t + " speed");
                        Assert.AreEqual(a.sizeMult, b.SizeMult, t + " size");
                        Assert.AreEqual(a.durationMult, b.DurationMult, t + " duration");
                        Assert.AreEqual(a.knockbackMult, b.KnockbackMult, t + " knockback");
                        WeaponEvolutionRuntime.TryGetEvolutionId(a.def.id, out var evo);
                        Assert.AreEqual(evo, b.EvolutionId, t + " evolution");
                    }
                    foreach (StatId s in Enum.GetValues(typeof(StatId))) Assert.AreEqual(rm.Stats.Get(s), v4Stats.Get(s), $"{tag} {s}");
                }
                Assert.AreEqual(rm.Rng.LootFloat(), v4Rng.LootFloat(), $"seed {seed}: same Loot draws");
                Object.DestroyImmediate(levelHost);
                Object.DestroyImmediate(recordHost);
                Object.DestroyImmediate(host);
            }
        }

        // ---- Items (V3 V3RunItemInventory + V3RunItemEffects) --------------------------------------------------------

        sealed class ItemWorld : IItemWorld
        {
            public HighSeas.Player.PlayerShipController Ship;
            public Func<int> GoldOf;
            public Func<IEnumerable<WeaponConfig>> WeaponsOf;
            public bool RunActive => true;
            public bool ShipExists => Ship != null;
            public Vector3 ShipForward => Ship.MovementForward;
            public Vector3 ShipPosition => Ship.Position;
            public Vector3 ShipVelocity => Ship.Velocity;
            public int Gold => GoldOf();
            public IEnumerable<WeaponConfig> Weapons => WeaponsOf();
        }

        [Test]
        public void Items_EqualV3sInventoryAndEffects_OverAScriptedRun()
        {
            using var set = Parity();
            var db = RunDatabase();
            var host = new GameObject("run");
            _created.Add(host);
            var rm = Awake<RunManager>(host);
            rm.BeginRun(7);
            var inventory = Inventory();
            inventory.ClearForNewRun();
            var effects = V3RunItemEffects.Ensure();
            _created.Add(effects);
            effects.ResetForNewRun();
            var shipHost = new GameObject("ship");
            _created.Add(shipHost);
            shipHost.AddComponent<HighSeas.Combat.DamageReceiver>();
            var ship = shipHost.AddComponent<HighSeas.Player.PlayerShipController>();
            var velocity = typeof(HighSeas.Player.PlayerShipController).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
            var recordHost = new GameObject("record");
            _created.Add(recordHost);
            var record = Awake<WeaponController>(recordHost);
            record.ApplyCombatExperiment(CombatExperimentContext.Active);
            record.Initialize(db, "swivel_guns");

            var v4Stats = new PlayerStats();
            var resolver = new StatResolver(() => v4Stats);
            var items = new ItemModule(id => set.All<ItemConfig>().FirstOrDefault(i => i.itemId == id), resolver, new ItemWorld
            {
                Ship = ship, GoldOf = () => rm.Gold,
                WeaponsOf = () => record.Weapons.Select(w => set.All<WeaponConfig>().First(c => c.weaponId == w.def.id)),
            });
            var v4Bells = 0;
            items.FireAllWeapons += () => v4Bells++;
            var bellCounter = typeof(V3RunItemEffects).GetField("_bellKillCounter", BindingFlags.Instance | BindingFlags.NonPublic);
            // V3's Update with an explicit frame (Time.deltaTime is 0 in edit mode): its timers, then its two steps.
            var gloves = typeof(V3RunItemEffects).GetField("_glovesTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            var lantern = typeof(V3RunItemEffects).GetField("_lanternTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            var track = typeof(V3RunItemEffects).GetMethod("TrackHeadingAndMovement", BindingFlags.Instance | BindingFlags.NonPublic);
            var refresh = typeof(V3RunItemEffects).GetMethod("RefreshDynamicMultipliers", BindingFlags.Instance | BindingFlags.NonPublic);
            const float dt = 1f / 60f;
            var turnBuffEnded = false;
            var addEffect = typeof(V3RunItemInventory).GetMethod("ApplyItemEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            var itemsList = (List<string>)typeof(V3RunItemInventory).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            var stacks = (Dictionary<string, int>)typeof(V3RunItemInventory).GetField("_stacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            var ids = set.All<ItemConfig>().Select(i => i.itemId).OrderBy(i => i).ToArray();
            var scenario = new System.Random(99);
            var v3Bells = 0;
            var extraWeapons = new[] { "deck_cannons", "broadside_battery", "powder_kegs", "flying_cutlasses", "lightning_mast" };
            var heading = 0f;

            for (var frame = 0; frame < 3000; frame++)
            {
                if (frame % 60 == 0)
                {
                    var id = ids[scenario.Next(ids.Length)];
                    // V3 TryAddItem without its telemetry/toast singletons: the stack, the list, the stat changes.
                    stacks.TryGetValue(id, out var n); stacks[id] = n + 1;
                    if (!itemsList.Contains(id)) itemsList.Add(id);
                    addEffect.Invoke(inventory, new object[] { id });
                    items.TryAddItem(id);
                }
                if (frame % 400 == 200 && record.Weapons.Count < 6) record.EquipWeapon(extraWeapons[record.Weapons.Count - 1]);
                if (frame % 7 == 0) rm.AddGold(scenario.Next(0, 40));
                heading += scenario.Next(0, 200) == 0 ? scenario.Next(-60, 60) : 0.05f;
                var dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                shipHost.transform.rotation = Quaternion.LookRotation(dir);
                var speed = scenario.Next(0, 4) == 0 ? 2f : 6f;
                shipHost.transform.position += dir * speed / 60f;
                velocity.SetValue(ship, dir * speed);
                if (scenario.Next(0, 15) == 0) { effects.NotifyKill(); items.NotifyKill(); if ((int)bellCounter.GetValue(effects) == 0 && inventory.HasItem("captains_bell")) v3Bells++; }
                if (scenario.Next(0, 90) == 0) { effects.NotifyPlayerTookDamage(); items.NotifyPlayerTookDamage(); }

                var hadBuff = effects.GetAttackSpeedMultiplier() > 1f;
                gloves.SetValue(effects, Mathf.Max(0f, (float)gloves.GetValue(effects) - dt));
                lantern.SetValue(effects, Mathf.Max(0f, (float)lantern.GetValue(effects) - dt));
                track.Invoke(effects, new object[] { dt });
                refresh.Invoke(effects, null);
                items.Tick(dt);
                if (hadBuff && effects.GetAttackSpeedMultiplier() == 1f) turnBuffEnded = true;

                var tag = $"frame {frame}";
                Assert.AreEqual(effects.GetAttackSpeedMultiplier(), items.AttackSpeedMultiplier(), tag + " attack speed");
                Assert.AreEqual(effects.GetCritChanceBonus(), items.CritChanceBonus(), tag + " crit bonus");
                Assert.AreEqual(effects.GetIncomingDamageMultiplier(ship), items.IncomingDamageMultiplier(), tag + " incoming damage");
                Assert.AreEqual(effects.GetGlobalDamageMultiplier(record), items.GlobalDamageMultiplier(), tag + " damage");
                Assert.AreEqual(v3Bells, v4Bells, tag + " Captain's Bell");
            }
            CollectionAssert.AreEqual(inventory.Items, items.Items);
            foreach (var id in ids) Assert.AreEqual(inventory.GetStackCount(id), items.StackCount(id), id);
            foreach (StatId s in Enum.GetValues(typeof(StatId))) Assert.AreEqual(rm.Stats.Get(s), v4Stats.Get(s), s.ToString());
            Assert.Greater(v4Bells, 0, "the Bell rang");
            Assert.IsTrue(turnBuffEnded, "the Gloves' buff ran out at least once");
        }

        // ---- Captains (V3 V3CaptainController + V3CaptainActiveController) ---------------------------------------------

        sealed class CaptainShip : ICaptainShip
        {
            public float SpeedValue;
            public readonly List<string> Calls = new List<string>();
            public bool Exists => true;
            public float Speed => SpeedValue;
            public void InitializeHull(float baseHull) => Calls.Add("hull " + baseHull);
            public void SyncMaxHullKeepRatio() => Calls.Add("sync hull");
            public void ApplySlow(float multiplier, float seconds) => Calls.Add($"slow {multiplier} {seconds}");
            public void ApplyMoveEffect(float multiplier, float seconds) => Calls.Add($"move {multiplier} {seconds}");
        }

        sealed class CaptainRun : ICaptainRun
        {
            public Func<int> GoldOf;
            public bool Exists => true;
            public bool IsPaused => false;
            public bool IsRunEnded => false;
            public int Gold => GoldOf();
            public int PlayerLevel => 1; // V3 reads LevelSystem.Instance.Level, else 1 (no level system here)
        }

        [Test]
        public void Captains_EqualV3sControllers_ForEveryCaptain()
        {
            using var set = Parity();
            const string activesKey = "combat_captain_actives_enabled";
            var hadKey = PlayerPrefs.HasKey(activesKey);
            var old = PlayerPrefs.GetInt(activesKey, 0);
            PlayerPrefs.SetInt(activesKey, 1);
            try
            {
                foreach (V3CaptainId id in Enum.GetValues(typeof(V3CaptainId)))
                {
                    var host = new GameObject("run");
                    _created.Add(host);
                    var rm = Awake<RunManager>(host);
                    rm.BeginRun(3);
                    var shipHost = new GameObject("ship");
                    _created.Add(shipHost);
                    var damage = shipHost.AddComponent<HighSeas.Combat.DamageReceiver>();
                    var ship = shipHost.AddComponent<HighSeas.Player.PlayerShipController>();
                    typeof(HighSeas.Player.PlayerShipController).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ship, damage);
                    var velocity = typeof(HighSeas.Player.PlayerShipController).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
                    var captainHost = new GameObject("captain");
                    _created.Add(captainHost);
                    var v3 = Awake<V3CaptainController>(captainHost);
                    v3.Initialize(id, ship);
                    var active = Awake<V3CaptainActiveController>(captainHost);
                    active.Initialize(id);
                    active.ResetForNewRun();

                    var v4Stats = new PlayerStats();
                    var resolver = new StatResolver(() => v4Stats);
                    var v4Ship = new CaptainShip();
                    var module = new CaptainModule(resolver, v4Ship, new CaptainRun { GoldOf = () => rm.Gold }, () => true);
                    var config = set.All<CaptainConfig>().Single(c => c.legacyId == (int)id);
                    module.BeginRun(config);
                    Assert.AreEqual(rm.Stats.Get(StatId.Hull), v4Stats.Get(StatId.Hull), id + " hull base");
                    Assert.AreEqual(rm.Stats.Get(StatId.MoveSpeed), v4Stats.Get(StatId.MoveSpeed), id + " speed base");
                    Assert.AreEqual("hull " + V3CaptainProfile.Get(id).BaseHull, v4Ship.Calls[0], id + " InitializeFromRun");
                    Assert.AreEqual(id == V3CaptainId.Brine, v4Ship.Calls.Contains("sync hull"), id + " Hank's hull resync");

                    // V3's two Updates with an explicit frame (Time.deltaTime is 0 in edit mode).
                    const float dt = 1f / 60f;
                    string[] timers = { "_cooldownRemaining", "_braceTimer", "_kegRadiusBoostTimer", "_richieTimer", "_emberFlameTimer", "_roxieTimer", "_richieDamageBuffTimer" };
                    var fastMove = typeof(V3CaptainController).GetField("_fastMoveTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                    var fuse = typeof(V3CaptainController).GetField("_runningFuseActive", BindingFlags.Instance | BindingFlags.NonPublic);
                    var scenario = new System.Random((int)id + 11);
                    var uses = 0;
                    for (var frame = 0; frame < 3600; frame++)
                    {
                        var speed = scenario.Next(0, 90) == 0 ? 2f : 6f;
                        velocity.SetValue(ship, new Vector3(0f, 0f, speed));
                        v4Ship.SpeedValue = speed;
                        if (frame % 5 == 0) rm.AddGold(scenario.Next(0, 30));
                        if (id == V3CaptainId.Flint)
                        {
                            if (speed >= 4f) fastMove.SetValue(v3, (float)fastMove.GetValue(v3) + dt);
                            else { fastMove.SetValue(v3, 0f); fuse.SetValue(v3, false); }
                            if (!(bool)fuse.GetValue(v3) && (float)fastMove.GetValue(v3) >= 2f) fuse.SetValue(v3, true);
                        }
                        foreach (var t in timers)
                        {
                            var f = typeof(V3CaptainActiveController).GetField(t, BindingFlags.Instance | BindingFlags.NonPublic);
                            f.SetValue(active, Mathf.Max(0f, (float)f.GetValue(active) - dt));
                        }
                        var press = scenario.Next(0, 120) == 0;
                        if (press) { var used = active.TryActivate(); if (used) uses++; }
                        module.Tick(dt, press);

                        var tag = $"{id} frame {frame}";
                        Assert.AreEqual(v3.GetAdditiveDamageBonus() + active.GetRichieActiveDamageBonus(), module.AdditiveDamageBonus(), tag + " additive damage");
                        Assert.AreEqual(v3.GetWeaponDamageMultiplier("powder_kegs"), module.WeaponDamageMultiplier("powder_kegs"), tag + " keg damage");
                        Assert.AreEqual(active.IncomingDamageMultiplier, module.IncomingDamageMultiplier, tag + " incoming damage");
                        Assert.AreEqual(active.PowderKegBlastRadiusMultiplier, module.PowderKegBlastRadiusMultiplier, tag + " keg radius");
                        Assert.AreEqual(active.FlameRangeMultiplier, module.FlameRangeMultiplier, tag + " flame range");
                        Assert.AreEqual(active.BurnDamageMultiplier, module.BurnDamageMultiplier, tag + " burn");
                        Assert.AreEqual(active.CooldownRemaining, module.CooldownRemaining, tag + " cooldown");
                    }
                    Assert.Greater(uses, 2, id + " used its active");
                    Object.DestroyImmediate(captainHost);
                }
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(activesKey, old); else PlayerPrefs.DeleteKey(activesKey);
            }
        }

        // ---- Powerups (V3 V3FloorPowerupWorld) --------------------------------------------------------------------------

        sealed class PowerupRng : IPowerupRandom
        {
            readonly RunRng _rng;
            public PowerupRng(RunRng rng) { _rng = rng; }
            public float PowerupFloat() => _rng.PowerupFloat();
        }

        sealed class PowerupWorld : IPowerupWorld
        {
            public float Hull = 40f, MaxHull = 100f;
            public int SpeedSyncs, AttractXp, AttractLoose;
            public bool IsFreeSpot(Vector3 p, float margin, float clearance) =>
                HighSeas.World.PlayBounds.IsInside(p, margin) && !(HighSeas.World.IslandObstacles.IsAvailable && HighSeas.World.IslandObstacles.IsPositionBlocked(p, clearance));
            public bool IsBossFight => false;
            public bool ShipAlive => Hull > 0f;
            public float ShipHull => Hull;
            public float ShipMaxHull => MaxHull;
            public void HealShip(float amount) => Hull += amount;
            public void SyncShipSpeed() => SpeedSyncs++;
        }

        [Test]
        public void Powerups_EqualV3sFloorPowerupWorld_OverKillsAndPickups()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // V3 destroys its placeholder views (edit mode logs)
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile);
            V3RunContext.IsActive = true;
            try
            {
                using var set = Parity();
                var host = new GameObject("run");
                _created.Add(host);
                var rm = Awake<RunManager>(host);
                rm.BeginRun(21);
                var spawnerHost = new GameObject("spawner");
                _created.Add(spawnerHost);
                var spawner = spawnerHost.AddComponent<HighSeas.Enemies.EncounterSpawner>(); // Dead Calm's V3 sink
                var shipHost = new GameObject("ship");
                _created.Add(shipHost);
                var damage = shipHost.AddComponent<HighSeas.Combat.DamageReceiver>();
                damage.Initialize(100f);
                typeof(HighSeas.Combat.DamageReceiver).GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(damage, 40f);
                var ship = shipHost.AddComponent<HighSeas.Player.PlayerShipController>();
                typeof(HighSeas.Player.PlayerShipController).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ship, damage);
                var worldHost = new GameObject("powerups");
                _created.Add(worldHost);
                var v3 = Awake<V3FloorPowerupWorld>(worldHost);
                v3.ResetForRun();

                var v4Stats = new PlayerStats();
                var resolver = new StatResolver(() => v4Stats);
                var v4Rng = new RunRng(21);
                var world = new PowerupWorld();
                var module = new PowerupModule(() => set.Single<PowerupConfig>(), new PowerupRng(v4Rng), resolver, world, null);
                module.ResetForRun();
                module.AttractXp += () => world.AttractXp++;
                module.AttractLooseRewards += () => world.AttractLoose++;
                var agentsField = typeof(V3FloorPowerupWorld).GetField("_agents", BindingFlags.Instance | BindingFlags.NonPublic);

                var scenario = new System.Random(5);
                var player = Vector3.zero;
                var drops = new List<Vector3>();
                for (var frame = 0; frame < 20000; frame++)
                {
                    const float dt = 1f / 60f;
                    if (scenario.Next(0, 4) == 0)
                    {
                        var at = new Vector3(scenario.Next(-40, 40), 0f, scenario.Next(-40, 40));
                        var elite = scenario.Next(0, 300) == 0;
                        var boss = scenario.Next(0, 2000) == 0;
                        var before = v3.SpawnedThisStage;
                        v3.TryRollOnKill(at, boss, elite);
                        module.TryRollOnKill(at, boss, elite);
                        if (v3.SpawnedThisStage > before) drops.Add(at);
                    }
                    // The ship sails to the latest drop, so most powerups are collected before they expire.
                    var target = drops.Count > 0 ? drops[drops.Count - 1] : Vector3.zero;
                    player = Vector3.MoveTowards(player, target, 10f * dt);
                    v3.Tick(dt, player);
                    module.Tick(dt, player);

                    var tag = $"frame {frame}";
                    Assert.AreEqual(v3.SpawnedThisStage, module.SpawnedThisStage, tag + " spawned");
                    var agents = (Array)agentsField.GetValue(v3);
                    var active = 0;
                    foreach (var a in agents) if ((bool)a.GetType().GetField("active").GetValue(a)) active++;
                    Assert.AreEqual(active, module.ActiveCount, tag + " on the water");
                    foreach (StatId s in Enum.GetValues(typeof(StatId))) Assert.AreEqual(rm.Stats.Get(s), v4Stats.Get(s), $"{tag} {s}");
                    Assert.AreEqual(spawner.DebugSpawnRateMultiplier, module.SpawnRateMultiplier, tag + " Dead Calm");
                    Assert.AreEqual(damage.CurrentHP, world.Hull, tag + " hull");
                }
                Assert.AreEqual(rm.Rng.PowerupFloat(), v4Rng.PowerupFloat(), "same Powerup draws");
                Assert.AreEqual(set.Single<PowerupConfig>().maxPerStage, module.SpawnedThisStage, "the stage cap was reached");
                Assert.Greater(world.AttractLoose, 0, "a Magnetic Tide was collected");
            }
            finally
            {
                V3RunContext.IsActive = false;
            }
        }

        [Test]
        public void WeaponOfferData_EqualsV3sRunDatabase()
        {
            var db = RunDatabase();
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            foreach (var w in set.configs.OfType<WeaponConfig>())
            {
                var def = db.weapons[w.databaseOrder];
                Assert.AreEqual(def.id, w.weaponId, "database order");
                Assert.AreEqual(def.maxLevel, w.maxLevel, w.weaponId);
                var v3 = def.GetUpgradeConfigs();
                Assert.AreEqual(v3.Count, w.upgradeAxes.Length, w.weaponId + " axes (V3's pattern defaults where the asset has none)");
                for (var i = 0; i < v3.Count; i++)
                {
                    Assert.AreEqual((int)v3[i].axis, (int)w.upgradeAxes[i].axis);
                    Assert.AreEqual(v3[i].baseIncrement, w.upgradeAxes[i].baseIncrement);
                    Assert.AreEqual(v3[i].offerWeight, w.upgradeAxes[i].offerWeight);
                }
            }
        }
    }
}
