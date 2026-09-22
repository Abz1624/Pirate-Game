using System;
using System.Collections.Generic;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>
    /// Plain serializable stage balance contract shared by runtime and Balance Lab (no Unity types).
    /// </summary>
    [Serializable]
    public sealed class StageBalanceProfileExport
    {
        public string ProfileId = "saltwind_solo";
        public int SchemaVersion = 1;
        public int BalanceVersion = 1;
        public string ContentHash = string.Empty;

        public StageTimingExport Timing = new();
        public StageSpawnExport Spawn = new();
        public StageRewardExport Rewards = new();
        public StageExplorationExport Exploration = new();
        public StageProgressionExport Progression = new();
        public StageEconomyExport Economy = new();
        public StageOvertimeExport Overtime = new();
        public List<string> StarterWeaponIds = new();
        public List<StageUnlockExport> WeaponUnlocks = new();
        public List<StageUnlockExport> CaptainUnlocks = new();
        public List<string> ScheduleProfileKeys = new();
        public List<StageScheduleProfileSnapshot> ScheduleProfileSnapshots = new();
        public List<StageWeaponStatSnapshot> WeaponStatSnapshots = new();
        public StageBossStatSnapshot BossStatSnapshot = new();
    }

    [Serializable]
    public sealed class StageBossStatSnapshot
    {
        public string BossId = "barnacle_king";
        public float BaseHull = 800f;
        public float BaseDamage = 18f;
        public float MoveSpeed = 2.5f;
        public float AttackCooldownSeconds = 2.8f;
        public float PreferredRange = 10f;
    }

    [Serializable]
    public sealed class StageWeaponStatSnapshot
    {
        public string WeaponId = string.Empty;
        public float BaseDamage = 10f;
        public float BaseCooldown = 1.2f;
        public float Range = 14f;
        public float ProjectileSpeed = 16f;
        public int MaxLevel = 12;
        public float DamageUpgradePerLevel = 0.08f;
    }

    [Serializable]
    public sealed class StageScheduleProfileSnapshot
    {
        public string ProfileKey = string.Empty;
        public string EnemyId = string.Empty;
        public float SpawnIntervalSeconds;
        public float DurationSeconds;
        public float StartDelaySeconds;
        public float TriggerTimeSeconds;
        public int SpawnCount;
        public float EliteHpMultiplier = 1f;
        public float EliteDamageMultiplier = 1f;
    }

    [Serializable]
    public sealed class StageTimingExport
    {
        public float GraceSeconds = 2f;
        public float StageDeadlineSeconds = 600f;
        public float BeaconDirectionHintSeconds = 300f;
        public float BeaconExactMarkerSeconds = 420f;
        public float StormStartSeconds = 720f;
        public float StormWarningLeadSeconds = 10f;
    }

    [Serializable]
    public sealed class StageSpawnExport
    {
        public float[] AmbientRatePerSecond = { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f };
        public float AmbientBandDurationSeconds = 120f;
        public int MaxActiveEnemies = 80;
        public int ReservedEncounterSlots = 8;
        public float BossAmbientRateMultiplier = 0.5f;
        public float ScheduledEliteSecondsA = 180f;
        public float ScheduledEliteSecondsB = 480f;
        public float SwarmStartSecondsA = 240f;
        public float SwarmStartSecondsB = 420f;
        public float SwarmDurationSeconds = 30f;
        public float EventWarningSeconds = 5f;
        public int MaxSimultaneousCannonboats = 4;
        public int MaxSimultaneousAreaDenial = 2;
        public int MaxSimultaneousChargeWindups = 2;
        public float MinChargeWarningSeconds = 1f;
        public float MinExplosiveWarningSeconds = 1.2f;
        public float SpawnRetryIntervalSeconds = 0.35f;
        public float RequiredEncounterTimeoutSeconds = 10f;
        public float SwarmIntensityMultiplier = 1.25f;
    }

    [Serializable]
    public sealed class StageRewardExport
    {
        public float OrdinaryEnemyGoldChance = 0.5f;
        public int OrdinaryEnemyGoldAmount = 1;
        public int SpecialistEnemyGoldAmount = 2;
        public int ScheduledEliteGoldAmount = 25;
        public float RandomPowerupChancePerKill = 0.01f;
        public float RandomPowerupMinIntervalSeconds = 20f;
        public float RepairSuppliesMaxHullFraction = 0.2f;
        public float PowderRushAttackSpeedBonus = 0.3f;
        public float PowderRushDurationSeconds = 8f;
        public float DeadCalmSlowFraction = 0.3f;
        public float DeadCalmDurationSeconds = 5f;
        public float RepairWreckMaxHullFraction = 0.25f;
        public int PowderCacheBonusGold = 20;
        public float SignalBuoyOccupancySeconds = 8f;
    }

    [Serializable]
    public sealed class StageExplorationExport
    {
        public int SalvageChestCount = 12;
        public bool DisableGoldTriggeredChestSpawn = true;
        public bool DisableAutomaticRouteEvents = true;
        public float ExplosiveBarrelFuseSeconds = 1.2f;
        public float ExplosiveBarrelRadius = 4f;
        public int ExplosiveBarrelDamage = 20;
        public int ExplosiveBarrelCount = 2;
        public float FirstChestMaxTravelSeconds = 15f;
        public float RepresentativeCruiseSpeed = 5.5f;
    }

    [Serializable]
    public sealed class StageProgressionExport
    {
        public int WeaponSlotCap = 6;
        public int LogbookSlotCap = 2;
        public int LevelUpChoiceCount = 3;
        public int LevelZeroToOneXpCost = 7;
        public double XpCurveMultiplier = 5d * 1.065000057220459d;
        public double XpCurveExponent = 1.065000057220459d;
        public int[] NewWeaponOfferProtectionLevels = { 4, 8, 12, 16 };
    }

    [Serializable]
    public sealed class StageEconomyExport
    {
        public int ChestBasePrice = 20;
        public float ChestPriceGrowth = 1.22f;
        public int ChestPriceIncrementBase = 25;
        public int MarksPerQualifyingMinute = 2;
        public int MarksPerQualifyingMinuteMinKills = 10;
        public int MarksPerSpecialPoi = 3;
        public int MarksPerScheduledElite = 5;
        public int MarksBossVictory = 25;
        public int MarksPerOvertimeMinute = 3;
        public int MarksOvertimeCap = 15;
        public bool RunGoldExpiresAtRunEnd = true;
        public bool BankRunGoldToDoubloons = false;
    }

    [Serializable]
    public sealed class StageOvertimeExport
    {
        public float InitialSpawnIntervalSeconds = 1.4f;
        public float IntervalFloorSeconds = 0.35f;
        public float IntervalDecayPerStep = 0.85f;
        public float PressureStepSeconds = 30f;
        public float HpMultiplierBase = 2f;
        public float HpMultiplierExponentPerMinute = 0.5f;
        public float DamageBonusPerMinute = 0.12f;
        public float PursuerWeightFirstTwoMinutes = 0.8f;
        public float HarrierWeightFirstTwoMinutes = 0.2f;
        public float PursuerWeightAfterTwoMinutes = 0.6f;
        public float HarrierWeightAfterTwoMinutes = 0.4f;
        public float StormSafeRegionStartFraction = 1f;
        public float StormSafeRegionEndFraction = 0.4f;
        public float StormContractionStartSeconds = 720f;
        public float StormContractionEndSeconds = 900f;
        public float StormOutsideDamagePerSecond = 8f;
    }

    [Serializable]
    public sealed class StageUnlockExport
    {
        public string ContentId = string.Empty;
        public int MarksCost;
    }
}
