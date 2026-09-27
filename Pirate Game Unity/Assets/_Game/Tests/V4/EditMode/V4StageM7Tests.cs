using System.Collections.Generic;
using System.IO;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.World;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Views;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4StageM7Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static string ParityPresetPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v4-verification", "presets", "v3-parity.json"));
        readonly List<Object> _owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        // ---- Fakes ----------------------------------------------------------------------------------------------------

        sealed class Log
        {
            public readonly List<string> Lines = new List<string>();
            public void Add(string s) => Lines.Add(s);
            public int Count(string prefix) => Lines.Count(l => l.StartsWith(prefix));
        }

        sealed class FakeRun : IStageRun
        {
            readonly Log _log;
            public FakeRun(Log log) => _log = log;
            public bool Exists => true;
            public bool IsRunEnded { get; set; }
            public bool IsPaused => PauseReason != PauseReason.None;
            public PauseReason PauseReason { get; set; }
            public RunState State { get; set; } = RunState.Playing;
            public float ElapsedSeconds { get; set; }
            public int Gold { get; set; } = 1000;
            public int RunSeed => 101;
            public float MaxHull { get; set; } = 100f;
            public float Luck => 0f;
            public void SetState(RunState state) { State = state; _log.Add("state " + state); }
            public void SetPause(PauseReason reason) { PauseReason = reason; _log.Add("pause " + reason); }
            public void AddGold(int amount) { Gold += amount; _log.Add("gold +" + amount); }
            public bool TrySpendGold(int amount) { if (Gold < amount) return false; Gold -= amount; _log.Add("spend " + amount); return true; }
            public void SecureObjective() => _log.Add("secure");
            public void CompleteRun(bool victory) => _log.Add("complete " + victory);
            public void AddStatModifier(int stat, float add, float multiply) => _log.Add($"stat {stat} +{add} x{multiply}");
        }

        sealed class FakeShip : IStageShip
        {
            public Vector3 Pos;
            public float Hp = 100f;
            public bool Exists => true;
            public Vector3 Position => Pos;
            public float CurrentHp => Hp;
            public void Heal(float amount) => Hp += amount;
        }

        sealed class FakeEconomy : IStageEconomy
        {
            readonly Log _log;
            public int Price = 20;
            public bool InventoryFull;
            public string PlanError;
            public int MapResult;
            public FakeEconomy(Log log) => _log = log;
            public int QuoteChestPrice() => Price;
            public int ProgressChestPrice(int paidChests) => Price;
            public ChestPlan PlanChest(IReadOnlyList<string> equippedWeaponIds)
            {
                _log.Add("plan " + (equippedWeaponIds == null ? "none" : string.Join(",", equippedWeaponIds)));
                return PlanError != null ? new ChestPlan(false, Price, null, PlanError) : new ChestPlan(true, Price, "item_x", null);
            }
            public void RegisterChestPurchase() => _log.Add("purchase");
            public bool TryAddItem(string itemId) { _log.Add("item " + itemId); return !InventoryFull; }
            public string ItemName(string itemId) => "Item X";
            public string RollChestPoolItem() { _log.Add("roll item"); return "cache_item"; }
            public int MapInt(int maxExclusive) { _log.Add("map " + maxExclusive); return MapResult; }
            public IReadOnlyList<string> EquippedWeaponIds => new[] { "w1" };
        }

        sealed class FakeRewards : IStageRewards
        {
            readonly Log _log;
            public FakeRewards(Log log) => _log = log;
            public void RegisterSpecialPoi(int poiIndex, POIType type) => _log.Add($"special {poiIndex} {type}");
            public void RegisterBossVictory() => _log.Add("marks boss");
            public void LockHomeSeed(int runSeed) => _log.Add("home seed " + runSeed);
            public void ChestOpened() => _log.Add("chest opened");
            public void PoiUsedTelemetry(POIType type) => _log.Add("telemetry " + type);
            public void BossSummonedTelemetry(int encounterId) => _log.Add("telemetry summoned");
            public void BossDefeatedTelemetry(int encounterId) => _log.Add("telemetry defeated");
        }

        sealed class FakeToasts : IStageToasts
        {
            public readonly List<string> Shown = new List<string>();
            public void Show(string text, float seconds) => Shown.Add(text);
        }

        sealed class FakeTracker : IEncounterTracker
        {
            public readonly Dictionary<int, (EncounterKind kind, EncounterState state, int spawned, int expected)> Records = new Dictionary<int, (EncounterKind, EncounterState, int, int)>();
            int _next = 1;
            public void ResetForRun() { Records.Clear(); _next = 1; }
            public int Request(EncounterKind kind, int expectedSpawnCount) { var id = _next++; Records[id] = (kind, EncounterState.Requested, 0, expectedSpawnCount); return id; }
            public bool Exists(int id) => Records.ContainsKey(id);
            public EncounterState State(int id) => Records.TryGetValue(id, out var r) ? r.state : EncounterState.Failed;
            public int SpawnedCount(int id) => Records.TryGetValue(id, out var r) ? r.spawned : 0;
            public bool IsTerminal(int id) => !Records.TryGetValue(id, out var r) || r.state == EncounterState.Completed || r.state == EncounterState.Failed;
            public void RegisterSpawned(int id, int unitId) { var r = Records[id]; Records[id] = (r.kind, EncounterState.Spawned, r.spawned + 1, r.expected); }
            public void MarkFailed(int id) { if (Records.TryGetValue(id, out var r)) Records[id] = (r.kind, EncounterState.Failed, r.spawned, r.expected); }
            public void Set(int id, EncounterState state) { var r = Records[id]; Records[id] = (r.kind, state, r.spawned, r.expected); }
            public bool TryGetOpenBossEncounter(out int id)
            {
                id = Records.Where(p => p.Value.kind == EncounterKind.Boss && !IsTerminal(p.Key)).Select(p => p.Key).DefaultIfEmpty(0).Last();
                return id > 0;
            }
            public bool BossObjectiveSecured => Records.Any(p => p.Value.kind == EncounterKind.Boss && p.Value.state == EncounterState.Completed);
        }

        sealed class FakeExecutor : IEncounterExecutor
        {
            readonly FakeTracker _tracker;
            readonly Log _log;
            public int SpawnOnImmediate = 1;
            public FakeExecutor(FakeTracker tracker, Log log) { _tracker = tracker; _log = log; }
            public void Trigger(EnemySpawnerConfig spawner, int count, int encounterId) => _log.Add($"trigger {spawner.name} {count} {encounterId}");
            public int TriggerImmediate(EnemySpawnerConfig spawner, int count, int encounterId)
            {
                _log.Add($"immediate {spawner.name} {count} {encounterId}");
                for (var i = 0; i < SpawnOnImmediate; i++) _tracker.RegisterSpawned(encounterId, 100 + i);
                return _tracker.SpawnedCount(encounterId);
            }
            public int ProcessNow() => 0;
            public void Cancel(int encounterId) => _log.Add("cancel " + encounterId);
            public bool HasPending(int encounterId) => false;
        }

        sealed class FakeMarkers : IPoiMarkers
        {
            public readonly Dictionary<int, Vector3> Spawned = new Dictionary<int, Vector3>();
            public readonly HashSet<int> Hidden = new HashSet<int>(), Consumed = new HashSet<int>();
            public readonly Dictionary<int, System.Action<int, float>> Ticks = new Dictionary<int, System.Action<int, float>>();
            public bool Telegraph;
            public void Clear() => Spawned.Clear();
            public void Spawn(int index, POIType type, Vector3 position, System.Action<int, float> tick) { Spawned[index] = position + Vector3.up * 0.15f; Ticks[index] = tick; }
            public bool IsSpawned(int index) => Spawned.ContainsKey(index);
            public Vector3 Position(int index) => Spawned.TryGetValue(index, out var p) ? p : Vector3.zero;
            public void Hide(int index) => Hidden.Add(index);
            public void Consume(int index) => Consumed.Add(index);
            public void SetChargeRing(int index, float charge01, float radius) { }
            public void ShowTelegraph(int index) => Telegraph = true;
            public void PulseTelegraph(int index) { }
            public void HideTelegraph(int index) => Telegraph = false;
            public void TickAll(float dt) { foreach (var t in Ticks.ToList()) t.Value(t.Key, dt); }
        }

        sealed class Rig
        {
            public Log Log = new Log();
            public FakeRun Run;
            public FakeShip Ship = new FakeShip();
            public FakeEconomy Economy;
            public FakeToasts Toasts = new FakeToasts();
            public FakeTracker Tracker = new FakeTracker();
            public FakeExecutor Executor;
            public FakeMarkers Markers = new FakeMarkers();
            public StageContentConfig Config;
            public StageContentModule Stage;
            public InteractionModel Interaction;
            public StageLayout Layout;
            public readonly List<(CombatEventKind kind, int a)> Events = new List<(CombatEventKind, int)>();

            public void Tick(float dt)
            {
                Markers.TickAll(dt);
                Stage.TickManager(dt);
            }
        }

        Rig Make(params (POIType type, Vector3 pos)[] pois)
        {
            var rig = new Rig();
            rig.Run = new FakeRun(rig.Log);
            rig.Economy = new FakeEconomy(rig.Log);
            rig.Executor = new FakeExecutor(rig.Tracker, rig.Log);
            rig.Config = ScriptableObject.CreateInstance<StageContentConfig>();
            rig.Config.bossSpawner = ScriptableObject.CreateInstance<EnemySpawnerConfig>();
            rig.Config.bossSpawner.name = "boss";
            rig.Config.powderCacheSpawner = ScriptableObject.CreateInstance<EnemySpawnerConfig>();
            rig.Config.powderCacheSpawner.name = "cache";
            _owned.Add(rig.Config);
            _owned.Add(rig.Config.bossSpawner);
            _owned.Add(rig.Config.powderCacheSpawner);
            rig.Stage = new StageContentModule(() => rig.Config, rig.Run, rig.Ship, rig.Economy, new FakeRewards(rig.Log), rig.Toasts, rig.Tracker, rig.Executor)
            {
                Publish = (k, a, b, p, t) => rig.Events.Add((k, a))
            };
            rig.Interaction = new InteractionModel(rig.Stage, rig.Run, rig.Toasts);
            rig.Layout = new StageLayout { playerSpawn = Vector3.zero, bossBeacon = new Vector3(200f, 0f, 200f) };
            foreach (var (type, pos) in pois) rig.Layout.pois.Add(new POISlot { type = type, position = pos });
            var beacon = rig.Layout.pois.FindIndex(p => p.type == POIType.BossBeacon);
            if (beacon >= 0) rig.Layout.bossBeacon = rig.Layout.pois[beacon].position;
            rig.Stage.Initialize(rig.Layout, rig.Markers);
            rig.Stage.Discovery.Initialize(rig.Layout);
            return rig;
        }

        // ---- Chests ----------------------------------------------------------------------------------------------------

        [Test]
        public void Chest_Purchase_SpendsBuysAndConsumes_AsV3()
        {
            var rig = Make((POIType.SalvageChest, new Vector3(3f, 0f, 0f)));
            Assert.IsTrue(rig.Markers.IsSpawned(0), "Saltwind spawns every chest at the start");
            rig.Stage.TryCompleteInteraction(0);
            CollectionAssert.AreEqual(new[] { "plan none", "spend 20", "purchase", "item item_x", "chest opened", "telemetry SalvageChest", "special 0 SalvageChest" }, rig.Log.Lines,
                "V3 order (and R-47: no equipped weapons passed by default)");
            Assert.IsTrue(rig.Layout.pois[0].used);
            Assert.IsTrue(rig.Markers.Consumed.Contains(0));
            Assert.AreEqual(1, rig.Stage.PaidChestCount);
            CollectionAssert.Contains(rig.Toasts.Shown, "Salvage Chest — gained Item X.");
            Assert.AreEqual((CombatEventKind.ChestOpened, 0), rig.Events.Single());
        }

        [Test]
        public void Chest_RefundsWhenTheInventoryIsFull_AndNamesMissingGold()
        {
            var rig = Make((POIType.SalvageChest, Vector3.zero), (POIType.SalvageChest, Vector3.one));
            rig.Economy.InventoryFull = true;
            rig.Stage.TryCompleteInteraction(0);
            Assert.AreEqual(1000, rig.Run.Gold, "refunded");
            Assert.IsFalse(rig.Layout.pois[0].used);
            CollectionAssert.Contains(rig.Toasts.Shown, "Inventory full — chest unchanged.");
            rig.Economy.PlanError = ChestPlan.InsufficientGold;
            rig.Stage.TryCompleteInteraction(1);
            CollectionAssert.Contains(rig.Toasts.Shown, "Need 20 run gold to open chest.");
        }

        [Test]
        public void Chest_R47Fix_PassesTheEquippedWeapons()
        {
            var rig = Make((POIType.SalvageChest, Vector3.zero));
            rig.Config.chestPlanUsesLoadout = true;
            rig.Stage.TryCompleteInteraction(0);
            Assert.AreEqual("plan w1", rig.Log.Lines[0]);
        }

        // ---- Landmarks ---------------------------------------------------------------------------------------------------

        [Test]
        public void RepairWreck_HealsAQuarter_OrRefusesAtFullHull()
        {
            var rig = Make((POIType.GildedWreck, Vector3.zero));
            rig.Stage.TryCompleteInteraction(0);
            CollectionAssert.Contains(rig.Toasts.Shown, "Hull already full — wreck unchanged.");
            Assert.IsFalse(rig.Layout.pois[0].used);
            rig.Ship.Hp = 50f;
            rig.Stage.TryCompleteInteraction(0);
            Assert.AreEqual(75f, rig.Ship.Hp);
            Assert.IsTrue(rig.Layout.pois[0].used);
            Assert.AreEqual((CombatEventKind.LandmarkUsed, 0), rig.Events.Single());
        }

        [Test]
        public void SignalBuoy_ChargesInsideTheCircle_ThenDrawsItsReward()
        {
            var rig = Make((POIType.TrialBuoy, new Vector3(10f, 0f, 0f)));
            rig.Ship.Pos = new Vector3(20f, 0f, 0f);
            for (var i = 0; i < 60; i++) rig.Tick(0.1f);
            Assert.AreEqual(0f, rig.Stage.SignalBuoyCharge(0), "no charge outside the 4 m circle");
            rig.Ship.Pos = new Vector3(12f, 0f, 0f);
            rig.Economy.MapResult = 2;
            for (var i = 0; i < 79; i++) rig.Tick(0.1f);
            Assert.IsFalse(rig.Layout.pois[0].used, "not yet: 8 s");
            rig.Tick(0.2f);
            Assert.IsTrue(rig.Layout.pois[0].used);
            CollectionAssert.Contains(rig.Log.Lines, "map 3");
            CollectionAssert.Contains(rig.Log.Lines, "stat 14 +0.8 x1", "third reward: pickup radius");
            Assert.AreEqual(1, rig.Toasts.Shown.Count(t => t == "Signal Buoy charging — stay in the circle!"));
            Assert.IsTrue(rig.Markers.Consumed.Contains(0));
        }

        [Test]
        public void PowderCache_AmbushesOnApproach_AndItsPayoutIsOnlyReachableByDirectUse()
        {
            var rig = Make((POIType.MerchantSkiff, new Vector3(30f, 0f, 0f)));
            rig.Ship.Pos = new Vector3(40f, 0f, 0f);
            rig.Tick(0.1f);
            Assert.AreEqual(0, rig.Tracker.Records.Count);
            rig.Ship.Pos = new Vector3(37f, 0f, 0f);
            rig.Tick(0.1f);
            CollectionAssert.Contains(rig.Log.Lines, "trigger cache 3 1");
            Assert.IsTrue(rig.Markers.Hidden.Contains(0));
            CollectionAssert.Contains(rig.Toasts.Shown, "Powder Cache — ambush incoming!");
            Assert.IsFalse(rig.Stage.CanInteract(0), "hidden while the ambush lasts");

            Assert.IsFalse(rig.Stage.Interact(POIType.MerchantSkiff, 0), "not cleared yet");
            rig.Tracker.Set(1, EncounterState.Completed);
            rig.Tick(0.1f);
            rig.Interaction.Tick(0.1f);
            Assert.IsFalse(rig.Layout.pois[0].used, "V3 quirk (R-97): the hidden cache is never targeted again, so it never pays in play");
            // The payout code itself (reached only by direct use in V3):
            Assert.IsTrue(rig.Stage.Interact(POIType.MerchantSkiff, 0));
            CollectionAssert.IsSubsetOf(new[] { "roll item", "item cache_item", "gold +20", "special 0 MerchantSkiff" }, rig.Log.Lines);
        }

        [Test]
        public void PowderCache_R97Fix_PaysWhenTheAmbushIsCleared()
        {
            var rig = Make((POIType.MerchantSkiff, new Vector3(30f, 0f, 0f)));
            rig.Config.powderCachePaysWhenCleared = true;
            rig.Ship.Pos = new Vector3(37f, 0f, 0f);
            rig.Tick(0.1f);
            rig.Tracker.Set(1, EncounterState.Completed);
            rig.Tick(0.1f);
            Assert.IsTrue(rig.Layout.pois[0].used);
            CollectionAssert.IsSubsetOf(new[] { "roll item", "item cache_item", "gold +20" }, rig.Log.Lines);
            Assert.AreEqual(1, rig.Events.Count(e => e.kind == CombatEventKind.LandmarkUsed));
        }

        [Test]
        public void ModuleLab_StageOff_RefusesTheBossSummon()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            rig.Stage.SetEnabled(false);
            Assert.IsFalse(rig.Stage.TrySummonBoss());
            Assert.AreEqual(0, rig.Log.Count("immediate"), "nothing summoned in the lab");
        }

        [Test]
        public void ChestPrices_FollowV3sSequence()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var runtime = new ConfigSetRuntime(set);
            var asset = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>("Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            var profile = Object.Instantiate(asset);
            _owned.Add(profile);
            LegacyConfigMapping.ApplyStage(runtime.Single<StageContentConfig>(), profile);
            var export = profile.ToExport();
            CollectionAssert.AreEqual(new[] { 20, 31, 38, 46, 56 }, Enumerable.Range(0, 5).Select(n => SaltwindStageEconomyLogic.QuoteChestPrice(export, n)).ToArray());
        }

        [Test]
        public void TreasureChart_RevealsTheNearestChests_AndGivesTheBeaconBearing()
        {
            var rig = Make((POIType.CursedIdol, Vector3.zero), (POIType.SalvageChest, new Vector3(5f, 0f, 0f)), (POIType.SalvageChest, new Vector3(500f, 0f, 0f)),
                (POIType.BossBeacon, new Vector3(0f, 0f, 100f)));
            rig.Stage.TryCompleteInteraction(0);
            Assert.IsTrue(rig.Stage.Discovery.IsPoiVisible(1));
            Assert.IsTrue(rig.Stage.Discovery.IsPoiVisible(2), "three nearest: both chests");
            CollectionAssert.Contains(rig.Toasts.Shown, "Treasure Chart — chests revealed; beacon bearing 0°.");
        }

        // ---- Beacon and boss -----------------------------------------------------------------------------------------------

        [Test]
        public void Beacon_TelegraphsThenSummons_AndTheBossFightStarts()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            rig.Stage.TryCompleteInteraction(0);
            Assert.IsTrue(rig.Markers.Telegraph);
            Assert.IsTrue(rig.Stage.IsTelegraphing(0));
            rig.Tick(0.5f);
            Assert.IsFalse(rig.Stage.BossSummoned, "0.7 s telegraph");
            rig.Tick(0.3f);
            Assert.IsTrue(rig.Stage.BossSummoned);
            CollectionAssert.Contains(rig.Log.Lines, "immediate boss 1 1");
            Assert.AreEqual(RunState.BossFight, rig.Run.State);
            Assert.IsTrue(rig.Layout.pois[0].used && rig.Markers.Consumed.Contains(0));
            Assert.AreEqual(1, rig.Events.Count(e => e.kind == CombatEventKind.BossState && e.a == 1));
        }

        [Test]
        public void Beacon_FailedSummon_Toasts_AndV3sStickyTelegraphIsKept()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            rig.Executor.SpawnOnImmediate = 0;
            rig.Stage.TryCompleteInteraction(0);
            rig.Tick(1f);
            Assert.IsFalse(rig.Stage.BossSummoned);
            CollectionAssert.Contains(rig.Toasts.Shown, "Beacon failed — clear space near the beacon and try again.");
            var before = rig.Log.Count("immediate");
            rig.Stage.TryCompleteInteraction(0);
            rig.Tick(1f);
            Assert.AreEqual(before, rig.Log.Count("immediate"), "V3 quirk: the telegraph flag stays set, the Beacon can't retry");
        }

        [Test]
        public void BossVictory_FollowsV3sOrder_AndTheRunContinues()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            Assert.IsTrue(rig.Stage.TrySummonBoss());
            rig.Log.Lines.Clear();
            rig.Stage.OnBossKilled();
            Assert.IsFalse(rig.Stage.BossDefeated, "only once the boss encounter completed");
            rig.Tracker.Set(1, EncounterState.Completed);
            rig.Stage.OnBossKilled();
            CollectionAssert.AreEqual(new[] { "marks boss", "telemetry defeated", "secure", "home seed 101", "state Playing" }, rig.Log.Lines);
            CollectionAssert.Contains(rig.Toasts.Shown, "TERRITORY OBJECTIVE SECURED — survive or keep farming.");
            rig.Stage.OnBossKilled();
            Assert.AreEqual(1, rig.Log.Count("secure"), "once");
        }

        [Test]
        public void SameTickDeath_CountsTheWin_OnlyIfTheBossWasSummoned()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            rig.Stage.TrySecureIfBossDead();
            Assert.IsFalse(rig.Stage.BossDefeated);
            rig.Stage.TrySummonBoss();
            rig.Tracker.Set(1, EncounterState.Completed);
            rig.Run.IsRunEnded = true;
            rig.Stage.TrySecureIfBossDead();
            Assert.IsTrue(rig.Stage.BossDefeated);
            Assert.IsFalse(rig.Log.Lines.Contains("state Playing"), "the run already ended");
        }

        // ---- Discovery ---------------------------------------------------------------------------------------------------

        [Test]
        public void BeaconTimeline_KeepsV3sQuirks_ByDefault()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 300f)));
            var d = rig.Stage.Discovery;
            d.Tick(false, 299f);
            Assert.AreEqual(BeaconReveal.Hidden, d.Beacon);
            d.Tick(false, 300f);
            Assert.AreEqual(BeaconReveal.DirectionSector, d.Beacon);
            d.Tick(false, 480f);
            Assert.AreEqual(BeaconReveal.DirectionSector, d.Beacon, "R-94: the exact marker never follows the hint");
            rig.Ship.Pos = new Vector3(0f, 0f, 290f);
            d.Tick(false, 481f);
            Assert.AreEqual(BeaconReveal.DirectionSector, d.Beacon, "R-93: sailing to it no longer counts as finding it");
        }

        [Test]
        public void BeaconTimeline_Fixes_WhenSwitchedOn()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 300f)));
            rig.Config.beaconExactMarkerAfterHint = true;
            rig.Config.beaconFoundByProximityAfterHints = true;
            var d = rig.Stage.Discovery;
            d.Tick(false, 300f);
            d.Tick(false, 420f);
            Assert.AreEqual(BeaconReveal.ExactMarker, d.Beacon);
            rig.Ship.Pos = new Vector3(0f, 0f, 290f);
            d.Tick(false, 421f);
            Assert.AreEqual(BeaconReveal.Discovered, d.Beacon);
            CollectionAssert.Contains(rig.Toasts.Shown, "Treasury Beacon spotted — follow the guide.");
        }

        [Test]
        public void Discovery_FindsPoisInRadius_AndTheBeaconOnlyWhenDiscovered()
        {
            var rig = Make((POIType.GildedWreck, new Vector3(20f, 0f, 0f)), (POIType.GildedWreck, new Vector3(30f, 0f, 0f)), (POIType.BossBeacon, new Vector3(0f, 0f, 20f)));
            var d = rig.Stage.Discovery;
            d.Tick(false, 1f);
            Assert.IsTrue(d.IsPoiVisible(0));
            Assert.IsFalse(d.IsPoiVisible(1), "beyond 24 m");
            Assert.IsTrue(d.IsPoiVisible(2), "the Beacon within 24 m is discovered");
            Assert.IsTrue(d.IsCellExplored(Vector3.zero));
            Assert.IsFalse(d.IsCellExplored(new Vector3(300f, 0f, 300f)));
        }

        // ---- Interaction -------------------------------------------------------------------------------------------------

        [Test]
        public void ChestPrompt_PausesForTheChoice_AndSkipResumes()
        {
            var rig = Make((POIType.SalvageChest, new Vector3(3f, 0f, 0f)));
            rig.Run.State = RunState.BossFight;
            rig.Interaction.Tick(0.016f);
            Assert.IsTrue(rig.Interaction.Visible);
            Assert.AreEqual(PromptLayout.SalvageChestPopup, rig.Interaction.Layout);
            Assert.AreEqual(PauseReason.POIChoice, rig.Run.PauseReason);
            Assert.AreEqual(RunState.POIChoicePaused, rig.Run.State);
            StringAssert.Contains("Take (E)  ·  Skip (R)", rig.Interaction.Text);

            rig.Interaction.SkipChest();
            Assert.AreEqual(PauseReason.None, rig.Run.PauseReason);
            Assert.AreEqual(RunState.BossFight, rig.Run.State, "back to the state before the choice");
            rig.Interaction.Tick(0.016f);
            Assert.IsFalse(rig.Interaction.Visible, "skipped until the ship leaves its range");
            rig.Ship.Pos = new Vector3(50f, 0f, 0f);
            rig.Interaction.Tick(0.016f);
            rig.Ship.Pos = Vector3.zero;
            rig.Interaction.Tick(0.016f);
            // V3 quirk (R-96): the "forget the skip when the ship leaves" branch can't run (an out-of-range target is
            // dropped first), so a skipped chest stays skipped until another chest is skipped.
            Assert.IsFalse(rig.Interaction.Visible, "V3: a skipped chest is not offered again");
        }

        [Test]
        public void ChestPrompt_R96Fix_OffersASkippedChestAgainAfterLeaving()
        {
            var rig = Make((POIType.SalvageChest, new Vector3(3f, 0f, 0f)));
            rig.Config.forgetChestSkipWhenLeaving = true;
            rig.Interaction.Tick(0.016f);
            rig.Interaction.SkipChest();
            rig.Interaction.Tick(0.016f);
            Assert.IsFalse(rig.Interaction.Visible, "skipped while the ship stays");
            rig.Ship.Pos = new Vector3(50f, 0f, 0f);
            rig.Interaction.Tick(0.016f);
            rig.Ship.Pos = Vector3.zero;
            rig.Interaction.Tick(0.016f);
            Assert.IsTrue(rig.Interaction.Visible, "offered again on the way back");
            Assert.AreEqual(PauseReason.POIChoice, rig.Run.PauseReason);
        }

        [Test]
        public void ChestPrompt_WithoutGold_ToastsOncePerApproach()
        {
            var rig = Make((POIType.SalvageChest, new Vector3(3f, 0f, 0f)));
            rig.Run.Gold = 5;
            for (var i = 0; i < 5; i++) rig.Interaction.Tick(0.016f);
            Assert.AreEqual(1, rig.Toasts.Shown.Count(t => t == "Not enough funds — need 20 gold."));
            Assert.AreEqual(PauseReason.None, rig.Run.PauseReason);
        }

        [Test]
        public void Interaction_KeepsAUsedTarget_WhileInRange_AsV3()
        {
            var rig = Make((POIType.SalvageChest, new Vector3(3f, 0f, 0f)), (POIType.SalvageChest, new Vector3(2f, 0f, 1f)));
            rig.Interaction.Tick(0.016f);
            var first = rig.Interaction.Target;
            rig.Stage.TryCompleteInteraction(first);
            rig.Run.PauseReason = PauseReason.None;
            rig.Interaction.Tick(0.016f);
            Assert.AreEqual(first, rig.Interaction.Target, "the opened chest stays the target while the ship is in range");
            Assert.IsFalse(rig.Interaction.Visible);
        }

        [Test]
        public void BeaconPrompt_AfterTheSummon_LoopsAsV3_AndTheR101FixHidesIt()
        {
            foreach (var fix in new[] { false, true })
            {
                var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 3f)));
                rig.Config.promptReleasesUsedLandmark = fix;
                rig.Interaction.TouchHolding = true;
                for (var i = 0; i < 50; i++) rig.Interaction.Tick(0.016f); // hold 0.7 s: telegraph starts
                rig.Tick(0.8f); // telegraph ends: boss summoned, Beacon used
                Assert.IsTrue(rig.Stage.BossSummoned);
                var texts = new List<string>();
                var visible = 0;
                for (var i = 0; i < 120; i++)
                {
                    rig.Interaction.Tick(0.016f);
                    if (rig.Interaction.Visible) { visible++; texts.Add(rig.Interaction.Text); }
                }
                if (fix)
                {
                    Assert.AreEqual(0, visible, "R-101 fix: no prompt on the used Beacon");
                    Assert.AreEqual(-1, rig.Interaction.Target);
                }
                else
                {
                    Assert.AreEqual(120, visible, "V3: the used Beacon keeps its prompt while E is held");
                    Assert.Greater(texts.Count(t => t.EndsWith("hold 100%")), 1, "V3: the hold loops 0-100% without effect");
                }
            }
        }

        [Test]
        public void Beacon_R96Fix_CanBeRetriedAfterAFailedSummon()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 5f)));
            rig.Config.beaconRetryAfterFailedSummon = true;
            rig.Executor.SpawnOnImmediate = 0;
            rig.Stage.TryCompleteInteraction(0);
            rig.Tick(1f);
            Assert.IsFalse(rig.Stage.BossSummoned);
            Assert.IsFalse(rig.Stage.IsTelegraphing(0), "the telegraph ends with the failed summon");
            Assert.IsFalse(rig.Markers.Telegraph);
            // In a run the queued boss encounter keeps retrying for up to 10 s, then fails; the next activation asks again.
            rig.Tracker.Set(1, EncounterState.Failed);
            rig.Executor.SpawnOnImmediate = 1;
            rig.Stage.TryCompleteInteraction(0);
            rig.Tick(1f);
            Assert.IsTrue(rig.Stage.BossSummoned, "the retry summons the boss");
        }

        [Test]
        public void BeaconPrompt_NeedsA0_7sHold()
        {
            var rig = Make((POIType.BossBeacon, new Vector3(0f, 0f, 3f)));
            rig.Interaction.Tick(0.016f);
            Assert.AreEqual(PromptLayout.CenterCompact, rig.Interaction.Layout);
            Assert.AreEqual("Activate Treasury Beacon — hold E or USE (0.7s)", rig.Interaction.Text);
            rig.Interaction.TouchHolding = true;
            for (var i = 0; i < 40; i++) rig.Interaction.Tick(0.016f);
            Assert.IsFalse(rig.Stage.IsTelegraphing(0), "0.64 s");
            for (var i = 0; i < 5; i++) rig.Interaction.Tick(0.016f);
            Assert.IsTrue(rig.Stage.IsTelegraphing(0));
        }

        // ---- Config ------------------------------------------------------------------------------------------------------------

        [Test]
        public void StageConfig_HoldsV3sValues_ExceptTheAdoptedNoBarrels()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            var stage = set.configs.OfType<StageContentConfig>().Single();
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>("Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            Assert.AreEqual(profile.salvageChestCount, stage.salvageChestCount);
            Assert.AreEqual(profile.chestBasePrice, stage.chestBasePrice);
            Assert.AreEqual(profile.chestPriceGrowth, stage.chestPriceGrowth);
            Assert.AreEqual(profile.chestPriceIncrementBase, stage.chestPriceIncrementBase);
            Assert.AreEqual(profile.disableGoldTriggeredChestSpawn, stage.disableGoldTriggeredChestSpawn);
            Assert.AreEqual(profile.signalBuoyOccupancySeconds, stage.signalBuoyChargeSeconds);
            Assert.AreEqual(profile.powderCacheBonusGold, stage.powderCacheBonusGold);
            Assert.AreEqual(profile.beaconDirectionHintSeconds, stage.beaconDirectionHintSeconds);
            Assert.AreEqual(profile.beaconExactMarkerSeconds, stage.beaconExactMarkerSeconds);
            Assert.AreEqual(0, stage.explosiveBarrelCount, "adopted decision: no barrels");
            Assert.AreEqual(2, profile.explosiveBarrelCount);
            Assert.IsNotNull(stage.bossSpawner);
            Assert.IsNotNull(stage.powderCacheSpawner);
            Assert.IsTrue(stage.chestPlanUsesLoadout && stage.beaconExactMarkerAfterHint && stage.beaconFoundByProximityAfterHints && stage.forgetChestSkipWhenLeaving
                && stage.beaconRetryAfterFailedSummon && stage.powderCachePaysWhenCleared && stage.promptReleasesUsedLandmark,
                "fixes approved at the M7 playtest (R-47, R-93, R-94, R-96, R-97, R-101) are on; the v3-parity preset turns them off");
            Assert.IsNotNull(set.configs.OfType<BossConfig>().SingleOrDefault(), "boss config in the set");
        }

        [Test]
        public void RuntimeProfile_WithTheParityPreset_EqualsTheV3Asset()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var parity = new ConfigSetRuntime(set);
            var errors = new List<string>();
            Assert.IsTrue(PresetCodec.Apply(parity, PresetCodec.FromJson(File.ReadAllText(ParityPresetPath)), 0f, errors), string.Join("\n", errors));
            var asset = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>("Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            var runtime = Object.Instantiate(asset);
            _owned.Add(runtime);
            LegacyConfigMapping.ApplyProfile(parity.Single<WaveTimelineConfig>(), parity.Single<DifficultyCurveConfig>(), runtime, parity.Single<StageContentConfig>());
            Assert.AreEqual(asset.ToExport().ContentHash, runtime.ToExport().ContentHash, "with V3's barrel count the stage fields equal V3's profile");
        }
    }
}
