using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.V3;
using HighSeas.World;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Tests
{
    sealed class InMemorySaveBackend : ISaveBackend
    {
        readonly System.Collections.Generic.Dictionary<string, string> _store = new();

        public bool TryLoad(string key, out string json)
        {
            if (_store.TryGetValue(key, out json)) return true;
            json = null;
            return false;
        }

        public void Save(string key, string json) => _store[key] = json;

        public void Delete(string key) => _store.Remove(key);
    }

    public sealed class SaltwindSettlementGateTests
    {
        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            SaltwindRunSettlement.ClearLastResults();
        }

        [Test]
        public void SaveMigration_V1_PreservesDoubloons_AndInitializesMarks()
        {
            var legacy = new MetaSaveData
            {
                version = 1,
                doubloons = 77,
                unlockedIds = new[] { "deck_cannons" }
            };
            var migrated = SaveService.Migrate(legacy);
            Assert.AreEqual(2, migrated.version);
            Assert.AreEqual(77, migrated.doubloons);
            Assert.AreEqual(0, migrated.marks);
            Assert.IsNotNull(migrated.marksOwnedContentIds);
            Assert.AreEqual(0, migrated.marksOwnedContentIds.Length);
        }

        [Test]
        public void ApprovedFixture_TotalMarks_Is52()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var record = SaltwindStageSettlementLogic.BuildFixtureRecord();
            Assert.AreEqual(SaltwindStageSettlementLogic.ApprovedFixtureTotalMarks,
                SaltwindStageSettlementLogic.ComputeTotalMarks(export, record));
        }

        [Test]
        public void TrySettleRun_IsIdempotentPerRunId()
        {
            var save = new SaveService(new InMemorySaveBackend());
            Assert.IsTrue(save.TrySettleRun("run-a", 12));
            Assert.AreEqual(12, save.Meta.marks);
            Assert.IsFalse(save.TrySettleRun("run-a", 99));
            Assert.AreEqual(12, save.Meta.marks);
        }

        [Test]
        public void TryPurchaseMarksUnlock_IsIdempotentWhenOwned()
        {
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.marks = 100;
            Assert.IsTrue(save.TryPurchaseMarksUnlock("mortar", 100));
            Assert.IsTrue(save.HasMarksOwned("mortar"));
            Assert.AreEqual(0, save.Meta.marks);
            Assert.IsFalse(save.TryPurchaseMarksUnlock("mortar", 100));
        }

        [Test]
        public void QualifyingMinute_RequiresMinKills()
        {
            Assert.IsFalse(SaltwindStageSettlementLogic.QualifiesOrdinaryMinute(9, 10));
            Assert.IsTrue(SaltwindStageSettlementLogic.QualifiesOrdinaryMinute(10, 10));
        }

        [Test]
        public void WeaponUnlockCost_ReadsProfileExport()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(80, SaltwindStageSettlementLogic.TryGetWeaponUnlockCost(export, "flamethrower"));
            Assert.AreEqual(-1, SaltwindStageSettlementLogic.TryGetWeaponUnlockCost(export, "missing"));
        }
    }

    public sealed class SaltwindEconomyGateTests
    {
        [TearDown]
        public void TearDown() => StageBalanceProfileContext.ResetForTests();

        [Test]
        public void ChestPrices_MatchApprovedFixtures()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(20, SaltwindStageEconomyLogic.QuoteChestPrice(export, 0));
            Assert.AreEqual(31, SaltwindStageEconomyLogic.QuoteChestPrice(export, 1));
            Assert.AreEqual(38, SaltwindStageEconomyLogic.QuoteChestPrice(export, 2));
            Assert.AreEqual(46, SaltwindStageEconomyLogic.QuoteChestPrice(export, 3));
            Assert.AreEqual(56, SaltwindStageEconomyLogic.QuoteChestPrice(export, 4));
        }

        [Test]
        public void ChestPurchasePlan_RepeatedQuotes_DoNotAdvancePrice()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var rng = new RunRng(42);
            var first = SaltwindStageEconomyLogic.TryPlanChestPurchase(export, 0, 100, rng, 0f);
            var second = SaltwindStageEconomyLogic.TryPlanChestPurchase(export, 0, 100, new RunRng(99), 0f);
            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.AreEqual(first.Price, second.Price);
            Assert.AreEqual(20, first.Price);
        }

        [Test]
        public void ChestPurchasePlan_InsufficientGold_DoesNotGrant()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var plan = SaltwindStageEconomyLogic.TryPlanChestPurchase(export, 0, 5, new RunRng(1), 0f);
            Assert.IsFalse(plan.IsValid);
            Assert.AreEqual("insufficient gold", plan.Error);
        }

        [Test]
        public void PickupMerge_ConservesTotals()
        {
            var merged = SaltwindStageEconomyLogic.MergePickupValues(3, 5);
            Assert.AreEqual(8, merged);
            Assert.AreEqual(8, SaltwindStageEconomyLogic.SumPickupValues(new[] { 3, 5 }));
        }

        [Test]
        public void ProfileGold_SkipsHiddenGoldGainStat()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(7, SaltwindStageEconomyLogic.ApplyGoldGrant(7, export, 2f));
            Assert.AreEqual(14, SaltwindStageEconomyLogic.ApplyGoldGrant(7, null, 2f));
        }

        [Test]
        public void SaltwindPowerupPool_UsesFourApprovedKinds()
        {
            var rng = new RunRng(808);
            for (var i = 0; i < 40; i++)
            {
                var kind = V3PowerupCatalog.RollSaltwindKind(new RunRng(i + 1));
                Assert.IsTrue(kind is V3FloorPowerupKind.RepairSupplies
                    or V3FloorPowerupKind.PowderRush
                    or V3FloorPowerupKind.MagneticTide
                    or V3FloorPowerupKind.DeadCalm);
            }
        }
    }

    public sealed class SaltwindProgressionGateTests
    {
        [TearDown]
        public void TearDown() => StageBalanceProfileContext.ResetForTests();

        [Test]
        public void XpCurve_MatchesCodecFixtures()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(7, SaltwindStageProgressionLogic.XpToNext(export, 0));
            for (var level = 1; level <= 12; level++)
            {
                Assert.AreEqual(
                    StageBalanceProfileExportCodec.XpToNextLevel(export, level),
                    SaltwindStageProgressionLogic.XpToNext(export, level),
                    $"level {level}");
            }
        }

        [Test]
        public void MultiLevelXpGrant_IndexesFromLevelZero()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var first = StageBalanceProfileExportCodec.XpToNextLevel(export, 0);
            var second = StageBalanceProfileExportCodec.XpToNextLevel(export, 1);
            var advance = SaltwindStageProgressionLogic.ApplyXpGrant(export, 0, 0f, first);
            Assert.AreEqual(1, advance.Level);
            Assert.AreEqual(0f, advance.CurrentXp, 0.001f);

            advance = SaltwindStageProgressionLogic.ApplyXpGrant(export, 0, 0f, first + second);
            Assert.AreEqual(2, advance.Level);
            Assert.AreEqual(0f, advance.CurrentXp, 0.001f);
        }

        [Test]
        public void StarterPool_IncludesSixWeapons_ExcludesMarksUnlocks()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.IsTrue(SaltwindStageProgressionLogic.IsWeaponInOfferPool(export, "deck_cannons"));
            Assert.IsTrue(SaltwindStageProgressionLogic.IsWeaponInOfferPool(export, "swivel_guns"));
            Assert.IsFalse(SaltwindStageProgressionLogic.IsWeaponInOfferPool(export, "lightning_mast"));
            Assert.IsFalse(SaltwindStageProgressionLogic.IsWeaponInOfferPool(export, "mortar"));
        }

        [Test]
        public void EvolutionIngredients_RequireBaseWeaponInPool()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.IsTrue(SaltwindStageProgressionLogic.IsRunItemOfferEligible(export, "grapeshot_pouch"));
            Assert.IsFalse(SaltwindStageProgressionLogic.IsRunItemOfferEligible(export, "copper_conductor"));
        }

        [Test]
        public void NewWeaponPityThresholds_MatchProfileLevels()
        {
            var levels = new[] { 4, 8, 12, 16 };
            Assert.AreEqual(0, SaltwindStageProgressionLogic.GetNewWeaponPityTargetCount(3, levels));
            Assert.AreEqual(3, SaltwindStageProgressionLogic.GetNewWeaponPityTargetCount(4, levels));
            Assert.AreEqual(6, SaltwindStageProgressionLogic.GetNewWeaponPityTargetCount(16, levels));
        }

        [Test]
        public void ProcCascade_StopsAtConfiguredCap()
        {
            Assert.AreEqual(12, SaltwindStageProgressionLogic.SimulateBoundedProcCascade(3, 12));
            Assert.AreEqual(1, SaltwindStageProgressionLogic.SimulateBoundedProcCascade(3, 1));
            Assert.AreEqual(
                DamageProcPolicy.MaxProcEventsPerResolution,
                SaltwindStageProgressionLogic.SimulateBoundedProcCascade(
                    3,
                    DamageProcPolicy.MaxProcEventsPerResolution));
        }
    }

    public sealed class SaltwindPoiLayoutGateTests
    {
        static void PlaceSaltwindLayout(int seed, IslandLayoutDefinition layoutDef, out StageLayout layout)
        {
            IslandSpawnExclusionRegistry.Clear();
            IslandLayoutSpawner.RegisterPredictedZones(layoutDef, seed);
            PlayBounds.ConfigureFromExclusionRegistry();

            layout = new StageLayout { seed = seed };
            V3SpawnAnchorValidator.TryResolveAnchors(layout, layoutDef, new RunRng(seed));
            V3StagePoiPlacer.Place(
                layout,
                new RunRng(seed + 404),
                layoutDef,
                V3StagePoiPlacementOptions.SaltwindSolo());
        }

        [Test]
        public void SaltwindLayout_100Seeds_PassValidation()
        {
            var layoutDef = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            if (layoutDef == null)
                Assert.Ignore("V3SaltwindIslandLayout missing from Resources.");

            var options = V3StagePoiPlacementOptions.SaltwindSolo();
            for (var seed = 1; seed <= 100; seed++)
            {
                PlaceSaltwindLayout(seed, layoutDef, out var layout);
                var report = SaltwindPoiLayoutLogic.Validate(layout, options, layoutDef);
                Assert.IsTrue(report.IsValid, $"seed={seed}: {report.Error}");
            }

            IslandSpawnExclusionRegistry.Clear();
        }

        [Test]
        public void ExplosiveBarrelBlast_OneDamagePerActor()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(10f, 0f, 0f)
            };
            var already = new HashSet<int>();
            var first = SaltwindExplosiveBarrelLogic.ApplyBlastDamage(positions, Vector3.zero, 4f, already);
            Assert.IsTrue(first.Contains(0));
            Assert.IsTrue(first.Contains(1));
            Assert.IsFalse(first.Contains(2));

            already.UnionWith(first);
            var second = SaltwindExplosiveBarrelLogic.ApplyBlastDamage(positions, Vector3.zero, 4f, already);
            Assert.IsEmpty(second);
        }

        [Test]
        public void ProfileExport_BeaconHints_AtFiveAndSevenMinutes()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(300f, export.Timing.BeaconDirectionHintSeconds, 0.001f);
            Assert.AreEqual(420f, export.Timing.BeaconExactMarkerSeconds, 0.001f);
            Assert.IsTrue(export.Exploration.DisableGoldTriggeredChestSpawn);
            Assert.AreEqual(12, export.Exploration.SalvageChestCount);
        }
    }

    public sealed class SaltwindScheduleGateTests
    {
        [Test]
        public void ResourcesSchedule_HasRequiredSaltwindProfiles()
        {
            var schedule = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule");
            Assert.NotNull(schedule, "EncounterSpawnSchedule missing from Resources.");
            if (schedule.bossSummon == null)
                Assert.Ignore("Run menu HighSeas/Saltwind/Author Step 04 Spawn Schedule.");

            Assert.NotNull(schedule.powderCacheAmbush);
            Assert.NotNull(schedule.swarmPulse);
            Assert.NotNull(schedule.drownedPursuer);
            Assert.NotNull(schedule.drownedHarrier);
            Assert.NotNull(schedule.hammerheadBeat);
            Assert.NotNull(schedule.krakenBeat);
            Assert.GreaterOrEqual(schedule.maxActiveEnemies, 80);
        }

        [Test]
        public void BalanceProfile_ValidatesWithCompleteSchedule()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Editor-only asset lookup.");
#else
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(
                "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            if (profile == null || profile.encounterSchedule == null)
                Assert.Ignore("Saltwind balance profile or schedule not linked.");

            if (profile.encounterSchedule.bossSummon == null)
                Assert.Ignore("Run menu HighSeas/Saltwind/Author Step 04 Spawn Schedule.");

            profile.enforceCompleteScheduleReferences = true;
            var result = StageBalanceProfileValidator.Validate(profile);
            Assert.IsTrue(result.IsValid, result.ErrorMessage);
#endif
        }

        [Test]
        public void Timeline_AmbientRatesMatchProfileBands()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            Assert.AreEqual(0.6f, SaltwindStageTimelineLogic.GetAmbientRateAtTime(profile, 0f), 0.001f);
            Assert.AreEqual(1.2f, SaltwindStageTimelineLogic.GetAmbientRateAtTime(profile, 120f), 0.001f);
            Assert.AreEqual(3.0f, SaltwindStageTimelineLogic.GetAmbientRateAtTime(profile, 599f), 0.001f);
        }

        [Test]
        public void Timeline_AmbientStopsAtDeadlineAndDuringOvertime()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            Assert.IsTrue(SaltwindStageTimelineLogic.ShouldTickAmbientSchedule(599f, false, profile.stageDeadlineSeconds));
            Assert.IsFalse(SaltwindStageTimelineLogic.ShouldTickAmbientSchedule(600f, false, profile.stageDeadlineSeconds));
            Assert.IsFalse(SaltwindStageTimelineLogic.ShouldTickAmbientSchedule(100f, true, profile.stageDeadlineSeconds));
        }

        [Test]
        public void Timeline_EliteAndSwarmCheckpoints()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            Assert.IsTrue(SaltwindStageTimelineLogic.IsScheduledEliteMinute(180f, profile, 0));
            Assert.IsTrue(SaltwindStageTimelineLogic.IsScheduledEliteMinute(480f, profile, 1));
            Assert.IsTrue(SaltwindStageTimelineLogic.IsSwarmPulseStart(240f, profile, 0));
            Assert.IsTrue(SaltwindStageTimelineLogic.IsSwarmPulseStart(420f, profile, 1));
        }

        [Test]
        public void PressureBeat_SkipsEliteAndSwarmDuringBossConflict()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var hammerhead = SaltwindPressureBeatLogic.GetEliteBeatSeconds(profile, 0);
            var swarmA = SaltwindPressureBeatLogic.GetSwarmStartSeconds(profile, 0);

            Assert.IsTrue(SaltwindPressureBeatLogic.ShouldSkipScheduledEventForBoss(
                hammerhead + 1f, hammerhead, bossFightActive: true));
            Assert.IsFalse(SaltwindPressureBeatLogic.ShouldStartSwarmPulse(
                swarmA + 1f, profile, 0, bossFightActive: true, alreadyStarted: false));
            Assert.AreEqual(30f, SaltwindPressureBeatLogic.GetSwarmDurationSeconds(profile), 0.001f);
            Assert.AreEqual(5f, SaltwindPressureBeatLogic.GetWarningSeconds(profile), 0.001f);
        }

        [Test]
        public void PressureBeat_WarnsFiveSecondsBeforeEvents()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var swarmA = SaltwindPressureBeatLogic.GetSwarmStartSeconds(profile, 0);
            Assert.IsTrue(SaltwindPressureBeatLogic.ShouldShowEventWarning(
                swarmA - 5f, swarmA, SaltwindPressureBeatLogic.GetWarningSeconds(profile)));
            Assert.IsFalse(SaltwindPressureBeatLogic.ShouldShowEventWarning(
                swarmA - 6f, swarmA, SaltwindPressureBeatLogic.GetWarningSeconds(profile)));
        }

        [Test]
        public void Composition_BandAnchors_AreAuthoredOnSchedule()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Editor-only asset lookup.");
