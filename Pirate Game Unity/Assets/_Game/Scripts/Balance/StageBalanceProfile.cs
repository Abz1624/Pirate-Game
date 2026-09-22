using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Balance
{
    [CreateAssetMenu(fileName = "StageBalanceProfile", menuName = "HighSeas/Balance/Stage Balance Profile")]
    public sealed class StageBalanceProfile : ScriptableObject
    {
        public const string DefaultSaltwindSoloId = "saltwind_solo";

        [Header("Identity")]
        public string profileId = DefaultSaltwindSoloId;
        public int schemaVersion = 1;
        public int balanceVersion = 1;

        [Header("References")]
        public EncounterSpawnSchedule encounterSchedule;
        [Tooltip("When enabled, missing boss/cache/swarm/pursuer schedule profiles block run start (C04).")]
        public bool enforceCompleteScheduleReferences;

        [Header("Timing")]
        public float graceSeconds = 2f;
        public float stageDeadlineSeconds = 600f;
        public float beaconDirectionHintSeconds = 300f;
        public float beaconExactMarkerSeconds = 420f;
        public float stormStartSeconds = 720f;
        public float stormWarningLeadSeconds = 10f;

        [Header("Spawn")]
        public float[] ambientRatePerSecond = { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f };
        public float ambientBandDurationSeconds = 120f;
        public int maxActiveEnemies = 80;
        public int reservedEncounterSlots = 8;
        public float bossAmbientRateMultiplier = 0.5f;
        public float scheduledEliteSecondsA = 180f;
        public float scheduledEliteSecondsB = 480f;
        public float swarmStartSecondsA = 240f;
        public float swarmStartSecondsB = 420f;
        public float swarmDurationSeconds = 30f;
        public float eventWarningSeconds = 5f;
        public int maxSimultaneousCannonboats = 4;
        public int maxSimultaneousAreaDenial = 2;
        public int maxSimultaneousChargeWindups = 2;
        public float minChargeWarningSeconds = 1f;
        public float minExplosiveWarningSeconds = 1.2f;
        public float spawnRetryIntervalSeconds = 0.35f;
        public float requiredEncounterTimeoutSeconds = 10f;
        public float swarmIntensityMultiplier = 1.25f;

        [Header("Rewards")]
        [Range(0f, 1f)] public float ordinaryEnemyGoldChance = 0.5f;
        public int ordinaryEnemyGoldAmount = 1;
        public int specialistEnemyGoldAmount = 2;
        public int scheduledEliteGoldAmount = 25;
        [Range(0f, 1f)] public float randomPowerupChancePerKill = 0.01f;
        public float randomPowerupMinIntervalSeconds = 20f;
        public float repairSuppliesMaxHullFraction = 0.2f;
        public float powderRushAttackSpeedBonus = 0.3f;
        public float powderRushDurationSeconds = 8f;
        public float deadCalmSlowFraction = 0.3f;
        public float deadCalmDurationSeconds = 5f;
        public float repairWreckMaxHullFraction = 0.25f;
        public int powderCacheBonusGold = 20;
        public float signalBuoyOccupancySeconds = 8f;

        [Header("Exploration")]
        public int salvageChestCount = 12;
        public bool disableGoldTriggeredChestSpawn = true;
        public bool disableAutomaticRouteEvents = true;
        public float explosiveBarrelFuseSeconds = 1.2f;
        public float explosiveBarrelRadius = 4f;
        public int explosiveBarrelDamage = 20;
        public int explosiveBarrelCount = 2;

        [Header("Progression")]
        public int weaponSlotCap = 6;
        public int logbookSlotCap = 2;
        public int levelUpChoiceCount = 3;
        public int levelZeroToOneXpCost = 7;
        public double xpCurveMultiplier = 5d * 1.065000057220459d;
        public double xpCurveExponent = 1.065000057220459d;
        public int[] newWeaponOfferProtectionLevels = { 4, 8, 12, 16 };

        [Header("Economy")]
        public int chestBasePrice = 20;
        public float chestPriceGrowth = 1.22f;
        public int chestPriceIncrementBase = 25;
        public int marksPerQualifyingMinute = 2;
        public int marksPerQualifyingMinuteMinKills = 10;
        public int marksPerSpecialPoi = 3;
        public int marksPerScheduledElite = 5;
        public int marksBossVictory = 25;
        public int marksPerOvertimeMinute = 3;
        public int marksOvertimeCap = 15;
        public bool runGoldExpiresAtRunEnd = true;
        public bool bankRunGoldToDoubloons;

        [Header("Overtime")]
        public float overtimeInitialSpawnIntervalSeconds = 1.4f;
        public float overtimeIntervalFloorSeconds = 0.35f;
        public float overtimeIntervalDecayPerStep = 0.85f;
        public float overtimePressureStepSeconds = 30f;
        public float overtimeHpMultiplierBase = 2f;
        public float overtimeHpMultiplierExponentPerMinute = 0.5f;
        public float overtimeDamageBonusPerMinute = 0.12f;
        public float pursuerWeightFirstTwoMinutes = 0.8f;
        public float harrierWeightFirstTwoMinutes = 0.2f;
        public float pursuerWeightAfterTwoMinutes = 0.6f;
        public float harrierWeightAfterTwoMinutes = 0.4f;
        public float stormSafeRegionStartFraction = 1f;
        public float stormSafeRegionEndFraction = 0.4f;
        public float stormContractionStartSeconds = 720f;
        public float stormContractionEndSeconds = 900f;
        public float stormOutsideDamagePerSecond = 8f;

        [Header("Content IDs")]
        public string[] starterWeaponIds =
        {
            "deck_cannons",
            "broadside_battery",
            "powder_kegs",
            "flying_cutlasses",
            "long_nine",
            "swivel_guns"
        };

        public StageUnlockEntry[] weaponUnlocks =
        {
            new() { contentId = "lightning_mast", marksCost = 60 },
            new() { contentId = "flamethrower", marksCost = 80 },
            new() { contentId = "mortar", marksCost = 100 },
            new() { contentId = "harpoon_gun", marksCost = 100 }
        };

        public StageUnlockEntry[] captainUnlocks =
        {
            new() { contentId = "brine", marksCost = 100 },
            new() { contentId = "flint", marksCost = 140 }
        };

        public StageBalanceProfileExport ToExport()
        {
            var export = new StageBalanceProfileExport
            {
                ProfileId = profileId,
                SchemaVersion = schemaVersion,
                BalanceVersion = balanceVersion,
                Timing = new StageTimingExport
                {
                    GraceSeconds = graceSeconds,
                    StageDeadlineSeconds = stageDeadlineSeconds,
                    BeaconDirectionHintSeconds = beaconDirectionHintSeconds,
                    BeaconExactMarkerSeconds = beaconExactMarkerSeconds,
                    StormStartSeconds = stormStartSeconds,
                    StormWarningLeadSeconds = stormWarningLeadSeconds
                },
                Spawn = new StageSpawnExport
                {
                    AmbientRatePerSecond = ambientRatePerSecond != null && ambientRatePerSecond.Length > 0
                        ? (float[])ambientRatePerSecond.Clone()
                        : new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f },
                    AmbientBandDurationSeconds = ambientBandDurationSeconds,
                    MaxActiveEnemies = maxActiveEnemies,
                    ReservedEncounterSlots = reservedEncounterSlots,
                    BossAmbientRateMultiplier = bossAmbientRateMultiplier,
                    ScheduledEliteSecondsA = scheduledEliteSecondsA,
                    ScheduledEliteSecondsB = scheduledEliteSecondsB,
                    SwarmStartSecondsA = swarmStartSecondsA,
                    SwarmStartSecondsB = swarmStartSecondsB,
                    SwarmDurationSeconds = swarmDurationSeconds,
                    EventWarningSeconds = eventWarningSeconds,
                    MaxSimultaneousCannonboats = maxSimultaneousCannonboats,
                    MaxSimultaneousAreaDenial = maxSimultaneousAreaDenial,
                    MaxSimultaneousChargeWindups = maxSimultaneousChargeWindups,
                    MinChargeWarningSeconds = minChargeWarningSeconds,
                    MinExplosiveWarningSeconds = minExplosiveWarningSeconds,
                    SpawnRetryIntervalSeconds = spawnRetryIntervalSeconds,
                    RequiredEncounterTimeoutSeconds = requiredEncounterTimeoutSeconds,
                    SwarmIntensityMultiplier = swarmIntensityMultiplier
                },
                Rewards = new StageRewardExport
                {
                    OrdinaryEnemyGoldChance = ordinaryEnemyGoldChance,
                    OrdinaryEnemyGoldAmount = ordinaryEnemyGoldAmount,
                    SpecialistEnemyGoldAmount = specialistEnemyGoldAmount,
                    ScheduledEliteGoldAmount = scheduledEliteGoldAmount,
                    RandomPowerupChancePerKill = randomPowerupChancePerKill,
                    RandomPowerupMinIntervalSeconds = randomPowerupMinIntervalSeconds,
                    RepairSuppliesMaxHullFraction = repairSuppliesMaxHullFraction,
                    PowderRushAttackSpeedBonus = powderRushAttackSpeedBonus,
                    PowderRushDurationSeconds = powderRushDurationSeconds,
                    DeadCalmSlowFraction = deadCalmSlowFraction,
                    DeadCalmDurationSeconds = deadCalmDurationSeconds,
                    RepairWreckMaxHullFraction = repairWreckMaxHullFraction,
                    PowderCacheBonusGold = powderCacheBonusGold,
                    SignalBuoyOccupancySeconds = signalBuoyOccupancySeconds
                },
                Exploration = new StageExplorationExport
                {
                    SalvageChestCount = salvageChestCount,
                    DisableGoldTriggeredChestSpawn = disableGoldTriggeredChestSpawn,
                    DisableAutomaticRouteEvents = disableAutomaticRouteEvents,
                    ExplosiveBarrelFuseSeconds = explosiveBarrelFuseSeconds,
                    ExplosiveBarrelRadius = explosiveBarrelRadius,
                    ExplosiveBarrelDamage = explosiveBarrelDamage,
                    ExplosiveBarrelCount = explosiveBarrelCount
                },
                Progression = new StageProgressionExport
                {
                    WeaponSlotCap = weaponSlotCap,
                    LogbookSlotCap = logbookSlotCap,
                    LevelUpChoiceCount = levelUpChoiceCount,
                    LevelZeroToOneXpCost = levelZeroToOneXpCost,
                    XpCurveMultiplier = xpCurveMultiplier,
                    XpCurveExponent = xpCurveExponent,
                    NewWeaponOfferProtectionLevels = newWeaponOfferProtectionLevels != null
                        ? (int[])newWeaponOfferProtectionLevels.Clone()
                        : new[] { 4, 8, 12, 16 }
                },
                Economy = new StageEconomyExport
                {
                    ChestBasePrice = chestBasePrice,
                    ChestPriceGrowth = chestPriceGrowth,
                    ChestPriceIncrementBase = chestPriceIncrementBase,
                    MarksPerQualifyingMinute = marksPerQualifyingMinute,
                    MarksPerQualifyingMinuteMinKills = marksPerQualifyingMinuteMinKills,
                    MarksPerSpecialPoi = marksPerSpecialPoi,
                    MarksPerScheduledElite = marksPerScheduledElite,
                    MarksBossVictory = marksBossVictory,
                    MarksPerOvertimeMinute = marksPerOvertimeMinute,
                    MarksOvertimeCap = marksOvertimeCap,
                    RunGoldExpiresAtRunEnd = runGoldExpiresAtRunEnd,
                    BankRunGoldToDoubloons = bankRunGoldToDoubloons
                },
                Overtime = new StageOvertimeExport
                {
                    InitialSpawnIntervalSeconds = overtimeInitialSpawnIntervalSeconds,
                    IntervalFloorSeconds = overtimeIntervalFloorSeconds,
                    IntervalDecayPerStep = overtimeIntervalDecayPerStep,
                    PressureStepSeconds = overtimePressureStepSeconds,
                    HpMultiplierBase = overtimeHpMultiplierBase,
                    HpMultiplierExponentPerMinute = overtimeHpMultiplierExponentPerMinute,
                    DamageBonusPerMinute = overtimeDamageBonusPerMinute,
                    PursuerWeightFirstTwoMinutes = pursuerWeightFirstTwoMinutes,
                    HarrierWeightFirstTwoMinutes = harrierWeightFirstTwoMinutes,
                    PursuerWeightAfterTwoMinutes = pursuerWeightAfterTwoMinutes,
                    HarrierWeightAfterTwoMinutes = harrierWeightAfterTwoMinutes,
                    StormSafeRegionStartFraction = stormSafeRegionStartFraction,
                    StormSafeRegionEndFraction = stormSafeRegionEndFraction,
                    StormContractionStartSeconds = stormContractionStartSeconds,
                    StormContractionEndSeconds = stormContractionEndSeconds,
                    StormOutsideDamagePerSecond = stormOutsideDamagePerSecond
                },
                StarterWeaponIds = new List<string>(starterWeaponIds ?? System.Array.Empty<string>()),
                WeaponUnlocks = MapUnlocks(weaponUnlocks),
                CaptainUnlocks = MapUnlocks(captainUnlocks),
                ScheduleProfileKeys = CollectScheduleProfileKeys()
            };

            return StageBalanceProfileExportCodec.WithContentHash(export);
        }

        static List<StageUnlockExport> MapUnlocks(StageUnlockEntry[] entries)
        {
            var list = new List<StageUnlockExport>();
            if (entries == null) return list;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || string.IsNullOrWhiteSpace(entries[i].contentId)) continue;
                list.Add(new StageUnlockExport
                {
                    ContentId = entries[i].contentId,
                    MarksCost = entries[i].marksCost
                });
            }
            return list;
        }

        List<string> CollectScheduleProfileKeys()
        {
            var keys = new List<string>();
            if (encounterSchedule == null) return keys;
            AddProfileKey(keys, encounterSchedule.hammerheadBeat);
            AddProfileKey(keys, encounterSchedule.krakenBeat);
            AddProfileKey(keys, encounterSchedule.swarmPulse);
            AddProfileKey(keys, encounterSchedule.drownedPursuer);
            AddProfileKey(keys, encounterSchedule.drownedHarrier);
            AddProfileKey(keys, encounterSchedule.powderCacheAmbush);
            AddProfileKey(keys, encounterSchedule.bossSummon);
            if (encounterSchedule.entries == null) return keys;
            for (var i = 0; i < encounterSchedule.entries.Length; i++)
                AddProfileKey(keys, encounterSchedule.entries[i]?.profile);
            return keys;
        }

        static void AddProfileKey(List<string> keys, SpawnProfile profile)
        {
            if (profile == null) return;
            var key = profile.ResolvedId;
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!keys.Contains(key)) keys.Add(key);
        }

        public static StageBalanceProfile CreateBuiltInSaltwindDefaults()
        {
            var profile = CreateInstance<StageBalanceProfile>();
            profile.name = "BuiltInSaltwindSolo";
            return profile;
        }
    }

    [System.Serializable]
    public sealed class StageUnlockEntry
    {
        public string contentId;
        public int marksCost;
    }
}
