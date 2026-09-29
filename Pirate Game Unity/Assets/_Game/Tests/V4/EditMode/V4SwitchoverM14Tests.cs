using System.Collections.Generic;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Spawning;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M14 switch-over: each V4 replacement equals the legacy piece it retires.</summary>
    [Category("V4")]
    public sealed class V4SwitchoverM14Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        static IEnumerable<SpawnProfile> V3ScheduleProfiles()
        {
            var s = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule");
            foreach (var e in s.entries ?? new ScheduledSpawnEntry[0]) if (e.profile != null) yield return e.profile;
            foreach (var p in new[] { s.hammerheadBeat, s.krakenBeat, s.swarmPulse, s.drownedPursuer, s.drownedHarrier, s.routeTreasureRaider,
                         s.routeCursedRingSwordfish, s.routeCursedRingCannon, s.powderCacheAmbush, s.bossSummon })
                if (p != null) yield return p;
        }

        static string Blocks(SpawnProfile p) => string.Join("|", new object[]
        {
            p.ResolvedId, p.spawnMode, p.groupLayout, p.specialBehavior,
            JsonUtility.ToJson(p.timing), JsonUtility.ToJson(p.placement), JsonUtility.ToJson(p.elite), JsonUtility.ToJson(p.movement),
            JsonUtility.ToJson(p.attack), JsonUtility.ToJson(p.formation), p.summon.minionProfile != null ? p.summon.minionProfile.ResolvedId : "-",
            p.summon.minionsPerWave, p.summon.spawnIntervalSeconds, p.summon.maxAliveMinions, p.summon.minionLifetimeSeconds, p.summon.inheritElite,
            p.summon.despawnOnParentDeath, JsonUtility.ToJson(p.tether), JsonUtility.ToJson(p.supportAura), JsonUtility.ToJson(p.phaseDash),
            JsonUtility.ToJson(p.terrainAmbush), JsonUtility.ToJson(p.adaptive),
        });

        static string Modules(SpawnProfile p) => string.Join("|", new object[]
        {
            p.specialBehavior, JsonUtility.ToJson(p.formation), JsonUtility.ToJson(p.tether), JsonUtility.ToJson(p.supportAura),
            JsonUtility.ToJson(p.phaseDash), JsonUtility.ToJson(p.terrainAmbush), JsonUtility.ToJson(p.adaptive),
            p.summon.minionsPerWave, p.summon.spawnIntervalSeconds, p.summon.maxAliveMinions, p.summon.minionLifetimeSeconds,
            p.summon.inheritElite, p.summon.despawnOnParentDeath,
        });

        /// <summary>
        /// 14.2/14.4: the V4 spawner profiles (config only) equal what the legacy config bridge built — a clone of the V3
        /// schedule profile with the config's blocks written in and its enemy pointed at the run's copy of the anchor — block
        /// by block (the direct old-vs-new comparison ran green before LegacyConfigRuntime was deleted), and their special-
        /// behaviour modules equal V3's assets (they were passed through before M14).
        /// </summary>
        [Test]
        public void SpawnerProfiles_EqualTheLegacyConstruction_AndTheV3Modules()
        {
            using var set = Default();
            using var runtimeDatabase = new V4RuntimeDatabase(set); // the run's database (includes the Drowned enemies)
            var db = runtimeDatabase.Build(V3ConfigMigrator.LoadCleanDatabase());
            var v4 = new SpawnerProfiles(set);
            var clones = new List<Object>();
            try
            {
                v4.Build();
                v4.BindEnemies(db.GetEnemy);
                var v3 = V3ScheduleProfiles().ToList();
                var timeline = set.Single<WaveTimelineConfig>();
                var compared = 0;
                foreach (var spawner in set.All<EnemySpawnerConfig>())
                {
                    var b = v4.For(spawner);
                    Assert.IsNotNull(b, spawner.configId);
                    var source = v3.FirstOrDefault(p => p.ResolvedId == spawner.legacyProfileId);
                    // The legacy construction: V3's profile (or a fresh one for a V4-only spawner) + the config's blocks.
                    var a = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<SpawnProfile>();
                    clones.Add(a);
                    if (source == null) a.profileId = spawner.legacyProfileId;
                    V3ConfigMapping.ApplySpawner(spawner, a);
                    var band = timeline.phases.FirstOrDefault(ph => ph.spawner == spawner && ph.phaseId.StartsWith(PhaseIds.BandPrefix));
                    if (band != null && band.ratePerSecond > 0f) a.timing.spawnIntervalSeconds = 1f / band.ratePerSecond;
                    if (spawner.anchor != null && db.GetEnemy(spawner.anchor.enemyId) is var anchored && anchored != null) a.enemy = anchored;
                    Assert.AreEqual(Blocks(a), Blocks(b), spawner.configId + " blocks");
                    Assert.AreSame(a.enemy, b.enemy, spawner.configId + " enemy");
                    if (source != null) Assert.AreEqual(Modules(source), Modules(b), spawner.configId + " modules equal V3's asset");
                    compared++;
                }
                Assert.GreaterOrEqual(compared, 14);
            }
            finally
            {
                v4.Dispose();
                foreach (var o in clones) Object.DestroyImmediate(o);
            }
        }

        /// <summary>
        /// 14.3: the stage profile the shared V3 code reads, built from the V4 configs alone, exports what the clone of V3's
        /// asset with pass-through fields exported (field-by-field JSON; only the unused schedule snapshot is gone), with the
        /// default config and with the v3-parity preset.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void StageProfilePort_ExportsWhatTheLegacyCloneExported(bool v3Parity)
        {
            using var set = Default();
            if (v3Parity)
            {
                var json = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
                var errors = new List<string>();
                Assert.IsTrue(PresetCodec.Apply(set, PresetCodec.FromJson(json), 0f, errors), string.Join("; ", errors));
            }
            // The legacy construction: a clone of V3's asset with the V4-owned fields written in (the direct old-vs-new
            // comparison ran green before LegacyConfigRuntime was deleted).
            var legacyProfile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath));
            V3ConfigMapping.ApplyProfile(set.Single<WaveTimelineConfig>(), set.Single<DifficultyCurveConfig>(), legacyProfile, set.Single<StageContentConfig>());
            legacyProfile.encounterSchedule = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule");
            var port = new LegacyStageProfilePort(set);
            try
            {
                var expected = legacyProfile.ToExport();
                var actual = port.Profile.ToExport();
                // Intended difference: the export's schedule snapshot (keys, enemy ids, intervals of V3's encounter schedule,
                // taken before the run's enemies are bound — e.g. the Drowned Harrier shows V3's "cannonboat") is empty now:
                // no reader uses it, V4 has no V3 schedule, and the V4 config set hash fingerprints the spawners. Everything
                // else in the export is equal; the content hash differs only through that section.
                Assert.IsNotEmpty(expected.ScheduleProfileKeys);
                CollectionAssert.IsEmpty(actual.ScheduleProfileKeys);
                CollectionAssert.IsEmpty(actual.ScheduleProfileSnapshots);
                expected.ScheduleProfileKeys.Clear();
                expected.ScheduleProfileSnapshots.Clear();
                expected.ContentHash = actual.ContentHash = null;
                Assert.AreEqual(JsonUtility.ToJson(expected, true), JsonUtility.ToJson(actual, true));
                // Authoring readers (V3StagePoiPlacementOptions) read the placement counts straight off the profile.
                Assert.AreEqual(legacyProfile.salvageChestCount, port.Profile.salvageChestCount);
                Assert.AreEqual(legacyProfile.explosiveBarrelCount, port.Profile.explosiveBarrelCount);
            }
            finally
            {
                port.Dispose();
                Object.DestroyImmediate(legacyProfile);
            }
        }

        sealed class RngStreams : RoguePirates.V4.Stage.IStageRandom
        {
            readonly HighSeas.Core.RunRng _rng;
            public RngStreams(HighSeas.Core.RunRng rng) { _rng = rng; }
            public float LootFloat() => _rng.LootFloat();
            public int LootInt(int a, int b) => _rng.LootInt(a, b);
            public int MapInt(int a, int b) => _rng.MapInt(a, b);
        }

        sealed class FakeRun : RoguePirates.V4.Stage.IStageRun
        {
            public int GoldValue;
            public float LuckValue;
            public bool Exists => true;
            public bool IsRunEnded => false;
            public bool IsPaused => false;
            public HighSeas.Core.PauseReason PauseReason => HighSeas.Core.PauseReason.None;
            public HighSeas.Core.RunState State => HighSeas.Core.RunState.Playing;
            public float ElapsedSeconds => 0f;
            public int Gold => GoldValue;
            public int RunSeed => 0;
            public float MaxHull => 100f;
            public float Luck => LuckValue;
            public void SetState(HighSeas.Core.RunState state) { }
            public void SetPause(HighSeas.Core.PauseReason reason) { }
            public void AddGold(int amount) { }
            public bool TrySpendGold(int amount) => true;
            public void SecureObjective() { }
            public void CompleteRun(bool victory) { }
            public void AddStatModifier(int stat, float add, float multiply) { }
        }

        /// <summary>
        /// 14.3: the V4 chest economy equals V3's (SaltwindStageEconomyLogic over the stage-profile export, V3ChestLoot,
        /// the item-offer eligibility, V3's chest pool): prices, plans and draws for the same seeds, luck, gold and owned
        /// weapons; the chest pool in V3's order; the Powder Cache's pool rolls.
        /// </summary>
        [Test]
        public void ChestEconomy_EqualsV3sChestLogic()
        {
            using var set = Default();
            var port = new LegacyStageProfilePort(set);
            try
            {
                Assert.IsTrue(port.TryBegin(out var error), error);
                var export = StageBalanceProfileContext.Export;
                var v4Pool = set.All<ItemConfig>().Where(i => i.inChestPool).OrderBy(i => i.chestPoolOrder).Select(i => i.itemId + ":" + i.rarity);
                CollectionAssert.AreEqual(HighSeas.V3.V3ItemCatalog.ChestPool.Select(e => e.id + ":" + (int)e.rarity), v4Pool, "V3's chest pool, in order");
                CollectionAssert.AreEqual(set.Single<StageContentConfig>().sharedProfile.starterWeaponIds, set.Single<UnlockConfig>().starterWeaponIds,
                    "the unlock config's starter pool is V3's (the item eligibility reads it)");

                var owned = new[] { null, new[] { "flamethrower" }, new[] { "lightning_mast", "harpoon_gun" } };
                var plans = 0;
                foreach (var seed in new[] { 3, 101, 202, 9001 })
                foreach (var luck in new[] { 0f, 35f, 250f })
                foreach (var ownedList in owned)
                {
                    var v3Rng = new HighSeas.Core.RunRng(seed);
                    var v4Rng = new HighSeas.Core.RunRng(seed);
                    var run = new FakeRun { LuckValue = luck };
                    var v4 = new RoguePirates.V4.Stage.ChestEconomy(() => set, run, new RngStreams(v4Rng), () => null, null);
                    for (var purchase = 0; purchase < 12; purchase++)
                    {
                        run.GoldValue = 15 + purchase * 17; // sometimes short of the price
                        Assert.AreEqual(HighSeas.Balance.SaltwindStageEconomyLogic.QuoteChestPrice(export, purchase), v4.QuoteChestPrice(), $"price {purchase}");
                        var a = HighSeas.Balance.SaltwindStageEconomyLogic.TryPlanChestPurchase(export, purchase, run.GoldValue, v3Rng, luck, null, ownedList);
                        var b = v4.PlanChest(ownedList);
                        Assert.AreEqual(a.IsValid, b.Valid, $"seed {seed} luck {luck} purchase {purchase}");
                        Assert.AreEqual(a.IsValid ? a.Price : 0, b.Valid ? b.Price : 0);
                        Assert.AreEqual(a.IsValid ? a.ItemId : null, b.Valid ? b.ItemId : null, $"seed {seed} luck {luck} purchase {purchase} item");
                        v4.RegisterChestPurchase();
                        plans++;
                    }
                    // Powder Cache rolls (V3: the chest pool, uniform on the map stream).
                    var pool = HighSeas.V3.V3ItemCatalog.ChestPool;
                    Assert.AreEqual(pool[v3Rng.MapInt(0, pool.Count)].id, v4.RollChestPoolItem());
                    var rare = pool.Where(e => (int)e.rarity >= 2).ToList();
                    Assert.AreEqual(rare[v3Rng.MapInt(0, rare.Count)].id, v4.RollChestPoolItem(2));
                }
                Assert.AreEqual(4 * 3 * 3 * 12, plans);
            }
            finally
            {
                port.EndIfActive();
                port.Dispose();
                StageBalanceProfileContext.ResetForTests();
            }
        }

        /// <summary>14.6: the V4 run telemetry exports byte for byte what V3's RunCombatTelemetry + WeaponDamageLedger exported
        /// for the same calls (gold now read from the run's gross counters, which V3's AddGold / TrySpendGold kept equal).</summary>
        [Test]
        public void RunTelemetry_ExportsWhatV3Exported()
        {
            var profile = HighSeas.Combat.CombatExperimentProfile.CreateBuiltIn();
            HighSeas.Combat.WeaponDamageLedger.Reset();
            var v3 = new HighSeas.Combat.RunCombatTelemetry(profile);
            var goldEarned = 0;
            var goldSpent = 0;
            var v4 = new RoguePirates.V4.Telemetry.RunTelemetry(profile.profileId, profile.weaponSlotCap, profile.telemetryEnabled, null, null)
            {
                GoldEarned = () => goldEarned,
                GoldSpent = () => goldSpent,
            };
            try
            {
                void Both(System.Action<HighSeas.Combat.RunCombatTelemetry> a3, System.Action<RoguePirates.V4.Telemetry.RunTelemetry> a4) { a3(v3); a4(v4); }
                Both(t => t.RecordStageProfileStart("saltwind_solo", "3", "abc", "run-1"), t => t.RecordStageProfileStart("saltwind_solo", "3", "abc", "run-1"));
                for (var i = 0; i < 40; i++)
                {
                    var pos = new Vector3(i * 1.5f, 0f, Mathf.Sin(i) * 3f);
                    var heading = i * 7f % 360f;
                    Both(t => t.TickMovement(pos, heading, 1f / 60f), t => t.TickMovement(pos, heading, 1f / 60f));
                    Both(t => t.SamplePerformance(i % 13, i * 2), t => t.SamplePerformance(i % 13, i * 2));
                    if (i % 5 == 0) Both(t => t.RecordDamageTaken(3.5f + i), t => t.RecordDamageTaken(3.5f + i));
                    if (i % 7 == 0) Both(t => t.RecordWeaponCount(1 + i / 7, i, i * 12.5f), t => t.RecordWeaponCount(1 + i / 7, i, i * 12.5f));
                    var weapon = i % 3 == 0 ? "deck_cannons" : "swivel_guns";
                    HighSeas.Combat.WeaponDamageLedger.RecordHit(weapon, 4.25f + i, i % 4 == 0, i % 6 == 0, false);
                    v4.RecordHit(weapon, 4.25f + i, i % 4 == 0, i % 6 == 0, false);
                    if (i % 4 == 0) { HighSeas.Combat.WeaponDamageLedger.RecordKill(weapon); v4.RecordKill(weapon); }
                    if (i % 9 == 0) { v3.RecordGoldEarned(12); goldEarned += 12; }
                    if (i % 11 == 0) { v3.RecordGoldSpent(20); goldSpent += 20; }
                }
                Both(t => t.RecordNewWeaponOfferShown(), t => t.RecordNewWeaponOfferShown());
                Both(t => t.RecordNewWeaponOfferAccepted(), t => t.RecordNewWeaponOfferAccepted());
                Both(t => t.RecordItemAcquired("lucky_doubloon"), t => t.RecordItemAcquired("lucky_doubloon"));
                Both(t => t.RecordOfferChoice("weapon", "mortar"), t => t.RecordOfferChoice("weapon", "mortar"));
                Both(t => t.RecordPoiInteraction("SignalBuoy"), t => t.RecordPoiInteraction("SignalBuoy"));
                Both(t => t.RecordBossSummoned(1), t => t.RecordBossSummoned(1));
                Both(t => t.RecordBossDefeated(1), t => t.RecordBossDefeated(1));
                Both(t => t.RecordMarksSettlement(42, true, 42, 12, 3, 5, 25, 0), t => t.RecordMarksSettlement(42, true, 42, 12, 3, 5, 25, 0));
                Both(t => t.RecordPlayerDeath(true), t => t.RecordPlayerDeath(true));
                Both(t => t.RecordRunResultsShown(true, 42), t => t.RecordRunResultsShown(true, 42));
                Both(t => t.Record("captain_active_used", new Dictionary<string, object> { ["captain"] = "Rook" }),
                    t => t.Record("captain_active_used", new Dictionary<string, object> { ["captain"] = "Rook" }));
                Assert.AreEqual(v3.ExportJsonSummary(), v4.ExportJsonSummary());
            }
            finally
            {
                HighSeas.Combat.WeaponDamageLedger.Reset();
                Object.DestroyImmediate(profile);
            }
        }

        /// <summary>14.8 (intended change): pickup draws come from a seed-derived stream that Unity's global random state —
        /// which damage numbers, camera shake and other VFX also consume — can no longer move.</summary>
        [Test]
        public void PickupStream_IsSeeded_AndIndependentOfUnitysRandom()
        {
            var a = new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5);
            var b = new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5);
            for (var i = 0; i < 200; i++)
            {
                Random.InitState(i * 31); // presentation drawing from the global state in between
                _ = Random.value;
                Assert.AreEqual(a.Range(-18f, 18f), b.Range(-18f, 18f));
                Assert.AreEqual(a.Range(6, 11), b.Range(6, 11));
                Assert.AreEqual(a.InsideUnitSphere(), b.InsideUnitSphere());
            }
            var c = new RoguePirates.V4.Progression.SeededPickupRandom(202 ^ 0x71C0B5);
            Assert.AreNotEqual(new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5).Range(0f, 1f), c.Range(0f, 1f), "the run seed decides the stream");
        }

        /// <summary>14.4: the adaptive-elite copy is one reused object per profile, current at each call (no per-spawn create).</summary>
        [Test]
        public void EliteCopy_IsReused_AndCurrent()
        {
            using var set = Default();
            var v4 = new SpawnerProfiles(set);
            try
            {
                v4.Build();
                var spawner = set.All<EnemySpawnerConfig>().First(s => s.legacyProfileId.Contains("ambient"));
                var source = v4.For(spawner);
                var d = set.Single<DirectorConfig>();
                var a = v4.EliteCopy(source, d);
                Assert.IsTrue(a.elite.isElite);
                Assert.AreEqual(Mathf.Max(0.1f, d.eliteHpMultiplier), a.elite.hpMultiplier);
                Assert.AreSame(source.movement, a.movement);
                Assert.AreSame(source.enemy, a.enemy);
                Assert.IsFalse(source.elite.isElite, "the source stays as it was");
                d.eliteHpMultiplier = 2.25f;
                var b = v4.EliteCopy(source, d);
                Assert.AreSame(a, b, "one reusable copy");
                Assert.AreEqual(2.25f, b.elite.hpMultiplier, "current at each call");
            }
            finally { v4.Dispose(); }
        }
    }
}