#else
            var schedule = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule");
            if (schedule?.entries == null || schedule.entries.Length < 5)
                Assert.Ignore("Run menu HighSeas/Saltwind/Author Step 04 Spawn Schedule.");

            var expectedAnchors = new[] { "shark", "cannonboat", "manta_ray", "hammerhead", "manta_ray" };
            for (var i = 0; i < 5; i++)
            {
                var entry = schedule.entries[i];
                Assert.NotNull(entry?.profile, $"band {i} profile");
                Assert.AreEqual($"saltwind_ambient_band_{i}", entry.profile.ResolvedId);
                Assert.NotNull(entry.profile.enemy, $"band {i} anchor enemy");
                Assert.AreEqual(expectedAnchors[i], entry.profile.enemy.id,
                    $"band {i} anchor should match Step 04 roster (runtime composition overrides spawn id).");
            }
#endif
        }
    }

    public sealed class SaltwindFinaleGateTests
    {
        [Test]
        public void Overtime_StartsAtStageDeadline_NotBossDeath()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.IsFalse(SaltwindFinaleLogic.IsOvertime(599f, export));
            Assert.IsTrue(SaltwindFinaleLogic.IsOvertime(600.1f, export));
        }

        [Test]
        public void Storm_StartsAt720Seconds()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.IsFalse(SaltwindFinaleLogic.IsStormDamageActive(719f, export));
            Assert.IsTrue(SaltwindFinaleLogic.IsStormDamageActive(720f, export));
        }

        [Test]
        public void StormSafeRegion_ContractsBetween720And900()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.AreEqual(1f, SaltwindFinaleLogic.GetStormSafeRegionScale(700f, export), 0.001f);
            Assert.AreEqual(0.7f, SaltwindFinaleLogic.GetStormSafeRegionScale(810f, export), 0.05f);
            Assert.AreEqual(0.4f, SaltwindFinaleLogic.GetStormSafeRegionScale(900f, export), 0.001f);
        }

        [Test]
        public void BossAttacks_NoDamageDuringTelegraph()
        {
            var state = new BarnacleKingAttackState
            {
                Attack = BarnacleKingAttackKind.AimedSpread,
                Phase = BarnacleKingBossPhase.Telegraph,
                PhaseTimer = 0.5f
            };
            Assert.IsFalse(BarnacleKingBossAttackLogic.CanDealDamage(state));
        }

        [Test]
        public void BossCharge_CommitsDirectionAtTelegraphEnd()
        {
            var state = default(BarnacleKingAttackState);
            BarnacleKingBossAttackLogic.BeginRun(ref state);
            var locked = new Vector3(1f, 0f, 0f);
            state.CommittedDirection = locked;
            state.Attack = BarnacleKingAttackKind.CommittedCharge;
            state.Phase = BarnacleKingBossPhase.Telegraph;
            state.PhaseTimer = 0f;
            BarnacleKingBossAttackLogic.Tick(ref state, 0f, Vector3.zero, new Vector3(0f, 0f, 10f));
            Assert.AreEqual(BarnacleKingBossPhase.Execute, state.Phase);
            Assert.AreEqual(locked, state.CommittedDirection);
            Assert.IsTrue(BarnacleKingBossAttackLogic.IsChargeCommit(state));
        }

        [Test]
        public void EnragedPhase_ShortensRecovery()
        {
            var state = new BarnacleKingAttackState
            {
                Attack = BarnacleKingAttackKind.AimedSpread,
                Phase = BarnacleKingBossPhase.Execute,
                PhaseTimer = 0f,
                Enraged = true
            };
            BarnacleKingBossAttackLogic.Tick(ref state, 0f, Vector3.zero, Vector3.forward);
            Assert.AreEqual(BarnacleKingBossPhase.Recovery, state.Phase);
            Assert.AreEqual(
                BarnacleKingBossAttackLogic.SpreadRecoverySeconds * BarnacleKingBossAttackLogic.EnragedRecoveryScale,
                state.PhaseTimer,
                0.001f);
        }

        [Test]
        public void MarkedBombardment_NoDamageDuringTelegraph_UsesFootprintRadius()
        {
            var telegraph = new BarnacleKingAttackState
            {
                Attack = BarnacleKingAttackKind.MarkedBombardment,
                Phase = BarnacleKingBossPhase.Telegraph,
                MarkedPoint = Vector3.zero
            };
            Assert.IsFalse(BarnacleKingBossAttackLogic.ShouldApplyMarkedAreaDamage(telegraph));

            var center = new Vector3(10f, 0f, 10f);
            Assert.IsTrue(BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(
                center, center + new Vector3(BarnacleKingBossAttackLogic.MarkedBombardmentRadius - 0.5f, 0f, 0f)));
            Assert.IsFalse(BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(
                center, center + new Vector3(BarnacleKingBossAttackLogic.MarkedBombardmentRadius + 1f, 0f, 0f)));
        }
    }
}
